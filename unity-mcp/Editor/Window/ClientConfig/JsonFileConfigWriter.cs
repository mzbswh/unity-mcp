using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityMcp.Editor.Core;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Window.ClientConfig
{
    public class JsonFileConfigWriter : IConfigWriter
    {
        public McpStatus CheckStatus(ClientProfile profile, int port, string transport)
        {
            string path = ResolvePath(profile);
            if (!File.Exists(path)) return McpStatus.NotConfigured;

            try
            {
                var root = JObject.Parse(File.ReadAllText(path));
                string serversKey = GetServersKey(profile);
                var unity = root[serversKey]?["unity"];
                if (unity == null) return McpStatus.NotConfigured;

                var expected = BuildServerEntry(port, transport, 0);
                if (unity["command"]?.ToString() == expected["command"]?.ToString())
                    return McpStatus.Configured;
                return McpStatus.NeedsUpdate;
            }
            catch { return McpStatus.NotConfigured; }
        }

        public void Configure(ClientProfile profile, int port, string transport, int httpPort)
        {
            string path = ResolvePath(profile);
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            JObject root;
            if (File.Exists(path))
                root = JObject.Parse(File.ReadAllText(path));
            else
                root = new JObject();

            string serversKey = GetServersKey(profile);
            if (root[serversKey] == null)
                root[serversKey] = new JObject();

            ((JObject)root[serversKey])["unity"] = BuildServerEntry(port, transport, httpPort);
            File.WriteAllText(path, root.ToString(Formatting.Indented));
        }

        public void Unconfigure(ClientProfile profile)
        {
            string path = ResolvePath(profile);
            if (!File.Exists(path)) return;

            try
            {
                var root = JObject.Parse(File.ReadAllText(path));
                string serversKey = GetServersKey(profile);
                var servers = root[serversKey] as JObject;
                if (servers != null && servers.ContainsKey("unity"))
                {
                    servers.Remove("unity");
                    File.WriteAllText(path, root.ToString(Formatting.Indented));
                }
            }
            catch { /* ignore */ }
        }

        public string GetManualSnippet(ClientProfile profile, int port, string transport, int httpPort)
        {
            string serversKey = GetServersKey(profile);
            var wrapper = new JObject
            {
                [serversKey] = new JObject { ["unity"] = BuildServerEntry(port, transport, httpPort) }
            };
            return wrapper.ToString(Formatting.Indented);
        }

        private static string ResolvePath(ClientProfile profile)
        {
            string raw = profile.Paths.Current;
            if (profile.IsProjectLevel)
            {
                string projectRoot = Application.dataPath.Replace("/Assets", "");
                return Path.Combine(projectRoot, raw);
            }
            return raw;
        }

        private static string GetServersKey(ClientProfile profile)
        {
            // VS Code uses "servers", most others use "mcpServers"
            return profile.Id == "vscode" ? "servers" : "mcpServers";
        }

        private static JObject BuildServerEntry(int port, string transport, int httpPort)
        {
            return ServerEntryBuilder.Build(port, transport, httpPort);
        }
    }
}
