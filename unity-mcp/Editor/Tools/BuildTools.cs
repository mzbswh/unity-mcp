using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Build")]
    public static class BuildTools
    {
        [McpTool("build_player", "Build the player for a target platform",
            Group = "build")]
        public static ToolResult BuildPlayer(
            [Desc("Output path (e.g. Builds/MyGame.exe, Builds/MyGame.apk)")] string outputPath,
            [Desc("Target platform: StandaloneWindows64, StandaloneOSX, StandaloneLinux64, Android, iOS, WebGL")] string target = null,
            [Desc("Scene paths to include (null = Build Settings scenes)")] string[] scenes = null,
            [Desc("Development build")] bool development = false,
            [Desc("Autoconnect profiler (requires development)")] bool connectProfiler = false,
            [Desc("Allow debugging (requires development)")] bool allowDebugging = false)
        {
            BuildTarget buildTarget;
            if (!string.IsNullOrEmpty(target))
            {
                if (!System.Enum.TryParse(target, true, out buildTarget))
                    return ToolResult.Error($"Unknown build target: {target}. Use: StandaloneWindows64, StandaloneOSX, StandaloneLinux64, Android, iOS, WebGL");
            }
            else
            {
                buildTarget = EditorUserBuildSettings.activeBuildTarget;
            }

            var sceneList = scenes ?? EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (sceneList.Length == 0)
                return ToolResult.Error("No scenes to build. Add scenes to Build Settings or pass scene paths.");

            var options = BuildOptions.None;
            if (development) options |= BuildOptions.Development;
            if (connectProfiler) options |= BuildOptions.ConnectWithProfiler;
            if (allowDebugging) options |= BuildOptions.AllowDebugging;

            var dir = System.IO.Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var report = BuildPipeline.BuildPlayer(sceneList, outputPath, buildTarget, options);

            return ToolResult.Json(new
            {
                success = report.summary.result == BuildResult.Succeeded,
                result = report.summary.result.ToString(),
                platform = buildTarget.ToString(),
                outputPath,
                totalSize = report.summary.totalSize,
                totalTime = report.summary.totalTime.TotalSeconds,
                totalErrors = report.summary.totalErrors,
                totalWarnings = report.summary.totalWarnings,
                scenes = sceneList,
            });
        }

        [McpTool("build_get_settings", "Get current Build Settings (scenes, target, options)",
            Group = "build", ReadOnly = true)]
        public static ToolResult GetSettings()
        {
            var scenes = EditorBuildSettings.scenes.Select((s, i) => new
            {
                index = i,
                path = s.path,
                enabled = s.enabled,
                guid = s.guid.ToString(),
            }).ToArray();

            return ToolResult.Json(new
            {
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                activeBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup.ToString(),
                development = EditorUserBuildSettings.development,
                scenes,
            });
        }

        [McpTool("build_set_scenes", "Set the scenes in Build Settings",
            Group = "build")]
        public static ToolResult SetScenes(
            [Desc("Array of scene asset paths in build order")] string[] scenes)
        {
            if (scenes == null || scenes.Length == 0)
                return ToolResult.Error("At least one scene is required");

            var editorScenes = scenes.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            EditorBuildSettings.scenes = editorScenes;

            return ToolResult.Text($"Set {scenes.Length} scenes in Build Settings");
        }

        [McpTool("build_switch_platform", "Switch the active build target platform",
            Group = "build")]
        public static ToolResult SwitchPlatform(
            [Desc("Target platform: StandaloneWindows64, StandaloneOSX, StandaloneLinux64, Android, iOS, WebGL")] string target)
        {
            if (!System.Enum.TryParse<BuildTarget>(target, true, out var buildTarget))
                return ToolResult.Error($"Unknown build target: {target}");

            var targetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            bool success = EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, buildTarget);

            return success
                ? ToolResult.Text($"Switched to {buildTarget}")
                : ToolResult.Error($"Failed to switch to {buildTarget}. Check if the platform module is installed.");
        }

        [McpTool("build_get_player_settings", "Get PlayerSettings for the current platform",
            Group = "build", ReadOnly = true)]
        public static ToolResult GetPlayerSettings()
        {
            return ToolResult.Json(new
            {
                companyName = PlayerSettings.companyName,
                productName = PlayerSettings.productName,
                bundleVersion = PlayerSettings.bundleVersion,
                defaultScreenWidth = PlayerSettings.defaultScreenWidth,
                defaultScreenHeight = PlayerSettings.defaultScreenHeight,
                fullscreenMode = PlayerSettings.fullScreenMode.ToString(),
                runInBackground = PlayerSettings.runInBackground,
                apiCompatibilityLevel = PlayerSettings.GetApiCompatibilityLevel(
                    EditorUserBuildSettings.selectedBuildTargetGroup).ToString(),
                scriptingBackend = PlayerSettings.GetScriptingBackend(
                    EditorUserBuildSettings.selectedBuildTargetGroup).ToString(),
                colorSpace = PlayerSettings.colorSpace.ToString(),
            });
        }

        [McpTool("build_set_player_settings", "Modify PlayerSettings",
            Group = "build")]
        public static ToolResult SetPlayerSettings(
            [Desc("Company name")] string companyName = null,
            [Desc("Product name")] string productName = null,
            [Desc("Bundle version string")] string bundleVersion = null,
            [Desc("Default screen width")] int? screenWidth = null,
            [Desc("Default screen height")] int? screenHeight = null,
            [Desc("Run in background")] bool? runInBackground = null,
            [Desc("Color space: Gamma, Linear")] string colorSpace = null,
            [Desc("Scripting backend: Mono2x, IL2CPP")] string scriptingBackend = null)
        {
            int modified = 0;

            if (!string.IsNullOrEmpty(companyName)) { PlayerSettings.companyName = companyName; modified++; }
            if (!string.IsNullOrEmpty(productName)) { PlayerSettings.productName = productName; modified++; }
            if (!string.IsNullOrEmpty(bundleVersion)) { PlayerSettings.bundleVersion = bundleVersion; modified++; }
            if (screenWidth.HasValue) { PlayerSettings.defaultScreenWidth = screenWidth.Value; modified++; }
            if (screenHeight.HasValue) { PlayerSettings.defaultScreenHeight = screenHeight.Value; modified++; }
            if (runInBackground.HasValue) { PlayerSettings.runInBackground = runInBackground.Value; modified++; }

            if (!string.IsNullOrEmpty(colorSpace))
            {
                if (System.Enum.TryParse<ColorSpace>(colorSpace, true, out var cs))
                { PlayerSettings.colorSpace = cs; modified++; }
            }

            if (!string.IsNullOrEmpty(scriptingBackend))
            {
                if (System.Enum.TryParse<ScriptingImplementation>(scriptingBackend, true, out var sb))
                {
                    PlayerSettings.SetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup, sb);
                    modified++;
                }
            }

            return ToolResult.Text($"Modified {modified} PlayerSettings");
        }
    }
}
