using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Services.Documents
{
    /// <summary>
    /// DOC_FILES faylini brauzerga berish: MIME, RFC 5987 Content-Disposition va kesh sarlavhalari.
    /// Avval OfficialLetters, CompanyOrders va STD'da alohida nusxalar edi (dublikat #9, DOC-B14).
    /// DOC_FILES qatori o'zgarmaydi (yangi versiya = yangi ID), shuning uchun brauzer bir kun keshlaydi.
    /// </summary>
    public sealed class DocFileResult : ActionResult
    {
        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp" };

        private readonly byte[] _content;
        private readonly string _mimeType;
        private readonly string _fileName;
        private readonly bool _inline;

        public DocFileResult(byte[] content, string mimeType, string fileName, bool inline)
        {
            _content = content;
            _fileName = string.IsNullOrWhiteSpace(fileName) ? "file" : fileName;
            _mimeType = string.IsNullOrEmpty(mimeType) ? MimeMapping.GetMimeMapping(_fileName) : mimeType;
            _inline = inline;
        }

        public static bool IsImage(string mimeType, string extension)
        {
            return (mimeType != null && mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) ||
                   (extension != null && ImageExtensions.Contains(extension.ToLowerInvariant()));
        }

        /// <summary>
        /// Faylni beradi (rasm — inline, qolgani yuklab olish); topilmasa yoki bo'sh bo'lsa 404.
        /// <paramref name="file"/> — metadata (DocFileAccess), baytlar alohida o'qiladi (DOC-P02).
        /// </summary>
        public static async Task<ActionResult> ForAsync(DBTHSNEntities db, DOC_FILES file)
        {
            if (file == null)
                return new HttpNotFoundResult();
            var content = await DocFileService.ContentAsync(db, file.ID);
            if (content == null)
                return new HttpNotFoundResult();
            string name = file.FileName ?? ("file" + file.FileExtension);
            return new DocFileResult(content, file.MimeType, name, IsImage(file.MimeType, file.FileExtension));
        }

        public override void ExecuteResult(ControllerContext context)
        {
            var response = context.HttpContext.Response;
            response.ContentType = _mimeType;

            // ASCII zaxira + filename* (UTF-8) — nomdagi qo'shtirnoq/kirill harflari sarlavhani buzmaydi
            string asciiName = new string(_fileName.Select(c => c < 32 || c > 126 || c == '"' || c == '\\' ? '_' : c).ToArray());
            response.AppendHeader("Content-Disposition",
                (_inline ? "inline" : "attachment") + "; filename=\"" + asciiName + "\"; filename*=UTF-8''" + Uri.EscapeDataString(_fileName));

            response.Cache.SetCacheability(HttpCacheability.Private);
            response.Cache.SetMaxAge(TimeSpan.FromDays(1));
            response.OutputStream.Write(_content, 0, _content.Length);
        }
    }
}
