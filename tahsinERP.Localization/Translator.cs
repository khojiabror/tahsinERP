using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;

namespace tahsinERP.Localization
{
    /// <summary>
    /// Bir yoki bir nechta .resx to'plami ustidan tarjima. Zaxira zanjiri hech qachon yiqilmaydi:
    ///   ru: .ru.resx -> asosiy (o'zbek lotin) -> kalitning o'zi;
    ///   oz: .uz-Cyrl.resx (qo'lda tuzatilgan bo'lsa) -> asosiy matn transliteratsiyasi -> kalit;
    ///   uz: asosiy -> kalit.
    /// To'plamlar berilgan tartibda qidiriladi (birinchi topilgani). Formatlashdagi xato ({0} yo'q va h.k.)
    /// istisno bermaydi — formatlanmagan matn qaytadi.
    /// </summary>
    public sealed class Translator
    {
        private static readonly CultureInfo CyrlCulture = CultureInfo.GetCultureInfo("uz-Cyrl-UZ");
        private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

        private readonly ResourceManager[] _managers;

        public Translator(params ResourceManager[] managers)
        {
            if (managers == null || managers.Length == 0) throw new ArgumentException("Kamida bitta resurs to'plami kerak.", nameof(managers));
            _managers = managers;
        }

        /// <summary>Tizimning umumiy atamalari (Saqlash, Bekor qilish, ...) — har bir ilova o'z to'plamidan keyin qo'shadi.</summary>
        public static ResourceManager Common { get; } = new ResourceManager("tahsinERP.Localization.Resources.Common", typeof(Translator).Assembly);

        /// <summary>Joriy UI tilida (CurrentUICulture).</summary>
        public string T(string key) => Get(key, Languages.Current);

        public string T(string key, params object[] args) => Format(Languages.Current, Get(key, Languages.Current), args);

        /// <summary>Berilgan tilda (masalan, bildirishnoma qabul qiluvchining tilida).</summary>
        public string TFor(string language, string key, params object[] args) => Format(language, Get(key, language), args);

        /// <summary>Kalit mavjudmi (biror to'plamning asosiy tilida).</summary>
        public bool Has(string key)
        {
            return Neutral(key) != null;
        }

        public string Get(string key, string language)
        {
            if (string.IsNullOrEmpty(key)) return key ?? "";
            switch (Languages.Normalize(language))
            {
                case Languages.Russian:
                    foreach (var rm in _managers)
                    {
                        var s = Safe(rm, key, RuCulture); // satellite yo'q bo'lsa ResourceManager o'zi asosiyga tushadi
                        if (s != null) return s;
                    }
                    return key;
                case Languages.UzbekCyrillic:
                    foreach (var rm in _managers)
                    {
                        var exact = Exact(rm, key, CyrlCulture);
                        if (exact != null) return exact;
                    }
                    var latin = Neutral(key);
                    return latin == null ? key : UzTransliterator.ToCyrillicCached(latin);
                default:
                    return Neutral(key) ?? key;
            }
        }

        private string Neutral(string key)
        {
            foreach (var rm in _managers)
            {
                var s = Safe(rm, key, CultureInfo.InvariantCulture);
                if (s != null) return s;
            }
            return null;
        }

        private static string Safe(ResourceManager rm, string key, CultureInfo culture)
        {
            try { return rm.GetString(key, culture); }
            catch (MissingManifestResourceException) { return null; }
        }

        /// <summary>Faqat shu madaniyatning o'z satellite'idan (ota-onasiga tushmasdan).</summary>
        private static string Exact(ResourceManager rm, string key, CultureInfo culture)
        {
            try { return rm.GetResourceSet(culture, true, false)?.GetString(key); }
            catch (MissingManifestResourceException) { return null; }
            catch (MissingSatelliteAssemblyException) { return null; }
        }

        public static string Format(string language, string text, object[] args)
        {
            if (args == null || args.Length == 0 || string.IsNullOrEmpty(text)) return text;
            try { return string.Format(DisplayCultures.For(language), text, args); }
            catch (FormatException) { return text; }
        }

        /// <summary>Prefiks bilan boshlanadigan barcha kalitlar (JS to'plami uchun: "Js_", "Dt_").</summary>
        public IReadOnlyList<string> Keys(string prefix)
        {
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var rm in _managers)
            {
                ResourceSet set;
                try { set = rm.GetResourceSet(CultureInfo.InvariantCulture, true, true); }
                catch (MissingManifestResourceException) { continue; }
                if (set == null) continue;
                foreach (System.Collections.DictionaryEntry e in set)
                    if (e.Key is string k && k.StartsWith(prefix, StringComparison.Ordinal)) keys.Add(k);
            }
            return new List<string>(keys);
        }

        /// <summary>Barcha kalitlar (testlar: har kalitning ruscha tarjimasi borligini tekshirish uchun).</summary>
        public static IEnumerable<KeyValuePair<string, string>> Entries(ResourceManager rm, CultureInfo culture)
        {
            var set = rm.GetResourceSet(culture, true, false);
            if (set == null) yield break;
            foreach (System.Collections.DictionaryEntry e in set)
                if (e.Value is string s) yield return new KeyValuePair<string, string>((string)e.Key, s);
        }
    }
}
