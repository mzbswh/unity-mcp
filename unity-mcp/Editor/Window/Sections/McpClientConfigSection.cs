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
        private readonly VisualElement _cardContainer;

        public McpClientConfigSection(VisualElement panel)
        {
            _root = panel;
            BuildUI();
            _cardContainer = _root.Q("client-cards");
            RefreshCards();

            // Periodic refresh
            _root.schedule.Execute(RefreshCards).Every(3000);
        }

        private void BuildUI()
        {
            var descBox = new VisualElement();
            descBox.AddToClassList("info-box");
            descBox.Add(new Label("Click Configure to write the Unity MCP server entry into the client's config file. A green border indicates the client is already configured."));
            _root.Add(descBox);

            var container = new VisualElement { name = "client-cards" };
            _root.Add(container);

            // Manual setup
            var manualBox = new VisualElement();
            manualBox.AddToClassList("section-box");
            manualBox.style.marginTop = 8;

            var manualTitle = new Label("Manual Setup");
            manualTitle.AddToClassList("section-title");
            manualBox.Add(manualTitle);

            manualBox.Add(new Label("Copy the JSON config to clipboard for clients not listed above.") { style = { fontSize = 11, marginBottom = 4 } });

            var copyBtn = new Button(CopyConfigToClipboard) { text = "Copy Config to Clipboard" };
            copyBtn.AddToClassList("action-btn");
            manualBox.Add(copyBtn);

            _root.Add(manualBox);
        }

        private void RefreshCards()
        {
            _cardContainer.Clear();
            var settings = McpSettings.Instance;
            string transport = settings.Transport == McpSettings.TransportMode.StreamableHttp
                ? "streamable-http" : "stdio";
            int port = settings.Port;

            foreach (var profile in ClientRegistry.All)
            {
                var writer = ClientRegistry.GetWriter(profile.Strategy);
                var status = writer.CheckStatus(profile, port, transport);
                _cardContainer.Add(CreateCard(profile, writer, status, settings));
            }
        }

        private VisualElement CreateCard(ClientProfile profile, IConfigWriter writer, McpStatus status, McpSettings settings)
        {
            var card = new VisualElement();
            card.AddToClassList("client-card");
            if (status == McpStatus.Configured)
                card.AddToClassList("client-card-configured");

            // Header row
            var header = new VisualElement();
            header.AddToClassList("client-card-header");

            var nameLabel = new Label(profile.DisplayName);
            nameLabel.AddToClassList("client-name");
            header.Add(nameLabel);

            var statusLabel = new Label(GetStatusText(status));
            statusLabel.AddToClassList("client-status");
            statusLabel.AddToClassList(status == McpStatus.Configured ? "client-status-configured" : "client-status-not-configured");
            header.Add(statusLabel);

            string btnText = status == McpStatus.Configured ? "Update" : "Configure";
            var configBtn = new Button(() => DoConfigure(profile, settings)) { text = btnText };
            configBtn.AddToClassList("action-btn");
            header.Add(configBtn);

            card.Add(header);

            // Path detail
            var pathLabel = new Label(profile.Paths.Current);
            pathLabel.AddToClassList("client-path");
            card.Add(pathLabel);

            // Actions
            var actions = new VisualElement();
            actions.AddToClassList("client-actions");

            var copySnippetBtn = new Button(() => CopySnippet(profile, settings)) { text = "Copy Snippet" };
            copySnippetBtn.style.fontSize = 11;
            actions.Add(copySnippetBtn);

            if (profile.InstallSteps != null && profile.InstallSteps.Length > 0)
            {
                var stepsBtn = new Button(() => ShowSteps(profile)) { text = "Install Steps" };
                stepsBtn.style.fontSize = 11;
                actions.Add(stepsBtn);
            }

            card.Add(actions);

            return card;
        }

        private static string GetStatusText(McpStatus status)
        {
            return status switch
            {
                McpStatus.Configured => "Configured",
                McpStatus.NeedsUpdate => "Needs Update",
                _ => "Not Configured"
            };
        }

        private void DoConfigure(ClientProfile profile, McpSettings settings)
        {
            try
            {
                var writer = ClientRegistry.GetWriter(profile.Strategy);
                string transport = settings.Transport == McpSettings.TransportMode.StreamableHttp
                    ? "streamable-http" : "stdio";
                writer.Configure(profile, settings.Port, transport, settings.HttpPort);
                McpLogger.Info($"Configured {profile.DisplayName}: {profile.Paths.Current}");
                EditorUtility.DisplayDialog("Success",
                    $"Unity MCP configured for {profile.DisplayName}.\n\nConfig: {profile.Paths.Current}", "OK");
                RefreshCards();
            }
            catch (System.Exception ex)
            {
                McpLogger.Error($"Failed to configure {profile.DisplayName}: {ex.Message}");
                EditorUtility.DisplayDialog("Error",
                    $"Failed to configure {profile.DisplayName}:\n{ex.Message}", "OK");
            }
        }

        private static void CopySnippet(ClientProfile profile, McpSettings settings)
        {
            var writer = ClientRegistry.GetWriter(profile.Strategy);
            string transport = settings.Transport == McpSettings.TransportMode.StreamableHttp
                ? "streamable-http" : "stdio";
            string snippet = writer.GetManualSnippet(profile, settings.Port, transport, settings.HttpPort);
            EditorGUIUtility.systemCopyBuffer = snippet;
        }

        private static void ShowSteps(ClientProfile profile)
        {
            string steps = string.Join("\n", profile.InstallSteps);
            EditorUtility.DisplayDialog($"{profile.DisplayName} — Install Steps", steps, "OK");
        }

        private static void CopyConfigToClipboard()
        {
            var settings = McpSettings.Instance;
            // Use JsonFile writer with a generic profile for the manual snippet
            var writer = new JsonFileConfigWriter();
            var genericProfile = new ClientProfile
            {
                Id = "generic",
                Strategy = ConfigStrategy.JsonFile,
                Paths = new PlatformPaths { Windows = "", Mac = "", Linux = "" },
            };
            string transport = settings.Transport == McpSettings.TransportMode.StreamableHttp
                ? "streamable-http" : "stdio";
            string snippet = writer.GetManualSnippet(genericProfile, settings.Port, transport, settings.HttpPort);
            EditorGUIUtility.systemCopyBuffer = snippet;
        }
    }
}
