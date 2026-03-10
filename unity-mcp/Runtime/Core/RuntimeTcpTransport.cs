#if UNITY_MCP_RUNTIME
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityMcp.Shared.Interfaces;
using UnityMcp.Shared.Models;

namespace UnityMcp.Runtime.Core
{
    public class RuntimeTcpTransport : ITcpTransport
    {
        private readonly RuntimeRequestHandler _handler;
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private readonly List<TcpClient> _clients = new();
        private readonly object _clientsLock = new();
        private readonly ConcurrentDictionary<TcpClient, object> _writeLocks = new();
        private readonly ConcurrentDictionary<TcpClient, ConnectedClientInfo> _clientInfos = new();

        public int Port { get; }
        public bool IsRunning { get; private set; }

        public int ClientCount
        {
            get { lock (_clientsLock) return _clients.Count; }
        }

        public IReadOnlyList<ConnectedClientInfo> ConnectedClients
        {
            get
            {
                lock (_clientsLock)
                {
                    var list = new List<ConnectedClientInfo>();
                    foreach (var client in _clients)
                    {
                        if (_clientInfos.TryGetValue(client, out var info))
                            list.Add(info);
                    }
                    return list;
                }
            }
        }

        public RuntimeTcpTransport(int port, RuntimeRequestHandler handler)
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
            _clientInfos.Clear();
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

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _writeLocks[client] = new object();
                    var endpoint = client.Client.RemoteEndPoint;
                    _clientInfos[client] = new ConnectedClientInfo
                    {
                        Name = endpoint?.ToString() ?? "Unknown",
                        Endpoint = endpoint?.ToString(),
                        ConnectedAt = DateTime.Now
                    };
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

                    // Notifications (0x03) don't need a response
                    if (msgType == McpConst.MsgTypeNotification)
                    {
                        UnityEngine.Debug.Log($"[MCP Runtime] Received notification: {json.Substring(0, System.Math.Min(json.Length, 200))}");
                        continue;
                    }

                    if (msgType != McpConst.MsgTypeRequest)
                    {
                        UnityEngine.Debug.LogWarning($"[MCP Runtime] Unexpected message type 0x{msgType:X2}, ignoring");
                        continue;
                    }

                    TryExtractClientInfo(client, json);
                    var response = await _handler.HandleRequest(json);
                    var respBytes = Encoding.UTF8.GetBytes(response);
                    SendFrame(client, McpConst.MsgTypeResponse, respBytes);
                    await stream.FlushAsync(ct);
                }
            }
            catch (IOException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex) { UnityEngine.Debug.LogError($"[MCP Runtime] Client error: {ex.Message}"); }
            finally
            {
                _writeLocks.TryRemove(client, out _);
                _clientInfos.TryRemove(client, out _);
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
                catch (Exception ex) { UnityEngine.Debug.LogWarning($"[MCP Runtime] SendFrame error: {ex.Message}"); }
            }
        }

        private void TryExtractClientInfo(TcpClient client, string json)
        {
            try
            {
                var req = JObject.Parse(json);
                if (req["method"]?.ToString() != "initialize") return;
                var ci = req["params"]?["clientInfo"];
                if (ci == null) return;
                if (_clientInfos.TryGetValue(client, out var info))
                {
                    info.Name = ci["name"]?.ToString() ?? info.Name;
                    info.Version = ci["version"]?.ToString();
                }
            }
            catch { /* ignore parse errors */ }
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
#endif
