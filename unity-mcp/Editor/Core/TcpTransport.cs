using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityMcp.Shared.Interfaces;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Core
{
    public class TcpTransport : ITcpTransport
    {
        private readonly RequestHandler _handler;
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private readonly List<TcpClient> _clients = new();
        private readonly object _clientsLock = new();
        // Per-client write lock to prevent concurrent SendFrame from corrupting the TCP stream
        private readonly ConcurrentDictionary<TcpClient, object> _writeLocks = new();

        public int Port { get; }
        public bool IsRunning { get; private set; }

        public int ClientCount
        {
            get { lock (_clientsLock) return _clients.Count; }
        }

        public TcpTransport(int port, RequestHandler handler)
        {
            Port = port;
            _handler = handler;
        }

        public void Start()
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();
            IsRunning = true;
            Task.Run(() => AcceptLoop(_cts.Token));
        }

        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            _cts?.Cancel();
            _listener?.Stop();
            lock (_clientsLock)
            {
                foreach (var client in _clients)
                    try { client.Close(); } catch { }
                _clients.Clear();
            }
            _writeLocks.Clear();
            _cts?.Dispose();
            _cts = null;
            _listener = null;
        }

        public void BroadcastNotification(string json)
        {
            var payload = Encoding.UTF8.GetBytes(json);
            lock (_clientsLock)
            {
                foreach (var client in _clients)
                    SendFrame(client, McpConst.MsgTypeNotification, payload);
            }
        }

        public void BroadcastReloading()
        {
            BroadcastNotification(
                "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/reloading\"}");
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _writeLocks[client] = new object();
                    lock (_clientsLock) _clients.Add(client);
                    _ = HandleClient(client, ct);
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { break; }
            }
        }

        private async Task HandleClient(TcpClient client, CancellationToken ct)
        {
            var stream = client.GetStream();
            var headerBuf = new byte[5];

            try
            {
                while (!ct.IsCancellationRequested && client.Connected)
                {
                    await ReadExactAsync(stream, headerBuf, 5, ct);
                    int frameLen = IPAddress.NetworkToHostOrder(
                        BitConverter.ToInt32(headerBuf, 0));
                    byte msgType = headerBuf[4];
                    int payloadLen = frameLen - 1;

                    if (payloadLen <= 0 || payloadLen > 10 * 1024 * 1024)
                        throw new InvalidDataException($"Invalid payload length: {payloadLen}");

                    var msgBuf = new byte[payloadLen];
                    await ReadExactAsync(stream, msgBuf, payloadLen, ct);
                    var json = Encoding.UTF8.GetString(msgBuf);

                    McpLogger.Debug($"<- [{msgType:X2}] {json}");

                    // Notifications (0x03) with no id don't need a response
                    if (msgType == McpConst.MsgTypeNotification)
                    {
                        await _handler.HandleNotification(json);
                        continue;
                    }

                    if (msgType != McpConst.MsgTypeRequest)
                    {
                        McpLogger.Warning($"Unexpected message type 0x{msgType:X2}, ignoring");
                        continue;
                    }

                    var response = await _handler.HandleRequest(json);
                    McpLogger.Debug($"-> {response}");

                    var respBytes = Encoding.UTF8.GetBytes(response);
                    SendFrame(client, McpConst.MsgTypeResponse, respBytes);
                    await stream.FlushAsync(ct);
                }
            }
            catch (IOException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex) { McpLogger.Error($"Client error: {ex.Message}"); }
            finally
            {
                _writeLocks.TryRemove(client, out _);
                lock (_clientsLock) _clients.Remove(client);
                try { client.Close(); } catch { }
            }
        }

        private void SendFrame(TcpClient client, byte msgType, byte[] payload)
        {
            if (!_writeLocks.TryGetValue(client, out var writeLock)) return;
            lock (writeLock)
            {
                try
                {
                    if (!client.Connected) return;
                    var stream = client.GetStream();
                    int frameLen = 1 + payload.Length;
                    var header = new byte[5];
                    header[0] = (byte)(frameLen >> 24);
                    header[1] = (byte)(frameLen >> 16);
                    header[2] = (byte)(frameLen >> 8);
                    header[3] = (byte)(frameLen);
                    header[4] = msgType;
                    stream.Write(header, 0, 5);
                    stream.Write(payload, 0, payload.Length);
                }
                catch (Exception ex) { McpLogger.Debug($"SendFrame error: {ex.Message}"); }
            }
        }

        private static async Task ReadExactAsync(
            NetworkStream s, byte[] buf, int count, CancellationToken ct)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = await s.ReadAsync(buf, offset, count - offset, ct);
                if (read == 0) throw new IOException("Connection closed by remote");
                offset += read;
            }
        }
    }
}
