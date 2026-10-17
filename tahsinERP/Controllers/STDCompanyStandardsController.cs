using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.Services.Documents;
using System.Web;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Korxona standartlari CRUD - STD_COMPANY_STANDARDS jadvali uchun
    /// </summary>
    [Permission]
    public class STDCompanyStandardsController : BaseController
    {
        // Olish so'rovi: STDCompanyStandards
        public async Task<ActionResult> Index()
        {
            using (var db = new DBTHSNEntities())
            {
                var standards = await ActiveStandards(db)
                    .OrderByDescending(s => s.DOC_DOCUMENTS.CreatedDT)
                    .Select(s => new STDCompanyStandardsIndexViewModel
                    {
                        DocumentID = s.DocumentID,
                        StandardCode = s.StandardCode,
                        Area = s.Area,
                        VersionNo = s.VersionNo,
                        Notes = s.Notes,
                        DocumentTitle = s.DOC_DOCUMENTS.Title,
                        CreatedDT = s.DOC_DOCUMENTS.CreatedDT
                    })
                    .ToListAsync();

                return View(standards);
            }
        }

        // Olish so'rovi: STDCompanyStandards/yaratish
        public async Task<ActionResult> Create()
        {
            await SetTemplatesAsync();
            return View();
        }

        /// <summary>Create sahifasidagi shablon tanlovi (shablonlar bazada — "Shablonlar" sahifasi).</summary>
        private async Task SetTemplatesAsync()
        {
            using (var db = new DBTHSNEntities())
            {
                ViewBag.Templates = await DocTemplateStore.ListAsync(db, DocPlaceholders.StandardType, activeOnly: true);
            }
        }

        // Olish so'rovi: STDCompanyStandards/GetTemplate?templateId=5 (Create sahifasi muharrirga yuklaydi)
        // Avval ruxsat tekshirilmasdi (DOC-S05) — shablon Create sahifasiniki, o'zgartirish huquqi
        [Permission(Change = true)]
        public async Task<ActionResult> GetTemplate(int? templateId)
        {
            using (var db = new DBTHSNEntities())
            {
                // Shablon bazadan (DOC_TEMPLATES); tanlanmasa — eng yangi faol shablon
                var resolved = await DocTemplateStore.ResolveActiveAsync(db, DocPlaceholders.StandardType, templateId);
                if (resolved.HasValue)
                {
                    var t = await db.DOC_TEMPLATES.Where(x => x.ID == resolved.Value)
                        .Select(x => new { x.FileName, x.FileContent }).FirstAsync();
                    return File(t.FileContent, DocFileService.DocxMime, t.FileName ?? "CompanyStandardTemplate.docx");
                }
            }

            // Bazada shablon hali yo'q — eski papka fayli ("Shablonlar" sahifasida bir bosishda bazaga ko'chiriladi)
            var templatePath = Server.MapPath("~/App_Data/Templates/CompanyStandardTemplate.docx");
            if (System.IO.File.Exists(templatePath))
            {
                return File(templatePath, DocFileService.DocxMime, "CompanyStandardTemplate.docx");
            }

            return File(WordDocumentConverterService.CreateEmptyDocx(), DocFileService.DocxMime, "CompanyStandardTemplate.docx");
        }

        // POST so'rovi: STDCompanyStandards/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(STDCompanyStandardsCreateViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.StandardCode))
            {
                ModelState.AddModelError("StandardCode", "Standart kodi kiritilishi shart");
            }

            byte[] docxBytes = null;
            if (string.IsNullOrWhiteSpace(model.DocumentContent))
            {
                ModelState.AddModelError("DocumentContent", "Hujjat kontenti bo'sh bo'lishi mumkin emas");
            }
            else
            {
                // Avval base64 ikki marta dekodlanar, HTML bo'lmasa xom baytlar ".docx" sifatida saqlanardi (DOC-B13)
                docxBytes = ConvertDocumentContent(model.DocumentContent);
                if (docxBytes == null)
                {
                    ModelState.AddModelError("DocumentContent", "Hujjat kontenti noto'g'ri formatda");
                }
            }

            if (!ModelState.IsValid)
            {
                await SetTemplatesAsync();
                return View(model);
            }

            try
            {
                using (var db = new DBTHSNEntities())
                using (var tx = db.Database.BeginTransaction())
                {
                    // Hujjat, standart va fayllar bitta tranzaksiyada — xato bo'lsa yetim DOC_DOCUMENTS qolmaydi (DOC-B05)
                    int year = DateTime.Now.Year;
                    // Tur/holat kod bo'yicha (avval nom bo'yicha qidirilib, nom o'zgarsa yangi tur yaratilardi)
                    int typeId = await DocTemplateStore.EnsureDocTypeIdAsync(db, DocPlaceholders.StandardType);
                    int statusId = await DocumentService.EnsureStatusIdAsync(db, ActiveStatusCode);
                    // Avval "shu yil barcha hujjatlar soni + 1" — o'chirish/parallel yaratishda takrorlanardi (DOC-B07)
                    string code = await DocumentNumberService.NextAsync(db, "DOC_DOCUMENTS", "Code", $"STD-{year}-", 6);

                    var document = DocumentService.NewHeader(typeId, statusId, code, model.StandardCode, CurrentUserId);
                    db.DOC_DOCUMENTS.Add(document);
                    await db.SaveChangesAsync();

                    db.STD_COMPANY_STANDARDS.Add(new STD_COMPANY_STANDARDS
                    {
                        DocumentID = document.ID,
                        StandardCode = model.StandardCode.Trim(),
                        Area = model.Area?.Trim(),
                        VersionNo = 1.00m,
                        Notes = model.Notes?.Trim()
                    });

                    await AddDocxVersionAsync(db, document.ID, docxBytes, $"{model.StandardCode}_v1.00.docx", versionNo: 1);

                    await db.SaveChangesAsync();
                    tx.Commit();

                    LogHelper.LogToDatabase(CurrentUserName, "STDCompanyStandards", $"Yangi standart yaratildi: {model.StandardCode}");
                    TempData["Success"] = "Standart muvaffaqiyatli yaratildi";

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("STD Create: {0}", ex);
                ModelState.AddModelError("", "Saqlashda xatolik yuz berdi.");
                await SetTemplatesAsync();
                return View(model);
            }
        }

        /// <summary>View yuborgan base64(HTML) kontentni .docx'ga aylantiradi; noto'g'ri bo'lsa null.</summary>
        private static byte[] ConvertDocumentContent(string base64Html)
        {
            string html;
            try
            {
                html = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Html));
            }
            catch (FormatException)
            {
                return null;
            }

            if (!html.Contains("<") || !html.Contains(">"))
                return null;

            return HtmlToDocxConverter.ConvertHtmlToDocx(html);
        }

        /// <summary>
        /// Yangi asosiy .docx versiyasini qo'shadi (SaveChanges chaqiruvchida). Rasmlar endi alohida DOC_FILES
        /// qatori sifatida saqlanmaydi: Details ularni joriy .docx ichidan ko'rsatadi (DOC-P08).
        /// </summary>
        private Task AddDocxVersionAsync(DBTHSNEntities db, long documentId, byte[] docxBytes, string fileName, int? versionNo = null)
        {
            return DocFileService.AddMainVersionAsync(db, documentId, docxBytes, fileName, ".docx",
                DocFileService.DocxMime, CurrentUserId, versionNo);
        }

        /// <summary>O'chirilmagan standartlar (avval soft delete faqat DOC_DOCUMENTS'ni belgilardi, Index filtrsiz edi — DOC-B01).</summary>
        private static IQueryable<STD_COMPANY_STANDARDS> ActiveStandards(DBTHSNEntities db)
        {
            return db.STD_COMPANY_STANDARDS.Where(s => s.IsDeleted != true && s.DOC_DOCUMENTS.IsDeleted != true);
        }

        private async Task<ActionResult> EditViewAsync(STDCompanyStandardsEditViewModel model)
        {
            using (var db = new DBTHSNEntities())
            {
                ViewBag.DocumentFileId = await DocFileService.LatestMainFileIdAsync(db, model.DocumentID, ".docx");
            }
            return View("Edit", model);
        }

        // Olish so'rovi: STDCompanyStandards/Edit/5
        public async Task<ActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var standard = await ActiveStandards(db)
                    .Include(s => s.DOC_DOCUMENTS)
                    .FirstOrDefaultAsync(s => s.DocumentID == id);

                if (standard == null)
                {
                    return HttpNotFound();
                }

                var viewModel = new STDCompanyStandardsEditViewModel
                {
                    DocumentID = standard.DocumentID,
                    StandardCode = standard.StandardCode,
                    Area = standard.Area,
                    CurrentVersionNo = standard.VersionNo,
                    NewVersionNo = standard.VersionNo + 0.01m,
                    Notes = standard.Notes
                };

                // View joriy .docx'ni shu ID bo'yicha muharrirga yuklaydi. Avval o'rnatilmagan edi —
                // muharrir bo'sh ochilardi (DOC-B03); o'rniga ishlatilmaydigan blob o'qilardi (DOC-P04).
                ViewBag.DocumentFileId = await DocFileService.LatestMainFileIdAsync(db, standard.DocumentID, ".docx");

                return View(viewModel);
            }
        }

        // POST so'rovi: STDCompanyStandards/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(STDCompanyStandardsEditViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.StandardCode))
            {
                ModelState.AddModelError("StandardCode", "Standart kodi kiritilishi shart");
            }

            // Bo'sh DocumentContent = faqat metadata tahrirlandi (view matn o'zgarmasa yubormaydi)
            byte[] docxBytes = null;
            if (!string.IsNullOrEmpty(model.DocumentContent))
            {
                docxBytes = ConvertDocumentContent(model.DocumentContent);
                if (docxBytes == null)
                {
                    ModelState.AddModelError("DocumentContent", "Hujjat kontenti noto'g'ri formatda");
                }
            }

            if (!ModelState.IsValid)
            {
                return await EditViewAsync(model);
            }

            try
            {
                using (var db = new DBTHSNEntities())
                using (var tx = db.Database.BeginTransaction())
                {
                    var standard = await ActiveStandards(db)
                        .Include(s => s.DOC_DOCUMENTS)
                        .FirstOrDefaultAsync(s => s.DocumentID == model.DocumentID);

                    if (standard == null)
                    {
                        return HttpNotFound();
                    }

                    // Versiya raqamini yangilash
                    standard.StandardCode = model.StandardCode.Trim();
                    standard.Area = model.Area?.Trim();
                    standard.VersionNo = model.NewVersionNo;
                    standard.Notes = model.Notes?.Trim();

                    // hujjat title ni yangilash
                    standard.DOC_DOCUMENTS.Title = model.StandardCode;
                    standard.DOC_DOCUMENTS.UpdatedDT = DateTime.Now;

                    if (docxBytes != null)
                    {
                        await AddDocxVersionAsync(db, standard.DocumentID, docxBytes,
                            $"{model.StandardCode}_v{model.NewVersionNo:F2}.docx");
                    }

                    await db.SaveChangesAsync();
                    tx.Commit();

                    LogHelper.LogToDatabase(CurrentUserName, "STDCompanyStandards", $"Standart yangilandi: {model.StandardCode} (v{model.NewVersionNo:F2})");
                    TempData["Success"] = "Standart muvaffaqiyatli yangilandi";

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("STD Edit {0}: {1}", model.DocumentID, ex);
                ModelState.AddModelError("", "Saqlashda xatolik yuz berdi.");
                return await EditViewAsync(model);
            }
        }

        // Olish so'rovi: STDCompanyStandards/Details/5
        public async Task<ActionResult> Details(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var standard = await ActiveStandards(db)
                    .Where(s => s.DocumentID == id)
                    .Include(s => s.DOC_DOCUMENTS)
                    .FirstOrDefaultAsync();

                if (standard == null)
                {
                    return HttpNotFound();
                }

                // Versiya tarixi (faqat .docx) — metadata proyeksiyasi, blob'lar o'qilmaydi (DOC-P03)
                var docxFiles = await DocFileService.ListVersionsAsync(db, id.Value, ".docx");

                var versionHistory = docxFiles.Select((f, index) => new VersionHistoryItem
                {
                    VersionNo = f.VersionNo,
                    VersionDecimal = standard.VersionNo - index * 0.01m, // Taxminiy versiya; ro'yxat yangisidan boshlanadi (avval eng yangisiga eng kichik raqam chiqardi)
                    CreatedDT = f.CreatedDT,
                    FileName = f.FileName,
                    FileID = f.ID
                }).ToList();

                // Rasmlar joriy .docx ichidan (avval har versiya rasmlari alohida DOC_FILES qatorlari edi va
                // galereyada barcha versiyalarniki ko'rinardi — DOC-P08). Faqat metadata; baytlarni GetImage beradi.
                var imageFiles = new List<DocumentImageItem>();
                var currentDocx = docxFiles.FirstOrDefault(f => f.IsMain) ?? docxFiles.FirstOrDefault(); // ro'yxat VersionNo DESC
                if (currentDocx != null)
                {
                    var docxBytes = await db.DOC_FILES.AsNoTracking()
                        .Where(f => f.ID == currentDocx.ID)
                        .Select(f => f.FileContent)
                        .FirstOrDefaultAsync();
                    if (docxBytes != null)
                    {
                        imageFiles = WordDocumentImageService.ListImages(docxBytes)
                            .Select(im => new DocumentImageItem
                            {
                                FileID = currentDocx.ID,
                                Index = im.Index,
                                FileName = im.FileName,
                                MimeType = im.MimeType,
                                FileSizeBytes = im.SizeBytes,
                                VersionNo = currentDocx.VersionNo
                            })
                            .ToList();
                    }
                }

                var viewModel = new STDCompanyStandardsDetailsViewModel
                {
                    DocumentID = standard.DocumentID,
                    StandardCode = standard.StandardCode,
                    Area = standard.Area,
                    VersionNo = standard.VersionNo,
                    Notes = standard.Notes,
                    DocumentTitle = standard.DOC_DOCUMENTS?.Title,
                    CreatedDT = standard.DOC_DOCUMENTS?.CreatedDT ?? DateTime.Now,
                    UpdatedDT = standard.DOC_DOCUMENTS?.UpdatedDT,
                    VersionHistory = versionHistory,
                    HasDocumentFile = versionHistory.Any(),
                    Images = imageFiles
                };

                return View(viewModel);
            }
        }

        // Olish so'rovi: STDCompanyStandards/o'chirish/5
        public async Task<ActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var standard = await ActiveStandards(db)
                    .Include(s => s.DOC_DOCUMENTS)
                    .FirstOrDefaultAsync(s => s.DocumentID == id);

                if (standard == null)
                {
                    return HttpNotFound();
                }

                // Versiya raqamini decimal formatda ko'rsatish uchun
                ViewBag.VersionNo = standard.VersionNo;

                return View(standard);
            }
        }

        // POST so'rovi: STDCompanyStandards/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(long id)
        {
            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var standardCode = await db.STD_COMPANY_STANDARDS
                        .Where(s => s.DocumentID == id && s.IsDeleted != true && s.DOC_DOCUMENTS.IsDeleted != true)
                        .Select(s => s.StandardCode)
                        .FirstOrDefaultAsync();

                    if (standardCode == null)
                    {
                        return HttpNotFound();
                    }

                    // Hujjat, standart (avval faqat DOC_DOCUMENTS — DOC-B01) va fayllar set-based (DOC-P06)
                    await DocumentService.SoftDeleteAsync(db, id);

                    LogHelper.LogToDatabase(CurrentUserName, "STDCompanyStandards", $"Standart o'chirildi: {standardCode}");
                    TempData["Success"] = "Standart muvaffaqiyatli o'chirildi";

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Xatolik yuz berdi: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Olish so'rovi: STDCompanyStandards/DownloadFile/5 (rasmlar inline, qolganlari yuklab olish)
        public Task<ActionResult> DownloadFile(long? fileId)
        {
            return ServeFileAsync(fileId, imagesOnly: false);
        }

        // Olish so'rovi: STDCompanyStandards/GetImage?fileId=5&index=0 - .docx ichidagi rasm.
        // index'siz — eski (DOC-P08 gacha alohida saqlangan) rasm qatorlari uchun.
        public Task<ActionResult> GetImage(long? fileId, int? index)
        {
            return index.HasValue ? ServeDocxImageAsync(fileId, index.Value) : ServeFileAsync(fileId, imagesOnly: true);
        }

        private async Task<ActionResult> ServeDocxImageAsync(long? fileId, int index)
        {
            if (fileId == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var file = await DocFileAccess.FindAsync(db, fileId.Value, DocFileAccess.Owner.CompanyStandard);
                if (file == null || file.FileContent == null || !string.Equals(file.FileExtension, ".docx", StringComparison.OrdinalIgnoreCase))
                {
                    return HttpNotFound();
                }

                var image = WordDocumentImageService.GetImage(file.FileContent, index);
                if (image == null)
                {
                    return HttpNotFound();
                }

                return new DocFileResult(image.ImageBytes, image.MimeType, image.FileName, inline: true);
            }
        }

        private async Task<ActionResult> ServeFileAsync(long? fileId, bool imagesOnly)
        {
            if (fileId == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var file = await DocFileAccess.FindAsync(db, fileId.Value, DocFileAccess.Owner.CompanyStandard);
                if (file == null || file.FileContent == null)
                {
                    return HttpNotFound();
                }

                if (imagesOnly && !DocFileResult.IsImage(file.MimeType, file.FileExtension))
                {
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Bu fayl rasm emas");
                }

                return DocFileResult.For(file);
            }
        }

        /// <summary>Standartlar "Active" holatida yaratiladi (ish jarayoni yo'q).</summary>
        private const string ActiveStatusCode = "Active";
    }
}

