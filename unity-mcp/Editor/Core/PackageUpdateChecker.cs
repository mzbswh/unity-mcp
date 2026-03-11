using System;
using UnityEditor;
using UnityEngine.Networking;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Core
{
    public static class PackageUpdateChecker
    {
        private const string PackageJsonUrl =
            "https://raw.githubusercontent.com/mzbswh/unity-mcp/main/unity-mcp/package.json";
        private const string PrefKeyLastCheck = "UnityMcp_LastUpdateCheck";
        private const string PrefKeyLatestVersion = "UnityMcp_LatestVersion";

        public static string LatestVersion =>
            EditorPrefs.GetString(PrefKeyLatestVersion, null);

        public static bool HasUpdate
        {
            get
            {
                var latest = LatestVersion;
                if (string.IsNullOrEmpty(latest)) return false;
                return latest != McpConst.ServerVersion &&
                       IsNewer(latest, McpConst.ServerVersion);
            }
        }

        public static void CheckOncePerDay()
        {
            var lastCheck = EditorPrefs.GetString(PrefKeyLastCheck, "");
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            if (lastCheck == today) return;

            EditorPrefs.SetString(PrefKeyLastCheck, today);

            var request = UnityWebRequest.Get(PackageJsonUrl);
            var op = request.SendWebRequest();
            op.completed += _ =>
            {
                try
                {
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var json = Newtonsoft.Json.Linq.JObject.Parse(request.downloadHandler.text);
                        var version = json["version"]?.ToString();
                        if (!string.IsNullOrEmpty(version))
                        {
                            EditorPrefs.SetString(PrefKeyLatestVersion, version);
                            if (IsNewer(version, McpConst.ServerVersion))
                                McpLogger.Info($"Unity MCP update available: v{version} (current: v{McpConst.ServerVersion})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    McpLogger.Debug($"Update check failed: {ex.Message}");
                }
                finally
                {
                    request.Dispose();
                }
            };
        }

        private static bool IsNewer(string candidate, string current)
        {
            if (Version.TryParse(candidate, out var cVer) &&
                Version.TryParse(current, out var curVer))
                return cVer > curVer;
            return false;
        }
    }
}
