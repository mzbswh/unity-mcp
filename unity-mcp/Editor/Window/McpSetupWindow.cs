using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Window
{
    public class McpSetupWindow : EditorWindow
    {
        private DependencyChecker.DependencyStatus _status;

        public static void ShowWindow(DependencyChecker.DependencyStatus status)
        {
            var wnd = GetWindow<McpSetupWindow>("Unity MCP Setup");
            wnd.minSize = new Vector2(400, 250);
            wnd.maxSize = new Vector2(500, 300);
            wnd._status = status;
        }

        public void CreateGUI()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.mzbswh.unity-mcp/Editor/Window/McpSetupWindow.uxml");
            if (tree != null) tree.CloneTree(rootVisualElement);

            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.mzbswh.unity-mcp/Editor/Window/McpSetupWindow.uss");
            if (style != null) rootVisualElement.styleSheets.Add(style);

            rootVisualElement.Q<Button>("btn-install-python")?.RegisterCallback<ClickEvent>(_ =>
                Application.OpenURL("https://www.python.org/downloads/"));
            rootVisualElement.Q<Button>("btn-install-uv")?.RegisterCallback<ClickEvent>(_ =>
                Application.OpenURL("https://docs.astral.sh/uv/getting-started/installation/"));

            rootVisualElement.Q<Button>("btn-refresh")?.RegisterCallback<ClickEvent>(_ => Refresh());
            rootVisualElement.Q<Button>("btn-done")?.RegisterCallback<ClickEvent>(_ =>
            {
                EditorPrefs.SetBool("UnityMcp_SetupDone", true);
                Close();
                McpServer.Restart();
            });
            rootVisualElement.Q<Button>("btn-skip")?.RegisterCallback<ClickEvent>(_ =>
            {
                EditorPrefs.SetBool("UnityMcp_SetupDone", true);
                Close();
            });

            UpdateUI();
        }

        private void Refresh()
        {
            _status = DependencyChecker.Check();
            UpdateUI();
        }

        private void UpdateUI()
        {
            SetDot("python-dot", _status.PythonFound);
            SetDot("uv-dot", _status.UvFound);
            SetDot("uvx-dot", _status.UvxFound);

            SetVersion("python-version", _status.PythonVersion);
            SetVersion("uv-version", _status.UvVersion);
            SetVersion("uvx-version", _status.UvxFound ? "available" : "");

            var doneBtn = rootVisualElement.Q<Button>("btn-done");
            if (doneBtn != null)
                doneBtn.SetEnabled(_status.AllSatisfied);
        }

        private void SetDot(string name, bool found)
        {
            var dot = rootVisualElement.Q(name);
            if (dot == null) return;
            dot.RemoveFromClassList("dep-dot-green");
            dot.RemoveFromClassList("dep-dot-red");
            dot.AddToClassList(found ? "dep-dot-green" : "dep-dot-red");
        }

        private void SetVersion(string name, string version)
        {
            var label = rootVisualElement.Q<Label>(name);
            if (label != null)
                label.text = string.IsNullOrEmpty(version) ? "Not found" : version;
        }
    }
}
