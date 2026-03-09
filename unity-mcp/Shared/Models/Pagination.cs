namespace UnityMcp.Shared.Models
{
    /// <summary>
    /// Pagination state for cursor-based result paging.
    /// Used by tools that return large result sets (e.g. scene_get_hierarchy).
    /// </summary>
    public class Pagination
    {
        /// <summary>Current page size</summary>
        public int PageSize { get; set; } = 50;

        /// <summary>Opaque cursor for the next page (null if no more pages)</summary>
        public string Cursor { get; set; }

        /// <summary>Total number of items (if known, -1 otherwise)</summary>
        public int Total { get; set; } = -1;

        /// <summary>Whether there are more pages available</summary>
        public bool HasMore => !string.IsNullOrEmpty(Cursor);

        /// <summary>Parse a cursor string to an integer offset, defaulting to 0</summary>
        public static int ParseCursor(string cursor, int defaultValue = 0)
        {
            if (string.IsNullOrEmpty(cursor)) return defaultValue;
            return int.TryParse(cursor, out int val) ? val : defaultValue;
        }

        /// <summary>Create the next cursor string from an offset, or null if done</summary>
        public static string NextCursor(int offset, int pageSize, int total)
        {
            int next = offset + pageSize;
            return next < total ? next.ToString() : null;
        }
    }
}
