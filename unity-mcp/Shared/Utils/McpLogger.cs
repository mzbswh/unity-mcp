using System;
using UnityEngine;

namespace UnityMcp.Shared.Utils
{
    public static class McpLogger
    {
        public enum LogLevel { Debug, Info, Warning, Error, Off }

        private const string Tag = "[UnityMCP]";

        /// <summary>Current log level. Set by Editor McpSettings or Runtime configuration.</summary>
        public static LogLevel CurrentLogLevel { get; set; } = LogLevel.Info;

        /// <summary>Whether audit logging is enabled.</summary>
        public static bool AuditEnabled { get; set; } = false;

        public static void Debug(string message)
        {
            if (CurrentLogLevel <= LogLevel.Debug)
                UnityEngine.Debug.Log($"{Tag} {message}");
        }

        public static void Info(string message)
        {
            if (CurrentLogLevel <= LogLevel.Info)
                UnityEngine.Debug.Log($"{Tag} {message}");
        }

        public static void Warning(string message)
        {
            if (CurrentLogLevel <= LogLevel.Warning)
                UnityEngine.Debug.LogWarning($"{Tag} {message}");
        }

        public static void Error(string message)
        {
            if (CurrentLogLevel <= LogLevel.Error)
                UnityEngine.Debug.LogError($"{Tag} {message}");
        }

        public static void Audit(string toolName, string args, long durationMs,
            bool success, string error = null)
        {
            if (!AuditEnabled) return;
            var status = success ? "OK" : $"ERR: {error}";
            UnityEngine.Debug.Log($"{Tag} AUDIT | {toolName} | {durationMs}ms | {status} | {args}");
        }
    }
}
