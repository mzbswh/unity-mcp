using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Graphics")]
    public static class GraphicsTools
    {
        // --- Skybox ---

        [McpTool("graphics_get_skybox", "Get current skybox material and settings",
            Group = "graphics", ReadOnly = true)]
        public static ToolResult GetSkybox()
        {
            var skybox = RenderSettings.skybox;
            if (skybox == null)
                return ToolResult.Json(new { hasSkybox = false });

            var path = AssetDatabase.GetAssetPath(skybox);
            return ToolResult.Json(new
            {
                hasSkybox = true,
                materialName = skybox.name,
                materialPath = path,
                shaderName = skybox.shader?.name,
            });
        }

        [McpTool("graphics_set_skybox", "Set the scene skybox material",
            Group = "graphics")]
        public static ToolResult SetSkybox(
            [Desc("Skybox material asset path (e.g. Assets/Materials/MySkybox.mat)")] string materialPath)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null)
                return ToolResult.Error($"Material not found: {materialPath}");

            Undo.RecordObject(RenderSettings.GetRenderSettings(), "Set Skybox");
            RenderSettings.skybox = mat;
            return ToolResult.Json(new { skybox = mat.name, path = materialPath });
        }

        // --- Fog ---

        [McpTool("graphics_get_fog", "Get current fog settings",
            Group = "graphics", ReadOnly = true)]
        public static ToolResult GetFog()
        {
            return ToolResult.Json(new
            {
                enabled = RenderSettings.fog,
                mode = RenderSettings.fogMode.ToString(),
                color = ColorUtility.ToHtmlStringRGBA(RenderSettings.fogColor),
                density = RenderSettings.fogDensity,
                startDistance = RenderSettings.fogStartDistance,
                endDistance = RenderSettings.fogEndDistance,
            });
        }

        [McpTool("graphics_set_fog", "Configure scene fog settings",
            Group = "graphics")]
        public static ToolResult SetFog(
            [Desc("Enable/disable fog")] bool? enabled = null,
            [Desc("Fog mode: Linear, Exponential, ExponentialSquared")] string mode = null,
            [Desc("Fog color (hex e.g. '#808080FF')")] string color = null,
            [Desc("Fog density (for Exponential modes)")] float? density = null,
            [Desc("Start distance (for Linear mode)")] float? startDistance = null,
            [Desc("End distance (for Linear mode)")] float? endDistance = null)
        {
            Undo.RecordObject(RenderSettings.GetRenderSettings(), "Set Fog");

            if (enabled.HasValue) RenderSettings.fog = enabled.Value;
            if (!string.IsNullOrEmpty(mode) && Enum.TryParse<FogMode>(mode, true, out var fogMode))
                RenderSettings.fogMode = fogMode;
            if (!string.IsNullOrEmpty(color) && ColorUtility.TryParseHtmlString(color, out var fogColor))
                RenderSettings.fogColor = fogColor;
            if (density.HasValue) RenderSettings.fogDensity = density.Value;
            if (startDistance.HasValue) RenderSettings.fogStartDistance = startDistance.Value;
            if (endDistance.HasValue) RenderSettings.fogEndDistance = endDistance.Value;

            return ToolResult.Json(new
            {
                enabled = RenderSettings.fog,
                mode = RenderSettings.fogMode.ToString(),
                density = RenderSettings.fogDensity,
            });
        }

        // --- Ambient Lighting ---

        [McpTool("graphics_get_ambient", "Get ambient lighting settings",
            Group = "graphics", ReadOnly = true)]
        public static ToolResult GetAmbient()
        {
            return ToolResult.Json(new
            {
                mode = RenderSettings.ambientMode.ToString(),
                skyColor = ColorUtility.ToHtmlStringRGBA(RenderSettings.ambientSkyColor),
                equatorColor = ColorUtility.ToHtmlStringRGBA(RenderSettings.ambientEquatorColor),
                groundColor = ColorUtility.ToHtmlStringRGBA(RenderSettings.ambientGroundColor),
                intensity = RenderSettings.ambientIntensity,
            });
        }

        [McpTool("graphics_set_ambient", "Configure ambient lighting settings",
            Group = "graphics")]
        public static ToolResult SetAmbient(
            [Desc("Ambient mode: Skybox, Trilight, Flat, Custom")] string mode = null,
            [Desc("Sky color (hex)")] string skyColor = null,
            [Desc("Equator color (hex, for Trilight)")] string equatorColor = null,
            [Desc("Ground color (hex, for Trilight)")] string groundColor = null,
            [Desc("Intensity multiplier")] float? intensity = null)
        {
            Undo.RecordObject(RenderSettings.GetRenderSettings(), "Set Ambient");

            if (!string.IsNullOrEmpty(mode) && Enum.TryParse<AmbientMode>(mode, true, out var ambientMode))
                RenderSettings.ambientMode = ambientMode;
            if (!string.IsNullOrEmpty(skyColor) && ColorUtility.TryParseHtmlString(skyColor, out var sc))
                RenderSettings.ambientSkyColor = sc;
            if (!string.IsNullOrEmpty(equatorColor) && ColorUtility.TryParseHtmlString(equatorColor, out var ec))
                RenderSettings.ambientEquatorColor = ec;
            if (!string.IsNullOrEmpty(groundColor) && ColorUtility.TryParseHtmlString(groundColor, out var gc))
                RenderSettings.ambientGroundColor = gc;
            if (intensity.HasValue)
                RenderSettings.ambientIntensity = intensity.Value;

            return ToolResult.Json(new
            {
                mode = RenderSettings.ambientMode.ToString(),
                intensity = RenderSettings.ambientIntensity,
            });
        }

        // --- Render Pipeline ---

        [McpTool("graphics_get_render_pipeline", "Get information about the current render pipeline",
            Group = "graphics", ReadOnly = true)]
        public static ToolResult GetRenderPipeline()
        {
            var currentRP = GraphicsSettings.currentRenderPipeline;
            var defaultRP = GraphicsSettings.defaultRenderPipeline;

            return ToolResult.Json(new
            {
                currentPipeline = currentRP != null ? currentRP.GetType().Name : "Built-in",
                currentPipelineAsset = currentRP != null ? AssetDatabase.GetAssetPath(currentRP) : null,
                defaultPipeline = defaultRP != null ? defaultRP.GetType().Name : "Built-in",
                defaultPipelineAsset = defaultRP != null ? AssetDatabase.GetAssetPath(defaultRP) : null,
                colorSpace = QualitySettings.activeColorSpace.ToString(),
            });
        }

        // --- Quality Settings ---

        [McpTool("graphics_get_quality", "Get current quality settings level and details",
            Group = "graphics", ReadOnly = true)]
        public static ToolResult GetQuality()
        {
            var names = QualitySettings.names;
            return ToolResult.Json(new
            {
                currentLevel = QualitySettings.GetQualityLevel(),
                currentName = names.Length > 0 ? names[QualitySettings.GetQualityLevel()] : "Unknown",
                availableLevels = names,
                shadowDistance = QualitySettings.shadowDistance,
                shadowResolution = QualitySettings.shadowResolution.ToString(),
                antiAliasing = QualitySettings.antiAliasing,
                vSyncCount = QualitySettings.vSyncCount,
                lodBias = QualitySettings.lodBias,
                maxLod = QualitySettings.maximumLODLevel,
            });
        }

        [McpTool("graphics_set_quality", "Set quality level by name or index",
            Group = "graphics")]
        public static ToolResult SetQuality(
            [Desc("Quality level name (e.g. 'Ultra') or index")] string level)
        {
            var names = QualitySettings.names;

            if (int.TryParse(level, out int idx))
            {
                if (idx < 0 || idx >= names.Length)
                    return ToolResult.Error($"Quality index {idx} out of range (0-{names.Length - 1})");
                QualitySettings.SetQualityLevel(idx, true);
            }
            else
            {
                idx = Array.FindIndex(names, n => string.Equals(n, level, StringComparison.OrdinalIgnoreCase));
                if (idx < 0)
                    return ToolResult.Error($"Quality level '{level}' not found. Available: {string.Join(", ", names)}");
                QualitySettings.SetQualityLevel(idx, true);
            }

            return ToolResult.Json(new
            {
                level = QualitySettings.GetQualityLevel(),
                name = names[QualitySettings.GetQualityLevel()],
            });
        }

        // --- Rendering Stats ---

        [McpTool("graphics_get_stats", "Get rendering statistics from the current scene view or game view",
            Group = "graphics", ReadOnly = true)]
        public static ToolResult GetStats()
        {
            var sceneView = SceneView.lastActiveSceneView;
            var camera = sceneView?.camera ?? Camera.main;

            return ToolResult.Json(new
            {
                systemInfo = new
                {
                    gpuName = SystemInfo.graphicsDeviceName,
                    gpuVendor = SystemInfo.graphicsDeviceVendor,
                    gpuMemory = SystemInfo.graphicsMemorySize,
                    graphicsAPI = SystemInfo.graphicsDeviceType.ToString(),
                    maxTextureSize = SystemInfo.maxTextureSize,
                    shaderLevel = SystemInfo.graphicsShaderLevel,
                    supportsInstancing = SystemInfo.supportsInstancing,
                    supportsComputeShaders = SystemInfo.supportsComputeShaders,
                    supportsRayTracing = SystemInfo.supportsRayTracing,
                },
                sceneStats = new
                {
                    hasCamera = camera != null,
                    cameraName = camera?.name,
                },
            });
        }

        // --- Light Baking ---

        [McpTool("graphics_bake_lighting", "Start lightmap baking for the current scene",
            Group = "graphics")]
        public static ToolResult BakeLighting(
            [Desc("If true, clear baked data first")] bool clearFirst = false)
        {
            if (clearFirst)
                Lightmapping.Clear();

            Lightmapping.BakeAsync();

            return ToolResult.Json(new
            {
                started = true,
                isRunning = Lightmapping.isRunning,
                message = "Lightmap baking started asynchronously",
            });
        }

        [McpTool("graphics_get_lightmap_settings", "Get current lightmap baking settings",
            Group = "graphics", ReadOnly = true)]
        public static ToolResult GetLightmapSettings()
        {
            return ToolResult.Json(new
            {
                isRunning = Lightmapping.isRunning,
                lightmapCount = LightmapSettings.lightmaps?.Length ?? 0,
                giWorkflowMode = Lightmapping.giWorkflowMode.ToString(),
                bakedGI = Lightmapping.bakedGI,
                realtimeGI = Lightmapping.realtimeGI,
            });
        }
    }
}
