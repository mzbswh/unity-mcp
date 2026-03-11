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
            return ToolResult.Json(new { path, name = scene.name });
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
            return ToolResult.Json(new { name = scene.name, path = scene.path });
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

            var changes = new List<string>();

            if (is2D.HasValue) { sv.in2DMode = is2D.Value; changes.Add($"is2D={is2D.Value}"); }
            if (sceneLighting.HasValue) { sv.sceneLighting = sceneLighting.Value; changes.Add($"sceneLighting={sceneLighting.Value}"); }
            if (drawGizmos.HasValue) { sv.drawGizmos = drawGizmos.Value; changes.Add($"drawGizmos={drawGizmos.Value}"); }
            if (showGrid.HasValue) { sv.showGrid = showGrid.Value; changes.Add($"showGrid={showGrid.Value}"); }
            if (orthographic.HasValue) { sv.orthographic = orthographic.Value; changes.Add($"orthographic={orthographic.Value}"); }

            if (!string.IsNullOrEmpty(drawMode))
            {
                if (System.Enum.TryParse<DrawCameraMode>(drawMode, true, out var dcm))
                {
                    var modes = SceneView.GetBuiltinCameraMode(dcm);
                    sv.cameraMode = modes;
                    changes.Add($"drawMode={dcm}");
                }
            }

            sv.Repaint();
            if (changes.Count == 0) return ToolResult.Text("No Scene View settings changed");
            return ToolResult.Text($"Scene View updated: {string.Join(", ", changes)}");
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

            // Get current resolution info
            string currentResolution = "Free Aspect";
            int resWidth = 0, resHeight = 0;
            try
            {
                var sizeProp = gameViewType.GetProperty("selectedSizeIndex",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                int sizeIndex = sizeProp != null ? (int)sizeProp.GetValue(gameView) : 0;

                var gameViewSizesType = System.Type.GetType("UnityEditor.GameViewSizes, UnityEditor");
                var instanceProp = gameViewSizesType?.GetProperty("instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
                var instance = instanceProp?.GetValue(null);
                var currentGroupProp = instance?.GetType().GetProperty("currentGroupType");
                var currentGroupTypeValue = currentGroupProp?.GetValue(instance);
                var getGroupMethod = instance?.GetType().GetMethod("GetGroup");
                var group = getGroupMethod?.Invoke(instance, new object[] { (int)currentGroupTypeValue });
                var getSizeMethod = group?.GetType().GetMethod("GetGameViewSize");

                if (getSizeMethod != null && sizeIndex >= 0)
                {
                    var size = getSizeMethod.Invoke(group, new object[] { sizeIndex });
                    if (size != null)
                    {
                        resWidth = (int)size.GetType().GetProperty("width").GetValue(size);
                        resHeight = (int)size.GetType().GetProperty("height").GetValue(size);
                        var displayText = (string)size.GetType().GetMethod("GetDisplayText")?.Invoke(size, null);
                        currentResolution = !string.IsNullOrEmpty(displayText) ? displayText :
                            (resWidth > 0 && resHeight > 0 ? $"{resWidth}x{resHeight}" : "Free Aspect");
                    }
                }
            }
            catch { /* reflection may fail on some Unity versions */ }

            var viewRect = gameView.position;

            // Get current zoom/scale area
            float scale = GetGameViewScale(gameView);

            return ToolResult.Json(new
            {
                targetDisplay = GetGameViewField<int>(gameView, "m_TargetDisplay"),
                resolution = currentResolution,
                resolutionWidth = resWidth,
                resolutionHeight = resHeight,
                scale,
                viewSize = new { width = (int)viewRect.width, height = (int)viewRect.height },
                showGizmos = GetGameViewField<bool>(gameView, "m_Gizmos"),
                showStats = GetGameViewField<bool>(gameView, "m_Stats"),
                muteAudio = EditorUtility.audioMasterMute,
                maximizeOnPlay = gameView.maximized,
            });
        }

        [McpTool("game_view_set_settings", "Set Game View settings (resolution, scale, mute audio, stats, gizmos, target display)",
            Group = "scene")]
        public static ToolResult GameViewSetSettings(
            [Desc("Mute audio globally in editor")] bool? muteAudio = null,
            [Desc("Show gizmos in Game View")] bool? showGizmos = null,
            [Desc("Show stats overlay")] bool? showStats = null,
            [Desc("Game View resolution as 'WxH' (e.g. '1920x1080', '1280x720'). Use 'Free' for free aspect.")] string resolution = null,
            [Desc("Scale/zoom level (e.g. 0.5, 1.0, 2.0). Use -1 to auto-fit the resolution in the view.")] float? scale = null,
            [Desc("Target display index (0-based)")] int? targetDisplay = null)
        {
            var gameView = GetGameView();
            if (gameView == null)
                return ToolResult.Error("No Game View found");

            var changes = new List<string>();
            string resolutionError = null;

            if (muteAudio.HasValue)
            {
                EditorUtility.audioMasterMute = muteAudio.Value;
                changes.Add($"muteAudio={muteAudio.Value}");
            }

            if (showGizmos.HasValue)
            {
                SetGameViewField(gameView, "m_Gizmos", showGizmos.Value);
                changes.Add($"showGizmos={showGizmos.Value}");
            }

            if (showStats.HasValue)
            {
                SetGameViewField(gameView, "m_Stats", showStats.Value);
                changes.Add($"showStats={showStats.Value}");
            }

            if (targetDisplay.HasValue)
            {
                SetGameViewField(gameView, "m_TargetDisplay", targetDisplay.Value);
                changes.Add($"targetDisplay={targetDisplay.Value}");
            }

            if (!string.IsNullOrEmpty(resolution))
            {
                resolutionError = SetGameViewResolution(gameView, resolution);
                if (resolutionError == null)
                    changes.Add($"resolution={resolution}");
            }

            if (scale.HasValue)
            {
                if (SetGameViewScale(gameView, scale.Value))
                    changes.Add(scale.Value < 0 ? "scale=auto-fit" : $"scale={scale.Value:F2}");
            }

            gameView.Repaint();

            if (resolutionError != null)
                return ToolResult.Error($"Failed to set resolution: {resolutionError}." +
                    (changes.Count > 0 ? $" Other changes applied: {string.Join(", ", changes)}" : ""));

            if (changes.Count == 0)
                return ToolResult.Text("No settings were changed");

            return ToolResult.Text($"Game View updated: {string.Join(", ", changes)}");
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

        private static float GetGameViewScale(EditorWindow gameView)
        {
            try
            {
                // GameView stores zoom in m_ZoomArea (ZoomableArea) which has m_Scale (Vector2)
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var zoomAreaField = gameView.GetType().GetField("m_ZoomArea", flags);
                if (zoomAreaField == null) return -1f;
                var zoomArea = zoomAreaField.GetValue(gameView);
                if (zoomArea == null) return -1f;

                var scaleProp = zoomArea.GetType().GetProperty("scale", flags | System.Reflection.BindingFlags.Public);
                if (scaleProp != null)
                {
                    var scaleVec = (Vector2)scaleProp.GetValue(zoomArea);
                    return scaleVec.x;
                }

                var scaleField = zoomArea.GetType().GetField("m_Scale", flags);
                if (scaleField != null)
                {
                    var scaleVec = (Vector2)scaleField.GetValue(zoomArea);
                    return scaleVec.x;
                }
            }
            catch { }
            return -1f;
        }

        private static bool SetGameViewScale(EditorWindow gameView, float scale)
        {
            try
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var zoomAreaField = gameView.GetType().GetField("m_ZoomArea", flags);
                if (zoomAreaField == null) return false;
                var zoomArea = zoomAreaField.GetValue(gameView);
                if (zoomArea == null) return false;

                // Auto-fit: calculate scale to fit resolution in the view
                if (scale < 0f)
                {
                    var viewRect = gameView.position;
                    // Account for toolbar height (~17px)
                    float viewH = viewRect.height - 17f;
                    float viewW = viewRect.width;

                    // Get the target resolution size from the current game view size
                    var getTargetSize = gameView.GetType().GetMethod("GetSizeOfMainGameView",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                    Vector2 targetSize;
                    if (getTargetSize != null)
                    {
                        targetSize = (Vector2)getTargetSize.Invoke(null, null);
                    }
                    else
                    {
                        // Fallback: read from the selected game view size
                        targetSize = new Vector2(viewW, viewH);
                    }

                    if (targetSize.x > 0 && targetSize.y > 0)
                    {
                        float scaleX = viewW / targetSize.x;
                        float scaleY = viewH / targetSize.y;
                        scale = Mathf.Min(scaleX, scaleY);
                        scale = Mathf.Max(scale, 0.01f);
                    }
                    else
                    {
                        scale = 1f;
                    }
                }

                var scaleProp = zoomArea.GetType().GetProperty("scale",
                    flags | System.Reflection.BindingFlags.Public);
                if (scaleProp != null && scaleProp.CanWrite)
                {
                    scaleProp.SetValue(zoomArea, new Vector2(scale, scale));
                    return true;
                }

                var scaleField = zoomArea.GetType().GetField("m_Scale", flags);
                if (scaleField != null)
                {
                    scaleField.SetValue(zoomArea, new Vector2(scale, scale));
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Sets Game View resolution. Returns null on success, error message on failure.
        /// </summary>
        private static string SetGameViewResolution(EditorWindow gameView, string resolution)
        {
            try
            {
                if (resolution.Trim().ToLower() == "free")
                {
                    SetSelectedSizeIndex(gameView, 0);
                    return null;
                }

                var parts = resolution.Split('x', 'X');
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0].Trim(), out int width) ||
                    !int.TryParse(parts[1].Trim(), out int height))
                    return $"Invalid format '{resolution}'. Use 'WxH' (e.g. '1920x1080') or 'Free'.";

                if (width <= 0 || height <= 0)
                    return $"Width and height must be positive, got {width}x{height}";

                // Access GameViewSizes singleton via reflection
                // The 'instance' property is inherited from ScriptableSingleton<T>,
                // so we must include FlattenHierarchy to search base classes.
                var gameViewSizesType = System.Type.GetType("UnityEditor.GameViewSizes, UnityEditor");
                if (gameViewSizesType == null)
                    return "Cannot find UnityEditor.GameViewSizes type";

                var instanceProp = gameViewSizesType.GetProperty("instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
                if (instanceProp == null)
                    return "Cannot find GameViewSizes.instance property";
                var instance = instanceProp.GetValue(null);
                if (instance == null)
                    return "GameViewSizes.instance returned null";

                var currentGroupProp = instance.GetType().GetProperty("currentGroupType");
                var currentGroupTypeValue = currentGroupProp.GetValue(instance);
                var getGroupMethod = instance.GetType().GetMethod("GetGroup");
                var group = getGroupMethod.Invoke(instance, new object[] { (int)currentGroupTypeValue });

                var getTotalCountMethod = group.GetType().GetMethod("GetTotalCount");
                int totalCount = (int)getTotalCountMethod.Invoke(group, null);

                var getSizeMethod = group.GetType().GetMethod("GetGameViewSize");

                // Search existing sizes for a match
                for (int i = 0; i < totalCount; i++)
                {
                    var size = getSizeMethod.Invoke(group, new object[] { i });
                    var w = (int)size.GetType().GetProperty("width").GetValue(size);
                    var h = (int)size.GetType().GetProperty("height").GetValue(size);
                    if (w == width && h == height)
                    {
                        SetSelectedSizeIndex(gameView, i);
                        return null; // success
                    }
                }

                // Not found — add a new custom size
                var gameViewSizeType = System.Type.GetType("UnityEditor.GameViewSize, UnityEditor");
                var sizeTypeEnum = System.Type.GetType("UnityEditor.GameViewSizeType, UnityEditor");
                if (gameViewSizeType == null || sizeTypeEnum == null)
                    return "Cannot find GameViewSize or GameViewSizeType types";

                var fixedRes = System.Enum.Parse(sizeTypeEnum, "FixedResolution");

                // GameViewSize constructor varies by Unity version.
                // Try (GameViewSizeType, int, int, string) first,
                // then (int, int, int, string) as fallback.
                object newSize = null;
                var ctors = gameViewSizeType.GetConstructors();
                foreach (var ctor in ctors)
                {
                    var ps = ctor.GetParameters();
                    if (ps.Length == 4 && ps[0].ParameterType == sizeTypeEnum
                        && ps[1].ParameterType == typeof(int)
                        && ps[2].ParameterType == typeof(int)
                        && ps[3].ParameterType == typeof(string))
                    {
                        newSize = ctor.Invoke(new object[] { fixedRes, width, height, $"{width}x{height}" });
                        break;
                    }
                }
                if (newSize == null)
                {
                    // Fallback: try enum as int
                    foreach (var ctor in ctors)
                    {
                        var ps = ctor.GetParameters();
                        if (ps.Length == 4 && ps[0].ParameterType == typeof(int)
                            && ps[1].ParameterType == typeof(int)
                            && ps[2].ParameterType == typeof(int)
                            && ps[3].ParameterType == typeof(string))
                        {
                            newSize = ctor.Invoke(new object[] { (int)fixedRes, width, height, $"{width}x{height}" });
                            break;
                        }
                    }
                }
                if (newSize == null)
                    return $"Cannot create GameViewSize — no matching constructor found. Available: {string.Join(", ", System.Array.ConvertAll(ctors, c => $"({string.Join(", ", System.Array.ConvertAll(c.GetParameters(), p => p.ParameterType.Name))})"))}";

                var addMethod = group.GetType().GetMethod("AddCustomSize");
                if (addMethod == null)
                    return "Cannot find AddCustomSize method on GameViewSizeGroup";

                addMethod.Invoke(group, new object[] { newSize });
                SetSelectedSizeIndex(gameView, totalCount);
                gameView.Repaint();
                return null; // success
            }
            catch (System.Exception ex)
            {
                return $"Exception: {ex.Message}";
            }
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
