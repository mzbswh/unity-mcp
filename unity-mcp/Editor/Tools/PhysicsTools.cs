using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Physics")]
    public static class PhysicsTools
    {
        [McpTool("physics_create_material", "Create a PhysicMaterial or PhysicsMaterial2D asset",
            Group = "physics")]
        public static ToolResult CreateMaterial(
            [Desc("Save path (e.g. Assets/Physics/Bouncy.physicMaterial)")] string path,
            [Desc("Is 2D material")] bool is2D = false,
            [Desc("Dynamic friction (3D only)")] float dynamicFriction = 0.6f,
            [Desc("Static friction (3D only)")] float staticFriction = 0.6f,
            [Desc("Bounciness")] float bounciness = 0f,
            [Desc("Friction (2D only)")] float friction = 0.4f)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            if (is2D)
            {
                var mat = new PhysicsMaterial2D { friction = friction, bounciness = bounciness };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                var mat = new PhysicMaterial
                {
                    dynamicFriction = dynamicFriction,
                    staticFriction = staticFriction,
                    bounciness = bounciness
                };
                AssetDatabase.CreateAsset(mat, path);
            }

            AssetDatabase.SaveAssets();
            return ToolResult.Text($"Created {(is2D ? "PhysicsMaterial2D" : "PhysicMaterial")}: {path}");
        }

        [McpTool("physics_raycast", "Perform a physics raycast in the scene (Editor mode)",
            Group = "physics", ReadOnly = true)]
        public static ToolResult Raycast(
            [Desc("Ray origin")] Vector3 origin,
            [Desc("Ray direction")] Vector3 direction,
            [Desc("Max distance")] float maxDistance = 1000f,
            [Desc("Use 2D physics")] bool is2D = false,
            [Desc("Max hits to return")] int maxHits = 10)
        {
            if (is2D)
            {
                var hits = Physics2D.RaycastAll(
                    new Vector2(origin.x, origin.y),
                    new Vector2(direction.x, direction.y),
                    maxDistance);

                var results = hits.Take(maxHits).Select(h => new
                {
                    gameObject = h.collider.gameObject.name,
                    instanceId = h.collider.gameObject.GetInstanceID(),
                    point = new { x = h.point.x, y = h.point.y },
                    distance = h.distance,
                    collider = h.collider.GetType().Name,
                }).ToArray();

                return ToolResult.Json(new { totalHits = hits.Length, returned = results.Length, hits = results });
            }
            else
            {
                var hits = Physics.RaycastAll(origin, direction.normalized, maxDistance);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                var results = hits.Take(maxHits).Select(h => new
                {
                    gameObject = h.collider.gameObject.name,
                    instanceId = h.collider.gameObject.GetInstanceID(),
                    point = new { x = h.point.x, y = h.point.y, z = h.point.z },
                    normal = new { x = h.normal.x, y = h.normal.y, z = h.normal.z },
                    distance = h.distance,
                    collider = h.collider.GetType().Name,
                }).ToArray();

                return ToolResult.Json(new { totalHits = hits.Length, returned = results.Length, hits = results });
            }
        }

        [McpTool("physics_get_settings", "Get Physics/Physics2D project settings",
            Group = "physics", ReadOnly = true)]
        public static ToolResult GetSettings(
            [Desc("Get 2D physics settings")] bool is2D = false)
        {
            if (is2D)
            {
                return ToolResult.Json(new
                {
                    gravity = new { x = Physics2D.gravity.x, y = Physics2D.gravity.y },
                    defaultContactOffset = Physics2D.defaultContactOffset,
                    velocityIterations = Physics2D.velocityIterations,
                    positionIterations = Physics2D.positionIterations,
                });
            }

            return ToolResult.Json(new
            {
                gravity = new { x = Physics.gravity.x, y = Physics.gravity.y, z = Physics.gravity.z },
                defaultContactOffset = Physics.defaultContactOffset,
                defaultSolverIterations = Physics.defaultSolverIterations,
                bounceThreshold = Physics.bounceThreshold,
                sleepThreshold = Physics.sleepThreshold,
            });
        }

        [McpTool("physics_set_gravity", "Set the global gravity vector",
            Group = "physics")]
        public static ToolResult SetGravity(
            [Desc("Gravity vector")] Vector3 gravity,
            [Desc("Set for 2D physics")] bool is2D = false)
        {
            if (is2D)
            {
                var physics2DManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/Physics2DSettings.asset")
                    .FirstOrDefault();
                if (physics2DManager != null)
                {
                    var so = new SerializedObject(physics2DManager);
                    var prop = so.FindProperty("m_Gravity");
                    if (prop != null)
                    {
                        Undo.RecordObject(physics2DManager, "Set 2D Gravity");
                        prop.vector2Value = new Vector2(gravity.x, gravity.y);
                        so.ApplyModifiedProperties();
                        return ToolResult.Text($"Set 2D gravity to ({gravity.x}, {gravity.y})");
                    }
                }
                // Fallback
                Physics2D.gravity = new Vector2(gravity.x, gravity.y);
                return ToolResult.Text($"Set 2D gravity to ({gravity.x}, {gravity.y})");
            }

            var physicsManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")
                .FirstOrDefault();
            if (physicsManager != null)
            {
                var so = new SerializedObject(physicsManager);
                var prop = so.FindProperty("m_Gravity");
                if (prop != null)
                {
                    Undo.RecordObject(physicsManager, "Set Gravity");
                    prop.vector3Value = gravity;
                    so.ApplyModifiedProperties();
                    return ToolResult.Text($"Set gravity to ({gravity.x}, {gravity.y}, {gravity.z})");
                }
            }
            // Fallback
            Physics.gravity = gravity;
            return ToolResult.Text($"Set gravity to ({gravity.x}, {gravity.y}, {gravity.z})");
        }
    }
}
