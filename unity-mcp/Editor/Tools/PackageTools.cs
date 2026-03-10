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

        [McpTool("package_remove", "Remove an installed UPM package",
            Group = "package")]
        public static ToolResult Remove(
            [Desc("Package name (e.g. 'com.unity.textmeshpro')")] string name)
        {
            if (string.IsNullOrEmpty(name))
                return ToolResult.Error("Package name is required");

            var request = Client.Remove(name);
            while (!request.IsCompleted) { }

            if (request.Status == StatusCode.Failure)
                return ToolResult.Error($"Failed to remove package: {request.Error?.message}");

            return ToolResult.Text($"Removed package: {name}");
        }

        [McpTool("package_search", "Search for available UPM packages",
            Group = "package", ReadOnly = true)]
        public static ToolResult Search(
            [Desc("Search query (package name or keyword)")] string query = null)
        {
            var request = string.IsNullOrEmpty(query) ? Client.SearchAll() : Client.Search(query);
            while (!request.IsCompleted) { }

            if (request.Status == StatusCode.Failure)
                return ToolResult.Error($"Search failed: {request.Error?.message}");

            var packages = request.Result.Select(p => new
            {
                name = p.name,
                version = p.versions.latest,
                displayName = p.displayName,
                description = p.description?.Length > 100
                    ? p.description.Substring(0, 100) + "..."
                    : p.description,
            }).ToArray();

            return ToolResult.Json(new { count = packages.Length, packages });
        }

        [McpTool("package_get_info", "Get detailed info about an installed package",
            Group = "package", ReadOnly = true)]
        public static ToolResult GetInfo(
            [Desc("Package name (e.g. 'com.unity.textmeshpro')")] string name)
        {
            var listRequest = Client.List(true);
            while (!listRequest.IsCompleted) { }

            if (listRequest.Status == StatusCode.Failure)
                return ToolResult.Error($"Failed: {listRequest.Error?.message}");

            var pkg = listRequest.Result.FirstOrDefault(p => p.name == name);
            if (pkg == null)
                return ToolResult.Error($"Package not found: {name}");

            return ToolResult.Json(new
            {
                name = pkg.name,
                version = pkg.version,
                displayName = pkg.displayName,
                description = pkg.description,
                source = pkg.source.ToString(),
                status = pkg.status.ToString(),
                category = pkg.category,
                dependencies = pkg.dependencies.Select(d => new { name = d.name, version = d.version }).ToArray(),
                resolvedPath = pkg.resolvedPath,
            });
        }
    }
}
