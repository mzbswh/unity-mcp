using UnityEditor;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Lighting")]
    public static class LightingTools
    {
        [McpTool("lighting_bake", "Start lightmap baking (async). Use graphics_get_lightmap_settings to check progress.",
            Group = "lighting")]
        public static ToolResult Bake(
            [Desc("Clear baked data first")] bool clearFirst = false)
        {
            if (Lightmapping.isRunning)
                return ToolResult.Error("Lightmap baking is already in progress");

            if (clearFirst)
                Lightmapping.Clear();

            Lightmapping.BakeAsync();

            return ToolResult.Text("Lightmap baking started. Use graphics_get_lightmap_settings to check progress.");
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
    }
}
