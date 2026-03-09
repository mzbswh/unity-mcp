using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityMcp.Shared.Instance
{
    public static class InstanceDiscovery
    {
        private static readonly string RegistryDir =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UnityMCP", "instances");

        public static void Register(int port, string projectPath)
        {
            Directory.CreateDirectory(RegistryDir);

            var info = new InstanceInfo
            {
                Port = port,
                ProjectPath = projectPath,
                ProjectName = Path.GetFileName(projectPath),
                Pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                UnityVersion = Application.unityVersion,
                StartTime = DateTime.UtcNow.ToString("o"),
            };

            var filePath = Path.Combine(RegistryDir, $"{port}.json");
            File.WriteAllText(filePath, JsonUtility.ToJson(info, true));
        }

        public static void Unregister(int port)
        {
            var filePath = Path.Combine(RegistryDir, $"{port}.json");
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        public static List<InstanceInfo> DiscoverAll()
        {
            if (!Directory.Exists(RegistryDir))
                return new List<InstanceInfo>();

            var instances = new List<InstanceInfo>();
            foreach (var file in Directory.GetFiles(RegistryDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var info = JsonUtility.FromJson<InstanceInfo>(json);

                    try
                    {
                        var process = System.Diagnostics.Process.GetProcessById(info.Pid);
                        if (!process.HasExited)
                        {
                            instances.Add(info);
                            continue;
                        }
                    }
                    catch
                    {
                        // Process doesn't exist
                    }

                    // Zombie registration, clean up
                    File.Delete(file);
                }
                catch
                {
                    try { File.Delete(file); } catch { }
                }
            }

            return instances;
        }
    }
}
