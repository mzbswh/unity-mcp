using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityMcp.Editor.Core
{
    public static class DependencyChecker
    {
        public struct DependencyStatus
        {
            public bool UvFound;
            public string UvVersion;
            /// <summary>Only requirement is uv — it provides uvx and manages Python automatically.</summary>
            public bool AllSatisfied => UvFound;
        }

        public static DependencyStatus Check()
        {
            var status = new DependencyStatus();

            string uvPath = FindInPath("uv");
            if (uvPath != null)
            {
                var (uvOk, uvOut) = TryRun(uvPath, "--version");
                status.UvFound = uvOk;
                if (uvOk)
                    status.UvVersion = ParseVersion(uvOut);
            }

            return status;
        }

        /// <summary>
        /// Build an augmented PATH that includes common install locations.
        /// Unity.app on macOS inherits a minimal PATH that misses user-installed tools.
        /// </summary>
        private static string BuildAugmentedPath()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string existing = Environment.GetEnvironmentVariable("PATH") ?? "";

            var sb = new StringBuilder();
            // Common install locations for uv and other dev tools
            var extraDirs = new[]
            {
                Path.Combine(home, ".local", "bin"),
                Path.Combine(home, ".cargo", "bin"),
                "/opt/homebrew/bin",
                "/usr/local/bin",
                "/usr/bin",
                "/bin",
            };

            foreach (var dir in extraDirs)
            {
                if (sb.Length > 0) sb.Append(':');
                sb.Append(dir);
            }

            if (!string.IsNullOrEmpty(existing))
            {
                sb.Append(':');
                sb.Append(existing);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Resolve a command to its full path using /usr/bin/which with augmented PATH.
        /// Uses EnvironmentVariables (not Environment) for Mono compatibility.
        /// </summary>
        private static string FindInPath(string command)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // First: direct file existence checks for speed
            var candidates = new[]
            {
                Path.Combine(home, ".local", "bin", command),
                Path.Combine(home, ".cargo", "bin", command),
                "/opt/homebrew/bin/" + command,
                "/usr/local/bin/" + command,
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                    return path;
            }

            // Fallback: use /usr/bin/which with augmented PATH
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/which",
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                // Use EnvironmentVariables (older .NET API) for Mono compatibility
                psi.EnvironmentVariables["PATH"] = BuildAugmentedPath();

                var output = new StringBuilder();
                using var process = Process.Start(psi);
                if (process == null) return null;

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null) output.AppendLine(e.Data);
                };
                process.BeginOutputReadLine();
                if (!process.WaitForExit(5000))
                    return null;
                // Flush async output buffers
                process.WaitForExit();

                string result = output.ToString().Trim();
                if (process.ExitCode == 0 && !string.IsNullOrEmpty(result) && File.Exists(result))
                    return result;
            }
            catch
            {
                // Ignore
            }

            return null;
        }

        /// <summary>
        /// Run a resolved command with augmented PATH.
        /// </summary>
        internal static (bool success, string output) TryRun(string command, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                psi.EnvironmentVariables["PATH"] = BuildAugmentedPath();

                var stdout = new StringBuilder();
                var stderr = new StringBuilder();
                using var process = Process.Start(psi);
                if (process == null) return (false, null);

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null) stdout.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null) stderr.AppendLine(e.Data);
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (!process.WaitForExit(5000))
                    return (false, null);
                // Flush async output buffers
                process.WaitForExit();

                string output = stdout.ToString().Trim();
                if (string.IsNullOrEmpty(output))
                    output = stderr.ToString().Trim();
                return (process.ExitCode == 0, output);
            }
            catch
            {
                return (false, null);
            }
        }

        // Keep for backward compat with tests
        internal static (bool success, string output) RunProcess(string command, string args)
            => TryRun(command, args);

        internal static (bool success, string output) RunCommand(string command, string args)
            => TryRun(command, args);

        internal static string ParseVersion(string output)
        {
            if (string.IsNullOrEmpty(output)) return null;
            var match = Regex.Match(output, @"(\d+\.\d+[\.\d]*)");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
