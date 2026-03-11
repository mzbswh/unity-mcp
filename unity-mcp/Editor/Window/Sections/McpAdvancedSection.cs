using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMcp.Editor.Core;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Window.Sections
{
    public class McpAdvancedSection
    {
        private readonly VisualElement _root;
        private readonly Label _diagPortStatus;
        private readonly Label _diagClientCount;
        private readonly Label _diagVersion;
        private readonly Label _diagUnityVersion;

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

            // Diagnostics
            _diagPortStatus = _root.Q<Label>("diag-port-status");
            _diagClientCount = _root.Q<Label>("diag-client-count");
            _diagVersion = _root.Q<Label>("diag-version");
            _diagUnityVersion = _root.Q<Label>("diag-unity-version");

            _root.Q<Button>("btn-copy-diag").clicked += CopyDiagnostics;

            RefreshDiagnostics();
            _root.schedule.Execute(RefreshDiagnostics).Every(2000);
        }

        private void BuildUI()
        {
            // Settings
            var settingsBox = new VisualElement();
            settingsBox.AddToClassList("section-box");

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

            // Diagnostics
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

            var copyBtn = new Button { name = "btn-copy-diag", text = "Copy Diagnostics" };
            copyBtn.AddToClassList("action-btn");
            copyBtn.style.marginTop = 8;
            diagBox.Add(copyBtn);

            _root.Add(diagBox);
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
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            EditorGUIUtility.systemCopyBuffer = sb.ToString();
        }
    }
}
