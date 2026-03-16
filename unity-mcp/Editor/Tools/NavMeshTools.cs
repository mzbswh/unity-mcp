using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("NavMesh")]
    public static class NavMeshTools
    {
        [McpTool("navmesh_bake", "Bake NavMesh using legacy baking system",
            Group = "navmesh")]
        public static ToolResult Bake()
        {
#pragma warning disable CS0618
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#pragma warning restore CS0618
            return ToolResult.Text("NavMesh baked successfully");
        }

        [McpTool("navmesh_clear", "Clear the baked NavMesh data",
            Group = "navmesh")]
        public static ToolResult Clear()
        {
#pragma warning disable CS0618
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
#pragma warning restore CS0618
            return ToolResult.Text("NavMesh data cleared");
        }
    }
}
