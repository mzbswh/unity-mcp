using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("UIToolkit")]
    public static class UIToolkitTools
    {
        [McpTool("uitoolkit_create", "Create a new UXML or USS file for Unity UI Toolkit",
            Group = "uitoolkit")]
        public static ToolResult Create(
            [Desc("Save path (e.g. Assets/UI/MainMenu.uxml or Assets/UI/Styles.uss)")] string path,
            [Desc("File contents. If empty, generates a template.")] string contents = null)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            if (ext != ".uxml" && ext != ".uss")
                return ToolResult.Error("Path must end with .uxml or .uss");

            if (File.Exists(path))
                return ToolResult.Error($"File already exists: {path}");

            if (string.IsNullOrEmpty(contents))
                contents = ext == ".uxml" ? GenerateDefaultUxml() : GenerateDefaultUss();

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, contents);
            AssetDatabase.Refresh();

            return ToolResult.Json(new
            {
                path,
                type = ext == ".uxml" ? "UXML" : "USS",
                created = true,
            });
        }

        [McpTool("uitoolkit_list", "List all UXML and USS files in the project",
            Group = "uitoolkit", ReadOnly = true)]
        public static ToolResult List(
            [Desc("Filter: 'uxml', 'uss', or 'all' (default)")] string type = "all",
            [Desc("Search within a specific folder (e.g. Assets/UI)")] string folder = null)
        {
            string[] uxmlGuids = type != "uss" ? AssetDatabase.FindAssets("t:VisualTreeAsset", folder != null ? new[] { folder } : null) : Array.Empty<string>();
            string[] ussGuids = type != "uxml" ? AssetDatabase.FindAssets("t:StyleSheet", folder != null ? new[] { folder } : null) : Array.Empty<string>();

            var uxmlPaths = uxmlGuids.Select(g => new { path = AssetDatabase.GUIDToAssetPath(g), type = "UXML" });
            var ussPaths = ussGuids.Select(g => new { path = AssetDatabase.GUIDToAssetPath(g), type = "USS" });

            var all = uxmlPaths.Concat(ussPaths)
                .Where(x => x.path.StartsWith("Assets/"))
                .OrderBy(x => x.path)
                .ToArray();

            return ToolResult.Json(new { count = all.Length, files = all });
        }

        [McpTool("uitoolkit_attach", "Attach a UXML document to a GameObject via UIDocument component",
            Group = "uitoolkit")]
        public static ToolResult Attach(
            [Desc("Target GameObject name or path")] string gameObject,
            [Desc("UXML asset path (e.g. Assets/UI/MainMenu.uxml)")] string uxmlPath,
            [Desc("Optional PanelSettings asset path")] string panelSettingsPath = null)
        {
            var go = GameObjectTools.FindGameObject(gameObject, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: '{gameObject}'");

            var pv = PathValidator.QuickValidate(uxmlPath);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (vta == null)
                return ToolResult.Error($"VisualTreeAsset not found at: {uxmlPath}");

            var uid = go.GetComponent<UIDocument>();
            if (uid == null)
                uid = go.AddComponent<UIDocument>();

            uid.visualTreeAsset = vta;

            if (!string.IsNullOrEmpty(panelSettingsPath))
            {
                var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelSettingsPath);
                if (ps != null)
                    uid.panelSettings = ps;
            }

            EditorUtility.SetDirty(go);

            return ToolResult.Json(new
            {
                gameObject = go.name,
                uxmlPath,
                panelSettingsPath,
                attached = true,
            });
        }

        [McpTool("uitoolkit_get_visual_tree", "Get the visual element hierarchy of a UIDocument at runtime",
            Group = "uitoolkit", ReadOnly = true)]
        public static ToolResult GetVisualTree(
            [Desc("GameObject name with UIDocument component")] string gameObject,
            [Desc("Max depth to traverse (default 5)")] int maxDepth = 5)
        {
            var go = GameObjectTools.FindGameObject(gameObject, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: '{gameObject}'");

            var uid = go.GetComponent<UIDocument>();
            if (uid == null)
                return ToolResult.Error($"No UIDocument component on '{gameObject}'");

            var root = uid.rootVisualElement;
            if (root == null)
                return ToolResult.Error("UIDocument has no root visual element (may not be active)");

            var tree = BuildElementTree(root, 0, maxDepth);
            return ToolResult.Json(tree);
        }

        private static object BuildElementTree(VisualElement el, int depth, int maxDepth)
        {
            var result = new
            {
                type = el.GetType().Name,
                name = el.name,
                classes = el.GetClasses().ToArray(),
                childCount = el.childCount,
                children = depth < maxDepth
                    ? el.Children().Select(c => BuildElementTree(c, depth + 1, maxDepth)).ToArray()
                    : null,
            };
            return result;
        }

        private static string GenerateDefaultUxml()
        {
            return @"<ui:UXML xmlns:ui=""UnityEngine.UIElements"" xmlns:uie=""UnityEditor.UIElements"">
    <ui:VisualElement name=""root"" style=""flex-grow: 1;"">
        <ui:Label text=""Hello UI Toolkit"" name=""title"" />
        <ui:Button text=""Click Me"" name=""button"" />
    </ui:VisualElement>
</ui:UXML>";
        }

        private static string GenerateDefaultUss()
        {
            return @"#root {
    padding: 10px;
    background-color: rgb(40, 40, 40);
}

#title {
    font-size: 24px;
    color: white;
    margin-bottom: 10px;
}

#button {
    width: 120px;
    height: 36px;
}";
        }
    }
}
