using Newtonsoft.Json.Linq;
using UnityMcp.Editor.Core;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Window.ClientConfig
{
    /// <summary>
    /// Builds the MCP server entry JSON for client configs, respecting custom uvx path,
    /// server source override, and dev mode settings.
    /// </summary>
    public static class ServerEntryBuilder
    {
        public static JObject Build(int port, string transport, int httpPort)
        {
            if (transport == "streamable-http")
            {
                return new JObject
                {
                    ["type"] = "http",
                    ["url"] = $"http://127.0.0.1:{httpPort}/mcp"
                };
            }

            var settings = McpSettings.Instance;
            string uvxCommand = string.IsNullOrEmpty(settings.UvxPath) ? "uvx" : settings.UvxPath;
            string serverSource = settings.ServerSourceOverride;
            bool devMode = settings.DevModeForceRefresh;

            var args = new JArray();

            // Dev mode: add --no-cache --refresh before the package name
            if (devMode)
            {
                args.Add("--no-cache");
                args.Add("--refresh");
            }

            // Server source override: use --from to specify local or custom source
            if (!string.IsNullOrEmpty(serverSource))
            {
                args.Add("--from");
                args.Add(serverSource);
            }

            args.Add("unity-mcp-server");

            var entry = new JObject
            {
                ["type"] = "stdio",
                ["command"] = uvxCommand,
                ["args"] = args
            };

            if (port != PortResolver.DefaultPort)
                entry["env"] = new JObject { ["UNITY_MCP_PORT"] = port.ToString() };

            return entry;
        }
    }
}
