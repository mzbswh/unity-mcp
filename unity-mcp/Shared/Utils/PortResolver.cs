using System.Security.Cryptography;
using System.Text;

namespace UnityMcp.Shared.Utils
{
    /// <summary>
    /// Resolves the TCP port for MCP communication.
    /// Default is a well-known port (51279) so MCP clients work without extra configuration.
    /// For multi-instance setups, generates a deterministic port (50000-59999) from project path hash.
    /// </summary>
    public static class PortResolver
    {
        public const int DefaultPort = 51279;
        private const int PortRangeStart = 50000;
        private const int PortRangeSize = 10000;

        /// <summary>
        /// Generates a deterministic port from project path hash.
        /// Used for multi-instance scenarios where each Unity Editor needs a unique port.
        /// </summary>
        public static int GetPort(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath))
                return DefaultPort;

            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(projectPath));

            uint value = (uint)(hash[0] << 24 | hash[1] << 16 | hash[2] << 8 | hash[3]);
            return (int)(value % PortRangeSize) + PortRangeStart;
        }
    }
}
