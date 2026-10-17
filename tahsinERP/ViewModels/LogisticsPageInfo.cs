using System;
using System.Web.Routing;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// Logistika ro'yxatlari uchun server tomonidagi sahifalash (LOG-P02, LOG-P09):
    /// avval butun jadval brauzerga yuborilib, DataTables mijoz tomonida sahifalardi.
    /// </summary>
    public class LogisticsPageInfo
    {
        public const int DefaultPageSize = 50;
        public const int MaxPageSize = 500;

        public LogisticsPageInfo(int? page, int? pageSize, int totalCount)
        {
            PageSize = Math.Min(Math.Max(pageSize ?? DefaultPageSize, 10), MaxPageSize);
            TotalCount = totalCount;
            TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
            Page = Math.Min(Math.Max(page ?? 1, 1), TotalPages);
        }

        public int Page { get; }
        public int PageSize { get; }
        public int TotalCount { get; }
        public int TotalPages { get; }
        public int Skip => (Page - 1) * PageSize;

        /// <summary>Pager havolalari uchun joriy filtr qiymatlari (page'dan tashqari).</summary>
        public RouteValueDictionary RouteValues { get; set; } = new RouteValueDictionary();
    }
}
