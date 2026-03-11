using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMcp.Editor.Core;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Window.Sections
{
    public class McpAdvancedSection
    {
        private readonly VisualElement _root;

        // Diagnostics
        private readonly Label _diagPortStatus;
        private readonly Label _diagClientCount;
        private readonly Label _diagVersion;
        private readonly Label _diagUnityVersion;

        // Server override fields
        private TextField _uvxPathField;
        private TextField _serverSourceField;
        private Toggle _devModeRefreshToggle;
        private VisualElement _uvxPathStatus;
        private VisualElement _healthContainer;

        /// <summary>Fired when uvx path, server source, or dev mode changes.</summary>
        public event Action OnServerConfigChanged;

        public McpAdvancedSection(VisualElement panel)
        {
            _root = panel;
            BuildUI();

            var settings = McpSettings.Instance;

            // Timeout
            var timeoutField = _root.Q<IntegerField>("field-timeout");
            timeoutField.value = settings.RequestTimeoutSeconds;
            timeoutField.RegisterValueChangedCallback(e =>
                settings.RequestTimeoutSeconds = Mathf.Max(1, e.newValue));

            // Log level
            var logLevelField = _root.Q<EnumField>("field-log-level");
            logLevelField.Init(settings.LogLevel);
            logLevelField.value = settings.LogLevel;
            logLevelField.RegisterValueChangedCallback(e =>
            {
                var level = (McpSettings.McpLogLevel)e.newValue;
                settings.LogLevel = level;
                McpLogger.CurrentLogLevel = (McpLogger.LogLevel)(int)level;
            });

            // Audit log
            var auditToggle = _root.Q<Toggle>("field-audit");
            auditToggle.value = settings.EnableAuditLog;
            auditToggle.RegisterValueChangedCallback(e =>
            {
                settings.EnableAuditLog = e.newValue;
                McpLogger.AuditEnabled = e.newValue;
            });

            // Max batch
            var batchField = _root.Q<IntegerField>("field-max-batch");
            batchField.value = settings.MaxBatchOperations;
            batchField.RegisterValueChangedCallback(e =>
                settings.MaxBatchOperations = Mathf.Max(1, e.newValue));

            // UVX path override
            _uvxPathField.value = settings.UvxPath;
            _uvxPathField.RegisterValueChangedCallback(e =>
            {
                settings.UvxPath = e.newValue?.Trim() ?? "";
                UpdateUvxPathStatus();
                OnServerConfigChanged?.Invoke();
            });

            // Server source override
            _serverSourceField.value = settings.ServerSourceOverride;
            _serverSourceField.RegisterValueChangedCallback(e =>
            {
                string val = e.newValue?.Trim() ?? "";
                val = ResolveServerPath(val);
                if (val != e.newValue?.Trim())
                    _serverSourceField.SetValueWithoutNotify(val);
                settings.ServerSourceOverride = val;
                OnServerConfigChanged?.Invoke();
            });

            // Dev mode force refresh
            _devModeRefreshToggle.value = settings.DevModeForceRefresh;
            _devModeRefreshToggle.RegisterValueChangedCallback(e =>
            {
                settings.DevModeForceRefresh = e.newValue;
                OnServerConfigChanged?.Invoke();
            });

            // Diagnostics
            _diagPortStatus = _root.Q<Label>("diag-port-status");
            _diagClientCount = _root.Q<Label>("diag-client-count");
            _diagVersion = _root.Q<Label>("diag-version");
            _diagUnityVersion = _root.Q<Label>("diag-unity-version");

            _root.Q<Button>("btn-copy-diag").clicked += CopyDiagnostics;

            UpdateUvxPathStatus();
            RefreshDiagnostics();
            _root.schedule.Execute(RefreshDiagnostics).Every(2000);
        }

        private void BuildUI()
        {
            // === Server Override Section ===
            var overrideBox = new VisualElement();
            overrideBox.AddToClassList("section-box");

            var overrideTitle = new Label("Server Configuration");
            overrideTitle.AddToClassList("section-title");
            overrideBox.Add(overrideTitle);

            // UVX Path
            overrideBox.Add(BuildPathField(
                "UVX Path",
                "Override path to uvx executable. Leave empty to use system PATH.",
                "field-uvx-path",
                out _uvxPathField,
                out _uvxPathStatus,
                OnBrowseUvxClicked,
                () =>
                {
                    _uvxPathField.value = "";
                    McpSettings.Instance.UvxPath = "";
                    UpdateUvxPathStatus();
                    // OnServerConfigChanged fires via RegisterValueChangedCallback
                }
            ));

            // Server source override
            overrideBox.Add(BuildPathField(
                "Server Source",
                "Override server source for uvx --from. Leave empty to use default PyPI package.\nExample: /path/to/unity-mcp/unity-server",
                "field-server-source",
                out _serverSourceField,
                out _,
                OnBrowseServerSourceClicked,
                () =>
                {
                    _serverSourceField.value = "";
                    McpSettings.Instance.ServerSourceOverride = "";
                    // OnServerConfigChanged fires via RegisterValueChangedCallback
                }
            ));

            // Dev mode force refresh toggle
            _devModeRefreshToggle = new Toggle("Dev Mode (--no-cache --refresh)") { name = "field-dev-mode" };
            _devModeRefreshToggle.tooltip = "When enabled, adds --no-cache --refresh to uvx commands to avoid stale cached builds during local development.";
            _devModeRefreshToggle.AddToClassList("field-row");
            overrideBox.Add(_devModeRefreshToggle);

            // Info text
            var infoLabel = new Label("These settings affect the command generated for MCP client configs.\nModify these before configuring clients in the Client Config tab.");
            infoLabel.style.fontSize = 10;
            infoLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            infoLabel.style.whiteSpace = WhiteSpace.Normal;
            infoLabel.style.marginTop = 4;
            overrideBox.Add(infoLabel);

            _root.Add(overrideBox);

            // === Health Check Section ===
            var healthBox = new VisualElement();
            healthBox.AddToClassList("section-box");
            healthBox.style.marginTop = 8;

            var healthTitle = new Label("Server Health Check");
            healthTitle.AddToClassList("section-title");
            healthBox.Add(healthTitle);

            // Check items container
            _healthContainer = new VisualElement();
            _healthContainer.Add(BuildHealthRow("unity-tcp", "Unity TCP Server", out _));
            _healthContainer.Add(BuildHealthRow("mcp-connected", "MCP Server Connected", out _));
            healthBox.Add(_healthContainer);

            var testBtn = new Button { text = "Run Health Check" };
            testBtn.AddToClassList("action-btn");
            testBtn.style.marginTop = 6;
            testBtn.clicked += OnTestConnectionClicked;
            healthBox.Add(testBtn);

            _root.Add(healthBox);

            // === Settings Section ===
            var settingsBox = new VisualElement();
            settingsBox.AddToClassList("section-box");
            settingsBox.style.marginTop = 8;

            var settingsTitle = new Label("Settings");
            settingsTitle.AddToClassList("section-title");
            settingsBox.Add(settingsTitle);

            var timeoutField = new IntegerField("Request Timeout (s)") { name = "field-timeout" };
            timeoutField.AddToClassList("field-row");
            settingsBox.Add(timeoutField);

            var logLevelField = new EnumField("Log Level", McpSettings.McpLogLevel.Info) { name = "field-log-level" };
            logLevelField.AddToClassList("field-row");
            settingsBox.Add(logLevelField);

            var auditToggle = new Toggle("Enable Audit Log") { name = "field-audit" };
            auditToggle.AddToClassList("field-row");
            settingsBox.Add(auditToggle);

            var batchField = new IntegerField("Max Batch Operations") { name = "field-max-batch" };
            batchField.AddToClassList("field-row");
            settingsBox.Add(batchField);

            _root.Add(settingsBox);

            // === Diagnostics Section ===
            var diagBox = new VisualElement();
            diagBox.AddToClassList("section-box");
            diagBox.style.marginTop = 8;

            var diagTitle = new Label("Diagnostics");
            diagTitle.AddToClassList("section-title");
            diagBox.Add(diagTitle);

            diagBox.Add(CreateDiagRow("Port / Status:", "diag-port-status"));
            diagBox.Add(CreateDiagRow("Connected Clients:", "diag-client-count"));
            diagBox.Add(CreateDiagRow("Server Version:", "diag-version"));
            diagBox.Add(CreateDiagRow("Unity Version:", "diag-unity-version"));
            diagBox.Add(CreateDiagRow("Reconnects:", "diag-reconnects"));
            diagBox.Add(CreateDiagRow("Last Connected:", "diag-last-connected"));

            var copyBtn = new Button { name = "btn-copy-diag", text = "Copy Diagnostics" };
            copyBtn.AddToClassList("action-btn");
            copyBtn.style.marginTop = 8;
            diagBox.Add(copyBtn);

            _root.Add(diagBox);

            // === Recent Tool Calls ===
            var logBox = new VisualElement();
            logBox.AddToClassList("section-box");
            logBox.style.marginTop = 8;

            var logTitle = new Label("Recent Tool Calls");
            logTitle.AddToClassList("section-title");
            logBox.Add(logTitle);

            var logContainer = new VisualElement { name = "call-log-container" };
            logBox.Add(logContainer);
            _root.Add(logBox);
        }

        private static VisualElement CreateDiagRow(string key, string valueName)
        {
            var row = new VisualElement();
            row.AddToClassList("diag-row");
            var keyLabel = new Label(key);
            keyLabel.AddToClassList("diag-key");
            row.Add(keyLabel);
            var valueLabel = new Label("-") { name = valueName };
            valueLabel.AddToClassList("diag-value");
            row.Add(valueLabel);
            return row;
        }

        private static VisualElement BuildPathField(
            string label, string tooltip, string fieldName,
            out TextField textField, out VisualElement statusDot,
            Action onBrowse, Action onClear)
        {
            var container = new VisualElement();
            container.style.marginBottom = 6;

            // Label row with status dot
            var labelRow = new VisualElement();
            labelRow.style.flexDirection = FlexDirection.Row;
            labelRow.style.alignItems = Align.Center;
            labelRow.style.marginBottom = 2;

            var lbl = new Label(label);
            lbl.style.fontSize = 11;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            labelRow.Add(lbl);

            statusDot = new VisualElement();
            statusDot.style.width = 8;
            statusDot.style.height = 8;
            statusDot.style.borderTopLeftRadius = statusDot.style.borderTopRightRadius =
                statusDot.style.borderBottomLeftRadius = statusDot.style.borderBottomRightRadius = 4;
            statusDot.style.marginLeft = 6;
            statusDot.style.backgroundColor = new Color(0.6f, 0.6f, 0.6f);
            labelRow.Add(statusDot);

            container.Add(labelRow);

            // Input row: textfield + browse + clear
            var inputRow = new VisualElement();
            inputRow.style.flexDirection = FlexDirection.Row;
            inputRow.style.alignItems = Align.Center;

            textField = new TextField { name = fieldName };
            textField.tooltip = tooltip;
            textField.style.flexGrow = 1;
            textField.style.flexShrink = 1;
            textField.style.minWidth = 0;
            inputRow.Add(textField);

            var browseBtn = new Button { text = "..." };
            browseBtn.tooltip = "Browse...";
            browseBtn.style.width = 28;
            browseBtn.style.flexShrink = 0;
            browseBtn.style.marginLeft = 4;
            browseBtn.clicked += onBrowse;
            inputRow.Add(browseBtn);

            var clearBtn = new Button { text = "×" };
            clearBtn.tooltip = "Clear";
            clearBtn.style.width = 24;
            clearBtn.style.flexShrink = 0;
            clearBtn.style.marginLeft = 2;
            clearBtn.clicked += onClear;
            inputRow.Add(clearBtn);

            container.Add(inputRow);
            return container;
        }

        private void OnBrowseUvxClicked()
        {
#if UNITY_EDITOR_OSX
            string suggested = "/opt/homebrew/bin";
#else
            string suggested = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
#endif
            string picked = EditorUtility.OpenFilePanel("Select uvx Executable", suggested, "");
            if (!string.IsNullOrEmpty(picked))
            {
                _uvxPathField.value = picked;
                McpSettings.Instance.UvxPath = picked;
                UpdateUvxPathStatus();
                // OnServerConfigChanged fires via RegisterValueChangedCallback
            }
        }

        private void OnBrowseServerSourceClicked()
        {
            string picked = EditorUtility.OpenFolderPanel("Select Server folder (containing pyproject.toml)", "", "");
            if (!string.IsNullOrEmpty(picked))
            {
                picked = ResolveServerPath(picked);
                _serverSourceField.value = picked;
                McpSettings.Instance.ServerSourceOverride = picked;
                // OnServerConfigChanged fires via RegisterValueChangedCallback
            }
        }

        private static string ResolveServerPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            // If already points to a directory with pyproject.toml, it's correct
            if (File.Exists(Path.Combine(path, "pyproject.toml")))
                return path;

            // Check common sub-directories: "unity-server", "Server", "server"
            string[] subDirs = { "unity-server", "Server", "server" };
            foreach (var sub in subDirs)
            {
                string subPath = Path.Combine(path, sub);
                if (File.Exists(Path.Combine(subPath, "pyproject.toml")))
                    return subPath;
            }

            return path;
        }

        private void UpdateUvxPathStatus()
        {
            string path = McpSettings.Instance.UvxPath;
            if (string.IsNullOrEmpty(path))
            {
                // Using system PATH — show neutral
                _uvxPathStatus.style.backgroundColor = new Color(0.3f, 0.7f, 0.3f); // green
                _uvxPathField.SetValueWithoutNotify("");
            }
            else if (File.Exists(path))
            {
                _uvxPathStatus.style.backgroundColor = new Color(0.3f, 0.7f, 0.3f); // green
            }
            else
            {
                _uvxPathStatus.style.backgroundColor = new Color(0.9f, 0.3f, 0.3f); // red
            }
        }

        private static VisualElement BuildHealthRow(string name, string label, out Label statusLabel)
        {
            var row = new VisualElement { name = name };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;

            var dot = new VisualElement { name = "dot" };
            dot.style.width = 10;
            dot.style.height = 10;
            dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius =
                dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = 5;
            dot.style.marginRight = 8;
            dot.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f); // gray = untested
            row.Add(dot);

            var lbl = new Label(label);
            lbl.style.minWidth = 160;
            lbl.style.fontSize = 11;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(lbl);

            statusLabel = new Label("—") { name = "status" };
            statusLabel.style.fontSize = 11;
            statusLabel.style.flexGrow = 1;
            statusLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            row.Add(statusLabel);

            return row;
        }

        private void SetHealthStatus(string rowName, bool ok, string text)
        {
            var row = _healthContainer.Q(rowName);
            if (row == null) return;
            var dot = row.Q("dot");
            var status = row.Q<Label>("status");
            if (dot != null)
                dot.style.backgroundColor = ok
                    ? new Color(0.3f, 0.8f, 0.3f)
                    : new Color(0.9f, 0.3f, 0.3f);
            if (status != null)
                status.text = text;
        }

        private void SetHealthPending(string rowName, string text)
        {
            var row = _healthContainer.Q(rowName);
            if (row == null) return;
            var dot = row.Q("dot");
            var status = row.Q<Label>("status");
            if (dot != null)
                dot.style.backgroundColor = new Color(1f, 0.6f, 0f); // orange
            if (status != null)
                status.text = text;
        }

        private void OnTestConnectionClicked()
        {
            SetHealthPending("unity-tcp", "Checking...");
            SetHealthPending("mcp-connected", "Checking...");

            var settings = McpSettings.Instance;
            int port = settings.Port;

            // 1. Unity TCP Server
            var transport = McpServer.Transport;
            bool tcpRunning = transport?.IsRunning ?? false;
            if (tcpRunning)
                SetHealthStatus("unity-tcp", true, $"Running on port {port}");
            else
                SetHealthStatus("unity-tcp", false, $"Not running (port {port})");

            // 2. MCP Server Connected (Python server connects to Unity as a TCP client)
            int clientCount = transport?.ClientCount ?? 0;
            if (clientCount > 0)
                SetHealthStatus("mcp-connected", true, $"{clientCount} client(s) connected");
            else
                SetHealthStatus("mcp-connected", false, "No MCP server connected");

        }

        private void RefreshDiagnostics()
        {
            var transport = McpServer.Transport;
            bool running = transport?.IsRunning ?? false;
            int port = transport?.Port ?? McpSettings.Instance.Port;
            int clients = transport?.ClientCount ?? 0;

            _diagPortStatus.text = running ? $"{port} (Running)" : $"{port} (Stopped)";
            _diagClientCount.text = clients.ToString();
            _diagVersion.text = $"v{McpConst.ServerVersion}";
            _diagUnityVersion.text = Application.unityVersion;

            var diagReconnects = _root.Q<Label>("diag-reconnects");
            if (diagReconnects != null)
                diagReconnects.text = (transport?.ReconnectCount ?? 0).ToString();

            var diagLastConnected = _root.Q<Label>("diag-last-connected");
            if (diagLastConnected != null)
                diagLastConnected.text = transport?.LastConnectedAt?.ToString("HH:mm:ss") ?? "—";

            // Update banner
            if (PackageUpdateChecker.HasUpdate && _root.Q("update-banner") == null)
            {
                var banner = new VisualElement { name = "update-banner" };
                banner.style.backgroundColor = new Color(0.85f, 0.65f, 0.0f, 0.3f);
                banner.style.borderBottomLeftRadius = banner.style.borderBottomRightRadius =
                    banner.style.borderTopLeftRadius = banner.style.borderTopRightRadius = 4;
                banner.style.paddingTop = banner.style.paddingBottom = 4;
                banner.style.paddingLeft = banner.style.paddingRight = 8;
                banner.style.marginBottom = 8;
                var label = new Label($"Update available: v{PackageUpdateChecker.LatestVersion} (current: v{McpConst.ServerVersion})");
                label.style.color = new Color(0.9f, 0.7f, 0.0f);
                banner.Add(label);
                _root.Insert(0, banner);
            }

            // Refresh call log
            var logContainer = _root.Q("call-log-container");
            if (logContainer != null)
            {
                logContainer.Clear();
                var history = ToolCallLogger.GetHistory();
                if (history.Count == 0)
                {
                    var msg = new Label("No tool calls recorded yet.");
                    msg.style.color = new Color(0.5f, 0.5f, 0.5f);
                    logContainer.Add(msg);
                }
                else
                {
                    for (int i = history.Count - 1; i >= 0; i--)
                    {
                        var record = history[i];
                        var row = new VisualElement();
                        row.AddToClassList("diag-row");
                        var nameLabel = new Label(record.ToolName);
                        nameLabel.AddToClassList("diag-key");
                        nameLabel.style.width = 200;
                        row.Add(nameLabel);
                        var statusLabel = new Label($"{record.DurationMs}ms {(record.Success ? "OK" : "ERR")}");
                        statusLabel.AddToClassList("diag-value");
                        statusLabel.style.color = record.Success
                            ? new Color(0.3f, 0.8f, 0.3f)
                            : new Color(0.9f, 0.3f, 0.3f);
                        row.Add(statusLabel);
                        logContainer.Add(row);
                    }
                }
            }
        }

        private void CopyDiagnostics()
        {
            var transport = McpServer.Transport;
            bool running = transport?.IsRunning ?? false;
            int port = transport?.Port ?? McpSettings.Instance.Port;
            int clients = transport?.ClientCount ?? 0;
            var settings = McpSettings.Instance;

            var sb = new StringBuilder();
            sb.AppendLine("Unity MCP Diagnostics");
            sb.AppendLine("=====================");
            sb.AppendLine($"Server Version: {McpConst.ServerVersion}");
            sb.AppendLine($"Unity Version: {Application.unityVersion}");
            sb.AppendLine($"Platform: {Application.platform}");
            sb.AppendLine($"Port: {port}");
            sb.AppendLine($"Status: {(running ? "Running" : "Stopped")}");
            sb.AppendLine($"Connected Clients: {clients}");
            sb.AppendLine($"Transport: {settings.Transport}");
            sb.AppendLine($"Auto Start: {settings.AutoStart}");
            sb.AppendLine($"Log Level: {settings.LogLevel}");
            sb.AppendLine($"Request Timeout: {settings.RequestTimeoutSeconds}s");
            sb.AppendLine($"Max Batch Operations: {settings.MaxBatchOperations}");
            sb.AppendLine($"UVX Path: {(string.IsNullOrEmpty(settings.UvxPath) ? "(system PATH)" : settings.UvxPath)}");
            sb.AppendLine($"Server Source: {(string.IsNullOrEmpty(settings.ServerSourceOverride) ? "(PyPI default)" : settings.ServerSourceOverride)}");
            sb.AppendLine($"Dev Mode: {settings.DevModeForceRefresh}");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            EditorGUIUtility.systemCopyBuffer = sb.ToString();
        }
    }
}
