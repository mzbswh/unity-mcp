using System;
using System.IO;
using Newtonsoft.Json.Linq;
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

        // ======================== Quick Setup ========================

        private void DrawQuickSetup(McpSettings settings)
        {
            _showQuickSetup = EditorGUILayout.Foldout(_showQuickSetup, "Quick Setup", true);
            if (!_showQuickSetup) return;

            EditorGUILayout.HelpBox(
                "Auto-configure MCP client. Writes the Unity MCP server entry directly into the client's config file.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Claude Code"))
                ConfigureClient(settings, "claude_code");
            if (GUILayout.Button("Cursor"))
                ConfigureClient(settings, "cursor");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("VS Code"))
                ConfigureClient(settings, "vscode");
            if (GUILayout.Button("Windsurf"))
                ConfigureClient(settings, "windsurf");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            if (GUILayout.Button("Copy JSON to Clipboard"))
                CopyConfigToClipboard(settings);
        }

        private void ConfigureClient(McpSettings settings, string client)
        {
            try
            {
                var serverEntry = BuildServerEntry(settings);
                string configPath;
                bool success;

                switch (client)
                {
                    case "claude_code":
                        configPath = GetClaudeCodeConfigPath();
                        success = WriteClaudeCodeConfig(configPath, serverEntry);
                        break;
                    case "cursor":
                        configPath = GetProjectConfigPath(".cursor", "mcp.json");
                        success = WriteMcpServersConfig(configPath, "mcpServers", serverEntry);
                        break;
                    case "vscode":
                        configPath = GetProjectConfigPath(".vscode", "mcp.json");
                        success = WriteMcpServersConfig(configPath, "servers", serverEntry);
                        break;
                    case "windsurf":
                        configPath = GetWindsurfConfigPath();
                        success = WriteMcpServersConfig(configPath, "mcpServers", serverEntry);
                        break;
                    default:
                        McpLogger.Error($"Unknown client: {client}");
                        return;
                }

                if (success)
                {
                    McpLogger.Info($"Configured {client}: {configPath}");
                    ShowNotification(new GUIContent($"{client} configured!"));
                }
            }
            catch (Exception ex)
            {
                McpLogger.Error($"Failed to configure {client}: {ex.Message}");
                EditorUtility.DisplayDialog("Quick Setup Error",
                    $"Failed to configure {client}:\n{ex.Message}", "OK");
            }
        }

        // --- Build the server entry JObject ---

        private static JObject BuildServerEntry(McpSettings settings)
        {
            if (settings.Mode == McpSettings.ServerMode.BuiltIn)
            {
                string bridgePath = settings.BridgePath;
                if (string.IsNullOrEmpty(bridgePath))
                    bridgePath = ServerProcessManager.GetDefaultBridgePathStatic();
                bridgePath = bridgePath.Replace("\\", "/");

                return new JObject
                {
                    ["type"] = "stdio",
                    ["command"] = bridgePath,
                    ["args"] = new JArray { settings.Port.ToString() },
                    ["env"] = new JObject { ["UNITY_MCP_PORT"] = settings.Port.ToString() }
                };
            }

            // Python mode
            string script = settings.PythonServerScript;
            if (string.IsNullOrEmpty(script))
                script = "<path-to-server-script>";

            if (settings.UseUv)
                return new JObject
                {
                    ["type"] = "stdio",
                    ["command"] = "uv",
                    ["args"] = new JArray { "run", script },
                    ["env"] = new JObject { ["UNITY_MCP_PORT"] = settings.Port.ToString() }
                };

            return new JObject
            {
                ["type"] = "stdio",
                ["command"] = settings.PythonPath,
                ["args"] = new JArray { script },
                ["env"] = new JObject { ["UNITY_MCP_PORT"] = settings.Port.ToString() }
            };
        }

        // --- Config file paths ---

        private static string GetClaudeCodeConfigPath()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".claude.json");
        }

        private static string GetProjectConfigPath(string folder, string file)
        {
            string projectRoot = Application.dataPath.Replace("/Assets", "");
            string dir = Path.Combine(projectRoot, folder);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, file);
        }

        private static string GetWindsurfConfigPath()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string dir = Path.Combine(home, ".codeium", "windsurf");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "mcp_config.json");
        }

        // --- Write config: Claude Code (special structure) ---

        private static bool WriteClaudeCodeConfig(string configPath, JObject serverEntry)
        {
            // Claude Code: ~/.claude.json -> projects.<projectPath>.mcpServers.unity
            string projectPath = Application.dataPath.Replace("/Assets", "");

            JObject root;
            if (File.Exists(configPath))
                root = JObject.Parse(File.ReadAllText(configPath));
            else
                root = new JObject();

            // Ensure projects.<projectPath>.mcpServers exists
            if (root["projects"] == null)
                root["projects"] = new JObject();

            var projects = (JObject)root["projects"];
            if (projects[projectPath] == null)
                projects[projectPath] = new JObject();

            var project = (JObject)projects[projectPath];
            if (project["mcpServers"] == null)
                project["mcpServers"] = new JObject();

            var mcpServers = (JObject)project["mcpServers"];
            mcpServers["unity"] = serverEntry;

            File.WriteAllText(configPath, root.ToString(Newtonsoft.Json.Formatting.Indented));
            return true;
        }

        // --- Write config: Cursor / VS Code / Windsurf (standard structure) ---

        private static bool WriteMcpServersConfig(string configPath, string serversKey, JObject serverEntry)
        {
            // Format: { "<serversKey>": { "unity": { ... } } }
            JObject root;
            if (File.Exists(configPath))
                root = JObject.Parse(File.ReadAllText(configPath));
            else
                root = new JObject();

            if (root[serversKey] == null)
                root[serversKey] = new JObject();

            var servers = (JObject)root[serversKey];
            servers["unity"] = serverEntry;

            File.WriteAllText(configPath, root.ToString(Newtonsoft.Json.Formatting.Indented));
            return true;
        }

        // --- Fallback: copy to clipboard ---

        private void CopyConfigToClipboard(McpSettings settings)
        {
            var serverEntry = BuildServerEntry(settings);
            var config = new JObject
            {
                ["mcpServers"] = new JObject { ["unity"] = serverEntry }
            };
            EditorGUIUtility.systemCopyBuffer = config.ToString(Newtonsoft.Json.Formatting.Indented);
            McpLogger.Info("Copied MCP config JSON to clipboard");
            ShowNotification(new GUIContent("Config copied to clipboard!"));
        }
    }
}
