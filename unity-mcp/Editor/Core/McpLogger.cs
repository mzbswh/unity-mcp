using UnityEditor;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Core
{
    /// <summary>
    /// Editor-side logger configuration bridge.
    /// Syncs McpSettings log level to the shared McpLogger on domain reload.
    /// </summary>
    [InitializeOnLoad]
    internal static class McpLoggerConfig
    {
        static McpLoggerConfig()
        {
            SyncSettings();
        }

        internal static void SyncSettings()
        {
            var settings = McpSettings.Instance;
            McpLogger.CurrentLogLevel = (McpLogger.LogLevel)(int)settings.LogLevel;
            McpLogger.AuditEnabled = settings.EnableAuditLog;
        }
    }
}
