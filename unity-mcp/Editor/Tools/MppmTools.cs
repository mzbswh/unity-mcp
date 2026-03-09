using System;
using System.Linq;
using UnityEditor;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("MPPM")]
    public static class MppmTools
    {
        [McpTool("editor_is_clone", "Check if the current Editor is a Multiplayer Play Mode clone instance",
            Group = "mppm", ReadOnly = true, Idempotent = true)]
        public static ToolResult IsClone()
        {
            // MPPM clone detection via Unity 2023.1+ API
            // Check for Multiplayer Playmode package and clone status
            bool isClone = false;
            string playerIndex = null;

            // Try reflection to access MPPM API (com.unity.multiplayer.playmode)
            var mppmType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => t.FullName == "Unity.Multiplayer.Playmode.CurrentPlayer");

            if (mppmType != null)
            {
                var readOnlyTagProp = mppmType.GetProperty("ReadOnlyTag",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (readOnlyTagProp != null)
                {
                    var tag = readOnlyTagProp.GetValue(null)?.ToString();
                    isClone = !string.IsNullOrEmpty(tag);
                    playerIndex = tag;
                }
            }

            // Fallback: check command line args for MPPM indicators
            if (!isClone)
            {
                var args = Environment.GetCommandLineArgs();
                isClone = args.Any(a => a.Contains("mppmTag") || a.Contains("-integratedClone"));
            }

            return ToolResult.Json(new
            {
                isClone,
                playerIndex,
                message = isClone
                    ? $"This is an MPPM clone instance (player: {playerIndex ?? "unknown"})"
                    : "This is the main Editor instance"
            });
        }

        [McpTool("editor_get_mppm_tags", "Get Multiplayer Play Mode player tags configuration",
            Group = "mppm", ReadOnly = true, Idempotent = true)]
        public static ToolResult GetMppmTags()
        {
            // Try to access MPPM VirtualProjectsEditor API
            var vpType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => t.FullName == "Unity.Multiplayer.Playmode.VirtualProjects.Editor.VirtualProjectsEditor");

            if (vpType == null)
            {
                return ToolResult.Json(new
                {
                    available = false,
                    message = "Multiplayer Play Mode package (com.unity.multiplayer.playmode) is not installed or not available in this Unity version."
                });
            }

            // Get player count and tags via reflection
            var getPlayerCountMethod = vpType.GetMethod("GetPlayerCount",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            int playerCount = 0;
            if (getPlayerCountMethod != null)
                playerCount = (int)getPlayerCountMethod.Invoke(null, null);

            return ToolResult.Json(new
            {
                available = true,
                playerCount,
                message = $"MPPM configured with {playerCount} players"
            });
        }
    }
}
