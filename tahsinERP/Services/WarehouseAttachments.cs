using System;

namespace tahsinERP.Services
{
    /// <summary>
    /// Ombor hujjatlari (WRHS_INCOME_DOCS / WRHS_EXPENSE_DOCS) ilovalari uchun yordamchi.
    /// Fayl nomi va MIME turi bazada saqlanmaydi (sxema o'zgarmaydi), shuning uchun tur fayl
    /// boshidagi imzodan aniqlanadi — avval har qanday fayl "application/pdf" deb berilardi (SRM-S04).
    /// </summary>
    public static class WarehouseAttachments
    {
        public static string ContentType(byte[] data, out string extension)
        {
            extension = ".bin";
            if (data == null || data.Length < 4)
                return "application/octet-stream";

            if (StartsWith(data, 0x25, 0x50, 0x44, 0x46)) { extension = ".pdf"; return "application/pdf"; }
            if (StartsWith(data, 0x89, 0x50, 0x4E, 0x47)) { extension = ".png"; return "image/png"; }
            if (StartsWith(data, 0xFF, 0xD8, 0xFF)) { extension = ".jpg"; return "image/jpeg"; }
            if (StartsWith(data, 0x47, 0x49, 0x46, 0x38)) { extension = ".gif"; return "image/gif"; }
            if (StartsWith(data, 0x50, 0x4B, 0x03, 0x04)) { extension = ".zip"; return "application/zip"; }   // docx/xlsx ham ZIP
            if (StartsWith(data, 0xD0, 0xCF, 0x11, 0xE0)) { extension = ".doc"; return "application/msword"; }
            return "application/octet-stream";
        }

        /// <summary>Yuklab berish uchun MIME turi va fayl nomi (prefiks + imzodan aniqlangan kengaytma).</summary>
        public static string ContentType(byte[] data, string namePrefix, out string fileName)
        {
            string extension;
            var contentType = ContentType(data, out extension);
            fileName = namePrefix + extension;
            return contentType;
        }

        private static bool StartsWith(byte[] data, params byte[] signature)
        {
            if (data.Length < signature.Length)
                return false;
            for (int i = 0; i < signature.Length; i++)
            {
                if (data[i] != signature[i])
                    return false;
            }
            return true;
        }
    }
}
