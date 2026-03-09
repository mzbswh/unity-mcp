#if UNITY_MCP_RUNTIME
using UnityEngine;
using UnityEngine.Profiling;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Runtime.Resources
{
    [McpToolGroup("RuntimeStatsResources")]
    public static class RuntimeStatsResources
    {
        [McpResource("unity://runtime/stats", "Runtime Stats",
            "Current runtime performance statistics (FPS, memory, frame time)")]
        public static ToolResult GetStats()
        {
            return ToolResult.Json(new
            {
                fps = Mathf.RoundToInt(1f / Time.unscaledDeltaTime),
                frameTimeMs = Time.unscaledDeltaTime * 1000f,
                timeScale = Time.timeScale,
                memoryMB = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f),
                monoUsedMB = Profiler.GetMonoUsedSizeLong() / (1024f * 1024f),
                frameCount = Time.frameCount,
                realtimeSinceStartup = Time.realtimeSinceStartup
            });
        }
    }
}
#endif
