using System.Security.Cryptography;
using System.Text;

namespace UnityMcp.Shared.Utils
{
    /// <summary>
    /// Generates a deterministic port (50000-59999) from a project path via SHA256 hash.
    /// </summary>
    public static class PortResolver
    {
        private const int PortRangeStart = 50000;
        private const int PortRangeSize = 10000;

        public static int GetPort(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath))
                return PortRangeStart;

            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(projectPath));

            // Use first 4 bytes as unsigned int, map to port range
            uint value = (uint)(hash[0] << 24 | hash[1] << 16 | hash[2] << 8 | hash[3]);
            return (int)(value % PortRangeSize) + PortRangeStart;
        }
    }
}
