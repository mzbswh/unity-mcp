using UnityEditor;
using UnityEngine;
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
        private static ServerProcessManager s_processManager;
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

            // 1. Scan and register all tools
            Registry = new ToolRegistry();
            Registry.ScanAll();

            // 2. Initialize request handler
            s_handler = new RequestHandler(Registry, settings.RequestTimeoutMs);

            // 3. Start TCP listener
            int port = settings.Port;
            Transport = new TcpTransport(port, s_handler);
            Transport.Start();

            // 4. Start external server process if AutoStart enabled (Mode B only —
            //    Mode A Bridge is launched by MCP client, not by Unity)
            if (settings.AutoStart && settings.Mode == McpSettings.ServerMode.Python)
            {
                s_processManager = new ServerProcessManager(settings);
                s_processManager.StartServer();
            }

            // 5. Register instance for multi-instance discovery
            InstanceDiscovery.Register(port, Application.dataPath.Replace("/Assets", ""));

            // 6. Persist port for domain reload recovery
            EditorPrefs.SetInt("UnityMcp_Port", port);

            s_initialized = true;
            McpLogger.Info($"Server started on TCP:{port} " +
                           $"({Registry.ToolCount} tools, {Registry.ResourceCount} resources, " +
                           $"{Registry.PromptCount} prompts) Mode: {settings.Mode}");
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

        private static void Shutdown()
        {
            // Unregister instance
            int port = EditorPrefs.GetInt("UnityMcp_Port", 0);
            if (port > 0)
                InstanceDiscovery.Unregister(port);

            s_processManager?.StopServer();
            Transport?.Stop();
            s_initialized = false;
        }

        /// <summary>Restart the server (called from settings window).</summary>
        public static void Restart()
        {
            int oldPort = EditorPrefs.GetInt("UnityMcp_Port", 0);
            if (oldPort > 0) InstanceDiscovery.Unregister(oldPort);

            s_processManager?.StopServer();
            s_processManager = null;
            Transport?.Stop();
            s_initialized = false;
            Initialize();
        }
    }
}
