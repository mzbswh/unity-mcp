using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMcp.Editor.Core;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Resources
{
    [McpToolGroup("EditorResources")]
    public static class EditorResources
    {
        [McpResource("unity://editor/state", "Editor State",
            "Real-time editor state: compiling, playing, focused, selection, scene")]
        public static ToolResult GetEditorState()
        {
            var activeScene = SceneManager.GetActiveScene();
            return ToolResult.Json(new
            {
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                timeSinceStartup = EditorApplication.timeSinceStartup,
                applicationFocused = UnityEditorInternal.InternalEditorUtility.isApplicationActive,
                activeScene = new
                {
                    name = activeScene.name,
                    path = activeScene.path,
                    isDirty = activeScene.isDirty,
                },
                selection = new
                {
                    count = Selection.objects.Length,
                    activeObject = Selection.activeGameObject?.name,
                    activeInstanceId = Selection.activeGameObject?.GetInstanceID(),
                },
                platform = EditorUserBuildSettings.activeBuildTarget.ToString(),
                unityVersion = Application.unityVersion,
                mcpPort = McpServer.Transport?.Port,
                mcpClients = McpServer.Transport?.ClientCount ?? 0,
                mcpTools = McpServer.Registry?.ToolCount ?? 0,
                mcpResources = McpServer.Registry?.ResourceCount ?? 0,
                mcpPrompts = McpServer.Registry?.PromptCount ?? 0,
            });
        }

        [McpResource("unity://editor/selection", "Current Selection",
            "Detailed info about currently selected objects in the editor")]
        public static ToolResult GetSelection()
        {
            var gameObjects = Selection.gameObjects;
            var activeGo = Selection.activeGameObject;

            var selected = gameObjects.Select(go => new
            {
                instanceId = go.GetInstanceID(),
                name = go.name,
                path = GetGameObjectPath(go),
                type = "GameObject",
                isActive = go == activeGo,
            }).ToArray();

            var assetPaths = Selection.assetGUIDs.Select(guid =>
                AssetDatabase.GUIDToAssetPath(guid)).ToArray();

            return ToolResult.Json(new
            {
                count = selected.Length,
                activeObject = activeGo?.name,
                gameObjects = selected,
                assetPaths,
            });
        }

        private static string GetGameObjectPath(GameObject go)
        {
            var path = go.name;
            var t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }
    }
}
