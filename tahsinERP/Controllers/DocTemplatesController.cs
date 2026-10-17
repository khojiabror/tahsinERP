using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.Services.Documents;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Hujjat shablonlari (xat, buyruq, standart) — .docx fayllar bazada (DOC_TEMPLATES) saqlanadi,
    /// papkada emas. Ruxsat hujjat turining o'z bo'limidan olinadi: ko'rish — bo'lim ro'yxatini ko'rish,
    /// yuklash/arxivlash/o'chirish — bo'limda yaratish huquqi (alohida PERMISSIONMODULES yozuvi shart emas).
    /// </summary>
    public class DocTemplatesController : BaseController
    {
        private const int MaxTemplateBytes = 10 * 1024 * 1024;
        private const string LegacyStandardTemplatePath = "~/App_Data/Templates/CompanyStandardTemplate.docx";

        private bool CanViewType(string type)
        {
            string owner = DocPlaceholders.OwnerController(type);
            return owner != null && RoleHelper.IsPermittedForAction(CurrentUserName, owner, "Index", false);
        }

        private bool CanChangeType(string type)
        {
            string owner = DocPlaceholders.OwnerController(type);
            return owner != null && RoleHelper.IsPermittedForAction(CurrentUserName, owner, "Create", true);
        }

        // Olish so'rovi: DocTemplates?docType=OFFICIAL_LETTER
        public async Task<ActionResult> Index(string docType)
        {
            var visibleTypes = DocPlaceholders.DocTypes.Where(CanViewType).ToList();
            if (visibleTypes.Count == 0)
                return NotAuthorized();

            if (string.IsNullOrEmpty(docType) || !visibleTypes.Contains(docType, StringComparer.OrdinalIgnoreCase))
                docType = visibleTypes[0];
            docType = visibleTypes.First(t => string.Equals(t, docType, StringComparison.OrdinalIgnoreCase));

            using (var db = new DBTHSNEntities())
            {
                var templates = await DocTemplateStore.ListAsync(db, docType, activeOnly: false);

                ViewBag.DocType = docType;
                ViewBag.DocTypes = visibleTypes;
                ViewBag.CanChange = CanChangeType(docType);
                ViewBag.Placeholders = DocPlaceholders.For(docType);
                ViewBag.CanImportLegacy = docType == DocPlaceholders.StandardType &&
                                          System.IO.File.Exists(Server.MapPath(LegacyStandardTemplatePath)) &&
                                          !templates.Any();
                return View(templates);
            }
        }

        // POST so'rovi: DocTemplates/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Upload(string docType, string name, HttpPostedFileBase file, bool archiveOthers = false)
        {
            if (!DocPlaceholders.IsKnownType(docType) || !CanChangeType(docType))
                return NotAuthorized();

            if (file == null || file.ContentLength == 0)
                return Back(docType, error: "Fayl tanlanmagan.");
            if (!string.Equals(Path.GetExtension(file.FileName), ".docx", StringComparison.OrdinalIgnoreCase))
                return Back(docType, error: "Faqat Word (.docx) fayl yuklash mumkin. Eski .doc faylni Word'da .docx qilib saqlang.");
            if (file.ContentLength > MaxTemplateBytes)
                return Back(docType, error: "Fayl hajmi 10 MB dan oshmasligi kerak.");

            byte[] content;
            using (var ms = new MemoryStream())
            {
                file.InputStream.CopyTo(ms);
                content = ms.ToArray();
            }

            var scan = DocumentTemplateService.Scan(content, docType);
            if (!scan.IsValid)
                return Back(docType, error: scan.Error);

            string displayName = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(file.FileName) : name.Trim();
            using (var db = new DBTHSNEntities())
            using (var tx = db.Database.BeginTransaction())
            {
                int typeId = await DocTemplateStore.EnsureDocTypeIdAsync(db, docType);
                if (archiveOthers)
                {
                    await db.Database.ExecuteSqlCommandAsync(
                        "UPDATE DOC_TEMPLATES SET IsActive = 0 WHERE DocTypeID = @p0 AND IsActive = 1", typeId);
                }

                var template = new DOC_TEMPLATES
                {
                    DocTypeID = typeId,
                    TemplateCode = await DocumentNumberService.NextAsync(db, "DOC_TEMPLATES", "TemplateCode", docType + "-", 2),
                    Name = displayName.Length > 200 ? displayName.Substring(0, 200) : displayName,
                    FileName = Path.GetFileName(file.FileName),
                    FileExtension = ".docx",
                    MimeType = DocumentTemplateService.DocxMime,
                    FileContent = content,
                    IsActive = true,
                    CreatedByUserID = CurrentUserId,
                    CreatedDT = DateTime.Now
                };
                db.DOC_TEMPLATES.Add(template);
                await db.SaveChangesAsync();
                tx.Commit();

                LogHelper.LogToDatabase(CurrentUserName, "DocTemplates", "Upload", $"Shablon yuklandi: {template.TemplateCode} ({docType})");
            }

            string message = $"Shablon \"{displayName}\" yuklandi.";
            if (DocPlaceholders.For(docType).Count > 0)
            {
                message += scan.Recognized.Count > 0
                    ? " Tanilgan maydonlar: " + string.Join(", ", scan.Recognized) + "."
                    : " Diqqat: shablonda birorta ham maydon ({{...}}) topilmadi — hujjat o'zgarishsiz chiqadi.";
            }
            string warning = scan.Unknown.Count > 0
                ? "Tanilmagan maydonlar (hujjatda o'zgarishsiz qoladi): " + string.Join(", ", scan.Unknown.Select(u => "{{" + u + "}}")) + ". Pastdagi ro'yxatdan to'g'ri nomni tanlang."
                : null;
            return Back(docType, success: message, warning: warning);
        }

        // Olish so'rovi: DocTemplates/Download/5
        public async Task<ActionResult> Download(int id)
        {
            using (var db = new DBTHSNEntities())
            {
                var t = await (from tpl in db.DOC_TEMPLATES
                               join dt in db.DOC_DOCUMENT_TYPES on tpl.DocTypeID equals dt.ID
                               where tpl.ID == id
                               select new { tpl.FileName, tpl.FileContent, tpl.MimeType, DocType = dt.Code }).FirstOrDefaultAsync();
                if (t == null || t.FileContent == null)
                    return HttpNotFound();
                if (!CanViewType(t.DocType))
                    return NotAuthorized();

                return File(t.FileContent, t.MimeType ?? DocumentTemplateService.DocxMime, t.FileName ?? "shablon.docx");
            }
        }

        // Olish so'rovi: DocTemplates/Sample?docType=... — barcha maydonlar bilan namunaviy shablon
        public ActionResult Sample(string docType)
        {
            if (!DocPlaceholders.IsKnownType(docType) || !CanViewType(docType))
                return NotAuthorized();

            return File(DocumentTemplateService.BuildSample(docType), DocumentTemplateService.DocxMime,
                "Namuna_" + docType + ".docx");
        }

        // POST so'rovi: DocTemplates/ToggleActive/5 — arxivlash / qayta faollashtirish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ToggleActive(int id)
        {
            using (var db = new DBTHSNEntities())
            {
                var t = await db.DOC_TEMPLATES.FirstOrDefaultAsync(x => x.ID == id);
                if (t == null)
                    return HttpNotFound();
                string docType = await db.DOC_DOCUMENT_TYPES.Where(x => x.ID == t.DocTypeID).Select(x => x.Code).FirstOrDefaultAsync();
                if (!CanChangeType(docType))
                    return NotAuthorized();

                t.IsActive = !t.IsActive;
                await db.SaveChangesAsync();
                DocumentTemplateService.InvalidateTemplate(id);

                LogHelper.LogToDatabase(CurrentUserName, "DocTemplates", "ToggleActive", $"Shablon {t.TemplateCode}: {(t.IsActive ? "faollashtirildi" : "arxivlandi")}");
                return Back(docType, success: t.IsActive ? "Shablon faollashtirildi." : "Shablon arxivlandi — hujjat yaratishda tanlanmaydi.");
            }
        }

        // POST so'rovi: DocTemplates/Delete/5 — faqat hech qaysi hujjatda ishlatilmagan bo'lsa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id)
        {
            using (var db = new DBTHSNEntities())
            {
                var t = await db.DOC_TEMPLATES.FirstOrDefaultAsync(x => x.ID == id);
                if (t == null)
                    return HttpNotFound();
                string docType = await db.DOC_DOCUMENT_TYPES.Where(x => x.ID == t.DocTypeID).Select(x => x.Code).FirstOrDefaultAsync();
                if (!CanChangeType(docType))
                    return NotAuthorized();

                if (await db.DOC_DOCUMENTS.AnyAsync(d => d.TemplateID == id))
                    return Back(docType, error: "Bu shablon bilan hujjatlar yaratilgan — o'chirib bo'lmaydi, arxivlang.");

                string code = t.TemplateCode;
                db.DOC_TEMPLATES.Remove(t);
                await db.SaveChangesAsync();
                DocumentTemplateService.InvalidateTemplate(id);

                LogHelper.LogToDatabase(CurrentUserName, "DocTemplates", "Delete", $"Shablon o'chirildi: {code}");
                return Back(docType, success: "Shablon o'chirildi.");
            }
        }

        // POST so'rovi: DocTemplates/ImportLegacy — App_Data'dagi eski standart shablonini bazaga ko'chirish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ImportLegacy()
        {
            string docType = DocPlaceholders.StandardType;
            if (!CanChangeType(docType))
                return NotAuthorized();

            string path = Server.MapPath(LegacyStandardTemplatePath);
            if (!System.IO.File.Exists(path))
                return Back(docType, error: "Papkada eski shablon fayli topilmadi.");

            using (var db = new DBTHSNEntities())
            using (var tx = db.Database.BeginTransaction())
            {
                int typeId = await DocTemplateStore.EnsureDocTypeIdAsync(db, docType);
                db.DOC_TEMPLATES.Add(new DOC_TEMPLATES
                {
                    DocTypeID = typeId,
                    TemplateCode = await DocumentNumberService.NextAsync(db, "DOC_TEMPLATES", "TemplateCode", docType + "-", 2),
                    Name = "Korxona standarti (asosiy)",
                    FileName = "CompanyStandardTemplate.docx",
                    FileExtension = ".docx",
                    MimeType = DocumentTemplateService.DocxMime,
                    FileContent = System.IO.File.ReadAllBytes(path),
                    IsActive = true,
                    CreatedByUserID = CurrentUserId,
                    CreatedDT = DateTime.Now
                });
                await db.SaveChangesAsync();
                tx.Commit();
            }

            LogHelper.LogToDatabase(CurrentUserName, "DocTemplates", "ImportLegacy", "Standart shabloni papkadan bazaga ko'chirildi");
            return Back(docType, success: "Shablon bazaga ko'chirildi.");
        }

        private ActionResult Back(string docType, string success = null, string error = null, string warning = null)
        {
            if (success != null) TempData["Success"] = success;
            if (error != null) TempData["ErrorMessage"] = error;
            if (warning != null) TempData["Warning"] = warning;
            return RedirectToAction("Index", new { docType });
        }
    }
}
