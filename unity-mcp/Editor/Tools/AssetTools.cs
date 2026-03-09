using System.IO;
using System.Linq;
using UnityEditor;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Asset")]
    public static class AssetTools
    {
        [McpTool("asset_find", "Search for assets using AssetDatabase (supports type filters like t:Texture)",
            Group = "asset", ReadOnly = true)]
        public static ToolResult Find(
            [Desc("Search filter (e.g. 'Player t:Prefab', 't:Texture2D', 'l:MyLabel')")] string filter,
            [Desc("Folder paths to search in (e.g. Assets/Prefabs)")] string[] searchInFolders = null,
            [Desc("Max results")] int maxCount = 50)
        {
            if (searchInFolders != null)
            {
                foreach (var folder in searchInFolders)
                {
                    var pv = PathValidator.QuickValidate(folder);
                    if (!pv.IsValid) return ToolResult.Error($"searchInFolders: {pv.Error}");
                }
            }

            var guids = searchInFolders != null && searchInFolders.Length > 0
                ? AssetDatabase.FindAssets(filter, searchInFolders)
                : AssetDatabase.FindAssets(filter);

            var results = guids.Take(maxCount).Select(guid =>
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

            return ToolResult.Json(new { totalFound = guids.Length, returned = results.Length, assets = results });
        }

        [McpTool("asset_create_folder", "Create a folder in the Assets directory",
            Group = "asset")]
        public static ToolResult CreateFolder(
            [Desc("Full folder path (e.g. Assets/Scripts/Player)")] string path)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            if (AssetDatabase.IsValidFolder(path))
                return ToolResult.Text($"Folder already exists: {path}");

            // Create parent folders recursively
            var parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }

            return ToolResult.Text($"Created folder: {path}");
        }

        [McpTool("asset_delete", "Delete an asset at the given path",
            Group = "asset")]
        public static ToolResult Delete(
            [Desc("Asset path to delete (e.g. Assets/Materials/Old.mat)")] string path)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            if (!File.Exists(path) && !Directory.Exists(path))
                return ToolResult.Error($"Asset not found: {path}");

            bool deleted = AssetDatabase.DeleteAsset(path);
            return deleted
                ? ToolResult.Text($"Deleted: {path}")
                : ToolResult.Error($"Failed to delete: {path}");
        }

        [McpTool("asset_move", "Move or rename an asset",
            Group = "asset")]
        public static ToolResult Move(
            [Desc("Current asset path")] string source,
            [Desc("New asset path")] string destination)
        {
            var pvSrc = PathValidator.QuickValidate(source);
            if (!pvSrc.IsValid) return ToolResult.Error($"Source: {pvSrc.Error}");
            var pvDst = PathValidator.QuickValidate(destination);
            if (!pvDst.IsValid) return ToolResult.Error($"Destination: {pvDst.Error}");

            var error = AssetDatabase.MoveAsset(source, destination);
            return string.IsNullOrEmpty(error)
                ? ToolResult.Text($"Moved '{source}' -> '{destination}'")
                : ToolResult.Error($"Move failed: {error}");
        }

        [McpTool("asset_refresh", "Refresh the AssetDatabase to detect external changes",
            Group = "asset")]
        public static ToolResult Refresh()
        {
            AssetDatabase.Refresh();
            return ToolResult.Text("AssetDatabase refreshed");
        }

        [McpTool("asset_copy", "Copy an asset to a new path",
            Group = "asset")]
        public static ToolResult Copy(
            [Desc("Source asset path")] string source,
            [Desc("Destination path")] string destination)
        {
            var pvSrc = PathValidator.QuickValidate(source);
            if (!pvSrc.IsValid) return ToolResult.Error($"Source: {pvSrc.Error}");
            var pvDst = PathValidator.QuickValidate(destination);
            if (!pvDst.IsValid) return ToolResult.Error($"Destination: {pvDst.Error}");

            if (!File.Exists(source) && !Directory.Exists(source))
                return ToolResult.Error($"Source asset not found: {source}");

            var dir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            bool copied = AssetDatabase.CopyAsset(source, destination);
            return copied
                ? ToolResult.Json(new { success = true, source, destination, message = $"Copied '{source}' -> '{destination}'" })
                : ToolResult.Error($"Failed to copy: {source}");
        }

        [McpTool("asset_get_info", "Get detailed metadata about an asset",
            Group = "asset", ReadOnly = true)]
        public static ToolResult GetInfo(
            [Desc("Asset path (e.g. Assets/Materials/Player.mat)")] string path)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj == null)
                return ToolResult.Error($"Asset not found: {path}");

            var guid = AssetDatabase.AssetPathToGUID(path);
            var importer = AssetImporter.GetAtPath(path);
            var dependencies = AssetDatabase.GetDependencies(path, false);
            var labels = AssetDatabase.GetLabels(obj);
            var fileInfo = new FileInfo(path);

            return ToolResult.Json(new
            {
                path,
                guid,
                name = obj.name,
                type = obj.GetType().Name,
                instanceId = obj.GetInstanceID(),
                fileSize = fileInfo.Exists ? fileInfo.Length : 0,
                lastModified = fileInfo.Exists ? fileInfo.LastWriteTimeUtc.ToString("o") : null,
                importerType = importer?.GetType().Name,
                labels,
                directDependencies = dependencies,
                isMainAsset = AssetDatabase.IsMainAsset(obj),
                isNativeAsset = AssetDatabase.IsNativeAsset(obj),
                isForeignAsset = AssetDatabase.IsForeignAsset(obj),
            });
        }
    }
}
