using UnityEditor;
using UnityEngine;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Core
{
    [FilePath("UserSettings/UnityMcpSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class McpSettings : ScriptableSingleton<McpSettings>
    {
        public static new McpSettings Instance => instance;
        public enum McpLogLevel { Debug, Info, Warning, Error, Off }
        public enum TransportMode { Stdio, StreamableHttp }

        [SerializeField] private int port = PortResolver.DefaultPort;
        [SerializeField] private TransportMode transport = TransportMode.Stdio;
        [SerializeField] private int httpPort = 8080;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private int requestTimeoutSeconds = 60;
        [SerializeField] private McpLogLevel logLevel = McpLogLevel.Info;
        [SerializeField] private bool enableAuditLog;
        [SerializeField] private int maxBatchOperations = 50;
        [SerializeField] private string uvxPath = "";
        [SerializeField] private string serverSourceOverride = "";
        [SerializeField] private bool devModeForceRefresh;

        public int Port
        {
            get => port > 0 ? port : PortResolver.DefaultPort;
            set { port = value; Save(true); }
        }

        public TransportMode Transport
        {
            get => transport;
            set { transport = value; Save(true); }
        }

        public int HttpPort
        {
            get => httpPort;
            set { httpPort = value; Save(true); }
        }

        public bool AutoStart
        {
            get => autoStart;
            set { autoStart = value; Save(true); }
        }

        public int RequestTimeoutMs => requestTimeoutSeconds * 1000;

        public int RequestTimeoutSeconds
        {
            get => requestTimeoutSeconds;
            set { requestTimeoutSeconds = value; Save(true); }
        }

        public McpLogLevel LogLevel
        {
            get => logLevel;
            set { logLevel = value; Save(true); }
        }

        public bool EnableAuditLog
        {
            get => enableAuditLog;
            set { enableAuditLog = value; Save(true); }
        }

        public int MaxBatchOperations
        {
            get => maxBatchOperations;
            set { maxBatchOperations = value; Save(true); }
        }

        /// <summary>Override path to uvx executable. Empty = use system PATH.</summary>
        public string UvxPath
        {
            get => uvxPath;
            set { uvxPath = value; Save(true); }
        }

        /// <summary>Override server source for uvx --from. Empty = use default PyPI package.</summary>
        public string ServerSourceOverride
        {
            get => serverSourceOverride;
            set { serverSourceOverride = value; Save(true); }
        }

        /// <summary>When enabled, adds --no-cache --refresh to uvx commands (for local dev).</summary>
        public bool DevModeForceRefresh
        {
            get => devModeForceRefresh;
            set { devModeForceRefresh = value; Save(true); }
        }

        public string Version => McpConst.ServerVersion;
    }
}
