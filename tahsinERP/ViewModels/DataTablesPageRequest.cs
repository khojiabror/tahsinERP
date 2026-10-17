using System;
using System.Linq;
using System.Web;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// DataTables server-side so'rovi (draw, start, length, search[value], order[0][column|dir]).
    /// Ro'yxat sahifalari butun jadvalni brauzerga yuklash o'rniga faqat joriy sahifani SQL'da oladi.
    /// </summary>
    public sealed class DataTablesPageRequest
    {
        /// <summary>Bitta sahifadagi maksimal qatorlar ("Barchasi" tanlansa ham).</summary>
        public const int MaxLength = 500;

        public int Draw { get; private set; }
        public int Start { get; private set; }
        public int Length { get; private set; }
        public string Search { get; private set; }
        public int OrderColumn { get; private set; }
        public bool OrderDescending { get; private set; }

        public static DataTablesPageRequest Parse(HttpRequestBase request, int defaultOrderColumn, bool defaultDescending = true)
        {
            int draw, start, length, orderColumn;
            int.TryParse(request["draw"], out draw);
            int.TryParse(request["start"], out start);
            if (!int.TryParse(request["length"], out length) || length <= 0 || length > MaxLength)
                length = length == 0 ? 25 : MaxLength;

            var dir = request["order[0][dir]"];
            bool hasOrder = int.TryParse(request["order[0][column]"], out orderColumn);

            return new DataTablesPageRequest
            {
                Draw = draw,
                Start = Math.Max(0, start),
                Length = length,
                Search = string.IsNullOrWhiteSpace(request["search[value]"]) ? null : request["search[value]"].Trim(),
                OrderColumn = hasOrder ? orderColumn : defaultOrderColumn,
                OrderDescending = hasOrder ? string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase) : defaultDescending
            };
        }

        /// <summary>
        /// Sana filtri: birinchi ochilishda (parametr umuman yo'q) — oxirgi <paramref name="defaultDays"/> kun;
        /// foydalanuvchi maydonni bo'shatib yuborsa — filtrsiz.
        /// </summary>
        public static DateTime? DefaultFromDate(HttpRequestBase request, DateTime? fromDate, int defaultDays = 30)
        {
            if (fromDate.HasValue)
                return fromDate.Value.Date;
            bool explicitlyEmpty = request.QueryString.AllKeys.Any(k => string.Equals(k, "fromDate", StringComparison.OrdinalIgnoreCase));
            return explicitlyEmpty ? (DateTime?)null : DateTime.Today.AddDays(-defaultDays);
        }
    }
}
