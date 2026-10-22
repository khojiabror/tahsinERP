using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Web;
using System.Web.Hosting;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// Statik fayl URL'iga o'zgarish vaqtini qo'shadi (~/assets/css/theme.css?v=...).
    /// web.config statik fayllarni 1 soat keshlaydi — yangilanishdan keyin brauzer eski CSS/JS ni ushlab qolmasin.
    /// </summary>
    public static class AssetVersion
    {
        private static readonly ConcurrentDictionary<string, string> Cache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string Url(string virtualPath)
        {
            string url = VirtualPathUtility.ToAbsolute(virtualPath);
            // debug="true" (lokal) — har safar o'qiladi, CSS tahriri darhol ko'rinsin
            bool debug = HttpContext.Current != null && HttpContext.Current.IsDebuggingEnabled;
            return url + "?v=" + (debug ? Stamp(virtualPath) : Cache.GetOrAdd(virtualPath, Stamp));
        }

        private static string Stamp(string virtualPath)
        {
            try
            {
                string physical = HostingEnvironment.MapPath(virtualPath);
                if (physical != null && File.Exists(physical))
                    return File.GetLastWriteTimeUtc(physical).Ticks.ToString("x", CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is HttpException)
            {
            }
            return "0";
        }
    }
}
