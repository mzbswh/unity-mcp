#if UNITY_MCP_RUNTIME
using UnityEngine;
using UnityEngine.Profiling;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Runtime.Tools
{
    [McpToolGroup("Runtime.Stats")]
    public static class RuntimeStatsTools
    {
        private static float _deltaTimeAccumulator;
        private static int _frameCount;
        private static float _currentFps;

        internal static void UpdateStats()
        {
            _frameCount++;
            _deltaTimeAccumulator += Time.unscaledDeltaTime;
            if (_deltaTimeAccumulator >= 1f)
            {
                _currentFps = _frameCount / _deltaTimeAccumulator;
                _frameCount = 0;
                _deltaTimeAccumulator = 0f;
            }
        }

        [McpTool("runtime_get_stats", "Get runtime performance statistics",
            ReadOnly = true, Idempotent = true, Group = "runtime")]
        public static ToolResult GetStats()
        {
            return ToolResult.Json(new
            {
                fps = Mathf.RoundToInt(_currentFps),
                frameTime = new
                {
                    current = Time.unscaledDeltaTime * 1000f,
                    smoothed = Time.smoothDeltaTime * 1000f
                },
                memory = new
                {
                    totalAllocatedMB = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f),
                    totalReservedMB = Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f),
                    monoUsedMB = Profiler.GetMonoUsedSizeLong() / (1024f * 1024f),
                    monoHeapMB = Profiler.GetMonoHeapSizeLong() / (1024f * 1024f),
                    gfxDriverMB = Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024f * 1024f)
                },
                objects = new
                {
                    gameObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length,
                },
                time = new
                {
                    timeScale = Time.timeScale,
                    realtimeSinceStartup = Time.realtimeSinceStartup,
                    frameCount = Time.frameCount
                }
            });
        }

        [McpTool("runtime_profiler_snapshot", "Get Profiler snapshot with memory breakdown",
            ReadOnly = true, Idempotent = true, Group = "runtime")]
        public static ToolResult ProfilerSnapshot()
        {
            return ToolResult.Json(new
            {
                memory = new
                {
                    totalAllocatedMB = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f),
                    totalReservedMB = Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f),
                    totalUnusedReservedMB = Profiler.GetTotalUnusedReservedMemoryLong() / (1024f * 1024f),
                    monoUsedMB = Profiler.GetMonoUsedSizeLong() / (1024f * 1024f),
                    monoHeapMB = Profiler.GetMonoHeapSizeLong() / (1024f * 1024f),
                    gfxDriverMB = Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024f * 1024f),
                    tempAllocatorMB = Profiler.GetTempAllocatorSize() / (1024f * 1024f),
                },
                rendering = new
                {
                    screenResolution = $"{Screen.width}x{Screen.height}",
                    currentResolution = $"{Screen.currentResolution.width}x{Screen.currentResolution.height}@{Screen.currentResolution.refreshRateRatio}",
                    targetFrameRate = Application.targetFrameRate,
                    vSyncCount = QualitySettings.vSyncCount,
                    qualityLevel = QualitySettings.names[QualitySettings.GetQualityLevel()]
                },
                physics = new
                {
                    fixedDeltaTime = Time.fixedDeltaTime,
                    gravity = Physics.gravity
                }
            });
        }
    }
}
#endif
