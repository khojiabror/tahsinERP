using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Services;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Job Descriptions CRUD - JOB_DESC jadvali uchun
    /// </summary>
    public class JobDescController : Controller
    {
        // Olish so'rovi: JobDesc
        public async Task<ActionResult> Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var jobDescriptions = await db.JOB_DESC
                    .Where(s => (s.IsDeleted == false || s.IsDeleted == null) && (s.DOC_DOCUMENTS == null || s.DOC_DOCUMENTS.IsDeleted == false || s.DOC_DOCUMENTS.IsDeleted == null))
                    .Include(s => s.DOC_DOCUMENTS)
                    .OrderByDescending(s => s.DOC_DOCUMENTS.CreatedDT)
                    .Select(s => new JobDescIndexViewModel
                    {
                        DocumentID = s.DocumentID,
                        JobDescriptionCode = s.JobDescriptionCode,
                        JobTitle = s.JobTitle,
                        Department = s.Department,
                        VersionNo = s.VersionNo,
                        Notes = s.Notes,
                        DocumentTitle = s.DOC_DOCUMENTS.Title,
                        CreatedDT = s.DOC_DOCUMENTS.CreatedDT,
                        HRJobDescriptionID = s.HRJobDescriptionID
                    })
                    .ToListAsync();

                return View(jobDescriptions);
            }
        }

        // Olish so'rovi: JobDesc/yaratish
        public ActionResult Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Template .docx faylini yuklash
            var templatePath = Server.MapPath("~/App_Data/Templates/JobDescriptionTemplate.docx");
            ViewBag.HasTemplate = System.IO.File.Exists(templatePath);
            ViewBag.TemplatePath = templatePath;

            // HR Job Descriptions ro'yxatini yuklash
            LoadHRJobDescriptions();

            return View();
        }

        // Olish so'rovi: JobDesc/GetTemplate
        public ActionResult GetTemplate()
        {
            var templatePath = Server.MapPath("~/App_Data/Templates/JobDescriptionTemplate.docx");
            if (System.IO.File.Exists(templatePath))
            {
                return File(templatePath, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "JobDescriptionTemplate.docx");
            }
            else
            {
                // Bo'sh .docx fayl yaratish
                var emptyDocx = WordDocumentConverterService.CreateEmptyDocx();
                return File(emptyDocx, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "JobDescriptionTemplate.docx");
            }
        }

        // POST so'rovi: JobDesc/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(JobDescCreateViewModel model)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (string.IsNullOrWhiteSpace(model.JobDescriptionCode))
            {
                ModelState.AddModelError("JobDescriptionCode", "Lavozim kodi kiritilishi shart");
            }

            if (string.IsNullOrWhiteSpace(model.DocumentContent))
            {
                ModelState.AddModelError("DocumentContent", "Hujjat kontenti bo'sh bo'lishi mumkin emas");
            }

            if (!ModelState.IsValid)
            {
                var templatePath = Server.MapPath("~/App_Data/Templates/JobDescriptionTemplate.docx");
                ViewBag.HasTemplate = System.IO.File.Exists(templatePath);
                ViewBag.TemplatePath = templatePath;
                LoadHRJobDescriptions();
                return View(model);
            }

            try
            {
                using (var db = new DBTHSNEntities())
                {
                    // DOC_DOCUMENTS yaratish
                    var document = new DOC_DOCUMENTS
                    {
                        DocTypeID = GetOrCreateDocumentTypeID(db, "JobDescription"),
                        StatusID = GetOrCreateStatusID(db, "Active"),
                        Code = GenerateDocumentCode(db),
                        Title = model.JobDescriptionCode,
                        CreatedDT = DateTime.Now,
                        IntegrationUID = Guid.NewGuid()
                    };

                    db.DOC_DOCUMENTS.Add(document);
                    await db.SaveChangesAsync();

                    // JOB_DESC yaratish
                    var jobDescription = new JOB_DESC
                    {
                        DocumentID = document.ID,
                        JobDescriptionCode = model.JobDescriptionCode.Trim(),
                        JobTitle = model.JobTitle?.Trim(),
                        Department = model.Department?.Trim(),
                        VersionNo = 1.00m,
                        Notes = model.Notes?.Trim(),
                        HRJobDescriptionID = model.HRJobDescriptionID
                    };

                    db.JOB_DESC.Add(jobDescription);
                    await db.SaveChangesAsync();

                    // DOC_FILES yaratish (.docx fayl)
                    byte[] docxBytes = null;
                    if (!string.IsNullOrEmpty(model.DocumentContent))
                    {
                        // HTML kontentni .docx ga konvertatsiya qilish
                        try
                        {
                            docxBytes = WordDocumentConverterService.ConvertHtmlToDocx(model.DocumentContent);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"HTML dan DOCX ga o'tkazishda xatolik: {ex.Message}");
                            // Agar konvertatsiya xatolik bersa, bo'sh .docx yaratish
                            docxBytes = WordDocumentConverterService.CreateEmptyDocx();
                        }
                    }

                    if (docxBytes != null && docxBytes.Length > 0)
                    {
                        var docFile = new DOC_FILES
                        {
                            DocumentID = document.ID,
                            VersionNo = 1,
                            FileName = $"{model.JobDescriptionCode}_v1.00.docx",
                            FileExtension = ".docx",
                            MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                            FileSizeBytes = docxBytes.Length,
                            FileContent = docxBytes,
                            IsMain = true,
                            CreatedDT = DateTime.Now
                        };

                        db.DOC_FILES.Add(docFile);
                        await db.SaveChangesAsync();
                    }

                    LogHelper.LogToDatabase(User.Identity.Name, "JobDesc", $"Yangi lavozim yo'riqnomasi yaratildi: {model.JobDescriptionCode}");
                    TempData["Success"] = "Lavozim yo'riqnomasi muvaffaqiyatli yaratildi";

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Xatolik yuz berdi: {ex.Message}");
                var templatePath = Server.MapPath("~/App_Data/Templates/JobDescriptionTemplate.docx");
                ViewBag.HasTemplate = System.IO.File.Exists(templatePath);
                ViewBag.TemplatePath = templatePath;
                LoadHRJobDescriptions();
                return View(model);
            }
        }

        // Olish so'rovi: JobDesc/Edit/5
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
                var jobDescription = await db.JOB_DESC
                    .Include(s => s.DOC_DOCUMENTS)
                    .FirstOrDefaultAsync(s => s.DocumentID == id);

                if (jobDescription == null)
                {
                    return HttpNotFound();
                }

                // Eng so'nggi versiya faylini olish
                var latestFile = await db.DOC_FILES
                    .Where(f => f.DocumentID == id && f.IsMain)
                    .OrderByDescending(f => f.VersionNo)
                    .FirstOrDefaultAsync();

                // .docx ni HTML ga konvertatsiya qilish (editor uchun)
                string existingHtmlContent = string.Empty;
                if (latestFile?.FileContent != null)
                {
                    try
                    {
                        existingHtmlContent = WordDocumentConverterService.ConvertDocxToHtml(latestFile.FileContent);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"DOCX dan HTML ga o'tkazishda xatolik: {ex.Message}");
                    }
                }

                var viewModel = new JobDescEditViewModel
                {
                    DocumentID = jobDescription.DocumentID,
                    JobDescriptionCode = jobDescription.JobDescriptionCode,
                    JobTitle = jobDescription.JobTitle,
                    Department = jobDescription.Department,
                    CurrentVersionNo = jobDescription.VersionNo,
                    NewVersionNo = jobDescription.VersionNo + 0.01m,
                    Notes = jobDescription.Notes,
                    DocumentContent = existingHtmlContent,
                    HRJobDescriptionID = jobDescription.HRJobDescriptionID
                };

                // DocumentFileId ni ViewBag ga qo'shish (Edit.cshtml uchun)
                if (latestFile != null)
                {
                    ViewBag.DocumentFileId = latestFile.ID;
                }

                LoadHRJobDescriptions();
                return View(viewModel);
            }
        }

        // POST so'rovi: JobDesc/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(JobDescEditViewModel model)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (string.IsNullOrWhiteSpace(model.JobDescriptionCode))
            {
                ModelState.AddModelError("JobDescriptionCode", "Lavozim kodi kiritilishi shart");
            }

            if (!ModelState.IsValid)
            {
                LoadHRJobDescriptions();
                return View(model);
            }

            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var jobDescription = await db.JOB_DESC
                        .Include(s => s.DOC_DOCUMENTS)
                        .FirstOrDefaultAsync(s => s.DocumentID == model.DocumentID);

                    if (jobDescription == null)
                    {
                        return HttpNotFound();
                    }

                    // Versiya raqamini yangilash
                    jobDescription.JobDescriptionCode = model.JobDescriptionCode.Trim();
                    jobDescription.JobTitle = model.JobTitle?.Trim();
                    jobDescription.Department = model.Department?.Trim();
                    jobDescription.VersionNo = model.NewVersionNo;
                    jobDescription.Notes = model.Notes?.Trim();
                    jobDescription.HRJobDescriptionID = model.HRJobDescriptionID;

                    // hujjat title ni yangilash
                    if (jobDescription.DOC_DOCUMENTS != null)
                    {
                        jobDescription.DOC_DOCUMENTS.Title = model.JobDescriptionCode;
                        jobDescription.DOC_DOCUMENTS.UpdatedDT = DateTime.Now;
                    }

                    await db.SaveChangesAsync();

                    // Yangi versiya faylini saqlash
                    if (!string.IsNullOrEmpty(model.DocumentContent))
                    {
                        byte[] docxBytes = null;
                        try
                        {
                            docxBytes = WordDocumentConverterService.ConvertHtmlToDocx(model.DocumentContent);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"HTML dan DOCX ga o'tkazishda xatolik: {ex.Message}");
                            docxBytes = WordDocumentConverterService.CreateEmptyDocx();
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

                            var newFile = new DOC_FILES
                            {
                                DocumentID = model.DocumentID,
                                VersionNo = maxVersion + 1,
                                FileName = $"{model.JobDescriptionCode}_v{model.NewVersionNo:F2}.docx",
                                FileExtension = ".docx",
                                MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                                FileSizeBytes = docxBytes.Length,
                                FileContent = docxBytes,
                                IsMain = true,
                                CreatedDT = DateTime.Now
                            };

                            db.DOC_FILES.Add(newFile);
                            await db.SaveChangesAsync();
                        }
                    }

                    LogHelper.LogToDatabase(User.Identity.Name, "JobDesc", $"Lavozim yo'riqnomasi yangilandi: {model.JobDescriptionCode} (v{model.NewVersionNo:F2})");
                    TempData["Success"] = "Lavozim yo'riqnomasi muvaffaqiyatli yangilandi";

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Xatolik yuz berdi: {ex.Message}");
                LoadHRJobDescriptions();
                return View(model);
            }
        }

        // Olish so'rovi: JobDesc/Details/5
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
                var jobDescription = await db.JOB_DESC
                    .Where(s => s.DocumentID == id && (s.IsDeleted == false || s.IsDeleted == null))
                    .Include(s => s.DOC_DOCUMENTS)
                    .Include(s => s.HR_JOB_DESCRIPTIONS)
                    .FirstOrDefaultAsync();

                if (jobDescription == null)
                {
                    return HttpNotFound();
                }

                // Versiya tarixini olish
                var files = await db.DOC_FILES
                    .Where(f => f.DocumentID == id)
                    .OrderByDescending(f => f.VersionNo)
                    .ToListAsync();

                var versionHistory = new List<VersionHistoryItem>();
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    versionHistory.Add(new VersionHistoryItem
                    {
                        VersionNo = file.VersionNo,
                        VersionDecimal = jobDescription.VersionNo - (files.Count - 1 - i) * 0.01m,
                        CreatedDT = file.CreatedDT,
                        FileName = file.FileName,
                        FileID = file.ID
                    });
                }

                var viewModel = new JobDescDetailsViewModel
                {
                    DocumentID = jobDescription.DocumentID,
                    JobDescriptionCode = jobDescription.JobDescriptionCode,
                    JobTitle = jobDescription.JobTitle,
                    Department = jobDescription.Department,
                    VersionNo = jobDescription.VersionNo,
                    Notes = jobDescription.Notes,
                    DocumentTitle = jobDescription.DOC_DOCUMENTS?.Title,
                    CreatedDT = jobDescription.DOC_DOCUMENTS?.CreatedDT ?? DateTime.Now,
                    UpdatedDT = jobDescription.DOC_DOCUMENTS?.UpdatedDT,
                    VersionHistory = versionHistory,
                    HasDocumentFile = versionHistory.Any(),
                    HRJobDescriptionID = jobDescription.HRJobDescriptionID,
                    HRJobDescriptionTitle = jobDescription.HR_JOB_DESCRIPTIONS?.DOC_DOCUMENTS?.Title
                };

                return View(viewModel);
            }
        }

        // Olish so'rovi: JobDesc/o'chirish/5
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
                var jobDescription = await db.JOB_DESC
                    .Include(s => s.DOC_DOCUMENTS)
                    .FirstOrDefaultAsync(s => s.DocumentID == id);

                if (jobDescription == null)
                {
                    return HttpNotFound();
                }

                return View(jobDescription);
            }
        }

        // POST so'rovi: JobDesc/o'chirish/5
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
                    var jobDescription = await db.JOB_DESC
                        .Include(s => s.DOC_DOCUMENTS)
                        .FirstOrDefaultAsync(s => s.DocumentID == id);

                    if (jobDescription == null)
                    {
                        return HttpNotFound();
                    }

                    var jobDescriptionCode = jobDescription.JobDescriptionCode;

                    // DOC_DOCUMENTS ni soft o'chirish qilish
                    if (jobDescription.DOC_DOCUMENTS != null)
                    {
                        jobDescription.DOC_DOCUMENTS.IsDeleted = true;
                        db.Entry(jobDescription.DOC_DOCUMENTS).State = System.Data.Entity.EntityState.Modified;
                        await SoftDeleteReferenceService.AfterDocDocumentSoftDeletedAsync(db, jobDescription.DocumentID);
                    }
                    else
                    {
                        jobDescription.IsDeleted = true;
                        db.Entry(jobDescription).State = System.Data.Entity.EntityState.Modified;
                    }

                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "JobDesc", $"Lavozim yo'riqnomasi o'chirildi: {jobDescriptionCode}");
                    TempData["Success"] = "Lavozim yo'riqnomasi muvaffaqiyatli o'chirildi";

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Xatolik yuz berdi: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // yordamchi methods
        private void LoadHRJobDescriptions()
        {
            using (var db = new DBTHSNEntities())
            {
                try
                {
                    // HR_JOB_DESCRIPTIONS va HR_POSITIONS dan ma'lumotlarni olish
                    var hrJobDescriptions = db.HR_JOB_DESCRIPTIONS
                        .Include(h => h.HR_POSITIONS)
                        .ToList()
                        .Select(h => new SelectListItem
                        {
                            Value = h.DocumentID.ToString(),
                            Text = h.HR_POSITIONS != null ? h.HR_POSITIONS.Name : $"Lavozim #{h.DocumentID}"
                        })
                        .OrderBy(h => h.Text)
                        .ToList();

                    ViewBag.HRJobDescriptions = new SelectList(hrJobDescriptions, "Value", "Text");
                }
                catch (Exception ex)
                {
                    // Xatolikni log qilish
                    System.Diagnostics.Debug.WriteLine($"LoadHRJobDescriptions xatolik: {ex.Message}");
                    ViewBag.HRJobDescriptions = new SelectList(new List<SelectListItem>(), "Value", "Text");
                }
            }
        }

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
            return $"JD-{year}-{count:D6}";
        }

        // Olish so'rovi: JobDesc/DownloadFile/5
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
                var file = await db.DOC_FILES.FindAsync(fileId);
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
    }
}

