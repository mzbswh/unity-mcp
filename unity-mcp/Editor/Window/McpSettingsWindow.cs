using System.IO;
using UnityEditor;
using UnityEngine;
using UnityMcp.Editor.Core;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Window
{
    public class McpSettingsWindow : EditorWindow
    {
        private Vector2 _toolScrollPos;
        private bool _showTools = true;
        private bool _showAdvanced;
        private bool _showQuickSetup;
        private string _cachedDefaultBridgePath;

        [MenuItem("Window/Unity MCP")]
        public static void ShowWindow()
        {
            var window = GetWindow<McpSettingsWindow>("Unity MCP");
            window.minSize = new Vector2(420, 500);
        }

        private void OnGUI()
        {
            var settings = McpSettings.Instance;

            // Header
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Unity MCP", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            DrawStatus();
            EditorGUILayout.Space(6);

            DrawServerMode(settings);
            EditorGUILayout.Space(6);

            DrawConnection(settings);
            EditorGUILayout.Space(6);

            DrawModeSpecific(settings);
            EditorGUILayout.Space(6);

            DrawAdvanced(settings);
            EditorGUILayout.Space(6);

            DrawRegisteredTools();
            EditorGUILayout.Space(6);

            DrawQuickSetup(settings);
        }

        private void DrawStatus()
        {
            var transport = McpServer.Transport;
            bool isRunning = transport != null && transport.IsRunning;
            int clients = transport?.ClientCount ?? 0;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            {
                var statusColor = isRunning ? Color.green : Color.red;
                var oldColor = GUI.color;
                GUI.color = statusColor;
                GUILayout.Label("\u25CF", GUILayout.Width(14));
                GUI.color = oldColor;

                string statusText = isRunning
                    ? $"Running on TCP:{transport.Port}  |  {clients} client(s)"
                    : "Stopped";
                EditorGUILayout.LabelField(statusText);

                if (GUILayout.Button(isRunning ? "Restart" : "Start", GUILayout.Width(60)))
                    McpServer.Restart();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawServerMode(McpSettings settings)
        {
            EditorGUILayout.LabelField("Server Mode", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var mode = (McpSettings.ServerMode)EditorGUILayout.EnumPopup("Mode", settings.Mode);
            if (EditorGUI.EndChangeCheck())
                settings.Mode = mode;
        }

        private void DrawConnection(McpSettings settings)
        {
            EditorGUILayout.LabelField("Connection", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            int port = EditorGUILayout.IntField("Port (-1 = auto)", settings.Port);
            if (EditorGUI.EndChangeCheck())
                settings.Port = port;

            EditorGUI.BeginChangeCheck();
            bool autoStart = EditorGUILayout.Toggle("Auto Start", settings.AutoStart);
            if (EditorGUI.EndChangeCheck())
                settings.AutoStart = autoStart;
        }

        private void DrawModeSpecific(McpSettings settings)
        {
            if (settings.Mode == McpSettings.ServerMode.BuiltIn)
            {
                EditorGUILayout.LabelField("Built-in Server (C# Bridge)", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                string bridgePath = EditorGUILayout.TextField("Bridge Path", settings.BridgePath);
                if (EditorGUI.EndChangeCheck())
                    settings.BridgePath = bridgePath;

                if (GUILayout.Button("Browse...", GUILayout.Width(80)))
                {
                    string path = EditorUtility.OpenFilePanel("Select Bridge Executable", "", "");
                    if (!string.IsNullOrEmpty(path))
                        settings.BridgePath = path;
                }
                // Check if bridge binary exists (cache default path to avoid per-frame IO)
                if (_cachedDefaultBridgePath == null)
                    _cachedDefaultBridgePath = ServerProcessManager.GetDefaultBridgePathStatic();
                string effectiveBridgePath = string.IsNullOrEmpty(bridgePath)
                    ? _cachedDefaultBridgePath
                    : bridgePath;
                if (!File.Exists(effectiveBridgePath))
                {
                    EditorGUILayout.HelpBox(
                        $"Bridge binary not found:\n{effectiveBridgePath}\n\n" +
                        "Build it with: ./scripts/build_bridge.sh --current-only",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Leave Bridge Path empty to use the default bundled binary.",
                        MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Python Server (FastMCP)", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                string pythonPath = EditorGUILayout.TextField("Python Path", settings.PythonPath);
                if (EditorGUI.EndChangeCheck())
                    settings.PythonPath = pythonPath;

                EditorGUI.BeginChangeCheck();
                string script = EditorGUILayout.TextField("Server Script", settings.PythonServerScript);
                if (EditorGUI.EndChangeCheck())
                    settings.PythonServerScript = script;

                EditorGUI.BeginChangeCheck();
                bool useUv = EditorGUILayout.Toggle("Use uv", settings.UseUv);
                if (EditorGUI.EndChangeCheck())
                    settings.UseUv = useUv;
            }
        }

        private void DrawAdvanced(McpSettings settings)
        {
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced", true);
            if (!_showAdvanced) return;

            EditorGUI.indentLevel++;

            EditorGUI.BeginChangeCheck();
            int timeout = EditorGUILayout.IntField("Request Timeout (s)", settings.RequestTimeoutSeconds);
            if (EditorGUI.EndChangeCheck())
                settings.RequestTimeoutSeconds = Mathf.Max(1, timeout);

            EditorGUI.BeginChangeCheck();
            var logLevel = (McpSettings.McpLogLevel)EditorGUILayout.EnumPopup("Log Level", settings.LogLevel);
            if (EditorGUI.EndChangeCheck())
            {
                settings.LogLevel = logLevel;
                McpLogger.CurrentLogLevel = (McpLogger.LogLevel)(int)logLevel;
            }

            EditorGUI.BeginChangeCheck();
            bool audit = EditorGUILayout.Toggle("Enable Audit Log", settings.EnableAuditLog);
            if (EditorGUI.EndChangeCheck())
            {
                settings.EnableAuditLog = audit;
                McpLogger.AuditEnabled = audit;
            }

            EditorGUI.indentLevel--;
        }

        private void DrawRegisteredTools()
        {
            var registry = McpServer.Registry;
            if (registry == null) return;

            string header = $"Registered  —  Tools: {registry.ToolCount}  " +
                            $"Resources: {registry.ResourceCount}  Prompts: {registry.PromptCount}";
            _showTools = EditorGUILayout.Foldout(_showTools, header, true);
            if (!_showTools) return;

            _toolScrollPos = EditorGUILayout.BeginScrollView(_toolScrollPos, GUILayout.MaxHeight(200));
            var tools = registry.GetToolList();
            foreach (var tool in tools)
            {
                string name = tool["name"]?.ToString();
                string desc = tool["description"]?.ToString();
                bool enabled = registry.IsToolEnabled(name);

                EditorGUI.BeginChangeCheck();
                bool newEnabled = EditorGUILayout.ToggleLeft($"{name}  —  {desc}", enabled);
                if (EditorGUI.EndChangeCheck())
                    registry.SetToolEnabled(name, newEnabled);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawQuickSetup(McpSettings settings)
        {
            _showQuickSetup = EditorGUILayout.Foldout(_showQuickSetup, "Quick Setup", true);
            if (!_showQuickSetup) return;

            EditorGUILayout.HelpBox(
                "Copy MCP client configuration to clipboard for your AI tool.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Claude Code"))
                CopyConfig(settings, "claude");
            if (GUILayout.Button("Cursor"))
                CopyConfig(settings, "cursor");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("VS Code"))
                CopyConfig(settings, "vscode");
            if (GUILayout.Button("Windsurf"))
                CopyConfig(settings, "windsurf");
            EditorGUILayout.EndHorizontal();
        }

        private void CopyConfig(McpSettings settings, string client)
        {
            string config;
            if (settings.Mode == McpSettings.ServerMode.BuiltIn)
            {
                string bridgePath = settings.BridgePath;
                if (string.IsNullOrEmpty(bridgePath))
                    bridgePath = ServerProcessManager.GetDefaultBridgePathStatic();
                // Normalize path separators for cross-platform JSON configs
                bridgePath = bridgePath.Replace("\\", "/");

                config = $@"{{
  ""mcpServers"": {{
    ""unity"": {{
      ""command"": ""{EscapeJson(bridgePath)}"",
      ""args"": [""{settings.Port}""],
      ""env"": {{
        ""UNITY_MCP_PORT"": ""{settings.Port}""
      }}
    }}
  }}
}}";
            }
            else
            {
                string script = settings.PythonServerScript;
                if (string.IsNullOrEmpty(script))
                    script = "<path-to-server-script>";

                if (settings.UseUv)
                    config = $@"{{
  ""mcpServers"": {{
    ""unity"": {{
      ""command"": ""uv"",
      ""args"": [""run"", ""{EscapeJson(script)}""],
      ""env"": {{
        ""UNITY_MCP_PORT"": ""{settings.Port}""
      }}
    }}
  }}
}}";
                else
                    config = $@"{{
  ""mcpServers"": {{
    ""unity"": {{
      ""command"": ""{EscapeJson(settings.PythonPath)}"",
      ""args"": [""{EscapeJson(script)}""],
      ""env"": {{
        ""UNITY_MCP_PORT"": ""{settings.Port}""
      }}
    }}
  }}
}}";
            }

            EditorGUIUtility.systemCopyBuffer = config;
            McpLogger.Info($"Copied {client} MCP config to clipboard");
            ShowNotification(new GUIContent($"Copied {client} config!"));
        }

        private static string EscapeJson(string s) =>
            s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
    }
}
