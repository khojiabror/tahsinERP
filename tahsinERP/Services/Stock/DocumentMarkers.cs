using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace tahsinERP.Services.Stock
{
    /// <summary>
    /// Hujjatlar orasidagi matnli bog'lanish markerlari (FK ustunlari kiritilgunga qadar).
    ///
    /// Muammo (SRM-B06): <c>Description LIKE '%EXPENSE_ID:12%'</c> 120, 121, 1200... ga ham mos kelardi,
    /// natijada boshqa hujjat yangilanar yoki o'chirilardi. Endi SQL'da keng (LIKE) filtr bilan nomzodlar
    /// olinadi va xotirada <see cref="Matches"/> bilan aniq tekshiriladi: marker + ID dan keyin raqam
    /// kelmasligi shart. Sxema o'zgarmaydi; eski yozuvlar ham to'g'ri o'qiladi.
    /// </summary>
    public static class DocumentMarkers
    {
        /// <summary>Chiqimdan avtomatik yaratilgan kirim izohida: "... | EXPENSE_ID:123".</summary>
        public const string LinkedExpenseId = "EXPENSE_ID:";

        /// <summary>Lot tanlangan chiqim qatori izohida: "IncomeID:45".</summary>
        public const string IncomeId = "IncomeID:";

        /// <summary>Sotuv invoysi bo'yicha avtomatik chiqim izohida: ".../SalesInvoices/Details/77".</summary>
        public const string SalesInvoiceUrl = "/SalesInvoices/Details/";

        /// <summary>Marker matni (masalan "EXPENSE_ID:12") — SQL'dagi keng LIKE filtri uchun.</summary>
        public static string For(string marker, long id)
        {
            return marker + id;
        }

        /// <summary>
        /// <paramref name="text"/> ichida aynan <c>marker + id</c> bormi (keyingi belgi raqam emas).
        /// "EXPENSE_ID:12" → "x | EXPENSE_ID:12" = true, "EXPENSE_ID:120" = false.
        /// </summary>
        public static bool Matches(string text, string marker, long id)
        {
            if (string.IsNullOrEmpty(text) || id <= 0)
                return false;

            var token = marker + id;
            int idx = 0;
            while ((idx = text.IndexOf(token, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                int after = idx + token.Length;
                if (after >= text.Length || !char.IsDigit(text[after]))
                    return true;
                idx = after;
            }
            return false;
        }

        /// <summary>
        /// "NOM:123" ko'rinishidagi marker'dan keyingi birinchi ID (topilmasa null). Raqam to'liq o'qiladi.
        /// Faqat ":" bilan tugaydigan markerlar uchun (<see cref="LinkedExpenseId"/>, <see cref="IncomeId"/>).
        /// </summary>
        public static int? Parse(string text, string marker)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var m = Regex.Match(text, Regex.Escape(marker.TrimEnd(':')) + @"\s*:\s*(\d+)", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var id))
                return id;
            return null;
        }

        /// <summary>Nomzodlar ichidan faqat aniq moslarini qoldiradi.</summary>
        public static IEnumerable<T> Exact<T>(IEnumerable<T> candidates, Func<T, string> text, string marker, long id)
        {
            return candidates.Where(c => Matches(text(c), marker, id));
        }
    }
}
