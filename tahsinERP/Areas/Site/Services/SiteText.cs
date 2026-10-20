using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace tahsinERP.Areas.Site.Services
{
    /// <summary>Slug va matn yordamchilari (sof funksiyalar — testlanadi).</summary>
    public static class SiteText
    {
        public const int SlugMaxLength = 100;

        private static readonly Dictionary<char, string> Cyrillic = new Dictionary<char, string>
        {
            { 'а', "a" }, { 'б', "b" }, { 'в', "v" }, { 'г', "g" }, { 'д', "d" }, { 'е', "e" }, { 'ё', "yo" }, { 'ж', "zh" },
            { 'з', "z" }, { 'и', "i" }, { 'й', "y" }, { 'к', "k" }, { 'л', "l" }, { 'м', "m" }, { 'н', "n" }, { 'о', "o" },
            { 'п', "p" }, { 'р', "r" }, { 'с', "s" }, { 'т', "t" }, { 'у', "u" }, { 'ф', "f" }, { 'х', "h" }, { 'ц', "ts" },
            { 'ч', "ch" }, { 'ш', "sh" }, { 'щ', "sch" }, { 'ъ', "" }, { 'ы', "y" }, { 'ь', "" }, { 'э', "e" }, { 'ю', "yu" },
            { 'я', "ya" }, { 'ў', "o" }, { 'қ', "q" }, { 'ғ', "g" }, { 'ҳ', "h" }
        };

        /// <summary>"Sement M400 (50 kg)" → "sement-m400-50-kg"; kirill transliteratsiya; o'zbekcha apostrof tashlanadi.</summary>
        public static string Slugify(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var sb = new StringBuilder();
            foreach (var raw in text.Trim().ToLowerInvariant())
            {
                if (Cyrillic.TryGetValue(raw, out var lat)) sb.Append(lat);
                else if (raw >= 'a' && raw <= 'z' || raw >= '0' && raw <= '9') sb.Append(raw);
                else if (raw == '\'' || raw == '‘' || raw == '’' || raw == 'ʻ' || raw == 'ʼ' || raw == '`') continue;
                else sb.Append('-');
            }
            var parts = sb.ToString().Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            var slug = string.Join("-", parts);
            if (slug.Length > SlugMaxLength) slug = slug.Substring(0, SlugMaxLength).TrimEnd('-');
            return slug;
        }

        /// <summary>Band bo'lmagan slug: asos, keyin asos-2, asos-3...</summary>
        public static string Unique(string baseSlug, Func<string, bool> isTaken, string fallback)
        {
            var slug = string.IsNullOrEmpty(baseSlug) ? fallback : baseSlug;
            if (!isTaken(slug)) return slug;
            for (int i = 2; i < 1000; i++)
            {
                var candidate = slug + "-" + i;
                if (!isTaken(candidate)) return candidate;
            }
            return slug + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        /// <summary>Foydalanuvchi kiritgan slug: faqat a-z, 0-9, '-'.</summary>
        public static bool IsValidSlug(string slug) =>
            !string.IsNullOrEmpty(slug) && slug.Length <= SlugMaxLength + 20 && slug.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-')
            && !slug.StartsWith("-") && !slug.EndsWith("-") && !slug.Contains("--");

        public static string Clean(string s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length <= max ? s : s.Substring(0, max);
        }

        /// <summary>"#1A2B3C" ko'rinishidagi rang.</summary>
        public static bool IsColor(string s) =>
            s != null && s.Length == 7 && s[0] == '#' && s.Skip(1).All(Uri.IsHexDigit);

        /// <summary>"NEW,SALE" — faqat ma'lum belgilar, takrorsiz.</summary>
        public static string NormalizeBadges(IEnumerable<string> badges) =>
            badges == null ? null : string.Join(",", badges.Select(b => (b ?? "").Trim().ToUpperInvariant())
                .Where(b => Data.ProductBadges.All.Contains(b)).Distinct());

        /// <summary>Domen nomi (kichik harf, port va yo'lsiz).</summary>
        public static string NormalizeHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return null;
            var h = host.Trim().ToLowerInvariant();
            if (h.StartsWith("http://")) h = h.Substring(7);
            if (h.StartsWith("https://")) h = h.Substring(8);
            h = h.TrimEnd('/');
            if (h.Length == 0 || h.Length > 253 || !h.Contains('.') || h.StartsWith(".") || h.EndsWith(".")) return null;
            if (!h.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-' || c == '.')) return null;
            if (h.Split('.').Any(label => label.Length == 0 || label.Length > 63 || label.StartsWith("-") || label.EndsWith("-"))) return null;
            return h;
        }
    }
}
