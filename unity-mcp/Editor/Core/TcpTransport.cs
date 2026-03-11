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
        private readonly ConcurrentDictionary<TcpClient, ConnectedClientInfo> _clientInfos = new();
        private Timer _heartbeatTimer;
        private int _reconnectCount;
        private DateTime? _lastConnectedAt;

        public int Port { get; }
        public bool IsRunning { get; private set; }
        public int ReconnectCount => _reconnectCount;
        public DateTime? LastConnectedAt => _lastConnectedAt;

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

        public TcpTransport(int port, RequestHandler handler)
        {
            Port = port;
            _handler = handler;
        }

        public void Start()
        {
            if (IsRunning) return;
            if (_lastConnectedAt.HasValue) _reconnectCount++;
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();
            IsRunning = true;
            Task.Run(() => AcceptLoop(_cts.Token));
            // Heartbeat: every 10s, probe all clients to detect dead connections
            _heartbeatTimer = new Timer(_ => PruneDeadClients(), null, 10_000, 10_000);
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
                    try { client.Close(); } catch (Exception ex) { McpLogger.Debug($"Close error: {ex.Message}"); }
                _clients.Clear();
            }
            _writeLocks.Clear();
            _clientInfos.Clear();
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
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
                    // Enable TCP keepalive with aggressive timeouts to detect dead connections
                    client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    // Short send timeout so heartbeat probes fail fast on dead connections
                    client.Client.SendTimeout = 5000;
                    _writeLocks[client] = new object();
                    var endpoint = client.Client.RemoteEndPoint;
                    _clientInfos[client] = new ConnectedClientInfo
                    {
                        Name = endpoint?.ToString() ?? "Unknown",
                        Endpoint = endpoint?.ToString(),
                        ConnectedAt = DateTime.Now
                    };
                    lock (_clientsLock) _clients.Add(client);
                    _lastConnectedAt = DateTime.Now;
                    McpLogger.Info($"Bridge connected from {endpoint}");
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
            var endpoint = client.Client.RemoteEndPoint;

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

                    // Extract client info from initialize request
                    TryExtractClientInfo(client, json);

                    var response = await _handler.HandleRequest(json);
                    McpLogger.Debug($"-> {response}");

                    var respBytes = Encoding.UTF8.GetBytes(response);
                    SendFrame(client, McpConst.MsgTypeResponse, respBytes);
                    await stream.FlushAsync(ct);
                }
            }
            catch (IOException ex) { McpLogger.Debug($"Client IO: {ex.Message}"); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { McpLogger.Error($"Client error: {ex.Message}"); }
            finally
            {
                McpLogger.Info($"Bridge disconnected ({endpoint})");
                _writeLocks.TryRemove(client, out _);
                _clientInfos.TryRemove(client, out _);
                lock (_clientsLock) _clients.Remove(client);
                try { client.Close(); } catch (Exception ex) { McpLogger.Debug($"Close error: {ex.Message}"); }
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

        private bool TrySendFrame(TcpClient client, byte msgType, byte[] payload)
        {
            if (!_writeLocks.TryGetValue(client, out var writeLock)) return false;
            lock (writeLock)
            {
                try
                {
                    if (!client.Connected) return false;
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
                    stream.Flush();
                    return true;
                }
                catch (Exception ex) { McpLogger.Debug($"Heartbeat send: {ex.Message}"); return false; }
            }
        }

        private static readonly byte[] s_pingPayload =
            Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/ping\"}");

        /// <summary>
        /// Periodically probes all connected clients by sending a small notification frame.
        /// A failed write reliably detects dead connections (process killed, network down, etc.)
        /// unlike Socket.Poll which cannot detect half-open connections.
        /// </summary>
        private void PruneDeadClients()
        {
            List<TcpClient> snapshot;
            lock (_clientsLock) snapshot = new List<TcpClient>(_clients);

            List<TcpClient> dead = null;
            foreach (var client in snapshot)
            {
                try
                {
                    var socket = client.Client;
                    if (socket == null || !socket.Connected)
                    {
                        (dead ??= new List<TcpClient>()).Add(client);
                        continue;
                    }

                    // Poll first for fast detection of graceful close
                    if (socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0)
                    {
                        (dead ??= new List<TcpClient>()).Add(client);
                        continue;
                    }

                    // Try sending a ping — this reliably detects killed processes
                    if (!TrySendFrame(client, McpConst.MsgTypeNotification, s_pingPayload))
                        (dead ??= new List<TcpClient>()).Add(client);
                }
                catch (Exception ex)
                {
                    McpLogger.Debug($"Heartbeat check: {ex.Message}");
                    (dead ??= new List<TcpClient>()).Add(client);
                }
            }

            if (dead != null)
            {
                lock (_clientsLock)
                {
                    foreach (var client in dead)
                    {
                        McpLogger.Debug("Pruning dead client connection");
                        _clients.Remove(client);
                        _writeLocks.TryRemove(client, out _);
                        _clientInfos.TryRemove(client, out _);
                        try { client.Close(); } catch (Exception ex) { McpLogger.Debug($"Close error: {ex.Message}"); }
                    }
                }
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
            catch (Exception ex) { McpLogger.Debug($"Notification parse: {ex.Message}"); }
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
