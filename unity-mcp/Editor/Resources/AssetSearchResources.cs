using System.IO;
using System.Linq;
using UnityEditor;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Resources
{
    [McpToolGroup("AssetSearchResources")]
    public static class AssetSearchResources
    {
        [McpResource("unity://assets/search/{filter}", "Asset Search",
            "Search for assets using AssetDatabase filter syntax")]
        public static ToolResult SearchAssets(
            [Desc("Search filter (e.g. 't:Texture2D', 'Player t:Prefab')")] string filter)
        {
            var guids = AssetDatabase.FindAssets(filter);
            var results = guids.Take(100).Select(guid =>
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                return new
                {
                    guid,
                    path,
                    name = Path.GetFileNameWithoutExtension(path),
                    type = AssetDatabase.GetMainAssetTypeAtPath(path)?.Name
                };
            }).ToArray();

            return ToolResult.Json(new { filter, totalFound = guids.Length, returned = results.Length, assets = results });
        }
    }
}
