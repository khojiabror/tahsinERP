using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Services;
using tahsinERP.Controllers;

namespace tahsinERP.Controllers
{
    public class OfficialLettersController : Controller
    {
        private readonly IDocumentTemplateService _templateService;
        private readonly ITextSuggestionService _textSuggestionService;

        public OfficialLettersController()
        {
            _templateService = new DocumentTemplateService();
            _textSuggestionService = new TextSuggestionService();
        }

        // GET: OfficialLetters
        public async Task<ActionResult> Index(int? year, string directionCode, string topicSearch)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "OfficialLetters", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var query = from doc in db.DOC_DOCUMENTS
                            join letter in db.COM_OFFICIAL_LETTERS on doc.ID equals letter.DocumentID
                            join docType in db.DOC_DOCUMENT_TYPES on doc.DocTypeID equals docType.ID
                            where docType.Code == "OFFICIAL_LETTER"
                            select new { doc, letter, docType };

                if (year.HasValue)
                {
                    query = query.Where(x => x.letter.LetterDate.Year == year.Value);
                }

                if (!string.IsNullOrEmpty(directionCode))
                {
                    query = query.Where(x => x.letter.DirectionCode == directionCode);
                }

                if (!string.IsNullOrEmpty(topicSearch))
                {
                    query = query.Where(x => x.letter.Topic.Contains(topicSearch) ||
                                             x.letter.ExternalCompanyName.Contains(topicSearch));
                }

                var results = await query
                    .OrderByDescending(x => x.letter.LetterDate)
                    .ThenByDescending(x => x.doc.ID)
                    .ToListAsync();

                var viewModel = results.Select(x => new OfficialLetterIndexVm
                {
                    DocumentId = x.doc.ID,
                    LetterNo = x.letter.LetterNo,
                    LetterDate = x.letter.LetterDate,
                    DirectionCode = x.letter.DirectionCode,
                    ExternalCompanyName = x.letter.ExternalCompanyName,
                    ExternalContactName = x.letter.ExternalContactName,
                    Topic = x.letter.Topic,
                    StatusName = x.doc.DOC_DOCUMENT_STATUS_REF != null ? x.doc.DOC_DOCUMENT_STATUS_REF.Name : null,
                    CreatedDT = x.doc.CreatedDT
                }).ToList();

                ViewBag.Year = year;
                ViewBag.DirectionCode = directionCode;
                ViewBag.TopicSearch = topicSearch;
                ViewBag.Years = new SelectList(await db.COM_OFFICIAL_LETTERS
                    .Select(l => l.LetterDate.Year)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToListAsync(), year);
                ViewBag.Directions = new SelectList(new[] { new { Code = "OUT", Name = "Chiqish" }, new { Code = "IN", Name = "Kirish" } }, "Code", "Name", directionCode);

