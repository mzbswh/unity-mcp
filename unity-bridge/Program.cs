using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

/// <summary>
/// Stdio-to-TCP bridge for MCP clients.
/// Reads JSON-RPC from stdin, forwards to Unity TCP, returns responses to stdout.
///
/// Key feature: request buffering across TCP reconnections.
/// During Unity domain reload, stdin requests are queued and replayed once TCP recovers,
/// making script recompilation nearly transparent to MCP clients.
/// </summary>
class Program
{
    const byte MsgTypeRequest = 0x01;
    const byte MsgTypeResponse = 0x02;
    const byte MsgTypeNotification = 0x03;

    // Pending frames buffered while TCP is down. Each entry is a complete frame (header + payload).
    static readonly ConcurrentQueue<byte[]> s_pendingFrames = new();
    static readonly SemaphoreSlim s_frameAvailable = new(0);

    static async Task Main(string[] args)
    {
        int port = args.Length > 0 ? int.Parse(args[0]) : DetectPort();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        // Stdin reader runs for the entire process lifetime, independent of TCP state.
        // Requests arriving while TCP is down are queued in s_pendingFrames.
        // When stdin is closed (MCP client exited/reloaded), cancel everything so the process exits.
        _ = Task.Run(async () =>
        {
            await ReadStdinLoop(cts.Token);
            if (!cts.Token.IsCancellationRequested)
            {
                Console.Error.WriteLine("[unity-mcp-bridge] Stdin closed, exiting...");
                cts.Cancel();
            }
        }, cts.Token);

        int attempt = 0;
        // Aggressive early retries for domain reload (typically 2-5s),
        // then back off for longer outages.
        int[] delays = { 0, 300, 500, 1000, 1000, 2000, 2000, 3000, 3000, 5000 };

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port);
                attempt = 0;

                int pending = s_pendingFrames.Count;
                Console.Error.WriteLine(pending > 0
                    ? $"[unity-mcp-bridge] Connected to Unity on port {port}, replaying {pending} buffered request(s)"
                    : $"[unity-mcp-bridge] Connected to Unity on port {port}");

                var stream = client.GetStream();

