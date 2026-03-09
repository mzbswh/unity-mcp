using System.Linq;
using UnityEditor.PackageManager;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Resources
{
    [McpToolGroup("PackageResources")]
    public static class PackageResources
    {
        [McpResource("unity://packages/list", "Package List",
            "List all installed UPM packages")]
        public static ToolResult GetPackages()
        {
            var request = Client.List(true);
            while (!request.IsCompleted) { }

            if (request.Status == StatusCode.Failure)
                return ToolResult.Error($"Failed to list packages: {request.Error?.message}");

            var packages = request.Result.Select(p => new
            {
                name = p.name,
                version = p.version,
                displayName = p.displayName,
                source = p.source.ToString(),
            }).ToArray();

            return ToolResult.Json(new { count = packages.Length, packages });
        }
    }
}
