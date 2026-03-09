using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Package")]
    public static class PackageTools
    {
        [McpTool("package_list", "List all installed UPM packages",
            Group = "package", ReadOnly = true)]
        public static ToolResult List()
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
                status = p.status.ToString(),
            }).ToArray();

            return ToolResult.Json(new { count = packages.Length, packages });
        }

        [McpTool("package_add", "Install a UPM package by name or git URL",
            Group = "package")]
        public static ToolResult Add(
            [Desc("Package identifier (e.g. 'com.unity.textmeshpro' or git URL)")] string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return ToolResult.Error("Package identifier is required");

            var request = Client.Add(identifier);
            while (!request.IsCompleted) { }

            if (request.Status == StatusCode.Failure)
                return ToolResult.Error($"Failed to add package: {request.Error?.message}");

            var pkg = request.Result;
            return ToolResult.Json(new
            {
                success = true,
                name = pkg.name,
                version = pkg.version,
                displayName = pkg.displayName,
                message = $"Installed {pkg.displayName} ({pkg.name}@{pkg.version})"
            });
        }
    }
}
