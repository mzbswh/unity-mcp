using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Resources
{
    [McpToolGroup("GameObjectResources")]
    public static class GameObjectResources
    {
        [McpResource("unity://gameobject/{id}", "GameObject Detail",
            "Detailed information about a specific GameObject by instance ID")]
        public static ToolResult GetGameObject(
            [Desc("Instance ID of the GameObject")] string id)
        {
            if (!int.TryParse(id, out int instanceId))
                return ToolResult.Error($"Invalid instance ID: {id}");

            var go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (go == null)
                return ToolResult.Error($"GameObject not found with ID: {id}");

            var transform = go.transform;
            var components = go.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c => new
                {
                    type = c.GetType().Name,
                    instanceId = c.GetInstanceID(),
                    enabled = c is Behaviour b ? (bool?)b.enabled : null
                }).ToArray();

            return ToolResult.Json(new
            {
                instanceId = go.GetInstanceID(),
                name = go.name,
                tag = go.tag,
                layer = LayerMask.LayerToName(go.layer),
                activeSelf = go.activeSelf,
                activeInHierarchy = go.activeInHierarchy,
                isStatic = go.isStatic,
                transform = new
                {
                    position = new { x = transform.position.x, y = transform.position.y, z = transform.position.z },
                    rotation = new { x = transform.eulerAngles.x, y = transform.eulerAngles.y, z = transform.eulerAngles.z },
                    localScale = new { x = transform.localScale.x, y = transform.localScale.y, z = transform.localScale.z },
                    parent = transform.parent != null ? transform.parent.name : null,
                    childCount = transform.childCount,
                },
                components
            });
        }
    }
}
