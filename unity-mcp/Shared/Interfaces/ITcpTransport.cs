using System.Collections.Generic;
using UnityMcp.Shared.Models;

namespace UnityMcp.Shared.Interfaces
{
    public interface ITcpTransport
    {
        int Port { get; }
        bool IsRunning { get; }
        int ClientCount { get; }
        IReadOnlyList<ConnectedClientInfo> ConnectedClients { get; }
        void Start();
        void Stop();
        void BroadcastNotification(string json);
    }
}
