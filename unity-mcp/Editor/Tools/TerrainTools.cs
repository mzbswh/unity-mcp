using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Editor.Utils;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Terrain")]
    public static class TerrainTools
    {
        [McpTool("terrain_create", "Create a new Terrain GameObject with TerrainData",
            Group = "terrain")]
        public static ToolResult Create(
            [Desc("Terrain name")] string name = "Terrain",
            [Desc("World position")] Vector3? position = null,
            [Desc("Terrain size (width, height, length)")] Vector3? size = null,
            [Desc("Heightmap resolution (power of 2 + 1, e.g. 513, 1025)")] int heightmapResolution = 513,
            [Desc("Save TerrainData to this path (e.g. Assets/Terrain/MyTerrain.asset)")] string dataPath = null)
        {
            var terrainData = new TerrainData();
            terrainData.heightmapResolution = heightmapResolution;

            if (size.HasValue)
                terrainData.size = size.Value;
            else
                terrainData.size = new Vector3(500, 150, 500);

            if (!string.IsNullOrEmpty(dataPath))
            {
                var pv = PathValidator.QuickValidate(dataPath);
                if (!pv.IsValid) return ToolResult.Error(pv.Error);

                var dir = Path.GetDirectoryName(dataPath);
                if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
                {
                    Directory.CreateDirectory(dir);
                    AssetDatabase.Refresh();
                }

                AssetDatabase.CreateAsset(terrainData, dataPath);
            }

            var go = Terrain.CreateTerrainGameObject(terrainData);
            go.name = name;
            if (position.HasValue) go.transform.position = position.Value;

            UndoHelper.RegisterCreatedObject(go, "Create Terrain");

            if (!string.IsNullOrEmpty(dataPath))
                AssetDatabase.SaveAssets();

            return ToolResult.Json(new
            {
                success = true,
                instanceId = go.GetInstanceID(),
                name = go.name,
                size = new { x = terrainData.size.x, y = terrainData.size.y, z = terrainData.size.z },
                heightmapResolution,
                dataPath,
                message = $"Created terrain: {go.name}"
            });
        }

        [McpTool("terrain_get_info", "Get info about a Terrain in the scene",
            Group = "terrain", ReadOnly = true)]
        public static ToolResult GetInfo(
            [Desc("Name of the Terrain GameObject")] string target = null)
        {
            Terrain terrain;
            if (!string.IsNullOrEmpty(target))
            {
                var go = GameObjectTools.FindGameObject(target, null);
                if (go == null)
                    return ToolResult.Error($"GameObject not found: {target}");
                terrain = go.GetComponent<Terrain>();
            }
            else
            {
                terrain = Terrain.activeTerrain;
            }

            if (terrain == null)
                return ToolResult.Error("No Terrain found");

            var td = terrain.terrainData;
            var layers = td.terrainLayers?.Select(l => new
            {
                name = l != null ? l.name : null,
                diffuseTexture = l?.diffuseTexture != null ? AssetDatabase.GetAssetPath(l.diffuseTexture) : null,
                tileSize = l != null ? new { x = l.tileSize.x, y = l.tileSize.y } : null,
            }).ToArray();

            return ToolResult.Json(new
            {
                name = terrain.name,
                position = new { x = terrain.transform.position.x, y = terrain.transform.position.y, z = terrain.transform.position.z },
                size = new { x = td.size.x, y = td.size.y, z = td.size.z },
                heightmapResolution = td.heightmapResolution,
                alphamapResolution = td.alphamapResolution,
                detailResolution = td.detailResolution,
                treeInstanceCount = td.treeInstanceCount,
                terrainLayerCount = td.terrainLayers?.Length ?? 0,
                terrainLayers = layers,
            });
        }

        [McpTool("terrain_set_height", "Set terrain height at a specific area (normalized coordinates 0-1)",
            Group = "terrain")]
        public static ToolResult SetHeight(
            [Desc("Name of the Terrain GameObject (null = active terrain)")] string target = null,
            [Desc("Normalized X position (0-1)")] float x = 0.5f,
            [Desc("Normalized Z position (0-1)")] float z = 0.5f,
            [Desc("Height value (0-1)")] float height = 0.5f,
            [Desc("Brush radius in heightmap pixels")] int radius = 10,
            [Desc("Brush strength (0-1)")] float strength = 1f)
        {
            var terrain = GetTerrain(target);
            if (terrain == null)
                return ToolResult.Error("Terrain not found");

            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            int cx = Mathf.Clamp(Mathf.RoundToInt(x * (res - 1)), 0, res - 1);
            int cz = Mathf.Clamp(Mathf.RoundToInt(z * (res - 1)), 0, res - 1);

            int startX = Mathf.Max(0, cx - radius);
            int startZ = Mathf.Max(0, cz - radius);
            int endX = Mathf.Min(res - 1, cx + radius);
            int endZ = Mathf.Min(res - 1, cz + radius);
            int w = endX - startX + 1;
            int h = endZ - startZ + 1;

            Undo.RegisterCompleteObjectUndo(td, "Set Terrain Height");

            var heights = td.GetHeights(startX, startZ, w, h);
            for (int iz = 0; iz < h; iz++)
            {
                for (int ix = 0; ix < w; ix++)
                {
                    float dx = (startX + ix) - cx;
                    float dz = (startZ + iz) - cz;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    if (dist <= radius)
                    {
                        float falloff = 1f - (dist / radius);
                        heights[iz, ix] = Mathf.Lerp(heights[iz, ix], height, strength * falloff);
                    }
                }
            }

            td.SetHeights(startX, startZ, heights);
            return ToolResult.Text($"Set height at ({x:F2}, {z:F2}) with radius {radius}");
        }

        [McpTool("terrain_flatten", "Flatten the entire terrain to a uniform height",
            Group = "terrain")]
        public static ToolResult Flatten(
            [Desc("Name of the Terrain GameObject (null = active terrain)")] string target = null,
            [Desc("Height value (0-1)")] float height = 0f)
        {
            var terrain = GetTerrain(target);
            if (terrain == null)
                return ToolResult.Error("Terrain not found");

            var td = terrain.terrainData;
            int res = td.heightmapResolution;

            Undo.RegisterCompleteObjectUndo(td, "Flatten Terrain");

            var heights = new float[res, res];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                    heights[z, x] = height;

            td.SetHeights(0, 0, heights);
            return ToolResult.Text($"Flattened terrain to height {height}");
        }

        [McpTool("terrain_add_layer", "Add a terrain paint layer (texture)",
            Group = "terrain")]
        public static ToolResult AddLayer(
            [Desc("Name of the Terrain GameObject (null = active terrain)")] string target = null,
            [Desc("Diffuse texture asset path")] string diffuseTexture = null,
            [Desc("Normal map texture asset path")] string normalMap = null,
            [Desc("Tile size")] Vector2? tileSize = null,
            [Desc("Tile offset")] Vector2? tileOffset = null)
        {
            var terrain = GetTerrain(target);
            if (terrain == null)
                return ToolResult.Error("Terrain not found");

            var layer = new TerrainLayer();
            if (!string.IsNullOrEmpty(diffuseTexture))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(diffuseTexture);
                if (tex != null) layer.diffuseTexture = tex;
                else return ToolResult.Error($"Texture not found: {diffuseTexture}");
            }
            if (!string.IsNullOrEmpty(normalMap))
            {
                var norm = AssetDatabase.LoadAssetAtPath<Texture2D>(normalMap);
                if (norm != null) layer.normalMapTexture = norm;
            }
            if (tileSize.HasValue) layer.tileSize = tileSize.Value;
            if (tileOffset.HasValue) layer.tileOffset = tileOffset.Value;

            var td = terrain.terrainData;
            var existing = td.terrainLayers ?? new TerrainLayer[0];
            var newLayers = new TerrainLayer[existing.Length + 1];
            existing.CopyTo(newLayers, 0);
            newLayers[existing.Length] = layer;
            td.terrainLayers = newLayers;

            return ToolResult.Json(new
            {
                success = true,
                layerIndex = existing.Length,
                message = $"Added terrain layer (index {existing.Length})"
            });
        }

        [McpTool("terrain_add_tree", "Add tree instances to the terrain",
            Group = "terrain")]
        public static ToolResult AddTree(
            [Desc("Name of the Terrain GameObject (null = active terrain)")] string target = null,
            [Desc("Tree prefab asset path")] string prefabPath = null,
            [Desc("Normalized position (x, z from 0-1)")] Vector2? position = null,
            [Desc("Number of random trees to place")] int count = 1,
            [Desc("Height scale")] float heightScale = 1f,
            [Desc("Width scale")] float widthScale = 1f)
        {
            var terrain = GetTerrain(target);
            if (terrain == null)
                return ToolResult.Error("Terrain not found");

            if (string.IsNullOrEmpty(prefabPath))
                return ToolResult.Error("prefabPath is required");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return ToolResult.Error($"Prefab not found: {prefabPath}");

            var td = terrain.terrainData;

            // Ensure tree prototype exists
            int protoIndex = -1;
            var protos = td.treePrototypes;
            for (int i = 0; i < protos.Length; i++)
            {
                if (protos[i].prefab == prefab) { protoIndex = i; break; }
            }
            if (protoIndex < 0)
            {
                var newProtos = new TreePrototype[protos.Length + 1];
                protos.CopyTo(newProtos, 0);
                newProtos[protos.Length] = new TreePrototype { prefab = prefab };
                td.treePrototypes = newProtos;
                protoIndex = protos.Length;
            }

            Undo.RegisterCompleteObjectUndo(td, "Add Trees");

            for (int i = 0; i < count; i++)
            {
                var pos = position.HasValue
                    ? new Vector3(position.Value.x, 0, position.Value.y)
                    : new Vector3(Random.value, 0, Random.value);

                var instance = new TreeInstance
                {
                    prototypeIndex = protoIndex,
                    position = pos,
                    heightScale = heightScale,
                    widthScale = widthScale,
                    color = Color.white,
                    lightmapColor = Color.white,
                };
                terrain.AddTreeInstance(instance);
            }

            terrain.Flush();
            return ToolResult.Text($"Added {count} tree(s) to terrain");
        }

        private static Terrain GetTerrain(string target)
        {
            if (!string.IsNullOrEmpty(target))
            {
                var go = GameObjectTools.FindGameObject(target, null);
                return go?.GetComponent<Terrain>();
            }
            return Terrain.activeTerrain;
        }
    }
}
