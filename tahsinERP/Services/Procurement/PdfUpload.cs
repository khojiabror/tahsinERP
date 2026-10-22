using System;
using System.IO;
using System.Web;
using tahsinERP.I18n;

namespace tahsinERP.Services.Procurement
{
    /// <summary>
    /// Shartnoma/invoys hujjati (PDF) yuklash tekshiruvi. Avval forma faqat brauzerda accept=".pdf"
    /// bilan cheklangan, server istalgan faylni saqlab, keyin uni application/pdf sifatida qaytarardi (SRM-B23).
    /// </summary>
    public static class PdfUpload
    {
        public const int MaxBytes = 20 * 1024 * 1024;

        private static readonly byte[] PdfSignature = { 0x25, 0x50, 0x44, 0x46, 0x2D }; // "%PDF-"

        /// <summary>Fayl berilganmi (bo'sh emas).</summary>
        public static bool HasFile(HttpPostedFileBase file)
        {
            return file != null && file.ContentLength > 0;
        }

        /// <summary>Xato matni yoki null (fayl berilmagan bo'lsa ham null).</summary>
        public static string Validate(HttpPostedFileBase file)
        {
            if (!HasFile(file))
            {
                return null;
            }

            if (!string.Equals(Path.GetExtension(file.FileName ?? ""), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return L.T("Srm_OnlyPdfAllowed");
            }

            if (file.ContentLength > MaxBytes)
            {
                return L.T("Srm_FileTooLarge", MaxBytes / (1024 * 1024));
            }

            var header = new byte[PdfSignature.Length];
            var stream = file.InputStream;
            long position = stream.CanSeek ? stream.Position : 0;
            int read = stream.Read(header, 0, header.Length);
            if (stream.CanSeek)
            {
                stream.Position = position;
            }

            if (read < header.Length)
            {
                return "Fayl PDF emas.";
            }

            for (int i = 0; i < header.Length; i++)
            {
                if (header[i] != PdfSignature[i])
                {
                    return "Fayl PDF emas.";
                }
            }

            return null;
        }

        /// <summary>Fayl mazmuni (avval <see cref="Validate"/> chaqirilgan bo'lishi kerak).</summary>
        public static byte[] Read(HttpPostedFileBase file)
        {
            var stream = file.InputStream;
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using (var reader = new BinaryReader(stream))
            {
                return reader.ReadBytes(file.ContentLength);
            }
        }
    }
}
