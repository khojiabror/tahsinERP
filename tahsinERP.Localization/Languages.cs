using System;
using System.Collections.Generic;
using System.Globalization;

namespace tahsinERP.Localization
{
    /// <summary>
    /// Tizim tillari. Kodlar bazada saqlanadi (dst.USER_SETTINGS.Language, dst.SETTINGS DefaultLanguage, cookie):
    /// "uz" — o'zbek lotin (asosiy va zaxira til), "oz" — o'zbek kirill, "ru" — rus.
    /// Kirill matnlar alohida tarjima qilinmaydi: lotin matndan <see cref="UzTransliterator"/> bilan olinadi
    /// (kerak bo'lsa .uz-Cyrl.resx bilan alohida kalit ustidan yoziladi).
    /// </summary>
    public static class Languages
    {
        public const string Uzbek = "uz";
        public const string UzbekCyrillic = "oz";
        public const string Russian = "ru";

        /// <summary>Zaxira til: tarjimasi yo'q kalit shu tilda ko'rsatiladi.</summary>
        public const string Default = Uzbek;

        /// <summary>Tanlagichdagi tartib.</summary>
        public static readonly IReadOnlyList<string> All = new[] { Uzbek, UzbekCyrillic, Russian };

        private static readonly CultureInfo UzCulture = CultureInfo.GetCultureInfo("uz-Latn-UZ");
        private static readonly CultureInfo OzCulture = CultureInfo.GetCultureInfo("uz-Cyrl-UZ");
        private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

        /// <summary>Ma'lum til kodi bo'lsa — uning kanonik shakli, aks holda null ("ru-RU", "uz-Cyrl", "UZ" ham tushuniladi).</summary>
        public static string TryNormalize(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) return null;
            var s = language.Trim().ToLowerInvariant();
            if (s == Uzbek || s == "uz-latn" || s == "uz-latn-uz" || s == "uz-uz") return Uzbek;
            if (s == UzbekCyrillic || s == "uz-cyrl" || s == "uz-cyrl-uz" || s == "cyrl") return UzbekCyrillic;
            if (s == Russian || s.StartsWith("ru-", StringComparison.Ordinal)) return Russian;
            return null;
        }

        /// <summary>Kanonik kod; noma'lum yoki bo'sh bo'lsa — <see cref="Default"/>.</summary>
        public static string Normalize(string language)
        {
            return TryNormalize(language) ?? Default;
        }

        public static bool IsSupported(string language)
        {
            return TryNormalize(language) != null;
        }

        /// <summary>Tilning UI madaniyati (CurrentUICulture uchun).</summary>
        public static CultureInfo CultureFor(string language)
        {
            switch (Normalize(language))
            {
                case UzbekCyrillic: return OzCulture;
                case Russian: return RuCulture;
                default: return UzCulture;
            }
        }

        /// <summary>Madaniyatdan til kodi (CurrentUICulture -> "uz"/"oz"/"ru").</summary>
        public static string FromCulture(CultureInfo culture)
        {
            if (culture == null) return Default;
            if (culture.TwoLetterISOLanguageName == "ru") return Russian;
            if (culture.TwoLetterISOLanguageName == "uz")
                return culture.Name.IndexOf("Cyrl", StringComparison.OrdinalIgnoreCase) >= 0 ? UzbekCyrillic : Uzbek;
            return Default;
        }

        /// <summary>Joriy so'rov tili (CurrentUICulture bo'yicha).</summary>
        public static string Current => FromCulture(CultureInfo.CurrentUICulture);

        /// <summary>Tanlagichda ko'rsatiladigan nom (o'z tilida).</summary>
        public static string NativeName(string language)
        {
            switch (Normalize(language))
            {
                case UzbekCyrillic: return "Ўзбекча";
                case Russian: return "Русский";
                default: return "O'zbekcha";
            }
        }

        /// <summary>Qisqa belgi (sarlavhadagi tugma uchun).</summary>
        public static string ShortName(string language)
        {
            switch (Normalize(language))
            {
                case UzbekCyrillic: return "ЎЗ";
                case Russian: return "RU";
                default: return "UZ";
            }
        }

        /// <summary>HTML lang atributi.</summary>
        public static string HtmlLang(string language)
        {
            switch (Normalize(language))
            {
                case UzbekCyrillic: return "uz-Cyrl";
                case Russian: return "ru";
                default: return "uz";
            }
        }
    }
}
