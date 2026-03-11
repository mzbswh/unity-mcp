#if UNITY_MCP_RUNTIME
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Runtime.Tools
{
    [McpToolGroup("Runtime.Control")]
    public static class RuntimeControlTools
    {
        [McpTool("runtime_time_scale", "Set or get Time.timeScale (pause, slow motion, fast forward)",
            Group = "runtime")]
        public static ToolResult TimeScale(
            [Desc("New time scale value (0=pause, 1=normal, 0.5=slow, 2=fast). Pass -1 to just read current value.")] float value = -1f)
        {
            if (value >= 0f)
            {
                Time.timeScale = value;
                return ToolResult.Json(new
                {
                    timeScale = Time.timeScale,
                    message = value == 0f ? "Game paused" :
                              value == 1f ? "Normal speed" :
                              value < 1f ? $"Slow motion ({value}x)" :
                              $"Fast forward ({value}x)"
                });
            }

            return ToolResult.Json(new
            {
                timeScale = Time.timeScale
            });
        }

        [McpTool("runtime_load_scene", "Load a scene at runtime",
            Group = "runtime")]
        public static ToolResult LoadScene(
            [Desc("Scene name or build index")] string scene,
            [Desc("Load mode: 'single' (replace) or 'additive' (add)")] string mode = "single")
        {
            var loadMode = mode.ToLower() == "additive"
                ? LoadSceneMode.Additive
                : LoadSceneMode.Single;

            // Try as build index first
            if (int.TryParse(scene, out int index))
            {
                if (index < 0 || index >= SceneManager.sceneCountInBuildSettings)
                    return ToolResult.Error($"Scene index {index} is out of range (0-{SceneManager.sceneCountInBuildSettings - 1})");

                SceneManager.LoadScene(index, loadMode);
                return ToolResult.Json(new
                {
                    sceneIndex = index,
                    mode = loadMode.ToString(),
                    message = $"Loading scene index {index} ({loadMode})"
                });
            }

            // Load by name
            SceneManager.LoadScene(scene, loadMode);
            return ToolResult.Json(new
            {
                sceneName = scene,
                mode = loadMode.ToString(),
                message = $"Loading scene '{scene}' ({loadMode})"
            });
        }
    }
}
#endif
