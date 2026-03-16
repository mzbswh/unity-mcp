using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Texture")]
    public static class TextureTools
    {
        [McpTool("texture_get_info", "Get texture information (dimensions, format, compression, mipmap)",
            Group = "texture", ReadOnly = true)]
        public static ToolResult GetInfo(
            [Desc("Asset path (e.g. Assets/Textures/player.png)")] string path)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return ToolResult.Error($"Not a texture asset: {path}");

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            return ToolResult.Json(new
            {
                path,
                name = Path.GetFileNameWithoutExtension(path),
                width = tex?.width,
                height = tex?.height,
                textureType = importer.textureType.ToString(),
                textureShape = importer.textureShape.ToString(),
                sRGB = importer.sRGBTexture,
                alphaSource = importer.alphaSource.ToString(),
                alphaIsTransparency = importer.alphaIsTransparency,
                mipmapEnabled = importer.mipmapEnabled,
                filterMode = importer.filterMode.ToString(),
                wrapMode = importer.wrapMode.ToString(),
                maxTextureSize = importer.maxTextureSize,
                textureCompression = importer.textureCompression.ToString(),
                readWriteEnabled = importer.isReadable,
                spriteMode = importer.spriteImportMode.ToString(),
            });
        }

        [McpTool("texture_search", "Search for texture assets by criteria (paginated)",
            Group = "texture", ReadOnly = true)]
        public static ToolResult Search(
            [Desc("Search filter name (optional)")] string nameFilter = null,
            [Desc("Folder to search (e.g. Assets/Textures)")] string folder = null,
            [Desc("Min width filter")] int? minWidth = null,
            [Desc("Page size (default 50, max 200)")] int pageSize = 50,
            [Desc("Pagination cursor")] string cursor = null)
        {
            string filter = "t:Texture2D";
            if (!string.IsNullOrEmpty(nameFilter))
                filter = nameFilter + " " + filter;

            var folders = string.IsNullOrEmpty(folder) ? null : new[] { folder };
            var guids = folders != null
                ? AssetDatabase.FindAssets(filter, folders)
                : AssetDatabase.FindAssets(filter);

            var results = guids.Select(guid =>
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                return new
                {
                    path = p,
                    name = Path.GetFileNameWithoutExtension(p),
                    width = tex?.width ?? 0,
                    height = tex?.height ?? 0,
                };
            }).AsEnumerable();

            if (minWidth.HasValue)
                results = results.Where(r => r.width >= minWidth.Value);

            var allResults = results.ToArray();
            return PaginationHelper.ToPaginatedResult(allResults, pageSize, cursor);
        }
    }
}
