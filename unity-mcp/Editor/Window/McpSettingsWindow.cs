using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        // Bridge fields removed — window will be fully rewritten in UI Toolkit

        // Client config cache (refreshed periodically)
        private double _lastClientCheckTime;
        private const double ClientCheckInterval = 2.0;
        private bool _claudeCodeConfigured;
        private bool _cursorConfigured;
        private bool _vscodeConfigured;
        private bool _windsurfConfigured;

        [MenuItem("Window/Unity MCP", priority = 0)]
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

            // Server Settings
            EditorGUILayout.LabelField("Server Settings", _subHeaderStyle);
            EditorGUILayout.BeginVertical(_boxStyle);
            {
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

            DrawTransportSettings(settings);

            EditorGUILayout.Space(4);

            // Advanced
            DrawAdvanced(settings);
        }

        private void DrawTransportSettings(McpSettings settings)
        {
            EditorGUILayout.LabelField("Transport", _subHeaderStyle);
            EditorGUILayout.BeginVertical(_boxStyle);
            {
                EditorGUI.BeginChangeCheck();
                var transport = (McpSettings.TransportMode)EditorGUILayout.EnumPopup(
                    new GUIContent("Transport", "How MCP clients communicate with the Python server."),
                    settings.Transport);
                if (EditorGUI.EndChangeCheck())
                    settings.Transport = transport;

                if (settings.Transport == McpSettings.TransportMode.StreamableHttp)
                {
                    EditorGUI.BeginChangeCheck();
                    int httpPort = EditorGUILayout.IntField(
                        new GUIContent("HTTP Port", "Port for the Streamable HTTP server."),
                        settings.HttpPort);
                    if (EditorGUI.EndChangeCheck())
                        settings.HttpPort = Mathf.Max(1, httpPort);

                    EditorGUILayout.HelpBox(
                        "Streamable HTTP mode: run the server manually with\n" +
                        $"  UNITY_MCP_PORT={settings.Port} UNITY_MCP_TRANSPORT=streamable-http UNITY_MCP_HTTP_PORT={settings.HttpPort} uvx unity-mcp-server\n\n" +
                        "Then configure your MCP client with the URL shown in the Clients tab.",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Stdio mode: MCP clients launch the Python server automatically.\n" +
                        "Use the Clients tab to generate the configuration.",
                        MessageType.Info);
                }
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

                EditorGUI.BeginChangeCheck();
                int maxBatch = EditorGUILayout.IntField(
                    new GUIContent("Max Batch Operations", "Maximum number of operations allowed in a single batch_execute call."),
                    settings.MaxBatchOperations);
                if (EditorGUI.EndChangeCheck())
                    settings.MaxBatchOperations = Mathf.Max(1, maxBatch);
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
                    var clients = transport.ConnectedClients;
                    foreach (var info in clients)
                    {
                        EditorGUILayout.BeginHorizontal();
                        {
                            var oldColor = GUI.color;
                            GUI.color = new Color(0.2f, 0.9f, 0.3f);
                            GUILayout.Label("\u25CF", GUILayout.Width(14));
                            GUI.color = oldColor;

                            string version = string.IsNullOrEmpty(info.Version) ? "" : $" v{info.Version}";
                            string duration = FormatDuration(DateTime.Now - info.ConnectedAt);
                            EditorGUILayout.LabelField($"{info.Name}{version}",
                                EditorStyles.boldLabel, GUILayout.MinWidth(120));
                            var dimStyle = new GUIStyle(EditorStyles.miniLabel)
                            {
                                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
                            };
                            EditorGUILayout.LabelField($"{info.Endpoint}  ·  {duration}",
                                dimStyle, GUILayout.MinWidth(100));
                        }
                        EditorGUILayout.EndHorizontal();
                    }
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

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalSeconds < 60) return $"{(int)duration.TotalSeconds}s";
            if (duration.TotalMinutes < 60) return $"{(int)duration.TotalMinutes}m";
            if (duration.TotalHours < 24) return $"{(int)duration.TotalHours}h {duration.Minutes}m";
            return $"{(int)duration.TotalDays}d {duration.Hours}h";
        }

        // ======================== Tools Tab ========================

        private bool _showToolsFoldout = true;
        private bool _showResourcesFoldout = true;
        private bool _showPromptsFoldout = true;
        private bool _allToolsSelected = true;
        // Per-group foldout state (group name → expanded)
        private readonly Dictionary<string, bool> _groupFoldouts = new();

        private void DrawToolsTab()
        {
            var registry = McpServer.Registry;
            if (registry == null)
            {
                EditorGUILayout.HelpBox("Server not initialized. Tools will appear after the server starts.", MessageType.Info);
                return;
            }

            // Summary badges — stretch to fill width
            EditorGUILayout.BeginHorizontal();
            DrawCountBadge("Tools", registry.ToolCount);
            DrawCountBadge("Resources", registry.ResourceCount);
            DrawCountBadge("Prompts", registry.PromptCount);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            DrawToolsSection(registry);
            EditorGUILayout.Space(4);
            DrawResourcesSection(registry);
            EditorGUILayout.Space(4);
            DrawPromptsSection(registry);
        }

        private void DrawToolsSection(ToolRegistry registry)
        {
            _showToolsFoldout = EditorGUILayout.Foldout(_showToolsFoldout, $"Tools ({registry.ToolCount})", true, EditorStyles.foldoutHeader);
            if (!_showToolsFoldout) return;

            // Select All
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _allToolsSelected = EditorGUILayout.ToggleLeft("Select All", _allToolsSelected, EditorStyles.miniLabel);
            if (EditorGUI.EndChangeCheck())
                registry.SetAllToolsEnabled(_allToolsSelected);
            EditorGUILayout.EndHorizontal();

            // Group tools by category
            var allEntries = registry.GetAllToolEntries().ToList();
            var builtInByGroup = new SortedDictionary<string, List<(string name, string description)>>();
            var customTools = new List<(string name, string description)>();

            foreach (var (name, description, group, builtIn) in allEntries)
            {
                if (!builtIn)
                {
                    customTools.Add((name, description));
                    continue;
                }
                string groupKey = string.IsNullOrEmpty(group) ? "Other" : CapitalizeFirst(group);
                if (!builtInByGroup.ContainsKey(groupKey))
                    builtInByGroup[groupKey] = new List<(string, string)>();
                builtInByGroup[groupKey].Add((name, description));
            }

            // Draw built-in groups
            foreach (var kv in builtInByGroup)
            {
                string groupName = kv.Key;
                var tools = kv.Value;

                if (!_groupFoldouts.ContainsKey(groupName))
                    _groupFoldouts[groupName] = true;

                _groupFoldouts[groupName] = EditorGUILayout.Foldout(
                    _groupFoldouts[groupName],
                    $"{groupName} ({tools.Count})",
                    true);

                if (_groupFoldouts[groupName])
                {
                    EditorGUILayout.BeginVertical(_boxStyle);
                    foreach (var (name, description) in tools)
                        DrawToolRow(registry, name, description);
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.Space(2);
            }

            // Draw custom tools
            if (customTools.Count > 0)
            {
                string customKey = "Custom";
                if (!_groupFoldouts.ContainsKey(customKey))
                    _groupFoldouts[customKey] = true;

                _groupFoldouts[customKey] = EditorGUILayout.Foldout(
                    _groupFoldouts[customKey],
                    $"Custom ({customTools.Count})",
                    true);

                if (_groupFoldouts[customKey])
                {
                    EditorGUILayout.BeginVertical(_boxStyle);
                    foreach (var (name, description) in customTools)
                        DrawToolRow(registry, name, description);
                    EditorGUILayout.EndVertical();
                }
            }

            if (builtInByGroup.Count == 0 && customTools.Count == 0)
            {
                EditorGUILayout.BeginVertical(_boxStyle);
                EditorGUILayout.LabelField("No tools registered.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
            }
        }

        private static string CapitalizeFirst(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        private static void DrawToolRow(ToolRegistry registry, string name, string description)
        {
            bool enabled = registry.IsToolEnabled(name);
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUI.BeginChangeCheck();
                bool newEnabled = EditorGUILayout.Toggle(enabled, GUILayout.Width(16));
                if (EditorGUI.EndChangeCheck())
                    registry.SetToolEnabled(name, newEnabled);

                // Name (bold when enabled, grey when disabled)
                var nameStyle = enabled ? EditorStyles.miniLabel : new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } };
                EditorGUILayout.LabelField(name, nameStyle, GUILayout.Width(200));

                // Description (truncated, shown as tooltip on hover)
                if (!string.IsNullOrEmpty(description))
                {
                    var descStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = new Color(0.55f, 0.55f, 0.55f) }
                    };
                    EditorGUILayout.LabelField(new GUIContent(description, description), descStyle);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawResourcesSection(ToolRegistry registry)
        {
            _showResourcesFoldout = EditorGUILayout.Foldout(_showResourcesFoldout, $"Resources ({registry.ResourceCount})", true, EditorStyles.foldoutHeader);
            if (!_showResourcesFoldout) return;

            EditorGUILayout.BeginVertical(_boxStyle);
            {
                bool hasBuiltIn = false, hasCustom = false;
                foreach (var (name, description, builtIn) in registry.GetAllResourceEntries())
                {
                    if (builtIn) hasBuiltIn = true; else hasCustom = true;
                }

                if (hasBuiltIn) DrawResourceGroup(registry, "Built-in", true);
                if (hasCustom)
                {
                    if (hasBuiltIn) EditorGUILayout.Space(4);
                    DrawResourceGroup(registry, "Custom", false);
                }

                if (!hasBuiltIn && !hasCustom)
                    EditorGUILayout.LabelField("No resources registered.", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawResourceGroup(ToolRegistry registry, string header, bool filterBuiltIn)
        {
            EditorGUILayout.LabelField(header, _subHeaderStyle);
            foreach (var (name, description, builtIn) in registry.GetAllResourceEntries())
            {
                if (builtIn != filterBuiltIn) continue;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(name, EditorStyles.miniLabel, GUILayout.Width(200));
                if (!string.IsNullOrEmpty(description))
                    EditorGUILayout.LabelField(new GUIContent(description, description),
                        new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.55f, 0.55f, 0.55f) } });
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawPromptsSection(ToolRegistry registry)
        {
            _showPromptsFoldout = EditorGUILayout.Foldout(_showPromptsFoldout, $"Prompts ({registry.PromptCount})", true, EditorStyles.foldoutHeader);
            if (!_showPromptsFoldout) return;

            EditorGUILayout.BeginVertical(_boxStyle);
            {
                bool hasBuiltIn = false, hasCustom = false;
                foreach (var (name, description, builtIn) in registry.GetAllPromptEntries())
                {
                    if (builtIn) hasBuiltIn = true; else hasCustom = true;
                }

                if (hasBuiltIn) DrawPromptGroup(registry, "Built-in", true);
                if (hasCustom)
                {
                    if (hasBuiltIn) EditorGUILayout.Space(4);
                    DrawPromptGroup(registry, "Custom", false);
                }

                if (!hasBuiltIn && !hasCustom)
                    EditorGUILayout.LabelField("No prompts registered.", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawPromptGroup(ToolRegistry registry, string header, bool filterBuiltIn)
        {
            EditorGUILayout.LabelField(header, _subHeaderStyle);
            foreach (var (name, description, builtIn) in registry.GetAllPromptEntries())
            {
                if (builtIn != filterBuiltIn) continue;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(name, EditorStyles.miniLabel, GUILayout.Width(200));
                if (!string.IsNullOrEmpty(description))
                    EditorGUILayout.LabelField(new GUIContent(description, description),
                        new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.55f, 0.55f, 0.55f) } });
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawCountBadge(string label, int count)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(count.ToString(),
                new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 18 });
            EditorGUILayout.LabelField(label,
                new GUIStyle(EditorStyles.centeredGreyMiniLabel));
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
            if (settings.Transport == McpSettings.TransportMode.StreamableHttp)
            {
                return new JObject
                {
                    ["type"] = "http",
                    ["url"] = $"http://127.0.0.1:{settings.HttpPort}/mcp"
                };
            }

            // Stdio: MCP client launches uvx unity-mcp-server
            var entry = new JObject
            {
                ["type"] = "stdio",
                ["command"] = "uvx",
                ["args"] = new JArray { "unity-mcp-server" }
            };
            if (settings.Port != PortResolver.DefaultPort)
                entry["env"] = new JObject { ["UNITY_MCP_PORT"] = settings.Port.ToString() };
            return entry;
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
            return Path.Combine(projectRoot, folder, file);
        }

        private static string GetWindsurfConfigPath()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".codeium", "windsurf", "mcp_config.json");
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
            // Ensure directory exists only when actually writing
            string dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

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
