using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Hosting;
using Newtonsoft.Json.Linq;
using tahsinERP.ViewModels;

namespace tahsinERP.Services
{
    /// <summary>
    /// Qo'llanma kontenti — Word qo'llanmalardan <c>tools/manual/convert_docx.py</c> yaratgan statik fayllar
    /// (<c>ManualData/&lt;til&gt;/&lt;NN-slug&gt;.html</c>, <c>ManualData/img/*.webp</c>, <c>manifest.json</c>).
    /// Fayllar qo'lda tahrir qilinmaydi: docx o'zgarsa konvertor qayta ishga tushiriladi.
    /// Papka to'g'ridan-to'g'ri ochilmaydi (Web.config hiddenSegments) — faqat ruxsat tekshiruvi bilan kontroller orqali.
    /// </summary>
    public static class UserManualProvider
    {
        public const string Folder = "~/ManualData";
        private static readonly Regex ImgToken = new Regex(@"@@IMG@@/([0-9a-f]{16})\.webp", RegexOptions.Compiled);
        private static readonly Regex ImgName = new Regex(@"^[0-9a-f]{16}$", RegexOptions.Compiled);
        private static readonly Regex ModuleId = new Regex(@"^\d\d-[a-z0-9-]+$", RegexOptions.Compiled);

        // Fayl o'zgarish vaqti bilan keshlanadi: konvertor qayta ishga tushsa IIS qayta yuklamasdan yangisi olinadi
        private static readonly ConcurrentDictionary<string, Tuple<DateTime, string>> Cache =
            new ConcurrentDictionary<string, Tuple<DateTime, string>>(StringComparer.Ordinal);

        private static string Root => HostingEnvironment.MapPath(Folder);

        /// <summary>UI tili -> qo'llanma tili: oz (kirill) uchun alohida qo'llanma yo'q, lotin o'zbekchasi ko'rsatiladi.</summary>
        public static string ManualLanguage(string uiLanguage) => uiLanguage == "ru" ? "ru" : "uz";

        public static List<UserManualModuleViewModel> GetModules(string lang)
        {
            var json = ReadCached(Path.Combine(Root, "manifest.json"));
            if (json == null) return new List<UserManualModuleViewModel>();
            return JObject.Parse(json)["modules"]
                .Select(m => new UserManualModuleViewModel
                {
                    Id = (string)m["id"],
                    Number = (string)m["num"],
                    Title = (string)m["titles"]?[lang] ?? (string)m["titles"]?["uz"] ?? (string)m["id"]
                })
                .Where(m => File.Exists(ContentPath(lang, m.Id)))
                .ToList();
        }

        /// <summary>Modul HTML fragmenti (rasm havolalari <paramref name="imgBaseUrl"/> + xesh), topilmasa null.</summary>
        public static string GetContent(string lang, string moduleId, string imgBaseUrl)
        {
            if (moduleId == null || !ModuleId.IsMatch(moduleId)) return null;
            var html = ReadCached(ContentPath(lang, moduleId));
            return html == null ? null : ImgToken.Replace(html, m => imgBaseUrl + m.Groups[1].Value);
        }

        public static string ImagePath(string name)
        {
            if (name == null || !ImgName.IsMatch(name)) return null;
            var path = Path.Combine(Root, "img", name + ".webp");
            return File.Exists(path) ? path : null;
        }

        private static string ContentPath(string lang, string moduleId) => Path.Combine(Root, lang, moduleId + ".html");

        private static string ReadCached(string path)
        {
            if (!File.Exists(path)) return null;
            var stamp = File.GetLastWriteTimeUtc(path);
            Tuple<DateTime, string> hit;
            if (Cache.TryGetValue(path, out hit) && hit.Item1 == stamp) return hit.Item2;
            var text = File.ReadAllText(path);
            Cache[path] = Tuple.Create(stamp, text);
            return text;
        }
    }
}
