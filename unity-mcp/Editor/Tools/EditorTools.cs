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

        [McpTool("editor_selection_set", "Set the Editor selection to specified objects",
            Group = "editor")]
        public static ToolResult SelectionSet(
            [Desc("Names or paths of GameObjects to select")] string[] targets = null,
            [Desc("Instance IDs to select")] int[] instanceIds = null,
            [Desc("Asset paths to select")] string[] assetPaths = null)
        {
            var objects = new System.Collections.Generic.List<Object>();

            if (targets != null)
            {
                foreach (var target in targets)
                {
                    var go = GameObjectTools.FindGameObject(target, null);
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
    }
}
