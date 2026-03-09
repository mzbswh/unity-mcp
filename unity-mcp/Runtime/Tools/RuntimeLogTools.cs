#if UNITY_MCP_RUNTIME
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Runtime.Tools
{
    [McpToolGroup("Runtime.Logs")]
    public static class RuntimeLogTools
    {
        private static readonly List<LogEntry> _logs = new();
        private static readonly object _lock = new();
        private const int MaxLogs = 500;
        private static bool _registered;

        internal static void EnsureRegistered()
        {
            if (_registered) return;
            Application.logMessageReceived += OnLogMessage;
            _registered = true;
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            lock (_lock)
            {
                _logs.Add(new LogEntry
                {
                    message = condition,
                    stackTrace = stackTrace,
                    type = type.ToString(),
                    timestamp = Time.realtimeSinceStartup
                });

                if (_logs.Count > MaxLogs)
                    _logs.RemoveAt(0);
            }
        }

        [McpTool("runtime_get_logs", "Get runtime logs captured via Application.logMessageReceived",
            ReadOnly = true, Group = "runtime")]
        public static ToolResult GetLogs(
            [Desc("Filter by log type: 'Error', 'Warning', 'Log', or 'all'")] string filter = "all",
            [Desc("Maximum number of entries to return")] int maxCount = 50,
            [Desc("Clear logs after reading")] bool clear = false)
        {
            EnsureRegistered();

            List<LogEntry> result;
            lock (_lock)
            {
                var filtered = filter.ToLower() == "all"
                    ? _logs
                    : _logs.Where(l => l.type.ToLower().Contains(filter.ToLower())).ToList();

                result = filtered.TakeLast(maxCount).ToList();

                if (clear)
                    _logs.Clear();
            }

            return ToolResult.Json(new
            {
                count = result.Count,
                filter,
                logs = result.Select(l => new
                {
                    l.message,
                    l.type,
                    l.timestamp,
                    stackTrace = l.stackTrace?.Length > 500
                        ? l.stackTrace.Substring(0, 500) + "..."
                        : l.stackTrace
                })
            });
        }

        private class LogEntry
        {
            public string message;
            public string stackTrace;
            public string type;
            public float timestamp;
        }
    }
}
#endif
