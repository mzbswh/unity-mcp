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

            var versionLabel = rootVisualElement.Q<Label>("version-label");
            if (versionLabel != null)
                versionLabel.text = $"v{McpConst.ServerVersion}";

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
            _ = new McpClientConfigSection(_panels[1]);
            _ = new McpToolsSection(_panels[2]);
            _ = new McpAdvancedSection(_panels[3]);

            connectionSection.OnStatusChanged += UpdateHeaderStatus;

            UpdateHeaderStatus();
            SwitchTab(0);

            // Periodic status update
            rootVisualElement.schedule.Execute(UpdateHeaderStatus).Every(2000);
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
    }
}