                // Per-connection cancellation: cancelled when either direction fails
                using var connCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);

                var sender = Task.Run(() => DrainQueueToTcp(stream, connCts), connCts.Token);
                var receiver = Task.Run(() => PipeTcpToStdout(stream, connCts.Token), connCts.Token);

                // When either task completes (due to error), cancel the other
                var completed = await Task.WhenAny(sender, receiver);
                connCts.Cancel();

                // Await both to observe exceptions (prevents unobserved task exceptions)
                try { await sender; } catch { }
                try { await receiver; } catch { }
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception) when (!cts.Token.IsCancellationRequested)
            {
                int delay = attempt < delays.Length ? delays[attempt] : delays[^1];
                attempt++;
                int jitter = (int)(delay * 0.2 * Random.Shared.NextDouble());
                Console.Error.WriteLine($"[unity-mcp-bridge] Reconnecting in {delay + jitter}ms (attempt {attempt})");
                await Task.Delay(delay + jitter, cts.Token);
            }
        }
    }

    /// <summary>
    /// Continuously reads JSON-RPC lines from stdin and enqueues them as framed messages.
    /// Runs for the entire process lifetime, decoupled from TCP connection state.
    /// </summary>
    static async Task ReadStdinLoop(CancellationToken ct)
    {
        using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
        string line;
        while ((line = await reader.ReadLineAsync()) != null && !ct.IsCancellationRequested)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var frame = BuildFrame(line);
            s_pendingFrames.Enqueue(frame);
            s_frameAvailable.Release();
        }
    }

    /// <summary>
    /// Drains the pending frame queue to TCP. Replays any buffered requests first,
    /// then continues forwarding new requests as they arrive from stdin.
    /// </summary>
    static async Task DrainQueueToTcp(NetworkStream tcp, CancellationTokenSource connCts)
    {
        var ct = connCts.Token;
        while (!ct.IsCancellationRequested)
        {
            await s_frameAvailable.WaitAsync(ct);
            if (!s_pendingFrames.TryDequeue(out var frame)) continue;

            try
            {
                await tcp.WriteAsync(frame, 0, frame.Length, ct);
                await tcp.FlushAsync(ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // TCP write failed — put the frame back for replay after reconnect
                RequeueFrame(frame);
                connCts.Cancel();
                return;
            }
        }
    }

    /// <summary>
    /// Reads framed messages from TCP and writes them to stdout.
    /// Handles server notifications (reloading, tools/list_changed, etc.).
    /// </summary>
    static async Task PipeTcpToStdout(NetworkStream tcp, CancellationToken ct)
    {
        var headerBuf = new byte[5];
        while (!ct.IsCancellationRequested)
        {
            await ReadExactAsync(tcp, headerBuf, 5, ct);
            int frameLen = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(headerBuf, 0));
            byte msgType = headerBuf[4];
            int payloadLen = frameLen - 1;
            if (payloadLen <= 0 || payloadLen > 10 * 1024 * 1024)
                throw new InvalidDataException($"Invalid payload length: {payloadLen}");

            var msgBuf = new byte[payloadLen];
            await ReadExactAsync(tcp, msgBuf, payloadLen, ct);
            var json = Encoding.UTF8.GetString(msgBuf);

            if (msgType == MsgTypeNotification)
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var method = doc.RootElement.TryGetProperty("method", out var m) ? m.GetString() ?? "" : "";

                    // Internal Unity notification — don't forward to MCP client
                    if (method == "notifications/reloading")
                    {
                        Console.Error.WriteLine("[unity-mcp-bridge] Unity reloading, buffering requests until reconnect...");
                        continue;
                    }

                    // MCP-standard notifications — forward to client via stdout
                    Console.Error.WriteLine($"[unity-mcp-bridge] Forwarding notification: {method}");
                    Console.WriteLine(json);
                    Console.Out.Flush();
                }
                catch { }
                continue;
            }

            Console.WriteLine(json);
            Console.Out.Flush();
        }
    }

    /// <summary>
    /// Builds a complete TCP frame (4-byte length + 1-byte type + payload) from a JSON-RPC line.
    /// </summary>
    static byte[] BuildFrame(string jsonLine)
    {
        var payload = Encoding.UTF8.GetBytes(jsonLine);

        byte msgType = MsgTypeRequest;
        try
        {
            using var doc = JsonDocument.Parse(jsonLine);
            if (!doc.RootElement.TryGetProperty("id", out _)) msgType = MsgTypeNotification;
        }
        catch { }

        int frameLen = 1 + payload.Length;
        var frame = new byte[5 + payload.Length];
        frame[0] = (byte)(frameLen >> 24);
        frame[1] = (byte)(frameLen >> 16);
        frame[2] = (byte)(frameLen >> 8);
        frame[3] = (byte)(frameLen);
        frame[4] = msgType;
        Buffer.BlockCopy(payload, 0, frame, 5, payload.Length);
        return frame;
    }

    /// <summary>
    /// Re-enqueues a frame that failed to send. Since ConcurrentQueue doesn't support
    /// push-to-front, we accept FIFO ordering — the failed frame goes behind any
    /// frames queued by stdin during the failure window, which is acceptable since
    /// MCP clients handle out-of-order responses by matching on request id.
    /// </summary>
    static void RequeueFrame(byte[] frame)
    {
        s_pendingFrames.Enqueue(frame);
        s_frameAvailable.Release();
    }

    static async Task ReadExactAsync(NetworkStream s, byte[] buf, int count, CancellationToken ct)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = await s.ReadAsync(buf, offset, count - offset, ct);
            if (read == 0) throw new IOException("Connection closed");
            offset += read;
        }
    }

    static int DetectPort()
    {
        var envPort = Environment.GetEnvironmentVariable("UNITY_MCP_PORT");
        if (envPort != null && int.TryParse(envPort, out int p)) return p;
        Console.Error.WriteLine("[unity-mcp-bridge] Warning: UNITY_MCP_PORT not set. Using default 51279.");
        return 51279;
    }
}
