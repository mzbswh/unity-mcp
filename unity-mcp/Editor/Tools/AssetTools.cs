using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
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

        [McpTool("asset_set_import_settings", "Set import settings for an asset. Primarily for textures: set textureType to 'Sprite' for UI images, configure sprite settings, compression, etc.",
            Group = "asset")]
        public static ToolResult SetImportSettings(
            [Desc("Asset path (e.g. Assets/Textures/icon.png)")] string path,
            [Desc("Import settings as JSON. For textures: textureType (Default/Sprite/NormalMap/GUI), spriteImportMode (Single/Multiple), spritePixelsPerUnit, maxTextureSize, textureCompression (Uncompressed/Compressed/CompressedHQ/CompressedLQ), filterMode (Point/Bilinear/Trilinear), wrapMode (Repeat/Clamp), sRGBTexture, alphaIsTransparency, isReadable, mipmapEnabled")] Newtonsoft.Json.Linq.JObject settings)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var importer = AssetImporter.GetAtPath(path);
            if (importer == null)
                return ToolResult.Error($"No importer found for: {path}");

            if (importer is TextureImporter texImporter)
                return ApplyTextureImportSettings(texImporter, settings, path);

            return ToolResult.Error($"Unsupported importer type: {importer.GetType().Name}. Currently only TextureImporter is supported.");
        }

        [McpTool("asset_set_model_import", "Set import settings for a model asset (FBX, OBJ, etc.)",
            Group = "asset")]
        public static ToolResult SetModelImport(
            [Desc("Model asset path (e.g. Assets/Models/Character.fbx)")] string path,
            [Desc("Global scale factor")] float? scaleFactor = null,
            [Desc("Use file scale")] bool? useFileScale = null,
            [Desc("Import BlendShapes")] bool? importBlendShapes = null,
            [Desc("Import visibility")] bool? importVisibility = null,
            [Desc("Import cameras")] bool? importCameras = null,
            [Desc("Import lights")] bool? importLights = null,
            [Desc("Generate colliders (mesh collider)")] bool? generateColliders = null,
            [Desc("Mesh compression: Off, Low, Medium, High")] string meshCompression = null,
            [Desc("Read/Write enabled")] bool? isReadable = null,
            [Desc("Optimize mesh")] bool? optimizeMesh = null,
            [Desc("Import normals: Import, Calculate, None")] string importNormals = null,
            [Desc("Import animation")] bool? importAnimation = null,
            [Desc("Animation type: None, Legacy, Generic, Human")] string animationType = null,
            [Desc("Import materials")] bool? importMaterials = null,
            [Desc("Material import mode: None, ImportViaMaterialDescription, ImportStandard")] string materialImportMode = null)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                return ToolResult.Error($"Not a model asset or not found: {path}");

            int applied = 0;

            if (scaleFactor.HasValue) { importer.globalScale = scaleFactor.Value; applied++; }
            if (useFileScale.HasValue) { importer.useFileScale = useFileScale.Value; applied++; }
            if (importBlendShapes.HasValue) { importer.importBlendShapes = importBlendShapes.Value; applied++; }
            if (importVisibility.HasValue) { importer.importVisibility = importVisibility.Value; applied++; }
            if (importCameras.HasValue) { importer.importCameras = importCameras.Value; applied++; }
            if (importLights.HasValue) { importer.importLights = importLights.Value; applied++; }
            if (generateColliders.HasValue) { importer.addCollider = generateColliders.Value; applied++; }
            if (isReadable.HasValue) { importer.isReadable = isReadable.Value; applied++; }
            if (optimizeMesh.HasValue) { importer.optimizeMeshVertices = optimizeMesh.Value; importer.optimizeMeshPolygons = optimizeMesh.Value; applied++; }
            if (importAnimation.HasValue) { importer.importAnimation = importAnimation.Value; applied++; }
            if (importMaterials.HasValue) { importer.importMaterials = importMaterials.Value; applied++; }

            if (!string.IsNullOrEmpty(meshCompression))
            {
                if (System.Enum.TryParse<ModelImporterMeshCompression>(meshCompression, true, out var mc))
                { importer.meshCompression = mc; applied++; }
            }
            if (!string.IsNullOrEmpty(importNormals))
            {
                if (System.Enum.TryParse<ModelImporterNormals>(importNormals, true, out var n))
                { importer.importNormals = n; applied++; }
            }
            if (!string.IsNullOrEmpty(animationType))
            {
                if (System.Enum.TryParse<ModelImporterAnimationType>(animationType, true, out var at))
                { importer.animationType = at; applied++; }
            }
            if (!string.IsNullOrEmpty(materialImportMode))
            {
                if (System.Enum.TryParse<ModelImporterMaterialImportMode>(materialImportMode, true, out var mm))
                { importer.materialImportMode = mm; applied++; }
            }

            importer.SaveAndReimport();
            return ToolResult.Text($"Applied {applied} model import settings to '{path}'");
        }

        [McpTool("asset_find_references", "Find all assets that reference/depend on a given asset (reverse dependency lookup)",
            Group = "asset", ReadOnly = true)]
        public static ToolResult FindReferences(
            [Desc("Asset path to find references for (e.g. Assets/Textures/icon.png)")] string path,
            [Desc("Folders to search in (null = entire Assets folder)")] string[] searchInFolders = null,
            [Desc("Max results")] int maxCount = 50)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var targetGuid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(targetGuid))
                return ToolResult.Error($"Asset not found: {path}");

            var folders = searchInFolders ?? new[] { "Assets" };
            var allAssets = AssetDatabase.FindAssets("", folders);
            var referencingAssets = new System.Collections.Generic.List<object>();

            foreach (var guid in allAssets)
            {
                if (guid == targetGuid) continue;
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var deps = AssetDatabase.GetDependencies(assetPath, false);
                if (deps.Any(d => d == path))
                {
                    referencingAssets.Add(new
                    {
                        path = assetPath,
                        name = Path.GetFileNameWithoutExtension(assetPath),
                        type = AssetDatabase.GetMainAssetTypeAtPath(assetPath)?.Name
                    });
                    if (referencingAssets.Count >= maxCount) break;
                }
            }

            return ToolResult.Json(new
            {
                targetAsset = path,
                referencingCount = referencingAssets.Count,
                referencingAssets
            });
        }

        private static ToolResult ApplyTextureImportSettings(TextureImporter importer, Newtonsoft.Json.Linq.JObject settings, string path)
        {
            int applied = 0;

            if (settings["textureType"] != null)
            {
                if (System.Enum.TryParse<TextureImporterType>(settings["textureType"].ToString(), true, out var tt))
                { importer.textureType = tt; applied++; }
            }
            if (settings["spriteImportMode"] != null)
            {
                if (System.Enum.TryParse<SpriteImportMode>(settings["spriteImportMode"].ToString(), true, out var sm))
                { importer.spriteImportMode = sm; applied++; }
            }
            if (settings["spritePixelsPerUnit"] != null)
            { importer.spritePixelsPerUnit = settings["spritePixelsPerUnit"].Value<float>(); applied++; }
            if (settings["maxTextureSize"] != null)
            { importer.maxTextureSize = settings["maxTextureSize"].Value<int>(); applied++; }
            if (settings["textureCompression"] != null)
            {
                if (System.Enum.TryParse<TextureImporterCompression>(settings["textureCompression"].ToString(), true, out var tc))
                { importer.textureCompression = tc; applied++; }
            }
            if (settings["filterMode"] != null)
            {
                if (System.Enum.TryParse<FilterMode>(settings["filterMode"].ToString(), true, out var fm))
                { importer.filterMode = fm; applied++; }
            }
            if (settings["wrapMode"] != null)
            {
                if (System.Enum.TryParse<TextureWrapMode>(settings["wrapMode"].ToString(), true, out var wm))
                { importer.wrapMode = wm; applied++; }
            }
            if (settings["sRGBTexture"] != null)
            { importer.sRGBTexture = settings["sRGBTexture"].Value<bool>(); applied++; }
            if (settings["alphaIsTransparency"] != null)
            { importer.alphaIsTransparency = settings["alphaIsTransparency"].Value<bool>(); applied++; }
            if (settings["isReadable"] != null)
            { importer.isReadable = settings["isReadable"].Value<bool>(); applied++; }
            if (settings["mipmapEnabled"] != null)
            { importer.mipmapEnabled = settings["mipmapEnabled"].Value<bool>(); applied++; }
            if (settings["alphaSource"] != null)
            {
                if (System.Enum.TryParse<TextureImporterAlphaSource>(settings["alphaSource"].ToString(), true, out var als))
                { importer.alphaSource = als; applied++; }
            }
            if (settings["anisoLevel"] != null)
            { importer.anisoLevel = settings["anisoLevel"].Value<int>(); applied++; }

            importer.SaveAndReimport();
            return ToolResult.Text($"Applied {applied} import settings to '{path}' (importer: TextureImporter)");
        }
    }
}
