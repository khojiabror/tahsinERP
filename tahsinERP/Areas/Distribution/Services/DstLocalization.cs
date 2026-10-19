using System;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Runtime.Caching;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Configuration.Tenancy;

namespace tahsinERP.Areas.Distribution.Services
{
    /// <summary>
    /// Distributsiya modulining matnlari: Areas/Distribution/Resources/DstStrings.resx (o'zbek, asosiy)
    /// va DstStrings.ru.resx. Faqat UI tili (CurrentUICulture) almashadi — son/sana formati (CurrentCulture)
    /// ilovaning umumiy sozlamasida qoladi, model binding buzilmasligi uchun.
    /// </summary>
    public static class DstLocalization
    {
        public const string Uzbek = "uz";
        public const string Russian = "ru";

        private static readonly ResourceManager Resources = new ResourceManager(
            "tahsinERP.Areas.Distribution.Resources.DstStrings", typeof(DstLocalization).Assembly);

        private static readonly CultureInfo UzCulture = CultureInfo.GetCultureInfo("uz-Latn-UZ");
        private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

        public static ResourceManager ResourceManager => Resources;

        /// <summary>Kalit bo'yicha matn (joriy UI tilida); topilmasa — kalitning o'zi (xato ko'rinib tursin).</summary>
        public static string T(string key)
        {
            return Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        }

        public static string T(string key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, T(key), args);
        }

        public static CultureInfo CultureFor(string language)
        {
            return string.Equals(language, Russian, StringComparison.OrdinalIgnoreCase) ? RuCulture : UzCulture;
        }

        public static string Normalize(string language)
        {
            return string.Equals(language, Russian, StringComparison.OrdinalIgnoreCase) ? Russian : Uzbek;
        }

        /// <summary>Foydalanuvchi tili (dst.USER_SETTINGS, 10 daqiqa kesh). Yozuv bo'lmasa — o'zbek.</summary>
        public static string LanguageOf(int userId)
        {
            string key = "dst.lang:" + (TenantContext.CurrentOrNull?.Slug ?? "") + ":" + userId;
            if (MemoryCache.Default.Get(key) is string cached)
                return cached;

            string language;
            using (var db = new DistributionDbContext())
            {
                language = Normalize(db.UserSettings.Where(s => s.UserID == userId).Select(s => s.Language).FirstOrDefault());
            }
            MemoryCache.Default.Set(key, language, DateTimeOffset.Now.Add(CacheTtl));
            return language;
        }

        public static void ForgetLanguage(int userId)
        {
            MemoryCache.Default.Remove("dst.lang:" + (TenantContext.CurrentOrNull?.Slug ?? "") + ":" + userId);
        }
    }
}
