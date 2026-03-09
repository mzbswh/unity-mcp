#if UNITY_MCP_RUNTIME
using System.Linq;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Runtime.Resources
{
    [McpToolGroup("RuntimeObjectResources")]
    public static class RuntimeObjectResources
    {
        [McpResource("unity://runtime/objects/{query}", "Runtime Object Query",
            "Query active GameObjects in runtime by name pattern")]
        public static ToolResult QueryObjects(
            [Desc("Name pattern to search for (partial match)")] string query)
        {
            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            var matches = allObjects
                .Where(go => go.name.Contains(query))
                .Take(50)
                .Select(go => new
                {
                    name = go.name,
                    instanceId = go.GetInstanceID(),
                    active = go.activeInHierarchy,
                    layer = LayerMask.LayerToName(go.layer),
                    tag = go.tag,
                    position = new { x = go.transform.position.x, y = go.transform.position.y, z = go.transform.position.z },
                    componentCount = go.GetComponents<Component>().Count(c => c != null),
                    components = go.GetComponents<Component>()
                        .Where(c => c != null)
                        .Select(c => c.GetType().Name)
                        .ToArray()
                })
                .ToArray();

            return ToolResult.Json(new
            {
                query,
                totalMatches = matches.Length,
                objects = matches
            });
        }
    }
}
#endif
