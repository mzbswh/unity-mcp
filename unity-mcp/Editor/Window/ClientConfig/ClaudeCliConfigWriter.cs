using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityMcp.Editor.Core;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Window.ClientConfig
{
    public class ClaudeCliConfigWriter : IConfigWriter
    {
        public McpStatus CheckStatus(ClientProfile profile, int port, string transport)
        {
            string path = profile.Paths.Current;
            if (!File.Exists(path)) return McpStatus.NotConfigured;

            try
            {
                var root = JObject.Parse(File.ReadAllText(path));
                string projectPath = Application.dataPath.Replace("/Assets", "");
                var unity = root["projects"]?[projectPath]?["mcpServers"]?["unity"];
                if (unity == null) return McpStatus.NotConfigured;
                return McpStatus.Configured;
            }
            catch { return McpStatus.NotConfigured; }
        }

        public void Configure(ClientProfile profile, int port, string transport, int httpPort)
        {
            string path = profile.Paths.Current;
            string projectPath = Application.dataPath.Replace("/Assets", "");

            JObject root;
            if (File.Exists(path))
                root = JObject.Parse(File.ReadAllText(path));
            else
                root = new JObject();

            if (root["projects"] == null)
                root["projects"] = new JObject();
            var projects = (JObject)root["projects"];

            if (projects[projectPath] == null)
                projects[projectPath] = new JObject();
            var project = (JObject)projects[projectPath];

            if (project["mcpServers"] == null)
                project["mcpServers"] = new JObject();

            ((JObject)project["mcpServers"])["unity"] = BuildEntry(port, transport, httpPort);

            File.WriteAllText(path, root.ToString(Formatting.Indented));
            McpLogger.Info($"Claude Code configured: {path}");
        }

        public string GetManualSnippet(ClientProfile profile, int port, string transport, int httpPort)
        {
            string projectPath = Application.dataPath.Replace("/Assets", "");
            var snippet = new JObject
            {
                ["projects"] = new JObject
                {
                    [projectPath] = new JObject
                    {
                        ["mcpServers"] = new JObject
                        {
                            ["unity"] = BuildEntry(port, transport, httpPort)
                        }
                    }
                }
            };
            return snippet.ToString(Formatting.Indented);
        }

        private static JObject BuildEntry(int port, string transport, int httpPort)
        {
            if (transport == "streamable-http")
            {
                return new JObject
                {
                    ["type"] = "http",
                    ["url"] = $"http://127.0.0.1:{httpPort}/mcp"
                };
            }

            var entry = new JObject
            {
                ["type"] = "stdio",
                ["command"] = "uvx",
                ["args"] = new JArray { "unity-mcp-server" }
            };
            if (port != PortResolver.DefaultPort)
                entry["env"] = new JObject { ["UNITY_MCP_PORT"] = port.ToString() };
            return entry;
        }
    }
}
