using UnityMcp.Shared.Interfaces;

namespace UnityMcp.Editor.Core
{
    /// <summary>
    /// Lightweight service locator for core MCP services.
    /// Enables testability by allowing mock registration.
    /// </summary>
    public static class McpServices
    {
        public static IToolRegistry ToolRegistry { get; set; }
        public static ITcpTransport Transport { get; set; }
        public static RequestHandler RequestHandler { get; set; }

        /// <summary>Reset all services (for testing).</summary>
        public static void Reset()
        {
            ToolRegistry = null;
            Transport = null;
            RequestHandler = null;
        }
    }
}
