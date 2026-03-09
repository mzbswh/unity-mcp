namespace UnityMcp.Shared.Models
{
    public static class McpConst
    {
        public const string ProtocolVersion = "2024-11-05";
        public const string ServerName = "Unity MCP";
        public const string ServerVersion = "1.0.0";

        // TCP frame message types
        public const byte MsgTypeRequest = 0x01;
        public const byte MsgTypeResponse = 0x02;
        public const byte MsgTypeNotification = 0x03;

        // JSON-RPC error codes
        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32603;
    }
}
