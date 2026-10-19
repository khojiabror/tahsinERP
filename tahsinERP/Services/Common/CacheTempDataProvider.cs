using System;
using System.Collections.Generic;
using System.Runtime.Caching;
using System.Web;
using System.Web.Mvc;

namespace tahsinERP.Services.Common
{
    /// <summary>
    /// TempData'ni Session o'rniga server xotirasida (MemoryCache) saqlaydi; brauzerga faqat tasodifiy
    /// identifikator cookie'si beriladi (CORE-P05). Session'ga yozilmagani uchun controller'lar ReadOnly
    /// sessiya bilan ishlaydi va bir foydalanuvchining parallel AJAX so'rovlari navbatga turmaydi.
    /// Cookie provider'dan farqli o'laroq hajm cheklovi (4 KB) yo'q — import natijalari va fayllar ham sig'adi.
    /// </summary>
    public sealed class CacheTempDataProvider : ITempDataProvider
    {
        private const string CookieName = "tahsin.td";
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(20);
        private static readonly MemoryCache Cache = new MemoryCache("tahsinERP.TempData");

        public IDictionary<string, object> LoadTempData(ControllerContext controllerContext)
        {
            var key = Key(controllerContext.HttpContext, create: false);
            if (key == null)
                return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // TempData semantikasi: o'qilgandan keyin olib tashlanadi; qolgani SaveTempData'da qayta yoziladi
            var values = Cache.Remove(key) as IDictionary<string, object>;
            return values ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        public void SaveTempData(ControllerContext controllerContext, IDictionary<string, object> values)
        {
            var http = controllerContext.HttpContext;
            if (values == null || values.Count == 0)
            {
                var existing = Key(http, create: false);
                if (existing != null)
                    Cache.Remove(existing);
                return;
            }

            var key = Key(http, create: true);
            Cache.Set(key, new Dictionary<string, object>(values, StringComparer.OrdinalIgnoreCase),
                new CacheItemPolicy { SlidingExpiration = Lifetime });
        }

        private static string Key(HttpContextBase http, bool create)
        {
            var id = http.Request.Cookies[CookieName]?.Value;
            if (string.IsNullOrEmpty(id) || id.Length != 32)
            {
                id = http.Response.Cookies.AllKeys.Length > 0 && Array.IndexOf(http.Response.Cookies.AllKeys, CookieName) >= 0
                    ? http.Response.Cookies[CookieName].Value
                    : null;
            }

            if (string.IsNullOrEmpty(id))
            {
                if (!create)
                    return null;
                id = Guid.NewGuid().ToString("N");
                http.Response.Cookies.Add(new HttpCookie(CookieName, id)
                {
                    HttpOnly = true,
                    Secure = http.Request.IsSecureConnection,
                    Path = "/"
                });
            }

            // Login/logout oralig'ida ham xabar yo'qolmasin — kalit faqat tasodifiy 128-bitli identifikator
            return id;
        }
    }
}
