using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
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
        private VisualElement _healthIndicator;
        private Label _healthStatusLabel;

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
            });

            // Dev mode force refresh
            _devModeRefreshToggle.value = settings.DevModeForceRefresh;
            _devModeRefreshToggle.RegisterValueChangedCallback(e =>
                settings.DevModeForceRefresh = e.newValue);

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
            var uvxRow = new VisualElement();
            uvxRow.style.flexDirection = FlexDirection.Row;
            uvxRow.style.alignItems = Align.Center;
            uvxRow.style.marginBottom = 4;

            _uvxPathField = new TextField("UVX Path") { name = "field-uvx-path" };
            _uvxPathField.tooltip = "Override path to uvx executable. Leave empty to use system PATH.";
            _uvxPathField.style.flexGrow = 1;
            uvxRow.Add(_uvxPathField);

            _uvxPathStatus = new VisualElement();
            _uvxPathStatus.style.width = 10;
            _uvxPathStatus.style.height = 10;
            _uvxPathStatus.style.borderTopLeftRadius = _uvxPathStatus.style.borderTopRightRadius =
                _uvxPathStatus.style.borderBottomLeftRadius = _uvxPathStatus.style.borderBottomRightRadius = 5;
            _uvxPathStatus.style.marginLeft = 4;
            _uvxPathStatus.style.marginRight = 4;
            uvxRow.Add(_uvxPathStatus);

            var browseUvxBtn = new Button { text = "..." };
            browseUvxBtn.tooltip = "Browse for uvx executable";
            browseUvxBtn.style.width = 30;
            browseUvxBtn.clicked += OnBrowseUvxClicked;
            uvxRow.Add(browseUvxBtn);

            var clearUvxBtn = new Button { text = "×" };
            clearUvxBtn.tooltip = "Clear override and use system PATH";
            clearUvxBtn.style.width = 24;
            clearUvxBtn.clicked += () =>
            {
                _uvxPathField.value = "";
                McpSettings.Instance.UvxPath = "";
                UpdateUvxPathStatus();
            };
            uvxRow.Add(clearUvxBtn);

            overrideBox.Add(uvxRow);

            // Server source override
            var srcRow = new VisualElement();
            srcRow.style.flexDirection = FlexDirection.Row;
            srcRow.style.alignItems = Align.Center;
            srcRow.style.marginBottom = 4;

            _serverSourceField = new TextField("Server Source") { name = "field-server-source" };
            _serverSourceField.tooltip = "Override server source for uvx --from. Leave empty to use default PyPI package.\nExample: /path/to/unity-mcp/unity-server";
            _serverSourceField.style.flexGrow = 1;
            srcRow.Add(_serverSourceField);

            var browseSrcBtn = new Button { text = "..." };
            browseSrcBtn.tooltip = "Select local server source folder (containing pyproject.toml)";
            browseSrcBtn.style.width = 30;
            browseSrcBtn.clicked += OnBrowseServerSourceClicked;
            srcRow.Add(browseSrcBtn);

            var clearSrcBtn = new Button { text = "×" };
            clearSrcBtn.tooltip = "Clear override and use default PyPI package";
            clearSrcBtn.style.width = 24;
            clearSrcBtn.clicked += () =>
            {
                _serverSourceField.value = "";
                McpSettings.Instance.ServerSourceOverride = "";
            };
            srcRow.Add(clearSrcBtn);

            overrideBox.Add(srcRow);

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

            var healthRow = new VisualElement();
            healthRow.style.flexDirection = FlexDirection.Row;
            healthRow.style.alignItems = Align.Center;

            _healthIndicator = new VisualElement();
            _healthIndicator.style.width = 12;
            _healthIndicator.style.height = 12;
            _healthIndicator.style.borderTopLeftRadius = _healthIndicator.style.borderTopRightRadius =
                _healthIndicator.style.borderBottomLeftRadius = _healthIndicator.style.borderBottomRightRadius = 6;
            _healthIndicator.style.marginRight = 8;
            _healthIndicator.style.backgroundColor = new Color(0.6f, 0.6f, 0.6f); // gray = unknown
            healthRow.Add(_healthIndicator);

            _healthStatusLabel = new Label("Not tested");
            _healthStatusLabel.style.flexGrow = 1;
            healthRow.Add(_healthStatusLabel);

            var testBtn = new Button { text = "Test Connection" };
            testBtn.AddToClassList("action-btn");
            testBtn.clicked += OnTestConnectionClicked;
            healthRow.Add(testBtn);

            healthBox.Add(healthRow);
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

        private async void OnTestConnectionClicked()
        {
            _healthStatusLabel.text = "Testing...";
            _healthIndicator.style.backgroundColor = new Color(1f, 0.6f, 0f); // orange

            var settings = McpSettings.Instance;
            int port = settings.Port;

            try
            {
                bool tcpOk = await Task.Run(() =>
                {
                    try
                    {
                        using var client = new TcpClient();
                        var result = client.BeginConnect("127.0.0.1", port, null, null);
                        bool connected = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3));
                        if (connected && client.Connected)
                        {
                            client.EndConnect(result);
                            return true;
                        }
                        return false;
                    }
                    catch { return false; }
                });

                if (tcpOk)
                {
                    var transport = McpServer.Transport;
                    int clients = transport?.ClientCount ?? 0;
                    _healthStatusLabel.text = $"Connected — TCP:{port}, {clients} client(s)";
                    _healthIndicator.style.backgroundColor = new Color(0.3f, 0.8f, 0.3f); // green
                }
                else
                {
                    _healthStatusLabel.text = $"Cannot connect to TCP:{port}. Is the server running?";
                    _healthIndicator.style.backgroundColor = new Color(0.9f, 0.3f, 0.3f); // red
                }
            }
            catch (Exception e)
            {
                _healthStatusLabel.text = $"Error: {e.Message}";
                _healthIndicator.style.backgroundColor = new Color(0.9f, 0.3f, 0.3f); // red
            }
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
