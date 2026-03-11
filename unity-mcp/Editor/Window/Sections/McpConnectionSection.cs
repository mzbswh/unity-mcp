using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Window.Sections
{
    public class McpConnectionSection
    {
        public event Action OnStatusChanged;

        private readonly VisualElement _root;
        private readonly Label _connectedCountLabel;
        private readonly VisualElement _connectedListContainer;
        private readonly IntegerField _portField;
        private readonly Toggle _autoStartToggle;
        private readonly EnumField _transportField;
        private readonly IntegerField _httpPortField;
        private readonly VisualElement _httpPortRow;
        private readonly VisualElement _infoBoxStdio;
        private readonly VisualElement _infoBoxHttp;
        private Button _startBtn;
        private Button _stopBtn;
        private Button _restartBtn;
        private VisualElement _serverStatusDot;
        private Label _serverStatusLabel;

        public McpConnectionSection(VisualElement panel)
        {
            _root = panel;
            BuildUI();

            var settings = McpSettings.Instance;

            // Control buttons
            _startBtn = _root.Q<Button>("btn-start");
            _stopBtn = _root.Q<Button>("btn-stop");
            _restartBtn = _root.Q<Button>("btn-restart");
            _startBtn.clicked += () => { McpServer.Restart(); OnStatusChanged?.Invoke(); UpdateButtonStates(); };
            _stopBtn.clicked += () => { McpServer.Shutdown(); OnStatusChanged?.Invoke(); UpdateButtonStates(); };
            _restartBtn.clicked += () => { McpServer.Restart(); OnStatusChanged?.Invoke(); UpdateButtonStates(); };

            // Port
            _portField = _root.Q<IntegerField>("field-port");
            _portField.value = settings.Port;
            _portField.RegisterValueChangedCallback(e => settings.Port = e.newValue);

            // Auto start
            _autoStartToggle = _root.Q<Toggle>("field-autostart");
            _autoStartToggle.value = settings.AutoStart;
            _autoStartToggle.RegisterValueChangedCallback(e => settings.AutoStart = e.newValue);

            // Transport
            _transportField = _root.Q<EnumField>("field-transport");
            _transportField.Init(settings.Transport);
            _transportField.value = settings.Transport;
            _transportField.RegisterValueChangedCallback(e =>
            {
                settings.Transport = (McpSettings.TransportMode)e.newValue;
                UpdateTransportVisibility();
            });

            // HTTP port
            _httpPortField = _root.Q<IntegerField>("field-http-port");
            _httpPortField.value = settings.HttpPort;
            _httpPortField.RegisterValueChangedCallback(e => settings.HttpPort = Mathf.Max(1, e.newValue));

            _httpPortRow = _root.Q("row-http-port");
            _infoBoxStdio = _root.Q("info-stdio");
            _infoBoxHttp = _root.Q("info-http");

            _connectedCountLabel = _root.Q<Label>("connected-count");
            _connectedListContainer = _root.Q("connected-list");

            UpdateTransportVisibility();
            UpdateButtonStates();
            RefreshConnectedClients();

            // Periodic refresh
            _root.schedule.Execute(() => { UpdateButtonStates(); RefreshConnectedClients(); }).Every(2000);
        }

        private VisualElement _depBanner;
        private VisualElement _depDot;
        private Label _depVersionLabel;

        private void BuildDependencyBanner()
        {
            var status = DependencyChecker.Check();

            _depBanner = new VisualElement();
            _depBanner.AddToClassList("section-box");
            _depBanner.style.marginBottom = 8;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            var titleLabel = new Label("Environment");
            titleLabel.AddToClassList("section-title");
            titleLabel.style.flexGrow = 1;
            titleRow.Add(titleLabel);
            _depBanner.Add(titleRow);

            // uv row
            var uvRow = new VisualElement();
            uvRow.style.flexDirection = FlexDirection.Row;
            uvRow.style.alignItems = Align.Center;
            uvRow.style.marginTop = 4;

            _depDot = new VisualElement();
            _depDot.AddToClassList("dep-dot");
            _depDot.AddToClassList(status.UvFound ? "dep-dot-green" : "dep-dot-red");
            uvRow.Add(_depDot);

            var uvLabel = new Label("uv");
            uvLabel.style.marginLeft = 6;
            uvLabel.style.minWidth = 30;
            uvRow.Add(uvLabel);

            _depVersionLabel = new Label(status.UvFound ? status.UvVersion : "Not found");
            _depVersionLabel.style.marginLeft = 8;
            _depVersionLabel.style.color = status.UvFound ? new Color(0.6f, 0.8f, 0.6f) : new Color(0.8f, 0.4f, 0.4f);
            _depVersionLabel.style.flexGrow = 1;
            uvRow.Add(_depVersionLabel);

            var refreshBtn = new Button { text = "Refresh" };
            refreshBtn.AddToClassList("action-btn");
            refreshBtn.clicked += RefreshDependencyStatus;
            uvRow.Add(refreshBtn);

            if (!status.UvFound)
            {
                var installBtn = new Button { text = "Install" };
                installBtn.AddToClassList("action-btn");
                installBtn.clicked += () => Application.OpenURL("https://docs.astral.sh/uv/getting-started/installation/");
                uvRow.Add(installBtn);
            }

            _depBanner.Add(uvRow);

            if (!status.UvFound)
            {
                var hint = new Label("uv is required to run the MCP Python bridge. It manages Python and uvx automatically.");
                hint.style.fontSize = 11;
                hint.style.color = new Color(0.5f, 0.5f, 0.5f);
                hint.style.whiteSpace = WhiteSpace.Normal;
                hint.style.marginTop = 4;
                _depBanner.Add(hint);
            }

            _root.Add(_depBanner);
        }

        private void RefreshDependencyStatus()
        {
            var status = DependencyChecker.Check();

            _depDot.RemoveFromClassList("dep-dot-green");
            _depDot.RemoveFromClassList("dep-dot-red");
            _depDot.AddToClassList(status.UvFound ? "dep-dot-green" : "dep-dot-red");

            _depVersionLabel.text = status.UvFound ? status.UvVersion : "Not found";
            _depVersionLabel.style.color = status.UvFound ? new Color(0.6f, 0.8f, 0.6f) : new Color(0.8f, 0.4f, 0.4f);
        }

        private void BuildUI()
        {
            // Dependency check banner
            BuildDependencyBanner();

            // Server controls
            var controlBox = new VisualElement();
            controlBox.AddToClassList("section-box");

            var titleLabel = new Label("Server");
            titleLabel.AddToClassList("section-title");
            controlBox.Add(titleLabel);

            // Status row below title
            var statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;
            statusRow.style.marginBottom = 8;

            _serverStatusDot = new VisualElement();
            _serverStatusDot.AddToClassList("status-dot");
            _serverStatusDot.AddToClassList("status-red");
            statusRow.Add(_serverStatusDot);

            _serverStatusLabel = new Label("Stopped");
            _serverStatusLabel.AddToClassList("status-label");
            statusRow.Add(_serverStatusLabel);

            controlBox.Add(statusRow);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginBottom = 8;

            var startBtn = new Button { name = "btn-start", text = "▶ Start" };
            startBtn.AddToClassList("action-btn");
            startBtn.AddToClassList("action-btn-start");
            var stopBtn = new Button { name = "btn-stop", text = "■ Stop" };
            stopBtn.AddToClassList("action-btn");
            stopBtn.AddToClassList("action-btn-stop");
            var restartBtn = new Button { name = "btn-restart", text = "↻ Restart" };
            restartBtn.AddToClassList("action-btn");
            restartBtn.AddToClassList("action-btn-restart");
            btnRow.Add(startBtn);
            btnRow.Add(stopBtn);
            btnRow.Add(restartBtn);
            controlBox.Add(btnRow);

            var portField = new IntegerField("WebSocket Port") { name = "field-port" };
            portField.tooltip = "Port for Unity's internal WebSocket bridge. The MCP Python server connects to Unity through this port.";
            portField.AddToClassList("field-row");
            controlBox.Add(portField);

            var autoStartToggle = new Toggle("Auto Start") { name = "field-autostart" };
            autoStartToggle.tooltip = "Automatically start the WebSocket bridge when Unity opens.";
            autoStartToggle.AddToClassList("field-row");
            controlBox.Add(autoStartToggle);

            _root.Add(controlBox);

            // Transport
            var transportBox = new VisualElement();
            transportBox.AddToClassList("section-box");

            var transportTitle = new Label("Transport");
            transportTitle.AddToClassList("section-title");
            transportBox.Add(transportTitle);

            var transportField = new EnumField("Mode", McpSettings.TransportMode.Stdio) { name = "field-transport" };
            transportField.AddToClassList("field-row");
            transportBox.Add(transportField);

            var httpPortRow = new VisualElement { name = "row-http-port" };
            var httpPortField = new IntegerField("HTTP Port") { name = "field-http-port" };
            httpPortField.tooltip = "Port for the Streamable HTTP MCP server endpoint.";
            httpPortField.AddToClassList("field-row");
            httpPortRow.Add(httpPortField);
            transportBox.Add(httpPortRow);

            // Info boxes
            var infoStdio = new VisualElement { name = "info-stdio" };
            infoStdio.AddToClassList("info-box");
            infoStdio.Add(new Label("Stdio mode: MCP clients launch the Python server automatically.\nUse the Clients tab to generate the configuration."));
            transportBox.Add(infoStdio);

            var infoHttp = new VisualElement { name = "info-http" };
            infoHttp.AddToClassList("info-box");
            infoHttp.Add(new Label("Streamable HTTP mode: run the server manually, then configure your MCP client with the URL shown in the Clients tab."));
            transportBox.Add(infoHttp);

            _root.Add(transportBox);

            // Connected clients
            var connectedBox = new VisualElement();
            connectedBox.AddToClassList("section-box");

            var connectedTitle = new Label { name = "connected-count", text = "Connected Clients (0)" };
            connectedTitle.AddToClassList("section-title");
            connectedBox.Add(connectedTitle);

            var connectedList = new VisualElement { name = "connected-list" };
            connectedBox.Add(connectedList);

            _root.Add(connectedBox);
        }

        private void UpdateButtonStates()
        {
            bool running = McpServer.Transport?.IsRunning ?? false;
            int clients = McpServer.Transport?.ClientCount ?? 0;
            int port = McpServer.Transport?.Port ?? McpSettings.Instance.Port;

            _startBtn.SetEnabled(!running);
            _stopBtn.SetEnabled(running);
            _restartBtn.SetEnabled(running);

            // Update status indicator
            _serverStatusDot.RemoveFromClassList("status-green");
            _serverStatusDot.RemoveFromClassList("status-red");

            if (running)
            {
                _serverStatusDot.AddToClassList("status-green");
                _serverStatusLabel.text = $"Running  ·  Port {port}  ·  {clients} client(s)";
                _serverStatusLabel.style.color = new Color(0.6f, 0.8f, 0.6f);
            }
            else
            {
                _serverStatusDot.AddToClassList("status-red");
                _serverStatusLabel.text = "Stopped";
                _serverStatusLabel.style.color = new Color(0.8f, 0.4f, 0.4f);
            }

            // Disable port fields while running
            _portField.SetEnabled(!running);
            _httpPortField.SetEnabled(!running);
        }

        private void UpdateTransportVisibility()
        {
            bool isHttp = _transportField.value is McpSettings.TransportMode mode
                          && mode == McpSettings.TransportMode.StreamableHttp;
            _httpPortRow.style.display = isHttp ? DisplayStyle.Flex : DisplayStyle.None;
            _infoBoxStdio.style.display = isHttp ? DisplayStyle.None : DisplayStyle.Flex;
            _infoBoxHttp.style.display = isHttp ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshConnectedClients()
        {
            var transport = McpServer.Transport;
            int count = transport?.ClientCount ?? 0;
            _connectedCountLabel.text = $"Connected Clients ({count})";

            _connectedListContainer.Clear();

            if (count > 0)
            {
                var clients = transport.ConnectedClients;
                foreach (var info in clients)
                {
                    var row = new VisualElement();
                    row.AddToClassList("connected-client-row");

                    var dot = new VisualElement();
                    dot.AddToClassList("connected-dot");
                    row.Add(dot);

                    string version = string.IsNullOrEmpty(info.Version) ? "" : $" v{info.Version}";
                    string duration = FormatDuration(DateTime.Now - info.ConnectedAt);
                    var nameLabel = new Label($"{info.Name}{version}");
                    nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    nameLabel.style.flexGrow = 1;
                    row.Add(nameLabel);

                    var detailLabel = new Label($"{info.Endpoint}  ·  {duration}");
                    detailLabel.style.fontSize = 11;
                    detailLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                    row.Add(detailLabel);

                    _connectedListContainer.Add(row);
                }
            }
            else
            {
                var emptyLabel = new Label("No clients connected.\nConfigure a client, then invoke a tool from the MCP client to connect.");
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                emptyLabel.style.whiteSpace = WhiteSpace.Normal;
                emptyLabel.style.fontSize = 11;
                _connectedListContainer.Add(emptyLabel);
            }
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalSeconds < 60) return $"{(int)duration.TotalSeconds}s";
            if (duration.TotalMinutes < 60) return $"{(int)duration.TotalMinutes}m";
            if (duration.TotalHours < 24) return $"{(int)duration.TotalHours}h {duration.Minutes}m";
            return $"{(int)duration.TotalDays}d {duration.Hours}h";
        }
    }
}
