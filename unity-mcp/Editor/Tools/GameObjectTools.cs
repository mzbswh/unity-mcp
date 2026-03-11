using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Editor.Utils;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("GameObject")]
    public static class GameObjectTools
    {
        [McpTool("gameobject_create", "Create a new GameObject (empty or primitive)",
            Group = "gameobject")]
        public static ToolResult Create(
            [Desc("Name of the new GameObject")] string name,
            [Desc("Primitive type: Cube, Sphere, Capsule, Cylinder, Plane, Quad, or empty")] string primitiveType = null,
            [Desc("World position {x,y,z}")] Vector3? position = null,
            [Desc("Rotation euler angles {x,y,z}")] Vector3? rotation = null)
        {
            GameObject go;
            if (!string.IsNullOrEmpty(primitiveType) &&
                System.Enum.TryParse<PrimitiveType>(primitiveType, true, out var pt))
            {
                go = GameObject.CreatePrimitive(pt);
                go.name = name;
            }
            else
            {
                go = new GameObject(name);
            }

            if (position.HasValue) go.transform.position = position.Value;
            if (rotation.HasValue) go.transform.eulerAngles = rotation.Value;

            UndoHelper.RegisterCreatedObject(go, $"Create {name}");

            return ToolResult.Json(new
            {
                instanceId = go.GetInstanceID(),
                name = go.name,
                message = $"Created GameObject '{name}'"
            });
        }

        [McpTool("gameobject_destroy", "Delete a GameObject by name or instance ID",
            Group = "gameobject")]
        public static ToolResult Destroy(
            [Desc("Name or path of the GameObject")] string name = null,
            [Desc("Instance ID")] int? instanceId = null)
        {
            var go = FindGameObject(name, instanceId);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {name ?? instanceId?.ToString()}");

            string goName = go.name;
            UndoHelper.DestroyObject(go);
            return ToolResult.Text($"Destroyed GameObject '{goName}'");
        }

        [McpTool("gameobject_find", "Find GameObjects by name, tag, path, or component type (paginated)",
            Group = "gameobject", ReadOnly = true)]
        public static ToolResult Find(
            [Desc("Name to search for (partial match)")] string name = null,
            [Desc("Tag to filter by")] string tag = null,
            [Desc("Full hierarchy path")] string path = null,
            [Desc("Component type name to filter by")] string componentType = null,
            [Desc("Page size (default 50, max 200)")] int pageSize = 50,
            [Desc("Pagination cursor from previous response")] string cursor = null)
        {
            IEnumerable<GameObject> results;

            if (!string.IsNullOrEmpty(path))
            {
                var go = GameObject.Find(path);
                results = go != null ? new[] { go } : System.Array.Empty<GameObject>();
            }
            else if (!string.IsNullOrEmpty(tag))
            {
                results = GameObject.FindGameObjectsWithTag(tag);
            }
            else
            {
                results = Object.FindObjectsOfType<GameObject>();
            }

            if (!string.IsNullOrEmpty(name))
                results = results.Where(g => g.name.Contains(name));

            if (!string.IsNullOrEmpty(componentType))
                results = results.Where(g =>
                    g.GetComponents<Component>().Any(c => c != null && c.GetType().Name == componentType));

            var allResults = results.Select(g => new
            {
                instanceId = g.GetInstanceID(),
                name = g.name,
                path = GetPath(g),
                active = g.activeSelf,
                tag = g.tag,
                layer = LayerMask.LayerToName(g.layer)
            }).ToArray();

            return PaginationHelper.ToPaginatedResult(allResults, pageSize, cursor);
        }

        [McpTool("gameobject_modify", "Modify GameObject properties (name, tag, layer, active, static)",
            Group = "gameobject")]
        public static ToolResult Modify(
            [Desc("Name or path of the target GameObject")] string target = null,
            [Desc("Instance ID")] int? instanceId = null,
            [Desc("New name")] string name = null,
            [Desc("New tag")] string tag = null,
            [Desc("New layer name")] string layer = null,
            [Desc("Active state")] bool? active = null,
            [Desc("Static state")] bool? isStatic = null,
            [Desc("New world position")] Vector3? position = null,
            [Desc("New rotation (euler)")] Vector3? rotation = null,
            [Desc("New scale")] Vector3? scale = null)
        {
            var go = FindGameObject(target, instanceId);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target ?? instanceId?.ToString()}");

            UndoHelper.RecordObject(go, "Modify GameObject");
            UndoHelper.RecordObject(go.transform, "Modify Transform");

            if (name != null) go.name = name;
            if (tag != null) go.tag = tag;
            if (layer != null) go.layer = LayerMask.NameToLayer(layer);
            if (active.HasValue) go.SetActive(active.Value);
            if (isStatic.HasValue) go.isStatic = isStatic.Value;
            if (position.HasValue) go.transform.position = position.Value;
            if (rotation.HasValue) go.transform.eulerAngles = rotation.Value;
            if (scale.HasValue) go.transform.localScale = scale.Value;

            EditorUtility.SetDirty(go);
            return ToolResult.Text($"Modified GameObject '{go.name}'");
        }

        [McpTool("gameobject_set_parent", "Set the parent of a GameObject",
            Group = "gameobject")]
        public static ToolResult SetParent(
            [Desc("Name or path of the child")] string child,
            [Desc("Name or path of the parent (null to unparent)")] string parent = null,
            [Desc("Keep world position")] bool worldPositionStays = true)
        {
            var childGo = FindGameObject(child, null);
            if (childGo == null)
                return ToolResult.Error($"Child not found: {child}");

            Transform parentTransform = null;
            if (!string.IsNullOrEmpty(parent))
            {
                var parentGo = FindGameObject(parent, null);
                if (parentGo == null)
                    return ToolResult.Error($"Parent not found: {parent}");
                parentTransform = parentGo.transform;
            }

            UndoHelper.SetTransformParent(childGo.transform, parentTransform, worldPositionStays, "Set Parent");

            return ToolResult.Text($"Set parent of '{childGo.name}' to '{parent ?? "none"}'");
        }

        [McpTool("gameobject_duplicate", "Duplicate a GameObject",
            Group = "gameobject")]
        public static ToolResult Duplicate(
            [Desc("Name or path of the GameObject to duplicate")] string target = null,
            [Desc("Instance ID")] int? instanceId = null)
        {
            var go = FindGameObject(target, instanceId);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target ?? instanceId?.ToString()}");

            var clone = Object.Instantiate(go, go.transform.parent);
            clone.name = go.name;
            UndoHelper.RegisterCreatedObject(clone, $"Duplicate {go.name}");

            return ToolResult.Json(new
            {
                instanceId = clone.GetInstanceID(),
                name = clone.name,
                message = $"Duplicated '{go.name}'"
            });
        }

        [McpTool("gameobject_get_components", "List all components on a GameObject",
            Group = "gameobject", ReadOnly = true)]
        public static ToolResult GetComponents(
            [Desc("Name or path of the GameObject")] string target = null,
            [Desc("Instance ID")] int? instanceId = null)
        {
            var go = FindGameObject(target, instanceId);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target ?? instanceId?.ToString()}");

            var components = go.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c => new
                {
                    type = c.GetType().Name,
                    fullType = c.GetType().FullName,
                    instanceId = c.GetInstanceID(),
                    enabled = c is Behaviour b ? (bool?)b.enabled : null
                }).ToArray();

            return ToolResult.Json(new { gameObject = go.name, components });
        }

        // --- Helpers ---

        internal static GameObject FindGameObject(string nameOrPath, int? instanceId)
        {
            if (instanceId.HasValue)
                return EditorUtility.InstanceIDToObject(instanceId.Value) as GameObject;

            if (string.IsNullOrEmpty(nameOrPath))
                return null;

            // Try exact path first
            var go = GameObject.Find(nameOrPath);
            if (go != null) return go;

            // Fallback: search by name in all loaded objects
            foreach (var obj in Object.FindObjectsOfType<GameObject>())
                if (obj.name == nameOrPath)
                    return obj;

            return null;
        }

        [McpTool("gameobject_look_at", "Make a GameObject look at a target position or another GameObject",
            Group = "gameobject")]
        public static ToolResult LookAt(
            [Desc("Name or path of the GameObject")] string target,
            [Desc("Instance ID")] int? instanceId = null,
            [Desc("World position to look at {x,y,z}")] Vector3? lookAtPosition = null,
            [Desc("Name or path of the GameObject to look at")] string lookAtTarget = null,
            [Desc("Up vector (defaults to Vector3.up)")] Vector3? worldUp = null)
        {
            var go = FindGameObject(target, instanceId);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target ?? instanceId?.ToString()}");

            Vector3 point;
            if (lookAtPosition.HasValue)
            {
                point = lookAtPosition.Value;
            }
            else if (!string.IsNullOrEmpty(lookAtTarget))
            {
                var targetGo = FindGameObject(lookAtTarget, null);
                if (targetGo == null)
                    return ToolResult.Error($"Look-at target not found: {lookAtTarget}");
                point = targetGo.transform.position;
            }
            else
            {
                return ToolResult.Error("Provide either lookAtPosition or lookAtTarget");
            }

            UndoHelper.RecordObject(go.transform, "LookAt");
            go.transform.LookAt(point, worldUp ?? Vector3.up);

            return ToolResult.Text($"'{go.name}' now looking at ({point.x}, {point.y}, {point.z})");
        }

        [McpTool("gameobject_move_relative", "Move a GameObject by a relative offset (in world or local space)",
            Group = "gameobject")]
        public static ToolResult MoveRelative(
            [Desc("Name or path of the GameObject")] string target,
            [Desc("Instance ID")] int? instanceId = null,
            [Desc("Offset to move {x,y,z}")] Vector3? offset = null,
            [Desc("Use local space instead of world space")] bool localSpace = false)
        {
            var go = FindGameObject(target, instanceId);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target ?? instanceId?.ToString()}");

            if (!offset.HasValue)
                return ToolResult.Error("Offset is required");

            UndoHelper.RecordObject(go.transform, "Move Relative");

            if (localSpace)
                go.transform.Translate(offset.Value, Space.Self);
            else
                go.transform.Translate(offset.Value, Space.World);

            var pos = go.transform.position;
            return ToolResult.Json(new
            {
                gameObject = go.name,
                newPosition = new { x = pos.x, y = pos.y, z = pos.z }
            });
        }

        [McpTool("gameobject_set_sibling_index", "Set the sibling index (order among siblings) of a GameObject in the hierarchy",
            Group = "gameobject")]
        public static ToolResult SetSiblingIndex(
            [Desc("Name or path of the GameObject")] string target,
            [Desc("Instance ID")] int? instanceId = null,
            [Desc("New sibling index (0-based). Use -1 to move to last.")] int index = 0)
        {
            var go = FindGameObject(target, instanceId);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target ?? instanceId?.ToString()}");

            UndoHelper.RecordObject(go.transform, "Set Sibling Index");

            if (index < 0)
                go.transform.SetAsLastSibling();
            else
                go.transform.SetSiblingIndex(index);

            EditorUtility.SetDirty(go);
            return ToolResult.Text($"'{go.name}' sibling index set to {go.transform.GetSiblingIndex()}");
        }

        // --- Helpers ---

        internal static string GetPath(GameObject go)
        {
            var path = go.name;
            var t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }
    }
}
