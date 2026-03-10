using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Editor")]
    public static class EditorTools
    {
        [McpTool("editor_get_state", "Get current Unity Editor state",
            Group = "editor", ReadOnly = true)]
        public static ToolResult GetState()
        {
            return ToolResult.Json(new
            {
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                timeSinceStartup = EditorApplication.timeSinceStartup,
                applicationFocused = UnityEditorInternal.InternalEditorUtility.isApplicationActive,
                platform = EditorUserBuildSettings.activeBuildTarget.ToString(),
                unityVersion = Application.unityVersion,
            });
        }

        [McpTool("editor_set_playmode", "Enter, exit, or pause Play mode",
            Group = "editor")]
        public static ToolResult SetPlayMode(
            [Desc("Action: play, stop, pause, unpause, step")] string action)
        {
            switch (action?.ToLower())
            {
                case "play":
                    if (!EditorApplication.isPlaying)
                        EditorApplication.isPlaying = true;
                    break;
                case "stop":
                    if (EditorApplication.isPlaying)
                        EditorApplication.isPlaying = false;
                    break;
                case "pause":
                    EditorApplication.isPaused = true;
                    break;
                case "unpause":
                    EditorApplication.isPaused = false;
                    break;
                case "step":
                    EditorApplication.Step();
                    break;
                default:
                    return ToolResult.Error($"Unknown action: '{action}'. Use: play, stop, pause, unpause, step");
            }
            return ToolResult.Text($"Play mode action: {action}");
        }

        [McpTool("editor_execute_menu", "Execute a Unity menu item by path",
            Group = "editor")]
        public static ToolResult ExecuteMenu(
            [Desc("Menu item path (e.g. 'Edit/Undo', 'GameObject/Create Empty')")] string menuPath)
        {
            if (string.IsNullOrEmpty(menuPath))
                return ToolResult.Error("Menu path is required");

            bool executed = EditorApplication.ExecuteMenuItem(menuPath);
            return executed
                ? ToolResult.Text($"Executed menu: '{menuPath}'")
                : ToolResult.Error($"Menu item not found or not available: '{menuPath}'");
        }

        [McpTool("editor_selection_get", "Get currently selected objects in the Editor",
            Group = "editor", ReadOnly = true)]
        public static ToolResult SelectionGet()
        {
            var objects = Selection.objects;
            var gameObjects = Selection.gameObjects;
            var activeGo = Selection.activeGameObject;

            var selected = gameObjects.Select(go => new
            {
                instanceId = go.GetInstanceID(),
                name = go.name,
                path = GameObjectTools.GetPath(go),
                isActive = go == activeGo,
            }).ToArray();

            return ToolResult.Json(new
            {
                count = selected.Length,
                activeObject = activeGo != null ? activeGo.name : null,
                gameObjects = selected,
                assetPaths = Selection.assetGUIDs.Select(guid =>
                    AssetDatabase.GUIDToAssetPath(guid)).ToArray(),
            });
        }

        [McpTool("editor_selection_set", "Set the Editor selection to specified objects. Pass a single name or an array of names.",
            Group = "editor")]
        public static ToolResult SelectionSet(
            [Desc("Name or path of a single GameObject to select")] string target = null,
            [Desc("Names or paths of multiple GameObjects to select")] string[] targets = null,
            [Desc("Instance IDs to select")] int[] instanceIds = null,
            [Desc("Asset paths to select")] string[] assetPaths = null)
        {
            var objects = new System.Collections.Generic.List<Object>();

            // Support single target parameter
            if (target != null)
            {
                var go = GameObjectTools.FindGameObject(target, null);
                if (go != null) objects.Add(go);
            }

            if (targets != null)
            {
                foreach (var t in targets)
                {
                    var go = GameObjectTools.FindGameObject(t, null);
                    if (go != null) objects.Add(go);
                }
            }

            if (instanceIds != null)
            {
                foreach (var id in instanceIds)
                {
                    var obj = EditorUtility.InstanceIDToObject(id);
                    if (obj != null) objects.Add(obj);
                }
            }

            if (assetPaths != null)
            {
                foreach (var path in assetPaths)
                {
                    var pv = PathValidator.QuickValidate(path);
                    if (!pv.IsValid) return ToolResult.Error(pv.Error);
                    var obj = AssetDatabase.LoadMainAssetAtPath(path);
                    if (obj != null) objects.Add(obj);
                }
            }

            Selection.objects = objects.ToArray();
            return ToolResult.Text($"Selected {objects.Count} object(s)");
        }

        [McpTool("editor_undo", "Perform Undo (like Ctrl+Z)",
            Group = "editor")]
        public static ToolResult PerformUndo()
        {
            UnityEditor.Undo.PerformUndo();
            return ToolResult.Text("Undo performed");
        }

        [McpTool("editor_redo", "Perform Redo (like Ctrl+Y)",
            Group = "editor")]
        public static ToolResult PerformRedo()
        {
            UnityEditor.Undo.PerformRedo();
            return ToolResult.Text("Redo performed");
        }

        [McpTool("editor_open_window", "Open a Unity Editor window by type name",
            Group = "editor")]
        public static ToolResult OpenWindow(
            [Desc("Window type: Inspector, Hierarchy, Project, Console, Scene, Game, Animation, Animator, Profiler, AssetStore, PackageManager, Lighting, Occlusion, Navigation, AudioMixer, VersionControl, SpriteEditor, SpritePacker, TileMap")] string windowType)
        {
            if (string.IsNullOrEmpty(windowType))
                return ToolResult.Error("Window type is required");

            // Map common names to menu paths
            string menuPath = windowType.ToLower() switch
            {
                "inspector" => "Window/General/Inspector",
                "hierarchy" => "Window/General/Hierarchy",
                "project" => "Window/General/Project",
                "console" => "Window/General/Console",
                "scene" => "Window/General/Scene",
                "game" => "Window/General/Game",
                "animation" => "Window/Animation/Animation",
                "animator" => "Window/Animation/Animator",
                "profiler" => "Window/Analysis/Profiler",
                "assetstore" => "Window/Asset Store",
                "packagemanager" => "Window/Package Manager",
                "lighting" => "Window/Rendering/Lighting",
                "occlusion" => "Window/Rendering/Occlusion Culling",
                "navigation" => "Window/AI/Navigation",
                "audiomixer" => "Window/Audio/Audio Mixer",
                "versioncontrol" => "Window/Version Control",
                "spriteeditor" => "Window/2D/Sprite Editor",
                "spritepacker" => "Window/2D/Sprite Packer",
                "tilemap" => "Window/2D/Tile Palette",
                _ => null,
            };

            if (menuPath != null)
            {
                bool executed = EditorApplication.ExecuteMenuItem(menuPath);
                if (executed)
                    return ToolResult.Text($"Opened window: {windowType}");
            }

            // Fallback: try to find the EditorWindow type by name
            var type = System.Type.GetType($"UnityEditor.{windowType}, UnityEditor");
            if (type == null)
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetTypes().FirstOrDefault(t =>
                        t.Name.Equals(windowType, System.StringComparison.OrdinalIgnoreCase) &&
                        typeof(EditorWindow).IsAssignableFrom(t));
                    if (type != null) break;
                }
            }

            if (type != null)
            {
                EditorWindow.GetWindow(type);
                return ToolResult.Text($"Opened window: {type.Name}");
            }

            return ToolResult.Error($"Unknown window type: {windowType}");
        }
    }
}
