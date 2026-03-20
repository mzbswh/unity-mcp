using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Tools
{
    [System.Flags]
    internal enum LogMessageFlags : int
    {
        NoLogMessageFlags = 0,
        Error = 1 << 0,
        Assert = 1 << 1,
        Log = 1 << 2,
        Fatal = 1 << 4,
        AssetImportError = 1 << 6,
        AssetImportWarning = 1 << 7,
        ScriptingError = 1 << 8,
        ScriptingWarning = 1 << 9,
        ScriptingLog = 1 << 10,
        ScriptCompileError = 1 << 11,
        ScriptCompileWarning = 1 << 12,
        StickyLog = 1 << 13,
        MayIgnoreLineNumber = 1 << 14,
        ReportBug = 1 << 15,
        DisplayPreviousErrorInStatusBar = 1 << 16,
        ScriptingException = 1 << 17,
        DontExtractStacktrace = 1 << 18,
        ScriptingAssertion = 1 << 21,
        StacktraceIsPostprocessed = 1 << 22,
        IsCalledFromManaged = 1 << 23,
    }

    [McpToolGroup("Console")]
    public static class ConsoleTools
    {
        [McpTool("console_get_logs", "Get Unity console log entries with filtering",
            Group = "console", ReadOnly = true)]
        public static ToolResult GetLogs(
            [Desc("Log types to include: Log, Warning, Error, Exception, Assert")] string[] logTypes = null,
            [Desc("Maximum number of log entries to return")] int maxCount = 50,
            [Desc("Regex pattern to filter messages")] string pattern = null,
            [Desc("Only return the first line of each message")] bool onlyFirstLine = false)
        {
            var entries = GetLogEntries(maxCount * 2); // over-fetch for filtering

            if (logTypes != null && logTypes.Length > 0)
            {
                var allowedTypes = new HashSet<string>(logTypes, StringComparer.OrdinalIgnoreCase);
                entries = entries.Where(e => allowedTypes.Contains(e.type)).ToList();
            }

            if (!string.IsNullOrEmpty(pattern))
            {
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                entries = entries.Where(e => regex.IsMatch(e.message)).ToList();
            }

            var result = entries.Take(maxCount).Select(e => new
            {
                e.type,
                message = onlyFirstLine ? e.message.Split('\n')[0] : e.message,
                e.stackTrace
            }).ToArray();

            return ToolResult.Json(new { count = result.Length, logs = result });
        }

        // Use reflection to access internal Unity LogEntries API
        private static List<LogEntry> GetLogEntries(int maxCount)
        {
            var list = new List<LogEntry>();
            try
            {
                var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor.CoreModule")
                    ?? Type.GetType("UnityEditor.LogEntries, UnityEditor");
                if (logEntriesType == null) return list;

                var getCount = logEntriesType.GetMethod("GetCount",
                    BindingFlags.Public | BindingFlags.Static);
                var startGetting = logEntriesType.GetMethod("StartGettingEntries",
                    BindingFlags.Public | BindingFlags.Static);
                var endGetting = logEntriesType.GetMethod("EndGettingEntries",
                    BindingFlags.Public | BindingFlags.Static);
                var getEntry = logEntriesType.GetMethod("GetEntryInternal",
                    BindingFlags.Public | BindingFlags.Static);

                if (getCount == null || startGetting == null || getEntry == null) return list;

                int count = (int)getCount.Invoke(null, null);
                startGetting?.Invoke(null, null);

                // LogEntry type
                var logEntryType = Type.GetType("UnityEditor.LogEntry, UnityEditor.CoreModule")
                    ?? Type.GetType("UnityEditor.LogEntry, UnityEditor");
                if (logEntryType == null) return list;

                var entry = Activator.CreateInstance(logEntryType);
                var modeField = logEntryType.GetField("mode") ?? logEntryType.GetField("mode",
                    BindingFlags.Public | BindingFlags.Instance);
                var messageField = logEntryType.GetField("message") ?? logEntryType.GetField("condition",
                    BindingFlags.Public | BindingFlags.Instance);

                int start = Mathf.Max(0, count - maxCount);
                for (int i = start; i < count; i++)
                {
                    getEntry.Invoke(null, new[] { i, entry });
                    int mode = modeField != null ? (int)modeField.GetValue(entry) : 0;
                    string message = messageField?.GetValue(entry)?.ToString() ?? "";
                    list.Add(new LogEntry
                    {
                        type = ModeToType(mode),
                        message = message,
                        stackTrace = ""
                    });
                }

                endGetting?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                list.Add(new LogEntry
                {
                    type = "Error",
                    message = $"Failed to read logs: {ex.Message}",
                    stackTrace = ""
                });
            }
            return list;
        }

        private static string ModeToType(int mode)
        {
            const int errorMask = (int)(LogMessageFlags.Error | LogMessageFlags.Fatal
                | LogMessageFlags.ScriptingError | LogMessageFlags.ScriptCompileError
                | LogMessageFlags.AssetImportError);
            const int exceptionMask = (int)LogMessageFlags.ScriptingException;
            const int assertMask = (int)(LogMessageFlags.Assert | LogMessageFlags.ScriptingAssertion);
            const int warningMask = (int)(LogMessageFlags.ScriptingWarning
                | LogMessageFlags.ScriptCompileWarning | LogMessageFlags.AssetImportWarning);
            const int logMask = (int)(LogMessageFlags.Log | LogMessageFlags.ScriptingLog);

            if ((mode & errorMask) != 0) return "Error";
            if ((mode & exceptionMask) != 0) return "Exception";
            if ((mode & assertMask) != 0) return "Assert";
            if ((mode & warningMask) != 0) return "Warning";
            if ((mode & logMask) != 0) return "Log";
            return "Log";
        }

        private class LogEntry
        {
            public string type;
            public string message;
            public string stackTrace;
        }
    }
}
