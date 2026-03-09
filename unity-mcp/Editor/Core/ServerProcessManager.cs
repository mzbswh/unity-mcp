using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityMcp.Shared.Utils;
using Debug = UnityEngine.Debug;

namespace UnityMcp.Editor.Core
{
    public class ServerProcessManager
    {
        private readonly McpSettings _settings;
        private Process _serverProcess;
        private const string PidPrefKey = "UnityMcp_ServerPID";

        public bool IsRunning => _serverProcess != null && !_serverProcess.HasExited;

        public ServerProcessManager(McpSettings settings)
        {
            _settings = settings;
        }

        public void StartServer()
        {
            if (TryRecoverExistingProcess()) return;
            CleanupOrphanedProcess();
            LaunchNewProcess();
            ScheduleStartupVerification();
        }

        public void StopServer()
        {
            if (!IsRunning) return;
            try
            {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
                using var kill = Process.Start(new ProcessStartInfo
                {
                    FileName = "kill",
                    Arguments = $"-TERM {_serverProcess.Id}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                kill?.WaitForExit(1000);
                if (!_serverProcess.HasExited)
                    _serverProcess.Kill();
#else
                _serverProcess.Kill();
#endif
                _serverProcess.WaitForExit(5000);
            }
            catch { }
            finally { Cleanup(); }
        }

        private bool TryRecoverExistingProcess()
        {
            int savedPid = EditorPrefs.GetInt(PidPrefKey, -1);
            if (savedPid <= 0) return false;
            try
            {
                var process = Process.GetProcessById(savedPid);
                if (process != null && !process.HasExited)
                {
                    _serverProcess = process;
                    _serverProcess.EnableRaisingEvents = true;
                    _serverProcess.Exited += OnProcessExited;
                    McpLogger.Info($"Recovered existing server process (PID: {savedPid})");
                    return true;
                }
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }

            EditorPrefs.DeleteKey(PidPrefKey);
            return false;
        }

        private void CleanupOrphanedProcess()
        {
            int port = _settings.Port;
            int? listeningPid = GetPidListeningOnPort(port);
            if (listeningPid == null) return;

            // Never kill our own process — the Unity Editor's TcpTransport
            // is already listening on this port.
            int selfPid = Process.GetCurrentProcess().Id;
            if (listeningPid.Value == selfPid)
            {
                McpLogger.Debug($"Port {port} is held by current process (PID {selfPid}), skipping cleanup.");
                return;
            }

            try
            {
                var process = Process.GetProcessById(listeningPid.Value);
                if (process.HasExited) return;
                McpLogger.Warning($"Port {port} occupied by PID {listeningPid}. Terminating...");
                process.Kill();
                process.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                McpLogger.Warning($"Failed to clean orphaned process: {ex.Message}");
            }
        }

        private void LaunchNewProcess()
        {
            var startInfo = _settings.Mode switch
            {
                McpSettings.ServerMode.BuiltIn => CreateBridgeStartInfo(),
                McpSettings.ServerMode.Python => CreatePythonStartInfo(),
                _ => null
            };
            if (startInfo == null) return;

            _serverProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            _serverProcess.Exited += OnProcessExited;
            _serverProcess.Start();
            EditorPrefs.SetInt(PidPrefKey, _serverProcess.Id);
            McpLogger.Info($"Started {_settings.Mode} server (PID: {_serverProcess.Id})");
        }

        private void ScheduleStartupVerification()
        {
            int pid = _serverProcess?.Id ?? -1;
            double checkTime = EditorApplication.timeSinceStartup + 5.0;
            void Check()
            {
                if (EditorApplication.timeSinceStartup < checkTime) return;
                EditorApplication.update -= Check;
                if (_serverProcess == null || _serverProcess.HasExited)
                {
                    McpLogger.Error($"Server process (PID: {pid}) exited within 5s of startup.");
                    Cleanup();
                }
            }
            EditorApplication.update += Check;
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            McpLogger.Warning("Server process exited unexpectedly");
            MainThreadDispatcher.RunAsync(() => Cleanup());
        }

        private void Cleanup()
        {
            try { _serverProcess?.Dispose(); } catch { }
            _serverProcess = null;
            EditorPrefs.DeleteKey(PidPrefKey);
        }

        private ProcessStartInfo CreateBridgeStartInfo()
        {
            string bridgePath = _settings.BridgePath;
            if (string.IsNullOrEmpty(bridgePath))
                bridgePath = GetDefaultBridgePathStatic();

            return new ProcessStartInfo
            {
                FileName = bridgePath,
                Arguments = _settings.Port.ToString(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            };
        }

        private ProcessStartInfo CreatePythonStartInfo()
        {
            string serverScript = _settings.PythonServerScript;
            if (string.IsNullOrEmpty(serverScript))
                return null;

            // Validate script path to prevent command injection
            if (serverScript.Contains("..") || serverScript.Contains(";")
                || serverScript.Contains("&") || serverScript.Contains("|")
                || serverScript.Contains("`") || serverScript.Contains("$"))
            {
                McpLogger.Error($"Invalid server script path: {serverScript}");
                return null;
            }

            // Validate Python path
            string pythonPath = _settings.PythonPath;
            if (!string.IsNullOrEmpty(pythonPath))
            {
                var pythonName = Path.GetFileNameWithoutExtension(pythonPath).ToLower();
                if (pythonName != "python" && pythonName != "python3" && pythonName != "uv")
                {
                    McpLogger.Error($"Suspicious Python path: {pythonPath}. Expected python/python3/uv.");
                    return null;
                }
            }

            ProcessStartInfo psi;
            if (_settings.UseUv)
                psi = new ProcessStartInfo
                {
                    FileName = "uv",
                    Arguments = $"run {serverScript}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            else
                psi = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = serverScript,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

            // Pass Unity TCP port via environment variable
            psi.EnvironmentVariables["UNITY_MCP_PORT"] = _settings.Port.ToString();
            return psi;
        }

        /// <summary>Get the platform-specific runtime identifier for the bridge binary.</summary>
        public static string GetBridgeRid()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
                return "win-x64/unity-mcp-bridge.exe";
            if (Application.platform == RuntimePlatform.OSXEditor)
                return RuntimeInformation.OSArchitecture == Architecture.Arm64
                    ? "osx-arm64/unity-mcp-bridge" : "osx-x64/unity-mcp-bridge";
            return "linux-x64/unity-mcp-bridge";
        }

        /// <summary>Auto-detect the default bridge executable path.</summary>
        public static string GetDefaultBridgePathStatic()
        {
            string rid = GetBridgeRid();
            var packagePath = Path.GetFullPath("Packages/com.mzbswh.unity-mcp");

            // 1. Bundled inside the UPM package (Bridge~/{rid}/) — works with git URL installs
            var bundledPath = Path.Combine(packagePath, "Bridge~", rid);
            if (File.Exists(bundledPath)) return bundledPath;

            // 2. Sibling unity-bridge/ directory (local/clone installs)
            var repoRoot = Path.GetDirectoryName(packagePath);
            var siblingPath = Path.Combine(repoRoot, "unity-bridge", "bin", rid);
            if (File.Exists(siblingPath)) return siblingPath;

            // 3. Fallback: project root
            var projectPath = Path.GetFullPath(Path.Combine("unity-bridge", "bin", rid));
            if (File.Exists(projectPath)) return projectPath;

            // Not found
            McpLogger.Warning(
                $"Bridge binary not found at expected locations.\n" +
                $"  Bundled:  {bundledPath}\n" +
                $"  Sibling:  {siblingPath}\n" +
                $"  Project:  {projectPath}\n" +
                $"  Run: ./scripts/build_bridge.sh");
            return bundledPath;
        }

        private static int? GetPidListeningOnPort(int port)
        {
#if UNITY_EDITOR_WIN
            string cmd = "netstat";
            string args = "-ano -p tcp";
#else
            string cmd = "lsof";
            string args = $"-ti tcp:{port} -sTCP:LISTEN";
#endif
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = cmd, Arguments = args,
                    UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);

#if UNITY_EDITOR_WIN
                foreach (var line in output.Split('\n'))
                {
                    if (line.Contains($":{port}") && line.Contains("LISTENING"))
                    {
                        var parts = line.Trim().Split(new[] { ' ' },
                            StringSplitOptions.RemoveEmptyEntries);
                        if (int.TryParse(parts[parts.Length - 1], out int pid)) return pid;
                    }
                }
#else
                if (int.TryParse(output.Trim(), out int pid)) return pid;
#endif
            }
            catch { }
            return null;
        }
    }
}
