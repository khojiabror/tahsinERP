using System;
using System.IO;
using System.Web.Hosting;
using tahsinERP.Configuration.Tenancy;

namespace tahsinERP.Areas.Distribution.Services
{
    /// <summary>
    /// Web'dan yuklangan rasmlar (masalan, hisobdan chiqarish fotosi, 05_FINDINGS K3-32) — ERP serverida, tenant bo'yicha:
    /// App_Data/DstFiles/{tenant}/{papka}/{guid}.{jpg|png}. Kalit "local/..." — bot fayl omboridagi kalitlardan (FileLinks) farqlanadi;
    /// fayl faqat ERP action orqali (ruxsat tekshiruvi bilan) beriladi, to'g'ridan-to'g'ri havola yo'q.
    /// </summary>
    public static class DstLocalFiles
    {
        public const string Prefix = "local/";
        public const int MaxBytes = 5 * 1024 * 1024;

        public static bool IsLocal(string key) => key != null && key.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>Faqat JPEG/PNG (sarlavha baytlari bo'yicha, kengaytmaga ishonilmaydi). Qaytaradi: kengaytma yoki null.</summary>
        public static string ImageExtension(byte[] head)
        {
            if (head == null || head.Length < 8) return null;
            if (head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) return "jpg";
            if (head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47 && head[4] == 0x0D && head[5] == 0x0A && head[6] == 0x1A && head[7] == 0x0A) return "png";
            return null;
        }

        public static string ContentType(string key) => key != null && key.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";

        /// <summary>Rasmni saqlaydi; noto'g'ri tur yoki hajm — <see cref="DstValidationException"/>.</summary>
        public static string SaveImage(Stream input, string folder, string root = null)
        {
            if (input == null) throw new DstValidationException(DstLocalization.T("File_ImageOnly"));
            byte[] data;
            using (var ms = new MemoryStream())
            {
                input.CopyTo(ms);
                data = ms.ToArray();
            }
            if (data.Length == 0 || data.Length > MaxBytes) throw new DstValidationException(DstLocalization.T("File_ImageOnly"));
            var ext = ImageExtension(data) ?? throw new DstValidationException(DstLocalization.T("File_ImageOnly"));
            var key = Prefix + folder + "/" + Guid.NewGuid().ToString("N") + "." + ext;
            var path = PathOf(key, root);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
            return key;
        }

        /// <summary>Kalit bo'yicha fayl yo'li (kalit "local/" bilan boshlanishi va ".." bo'lmasligi shart).</summary>
        public static string PathOf(string key, string root = null)
        {
            if (!IsLocal(key) || key.Contains("..") || key.Contains("\\")) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            root = root ?? HostingEnvironment.MapPath("~/App_Data/DstFiles") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "DstFiles");
            var tenant = TenantContext.CurrentOrNull?.Slug ?? "default";
            foreach (var c in Path.GetInvalidFileNameChars()) tenant = tenant.Replace(c, '_');
            return Path.Combine(root, tenant, key.Substring(Prefix.Length).Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
