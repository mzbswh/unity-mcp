#if UNITY_MCP_RUNTIME
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Runtime.Resources
{
    [McpToolGroup("RuntimeSceneResources")]
    public static class RuntimeSceneResources
    {
        [McpResource("unity://runtime/scene", "Runtime Scene Info",
            "Information about currently loaded scenes at runtime")]
        public static ToolResult GetSceneInfo()
        {
            var scenes = new List<object>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                scenes.Add(new
                {
                    name = scene.name,
                    path = scene.path,
                    buildIndex = scene.buildIndex,
                    isLoaded = scene.isLoaded,
                    rootCount = scene.rootCount,
                    isDirty = scene.isDirty
                });
            }

            return ToolResult.Json(new
            {
                activeScene = SceneManager.GetActiveScene().name,
                loadedSceneCount = SceneManager.sceneCount,
                totalScenesInBuild = SceneManager.sceneCountInBuildSettings,
                scenes
            });
        }
    }
}
#endif