                return View(viewModel);
            }
        }

        // GET: OfficialLetters/Create
        public ActionResult Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "Create"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var vm = new OfficialLetterCreateVm
            {
                LetterDate = DateTime.Now,
                DirectionCode = "OUT",
                LetterNo = GenerateNextLetterNo()
            };

            vm.Directions = new SelectList(new[] { new { Code = "OUT", Name = "Chiqish" }, new { Code = "IN", Name = "Kirish" } }, "Code", "Name", "OUT");
            vm.Channels = new SelectList(new[] { "EMAIL", "LETTER", "COURIER", "FAX" }, "EMAIL");

            return View(vm);
        }

        // POST: OfficialLetters/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(OfficialLetterCreateVm vm)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "Create"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    var docType = await db.DOC_DOCUMENT_TYPES.FirstOrDefaultAsync(dt => dt.Code == "OFFICIAL_LETTER");
                    var statusDraft = await db.DOC_DOCUMENT_STATUS_REF.FirstOrDefaultAsync(s => s.Code == "DRAFT");

                    if (docType == null || statusDraft == null)
                    {
                        TempData["ErrorMessage"] = "Document type or status not found. Please run seed scripts.";
                        return RedirectToAction("Index");
                    }

                    var document = new DOC_DOCUMENTS
                    {
                        DocTypeID = docType.ID,
                        StatusID = statusDraft.ID,
                        Code = vm.LetterNo,
                        Title = vm.Topic,
                        CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                        CreatedDT = DateTime.Now,
                        IntegrationUID = Guid.NewGuid()
                    };

                    db.DOC_DOCUMENTS.Add(document);
                    await db.SaveChangesAsync();

                    var letter = new COM_OFFICIAL_LETTERS
                    {
                        DocumentID = document.ID,
                        DirectionCode = vm.DirectionCode,
                        LetterNo = vm.LetterNo,
                        LetterDate = vm.LetterDate,
                        ExternalCompanyName = vm.ExternalCompanyName,
                        ExternalContactName = vm.ExternalContactName,
                        ExternalContactPosition = vm.ExternalContactPosition,
                        OurRefNo = vm.OurRefNo,
                        TheirRefNo = vm.TheirRefNo,
                        ChannelCode = vm.ChannelCode,
                        Topic = vm.Topic
                    };

                    db.COM_OFFICIAL_LETTERS.Add(letter);
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "OfficialLetters", "Create", $"Letter {vm.LetterNo} created");

                    TempData["Success"] = "Maktub muvaffaqiyatli yaratildi";
                    return RedirectToAction("Index");
                }
            }

            vm.Directions = new SelectList(new[] { new { Code = "OUT", Name = "Chiqish" }, new { Code = "IN", Name = "Kirish" } }, "Code", "Name", vm.DirectionCode);
            vm.Channels = new SelectList(new[] { "EMAIL", "LETTER", "COURIER", "FAX" }, vm.ChannelCode);

            return View(vm);
        }

        // GET: OfficialLetters/Edit/5
        public async Task<ActionResult> Edit(long? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "Edit"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS
                    .Include(d => d.COM_OFFICIAL_LETTERS)
                    .FirstOrDefaultAsync(d => d.ID == id);

                if (doc == null || doc.COM_OFFICIAL_LETTERS == null)
                {
                    return HttpNotFound();
                }

                var letter = doc.COM_OFFICIAL_LETTERS;
                var vm = new OfficialLetterEditVm
                {
                    DocumentId = doc.ID,
                    DirectionCode = letter.DirectionCode,
                    LetterNo = letter.LetterNo,
                    LetterDate = letter.LetterDate,
                    ExternalCompanyName = letter.ExternalCompanyName,
                    ExternalContactName = letter.ExternalContactName,
                    ExternalContactPosition = letter.ExternalContactPosition,
                    OurRefNo = letter.OurRefNo,
                    TheirRefNo = letter.TheirRefNo,
                    ChannelCode = letter.ChannelCode,
                    Topic = letter.Topic,
                    GreetingLine = $"Hurmatli {letter.ExternalContactName ?? "hamkor"}!",
                    BodyText = doc.Title // Store body in Title or add new field
                };

                vm.Directions = new SelectList(new[] { new { Code = "OUT", Name = "Chiqish" }, new { Code = "IN", Name = "Kirish" } }, "Code", "Name", letter.DirectionCode);
                vm.Channels = new SelectList(new[] { "EMAIL", "LETTER", "COURIER", "FAX" }, letter.ChannelCode);

                return View(vm);
            }
        }

        // POST: OfficialLetters/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(OfficialLetterEditVm vm)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "Edit"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    var doc = await db.DOC_DOCUMENTS
                        .Include(d => d.COM_OFFICIAL_LETTERS)
                        .FirstOrDefaultAsync(d => d.ID == vm.DocumentId);

                    if (doc == null || doc.COM_OFFICIAL_LETTERS == null)
                    {
                        return HttpNotFound();
                    }

                    doc.Title = vm.Topic;
                    doc.UpdatedDT = DateTime.Now;

                    var letter = doc.COM_OFFICIAL_LETTERS;
                    letter.DirectionCode = vm.DirectionCode;
                    letter.LetterNo = vm.LetterNo;
                    letter.LetterDate = vm.LetterDate;
                    letter.ExternalCompanyName = vm.ExternalCompanyName;
                    letter.ExternalContactName = vm.ExternalContactName;
                    letter.ExternalContactPosition = vm.ExternalContactPosition;
                    letter.OurRefNo = vm.OurRefNo;
                    letter.TheirRefNo = vm.TheirRefNo;
                    letter.ChannelCode = vm.ChannelCode;
                    letter.Topic = vm.Topic;

                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "OfficialLetters", "Edit", $"Letter {vm.LetterNo} updated");

                    TempData["Success"] = "Maktub muvaffaqiyatli yangilandi";
                    return RedirectToAction("Index");
                }
            }

            vm.Directions = new SelectList(new[] { new { Code = "OUT", Name = "Chiqish" }, new { Code = "IN", Name = "Kirish" } }, "Code", "Name", vm.DirectionCode);
            vm.Channels = new SelectList(new[] { "EMAIL", "LETTER", "COURIER", "FAX" }, vm.ChannelCode);

            return View(vm);
        }

        // GET: OfficialLetters/Details/5
        public async Task<ActionResult> Details(long? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "OfficialLetters", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS
                    .Include(d => d.COM_OFFICIAL_LETTERS)
                    .Include(d => d.DOC_DOCUMENT_STATUS_REF)
                    .Include(d => d.DOC_FILES)
                    .FirstOrDefaultAsync(d => d.ID == id);

                if (doc == null || doc.COM_OFFICIAL_LETTERS == null)
                {
                    return HttpNotFound();
                }

                return View(doc);
            }
        }

        // POST: OfficialLetters/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(long id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "Delete"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS
                    .Include(d => d.COM_OFFICIAL_LETTERS)
                    .Include(d => d.DOC_FILES)
                    .FirstOrDefaultAsync(d => d.ID == id);

                if (doc != null)
                {
                    // First, delete all related DOC_FILES
                    if (doc.DOC_FILES != null && doc.DOC_FILES.Any())
                    {
                        var files = doc.DOC_FILES.ToList();
                        foreach (var file in files)
                        {
                            db.DOC_FILES.Remove(file);
                        }
                    }

                    // Then delete the official letter record
                    if (doc.COM_OFFICIAL_LETTERS != null)
                    {
                        db.COM_OFFICIAL_LETTERS.Remove(doc.COM_OFFICIAL_LETTERS);
                    }

                    // Finally, delete the document record
                    db.DOC_DOCUMENTS.Remove(doc);
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "OfficialLetters", "Delete", $"Letter {doc.Code} deleted");

                    TempData["Success"] = "Maktub muvaffaqiyatli o'chirildi";
                }
            }

            return RedirectToAction("Index");
        }

        // POST: OfficialLetters/GenerateDoc/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GenerateDoc(long id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "GenerateDoc"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS
                    .Include(d => d.COM_OFFICIAL_LETTERS)
                    .FirstOrDefaultAsync(d => d.ID == id);

                if (doc == null || doc.COM_OFFICIAL_LETTERS == null)
                {
                    return HttpNotFound();
                }

                var letter = doc.COM_OFFICIAL_LETTERS;
                var template = await db.DOC_TEMPLATES
                    .FirstOrDefaultAsync(t => t.DocTypeID == doc.DocTypeID && t.IsActive);

                if (template == null)
                {
                    TempData["ErrorMessage"] = "Template topilmadi";
                    return RedirectToAction("Details", new { id });
                }

                // Prepare placeholders
                var recipientBlock = letter.ExternalCompanyName ?? "";
                if (!string.IsNullOrEmpty(letter.ExternalContactPosition))
                {
                    recipientBlock += "\n" + letter.ExternalContactPosition;
                }
                if (!string.IsNullOrEmpty(letter.ExternalContactName))
                {
                    recipientBlock += "\n" + letter.ExternalContactName;
                }

                var placeholders = new Dictionary<string, string>
                {
                    { "LETTER_NO", letter.LetterNo },
                    { "LETTER_DATE", letter.LetterDate.ToString("dd.MM.yyyy") },
                    { "RECIPIENT_BLOCK", recipientBlock },
                    { "GREETING_LINE", $"Hurmatli {letter.ExternalContactName ?? "hamkor"}!" },
                    { "LETTER_BODY", doc.Title ?? "" },
                    { "ITEM_NAME_1", "" },
                    { "ITEM_DIM_1", "" },
                    { "ITEM_PRICE_1", "" },
                    { "ITEM_PRICE_VAT_1", "" },
                    { "DIRECTOR_NAME", "X.M. Maxmudov" }, // TODO: Get from settings
                    { "OUR_REF_NO", letter.OurRefNo ?? "" },
                    { "THEIR_REF_NO", letter.TheirRefNo ?? "" }
                };

                // Generate document
                string fileName, fileExtension, mimeType;
                byte[] fileContent = _templateService.GenerateDocFromTemplate(
                    template.ID, placeholders, out fileName, out fileExtension, out mimeType);

                // Get next version number
                int nextVersion = 1;
                var existingFiles = await db.DOC_FILES
                    .Where(f => f.DocumentID == doc.ID)
                    .OrderByDescending(f => f.VersionNo)
                    .FirstOrDefaultAsync();
                if (existingFiles != null)
                {
                    nextVersion = existingFiles.VersionNo + 1;
                    // Mark previous main file as not main
                    var prevMain = await db.DOC_FILES
                        .Where(f => f.DocumentID == doc.ID && f.IsMain)
                        .ToListAsync();
                    foreach (var prev in prevMain)
                    {
                        prev.IsMain = false;
                    }
                }

                // Save file - use letter date instead of current date
                var docFile = new DOC_FILES
                {
                    DocumentID = doc.ID,
                    VersionNo = nextVersion,
                    FileName = $"{letter.LetterNo}_{letter.LetterDate:yyyyMMdd}{fileExtension}",
                    FileExtension = fileExtension,
                    MimeType = mimeType,
                    FileSizeBytes = fileContent.Length,
                    FileContent = fileContent,
                    IsMain = true,
                    CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                    CreatedDT = DateTime.Now
                };

                db.DOC_FILES.Add(docFile);
                await db.SaveChangesAsync();

                LogHelper.LogToDatabase(User.Identity.Name, "OfficialLetters", "GenerateDoc", $"Document generated for letter {letter.LetterNo}");

                TempData["Success"] = "Hujjat muvaffaqiyatli yaratildi";
                return RedirectToAction("Details", new { id });
            }
        }

        // POST: OfficialLetters/ChangeStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangeStatus(long id, string statusCode)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "ChangeStatus"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS
                    .Include(d => d.COM_OFFICIAL_LETTERS)
                    .FirstOrDefaultAsync(d => d.ID == id);

                if (doc == null)
                {
                    return HttpNotFound();
                }

                var status = await db.DOC_DOCUMENT_STATUS_REF
                    .FirstOrDefaultAsync(s => s.Code == statusCode && s.IsActive);

                if (status == null)
                {
                    TempData["ErrorMessage"] = "Noto'g'ri holat kodi";
                    return RedirectToAction("Details", new { id });
                }

                doc.StatusID = status.ID;
                doc.UpdatedDT = DateTime.Now;

                // If approving, set ApprovedByUserID
                if (statusCode == "APPROVED")
                {
                    var currentUser = await db.USERS.FirstOrDefaultAsync(u => u.Uname == User.Identity.Name);
                    if (currentUser != null)
                    {
                        doc.ApprovedByUserID = currentUser.ID;
                    }
                }

                await db.SaveChangesAsync();

                LogHelper.LogToDatabase(User.Identity.Name, "OfficialLetters", "ChangeStatus", 
                    $"Status changed to {status.Name} for letter {doc.COM_OFFICIAL_LETTERS?.LetterNo}");

                TempData["Success"] = $"Holat '{status.Name}' ga o'zgartirildi";
                return RedirectToAction("Details", new { id });
            }
        }

        // GET: OfficialLetters/DownloadFile/5
        public async Task<ActionResult> DownloadFile(long fileId)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "OfficialLetters", "DownloadFile"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var file = await db.DOC_FILES.FindAsync(fileId);
                if (file == null || file.FileContent == null)
                {
                    return HttpNotFound();
                }

                return File(file.FileContent, file.MimeType ?? "application/octet-stream", file.FileName);
            }
        }

        // AJAX: Suggest text
        [HttpPost]
        public JsonResult SuggestText(string directionCode, string externalCompanyName, string externalContactName, string topic, string letterDate, string ourRefNo, string theirRefNo)
        {
            try
            {
                DateTime parsedDate = DateTime.Now;
                if (!string.IsNullOrEmpty(letterDate))
                {
                    if (DateTime.TryParse(letterDate, out DateTime tempDate))
                    {
                        parsedDate = tempDate;
                    }
                }

                var meta = new OfficialLetterMeta
                {
                    DirectionCode = directionCode ?? "OUT",
                    ExternalCompanyName = externalCompanyName ?? string.Empty,
                    ExternalContactName = externalContactName ?? string.Empty,
                    Topic = topic ?? string.Empty,
                    LetterDate = parsedDate,
                    OurRefNo = ourRefNo ?? string.Empty,
                    TheirRefNo = theirRefNo ?? string.Empty
                };

                var suggestedText = _textSuggestionService.SuggestLetterBody(meta);
                return Json(new { success = true, text = suggestedText }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        private string GenerateNextLetterNo()
        {
            using (var db = new DBTHSNEntities())
            {
                // Get all letter numbers and find the maximum numeric value
                var allLetters = db.COM_OFFICIAL_LETTERS
                    .Select(l => l.LetterNo)
                    .ToList();

                int maxLetterNo = 0;
                
                foreach (var letterNo in allLetters)
                {
                    // Try to parse as pure number (e.g., "0005", "0122")
                    if (int.TryParse(letterNo, out int num))
                    {
                        if (num > maxLetterNo)
                            maxLetterNo = num;
                    }
                    // Try to parse as year + number (e.g., "20250005")
                    else if (letterNo.Length >= 4 && int.TryParse(letterNo.Substring(4), out int yearNum))
                    {
                        if (yearNum > maxLetterNo)
                            maxLetterNo = yearNum;
                    }
                }

                // Return next number in format "0123", "0124", etc.
                return (maxLetterNo + 1).ToString("D4");
            }
        }

        // GET: OfficialLetters/RegisterIncoming
        public ActionResult RegisterIncoming()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "RegisterIncoming"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var vm = new IncomingLetterRegisterVm
            {
                LetterDate = DateTime.Now
            };

            return View(vm);
        }

        // POST: OfficialLetters/RegisterIncoming
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RegisterIncoming(IncomingLetterRegisterVm vm)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "OfficialLetters", "RegisterIncoming"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    // Get document type for OFFICIAL_LETTER
                    var docType = await db.DOC_DOCUMENT_TYPES
                        .FirstOrDefaultAsync(dt => dt.Code == "OFFICIAL_LETTER");

                    if (docType == null)
                    {
                        TempData["ErrorMessage"] = "Hujjat turi topilmadi";
                        return View(vm);
                    }

                    // Get DRAFT status
                    var statusDraft = await db.DOC_DOCUMENT_STATUS_REF
                        .FirstOrDefaultAsync(s => s.Code == "DRAFT" && s.IsActive);

                    if (statusDraft == null)
                    {
                        TempData["ErrorMessage"] = "Holat topilmadi";
                        return View(vm);
                    }

                    // Get current user
                    var currentUser = await db.USERS
                        .FirstOrDefaultAsync(u => u.Uname == User.Identity.Name);

                    // Create document
                    var doc = new DOC_DOCUMENTS
                    {
                        DocTypeID = docType.ID,
                        StatusID = statusDraft.ID,
                        Code = $"IN-{vm.LetterNo}",
                        Title = $"Kiruvchi xat: {vm.CompanyOrPersonName}",
                        CreatedDT = DateTime.Now,
                        IntegrationUID = Guid.NewGuid(),
                        CreatedByUserID = currentUser?.ID
                    };

                    db.DOC_DOCUMENTS.Add(doc);
                    await db.SaveChangesAsync();

                    // Create official letter record
                    var letter = new COM_OFFICIAL_LETTERS
                    {
                        DocumentID = doc.ID,
                        DirectionCode = "IN",
                        LetterNo = vm.LetterNo,
                        LetterDate = vm.LetterDate ?? DateTime.Now,
                        ExternalCompanyName = vm.CompanyOrPersonName,
                        ExternalContactName = null,
                        Topic = $"Kiruvchi xat: {vm.CompanyOrPersonName}"
                    };

                    db.COM_OFFICIAL_LETTERS.Add(letter);

                    // Handle file upload
                    if (vm.AttachmentFile != null && vm.AttachmentFile.ContentLength > 0)
                    {
                        // Validate file type (PDF only)
                        string fileExtension = System.IO.Path.GetExtension(vm.AttachmentFile.FileName).ToLower();
                        if (fileExtension != ".pdf")
                        {
                            ModelState.AddModelError("AttachmentFile", "Faqat PDF fayllarni yuklash mumkin");
                            return View(vm);
                        }

                        // Validate file size (max 10MB)
                        if (vm.AttachmentFile.ContentLength > 10 * 1024 * 1024)
                        {
                            ModelState.AddModelError("AttachmentFile", "Fayl hajmi 10 MB dan oshmasligi kerak");
                            return View(vm);
                        }

                        // Read file content
                        byte[] fileContent;
                        using (var binaryReader = new System.IO.BinaryReader(vm.AttachmentFile.InputStream))
                        {
                            fileContent = binaryReader.ReadBytes(vm.AttachmentFile.ContentLength);
                        }

                        // Save file to DOC_FILES
                        var docFile = new DOC_FILES
                        {
                            DocumentID = doc.ID,
                            VersionNo = 1,
                            FileName = vm.AttachmentFile.FileName,
                            FileExtension = fileExtension,
                            MimeType = vm.AttachmentFile.ContentType ?? "application/pdf",
                            FileSizeBytes = vm.AttachmentFile.ContentLength,
                            FileContent = fileContent,
                            IsMain = true,
                            CreatedByUserID = currentUser?.ID,
                            CreatedDT = DateTime.Now
                        };

                        db.DOC_FILES.Add(docFile);
                    }

                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "OfficialLetters", "RegisterIncoming", 
                        $"Kiruvchi xat ro'yxatga olindi: {vm.LetterNo} - {vm.CompanyOrPersonName}");

                    TempData["Success"] = "Kiruvchi xat muvaffaqiyatli ro'yxatga olindi";
                    return RedirectToAction("Index");
                }
            }

            return View(vm);
        }
    }
}

