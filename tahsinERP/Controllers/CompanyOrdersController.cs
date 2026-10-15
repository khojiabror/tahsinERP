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
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    public class CompanyOrdersController : Controller
    {
        private readonly IDocumentTemplateService _templateService;
        private readonly ITextSuggestionService _textSuggestionService;

        public CompanyOrdersController()
        {
            _templateService = new DocumentTemplateService();
            _textSuggestionService = new TextSuggestionService();
        }

        // Olish so'rovi: CompanyOrders
        public async Task<ActionResult> Index(int? year, string orderTypeCode, string searchTerm)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "CompanyOrders", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var query = from doc in db.DOC_DOCUMENTS
                            join order in db.ADM_COMPANY_ORDERS on doc.ID equals order.DocumentID
                            join docType in db.DOC_DOCUMENT_TYPES on doc.DocTypeID equals docType.ID
                            where docType.Code == "COMP_ORDER" && (doc.IsDeleted == false || doc.IsDeleted == null) && (order.IsDeleted == false || order.IsDeleted == null)
                            select new { doc, order, docType };

                if (year.HasValue)
                {
                    query = query.Where(x => x.order.OrderDate.Year == year.Value);
                }

                if (!string.IsNullOrEmpty(orderTypeCode))
                {
                    query = query.Where(x => x.order.OrderTypeCode == orderTypeCode);
                }

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(x => x.order.OrderNo.Contains(searchTerm) || 
                                             x.doc.Title.Contains(searchTerm) ||
                                             (x.order.Notes != null && x.order.Notes.Contains(searchTerm)));
                }

                var results = await query
                    .OrderByDescending(x => x.order.OrderDate)
                    .ThenByDescending(x => x.doc.ID)
                    .ToListAsync();

                var viewModel = results.Select(x => new CompanyOrderIndexVm
                {
                    DocumentId = x.doc.ID,
                    OrderNo = x.order.OrderNo,
                    OrderDate = x.order.OrderDate,
                    OrderTypeCode = x.order.OrderTypeCode,
                    OrderTypeName = x.order.OrderTypeCode,
                    OrgUnitName = x.doc.HR_ORG_UNITS != null ? x.doc.HR_ORG_UNITS.Name : null,
                    StatusName = x.doc.DOC_DOCUMENT_STATUS_REF != null ? x.doc.DOC_DOCUMENT_STATUS_REF.Name : null,
                    CreatedDT = x.doc.CreatedDT
                }).ToList();

                ViewBag.Year = year;
                ViewBag.OrderTypeCode = orderTypeCode;
                ViewBag.SearchTerm = searchTerm;
                ViewBag.Years = new SelectList(await db.ADM_COMPANY_ORDERS
                    .Select(o => o.OrderDate.Year)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToListAsync(), year);
                ViewBag.OrderTypes = new SelectList(new[] { "GENERAL", "URGENT", "SPECIAL" }, orderTypeCode);

                return View(viewModel);
            }
        }

        // Olish so'rovi: CompanyOrders/yaratish
        public async Task<ActionResult> Create()
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "CompanyOrders", "Create"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var vm = new CompanyOrderCreateVm
                {
                    OrderDate = DateTime.Now,
                    OrderNo = GenerateNextOrderNo(db)
                };

                vm.OrderTypes = new SelectList(new[] { "GENERAL", "URGENT", "SPECIAL" });
                vm.OrgUnits = new SelectList(await db.HR_ORG_UNITS
                    .Where(o => o.IsActive)
                    .OrderBy(o => o.Name)
                    .ToListAsync(), "ID", "Name");
                vm.Employees = new SelectList(await db.USERS
                    .Where(u => u.IsActive && u.IsDeleted != true)
                    .OrderBy(u => u.FullName)
                    .ToListAsync(), "ID", "FullName");

                return View(vm);
            }
        }

        // POST so'rovi: CompanyOrders/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(CompanyOrderCreateVm vm)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "CompanyOrders", "Create"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    var docType = await db.DOC_DOCUMENT_TYPES.FirstOrDefaultAsync(dt => dt.Code == "COMP_ORDER");
                    var statusDraft = await db.DOC_DOCUMENT_STATUS_REF.FirstOrDefaultAsync(s => s.Code == "DRAFT");

                    if (docType == null || statusDraft == null)
                    {
                        TempData["ErrorMessage"] = "Hujjat turi yoki holati topilmadi. Iltimos, seed skriptlarini ishga tushiring.";
                        return RedirectToAction("Index");
                    }

                    var document = new DOC_DOCUMENTS
                    {
                        DocTypeID = docType.ID,
                        StatusID = statusDraft.ID,
                        Code = vm.OrderNo,
                        Title = vm.Title,
                        OrgUnitID = vm.OrgUnitId,
                        CreatedByUserID = UserHelper.GetUserId(User.Identity.Name),
                        CreatedDT = DateTime.Now,
                        IntegrationUID = Guid.NewGuid()
                    };

                    db.DOC_DOCUMENTS.Add(document);
                    await db.SaveChangesAsync();

                    var order = new ADM_COMPANY_ORDERS
                    {
                        DocumentID = document.ID,
                        OrderNo = vm.OrderNo,
                        OrderDate = vm.OrderDate,
                        OrderTypeCode = vm.OrderTypeCode,
                        OrgUnitID = vm.OrgUnitId,
                        Notes = vm.BodyText
                    };

                    db.ADM_COMPANY_ORDERS.Add(order);
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "CompanyOrders", "Create", $"Buyruq yaratildi: {vm.OrderNo}");

                    TempData["Success"] = "Buyruq muvaffaqiyatli yaratildi";
                    return RedirectToAction("Index");
                }
            }

            using (var db = new DBTHSNEntities())
            {
                vm.OrderTypes = new SelectList(new[] { "GENERAL", "URGENT", "SPECIAL" });
                vm.OrgUnits = new SelectList(await db.HR_ORG_UNITS
                    .Where(o => o.IsActive)
                    .OrderBy(o => o.Name)
                    .ToListAsync(), "ID", "Name", vm.OrgUnitId);
                vm.Employees = new SelectList(await db.USERS
                    .Where(u => u.IsActive && u.IsDeleted != true)
                    .OrderBy(u => u.FullName)
                    .ToListAsync(), "ID", "FullName");
            }

            return View(vm);
        }

        // Olish so'rovi: CompanyOrders/Edit/5
        public async Task<ActionResult> Edit(long? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "CompanyOrders", "Edit"))
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
                    .Where(d => d.ID == id && (d.IsDeleted == false || d.IsDeleted == null))
                    .Include(d => d.ADM_COMPANY_ORDERS)
                    .FirstOrDefaultAsync();

                if (doc == null || doc.ADM_COMPANY_ORDERS == null || (doc.ADM_COMPANY_ORDERS.IsDeleted == true))
                {
                    return HttpNotFound();
                }

                var order = doc.ADM_COMPANY_ORDERS;
                var vm = new CompanyOrderEditVm
                {
                    DocumentId = doc.ID,
                    OrderNo = order.OrderNo,
                    OrderDate = order.OrderDate,
                    OrderTypeCode = order.OrderTypeCode,
                    OrgUnitId = order.OrgUnitID,
                    Title = doc.Title,
                    BodyText = order.Notes
                };

                vm.OrderTypes = new SelectList(new[] { "GENERAL", "URGENT", "SPECIAL" }, order.OrderTypeCode);
                vm.OrgUnits = new SelectList(await db.HR_ORG_UNITS
                    .Where(o => o.IsActive)
                    .OrderBy(o => o.Name)
                    .ToListAsync(), "ID", "Name", order.OrgUnitID);
                vm.Employees = new SelectList(await db.USERS
                    .Where(u => u.IsActive && u.IsDeleted != true)
                    .OrderBy(u => u.FullName)
                    .ToListAsync(), "ID", "FullName");

                return View(vm);
            }
        }

        // POST so'rovi: CompanyOrders/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(CompanyOrderEditVm vm)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "CompanyOrders", "Edit"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    var doc = await db.DOC_DOCUMENTS
                        .Include(d => d.ADM_COMPANY_ORDERS)
                        .FirstOrDefaultAsync(d => d.ID == vm.DocumentId);

                    if (doc == null || doc.ADM_COMPANY_ORDERS == null)
                    {
                        return HttpNotFound();
                    }

                    doc.Title = vm.Title;
                    doc.OrgUnitID = vm.OrgUnitId;
                    doc.UpdatedDT = DateTime.Now;

                    var order = doc.ADM_COMPANY_ORDERS;
                    order.OrderNo = vm.OrderNo;
                    order.OrderDate = vm.OrderDate;
                    order.OrderTypeCode = vm.OrderTypeCode;
                    order.OrgUnitID = vm.OrgUnitId;
                    order.Notes = vm.BodyText;

                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "CompanyOrders", "Edit", $"Buyruq yangilandi: {vm.OrderNo}");

                    TempData["Success"] = "Buyruq muvaffaqiyatli yangilandi";
                    return RedirectToAction("Index");
                }
            }

            using (var db = new DBTHSNEntities())
            {
                vm.OrderTypes = new SelectList(new[] { "GENERAL", "URGENT", "SPECIAL" }, vm.OrderTypeCode);
                vm.OrgUnits = new SelectList(await db.HR_ORG_UNITS
                    .Where(o => o.IsActive)
                    .OrderBy(o => o.Name)
                    .ToListAsync(), "ID", "Name", vm.OrgUnitId);
                vm.Employees = new SelectList(await db.USERS
                    .Where(u => u.IsActive && u.IsDeleted != true)
                    .OrderBy(u => u.FullName)
                    .ToListAsync(), "ID", "FullName");
            }

            return View(vm);
        }

        // Olish so'rovi: CompanyOrders/Details/5
        public async Task<ActionResult> Details(long? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "CompanyOrders", "Details"))
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
                    .Include(d => d.ADM_COMPANY_ORDERS)
                    .Include(d => d.HR_ORG_UNITS)
                    .Include(d => d.DOC_DOCUMENT_STATUS_REF)
                    .Include(d => d.DOC_FILES)
                    .FirstOrDefaultAsync(d => d.ID == id);

                if (doc == null || doc.ADM_COMPANY_ORDERS == null)
                {
                    return HttpNotFound();
                }

                return View(doc);
            }
        }

        // POST so'rovi: CompanyOrders/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(long id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "CompanyOrders", "Delete"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS
                    .Include(d => d.ADM_COMPANY_ORDERS)
                    .Include(d => d.DOC_FILES)
                    .FirstOrDefaultAsync(d => d.ID == id);

                if (doc != null)
                {
                    await SoftDeleteReferenceService.AfterDocDocumentSoftDeletedAsync(db, doc.ID);

                    // So'ng soft o'chirish the company order record
                    if (doc.ADM_COMPANY_ORDERS != null)
                    {
                        doc.ADM_COMPANY_ORDERS.IsDeleted = true;
                        db.Entry(doc.ADM_COMPANY_ORDERS).State = System.Data.Entity.EntityState.Modified;
                    }

                    // Oxirida, soft o'chirish the hujjat record
                    doc.IsDeleted = true;
                    db.Entry(doc).State = System.Data.Entity.EntityState.Modified;
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(User.Identity.Name, "CompanyOrders", "Delete", $"Buyruq o'chirildi: {doc.Code}");

                    TempData["Success"] = "Buyruq muvaffaqiyatli o'chirildi";
                }
            }

            return RedirectToAction("Index");
        }

        // POST so'rovi: CompanyOrders/GenerateDoc/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GenerateDoc(long id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "CompanyOrders", "GenerateDoc"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS
                    .Include(d => d.ADM_COMPANY_ORDERS)
                    .Include(d => d.HR_ORG_UNITS)
                    .Include(d => d.DOC_TEMPLATES)
                    .FirstOrDefaultAsync(d => d.ID == id);

                if (doc == null || doc.ADM_COMPANY_ORDERS == null)
                {
                    return HttpNotFound();
                }

                var order = doc.ADM_COMPANY_ORDERS;
                var template = await db.DOC_TEMPLATES
                    .FirstOrDefaultAsync(t => t.DocTypeID == doc.DocTypeID && t.IsActive);

                if (template == null)
                {
                    TempData["ErrorMessage"] = "Shablon topilmadi";
                    return RedirectToAction("Details", new { id });
                }

                // Notes endi mini Word muharriridan HTML bo'lishi mumkin - oddiy matnga aylantiramiz
                var notesText = OfficialLetterDataService.HtmlToPlainText(order.Notes);

                // Tayyorlash placeholders - matching template structure
                var placeholders = new Dictionary<string, string>
                {
                    { "ORDER_NO", order.OrderNo }, // Template uses ORDER_NO
                    { "ORDER NO", order.OrderNo }, // Also support space version
                    { "ORDER_DATE", order.OrderDate.ToString("dd.MM.yyyy") },
                    { "Order_date", order.OrderDate.ToString("dd.MM.yyyy") }, // Support lowercase variant
                    { "ORDER_TOPIC", doc.Title ?? "" }, // Template uses ORDER_TOPIC, not ORDER_TITLE
                    { "ORDER_BODY", notesText }, // Full body text without splitting
                    { "ORG_UNIT_NAME", doc.HR_ORG_UNITS?.Name ?? "" },
                    { "DIRECTOR_NAME", "X.M. Maxmudov" } // TODO: Get dan settings yoki user jadval
                };

                // Tahlil qilish Notes into individual content items (ORDER_DECREE_1_CONTENT_1, ORDER_DECREE_1_CONTENT_2, etc.)
                // Split by newlines va filtrlash out empty lines
                if (!string.IsNullOrEmpty(notesText))
                {
                    var lines = notesText.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.Trim())
                        .ToList();

                    // O'chirish existing numbering patterns (1., 2., 3., etc.) dan the beginning of har bir line
                    for (int i = 0; i < lines.Count; i++)
                    {
                        var line = lines[i];
                        // O'chirish patterns like "1.", "1)", "1-", etc. at the start
                        line = System.Text.RegularExpressions.Regex.Replace(line, @"^\s*\d+[\.\)\-\s]+\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        lines[i] = line.Trim();
                    }

                    // Qo'shish har bir line as ORDER_DECREE_1_CONTENT_1, ORDER_DECREE_1_CONTENT_2, etc.
                    // Template uses ORDER_DECREE_1_CONTENT_N format
                    for (int i = 0; i < lines.Count; i++)
                    {
                        string key = $"ORDER_DECREE_1_CONTENT_{i + 1}";
                        placeholders[key] = lines[i];
                    }
                }

                // Yaratish hujjat
                string fileName, fileExtension, mimeType;
                byte[] fileContent = _templateService.GenerateDocFromTemplate(
                    template.ID, placeholders, out fileName, out fileExtension, out mimeType);

                // Olish next version number
                int nextVersion = 1;
                var existingFiles = await db.DOC_FILES
                    .Where(f => f.DocumentID == doc.ID)
                    .OrderByDescending(f => f.VersionNo)
                    .FirstOrDefaultAsync();
                if (existingFiles != null)
                {
                    nextVersion = existingFiles.VersionNo + 1;
                    // Belgilash previous main fayl as not main
                    var prevMain = await db.DOC_FILES
                        .Where(f => f.DocumentID == doc.ID && f.IsMain)
                        .ToListAsync();
                    foreach (var prev in prevMain)
                    {
                        prev.IsMain = false;
                    }
                }

                // Saqlash fayl - use order date instead of current date
                var docFile = new DOC_FILES
                {
                    DocumentID = doc.ID,
                    VersionNo = nextVersion,
                    FileName = $"{order.OrderNo}_{order.OrderDate:yyyyMMdd}{fileExtension}",
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

                LogHelper.LogToDatabase(User.Identity.Name, "CompanyOrders", "GenerateDoc", $"Buyruq uchun hujjat yaratildi: {order.OrderNo}");

                TempData["Success"] = "Hujjat muvaffaqiyatli yaratildi";
                return RedirectToAction("Details", new { id });
            }
        }

        // POST so'rovi: CompanyOrders/ChangeStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangeStatus(long id, string statusCode)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "CompanyOrders", "ChangeStatus"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS
                    .Include(d => d.ADM_COMPANY_ORDERS)
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

                // agar approving, set ApprovedByUserID
                if (statusCode == "APPROVED")
                {
                    var currentUser = await db.USERS.FirstOrDefaultAsync(u => u.Uname == User.Identity.Name);
                    if (currentUser != null)
                    {
                        doc.ApprovedByUserID = currentUser.ID;
                    }
                }

                await db.SaveChangesAsync();

                LogHelper.LogToDatabase(User.Identity.Name, "CompanyOrders", "ChangeStatus", 
                    $"Buyruq holati '{status.Name}' ga o'zgartirildi: {doc.ADM_COMPANY_ORDERS?.OrderNo}");

                TempData["Success"] = $"Holat '{status.Name}' ga o'zgartirildi";
                return RedirectToAction("Details", new { id });
            }
        }

        // Olish so'rovi: CompanyOrders/DownloadFile/5
        public async Task<ActionResult> DownloadFile(long fileId)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "CompanyOrders", "DownloadFile"))
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

        // AJAX so'rovi: Suggest text
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SuggestText(string orderTypeCode, string orderDate, int? orgUnitId, string topic, string[] employeeNames)
        {
            try
            {
                string orgUnitName = null;
                if (orgUnitId.HasValue)
                {
                    using (var db = new DBTHSNEntities())
                    {
                        var orgUnit = db.HR_ORG_UNITS.Find(orgUnitId.Value);
                        orgUnitName = orgUnit?.Name;
                    }
                }

                DateTime parsedDate = DateTime.Now;
                if (!string.IsNullOrEmpty(orderDate))
                {
                    if (DateTime.TryParse(orderDate, out DateTime tempDate))
                    {
                        parsedDate = tempDate;
                    }
                }

                var meta = new CompanyOrderMeta
                {
                    OrderTypeCode = orderTypeCode ?? string.Empty,
                    OrderDate = parsedDate,
                    OrgUnitName = orgUnitName,
                    ResponsibleEmployeeNames = employeeNames?.ToList() ?? new List<string>(),
                    Topic = topic ?? string.Empty
                };

                var suggestedText = _textSuggestionService.SuggestOrderBody(meta);
                return Json(new { success = true, text = suggestedText }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        private string GenerateNextOrderNo(DBTHSNEntities db)
        {
            // Olish barcha order numbers va find the maksimal numeric value
            var allOrders = db.ADM_COMPANY_ORDERS
                .Where(o => o.IsDeleted != true)
                .Select(o => o.OrderNo)
                .ToList();

            int maxOrderNo = 0;
            
            foreach (var orderNo in allOrders)
            {
                // Urinish ga tahlil as pure number (e.g., "0001", "0043")
                if (int.TryParse(orderNo, out int num))
                {
                    if (num > maxOrderNo)
                        maxOrderNo = num;
                }
                // Urinish ga tahlil as year + number (e.g., "20250001")
                else if (orderNo.Length >= 4 && int.TryParse(orderNo.Substring(4), out int yearNum))
                {
                    if (yearNum > maxOrderNo)
                        maxOrderNo = yearNum;
                }
            }

            // Qaytarish next number in format "0044", "0045", etc.
            return (maxOrderNo + 1).ToString("D4");
        }
    }
}

