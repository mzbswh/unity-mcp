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
        [McpTool("physics_add_rigidbody", "Add a Rigidbody (3D) or Rigidbody2D to a GameObject",
            Group = "physics")]
        public static ToolResult AddRigidbody(
            [Desc("Name or path of the target GameObject")] string target,
            [Desc("Use 2D physics (Rigidbody2D)")] bool is2D = false,
            [Desc("Mass")] float mass = 1f,
            [Desc("Use gravity")] bool useGravity = true,
            [Desc("Is kinematic")] bool isKinematic = false,
            [Desc("Drag")] float drag = 0f,
            [Desc("Angular drag")] float angularDrag = 0.05f)
        {
            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target}");

            if (is2D)
            {
                if (go.GetComponent<Rigidbody2D>() != null)
                    return ToolResult.Error($"Rigidbody2D already exists on '{target}'");

                var rb = Undo.AddComponent<Rigidbody2D>(go);
                rb.mass = mass;
                rb.gravityScale = useGravity ? 1f : 0f;
                rb.bodyType = isKinematic ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
                rb.drag = drag;
                rb.angularDrag = angularDrag;
            }
            else
            {
                if (go.GetComponent<Rigidbody>() != null)
                    return ToolResult.Error($"Rigidbody already exists on '{target}'");

                var rb = Undo.AddComponent<Rigidbody>(go);
                rb.mass = mass;
                rb.useGravity = useGravity;
                rb.isKinematic = isKinematic;
                rb.drag = drag;
                rb.angularDrag = angularDrag;
            }

            return ToolResult.Text($"Added {(is2D ? "Rigidbody2D" : "Rigidbody")} to '{go.name}'");
        }

        [McpTool("physics_add_collider", "Add a collider to a GameObject",
            Group = "physics")]
        public static ToolResult AddCollider(
            [Desc("Name or path of the target GameObject")] string target,
            [Desc("Collider type: Box, Sphere, Capsule, Mesh, Box2D, Circle2D, Polygon2D, Edge2D")] string type,
            [Desc("Is trigger")] bool isTrigger = false,
            [Desc("Center offset")] Vector3? center = null,
            [Desc("Size for BoxCollider")] Vector3? size = null,
            [Desc("Radius for Sphere/Circle")] float? radius = null,
            [Desc("Height for CapsuleCollider")] float? height = null,
            [Desc("PhysicMaterial asset path")] string material = null)
        {
            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target}");

            Collider col3D = null;
            Collider2D col2D = null;

            switch (type?.ToLower())
            {
                case "box":
                    var box = Undo.AddComponent<BoxCollider>(go);
                    if (center.HasValue) box.center = center.Value;
                    if (size.HasValue) box.size = size.Value;
                    col3D = box;
                    break;
                case "sphere":
                    var sphere = Undo.AddComponent<SphereCollider>(go);
                    if (center.HasValue) sphere.center = center.Value;
                    if (radius.HasValue) sphere.radius = radius.Value;
                    col3D = sphere;
                    break;
                case "capsule":
                    var capsule = Undo.AddComponent<CapsuleCollider>(go);
                    if (center.HasValue) capsule.center = center.Value;
                    if (radius.HasValue) capsule.radius = radius.Value;
                    if (height.HasValue) capsule.height = height.Value;
                    col3D = capsule;
                    break;
                case "mesh":
                    var mesh = Undo.AddComponent<MeshCollider>(go);
                    col3D = mesh;
                    break;
                case "box2d":
                    var box2d = Undo.AddComponent<BoxCollider2D>(go);
                    if (size.HasValue) box2d.size = new Vector2(size.Value.x, size.Value.y);
                    col2D = box2d;
                    break;
                case "circle2d":
                    var circle = Undo.AddComponent<CircleCollider2D>(go);
                    if (radius.HasValue) circle.radius = radius.Value;
                    col2D = circle;
                    break;
                case "polygon2d":
                    col2D = Undo.AddComponent<PolygonCollider2D>(go);
                    break;
                case "edge2d":
                    col2D = Undo.AddComponent<EdgeCollider2D>(go);
                    break;
                default:
                    return ToolResult.Error($"Unknown collider type: {type}. Use: Box, Sphere, Capsule, Mesh, Box2D, Circle2D, Polygon2D, Edge2D");
            }

            if (col3D != null)
            {
                col3D.isTrigger = isTrigger;
                if (!string.IsNullOrEmpty(material))
                {
                    var mat = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(material);
                    if (mat != null) col3D.sharedMaterial = mat;
                }
            }
            if (col2D != null)
            {
                col2D.isTrigger = isTrigger;
                if (!string.IsNullOrEmpty(material))
                {
                    var mat2d = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(material);
                    if (mat2d != null) col2D.sharedMaterial = mat2d;
                }
            }

            return ToolResult.Text($"Added {type} collider to '{go.name}'");
        }

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
                Physics2D.gravity = new Vector2(gravity.x, gravity.y);
                return ToolResult.Text($"Set 2D gravity to ({gravity.x}, {gravity.y})");
            }

            Physics.gravity = gravity;
            return ToolResult.Text($"Set gravity to ({gravity.x}, {gravity.y}, {gravity.z})");
        }
    }
}
