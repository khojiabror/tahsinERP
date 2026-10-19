using System.Globalization;

namespace tahsinERP.Services.Crm
{
    /// <summary>
    /// Formadan kelgan sonni madaniyatdan qat'i nazar o'qish (CRM-B19).
    /// &lt;input type="number"&gt; nuqta bilan yuboradi ("12.5"), sayt madaniyati esa uz-UZ (vergul) —
    /// avval double.Parse joriy madaniyatda "12.5" ni 125 yoki xato deb o'qirdi.
    /// Avval invariant (faqat nuqta, minglik ajratgichsiz), keyin joriy madaniyat sinanadi.
    /// </summary>
    public static class CrmNumber
    {
        private const NumberStyles Styles = NumberStyles.Float;

        public static bool TryDouble(string raw, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            raw = raw.Trim().Replace(" ", "").Replace(" ", "");
            return double.TryParse(raw, Styles, CultureInfo.InvariantCulture, out value)
                || double.TryParse(raw, Styles, CultureInfo.CurrentCulture, out value);
        }

        public static bool TryDecimal(string raw, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            raw = raw.Trim().Replace(" ", "").Replace(" ", "");
            return decimal.TryParse(raw, Styles, CultureInfo.InvariantCulture, out value)
                || decimal.TryParse(raw, Styles, CultureInfo.CurrentCulture, out value);
        }

        public static decimal Decimal(string raw, decimal fallback = 0)
        {
            decimal v;
            return TryDecimal(raw, out v) ? v : fallback;
        }

        /// <summary>HTML &lt;input type="number"&gt; value atributi uchun (nuqta bilan, guruhlashsiz).</summary>
        public static string ForInput(double value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        public static string ForInput(decimal value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
