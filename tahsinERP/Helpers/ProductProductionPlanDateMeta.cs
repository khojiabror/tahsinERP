using System;
using System.Text.RegularExpressions;
using tahsinERP.Models;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// Ishlab chiqarishning REJA kuni. Manba — PRODUCT_PRODUCTION.PlanDate ustuni (audit MES-B20).
    /// Avval reja kuni Comment ichidagi "[RejaSana:yyyy-MM-dd]" matnida saqlanardi. Migratsiya
    /// (Database_Migrations/MES_2026_10_PlanDate_ProductionID.sql) eski yozuvlarni PlanDate ga ko'chiradi.
    /// Matn faqat zaxira sifatida o'qiladi va saqlashda Comment'dan olib tashlanadi.
    /// Yagona qoida (ProductPlan, Dashboard, Hisobotlar): reja kuni = PlanDate ?? Date.
    /// Sifat nazorati esa faqat PRODUCT_PRODUCTION fakt sanasi va Total bo'yicha ishlaydi (reja limiti yo'q).
    /// </summary>
    public static class ProductProductionPlanDateMeta
    {
        private static readonly Regex PlanDateRegex = new Regex(
            @"\[RejaSana:(?<d>\d{4}-\d{2}-\d{2})\]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Eski izoh formatidan reja kunini o'qiydi (faqat zaxira).</summary>
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

        /// <summary>Izohdan eski "[RejaSana:...]" metasini olib tashlaydi (reja kuni endi alohida ustunda).</summary>
        public static string StripMeta(string userComment)
        {
            var stripped = PlanDateRegex.Replace(userComment ?? "", "").Trim();
            return string.IsNullOrEmpty(stripped) ? null : stripped;
        }

        /// <summary>Reja kuni: PlanDate, bo'lmasa eski izoh metasi, bo'lmasa fakt sanasi.</summary>
        public static DateTime PlanDay(PRODUCT_PRODUCTION p)
        {
            if (p == null)
            {
                return default;
            }

            return (p.PlanDate ?? TryParsePlanDate(p.Comment) ?? p.Date).Date;
        }

        /// <summary>
        /// Kalendar kuni: fakt sanasi shu kunda yoki reja kuni shu kunda.
        /// </summary>
        public static bool MatchesCalendarDay(PRODUCT_PRODUCTION p, DateTime calendarDay)
        {
            if (p == null)
            {
                return false;
            }

            var d = calendarDay.Date;
            return p.Date.Date == d || PlanDay(p) == d;
        }
    }
}
