using UnityEditor;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Resources
{
    [McpToolGroup("ProjectResources")]
    public static class ProjectResources
    {
        [McpResource("unity://project/info", "Project Info",
            "Unity project information including version, platform, and render pipeline")]
        public static ToolResult GetProjectInfo()
        {
            return ToolResult.Json(new
            {
                projectName = Application.productName,
                companyName = Application.companyName,
                unityVersion = Application.unityVersion,
                dataPath = Application.dataPath,
                platform = Application.platform.ToString(),
                systemLanguage = Application.systemLanguage.ToString(),
                isPlaying = EditorApplication.isPlaying,
                buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                scriptingBackend = PlayerSettings.GetScriptingBackend(
                    EditorUserBuildSettings.selectedBuildTargetGroup).ToString(),
                colorSpace = PlayerSettings.colorSpace.ToString(),
            });
        }
    }
}
