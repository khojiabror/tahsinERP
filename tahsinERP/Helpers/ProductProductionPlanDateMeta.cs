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
        /// "[RejaSana:yyyy-MM-dd]" — SQL tomonda <c>Comment.Contains(...)</c> bilan qidirish uchun ham ishlatiladi.
        /// </summary>
        public static string FormatMeta(DateTime planDate)
        {
            return "[RejaSana:" + planDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + "]";
        }

        /// <summary>
        /// Eski [RejaSana:...] ni olib tashlab, yangisini qo'shadi.
        /// </summary>
        public static string MergeIntoComment(string userComment, DateTime planDate)
        {
            var stripped = PlanDateRegex.Replace(userComment ?? "", "").Trim();
            var meta = FormatMeta(planDate);
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
    }
}
