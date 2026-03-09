namespace UnityMcp.Shared.Interfaces
{
    public interface ITcpTransport
    {
        int Port { get; }
        bool IsRunning { get; }
        int ClientCount { get; }
        void Start();
        void Stop();
        void BroadcastNotification(string json);
    }
}
