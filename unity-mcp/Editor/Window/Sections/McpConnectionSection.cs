using System;
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

        public McpConnectionSection(VisualElement panel)
        {
            _root = panel;
            BuildUI();

            var settings = McpSettings.Instance;

            // Control buttons
            _root.Q<Button>("btn-start").clicked += () => { McpServer.Restart(); OnStatusChanged?.Invoke(); };
            _root.Q<Button>("btn-stop").clicked += () => { McpServer.Shutdown(); OnStatusChanged?.Invoke(); };
            _root.Q<Button>("btn-restart").clicked += () => { McpServer.Restart(); OnStatusChanged?.Invoke(); };

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
            RefreshConnectedClients();

            // Periodic refresh
            _root.schedule.Execute(RefreshConnectedClients).Every(2000);
        }

        private void BuildUI()
        {
            // Server controls
            var controlBox = new VisualElement();
            controlBox.AddToClassList("section-box");

            var titleLabel = new Label("Server");
            titleLabel.AddToClassList("section-title");
            controlBox.Add(titleLabel);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginBottom = 8;

            var startBtn = new Button { name = "btn-start", text = "Start" };
            startBtn.AddToClassList("action-btn");
            var stopBtn = new Button { name = "btn-stop", text = "Stop" };
            stopBtn.AddToClassList("action-btn");
            var restartBtn = new Button { name = "btn-restart", text = "Restart" };
            restartBtn.AddToClassList("action-btn");
            btnRow.Add(startBtn);
            btnRow.Add(stopBtn);
            btnRow.Add(restartBtn);
            controlBox.Add(btnRow);

            var portField = new IntegerField("Port") { name = "field-port" };
            portField.AddToClassList("field-row");
            controlBox.Add(portField);

            var autoStartToggle = new Toggle("Auto Start") { name = "field-autostart" };
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
