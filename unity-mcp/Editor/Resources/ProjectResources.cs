using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Resources
{
    [McpToolGroup("ProjectResources")]
    public static class ProjectResources
    {
        [McpResource("unity://project/info", "Project Info",
            "Unity project information including version, platform, render pipeline, and packages")]
        public static ToolResult GetProjectInfo()
        {
            return ToolResult.Json(new
            {
                projectName = Application.productName,
                companyName = Application.companyName,
                unityVersion = Application.unityVersion,
                dataPath = Application.dataPath,
                platform = Application.platform.ToString(),
                buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                scriptingBackend = PlayerSettings.GetScriptingBackend(
                    EditorUserBuildSettings.selectedBuildTargetGroup).ToString(),
                apiCompatibility = PlayerSettings.GetApiCompatibilityLevel(
                    EditorUserBuildSettings.selectedBuildTargetGroup).ToString(),
                colorSpace = PlayerSettings.colorSpace.ToString(),
                renderPipeline = GetRenderPipelineName(),
                packages = GetInstalledPackages(),
            });
        }

        private static string GetRenderPipelineName()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp == null) return "Built-in";
            var typeName = rp.GetType().Name;
            if (typeName.Contains("Universal") || typeName.Contains("URP"))
                return "URP";
            if (typeName.Contains("HighDefinition") || typeName.Contains("HDRP"))
                return "HDRP";
            return typeName;
        }

        private static object[] GetInstalledPackages()
        {
            try
            {
                var manifestPath = System.IO.Path.Combine(
                    Application.dataPath, "..", "Packages", "manifest.json");
                if (!System.IO.File.Exists(manifestPath)) return new object[0];
                var json = Newtonsoft.Json.Linq.JObject.Parse(
                    System.IO.File.ReadAllText(manifestPath));
                var deps = json["dependencies"] as Newtonsoft.Json.Linq.JObject;
                if (deps == null) return new object[0];
                return deps.Properties().Select(p => new
                {
                    name = p.Name,
                    version = p.Value.ToString()
                }).ToArray();
            }
            catch
            {
                return new object[0];
            }
        }
    }
}
