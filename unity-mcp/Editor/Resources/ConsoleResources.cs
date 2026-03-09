using UnityMcp.Editor.Tools;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Resources
{
    [McpToolGroup("ConsoleResources")]
    public static class ConsoleResources
    {
        [McpResource("unity://console/logs", "Console Logs",
            "Recent Unity console log entries")]
        public static ToolResult GetLogs()
        {
            return ConsoleTools.GetLogs(maxCount: 100, onlyFirstLine: true);
        }
    }
}
