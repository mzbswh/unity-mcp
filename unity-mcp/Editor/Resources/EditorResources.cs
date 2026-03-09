using UnityEditor;
using UnityEngine;
using UnityMcp.Editor.Core;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Resources
{
    [McpToolGroup("EditorResources")]
    public static class EditorResources
    {
        [McpResource("unity://editor/state", "Editor State",
            "Current Unity Editor state including play mode, compilation, and focus")]
        public static ToolResult GetEditorState()
        {
            return ToolResult.Json(new
            {
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                timeSinceStartup = EditorApplication.timeSinceStartup,
                applicationFocused = UnityEditorInternal.InternalEditorUtility.isApplicationActive,
                mcpPort = McpServer.Transport?.Port,
                mcpClients = McpServer.Transport?.ClientCount ?? 0,
                mcpTools = McpServer.Registry?.ToolCount ?? 0,
                mcpResources = McpServer.Registry?.ResourceCount ?? 0,
                mcpPrompts = McpServer.Registry?.PromptCount ?? 0,
            });
        }
    }
}
