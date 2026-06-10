using System.Collections.Generic;
using System.IO;
using System.Web.Mvc;

namespace tahsinERP.Controllers
{
    /// <summary>Docs/ papkadagi qo'llanma fayllarini yuklab olish.</summary>
    public class HelpDocsController : Controller
    {
        private static readonly Dictionary<string, string> AllowedFiles = new Dictionary<string, string>
        {
            { "DLOC_FIFO_User_Guide.pdf", "application/pdf" },
            { "DLOC_FIFO_User_Guide.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            { "DLOC_FIFO_User_Guide.md", "text/markdown; charset=utf-8" }
        };

        [HttpGet]
        public ActionResult Download(string file)
        {
            if (string.IsNullOrWhiteSpace(file) || !AllowedFiles.ContainsKey(file))
                return HttpNotFound();

            var path = Server.MapPath("~/Docs/" + file);
            if (!System.IO.File.Exists(path))
                return HttpNotFound();

            return File(path, AllowedFiles[file], file);
        }
    }
}
