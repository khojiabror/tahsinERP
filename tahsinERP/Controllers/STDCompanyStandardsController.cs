using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Services;
using tahsinERP.Services.Documents;
using System.Web;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Korxona standartlari CRUD - STD_COMPANY_STANDARDS jadvali uchun
    /// </summary>
    public class STDCompanyStandardsController : Controller
    {
        // Olish so'rovi: STDCompanyStandards
        public async Task<ActionResult> Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var standards = await db.STD_COMPANY_STANDARDS
                    .Include(s => s.DOC_DOCUMENTS)
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
        public ActionResult Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Template .docx faylini yuklash
            var templatePath = Server.MapPath("~/App_Data/Templates/CompanyStandardTemplate.docx");
            ViewBag.HasTemplate = System.IO.File.Exists(templatePath);
            ViewBag.TemplatePath = templatePath;

            return View();
        }

        // Olish so'rovi: STDCompanyStandards/GetTemplate
        public ActionResult GetTemplate()
        {
            var templatePath = Server.MapPath("~/App_Data/Templates/CompanyStandardTemplate.docx");
            if (System.IO.File.Exists(templatePath))
            {
                return File(templatePath, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "CompanyStandardTemplate.docx");
            }
            else
            {
                // Bo'sh .docx fayl yaratish
                var emptyDocxBytes = CreateEmptyDocx();
                return File(emptyDocxBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "CompanyStandardTemplate.docx");
            }
        }

        // POST so'rovi: STDCompanyStandards/UploadImage (OnlyOffice image upload)
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public ActionResult UploadImage()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            try
            {
                if (Request.Files.Count > 0)
                {
                    var file = Request.Files[0];
                    if (file != null && file.ContentLength > 0)
                    {
                        // Rasm faylini tekshirish
                        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp" };
                        var extension = System.IO.Path.GetExtension(file.FileName)?.ToLower();
                        
                        if (!allowedExtensions.Contains(extension))
                        {
                            return Json(new { error = 1, message = "Faqat rasm fayllari qabul qilinadi" });
                        }

                        // Rasmni byte array ga o'tkazish
                        byte[] imageBytes;
                        using (var binaryReader = new System.IO.BinaryReader(file.InputStream))
                        {
                            imageBytes = binaryReader.ReadBytes(file.ContentLength);
                        }

                        // Base64 ga o'tkazish (OnlyOffice uchun)
                        var base64Image = Convert.ToBase64String(imageBytes);
                        var mimeType = file.ContentType ?? "image/png";
                        var dataUrl = $"data:{mimeType};base64,{base64Image}";

                        return Json(new 
                        { 
                            error = 0,
                            file = new 
                            {
                                url = dataUrl,
                                name = file.FileName,
                                size = file.ContentLength
                            }
                        });
                    }
                }

                return Json(new { error = 1, message = "Fayl topilmadi" });
            }
            catch (Exception ex)
            {
                return Json(new { error = 1, message = $"Xatolik: {ex.Message}" });
            }
        }

        private byte[] CreateEmptyDocx()
        {
            // Bo'sh Word hujjat yaratish
            using (var memoryStream = new MemoryStream())
            {
                using (var wordDocument = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document))
                {
                    var mainPart = wordDocument.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    var body = mainPart.Document.AppendChild(new Body());
                    
                    // Bo'sh paragraf
                    var paragraph = new Paragraph();
                    var run = new Run();
                    run.AppendChild(new Text(" "));
                    paragraph.AppendChild(run);
                    body.AppendChild(paragraph);
                    
                    mainPart.Document.Save();
                }
                
                return memoryStream.ToArray();
            }
        }

        // POST so'rovi: STDCompanyStandards/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(STDCompanyStandardsCreateViewModel model)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (string.IsNullOrWhiteSpace(model.StandardCode))
            {
                ModelState.AddModelError("StandardCode", "Standart kodi kiritilishi shart");
            }

            if (string.IsNullOrWhiteSpace(model.DocumentContent))
            {
                ModelState.AddModelError("DocumentContent", "Hujjat kontenti bo'sh bo'lishi mumkin emas");
            }

            if (!ModelState.IsValid)
            {
                var templatePath = Server.MapPath("~/App_Data/Templates/CompanyStandardTemplate.docx");
                ViewBag.HasTemplate = System.IO.File.Exists(templatePath);
                ViewBag.TemplatePath = templatePath;
                return View(model);
            }

            try
            {
                using (var db = new DBTHSNEntities())
                {
                    // DOC_DOCUMENTS yaratish
                    var document = new DOC_DOCUMENTS
                    {
                        DocTypeID = GetOrCreateDocumentTypeID(db, "CompanyStandard"),
                        StatusID = GetOrCreateStatusID(db, "Active"),
                        Code = GenerateDocumentCode(db),
                        Title = model.StandardCode,
                        CreatedDT = DateTime.Now,
                        IntegrationUID = Guid.NewGuid()
                    };

                    db.DOC_DOCUMENTS.Add(document);
                    await db.SaveChangesAsync();

                    // STD_COMPANY_STANDARDS yaratish
                    var standard = new STD_COMPANY_STANDARDS
                    {
                        DocumentID = document.ID,
                        StandardCode = model.StandardCode.Trim(),
                        Area = model.Area?.Trim(),
                        VersionNo = 1.00m,
                        Notes = model.Notes?.Trim()
                    };

                    db.STD_COMPANY_STANDARDS.Add(standard);
                    await db.SaveChangesAsync();

                    // DOC_FILES yaratish (.docx fayl)
                    byte[] docxBytes = null;
                    if (!string.IsNullOrEmpty(model.DocumentContent))
                    {
                        // Base64 dan HTML kontentni olish
                        try
                        {
                            var base64Bytes = Convert.FromBase64String(model.DocumentContent);
                            var htmlContent = System.Text.Encoding.UTF8.GetString(base64Bytes);
                            
                            // HTML ni .docx ga konvertatsiya qilish
                            if (htmlContent.Contains("<") && htmlContent.Contains(">"))
                            {
                                docxBytes = HtmlToDocxConverter.ConvertHtmlToDocx(htmlContent);
                            }
                            else
                            {
                                // Agar HTML emas bo'lsa, oddiy .docx fayl sifatida qabul qilish
                                docxBytes = base64Bytes;
                            }
                        }
                        catch
                        {
                            // Agar base64 emas bo'lsa, HTML sifatida qabul qilish va .docx ga konvertatsiya qilish
                            try
                            {
                                docxBytes = HtmlToDocxConverter.ConvertHtmlToDocx(model.DocumentContent);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"HTML to DOCX konvertatsiya xatolik: {ex.Message}");
                                // Bo'sh .docx yaratish
                                docxBytes = CreateEmptyDocx();
                            }
                        }
                    }

                    if (docxBytes != null && docxBytes.Length > 0)
                    {
                        // Asosiy Word faylini saqlash
                        var docFile = new DOC_FILES
                        {
                            DocumentID = document.ID,
                            VersionNo = 1,
                            FileName = $"{model.StandardCode}_v1.00.docx",
                            FileExtension = ".docx",
                            MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                            FileSizeBytes = docxBytes.Length,
                            FileContent = docxBytes,
                            IsMain = true,
                            CreatedDT = DateTime.Now
                        };

                        db.DOC_FILES.Add(docFile);
                        await db.SaveChangesAsync();

                        // Word hujjatidan rasmlarni ajratib olish va saqlash
                        try
                        {
                            var images = WordDocumentImageService.ExtractImagesFromWord(docxBytes);
                            if (images.Any())
                            {
                                var imageInfos = WordDocumentImageService.PrepareImagesForStorage(images, document.ID, 1);
                                foreach (var imageInfo in imageInfos)
                                {
                                    var imageFile = new DOC_FILES
                                    {
                                        DocumentID = imageInfo.DocumentID,
                                        VersionNo = imageInfo.VersionNo,
                                        FileName = imageInfo.FileName,
                                        FileExtension = "." + imageInfo.FileExtension,
                                        MimeType = imageInfo.MimeType,
                                        FileSizeBytes = imageInfo.FileSizeBytes,
                                        FileContent = imageInfo.FileContent,
                                        IsMain = false,
                                        CreatedDT = imageInfo.CreatedDT
                                    };

                                    db.DOC_FILES.Add(imageFile);
                                }
                                await db.SaveChangesAsync();
                            }
                        }
                        catch (Exception imgEx)
                        {
                            // Rasmlarni ajratishda xatolik bo'lsa ham, asosiy fayl saqlangan bo'ladi
                            System.Diagnostics.Debug.WriteLine($"Rasmlarni ajratishda xatolik: {imgEx.Message}");
                        }
                    }

                    LogHelper.LogToDatabase(User.Identity.Name, "STDCompanyStandards", $"Yangi standart yaratildi: {model.StandardCode}");
                    TempData["Success"] = "Standart muvaffaqiyatli yaratildi";

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Xatolik yuz berdi: {ex.Message}");
                var templatePath = Server.MapPath("~/App_Data/Templates/CompanyStandardTemplate.docx");
                ViewBag.HasTemplate = System.IO.File.Exists(templatePath);
                ViewBag.TemplatePath = templatePath;
                return View(model);
            }
        }

        // Olish so'rovi: STDCompanyStandards/Edit/5
        public async Task<ActionResult> Edit(long? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var standard = await db.STD_COMPANY_STANDARDS
                    .Include(s => s.DOC_DOCUMENTS)
                    .FirstOrDefaultAsync(s => s.DocumentID == id);

                if (standard == null)
                {
                    return HttpNotFound();
                }

                // Eng so'nggi versiya faylini olish
                var latestFile = await db.DOC_FILES
                    .Where(f => f.DocumentID == id && f.IsMain)
                    .OrderByDescending(f => f.VersionNo)
                    .FirstOrDefaultAsync();

                var viewModel = new STDCompanyStandardsEditViewModel
                {
                    DocumentID = standard.DocumentID,
                    StandardCode = standard.StandardCode,
                    Area = standard.Area,
                    CurrentVersionNo = standard.VersionNo,
                    NewVersionNo = standard.VersionNo + 0.01m,
                    Notes = standard.Notes,
                    ExistingDocumentContent = latestFile?.FileContent
                };

                return View(viewModel);
            }
        }

        // POST so'rovi: STDCompanyStandards/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(STDCompanyStandardsEditViewModel model)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (string.IsNullOrWhiteSpace(model.StandardCode))
            {
                ModelState.AddModelError("StandardCode", "Standart kodi kiritilishi shart");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var standard = await db.STD_COMPANY_STANDARDS
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
                    if (standard.DOC_DOCUMENTS != null)
                    {
                        standard.DOC_DOCUMENTS.Title = model.StandardCode;
                        standard.DOC_DOCUMENTS.UpdatedDT = DateTime.Now;
                    }

                    await db.SaveChangesAsync();

                    // Yangi versiya faylini saqlash
                    if (!string.IsNullOrEmpty(model.DocumentContent))
                    {
                        byte[] docxBytes = null;
                        try
                        {
                            docxBytes = Convert.FromBase64String(model.DocumentContent);
                        }
                        catch
                        {
                            docxBytes = System.Text.Encoding.UTF8.GetBytes(model.DocumentContent);
                        }

                        if (docxBytes != null && docxBytes.Length > 0)
                        {
                            // Eski faylni IsMain = false qilish
                            var oldFiles = await db.DOC_FILES
                                .Where(f => f.DocumentID == model.DocumentID && f.IsMain)
                                .ToListAsync();

                            foreach (var oldFile in oldFiles)
                            {
                                oldFile.IsMain = false;
                            }

                            // Yangi versiya faylini yaratish
                            var maxVersion = await db.DOC_FILES
                                .Where(f => f.DocumentID == model.DocumentID)
                                .Select(f => (int?)f.VersionNo)
                                .DefaultIfEmpty(0)
                                .MaxAsync() ?? 0;

                            // Base64 dan HTML kontentni olish va .docx ga konvertatsiya qilish
                            try
                            {
                                var base64Bytes = Convert.FromBase64String(model.DocumentContent);
                                var htmlContent = System.Text.Encoding.UTF8.GetString(base64Bytes);
                                
                                if (htmlContent.Contains("<") && htmlContent.Contains(">"))
                                {
                                    docxBytes = HtmlToDocxConverter.ConvertHtmlToDocx(htmlContent);
                                }
                            }
                            catch
                            {
                                // Konvertatsiya qilishda xatolik bo'lsa, mavjud docxBytes ishlatiladi
                            }

                            var newFile = new DOC_FILES
                            {
                                DocumentID = model.DocumentID,
                                VersionNo = maxVersion + 1,
                                FileName = $"{model.StandardCode}_v{model.NewVersionNo:F2}.docx",
                                FileExtension = ".docx",
                                MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                                FileSizeBytes = docxBytes.Length,
                                FileContent = docxBytes,
                                IsMain = true,
                                CreatedDT = DateTime.Now
                            };

                            db.DOC_FILES.Add(newFile);
                            await db.SaveChangesAsync();

                            // Word hujjatidan rasmlarni ajratib olish va saqlash
                            try
                            {
                                var images = WordDocumentImageService.ExtractImagesFromWord(docxBytes);
                                if (images.Any())
                                {
                                    var imageInfos = WordDocumentImageService.PrepareImagesForStorage(images, model.DocumentID, maxVersion + 1);
                                    foreach (var imageInfo in imageInfos)
                                    {
                                        var imageFile = new DOC_FILES
                                        {
                                            DocumentID = imageInfo.DocumentID,
                                            VersionNo = imageInfo.VersionNo,
                                            FileName = imageInfo.FileName,
                                            FileExtension = "." + imageInfo.FileExtension,
                                            MimeType = imageInfo.MimeType,
                                            FileSizeBytes = imageInfo.FileSizeBytes,
                                            FileContent = imageInfo.FileContent,
                                            IsMain = false,
                                            CreatedDT = imageInfo.CreatedDT
                                        };

                                        db.DOC_FILES.Add(imageFile);
                                    }
                                    await db.SaveChangesAsync();
                                }
                            }
                            catch (Exception imgEx)
                            {
                                // Rasmlarni ajratishda xatolik bo'lsa ham, asosiy fayl saqlangan bo'ladi
                                System.Diagnostics.Debug.WriteLine($"Rasmlarni ajratishda xatolik: {imgEx.Message}");
                            }
                        }
                    }

                    LogHelper.LogToDatabase(User.Identity.Name, "STDCompanyStandards", $"Standart yangilandi: {model.StandardCode} (v{model.NewVersionNo:F2})");
                    TempData["Success"] = "Standart muvaffaqiyatli yangilandi";

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Xatolik yuz berdi: {ex.Message}");
                return View(model);
            }
        }

        // Olish so'rovi: STDCompanyStandards/Details/5
        public async Task<ActionResult> Details(long? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var standard = await db.STD_COMPANY_STANDARDS
                    .Where(s => s.DocumentID == id && (s.IsDeleted == false || s.IsDeleted == null))
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
                    VersionDecimal = standard.VersionNo - (docxFiles.Count - 1 - index) * 0.01m, // Taxminiy versiya
                    CreatedDT = f.CreatedDT,
                    FileName = f.FileName,
                    FileID = f.ID
                }).ToList();

                // Rasmlarni olish (faqat rasm fayllar)
                var imageFiles = await db.DOC_FILES
                    .Where(f => f.DocumentID == id && 
                                f.FileExtension != ".docx" && 
                                (f.MimeType.StartsWith("image/") || 
                                 f.FileExtension == ".png" || 
                                 f.FileExtension == ".jpg" || 
                                 f.FileExtension == ".jpeg" || 
                                 f.FileExtension == ".gif" || 
                                 f.FileExtension == ".bmp"))
                    .OrderBy(f => f.CreatedDT)
                    .Select(f => new DocumentImageItem
                    {
                        FileID = f.ID,
                        FileName = f.FileName,
                        MimeType = f.MimeType,
                        FileSizeBytes = f.FileSizeBytes ?? 0,
                        VersionNo = f.VersionNo
                    })
                    .ToListAsync();

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
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var standard = await db.STD_COMPANY_STANDARDS
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
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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

                    LogHelper.LogToDatabase(User.Identity.Name, "STDCompanyStandards", $"Standart o'chirildi: {standardCode}");
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

        // Olish so'rovi: STDCompanyStandards/DownloadFile/5
        public async Task<ActionResult> DownloadFile(long? fileId)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

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

                // Rasmlar uchun ga'g'ri MIME type
                var mimeType = file.MimeType;
                if (string.IsNullOrEmpty(mimeType))
                {
                    // fayl extension dan MIME type ni aniqlash
                    mimeType = GetMimeTypeFromExtension(file.FileExtension);
                }

                // Rasmlar uchun inline ko'rsatish, boshqa fayllar uchun download
                var contentDisposition = file.FileExtension != null && 
                                         (file.FileExtension.ToLower() == ".png" || 
                                          file.FileExtension.ToLower() == ".jpg" || 
                                          file.FileExtension.ToLower() == ".jpeg" || 
                                          file.FileExtension.ToLower() == ".gif" || 
                                          file.FileExtension.ToLower() == ".bmp")
                    ? "inline" 
                    : "attachment";

                Response.AppendHeader("Content-Disposition", $"{contentDisposition}; filename=\"{file.FileName}\"");

                return File(file.FileContent, mimeType ?? "application/octet-stream");
            }
        }

        // Olish so'rovi: STDCompanyStandards/GetImage/5 - Rasmlarni ko'rsatish uchun
        public async Task<ActionResult> GetImage(long? fileId)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

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

                // Faqat rasm fayllar
                var isImage = file.MimeType != null && file.MimeType.StartsWith("image/");
                if (!isImage && file.FileExtension != null)
                {
                    var ext = file.FileExtension.ToLower();
                    isImage = ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp";
                }

                if (!isImage)
                {
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Bu fayl rasm emas");
                }

                var mimeType = file.MimeType ?? GetMimeTypeFromExtension(file.FileExtension);
                Response.AppendHeader("Content-Disposition", $"inline; filename=\"{file.FileName}\"");

                return File(file.FileContent, mimeType);
            }
        }

        private string GetMimeTypeFromExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return "application/octet-stream";

            var ext = extension.ToLower().TrimStart('.');
            var mimeTypes = new Dictionary<string, string>
            {
                { "png", "image/png" },
                { "jpg", "image/jpeg" },
                { "jpeg", "image/jpeg" },
                { "gif", "image/gif" },
                { "bmp", "image/bmp" },
                { "docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
                { "doc", "application/msword" },
                { "pdf", "application/pdf" }
            };

            return mimeTypes.ContainsKey(ext) ? mimeTypes[ext] : "application/octet-stream";
        }

        // yordamchi methods
        private int GetOrCreateDocumentTypeID(DBTHSNEntities db, string typeName)
        {
            var docType = db.DOC_DOCUMENT_TYPES.FirstOrDefault(dt => dt.Name == typeName);
            if (docType == null)
            {
                docType = new DOC_DOCUMENT_TYPES
                {
                    Code = typeName.ToUpper().Replace(" ", "_"),
                    Name = typeName,
                    IsActive = true,
                    SortOrder = 0
                };
                db.DOC_DOCUMENT_TYPES.Add(docType);
                db.SaveChanges();
            }
            return docType.ID;
        }

        private int GetOrCreateStatusID(DBTHSNEntities db, string statusName)
        {
            var status = db.DOC_DOCUMENT_STATUS_REF.FirstOrDefault(s => s.Name == statusName);
            if (status == null)
            {
                status = new DOC_DOCUMENT_STATUS_REF
                {
                    Code = statusName.ToUpper().Replace(" ", "_"),
                    Name = statusName,
                    IsActive = true,
                    SortOrder = 0
                };
                db.DOC_DOCUMENT_STATUS_REF.Add(status);
                db.SaveChanges();
            }
            return status.ID;
        }

        private string GenerateDocumentCode(DBTHSNEntities db)
        {
            var year = DateTime.Now.Year;
            var count = db.DOC_DOCUMENTS.Count(d => d.CreatedDT.Year == year) + 1;
            return $"STD-{year}-{count:D6}";
        }
    }
}

