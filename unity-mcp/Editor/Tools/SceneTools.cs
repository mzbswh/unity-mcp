using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Scene")]
    public static class SceneTools
    {
        [McpTool("scene_create", "Create a new empty scene",
            Group = "scene")]
        public static ToolResult Create(
            [Desc("Path for the new scene (e.g. Assets/Scenes/NewScene.unity)")] string path,
            [Desc("Whether to activate it as the current scene")] bool activate = true)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                activate ? NewSceneMode.Single : NewSceneMode.Additive);

            EditorSceneManager.SaveScene(scene, path);
            return ToolResult.Json(new { success = true, path, name = scene.name });
        }

        [McpTool("scene_open", "Open a scene by path",
            Group = "scene")]
        public static ToolResult Open(
            [Desc("Scene asset path (e.g. Assets/Scenes/Main.unity)")] string path,
            [Desc("Open mode: Single or Additive")] string mode = "Single")
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            if (!File.Exists(path))
                return ToolResult.Error($"Scene not found: {path}");

            var openMode = mode.ToLower() == "additive"
                ? OpenSceneMode.Additive
                : OpenSceneMode.Single;

            var scene = EditorSceneManager.OpenScene(path, openMode);
            return ToolResult.Json(new { success = true, name = scene.name, path = scene.path });
        }

        [McpTool("scene_save", "Save the current scene",
            Group = "scene")]
        public static ToolResult Save(
            [Desc("Path to save to (empty = save current scene in place)")] string path = null)
        {
            if (!string.IsNullOrEmpty(path))
            {
                var pv = PathValidator.QuickValidate(path);
                if (!pv.IsValid) return ToolResult.Error(pv.Error);
            }

            var scene = SceneManager.GetActiveScene();
            bool saved;
            if (!string.IsNullOrEmpty(path))
                saved = EditorSceneManager.SaveScene(scene, path);
            else
                saved = EditorSceneManager.SaveScene(scene);

            return saved
                ? ToolResult.Text($"Saved scene '{scene.name}'")
                : ToolResult.Error("Failed to save scene");
        }

        [McpTool("scene_get_hierarchy", "Get the scene hierarchy tree",
            Group = "scene", ReadOnly = true)]
        public static ToolResult GetHierarchy(
            [Desc("Max depth to traverse (-1 for unlimited)")] int maxDepth = -1,
            [Desc("Max items per page")] int pageSize = 100,
            [Desc("Cursor for pagination")] string cursor = null)
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();

            int startIndex = 0;
            if (!string.IsNullOrEmpty(cursor) && int.TryParse(cursor, out int ci))
                startIndex = ci;

            var nodes = new List<object>();
            int total = 0;
            foreach (var root in roots)
                total += CountHierarchy(root.transform, maxDepth, 0);

            foreach (var root in roots.Skip(startIndex).Take(pageSize))
                nodes.Add(BuildNode(root.transform, maxDepth, 0));

            string nextCursor = (startIndex + pageSize < roots.Length)
                ? (startIndex + pageSize).ToString() : null;

            return ToolResult.Paginated(nodes, total, nextCursor);
        }

        [McpTool("scene_list_all", "List all scenes in Build Settings",
            Group = "scene", ReadOnly = true)]
        public static ToolResult ListAll()
        {
            var scenes = EditorBuildSettings.scenes.Select((s, i) => new
            {
                index = i,
                path = s.path,
                name = Path.GetFileNameWithoutExtension(s.path),
                enabled = s.enabled,
            }).ToArray();

            var activeScene = SceneManager.GetActiveScene();
            return ToolResult.Json(new
            {
                activeScene = activeScene.path,
                buildScenes = scenes,
                loadedSceneCount = SceneManager.sceneCount
            });
        }

        // --- Helpers ---

        private static object BuildNode(Transform t, int maxDepth, int depth)
        {
            var node = new Dictionary<string, object>
            {
                ["name"] = t.name,
                ["instanceId"] = t.gameObject.GetInstanceID(),
                ["active"] = t.gameObject.activeSelf,
                ["childCount"] = t.childCount,
            };

            if (maxDepth < 0 || depth < maxDepth)
            {
                var children = new List<object>();
                for (int i = 0; i < t.childCount; i++)
                    children.Add(BuildNode(t.GetChild(i), maxDepth, depth + 1));
                node["children"] = children;
            }

            return node;
        }

        private static int CountHierarchy(Transform t, int maxDepth, int depth)
        {
            int count = 1;
            if (maxDepth < 0 || depth < maxDepth)
                for (int i = 0; i < t.childCount; i++)
                    count += CountHierarchy(t.GetChild(i), maxDepth, depth + 1);
            return count;
        }
    }
}
