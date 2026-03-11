using System;
using System.Collections.Generic;
using System.Linq;
using UnityMcp.Shared.Models;

namespace UnityMcp.Shared.Utils
{
    public static class PaginationHelper
    {
        public const int DefaultPageSize = 50;
        public const int MaxPageSize = 200;

        /// <summary>
        /// Paginate a list of items using cursor-based pagination.
        /// Returns the page items, total count, and a nextCursor (null if no more pages).
        /// </summary>
        public static (List<T> items, int total, string nextCursor) Paginate<T>(
            IList<T> allItems, int pageSize = DefaultPageSize, string cursor = null)
        {
            int start = Pagination.ParseCursor(cursor);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
            int total = allItems.Count;
            var page = allItems.Skip(start).Take(pageSize).ToList();
            string next = Pagination.NextCursor(start, pageSize, total);
            return (page, total, next);
        }

        /// <summary>
        /// Create a paginated ToolResult from a list of items.
        /// </summary>
        public static ToolResult ToPaginatedResult<T>(
            IList<T> allItems, int pageSize = DefaultPageSize, string cursor = null)
        {
            var (items, total, next) = Paginate(allItems, pageSize, cursor);
            return ToolResult.Paginated(items, total, next);
        }
    }
}
