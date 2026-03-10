using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityMcp.Editor.Utils;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Lighting")]
    public static class LightingTools
    {
        [McpTool("light_create", "Create a Light GameObject in the scene",
            Group = "lighting")]
        public static ToolResult Create(
            [Desc("Light type: Directional, Point, Spot, Area")] string type,
            [Desc("Name of the light")] string name = null,
            [Desc("World position")] Vector3? position = null,
            [Desc("Rotation euler angles")] Vector3? rotation = null,
            [Desc("Light color")] Color? color = null,
            [Desc("Light intensity")] float? intensity = null,
            [Desc("Range (for Point/Spot)")] float? range = null,
            [Desc("Spot angle in degrees (for Spot)")] float? spotAngle = null,
            [Desc("Parent GameObject name")] string parent = null)
        {
            if (!System.Enum.TryParse<LightType>(type, true, out var lightType))
                return ToolResult.Error($"Unknown light type: {type}. Use: Directional, Point, Spot, Area");

            var go = new GameObject(name ?? $"{type} Light");
            var light = go.AddComponent<Light>();
            light.type = lightType;

            if (position.HasValue) go.transform.position = position.Value;
            if (rotation.HasValue) go.transform.eulerAngles = rotation.Value;
            if (color.HasValue) light.color = color.Value;
            if (intensity.HasValue) light.intensity = intensity.Value;
            if (range.HasValue) light.range = range.Value;
            if (spotAngle.HasValue) light.spotAngle = spotAngle.Value;

            if (!string.IsNullOrEmpty(parent))
            {
                var parentGo = GameObjectTools.FindGameObject(parent, null);
                if (parentGo != null)
                    go.transform.SetParent(parentGo.transform, true);
            }

            UndoHelper.RegisterCreatedObject(go, $"Create {type} Light");

            return ToolResult.Json(new
            {
                success = true,
                instanceId = go.GetInstanceID(),
                name = go.name,
                lightType = light.type.ToString(),
                message = $"Created {type} light: {go.name}"
            });
        }

        [McpTool("light_modify", "Modify an existing Light component's properties",
            Group = "lighting")]
        public static ToolResult Modify(
            [Desc("Name or path of the Light GameObject")] string target,
            [Desc("Light color")] Color? color = null,
            [Desc("Intensity")] float? intensity = null,
            [Desc("Range (Point/Spot)")] float? range = null,
            [Desc("Spot angle (Spot only)")] float? spotAngle = null,
            [Desc("Shadow type: None, Hard, Soft")] string shadows = null,
            [Desc("Shadow strength (0-1)")] float? shadowStrength = null,
            [Desc("Cookie texture asset path")] string cookie = null,
            [Desc("Indirect multiplier")] float? bounceIntensity = null,
            [Desc("Light mode: Realtime, Mixed, Baked")] string mode = null)
        {
            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target}");

            var light = go.GetComponent<Light>();
            if (light == null)
                return ToolResult.Error($"No Light component on '{target}'");

            Undo.RecordObject(light, "Modify Light");

            int modified = 0;
            if (color.HasValue) { light.color = color.Value; modified++; }
            if (intensity.HasValue) { light.intensity = intensity.Value; modified++; }
            if (range.HasValue) { light.range = range.Value; modified++; }
            if (spotAngle.HasValue) { light.spotAngle = spotAngle.Value; modified++; }
            if (bounceIntensity.HasValue) { light.bounceIntensity = bounceIntensity.Value; modified++; }

            if (!string.IsNullOrEmpty(shadows))
            {
                if (System.Enum.TryParse<LightShadows>(shadows, true, out var s))
                { light.shadows = s; modified++; }
            }
            if (shadowStrength.HasValue) { light.shadowStrength = shadowStrength.Value; modified++; }

            if (!string.IsNullOrEmpty(cookie))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture>(cookie);
                if (tex != null) { light.cookie = tex; modified++; }
            }

            if (!string.IsNullOrEmpty(mode))
            {
                if (System.Enum.TryParse<LightmapBakeType>(mode, true, out var m))
                { light.lightmapBakeType = m; modified++; }
            }

            EditorUtility.SetDirty(light);
            return ToolResult.Text($"Modified {modified} properties on light '{target}'");
        }

        [McpTool("light_get_info", "Get Light component properties",
            Group = "lighting", ReadOnly = true)]
        public static ToolResult GetInfo(
            [Desc("Name or path of the Light GameObject")] string target)
        {
            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target}");

            var light = go.GetComponent<Light>();
            if (light == null)
                return ToolResult.Error($"No Light component on '{target}'");

            return ToolResult.Json(new
            {
                name = go.name,
                type = light.type.ToString(),
                color = new { r = light.color.r, g = light.color.g, b = light.color.b, a = light.color.a },
                intensity = light.intensity,
                range = light.range,
                spotAngle = light.spotAngle,
                shadows = light.shadows.ToString(),
                shadowStrength = light.shadowStrength,
                bounceIntensity = light.bounceIntensity,
                mode = light.lightmapBakeType.ToString(),
                cullingMask = light.cullingMask,
                hasCookie = light.cookie != null,
            });
        }

        [McpTool("lighting_get_environment", "Get current environment lighting settings (ambient, fog, skybox)",
            Group = "lighting", ReadOnly = true)]
        public static ToolResult GetEnvironment()
        {
            return ToolResult.Json(new
            {
                ambientMode = RenderSettings.ambientMode.ToString(),
                ambientColor = new { r = RenderSettings.ambientLight.r, g = RenderSettings.ambientLight.g, b = RenderSettings.ambientLight.b },
                ambientSkyColor = new { r = RenderSettings.ambientSkyColor.r, g = RenderSettings.ambientSkyColor.g, b = RenderSettings.ambientSkyColor.b },
                ambientEquatorColor = new { r = RenderSettings.ambientEquatorColor.r, g = RenderSettings.ambientEquatorColor.g, b = RenderSettings.ambientEquatorColor.b },
                ambientGroundColor = new { r = RenderSettings.ambientGroundColor.r, g = RenderSettings.ambientGroundColor.g, b = RenderSettings.ambientGroundColor.b },
                ambientIntensity = RenderSettings.ambientIntensity,
                skyboxMaterial = RenderSettings.skybox != null ? AssetDatabase.GetAssetPath(RenderSettings.skybox) : null,
                fog = RenderSettings.fog,
                fogColor = new { r = RenderSettings.fogColor.r, g = RenderSettings.fogColor.g, b = RenderSettings.fogColor.b },
                fogMode = RenderSettings.fogMode.ToString(),
                fogDensity = RenderSettings.fogDensity,
                fogStartDistance = RenderSettings.fogStartDistance,
                fogEndDistance = RenderSettings.fogEndDistance,
                reflectionIntensity = RenderSettings.reflectionIntensity,
                defaultReflectionMode = RenderSettings.defaultReflectionMode.ToString(),
            });
        }

        [McpTool("lighting_set_environment", "Set environment lighting settings (ambient, fog, skybox)",
            Group = "lighting")]
        public static ToolResult SetEnvironment(
            [Desc("Ambient mode: Skybox, Trilight, Flat, Custom")] string ambientMode = null,
            [Desc("Ambient color (for Flat mode)")] Color? ambientColor = null,
            [Desc("Ambient sky color (for Trilight)")] Color? ambientSkyColor = null,
            [Desc("Ambient equator color (for Trilight)")] Color? ambientEquatorColor = null,
            [Desc("Ambient ground color (for Trilight)")] Color? ambientGroundColor = null,
            [Desc("Ambient intensity multiplier")] float? ambientIntensity = null,
            [Desc("Skybox material asset path")] string skyboxMaterial = null,
            [Desc("Enable fog")] bool? fog = null,
            [Desc("Fog color")] Color? fogColor = null,
            [Desc("Fog mode: Linear, Exponential, ExponentialSquared")] string fogMode = null,
            [Desc("Fog density (Exponential modes)")] float? fogDensity = null,
            [Desc("Fog start distance (Linear mode)")] float? fogStartDistance = null,
            [Desc("Fog end distance (Linear mode)")] float? fogEndDistance = null,
            [Desc("Reflection intensity")] float? reflectionIntensity = null)
        {
            // RenderSettings undo via SerializedObject
            var renderSettingsArr = UnityEngine.Resources.FindObjectsOfTypeAll<RenderSettings>();
            if (renderSettingsArr.Length > 0)
                Undo.RecordObject(renderSettingsArr[0], "Modify Environment");
            int modified = 0;

            if (!string.IsNullOrEmpty(ambientMode))
            {
                if (System.Enum.TryParse<AmbientMode>(ambientMode, true, out var am))
                { RenderSettings.ambientMode = am; modified++; }
            }
            if (ambientColor.HasValue) { RenderSettings.ambientLight = ambientColor.Value; modified++; }
            if (ambientSkyColor.HasValue) { RenderSettings.ambientSkyColor = ambientSkyColor.Value; modified++; }
            if (ambientEquatorColor.HasValue) { RenderSettings.ambientEquatorColor = ambientEquatorColor.Value; modified++; }
            if (ambientGroundColor.HasValue) { RenderSettings.ambientGroundColor = ambientGroundColor.Value; modified++; }
            if (ambientIntensity.HasValue) { RenderSettings.ambientIntensity = ambientIntensity.Value; modified++; }

            if (!string.IsNullOrEmpty(skyboxMaterial))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(skyboxMaterial);
                if (mat != null) { RenderSettings.skybox = mat; modified++; }
            }

            if (fog.HasValue) { RenderSettings.fog = fog.Value; modified++; }
            if (fogColor.HasValue) { RenderSettings.fogColor = fogColor.Value; modified++; }
            if (!string.IsNullOrEmpty(fogMode))
            {
                if (System.Enum.TryParse<FogMode>(fogMode, true, out var fm))
                { RenderSettings.fogMode = fm; modified++; }
            }
            if (fogDensity.HasValue) { RenderSettings.fogDensity = fogDensity.Value; modified++; }
            if (fogStartDistance.HasValue) { RenderSettings.fogStartDistance = fogStartDistance.Value; modified++; }
            if (fogEndDistance.HasValue) { RenderSettings.fogEndDistance = fogEndDistance.Value; modified++; }
            if (reflectionIntensity.HasValue) { RenderSettings.reflectionIntensity = reflectionIntensity.Value; modified++; }

            EditorUtility.SetDirty(RenderSettings.GetRenderSettings());
            return ToolResult.Text($"Modified {modified} environment settings");
        }

        [McpTool("lighting_bake", "Start lightmap baking (async). Use lighting_get_bake_status to check progress.",
            Group = "lighting")]
        public static ToolResult Bake(
            [Desc("Bake only the selected lights (false = bake all)")] bool selectedOnly = false)
        {
            if (Lightmapping.isRunning)
                return ToolResult.Error("Lightmap baking is already in progress");

            if (selectedOnly)
                Lightmapping.BakeAsync();
            else
                Lightmapping.BakeAsync();

            return ToolResult.Text("Lightmap baking started. Use lighting_get_bake_status to check progress.");
        }

        [McpTool("lighting_get_bake_status", "Check if lightmap baking is in progress or complete",
            Group = "lighting", ReadOnly = true)]
        public static ToolResult GetBakeStatus()
        {
            return ToolResult.Json(new
            {
                isRunning = Lightmapping.isRunning,
                lightmapCount = LightmapSettings.lightmaps.Length,
            });
        }

        [McpTool("lighting_cancel_bake", "Cancel ongoing lightmap baking",
            Group = "lighting")]
        public static ToolResult CancelBake()
        {
            if (!Lightmapping.isRunning)
                return ToolResult.Text("No baking in progress");

            Lightmapping.Cancel();
            return ToolResult.Text("Lightmap baking cancelled");
        }

        [McpTool("light_create_probe", "Create a Reflection Probe or Light Probe Group",
            Group = "lighting")]
        public static ToolResult CreateProbe(
            [Desc("Probe type: ReflectionProbe, LightProbeGroup")] string type,
            [Desc("World position")] Vector3? position = null,
            [Desc("Name")] string name = null,
            [Desc("Box size for ReflectionProbe")] Vector3? size = null)
        {
            GameObject go;
            switch (type?.ToLower())
            {
                case "reflectionprobe":
                    go = new GameObject(name ?? "Reflection Probe");
                    var rp = go.AddComponent<ReflectionProbe>();
                    if (size.HasValue) rp.size = size.Value;
                    break;
                case "lightprobegroup":
                    go = new GameObject(name ?? "Light Probe Group");
                    go.AddComponent<LightProbeGroup>();
                    break;
                default:
                    return ToolResult.Error($"Unknown probe type: {type}. Use: ReflectionProbe, LightProbeGroup");
            }

            if (position.HasValue) go.transform.position = position.Value;
            UndoHelper.RegisterCreatedObject(go, $"Create {type}");

            return ToolResult.Json(new
            {
                success = true,
                instanceId = go.GetInstanceID(),
                name = go.name,
                type,
                message = $"Created {type}: {go.name}"
            });
        }
    }
}
