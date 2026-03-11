using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("ProjectSettings")]
    public static class ProjectSettingsTools
    {
        [McpTool("settings_get_tags", "Get all tags defined in the project",
            Group = "settings", ReadOnly = true)]
        public static ToolResult GetTags()
        {
            return ToolResult.Json(new { tags = UnityEditorInternal.InternalEditorUtility.tags });
        }

        [McpTool("settings_add_tag", "Add a custom tag to the project",
            Group = "settings")]
        public static ToolResult AddTag(
            [Desc("Tag name to add")] string tag)
        {
            var tags = UnityEditorInternal.InternalEditorUtility.tags;
            if (tags.Contains(tag))
                return ToolResult.Text($"Tag '{tag}' already exists");

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProp = tagManager.FindProperty("tags");

            // Find empty slot or add new
            int emptySlot = -1;
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (string.IsNullOrEmpty(tagsProp.GetArrayElementAtIndex(i).stringValue))
                { emptySlot = i; break; }
            }

            if (emptySlot >= 0)
            {
                tagsProp.GetArrayElementAtIndex(emptySlot).stringValue = tag;
            }
            else
            {
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            }

            tagManager.ApplyModifiedProperties();
            return ToolResult.Text($"Added tag: '{tag}'");
        }

        [McpTool("settings_get_layers", "Get all layers defined in the project",
            Group = "settings", ReadOnly = true)]
        public static ToolResult GetLayers()
        {
            var layers = new List<object>();
            for (int i = 0; i < 32; i++)
            {
                string name = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(name))
                    layers.Add(new { index = i, name });
            }
            return ToolResult.Json(new { layers });
        }

        [McpTool("settings_add_layer", "Add a custom layer to the project (uses first available user layer slot 6-31)",
            Group = "settings")]
        public static ToolResult AddLayer(
            [Desc("Layer name to add")] string layer,
            [Desc("Specific layer index (6-31). If not set, uses first empty slot.")] int? index = null)
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProp = tagManager.FindProperty("layers");

            // Check if already exists
            for (int i = 0; i < layersProp.arraySize; i++)
            {
                if (layersProp.GetArrayElementAtIndex(i).stringValue == layer)
                    return ToolResult.Text($"Layer '{layer}' already exists at index {i}");
            }

            if (index.HasValue)
            {
                if (index.Value < 6 || index.Value > 31)
                    return ToolResult.Error("Custom layers must be between index 6 and 31");
                if (!string.IsNullOrEmpty(layersProp.GetArrayElementAtIndex(index.Value).stringValue))
                    return ToolResult.Error($"Layer index {index.Value} is already occupied by '{layersProp.GetArrayElementAtIndex(index.Value).stringValue}'");

                layersProp.GetArrayElementAtIndex(index.Value).stringValue = layer;
            }
            else
            {
                bool found = false;
                for (int i = 6; i < 32 && i < layersProp.arraySize; i++)
                {
                    if (string.IsNullOrEmpty(layersProp.GetArrayElementAtIndex(i).stringValue))
                    {
                        layersProp.GetArrayElementAtIndex(i).stringValue = layer;
                        index = i;
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return ToolResult.Error("No empty layer slots available (all 6-31 are used)");
            }

            tagManager.ApplyModifiedProperties();
            return ToolResult.Text($"Added layer '{layer}' at index {index.Value}");
        }

        [McpTool("settings_get_sorting_layers", "Get all sorting layers",
            Group = "settings", ReadOnly = true)]
        public static ToolResult GetSortingLayers()
        {
            var layers = SortingLayer.layers.Select(l => new
            {
                id = l.id,
                name = l.name,
                value = l.value,
            }).ToArray();

            return ToolResult.Json(new { layers });
        }

        [McpTool("settings_add_sorting_layer", "Add a sorting layer",
            Group = "settings")]
        public static ToolResult AddSortingLayer(
            [Desc("Sorting layer name")] string name)
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var sortingLayers = tagManager.FindProperty("m_SortingLayers");

            // Check if exists
            for (int i = 0; i < sortingLayers.arraySize; i++)
            {
                if (sortingLayers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name)
                    return ToolResult.Text($"Sorting layer '{name}' already exists");
            }

            sortingLayers.InsertArrayElementAtIndex(sortingLayers.arraySize);
            var newLayer = sortingLayers.GetArrayElementAtIndex(sortingLayers.arraySize - 1);
            newLayer.FindPropertyRelative("name").stringValue = name;
            newLayer.FindPropertyRelative("uniqueID").intValue = (int)System.DateTime.Now.Ticks;

            tagManager.ApplyModifiedProperties();
            return ToolResult.Text($"Added sorting layer: '{name}'");
        }

        [McpTool("settings_get_quality", "Get quality settings overview",
            Group = "settings", ReadOnly = true)]
        public static ToolResult GetQuality()
        {
            var names = QualitySettings.names;
            return ToolResult.Json(new
            {
                currentLevel = QualitySettings.GetQualityLevel(),
                currentName = names[QualitySettings.GetQualityLevel()],
                levels = names,
                vSyncCount = QualitySettings.vSyncCount,
                antiAliasing = QualitySettings.antiAliasing,
                shadowDistance = QualitySettings.shadowDistance,
                shadowResolution = QualitySettings.shadowResolution.ToString(),
                textureQuality = QualitySettings.masterTextureLimit,
                anisotropicFiltering = QualitySettings.anisotropicFiltering.ToString(),
            });
        }

        [McpTool("settings_set_quality", "Modify quality settings",
            Group = "settings")]
        public static ToolResult SetQuality(
            [Desc("Quality level index")] int? level = null,
            [Desc("VSync count (0=off, 1=every vblank, 2=every second)")] int? vSyncCount = null,
            [Desc("Anti-aliasing (0, 2, 4, 8)")] int? antiAliasing = null,
            [Desc("Shadow distance")] float? shadowDistance = null,
            [Desc("Shadow resolution: Low, Medium, High, VeryHigh")] string shadowResolution = null)
        {
            var changes = new List<string>();

            if (level.HasValue) { QualitySettings.SetQualityLevel(level.Value, true); changes.Add($"level={level.Value}"); }
            if (vSyncCount.HasValue) { QualitySettings.vSyncCount = vSyncCount.Value; changes.Add($"vSyncCount={vSyncCount.Value}"); }
            if (antiAliasing.HasValue) { QualitySettings.antiAliasing = antiAliasing.Value; changes.Add($"antiAliasing={antiAliasing.Value}"); }
            if (shadowDistance.HasValue) { QualitySettings.shadowDistance = shadowDistance.Value; changes.Add($"shadowDistance={shadowDistance.Value}"); }
            if (!string.IsNullOrEmpty(shadowResolution))
            {
                if (System.Enum.TryParse<ShadowResolution>(shadowResolution, true, out var sr))
                { QualitySettings.shadowResolution = sr; changes.Add($"shadowResolution={sr}"); }
            }

            if (changes.Count == 0) return ToolResult.Text("No quality settings changed");
            return ToolResult.Text($"Quality settings updated: {string.Join(", ", changes)}");
        }

        [McpTool("settings_get_time", "Get Time settings",
            Group = "settings", ReadOnly = true)]
        public static ToolResult GetTime()
        {
            return ToolResult.Json(new
            {
                fixedDeltaTime = Time.fixedDeltaTime,
                maximumDeltaTime = Time.maximumDeltaTime,
                timeScale = Time.timeScale,
                maximumParticleDeltaTime = Time.maximumParticleDeltaTime,
            });
        }

        [McpTool("settings_set_time", "Modify Time settings",
            Group = "settings")]
        public static ToolResult SetTime(
            [Desc("Fixed timestep")] float? fixedDeltaTime = null,
            [Desc("Maximum allowed timestep")] float? maximumDeltaTime = null,
            [Desc("Time scale (0=paused, 1=normal)")] float? timeScale = null)
        {
            var changes = new List<string>();
            if (fixedDeltaTime.HasValue) { Time.fixedDeltaTime = fixedDeltaTime.Value; changes.Add($"fixedDeltaTime={fixedDeltaTime.Value}"); }
            if (maximumDeltaTime.HasValue) { Time.maximumDeltaTime = maximumDeltaTime.Value; changes.Add($"maximumDeltaTime={maximumDeltaTime.Value}"); }
            if (timeScale.HasValue) { Time.timeScale = timeScale.Value; changes.Add($"timeScale={timeScale.Value}"); }

            if (changes.Count == 0) return ToolResult.Text("No time settings changed");
            return ToolResult.Text($"Time settings updated: {string.Join(", ", changes)}");
        }
    }
}
