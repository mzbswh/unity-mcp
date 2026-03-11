namespace UnityMcp.Editor.Window.ClientConfig
{
    public enum McpStatus { NotConfigured, Configured, NeedsUpdate }

    public interface IConfigWriter
    {
        McpStatus CheckStatus(ClientProfile profile, int port, string transport);
        void Configure(ClientProfile profile, int port, string transport, int httpPort);
        string GetManualSnippet(ClientProfile profile, int port, string transport, int httpPort);
    }
}
