using System;
using System.Globalization;
using System.Runtime.Caching;
using System.Threading;
using System.Web;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Configuration.Tenancy;
using tahsinERP.Localization;
using tahsinERP.Models;

namespace tahsinERP.I18n
{
    /// <summary>
    /// So'rov tilini aniqlash va qo'llash. Tartib: foydalanuvchi tanlovi (dst.USER_SETTINGS.Language)
    /// -> cookie (login sahifasi, hali tanlamagan foydalanuvchi) -> firma standart tili (dst.SETTINGS DefaultLanguage) -> uz.
    /// UI tili CurrentUICulture'ga, oy/kun nomlari va valyuta belgisi CurrentCulture'ga qo'yiladi;
    /// qisqa sana va son ajratkichlari o'zgarmaydi (model binding til almashganda ham bir xil ishlaydi).
    /// </summary>
    public static class UiLanguage
    {
        public const string CookieName = "tahsin_lang";
        public const string DefaultLanguageKey = "DefaultLanguage";

        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

        public static string Resolve(HttpContextBase http)
        {
            string language = null;
            try
            {
                if (http?.User?.Identity?.IsAuthenticated == true)
                {
                    var userId = RoleHelper.GetUserId(http.User.Identity.Name);
                    if (userId.HasValue)
                        language = DstLocalization.StoredLanguageOf(userId.Value);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("UiLanguage: foydalanuvchi tili o'qilmadi: " + ex.Message);
            }
            return language
                   ?? Languages.TryNormalize(http?.Request?.Cookies[CookieName]?.Value)
                   ?? FirmDefault();
        }

        public static void Apply(string language)
        {
            language = Languages.Normalize(language);
            Thread.CurrentThread.CurrentUICulture = Languages.CultureFor(language);
            Thread.CurrentThread.CurrentCulture = DisplayCultures.For(language, Thread.CurrentThread.CurrentCulture);
        }

        /// <summary>Firma standart tili (10 daqiqa kesh); sozlanmagan yoki o'qib bo'lmasa — uz.</summary>
        public static string FirmDefault()
        {
            string key = "ui.lang.default:" + (TenantContext.CurrentOrNull?.Slug ?? "");
            if (MemoryCache.Default.Get(key) is string cached)
                return cached;
            string language = Languages.Default;
            try
            {
                using (var db = new DistributionDbContext())
                    language = Languages.Normalize(DstSettings.Get(db, DefaultLanguageKey));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("UiLanguage: firma standart tili o'qilmadi: " + ex.Message);
            }
            MemoryCache.Default.Set(key, language, DateTimeOffset.Now.Add(CacheTtl));
            return language;
        }

        public static void SetFirmDefault(string language)
        {
            using (var db = new DistributionDbContext())
            {
                DstSettings.Set(db, DefaultLanguageKey, Languages.Normalize(language));
                db.SaveChanges();
            }
            MemoryCache.Default.Remove("ui.lang.default:" + (TenantContext.CurrentOrNull?.Slug ?? ""));
        }

        /// <summary>Foydalanuvchi tanlovini saqlaydi (bot bildirishnomalari ham shu tilda keladi).</summary>
        public static void SaveForUser(int userId, string language)
        {
            using (var db = new DistributionDbContext())
            {
                var setting = db.UserSettings.Find(userId);
                if (setting == null)
                    db.UserSettings.Add(new Areas.Distribution.Data.Entities.UserSetting { UserID = userId, Language = Languages.Normalize(language) });
                else
                    setting.Language = Languages.Normalize(language);
                db.SaveChanges();
            }
            DstLocalization.ForgetLanguage(userId);
        }

        public static HttpCookie Cookie(string language)
        {
            return new HttpCookie(CookieName, Languages.Normalize(language))
            {
                Expires = DateTime.Now.AddYears(1),
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
            };
        }
    }
}
