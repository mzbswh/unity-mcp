using UnityEditor;
using UnityEngine;
using UnityMcp.Editor.Window;
using UnityMcp.Shared.Instance;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Core
{
    [InitializeOnLoad]
    public static class McpServer
    {
        public static ToolRegistry Registry { get; private set; }
        public static TcpTransport Transport { get; private set; }

        private static RequestHandler s_handler;
        private static bool s_initialized;

        static McpServer()
        {
            EditorApplication.delayCall += Initialize;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterReload;
            EditorApplication.quitting += Shutdown;
        }

        private static void Initialize()
        {
            if (s_initialized) return;

            var settings = McpSettings.Instance;

            // 0. Sync log settings
            McpLogger.CurrentLogLevel = (McpLogger.LogLevel)(int)settings.LogLevel;
            McpLogger.AuditEnabled = settings.EnableAuditLog;

            // 0b. Dependency check (first run only)
            if (!EditorPrefs.GetBool("UnityMcp_SetupDone", false))
            {
                var deps = DependencyChecker.Check();
                if (!deps.AllSatisfied)
                    McpSetupWindow.ShowWindow(deps);
            }

            // 1. Scan and register all tools
            Registry = new ToolRegistry();
            Registry.ScanAll();

            // 2. Initialize request handler
            s_handler = new RequestHandler(Registry, settings.RequestTimeoutMs);

            // 3. Start TCP listener
            int port = settings.Port;
            Transport = new TcpTransport(port, s_handler);
            Transport.Start();

            // 4. No external process to start.
            //    - Built-in mode: Bridge is launched by MCP client, not by Unity.
            //    - Python mode: MCP client launches 'uvx unity-mcp-server' via stdio.

            // 5. Register instance for multi-instance discovery
            InstanceDiscovery.Register(port, Application.dataPath.Replace("/Assets", ""));

            // 6. Persist port for domain reload recovery
            EditorPrefs.SetInt("UnityMcp_Port", port);

            // 7. Check for updates (daily)
            PackageUpdateChecker.CheckOncePerDay();

            s_initialized = true;
            McpLogger.Info($"Server started on TCP:{port} " +
                           $"({Registry.ToolCount} tools, {Registry.ResourceCount} resources, " +
                           $"{Registry.PromptCount} prompts)");
        }

        private static void OnBeforeReload()
        {
            Transport?.BroadcastReloading();
            Transport?.Stop();
        }

        private static void OnAfterReload()
        {
            s_initialized = false;
            EditorApplication.delayCall += Initialize;
        }

        public static void Shutdown()
        {
            int port = EditorPrefs.GetInt("UnityMcp_Port", 0);
            if (port > 0)
                InstanceDiscovery.Unregister(port);

            Transport?.Stop();
            s_initialized = false;
        }

        /// <summary>Restart the server (called from settings window).</summary>
        public static void Restart()
        {
            int oldPort = EditorPrefs.GetInt("UnityMcp_Port", 0);
            if (oldPort > 0) InstanceDiscovery.Unregister(oldPort);

            Transport?.Stop();
            s_initialized = false;
            Initialize();
        }
    }
}
