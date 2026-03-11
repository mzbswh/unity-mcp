using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace UnityMcp.Editor.Core
{
    public static class DependencyChecker
    {
        public struct DependencyStatus
        {
            public bool PythonFound;
            public string PythonVersion;
            public bool UvFound;
            public string UvVersion;
            public bool UvxFound;
            public bool AllSatisfied => PythonFound && UvxFound;
        }

        public static DependencyStatus Check()
        {
            var status = new DependencyStatus();

            string pythonCmd = GetPythonCommand();
            var (pythonOk, pythonOut) = RunCommand(pythonCmd, "--version");
            status.PythonFound = pythonOk;
            if (pythonOk)
                status.PythonVersion = ParseVersion(pythonOut);

            var (uvOk, uvOut) = RunCommand("uv", "--version");
            status.UvFound = uvOk;
            if (uvOk)
                status.UvVersion = ParseVersion(uvOut);

            var (uvxOk, _) = RunCommand("uvx", "--version");
            status.UvxFound = uvxOk;

            return status;
        }

        private static string GetPythonCommand()
        {
#if UNITY_EDITOR_WIN
            return "python";
#else
            return "python3";
#endif
        }

        internal static (bool success, string output) RunCommand(string command, string args)
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
                using var process = Process.Start(psi);
                if (process == null) return (false, null);
                string output = process.StandardOutput.ReadToEnd().Trim();
                if (string.IsNullOrEmpty(output))
                    output = process.StandardError.ReadToEnd().Trim();
                process.WaitForExit(5000);
                return (process.ExitCode == 0, output);
            }
            catch
            {
                return (false, null);
            }
        }

        internal static string ParseVersion(string output)
        {
            if (string.IsNullOrEmpty(output)) return null;
            var match = Regex.Match(output, @"(\d+\.\d+[\.\d]*)");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
