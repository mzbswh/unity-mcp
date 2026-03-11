using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMcp.Editor.Core;
using UnityMcp.Editor.Window.ClientConfig;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Window.Sections
{
    public class McpClientConfigSection
    {
        private readonly VisualElement _root;
        private DropdownField _clientDropdown;
        private VisualElement _statusDot;
        private Label _statusLabel;
        private Button _configureBtn;
        private Label _configPathLabel;
        private TextField _configJsonField;
        private Label _installStepsLabel;
        private int _selectedIndex;
        private McpStatus _currentStatus;

        public McpClientConfigSection(VisualElement panel)
        {
            _root = panel;
            BuildUI();
            UpdateSelectedClient();
        }

        private void BuildUI()
        {
            // Client selector
            var selectorBox = new VisualElement();
            selectorBox.AddToClassList("section-box");

            var titleLabel = new Label("Client Configuration");
            titleLabel.AddToClassList("section-title");
            selectorBox.Add(titleLabel);

            // Client dropdown row
            var dropdownRow = new VisualElement();
            dropdownRow.AddToClassList("field-row");
            dropdownRow.style.flexDirection = FlexDirection.Row;
            dropdownRow.style.alignItems = Align.Center;

            var dropdownLabel = new Label("Client");
            dropdownLabel.style.minWidth = 140;
            dropdownRow.Add(dropdownLabel);

            var clientNames = ClientRegistry.All.Select(p => p.DisplayName).ToList();
            _clientDropdown = new DropdownField { choices = clientNames };
            _clientDropdown.style.flexGrow = 1;
            _clientDropdown.style.flexShrink = 1;
            _clientDropdown.style.minWidth = 0;
            _clientDropdown.style.overflow = Overflow.Hidden;
            if (clientNames.Count > 0)
                _clientDropdown.index = 0;
            _clientDropdown.RegisterValueChangedCallback(_ =>
            {
                _selectedIndex = _clientDropdown.index;
                UpdateSelectedClient();
            });
            dropdownRow.Add(_clientDropdown);

            selectorBox.Add(dropdownRow);

            // Status row
            var statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;
            statusRow.style.marginTop = 6;
            statusRow.style.marginBottom = 6;

            _statusDot = new VisualElement();
            _statusDot.AddToClassList("status-dot");
            _statusDot.AddToClassList("status-gray");
            statusRow.Add(_statusDot);

            _statusLabel = new Label("Not Configured");
            _statusLabel.AddToClassList("status-label");
            _statusLabel.style.flexGrow = 1;
            statusRow.Add(_statusLabel);

            _configureBtn = new Button(OnConfigureClicked) { text = "Configure" };
            _configureBtn.AddToClassList("action-btn");
            _configureBtn.AddToClassList("action-btn-start");
            statusRow.Add(_configureBtn);

            selectorBox.Add(statusRow);
            _root.Add(selectorBox);

            // Manual configuration section
            var manualBox = new VisualElement();
            manualBox.AddToClassList("section-box");

            var manualTitle = new Label("Manual Configuration");
            manualTitle.AddToClassList("section-title");
            manualBox.Add(manualTitle);

            // Config path
            var pathRow = new VisualElement();
            pathRow.style.flexDirection = FlexDirection.Row;
            pathRow.style.alignItems = Align.Center;
            pathRow.style.marginBottom = 4;

            var pathLabel = new Label("Config Path:");
            pathLabel.style.minWidth = 90;
            pathLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            pathLabel.style.fontSize = 11;
            pathRow.Add(pathLabel);

            _configPathLabel = new Label("");
            _configPathLabel.style.fontSize = 11;
            _configPathLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            _configPathLabel.style.flexGrow = 1;
            _configPathLabel.style.overflow = Overflow.Hidden;
            pathRow.Add(_configPathLabel);

            var copyPathBtn = new Button(OnCopyPathClicked) { text = "Copy" };
            copyPathBtn.style.fontSize = 11;
            copyPathBtn.style.height = 20;
            pathRow.Add(copyPathBtn);

            var openFileBtn = new Button(OnOpenFileClicked) { text = "Open" };
            openFileBtn.style.fontSize = 11;
            openFileBtn.style.height = 20;
            pathRow.Add(openFileBtn);

            manualBox.Add(pathRow);

            // Config JSON
            var jsonLabel = new Label("Configuration:");
            jsonLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            jsonLabel.style.fontSize = 11;
            jsonLabel.style.marginBottom = 4;
            manualBox.Add(jsonLabel);

            var jsonRow = new VisualElement();
            jsonRow.style.flexDirection = FlexDirection.Row;
            jsonRow.style.marginBottom = 6;

            _configJsonField = new TextField { multiline = true, isReadOnly = true };
            _configJsonField.style.flexGrow = 1;
            _configJsonField.style.minHeight = 80;
            _configJsonField.style.fontSize = 10;
            jsonRow.Add(_configJsonField);

            var copyJsonBtn = new Button(OnCopyJsonClicked) { text = "Copy" };
            copyJsonBtn.style.fontSize = 11;
            copyJsonBtn.style.height = 30;
            copyJsonBtn.style.alignSelf = Align.FlexStart;
            copyJsonBtn.style.marginLeft = 4;
            jsonRow.Add(copyJsonBtn);

            manualBox.Add(jsonRow);

            // Installation steps
            var stepsTitle = new Label("Installation Steps:");
            stepsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            stepsTitle.style.fontSize = 11;
            stepsTitle.style.marginBottom = 4;
            manualBox.Add(stepsTitle);

            _installStepsLabel = new Label("");
            _installStepsLabel.style.fontSize = 11;
            _installStepsLabel.style.whiteSpace = WhiteSpace.Normal;
            _installStepsLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            manualBox.Add(_installStepsLabel);

            _root.Add(manualBox);

            // Configure All button
            var configAllBtn = new Button(OnConfigureAllClicked) { text = "Configure All Detected Clients" };
            configAllBtn.AddToClassList("action-btn");
            configAllBtn.style.marginTop = 4;
            _root.Add(configAllBtn);
        }

        private void UpdateSelectedClient()
        {
            if (_selectedIndex < 0 || _selectedIndex >= ClientRegistry.All.Length)
                return;

            var profile = ClientRegistry.All[_selectedIndex];
            var settings = McpSettings.Instance;
            var writer = ClientRegistry.GetWriter(profile.Strategy);
            string transport = settings.Transport == McpSettings.TransportMode.StreamableHttp
                ? "streamable-http" : "stdio";
            _currentStatus = writer.CheckStatus(profile, settings.Port, transport);

            // Update status
            _statusDot.RemoveFromClassList("status-green");
            _statusDot.RemoveFromClassList("status-red");
            _statusDot.RemoveFromClassList("status-gray");
            _statusDot.RemoveFromClassList("status-yellow");
            _configureBtn.RemoveFromClassList("action-btn-start");
            _configureBtn.RemoveFromClassList("action-btn-stop");

            switch (_currentStatus)
            {
                case McpStatus.Configured:
                    _statusDot.AddToClassList("status-green");
                    _statusLabel.text = "Configured";
                    _statusLabel.style.color = new Color(0.6f, 0.8f, 0.6f);
                    _configureBtn.text = "Unconfigure";
                    _configureBtn.AddToClassList("action-btn-stop");
                    break;
                case McpStatus.NeedsUpdate:
                    _statusDot.AddToClassList("status-yellow");
                    _statusLabel.text = "Needs Update";
                    _statusLabel.style.color = new Color(1f, 0.6f, 0f);
                    _configureBtn.text = "Configure";
                    _configureBtn.AddToClassList("action-btn-start");
                    break;
                default:
                    _statusDot.AddToClassList("status-gray");
                    _statusLabel.text = "Not Configured";
                    _statusLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                    _configureBtn.text = "Configure";
                    _configureBtn.AddToClassList("action-btn-start");
                    break;
            }

            // Update manual config
            _configPathLabel.text = profile.Paths.Current;

            string snippet = writer.GetManualSnippet(profile, settings.Port, transport, settings.HttpPort);
            _configJsonField.value = snippet;

            if (profile.InstallSteps != null && profile.InstallSteps.Length > 0)
            {
                var numbered = profile.InstallSteps.Select((s, i) => $"{i + 1}. {s}");
                _installStepsLabel.text = string.Join("\n", numbered);
            }
            else
            {
                _installStepsLabel.text = "Click Configure to auto-configure.";
            }
        }

        private void OnConfigureClicked()
        {
            if (_selectedIndex < 0 || _selectedIndex >= ClientRegistry.All.Length)
                return;

            var profile = ClientRegistry.All[_selectedIndex];
            var settings = McpSettings.Instance;
            var writer = ClientRegistry.GetWriter(profile.Strategy);

            if (_currentStatus == McpStatus.Configured)
            {
                // Unconfigure
                try
                {
                    writer.Unconfigure(profile);
                    McpLogger.Info($"Unconfigured {profile.DisplayName}: {profile.Paths.Current}");
                    EditorUtility.DisplayDialog("Success",
                        $"Unity MCP removed from {profile.DisplayName}.", "OK");
                    UpdateSelectedClient();
                }
                catch (System.Exception ex)
                {
                    McpLogger.Error($"Failed to unconfigure {profile.DisplayName}: {ex.Message}");
                    EditorUtility.DisplayDialog("Error",
                        $"Failed to unconfigure {profile.DisplayName}:\n{ex.Message}", "OK");
                }
            }
            else
            {
                // Configure
                try
                {
                    string transport = settings.Transport == McpSettings.TransportMode.StreamableHttp
                        ? "streamable-http" : "stdio";
                    writer.Configure(profile, settings.Port, transport, settings.HttpPort);
                    McpLogger.Info($"Configured {profile.DisplayName}: {profile.Paths.Current}");
                    EditorUtility.DisplayDialog("Success",
                        $"Unity MCP configured for {profile.DisplayName}.\n\nConfig: {profile.Paths.Current}", "OK");
                    UpdateSelectedClient();
                }
                catch (System.Exception ex)
                {
                    McpLogger.Error($"Failed to configure {profile.DisplayName}: {ex.Message}");
                    EditorUtility.DisplayDialog("Error",
                        $"Failed to configure {profile.DisplayName}:\n{ex.Message}", "OK");
                }
            }
        }

        private void OnConfigureAllClicked()
        {
            var settings = McpSettings.Instance;
            string transport = settings.Transport == McpSettings.TransportMode.StreamableHttp
                ? "streamable-http" : "stdio";
            int success = 0, fail = 0;

            foreach (var profile in ClientRegistry.All)
            {
                try
                {
                    var writer = ClientRegistry.GetWriter(profile.Strategy);
                    writer.Configure(profile, settings.Port, transport, settings.HttpPort);
                    success++;
                }
                catch
                {
                    fail++;
                }
            }

            EditorUtility.DisplayDialog("Configure All",
                $"Configured: {success}, Failed: {fail}", "OK");
            UpdateSelectedClient();
        }

        private void OnCopyPathClicked()
        {
            EditorGUIUtility.systemCopyBuffer = _configPathLabel.text;
            McpLogger.Info("Config path copied to clipboard");
        }

        private void OnOpenFileClicked()
        {
            string path = _configPathLabel.text;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            else
            {
                EditorUtility.DisplayDialog("Open File", "Config file does not exist yet. Click Configure first.", "OK");
            }
        }

        private void OnCopyJsonClicked()
        {
            EditorGUIUtility.systemCopyBuffer = _configJsonField.value;
            McpLogger.Info("Configuration copied to clipboard");
        }

        /// <summary>Refresh the displayed status and config snippet for the current client.</summary>
        public void Refresh() => UpdateSelectedClient();

        /// <summary>Re-configure only clients that are already configured.</summary>
        public void ReconfigureConfigured()
        {
            var settings = McpSettings.Instance;
            string transport = settings.Transport == McpSettings.TransportMode.StreamableHttp
                ? "streamable-http" : "stdio";
            int updated = 0, skipped = 0;

            foreach (var profile in ClientRegistry.All)
            {
                try
                {
                    var writer = ClientRegistry.GetWriter(profile.Strategy);
                    var status = writer.CheckStatus(profile, settings.Port, transport);
                    if (status == McpStatus.NotConfigured)
                    {
                        skipped++;
                        continue;
                    }
                    writer.Configure(profile, settings.Port, transport, settings.HttpPort);
                    updated++;
                }
                catch
                {
                    // skip failures silently
                }
            }

            if (updated > 0)
                McpLogger.Info($"Updated {updated} client config(s), skipped {skipped} unconfigured");

            UpdateSelectedClient();
        }
    }
}
