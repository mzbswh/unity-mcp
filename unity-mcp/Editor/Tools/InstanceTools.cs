using System.Linq;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Instance;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("InstanceManagement")]
    public static class InstanceTools
    {
        [McpTool("instance_list",
            "List all connected Unity Editor instances with project name, port, PID, and Unity version.",
            ReadOnly = true, Idempotent = true, Group = "instance")]
        public static ToolResult ListInstances()
        {
            var instances = InstanceDiscovery.DiscoverAll();
            return ToolResult.Json(new
            {
                count = instances.Count,
                instances = instances.Select(i => new
                {
                    port = i.Port,
                    projectName = i.ProjectName,
                    projectPath = i.ProjectPath,
                    pid = i.Pid,
                    unityVersion = i.UnityVersion,
                    startTime = i.StartTime
                })
            });
        }

        [McpTool("instance_set_active",
            "Set the active Unity instance. All subsequent tool calls will be routed to this instance.",
            Group = "instance")]
        public static ToolResult SetActive(
            [Desc("Port number of the target Unity instance")] int port)
        {
            var instances = InstanceDiscovery.DiscoverAll();
            var target = instances.FirstOrDefault(i => i.Port == port);
            if (target == null)
                return ToolResult.Error($"No Unity instance found on port {port}");

            return ToolResult.Json(new
            {
                success = true,
                activeInstance = new
                {
                    port = target.Port,
                    projectName = target.ProjectName,
                    unityVersion = target.UnityVersion
                },
                _meta = new { routeSwitch = port }
            });
        }
    }
}
