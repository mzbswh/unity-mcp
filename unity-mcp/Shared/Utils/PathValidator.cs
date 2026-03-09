using System;
using System.IO;

namespace UnityMcp.Shared.Utils
{
    /// <summary>
    /// Validates that file/folder paths stay within the Unity project Assets directory.
    /// Prevents path traversal attacks (e.g. ../../etc/passwd).
    /// </summary>
    public static class PathValidator
    {
        /// <summary>
        /// Validates that a path is under the Assets/ directory (or Packages/).
        /// Returns normalized path on success, or error message on failure.
        /// </summary>
        public static (bool IsValid, string NormalizedPath, string Error) ValidateAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return (false, null, "Path cannot be empty");

            // Normalize separators and resolve ../ sequences
            string normalized;
            try
            {
                normalized = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                return (false, null, $"Invalid path: {ex.Message}");
            }

            // Get the project's Assets directory as the allowed root
            // In Unity, paths should start with "Assets/" or "Packages/"
            string projectRoot = Path.GetFullPath(".");
            string assetsRoot = Path.Combine(projectRoot, "Assets");
            string packagesRoot = Path.Combine(projectRoot, "Packages");

            // Ensure the resolved path is under Assets/ or Packages/
            if (!normalized.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase) &&
                !normalized.StartsWith(packagesRoot, StringComparison.OrdinalIgnoreCase))
            {
                return (false, null, $"Path must be under Assets/ or Packages/. Got: {path}");
            }

            return (true, normalized, null);
        }

        /// <summary>
        /// Quick check: does the path start with "Assets/" or "Packages/" and contain no ".." segments?
        /// Lightweight alternative for cases where full path resolution isn't needed.
        /// </summary>
        public static (bool IsValid, string Error) QuickValidate(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return (false, "Path cannot be empty");

            // Must start with Assets/ or Packages/
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) &&
                !path.StartsWith("Assets\\", StringComparison.Ordinal) &&
                !path.StartsWith("Packages/", StringComparison.Ordinal) &&
                !path.StartsWith("Packages\\", StringComparison.Ordinal) &&
                path != "Assets" && path != "Packages")
            {
                return (false, $"Path must start with 'Assets/' or 'Packages/'. Got: {path}");
            }

            // Reject path traversal sequences
            if (path.Contains(".."))
                return (false, "Path cannot contain '..' traversal sequences");

            return (true, null);
        }
    }
}
