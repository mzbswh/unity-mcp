using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMcp.Editor.Core;
using UnityMcp.Editor.Window.Sections;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Window
{
    public class McpSettingsWindow : EditorWindow
    {
        private VisualElement[] _panels;
        private Button[] _tabs;
        private VisualElement _statusDot;
        private Label _statusText;
        private Label _versionLabel;
        private VisualElement _updateBanner;
        private int _activeTab;

        [MenuItem("Window/Unity MCP", priority = 0)]
        public static void ShowWindow()
        {
            var wnd = GetWindow<McpSettingsWindow>("Unity MCP");
            wnd.minSize = new Vector2(500, 400);
        }

        public void CreateGUI()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.mzbswh.unity-mcp/Editor/Window/McpSettingsWindow.uxml");
            if (tree != null)
                tree.CloneTree(rootVisualElement);

            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.mzbswh.unity-mcp/Editor/Window/McpSettingsWindow.uss");
            if (style != null)
                rootVisualElement.styleSheets.Add(style);

            // Header
            _statusDot = rootVisualElement.Q("status-indicator");
            _statusText = rootVisualElement.Q<Label>("status-text");

            _versionLabel = rootVisualElement.Q<Label>("version-label");
            if (_versionLabel != null)
                _versionLabel.text = $"v{McpConst.ServerVersion}";

            rootVisualElement.Q<Button>("docs-btn")?.RegisterCallback<ClickEvent>(_ =>
                Application.OpenURL("https://github.com/mzbswh/unity-mcp#readme"));
            rootVisualElement.Q<Button>("issues-btn")?.RegisterCallback<ClickEvent>(_ =>
                Application.OpenURL("https://github.com/mzbswh/unity-mcp/issues"));

            // Tabs
            _tabs = new[]
            {
                rootVisualElement.Q<Button>("tab-connection"),
                rootVisualElement.Q<Button>("tab-clients"),
                rootVisualElement.Q<Button>("tab-tools"),
                rootVisualElement.Q<Button>("tab-advanced"),
            };
            _panels = new[]
            {
                rootVisualElement.Q("panel-connection"),
                rootVisualElement.Q("panel-clients"),
                rootVisualElement.Q("panel-tools"),
                rootVisualElement.Q("panel-advanced"),
            };

            for (int i = 0; i < _tabs.Length; i++)
            {
                int idx = i;
                _tabs[i]?.RegisterCallback<ClickEvent>(_ => SwitchTab(idx));
            }

            // Initialize sections
            var connectionSection = new McpConnectionSection(_panels[0]);
            var clientConfigSection = new McpClientConfigSection(_panels[1]);
            _ = new McpToolsSection(_panels[2]);
            var advancedSection = new McpAdvancedSection(_panels[3]);

            connectionSection.OnStatusChanged += UpdateHeaderStatus;
            advancedSection.OnServerConfigChanged += () =>
            {
                clientConfigSection.Refresh();
                bool reconfigure = EditorUtility.DisplayDialog(
                    "Server Configuration Changed",
                    "Server configuration has changed. Update already-configured clients now?\n\n" +
                    "Only clients that have been previously configured will be updated.\n" +
                    "If you skip, remember to reconfigure clients in the Client Config tab.",
                    "Update Configured Clients", "Skip");
                if (reconfigure)
                    clientConfigSection.ReconfigureConfigured();
            };

            UpdateHeaderStatus();
            SwitchTab(0);

            // Trigger update check (force, not daily-cached, so banner appears immediately)
            PackageUpdateChecker.ForceCheck();

            // Periodic status update
            rootVisualElement.schedule.Execute(() => { UpdateHeaderStatus(); UpdateUpdateBanner(); }).Every(2000);

            // Delayed first check (give network time)
            rootVisualElement.schedule.Execute(UpdateUpdateBanner).ExecuteLater(3000);
        }

        private void SwitchTab(int idx)
        {
            _activeTab = idx;
            for (int i = 0; i < _tabs.Length; i++)
            {
                if (_tabs[i] == null || _panels[i] == null) continue;

                if (i == idx)
                {
                    _tabs[i].AddToClassList("tab-active");
                    _panels[i].style.display = DisplayStyle.Flex;
                }
                else
                {
                    _tabs[i].RemoveFromClassList("tab-active");
                    _panels[i].style.display = DisplayStyle.None;
                }
            }
        }

        private void UpdateHeaderStatus()
        {
            if (_statusDot == null || _statusText == null) return;

            bool running = McpServer.Transport?.IsRunning ?? false;
            int clients = McpServer.Transport?.ClientCount ?? 0;
            int port = McpServer.Transport?.Port ?? McpSettings.Instance.Port;

            _statusDot.RemoveFromClassList("status-green");
            _statusDot.RemoveFromClassList("status-red");
            _statusDot.RemoveFromClassList("status-gray");

            if (running)
            {
                _statusDot.AddToClassList("status-green");
                _statusText.text = $"Running  |  Port {port}  |  {clients} client(s)";
            }
            else
            {
                _statusDot.AddToClassList("status-red");
                _statusText.text = "Stopped";
            }
        }

        private void UpdateUpdateBanner()
        {
            if (!PackageUpdateChecker.HasUpdate)
            {
                if (_updateBanner != null)
                    _updateBanner.style.display = DisplayStyle.None;
                return;
            }

            if (_updateBanner == null)
            {
                _updateBanner = new VisualElement();
                _updateBanner.style.flexDirection = FlexDirection.Row;
                _updateBanner.style.alignItems = Align.Center;
                _updateBanner.style.backgroundColor = new Color(0.85f, 0.55f, 0.0f, 0.25f);
                _updateBanner.style.borderBottomLeftRadius = _updateBanner.style.borderBottomRightRadius =
                    _updateBanner.style.borderTopLeftRadius = _updateBanner.style.borderTopRightRadius = 4;
                _updateBanner.style.paddingTop = _updateBanner.style.paddingBottom = 6;
                _updateBanner.style.paddingLeft = _updateBanner.style.paddingRight = 10;
                _updateBanner.style.marginLeft = _updateBanner.style.marginRight = 12;
                _updateBanner.style.marginTop = 6;

                var icon = new Label("\u26A0");
                icon.style.fontSize = 14;
                icon.style.marginRight = 6;
                _updateBanner.Add(icon);

                var text = new Label { name = "update-text" };
                text.style.flexGrow = 1;
                text.style.fontSize = 12;
                text.style.color = new Color(1f, 0.8f, 0.3f);
                text.style.whiteSpace = WhiteSpace.Normal;
                _updateBanner.Add(text);

                var updateBtn = new Button(() =>
                    Application.OpenURL("https://github.com/mzbswh/unity-mcp"));
                updateBtn.text = "Update";
                updateBtn.style.fontSize = 11;
                updateBtn.style.height = 22;
                _updateBanner.Add(updateBtn);

                // Insert after header (tab-bar), before content
                var tabBar = rootVisualElement.Q(className: "tab-bar");
                if (tabBar != null)
                {
                    int idx = rootVisualElement.IndexOf(tabBar);
                    rootVisualElement.Insert(idx + 1, _updateBanner);
                }
                else
                {
                    rootVisualElement.Insert(0, _updateBanner);
                }
            }

            _updateBanner.style.display = DisplayStyle.Flex;
            var textLabel = _updateBanner.Q<Label>("update-text");
            if (textLabel != null)
                textLabel.text = $"Update available: v{PackageUpdateChecker.LatestVersion}  (current: v{McpConst.ServerVersion})";
        }
    }
}
