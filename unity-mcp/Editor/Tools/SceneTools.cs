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

        [McpTool("scene_view_get_settings", "Get Scene View display settings (2D mode, lighting, gizmos, grid, etc.)",
            Group = "scene", ReadOnly = true)]
        public static ToolResult SceneViewGetSettings()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
                return ToolResult.Error("No active Scene View");

            return ToolResult.Json(new
            {
                is2D = sv.in2DMode,
                orthographic = sv.orthographic,
                sceneLighting = sv.sceneLighting,
                drawGizmos = sv.drawGizmos,
                showGrid = sv.showGrid,
                size = sv.size,
                pivot = new { x = sv.pivot.x, y = sv.pivot.y, z = sv.pivot.z },
                cameraMode = new { drawMode = sv.cameraMode.drawMode.ToString(), name = sv.cameraMode.name },
            });
        }

        [McpTool("scene_view_set_settings", "Set Scene View display settings (2D mode, lighting, gizmos, grid, etc.)",
            Group = "scene")]
        public static ToolResult SceneViewSetSettings(
            [Desc("Enable 2D mode")] bool? is2D = null,
            [Desc("Enable scene lighting (false = unlit)")] bool? sceneLighting = null,
            [Desc("Show gizmos")] bool? drawGizmos = null,
            [Desc("Show grid")] bool? showGrid = null,
            [Desc("Orthographic projection")] bool? orthographic = null,
            [Desc("Draw mode: Textured, Wireframe, TexturedWire, ShadedWireframe, Shaded")] string drawMode = null)
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
                return ToolResult.Error("No active Scene View");

            int modified = 0;

            if (is2D.HasValue) { sv.in2DMode = is2D.Value; modified++; }
            if (sceneLighting.HasValue) { sv.sceneLighting = sceneLighting.Value; modified++; }
            if (drawGizmos.HasValue) { sv.drawGizmos = drawGizmos.Value; modified++; }
            if (showGrid.HasValue) { sv.showGrid = showGrid.Value; modified++; }
            if (orthographic.HasValue) { sv.orthographic = orthographic.Value; modified++; }

            if (!string.IsNullOrEmpty(drawMode))
            {
                if (System.Enum.TryParse<DrawCameraMode>(drawMode, true, out var dcm))
                {
                    var modes = SceneView.GetBuiltinCameraMode(dcm);
                    sv.cameraMode = modes;
                    modified++;
                }
            }

            sv.Repaint();
            return ToolResult.Text($"Modified {modified} Scene View settings");
        }

        [McpTool("game_view_get_settings", "Get Game View settings (resolution, scale, mute audio, stats, gizmos)",
            Group = "scene", ReadOnly = true)]
        public static ToolResult GameViewGetSettings()
        {
            var gameView = GetGameView();
            if (gameView == null)
                return ToolResult.Error("No Game View found");

            var gameViewType = gameView.GetType();

            // Use reflection since many GameView properties are internal
            bool muteAudio = (bool)(gameViewType.GetProperty("vSyncEnabled",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.GetValue(gameView) ?? false);

            return ToolResult.Json(new
            {
                targetDisplay = GetGameViewField<int>(gameView, "m_TargetDisplay"),
                showGizmos = GetGameViewField<bool>(gameView, "m_Gizmos"),
                showStats = GetGameViewField<bool>(gameView, "m_Stats"),
                muteAudio = EditorUtility.audioMasterMute,
                maximizeOnPlay = gameView.maximized,
            });
        }

        [McpTool("game_view_set_settings", "Set Game View settings (mute audio, stats, gizmos, maximize on play)",
            Group = "scene")]
        public static ToolResult GameViewSetSettings(
            [Desc("Mute audio globally in editor")] bool? muteAudio = null,
            [Desc("Show gizmos in Game View")] bool? showGizmos = null,
            [Desc("Show stats overlay")] bool? showStats = null,
            [Desc("Game View resolution as 'WxH' (e.g. '1920x1080', '1280x720'). Use 'Free' for free aspect.")] string resolution = null,
            [Desc("Target display index (0-based)")] int? targetDisplay = null)
        {
            var gameView = GetGameView();
            if (gameView == null)
                return ToolResult.Error("No Game View found");

            int modified = 0;

            if (muteAudio.HasValue)
            {
                EditorUtility.audioMasterMute = muteAudio.Value;
                modified++;
            }

            if (showGizmos.HasValue)
            {
                SetGameViewField(gameView, "m_Gizmos", showGizmos.Value);
                modified++;
            }

            if (showStats.HasValue)
            {
                SetGameViewField(gameView, "m_Stats", showStats.Value);
                modified++;
            }

            if (targetDisplay.HasValue)
            {
                SetGameViewField(gameView, "m_TargetDisplay", targetDisplay.Value);
                modified++;
            }

            if (!string.IsNullOrEmpty(resolution))
            {
                if (SetGameViewResolution(gameView, resolution))
                    modified++;
            }

            gameView.Repaint();
            return ToolResult.Text($"Modified {modified} Game View settings");
        }

        [McpTool("scene_view_snap_angle", "Snap the Scene View to a standard angle (Top, Bottom, Front, Back, Left, Right, Perspective)",
            Group = "scene")]
        public static ToolResult SceneViewSnapAngle(
            [Desc("View angle: Top, Bottom, Front, Back, Left, Right, Perspective, Isometric")] string angle)
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
                return ToolResult.Error("No active Scene View");

            Quaternion rot;
            bool ortho = true;
            switch (angle?.ToLower())
            {
                case "top":
                    rot = Quaternion.Euler(90, 0, 0);
                    break;
                case "bottom":
                    rot = Quaternion.Euler(-90, 0, 0);
                    break;
                case "front":
                    rot = Quaternion.Euler(0, 0, 0);
                    break;
                case "back":
                    rot = Quaternion.Euler(0, 180, 0);
                    break;
                case "left":
                    rot = Quaternion.Euler(0, 90, 0);
                    break;
                case "right":
                    rot = Quaternion.Euler(0, -90, 0);
                    break;
                case "perspective":
                    rot = Quaternion.Euler(30, -45, 0);
                    ortho = false;
                    break;
                case "isometric":
                    rot = Quaternion.Euler(30, -45, 0);
                    break;
                default:
                    return ToolResult.Error($"Unknown angle: {angle}. Use: Top, Bottom, Front, Back, Left, Right, Perspective, Isometric");
            }

            sv.orthographic = ortho;
            sv.rotation = rot;
            sv.Repaint();
            return ToolResult.Text($"Scene View snapped to {angle}");
        }

        private static EditorWindow GetGameView()
        {
            var gameViewType = System.Type.GetType("UnityEditor.GameView, UnityEditor");
            if (gameViewType == null) return null;
            var windows = UnityEngine.Resources.FindObjectsOfTypeAll(gameViewType);
            return windows.Length > 0 ? (EditorWindow)windows[0] : null;
        }

        private static T GetGameViewField<T>(EditorWindow gameView, string fieldName)
        {
            var field = gameView.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                return (T)field.GetValue(gameView);
            return default;
        }

        private static void SetGameViewField<T>(EditorWindow gameView, string fieldName, T value)
        {
            var field = gameView.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(gameView, value);
        }

        private static bool SetGameViewResolution(EditorWindow gameView, string resolution)
        {
            if (resolution.ToLower() == "free")
            {
                // Index 0 is typically "Free Aspect"
                SetSelectedSizeIndex(gameView, 0);
                return true;
            }

            var parts = resolution.ToLower().Split('x');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0].Trim(), out int width) ||
                !int.TryParse(parts[1].Trim(), out int height))
                return false;

            // Use GameViewSizeGroup via reflection to add/find custom resolution
            var gameViewSizesType = System.Type.GetType("UnityEditor.GameViewSizes, UnityEditor");
            if (gameViewSizesType == null) return false;

            var instanceProp = gameViewSizesType.GetProperty("instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (instanceProp == null) return false;
            var instance = instanceProp.GetValue(null);

            var getCurrentGroupMethod = instance.GetType().GetMethod("GetGroup");
            var currentGroupProp = instance.GetType().GetProperty("currentGroupType");
            var currentGroupTypeValue = currentGroupProp.GetValue(instance);
            var group = getCurrentGroupMethod.Invoke(instance,
                new object[] { (int)currentGroupTypeValue });

            var getTotalCountMethod = group.GetType().GetMethod("GetTotalCount");
            int totalCount = (int)getTotalCountMethod.Invoke(group, null);

            var getGameViewSizeMethod = group.GetType().GetMethod("GetGameViewSize");

            // Search existing sizes
            for (int i = 0; i < totalCount; i++)
            {
                var size = getGameViewSizeMethod.Invoke(group, new object[] { i });
                var w = (int)size.GetType().GetProperty("width").GetValue(size);
                var h = (int)size.GetType().GetProperty("height").GetValue(size);
                if (w == width && h == height)
                {
                    SetSelectedSizeIndex(gameView, i);
                    return true;
                }
            }

            // Add new size
            var gameViewSizeType = System.Type.GetType("UnityEditor.GameViewSize, UnityEditor");
            var sizeTypeEnum = System.Type.GetType("UnityEditor.GameViewSizeType, UnityEditor");
            var fixedRes = System.Enum.Parse(sizeTypeEnum, "FixedResolution");
            var newSize = System.Activator.CreateInstance(gameViewSizeType,
                new object[] { fixedRes, width, height, $"{width}x{height}" });
            var addMethod = group.GetType().GetMethod("AddCustomSize");
            addMethod.Invoke(group, new object[] { newSize });

            SetSelectedSizeIndex(gameView, totalCount);
            return true;
        }

        private static void SetSelectedSizeIndex(EditorWindow gameView, int index)
        {
            var prop = gameView.GetType().GetProperty("selectedSizeIndex",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (prop != null)
                prop.SetValue(gameView, index);
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
