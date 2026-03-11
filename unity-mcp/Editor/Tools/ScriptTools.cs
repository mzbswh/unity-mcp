using System.IO;
using UnityEditor;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Script")]
    public static class ScriptTools
    {
        [McpTool("script_create", "Create a new C# script file",
            Group = "script")]
        public static ToolResult Create(
            [Desc("Script name (without .cs extension)")] string name,
            [Desc("Folder path (e.g. Assets/Scripts)")] string folder = "Assets/Scripts",
            [Desc("Script content (leave empty for default MonoBehaviour template)")] string content = null)
        {
            if (string.IsNullOrEmpty(name))
                return ToolResult.Error("Script name is required");

            var pv = PathValidator.QuickValidate(folder);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            string path = Path.Combine(folder, $"{name}.cs");
            if (File.Exists(path))
                return ToolResult.Error($"Script already exists: {path}");

            // Security check on user-provided content (auto-compiled by Unity)
            if (!string.IsNullOrEmpty(content))
            {
                var sc = SecurityChecker.Validate(content);
                if (!sc.IsValid)
                    return ToolResult.Error($"Security violation in script content: {sc.Reason}");
            }

            if (string.IsNullOrEmpty(content))
            {
                content = $@"using UnityEngine;

public class {name} : MonoBehaviour
{{
    void Start()
    {{
    }}

    void Update()
    {{
    }}
}}
";
            }

            File.WriteAllText(path, content);
            AssetDatabase.Refresh();

            return ToolResult.Json(new
            {
                path,
                name,
                message = $"Created script: {path}"
            });
        }

        [McpTool("script_read", "Read the content of a C# script file",
            Group = "script", ReadOnly = true)]
        public static ToolResult Read(
            [Desc("Script asset path (e.g. Assets/Scripts/Player.cs)")] string path)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            if (!File.Exists(path))
                return ToolResult.Error($"Script not found: {path}");

            string content = File.ReadAllText(path);
            return ToolResult.Json(new
            {
                path,
                name = Path.GetFileNameWithoutExtension(path),
                content,
                lineCount = content.Split('\n').Length,
                sizeBytes = new FileInfo(path).Length,
            });
        }

        [McpTool("script_update", "Update the content of an existing C# script",
            Group = "script")]
        public static ToolResult Update(
            [Desc("Script asset path (e.g. Assets/Scripts/Player.cs)")] string path,
            [Desc("New script content")] string content)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            if (!File.Exists(path))
                return ToolResult.Error($"Script not found: {path}");

            if (string.IsNullOrEmpty(content))
                return ToolResult.Error("Content cannot be empty");

            var sc = SecurityChecker.Validate(content);
            if (!sc.IsValid)
                return ToolResult.Error($"Security violation in script content: {sc.Reason}");

            File.WriteAllText(path, content);
            AssetDatabase.Refresh();

            return ToolResult.Text($"Updated script: {path}");
        }
    }
}
