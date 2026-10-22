using System;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Runtime.Caching;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Configuration.Tenancy;
using tahsinERP.Localization;

namespace tahsinERP.Areas.Distribution.Services
{
    /// <summary>
    /// Distributsiya modulining matnlari: Areas/Distribution/Resources/DstStrings.resx (o'zbek, asosiy)
    /// va DstStrings.ru.resx; kirill — tahsinERP.Localization transliteratsiyasi. Til so'rov boshida
    /// tahsinERP.I18n.UiLanguage tomonidan qo'yiladi (butun ERP uchun bitta mexanizm).
    /// </summary>
    public static class DstLocalization
    {
        public const string Uzbek = Languages.Uzbek;
        public const string UzbekCyrillic = Languages.UzbekCyrillic;
        public const string Russian = Languages.Russian;

        private static readonly ResourceManager Resources = new ResourceManager(
            "tahsinERP.Areas.Distribution.Resources.DstStrings", typeof(DstLocalization).Assembly);

        // Distributsiya kalitlari birinchi, keyin ERP va umumiy atamalar (tahsinERP.I18n.L — teskari tartibda)
        private static readonly Translator Translator = new Translator(Resources,
            new ResourceManager("tahsinERP.Resources.ErpStrings", typeof(DstLocalization).Assembly), Translator.Common);

        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Distributsiya so'rovlari uchun CurrentCulture: ilovaning uz-UZ madaniyati, lekin o'nlik ajratkich "."
        /// (brauzer &lt;input type=number&gt; va JSON shunday yuboradi; model binding shu madaniyatda ishlaydi).
        /// </summary>
        public static readonly CultureInfo FormCulture = CreateFormCulture();

        private static CultureInfo CreateFormCulture()
        {
            var c = (CultureInfo)CultureInfo.GetCultureInfo("uz-UZ").Clone();
            c.NumberFormat.NumberDecimalSeparator = ".";
            c.NumberFormat.CurrencyDecimalSeparator = ".";
            c.NumberFormat.PercentDecimalSeparator = ".";
            c.NumberFormat.NumberGroupSeparator = " ";
            return CultureInfo.ReadOnly(c);
        }

        public static ResourceManager ResourceManager => Resources;

        /// <summary>Kalit bo'yicha matn (joriy UI tilida); topilmasa — zaxira til, u ham bo'lmasa — kalitning o'zi.</summary>
        public static string T(string key) => Translator.T(key);

        public static string T(string key, params object[] args) => Translator.T(key, args);

        /// <summary>Berilgan tilda (masalan, bildirishnoma qabul qiluvchining tilida).</summary>
        public static string TFor(string language, string key, params object[] args) => Translator.TFor(language, key, args);

        public static CultureInfo CultureFor(string language) => Languages.CultureFor(language);

        public static string Normalize(string language) => Languages.Normalize(language);

        /// <summary>Ruscha (ikki tilli nomlar uchun: NameRu to'ldirilgan bo'lsa).</summary>
        public static bool IsRussian(string language) => Languages.Normalize(language) == Russian;

        /// <summary>Foydalanuvchi tili: o'zi tanlagan, aks holda firma standart tili.</summary>
        public static string LanguageOf(int userId)
        {
            return StoredLanguageOf(userId) ?? tahsinERP.I18n.UiLanguage.FirmDefault();
        }

        /// <summary>Foydalanuvchi tanlagan til (dst.USER_SETTINGS, 10 daqiqa kesh); tanlamagan bo'lsa — null.</summary>
        public static string StoredLanguageOf(int userId)
        {
            string key = "dst.lang:" + (TenantContext.CurrentOrNull?.Slug ?? "") + ":" + userId;
            if (MemoryCache.Default.Get(key) is string cached)
                return cached.Length == 0 ? null : cached;

            string language;
            using (var db = new DistributionDbContext())
            {
                language = Languages.TryNormalize(db.UserSettings.Where(s => s.UserID == userId).Select(s => s.Language).FirstOrDefault());
            }
            MemoryCache.Default.Set(key, language ?? "", DateTimeOffset.Now.Add(CacheTtl));
            return language;
        }

        public static void ForgetLanguage(int userId)
        {
            MemoryCache.Default.Remove("dst.lang:" + (TenantContext.CurrentOrNull?.Slug ?? "") + ":" + userId);
        }
    }
}
