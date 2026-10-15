using System;
using System.Text.RegularExpressions;
using tahsinERP.Models;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// Kunlik ishlab chiqarishda reja sanasi va fakt sanasi ajralganda, reja kunini saqlash uchun Comment maydonidagi meta.
    /// Ishlab chiqarish kiritish/reja yuklash: reja kuni va fakt sanasi ajratilganda Comment orqali bog'lanadi.
    /// Sifat nazorati esa faqat PRODUCT_PRODUCTION fakt sanasi va Total bo'yicha ishlaydi (reja limiti yo'q).
    /// </summary>
    public static class ProductProductionPlanDateMeta
    {
        private static readonly Regex PlanDateRegex = new Regex(
            @"\[RejaSana:(?<d>\d{4}-\d{2}-\d{2})\]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static DateTime? TryParsePlanDate(string comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                return null;
            }

            var m = PlanDateRegex.Match(comment);
            if (!m.Success)
            {
                return null;
            }

            if (DateTime.TryParseExact(
                    m.Groups["d"].Value,
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var dt))
            {
                return dt.Date;
            }

            return null;
        }

        /// <summary>
        /// Eski [RejaSana:...] ni olib tashlab, yangisini qo'shadi.
        /// </summary>
        public static string MergeIntoComment(string userComment, DateTime planDate)
        {
            var stripped = PlanDateRegex.Replace(userComment ?? "", "").Trim();
            var meta = $"[RejaSana:{planDate:yyyy-MM-dd}]";
            if (string.IsNullOrEmpty(stripped))
            {
                return meta;
            }

            return (stripped + " " + meta).Trim();
        }

        /// <summary>
        /// Kalendar kuni: fakt sanasi shu kunda yoki reja meta shu kunda (yoki ikkalasi bir xil — eski yozuvlar).
        /// </summary>
        public static bool MatchesCalendarDay(PRODUCT_PRODUCTION p, DateTime calendarDay)
        {
            if (p == null)
            {
                return false;
            }

            var d = calendarDay.Date;
            if (p.Date.Date == d)
            {
                return true;
            }

            var plan = TryParsePlanDate(p.Comment);
            return plan.HasValue && plan.Value.Date == d;
        }

        /// <summary>
        /// Hisobot/reja oyi uchun: [RejaSana:...] bo'lsa shu kun, bo'lmasa fakt sanasi.
        /// </summary>
        public static DateTime GetAttributionCalendarDate(PRODUCT_PRODUCTION p)
        {
            if (p == null)
            {
                return default;
            }

            var plan = TryParsePlanDate(p.Comment);
            return plan ?? p.Date.Date;
        }

        /// <summary>
        /// Tanlangan yil/oydagi reja yoki fakt (attribution) bo'yicha ishlab chiqarish qatorini o'z ichiga oladi.
        /// </summary>
        public static bool BelongsToCalendarMonth(PRODUCT_PRODUCTION p, int year, int month)
        {
            var d = GetAttributionCalendarDate(p);
            return d.Year == year && d.Month == month;
        }

        /// <summary>
        /// Kun oralig'i (inclusive, faqat sana qismi).
        /// </summary>
        public static bool BelongsToInclusiveCalendarDateRange(PRODUCT_PRODUCTION p, DateTime rangeStart, DateTime rangeEnd)
        {
            var d = GetAttributionCalendarDate(p);
            return d >= rangeStart.Date && d <= rangeEnd.Date;
        }
    }
}
