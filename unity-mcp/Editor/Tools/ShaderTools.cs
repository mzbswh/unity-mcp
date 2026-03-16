using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Shader")]
    public static class ShaderTools
    {
        [McpTool("shader_info", "Get detailed info about a shader: properties, passes, keywords, compilation errors",
            Group = "shader", ReadOnly = true)]
        public static ToolResult Info(
            [Desc("Shader name (e.g. 'Standard') or asset path (e.g. Assets/Shaders/X.shader)")] string shader)
        {
            Shader s = null;

            if (!string.IsNullOrEmpty(shader) && shader.StartsWith("Assets/"))
                s = AssetDatabase.LoadAssetAtPath<Shader>(shader);

            if (s == null)
                s = Shader.Find(shader);

            if (s == null)
                return ToolResult.Error($"Shader not found: '{shader}'");

            int propCount = ShaderUtil.GetPropertyCount(s);
            var properties = new object[propCount];
            for (int i = 0; i < propCount; i++)
            {
                properties[i] = new
                {
                    name = ShaderUtil.GetPropertyName(s, i),
                    description = ShaderUtil.GetPropertyDescription(s, i),
                    type = ShaderUtil.GetPropertyType(s, i).ToString(),
                };
            }

            return ToolResult.Json(new
            {
                name = s.name,
                hasErrors = ShaderUtil.ShaderHasError(s),
                isSupported = s.isSupported,
                renderQueue = s.renderQueue,
                passCount = s.passCount,
                propertyCount = propCount,
                properties,
            });
        }

        [McpTool("shader_list", "List all shaders available in the project (built-in + custom)",
            Group = "shader", ReadOnly = true)]
        public static ToolResult List(
            [Desc("Filter by name substring (case-insensitive)")] string filter = null,
            [Desc("Only list project shaders (not built-in)")] bool projectOnly = false)
        {
            var guids = AssetDatabase.FindAssets("t:Shader");
            var results = guids.Select(g =>
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var s = AssetDatabase.LoadAssetAtPath<Shader>(p);
                return new { path = p, name = s?.name ?? "" };
            });

            if (!string.IsNullOrEmpty(filter))
                results = results.Where(r => r.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

            if (projectOnly)
                results = results.Where(r => r.path.StartsWith("Assets/"));

            var list = results.ToArray();
            return ToolResult.Json(new { count = list.Length, shaders = list });
        }

    }
}
