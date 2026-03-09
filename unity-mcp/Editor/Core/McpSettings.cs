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
        public enum ServerMode { BuiltIn, Python }
        public enum McpLogLevel { Debug, Info, Warning, Error, Off }

        [SerializeField] private ServerMode serverMode = ServerMode.BuiltIn;
        [SerializeField] private int port = PortResolver.DefaultPort;
        [SerializeField] private string bridgePath = "";
        [SerializeField] private string pythonPath = "python3";
        [SerializeField] private string pythonServerScript = "";
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool useUv = true;
        [SerializeField] private int requestTimeoutSeconds = 60;
        [SerializeField] private McpLogLevel logLevel = McpLogLevel.Info;
        [SerializeField] private bool enableAuditLog;
        [SerializeField] private int maxBatchOperations = 30;

        public ServerMode Mode
        {
            get => serverMode;
            set { serverMode = value; Save(true); }
        }

        public int Port
        {
            get => port > 0 ? port : PortResolver.DefaultPort;
            set { port = value; Save(true); }
        }

        public string BridgePath
        {
            get => bridgePath;
            set { bridgePath = value; Save(true); }
        }

        public string PythonPath
        {
            get => pythonPath;
            set { pythonPath = value; Save(true); }
        }

        public string PythonServerScript
        {
            get => pythonServerScript;
            set { pythonServerScript = value; Save(true); }
        }

        public bool AutoStart
        {
            get => autoStart;
            set { autoStart = value; Save(true); }
        }

        public bool UseUv
        {
            get => useUv;
            set { useUv = value; Save(true); }
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

        public string Version => McpConst.ServerVersion;
    }
}
