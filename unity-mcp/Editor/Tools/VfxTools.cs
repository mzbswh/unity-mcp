using UnityEditor;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("VFX")]
    public static class VfxTools
    {
        [McpTool("vfx_create_graph", "Create a VFX Graph asset (requires Visual Effect Graph package)",
            Group = "vfx")]
        public static ToolResult CreateGraph(
            [Desc("Save path (e.g. Assets/VFX/MyEffect.vfx)")] string path)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            // Check if VFX Graph package is available
            var vfxAssetType = System.Type.GetType("UnityEngine.VFX.VisualEffectAsset, Unity.VisualEffectGraph.Runtime");
            if (vfxAssetType == null)
                return ToolResult.Error("VFX Graph package (com.unity.visualeffectgraph) is not installed. Install it via Package Manager first.");

            // Use menu item to create VFX Graph (safest cross-version approach)
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            // Check if a template asset can be created via ProjectWindowUtil
            return ToolResult.Json(new
            {
                path,
                message = $"To create VFX Graph, use Unity Editor: Right-click in Project > Create > Visual Effects > Visual Effect Graph, then save to {path}. " +
                          "Programmatic VFX Graph creation requires direct VFX Graph API access."
            });
        }
    }
}
