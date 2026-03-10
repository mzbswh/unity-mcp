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

        [McpTool("scene_align_with_view", "Align a GameObject's transform to match the current Scene View camera (position and rotation)",
            Group = "scene")]
        public static ToolResult AlignWithView(
            [Desc("Name or path of the target GameObject")] string target)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return ToolResult.Error("No active Scene View");

            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target}");

            Undo.RecordObject(go.transform, "Align With View");
            go.transform.position = sceneView.camera.transform.position;
            go.transform.rotation = sceneView.camera.transform.rotation;

            return ToolResult.Json(new
            {
                success = true,
                gameObject = go.name,
                position = new { x = go.transform.position.x, y = go.transform.position.y, z = go.transform.position.z },
                rotation = new { x = go.transform.eulerAngles.x, y = go.transform.eulerAngles.y, z = go.transform.eulerAngles.z }
            });
        }

        [McpTool("scene_move_to_view", "Move a GameObject to the center of the current Scene View (position only, no rotation change)",
            Group = "scene")]
        public static ToolResult MoveToView(
            [Desc("Name or path of the target GameObject")] string target)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return ToolResult.Error("No active Scene View");

            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target}");

            Undo.RecordObject(go.transform, "Move To View");
            // Place at the scene view pivot (center of view)
            go.transform.position = sceneView.pivot;

            return ToolResult.Json(new
            {
                success = true,
                gameObject = go.name,
                position = new { x = go.transform.position.x, y = go.transform.position.y, z = go.transform.position.z }
            });
        }

        [McpTool("scene_frame_selected", "Focus the Scene View camera on specified GameObjects (like pressing F in the editor)",
            Group = "scene")]
        public static ToolResult FrameSelected(
            [Desc("Name or path of the GameObject to focus on (uses current selection if omitted)")] string target = null)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return ToolResult.Error("No active Scene View");

            if (target != null)
            {
                var go = GameObjectTools.FindGameObject(target, null);
                if (go == null)
                    return ToolResult.Error($"GameObject not found: {target}");
                Selection.activeGameObject = go;
            }

            if (Selection.activeGameObject == null)
                return ToolResult.Error("No GameObject selected to frame");

            sceneView.FrameSelected();

            return ToolResult.Text($"Framed '{Selection.activeGameObject.name}' in Scene View");
        }

        [McpTool("scene_view_get", "Get the current Scene View camera position and rotation",
            Group = "scene", ReadOnly = true)]
        public static ToolResult SceneViewGet()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return ToolResult.Error("No active Scene View");

            var cam = sceneView.camera.transform;
            return ToolResult.Json(new
            {
                pivot = new { x = sceneView.pivot.x, y = sceneView.pivot.y, z = sceneView.pivot.z },
                cameraPosition = new { x = cam.position.x, y = cam.position.y, z = cam.position.z },
                cameraRotation = new { x = cam.eulerAngles.x, y = cam.eulerAngles.y, z = cam.eulerAngles.z },
                size = sceneView.size,
                orthographic = sceneView.orthographic
            });
        }

        [McpTool("scene_view_set", "Set the Scene View camera position/rotation (look at a specific point)",
            Group = "scene")]
        public static ToolResult SceneViewSet(
            [Desc("Pivot point to look at {x, y, z}")] Vector3? pivot = null,
            [Desc("Camera rotation euler angles {x, y, z}")] Vector3? rotation = null,
            [Desc("View size (zoom level)")] float? size = null,
            [Desc("Use orthographic projection")] bool? orthographic = null)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return ToolResult.Error("No active Scene View");

            if (pivot.HasValue) sceneView.pivot = pivot.Value;
            if (rotation.HasValue) sceneView.rotation = Quaternion.Euler(rotation.Value);
            if (size.HasValue) sceneView.size = size.Value;
            if (orthographic.HasValue) sceneView.orthographic = orthographic.Value;

            sceneView.Repaint();

            return ToolResult.Text("Scene View updated");
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
