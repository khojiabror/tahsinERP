using System.Collections.Concurrent;
using System.Globalization;

namespace tahsinERP.Localization
{
    /// <summary>
    /// Tilga mos sana/son madaniyati. Qisqa sana/vaqt shakli, o'nlik va guruh ajratkichlari ASOS madaniyatdan
    /// olinadi (formalardan kelgan qiymatlar til almashganda ham bir xil o'qilsin — model binding buzilmaydi);
    /// tilga qarab o'zgaradi: oy va kun nomlari, uzun sana shakli, valyuta belgisi (so'm / сўм / сум).
    /// </summary>
    public static class DisplayCultures
    {
        private static readonly ConcurrentDictionary<string, CultureInfo> Cache = new ConcurrentDictionary<string, CultureInfo>();

        /// <summary>Asos — joriy CurrentCulture (ilova sozlamasi).</summary>
        public static CultureInfo For(string language) => For(language, CultureInfo.CurrentCulture);

        public static CultureInfo For(string language, CultureInfo baseCulture)
        {
            language = Languages.Normalize(language);
            var key = language + "|" + baseCulture.Name + "|" + baseCulture.NumberFormat.NumberDecimalSeparator + baseCulture.DateTimeFormat.ShortDatePattern;
            return Cache.GetOrAdd(key, _ => Create(language, baseCulture));
        }

        /// <summary>Valyuta nomi (so'm) — narx yozuvlari uchun.</summary>
        public static string Sum(string language)
        {
            switch (Languages.Normalize(language))
            {
                case Languages.UzbekCyrillic: return "сўм";
                case Languages.Russian: return "сум";
                default: return "so'm";
            }
        }

        private static CultureInfo Create(string language, CultureInfo baseCulture)
        {
            var c = (CultureInfo)baseCulture.Clone();
            var src = Languages.CultureFor(language).DateTimeFormat;
            var d = c.DateTimeFormat;
            d.MonthNames = src.MonthNames;
            d.MonthGenitiveNames = src.MonthGenitiveNames;
            d.AbbreviatedMonthNames = src.AbbreviatedMonthNames;
            d.AbbreviatedMonthGenitiveNames = src.AbbreviatedMonthGenitiveNames;
            d.DayNames = src.DayNames;
            d.AbbreviatedDayNames = src.AbbreviatedDayNames;
            d.ShortestDayNames = src.ShortestDayNames;
            d.LongDatePattern = src.LongDatePattern;
            d.MonthDayPattern = src.MonthDayPattern;
            d.YearMonthPattern = src.YearMonthPattern;
            d.AMDesignator = src.AMDesignator;
            d.PMDesignator = src.PMDesignator;
            c.NumberFormat.CurrencySymbol = Sum(language);
            return CultureInfo.ReadOnly(c);
        }
    }
}
