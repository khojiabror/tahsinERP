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
using tahsinERP.Services.Common;
using tahsinERP.Services.Documents;
using tahsinERP.I18n;

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
            // Avval ruxsat tekshiruvi yo'q edi (HRM-S06)
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "JobDesc", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

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
                ModelState.AddModelError("JobDescriptionCode", L.T("Hr_JdCodeRequired"));
            }

            if (string.IsNullOrWhiteSpace(model.DocumentContent))
            {
                ModelState.AddModelError("DocumentContent", L.T("Hr_JdContentEmpty"));
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
                // Hujjat + JOB_DESC + fayl — bitta tranzaksiyada (HRM-M02); kod qulf ostida (HRM-B17)
                using (var tx = db.Database.BeginTransaction())
                {
                    // DOC_DOCUMENTS — tur/holat kod bo'yicha (avval nom bo'yicha qidirilib, nom o'zgarsa yangi tur yaratilardi)
                    int typeId = await DocTemplateStore.EnsureDocTypeIdAsync(db, JobDescriptionType);
                    int statusId = await DocumentService.EnsureStatusIdAsync(db, ActiveStatusCode);
                    var document = DocumentService.NewHeader(typeId, statusId, await GenerateDocumentCodeAsync(db),
                        model.JobDescriptionCode, RoleHelper.GetUserId(User.Identity.Name));

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
                        await DocFileService.AddMainVersionAsync(db, document.ID, docxBytes, $"{model.JobDescriptionCode}_v1.00.docx",
                            ".docx", DocFileService.DocxMime, RoleHelper.GetUserId(User.Identity.Name), versionNo: 1);
                        await db.SaveChangesAsync();
                    }

                    tx.Commit();
                    LogHelper.LogToDatabase(User.Identity.Name, "JobDesc", $"Yangi lavozim yo'riqnomasi yaratildi: {model.JobDescriptionCode}");
                    TempData["Success"] = L.T("Hr_JdCreated");

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", L.T("Hr_ErrorOccurred", ex.Message));
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

                // Eng so'nggi asosiy .docx (o'chirilganlar hisobga olinmaydi)
                long? latestFileId = await DocFileService.LatestMainFileIdAsync(db, id.Value, ".docx");
                var latestFile = latestFileId == null ? null : await db.DOC_FILE_CONTENTS.AsNoTracking()
                    .Where(f => f.ID == latestFileId.Value)
                    .Select(f => new { f.ID, f.FileContent })
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
                ModelState.AddModelError("JobDescriptionCode", L.T("Hr_JdCodeRequired"));
            }

            if (!ModelState.IsValid)
            {
                LoadHRJobDescriptions();
                return View(model);
            }

            try
            {
                using (var db = new DBTHSNEntities())
                // Metadata va yangi fayl versiyasi bitta tranzaksiyada (avval fayl xatosida metadata yangilanib qolardi)
                using (var tx = db.Database.BeginTransaction())
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
                            // Keyingi VersionNo SQL'da, avvalgi IsMain bitta UPDATE bilan (blob'lar yuklanmaydi)
                            await DocFileService.AddMainVersionAsync(db, model.DocumentID, docxBytes,
                                $"{model.JobDescriptionCode}_v{model.NewVersionNo:F2}.docx", ".docx", DocFileService.DocxMime,
                                RoleHelper.GetUserId(User.Identity.Name));
                            await db.SaveChangesAsync();
                        }
                    }

                    tx.Commit();
                    LogHelper.LogToDatabase(User.Identity.Name, "JobDesc", $"Lavozim yo'riqnomasi yangilandi: {model.JobDescriptionCode} (v{model.NewVersionNo:F2})");
                    TempData["Success"] = L.T("Hr_JdUpdated");

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", L.T("Hr_ErrorOccurred", ex.Message));
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

                // Versiya tarixi — metadata proyeksiyasi, blob'lar o'qilmaydi (avval har versiya fayli to'liq yuklanardi)
                var files = await DocFileService.ListVersionsAsync(db, id.Value, ".docx");

                var versionHistory = new List<VersionHistoryItem>();
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    versionHistory.Add(new VersionHistoryItem
                    {
                        VersionNo = file.VersionNo,
                        // Ro'yxat yangisidan boshlanadi (avval eng yangisiga eng kichik raqam chiqardi)
                        VersionDecimal = jobDescription.VersionNo - i * 0.01m,
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
                    TempData["Success"] = L.T("Hr_JdDeleted");

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = L.T("Hr_ErrorOccurred", ex.Message);
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
                    var hrJobDescriptions = db.HR_JOB_DESCRIPTIONS.AsNoTracking()
                        .Select(h => new { h.DocumentID, PositionName = h.HR_POSITIONS.Name })
                        .ToList()
                        .Select(h => new SelectListItem
                        {
                            Value = h.DocumentID.ToString(),
                            Text = h.PositionName ?? L.T("Hr_Position") + " #" + h.DocumentID
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

        private const string JobDescriptionType = "JOB_DESCRIPTION";
        private const string ActiveStatusCode = "Active";

        /// <summary>
        /// JD-YYYY-NNNNNN: shu prefiksli kodlarning eng kattasi + 1, tranzaksiya oxirigacha qulf ostida (HRM-B17).
        /// Avval yilning BARCHA hujjatlari soni + 1 olinardi — o'chirishdan keyin va parallel yaratishda takrorlanardi.
        /// </summary>
        private static Task<string> GenerateDocumentCodeAsync(DBTHSNEntities db)
        {
            return DocumentNumberService.NextAsync(db, "DOC_DOCUMENTS", "Code", $"JD-{DateTime.Now.Year}-", 6);
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
                return await DocFileResult.ForAsync(db, await DocFileAccess.FindAsync(db, fileId.Value, DocFileAccess.Owner.JobDescription));
            }
        }
    }
}

