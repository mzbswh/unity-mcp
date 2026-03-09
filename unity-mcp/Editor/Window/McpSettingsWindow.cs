using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Editor.Core;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Window
{
    public class McpSettingsWindow : EditorWindow
    {
        // Styles
        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _boxStyle;
        private GUIStyle _statusBoxStyle;
        private GUIStyle _clientCardStyle;
        private GUIStyle _clientCardConfiguredStyle;
        private GUIStyle _wrappedLabelStyle;
        private GUIStyle _versionStyle;
        private bool _stylesInitialized;

        // State
        private int _selectedTab;
        private readonly string[] _tabNames = { "Server", "Clients", "Tools" };
        private Vector2 _scrollPos;
        private string _cachedDefaultBridgePath;

        // Client config cache (refreshed periodically)
        private double _lastClientCheckTime;
        private const double ClientCheckInterval = 2.0;
        private bool _claudeCodeConfigured;
        private bool _cursorConfigured;
        private bool _vscodeConfigured;
        private bool _windsurfConfigured;

        [MenuItem("Window/Unity MCP")]
        public static void ShowWindow()
        {
            var window = GetWindow<McpSettingsWindow>("Unity MCP");
            window.minSize = new Vector2(480, 400);
        }

        private void OnEnable()
        {
            RefreshClientStatus();
        }

        private void OnGUI()
        {
            InitializeStyles();

            EditorGUILayout.BeginVertical();

            // Header
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Unity MCP", _headerStyle);
            EditorGUILayout.Space(4);

            // Status bar
            DrawStatusBar();
            EditorGUILayout.Space(6);

            // Tabs
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames, GUILayout.Height(24));
            EditorGUILayout.Space(6);

            // Tab content
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            switch (_selectedTab)
            {
                case 0: DrawServerTab(); break;
                case 1: DrawClientsTab(); break;
                case 2: DrawToolsTab(); break;
            }
            EditorGUILayout.EndScrollView();

            // Version footer
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"v{McpConst.ServerVersion}", _versionStyle);
            EditorGUILayout.Space(2);

            EditorGUILayout.EndVertical();

            // Periodic client status refresh
            if (EditorApplication.timeSinceStartup - _lastClientCheckTime > ClientCheckInterval)
                RefreshClientStatus();
        }

        // ======================== Status Bar ========================

        private void DrawStatusBar()
        {
            var transport = McpServer.Transport;
            bool isRunning = transport != null && transport.IsRunning;
            int clients = transport?.ClientCount ?? 0;

            EditorGUILayout.BeginHorizontal(_statusBoxStyle);
            {
                // Status dot
                var oldColor = GUI.color;
                GUI.color = isRunning ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.9f, 0.2f, 0.2f);
                GUILayout.Label("\u25CF", GUILayout.Width(16));
                GUI.color = oldColor;

                // Status text
                var statusStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = isRunning ? new Color(0.1f, 0.7f, 0.2f) : new Color(0.8f, 0.2f, 0.2f) }
                };
                string statusText = isRunning
                    ? $"Running  |  Port {transport.Port}  |  {clients} client(s)"
                    : "Stopped";
                EditorGUILayout.LabelField(statusText, statusStyle);

                // Control buttons
                GUI.enabled = !isRunning;
                if (GUILayout.Button("Start", GUILayout.Width(56), GUILayout.Height(22)))
                    McpServer.Restart();
                GUI.enabled = isRunning;
                if (GUILayout.Button("Stop", GUILayout.Width(56), GUILayout.Height(22)))
                    McpServer.Shutdown();
                GUI.enabled = true;

                if (GUILayout.Button("Restart", GUILayout.Width(56), GUILayout.Height(22)))
                    McpServer.Restart();
            }
            EditorGUILayout.EndHorizontal();
        }

        // ======================== Server Tab ========================

        private void DrawServerTab()
        {
            var settings = McpSettings.Instance;

            // Server Mode
            EditorGUILayout.LabelField("Server Mode", _subHeaderStyle);
            EditorGUILayout.BeginVertical(_boxStyle);
            {
                EditorGUI.BeginChangeCheck();
                var mode = (McpSettings.ServerMode)EditorGUILayout.EnumPopup("Mode", settings.Mode);
                if (EditorGUI.EndChangeCheck())
                    settings.Mode = mode;

                EditorGUILayout.Space(2);

                EditorGUI.BeginChangeCheck();
                int port = EditorGUILayout.IntField(
                    new GUIContent("Port", "TCP port for MCP communication. -1 = auto-assign from project path hash."),
                    settings.Port);
                if (EditorGUI.EndChangeCheck())
                    settings.Port = port;

                EditorGUI.BeginChangeCheck();
                bool autoStart = EditorGUILayout.Toggle(
                    new GUIContent("Auto Start", "Automatically start the MCP server when Unity opens."),
                    settings.AutoStart);
                if (EditorGUI.EndChangeCheck())
                    settings.AutoStart = autoStart;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Mode-specific settings
            if (settings.Mode == McpSettings.ServerMode.BuiltIn)
                DrawBuiltInSettings(settings);
            else
                DrawPythonSettings(settings);

            EditorGUILayout.Space(4);

            // Advanced
            DrawAdvanced(settings);
        }

        private void DrawBuiltInSettings(McpSettings settings)
        {
            EditorGUILayout.LabelField("Built-in Server (C# Bridge)", _subHeaderStyle);
            EditorGUILayout.BeginVertical(_boxStyle);
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                string bridgePath = EditorGUILayout.TextField("Bridge Path", settings.BridgePath);
                if (EditorGUI.EndChangeCheck())
                    settings.BridgePath = bridgePath;

                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string path = EditorUtility.OpenFilePanel("Select Bridge Executable", "", "");
                    if (!string.IsNullOrEmpty(path))
                        settings.BridgePath = path;
                }
                EditorGUILayout.EndHorizontal();

                if (_cachedDefaultBridgePath == null)
                    _cachedDefaultBridgePath = ServerProcessManager.GetDefaultBridgePathStatic();
                string effectivePath = string.IsNullOrEmpty(settings.BridgePath) ? _cachedDefaultBridgePath : settings.BridgePath;

                if (!File.Exists(effectivePath))
                    EditorGUILayout.HelpBox(
                        $"Bridge binary not found:\n{effectivePath}\n\nBuild with: ./scripts/build_bridge.sh --current-only",
                        MessageType.Warning);
                else
                    EditorGUILayout.HelpBox("Leave empty to use the default bundled binary.", MessageType.Info);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawPythonSettings(McpSettings settings)
        {
            EditorGUILayout.LabelField("Python Server (FastMCP)", _subHeaderStyle);
            EditorGUILayout.BeginVertical(_boxStyle);
            {
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
            EditorGUILayout.EndVertical();
        }

        private void DrawAdvanced(McpSettings settings)
        {
            EditorGUILayout.LabelField("Advanced", _subHeaderStyle);
            EditorGUILayout.BeginVertical(_boxStyle);
            {
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
            }
            EditorGUILayout.EndVertical();
        }

        // ======================== Clients Tab ========================

        private void DrawClientsTab()
        {
            EditorGUILayout.LabelField("Client Configuration", _subHeaderStyle);
            EditorGUILayout.BeginVertical(_boxStyle);
            EditorGUILayout.LabelField(
                "Click a client button to write the Unity MCP server entry into its config file. " +
                "A green checkmark indicates the client is already configured.",
                _wrappedLabelStyle);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            var settings = McpSettings.Instance;

            DrawClientCard("Claude Code", "claude_code", _claudeCodeConfigured, settings);
            DrawClientCard("Cursor", "cursor", _cursorConfigured, settings);
            DrawClientCard("VS Code / Copilot", "vscode", _vscodeConfigured, settings);
            DrawClientCard("Windsurf", "windsurf", _windsurfConfigured, settings);

            EditorGUILayout.Space(8);

            // Clipboard fallback
            EditorGUILayout.LabelField("Manual Setup", _subHeaderStyle);
            EditorGUILayout.BeginVertical(_boxStyle);
            {
                EditorGUILayout.LabelField("Copy the JSON config to clipboard for other clients.", _wrappedLabelStyle);
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Copy Config to Clipboard", GUILayout.Height(28)))
                    CopyConfigToClipboard(settings);
            }
            EditorGUILayout.EndVertical();

            // Connected Clients
            EditorGUILayout.Space(8);
            DrawConnectedClients();
        }

        private void DrawClientCard(string label, string clientId, bool isConfigured, McpSettings settings)
        {
            var style = isConfigured ? _clientCardConfiguredStyle : _clientCardStyle;

            EditorGUILayout.BeginHorizontal(style);
            {
                // Status icon
                var oldColor = GUI.color;
                GUI.color = isConfigured ? new Color(0.2f, 0.85f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
                GUILayout.Label(isConfigured ? "\u2714" : "\u2716", GUILayout.Width(20));
                GUI.color = oldColor;

                // Client name
                GUILayout.Label(label, EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                // Status label
                var statusLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = isConfigured ? new Color(0.1f, 0.6f, 0.1f) : new Color(0.6f, 0.4f, 0.1f) }
                };
                GUILayout.Label(isConfigured ? "Configured" : "Not configured", statusLabelStyle);

                // Configure button
                if (GUILayout.Button(isConfigured ? "Update" : "Configure", GUILayout.Width(80), GUILayout.Height(22)))
                {
                    ConfigureClient(settings, clientId);
                    RefreshClientStatus();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
        }

        private void DrawConnectedClients()
        {
            var transport = McpServer.Transport;
            int count = transport?.ClientCount ?? 0;

            EditorGUILayout.LabelField($"Connected Clients ({count})", _subHeaderStyle);
            EditorGUILayout.BeginVertical(_boxStyle);
            {
                if (count > 0)
                {
                    EditorGUILayout.LabelField(
                        $"{count} client(s) currently connected to TCP port {transport.Port}.",
                        _wrappedLabelStyle);
                }
                else
                {
                    var centeredStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true };
                    GUILayout.Label(
                        "No clients connected.\nConfigure a client above, then invoke a tool from the MCP client to connect.",
                        centeredStyle, GUILayout.ExpandWidth(true));
                }
            }
            EditorGUILayout.EndVertical();
        }

        // ======================== Tools Tab ========================

        private bool _showToolsFoldout = true;
        private bool _showResourcesFoldout = true;
        private bool _showPromptsFoldout = true;

        private void DrawToolsTab()
        {
            var registry = McpServer.Registry;
            if (registry == null)
            {
                EditorGUILayout.HelpBox("Server not initialized. Tools will appear after the server starts.", MessageType.Info);
                return;
            }

            // Summary badges
            EditorGUILayout.BeginVertical(_boxStyle);
            {
                EditorGUILayout.BeginHorizontal();
                DrawCountBadge("Tools", registry.ToolCount);
                DrawCountBadge("Resources", registry.ResourceCount);
                DrawCountBadge("Prompts", registry.PromptCount);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Tools section
            DrawToolsSection(registry);
            EditorGUILayout.Space(4);

            // Resources section
            DrawResourcesSection(registry);
            EditorGUILayout.Space(4);

            // Prompts section
            DrawPromptsSection(registry);
        }

        private void DrawToolsSection(ToolRegistry registry)
        {
            _showToolsFoldout = EditorGUILayout.Foldout(_showToolsFoldout, $"Tools ({registry.ToolCount})", true, EditorStyles.foldoutHeader);
            if (!_showToolsFoldout) return;

            EditorGUILayout.BeginVertical(_boxStyle);
            {
                string lastGroup = null;
                foreach (var (name, description, group) in registry.GetAllToolEntries())
                {
                    // Group header
                    string displayGroup = string.IsNullOrEmpty(group) ? "General" : group;
                    if (displayGroup != lastGroup)
                    {
                        if (lastGroup != null) EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField(displayGroup, EditorStyles.boldLabel);
                        lastGroup = displayGroup;
                    }

                    // Toggle
                    bool enabled = registry.IsToolEnabled(name);
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.BeginHorizontal();
                    bool newEnabled = EditorGUILayout.ToggleLeft(name, enabled);
                    EditorGUILayout.EndHorizontal();
                    if (EditorGUI.EndChangeCheck())
                        registry.SetToolEnabled(name, newEnabled);

                    // Description
                    if (!string.IsNullOrEmpty(description))
                    {
                        EditorGUI.indentLevel += 2;
                        var descStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            wordWrap = true,
                            normal = { textColor = enabled ? EditorStyles.miniLabel.normal.textColor
                                                          : new Color(0.5f, 0.5f, 0.5f) }
                        };
                        EditorGUILayout.LabelField(description, descStyle);
                        EditorGUI.indentLevel -= 2;
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawResourcesSection(ToolRegistry registry)
        {
            _showResourcesFoldout = EditorGUILayout.Foldout(_showResourcesFoldout, $"Resources ({registry.ResourceCount})", true, EditorStyles.foldoutHeader);
            if (!_showResourcesFoldout) return;

            EditorGUILayout.BeginVertical(_boxStyle);
            {
                foreach (var (name, description) in registry.GetAllResourceEntries())
                {
                    EditorGUILayout.LabelField(name, EditorStyles.boldLabel);
                    if (!string.IsNullOrEmpty(description))
                    {
                        EditorGUI.indentLevel += 2;
                        EditorGUILayout.LabelField(description, _wrappedLabelStyle);
                        EditorGUI.indentLevel -= 2;
                    }
                    EditorGUILayout.Space(2);
                }

                if (registry.ResourceCount == 0)
                    EditorGUILayout.LabelField("No resources registered.", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawPromptsSection(ToolRegistry registry)
        {
            _showPromptsFoldout = EditorGUILayout.Foldout(_showPromptsFoldout, $"Prompts ({registry.PromptCount})", true, EditorStyles.foldoutHeader);
            if (!_showPromptsFoldout) return;

            EditorGUILayout.BeginVertical(_boxStyle);
            {
                foreach (var (name, description) in registry.GetAllPromptEntries())
                {
                    EditorGUILayout.LabelField(name, EditorStyles.boldLabel);
                    if (!string.IsNullOrEmpty(description))
                    {
                        EditorGUI.indentLevel += 2;
                        EditorGUILayout.LabelField(description, _wrappedLabelStyle);
                        EditorGUI.indentLevel -= 2;
                    }
                    EditorGUILayout.Space(2);
                }

                if (registry.PromptCount == 0)
                    EditorGUILayout.LabelField("No prompts registered.", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawCountBadge(string label, int count)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinWidth(80));
            EditorGUILayout.LabelField(count.ToString(), new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 18 });
            EditorGUILayout.LabelField(label, new GUIStyle(EditorStyles.centeredGreyMiniLabel));
            EditorGUILayout.EndVertical();
        }

        // ======================== Client Configuration Logic ========================

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
                    EditorUtility.DisplayDialog("Success",
                        $"Unity MCP has been configured for {client}.\n\nConfig file:\n{configPath}", "OK");
                }
            }
            catch (Exception ex)
            {
                McpLogger.Error($"Failed to configure {client}: {ex.Message}");
                EditorUtility.DisplayDialog("Configuration Error",
                    $"Failed to configure {client}:\n{ex.Message}", "OK");
            }
        }

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

        // ======================== Config File Paths ========================

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

        // ======================== Config Writers ========================

        private static bool WriteClaudeCodeConfig(string configPath, JObject serverEntry)
        {
            string projectPath = Application.dataPath.Replace("/Assets", "");

            JObject root;
            if (File.Exists(configPath))
                root = JObject.Parse(File.ReadAllText(configPath));
            else
                root = new JObject();

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

        private static bool WriteMcpServersConfig(string configPath, string serversKey, JObject serverEntry)
        {
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

        private void CopyConfigToClipboard(McpSettings settings)
        {
            var serverEntry = BuildServerEntry(settings);
            var config = new JObject
            {
                ["mcpServers"] = new JObject { ["unity"] = serverEntry }
            };
            EditorGUIUtility.systemCopyBuffer = config.ToString(Newtonsoft.Json.Formatting.Indented);
            ShowNotification(new GUIContent("Copied to clipboard!"));
        }

        // ======================== Client Status Detection ========================

        private void RefreshClientStatus()
        {
            _lastClientCheckTime = EditorApplication.timeSinceStartup;
            _claudeCodeConfigured = CheckClaudeCodeConfigured();
            _cursorConfigured = CheckMcpConfigured(GetProjectConfigPath(".cursor", "mcp.json"), "mcpServers");
            _vscodeConfigured = CheckMcpConfigured(GetProjectConfigPath(".vscode", "mcp.json"), "servers");
            _windsurfConfigured = CheckMcpConfigured(GetWindsurfConfigPath(), "mcpServers");
            Repaint();
        }

        private static bool CheckClaudeCodeConfigured()
        {
            try
            {
                string path = GetClaudeCodeConfigPath();
                if (!File.Exists(path)) return false;

                var root = JObject.Parse(File.ReadAllText(path));
                string projectPath = Application.dataPath.Replace("/Assets", "");
                return root["projects"]?[projectPath]?["mcpServers"]?["unity"] != null;
            }
            catch { return false; }
        }

        private static bool CheckMcpConfigured(string configPath, string serversKey)
        {
            try
            {
                if (!File.Exists(configPath)) return false;
                var root = JObject.Parse(File.ReadAllText(configPath));
                return root[serversKey]?["unity"] != null;
            }
            catch { return false; }
        }

        // ======================== Style Initialization ========================

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 0, 4, 4)
            };

            _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                margin = new RectOffset(0, 0, 6, 3)
            };

            _boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 2, 2)
            };

            _statusBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(4, 4, 0, 0)
            };

            _clientCardStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(0, 0, 1, 1)
            };

            // Configured client card with a subtle green tint
            _clientCardConfiguredStyle = new GUIStyle(_clientCardStyle);
            var greenTex = new Texture2D(1, 1);
            greenTex.SetPixel(0, 0, EditorGUIUtility.isProSkin
                ? new Color(0.15f, 0.28f, 0.15f, 1f)
                : new Color(0.82f, 0.95f, 0.82f, 1f));
            greenTex.Apply();
            _clientCardConfiguredStyle.normal.background = greenTex;

            _wrappedLabelStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                richText = true
            };

            _versionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                margin = new RectOffset(0, 6, 0, 2)
            };

            _stylesInitialized = true;
        }
    }
}
