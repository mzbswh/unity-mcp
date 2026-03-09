using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

/// <summary>
/// Minimal stdio-to-TCP bridge for MCP clients.
/// Reads JSON-RPC from stdin, forwards to Unity TCP, returns responses to stdout.
/// </summary>
class Program
{
    const byte MsgTypeRequest = 0x01;
    const byte MsgTypeResponse = 0x02;
    const byte MsgTypeNotification = 0x03;

    static async Task Main(string[] args)
    {
        int port = args.Length > 0 ? int.Parse(args[0]) : DetectPort();

        int attempt = 0;
        int[] delays = { 0, 1000, 2000, 4000, 8000, 15000, 30000 };

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port);
                attempt = 0;
                Console.Error.WriteLine($"[unity-mcp-bridge] Connected to Unity on port {port}");

                var stream = client.GetStream();
                var stdinToTcp = Task.Run(() => PipeStdinToTcp(stream, cts.Token));
                var tcpToStdout = Task.Run(() => PipeTcpToStdout(stream, cts.Token));
                await Task.WhenAny(stdinToTcp, tcpToStdout);
            }
            catch (Exception) when (!cts.Token.IsCancellationRequested)
            {
                int delay = attempt < delays.Length ? delays[attempt] : delays[delays.Length - 1];
                attempt++;
                int jitter = (int)(delay * 0.2 * Random.Shared.NextDouble());
                Console.Error.WriteLine($"[unity-mcp-bridge] Reconnecting in {delay + jitter}ms (attempt {attempt})");
                await Task.Delay(delay + jitter, cts.Token);
            }
        }
    }

    static async Task PipeStdinToTcp(NetworkStream tcp, CancellationToken ct)
    {
        using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
        string line;
        while ((line = await reader.ReadLineAsync()) != null && !ct.IsCancellationRequested)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var payload = Encoding.UTF8.GetBytes(line);

            // Determine message type: requests have "id", notifications don't
            byte msgType = MsgTypeRequest;
            try
            {
                var obj = JObject.Parse(line);
                if (obj["id"] == null) msgType = MsgTypeNotification;
            }
            catch { }

            int frameLen = 1 + payload.Length;
            var header = new byte[5];
            header[0] = (byte)(frameLen >> 24);
            header[1] = (byte)(frameLen >> 16);
            header[2] = (byte)(frameLen >> 8);
            header[3] = (byte)(frameLen);
            header[4] = msgType;
            await tcp.WriteAsync(header, 0, 5, ct);
            await tcp.WriteAsync(payload, 0, payload.Length, ct);
            await tcp.FlushAsync(ct);
        }
    }

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

            // Handle server-initiated notifications:
            // - Internal notifications (e.g. notifications/reloading) are dropped
            // - MCP-standard notifications (e.g. notifications/tools/list_changed)
            //   must be forwarded to stdout per the MCP spec
            if (msgType == MsgTypeNotification)
            {
                try
                {
                    var obj = JObject.Parse(json);
                    var method = obj["method"]?.ToString() ?? "";
                    Console.Error.WriteLine($"[unity-mcp-bridge] Server notification: {method}");

                    // Internal Unity notifications — drop, don't forward
                    if (method == "notifications/reloading")
                    {
                        Console.Error.WriteLine("[unity-mcp-bridge] Unity reloading, waiting...");
                        continue;
                    }

                    // MCP-standard notifications — forward to client via stdout
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
        Console.Error.WriteLine("[unity-mcp-bridge] Warning: UNITY_MCP_PORT not set. Using default 52345.");
        return 52345;
    }
}
