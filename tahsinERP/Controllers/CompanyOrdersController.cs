using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Documents;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Korxona buyruqlari. Ruxsat — [Permission]: GET = ko'rish, POST/Create/Edit/Delete = o'zgartirish.
    /// </summary>
    [Permission]
    public class CompanyOrdersController : BaseController
    {
        private static readonly string[] OrderTypeItems = { "GENERAL", "URGENT", "SPECIAL" };

        // Servislar faqat kerak bo'lganda yaratiladi (avval har so'rovda, Index'da ham — yangi HttpClient bilan)
        private IDocumentTemplateService _templateService;
        private ITextSuggestionService _textSuggestionService;
        private IDocumentTemplateService TemplateService => _templateService ?? (_templateService = new DocumentTemplateService());
        private ITextSuggestionService TextSuggestion => _textSuggestionService ?? (_textSuggestionService = new TextSuggestionService());

        private static SelectList OrderTypes(string selected) => new SelectList(OrderTypeItems, selected);

        private static async Task<SelectList> OrgUnitsAsync(DBTHSNEntities db, int? selected)
        {
            var units = await db.HR_ORG_UNITS.AsNoTracking()
                .Where(o => o.IsActive)
                .OrderBy(o => o.Name)
                .Select(o => new { o.ID, o.Name })
                .ToListAsync();
            return new SelectList(units, "ID", "Name", selected);
        }

        // Olish so'rovi: CompanyOrders
        public async Task<ActionResult> Index(int? year, string orderTypeCode, string searchTerm)
        {
            using (var db = new DBTHSNEntities())
            {
                var query = from doc in db.DOC_DOCUMENTS.AsNoTracking()
                            join order in db.ADM_COMPANY_ORDERS on doc.ID equals order.DocumentID
                            where doc.DOC_DOCUMENT_TYPES.Code == DocumentService.CompanyOrderType && doc.IsDeleted != true && order.IsDeleted != true
                            select new { doc, order };

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

                // SQL'da proyeksiya — avval to'liq entity'lar olinib, bo'lim/status lazy-load qilinardi (DOC-P11)
                var viewModel = await query
                    .OrderByDescending(x => x.order.OrderDate)
                    .ThenByDescending(x => x.doc.ID)
                    .Select(x => new CompanyOrderIndexVm
                    {
                        DocumentId = x.doc.ID,
                        OrderNo = x.order.OrderNo,
                        OrderDate = x.order.OrderDate,
                        OrderTypeCode = x.order.OrderTypeCode,
                        OrderTypeName = x.order.OrderTypeCode,
                        OrgUnitName = x.doc.HR_ORG_UNITS.Name,
                        StatusName = x.doc.DOC_DOCUMENT_STATUS_REF.Name,
                        CreatedDT = x.doc.CreatedDT
                    })
                    .ToListAsync();

                ViewBag.Year = year;
                ViewBag.OrderTypeCode = orderTypeCode;
                ViewBag.SearchTerm = searchTerm;
                // O'chirilganlar yillari ko'rinmasin (DOC-B12)
                ViewBag.Years = new SelectList(await db.ADM_COMPANY_ORDERS
                    .Where(o => o.IsDeleted != true && o.DOC_DOCUMENTS.IsDeleted != true)
                    .Select(o => o.OrderDate.Year)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToListAsync(), year);
                ViewBag.OrderTypes = OrderTypes(orderTypeCode);

                return View(viewModel);
            }
        }

        // Olish so'rovi: CompanyOrders/yaratish
        public async Task<ActionResult> Create()
        {
            using (var db = new DBTHSNEntities())
            {
                var vm = new CompanyOrderCreateVm
                {
                    OrderDate = DateTime.Now,
                    // Taxminiy raqam; saqlashda qulf ostida qayta tekshiriladi
                    OrderNo = await DocNumbering.PeekOrderNoAsync(db)
                };

                vm.OrderTypes = OrderTypes(null);
                vm.OrgUnits = await OrgUnitsAsync(db, null);

                return View(vm);
            }
        }

        // POST so'rovi: CompanyOrders/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(CompanyOrderCreateVm vm)
        {
            using (var db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        using (var tx = db.Database.BeginTransaction())
                        {
                            var ids = await DocumentService.DraftHeaderIdsAsync(db, DocumentService.CompanyOrderType);
                            if (ids == null)
                            {
                                TempData["ErrorMessage"] = "Hujjat turi yoki holati topilmadi. Iltimos, seed skriptlarini ishga tushiring.";
                                return RedirectToAction("Index");
                            }

                            // Buyruq raqami qulf ostida: band bo'lsa keyingisi beriladi (DOC-B06)
                            string orderNo = await DocNumbering.EnsureOrderNoAsync(db, vm.OrderNo);

                            // Hujjat va buyruq bitta SaveChanges'da — yetim DOC_DOCUMENTS qolmaydi (DOC-B05)
                            db.DOC_DOCUMENTS.Add(new DOC_DOCUMENTS
                            {
                                DocTypeID = ids.Item1,
                                StatusID = ids.Item2,
                                Code = orderNo,
                                Title = vm.Title,
                                OrgUnitID = vm.OrgUnitId,
                                CreatedByUserID = CurrentUserId,
                                CreatedDT = DateTime.Now,
                                IntegrationUID = Guid.NewGuid(),
                                ADM_COMPANY_ORDERS = new ADM_COMPANY_ORDERS
                                {
                                    OrderNo = orderNo,
                                    OrderDate = vm.OrderDate,
                                    OrderTypeCode = vm.OrderTypeCode,
                                    OrgUnitID = vm.OrgUnitId,
                                    Notes = vm.BodyText
                                }
                            });

                            await db.SaveChangesAsync();
                            tx.Commit();

                            LogHelper.LogToDatabase(CurrentUserName, "CompanyOrders", "Create", $"Buyruq yaratildi: {orderNo}");

                            TempData["Success"] = orderNo == vm.OrderNo?.Trim()
                                ? "Buyruq muvaffaqiyatli yaratildi"
                                : $"Buyruq yaratildi. {vm.OrderNo} raqami band bo'lgani uchun {orderNo} raqami berildi";
                            return RedirectToAction("Index");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError("CompanyOrders Create: {0}", ex);
                        ModelState.AddModelError("", "Saqlashda xatolik yuz berdi: " + InnermostMessage(ex));
                    }
                }

                vm.OrderTypes = OrderTypes(vm.OrderTypeCode);
                vm.OrgUnits = await OrgUnitsAsync(db, vm.OrgUnitId);
            }

            return View(vm);
        }

        // Olish so'rovi: CompanyOrders/Edit/5
        public async Task<ActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS.AsNoTracking()
                    .Where(d => d.ID == id && d.IsDeleted != true)
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

                vm.OrderTypes = OrderTypes(order.OrderTypeCode);
                vm.OrgUnits = await OrgUnitsAsync(db, order.OrgUnitID);

                return View(vm);
            }
        }

        // POST so'rovi: CompanyOrders/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(CompanyOrderEditVm vm)
        {
            using (var db = new DBTHSNEntities())
            {
                if (ModelState.IsValid)
                {
                    var doc = await db.DOC_DOCUMENTS
                        .Include(d => d.ADM_COMPANY_ORDERS)
                        .FirstOrDefaultAsync(d => d.ID == vm.DocumentId && d.IsDeleted != true);

                    if (doc == null || doc.ADM_COMPANY_ORDERS == null || doc.ADM_COMPANY_ORDERS.IsDeleted == true)
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

                    LogHelper.LogToDatabase(CurrentUserName, "CompanyOrders", "Edit", $"Buyruq yangilandi: {vm.OrderNo}");

                    TempData["Success"] = "Buyruq muvaffaqiyatli yangilandi";
                    return RedirectToAction("Index");
                }

                vm.OrderTypes = OrderTypes(vm.OrderTypeCode);
                vm.OrgUnits = await OrgUnitsAsync(db, vm.OrgUnitId);
            }

            return View(vm);
        }

        // Olish so'rovi: CompanyOrders/Details/5
        public async Task<ActionResult> Details(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS.AsNoTracking()
                    .Include(d => d.ADM_COMPANY_ORDERS)
                    .Include(d => d.HR_ORG_UNITS)
                    .Include(d => d.DOC_DOCUMENT_STATUS_REF)
                    .FirstOrDefaultAsync(d => d.ID == id && d.IsDeleted != true);

                if (doc == null || doc.ADM_COMPANY_ORDERS == null || doc.ADM_COMPANY_ORDERS.IsDeleted == true)
                {
                    return HttpNotFound();
                }

                // Faqat metadata — avval Include(DOC_FILES) barcha versiyalar blob'ini o'qirdi (DOC-P01)
                ViewBag.DocFiles = await DocFileService.ListMainAsync(db, doc.ID);
                ViewBag.Templates = await DocTemplateStore.ListAsync(db, DocPlaceholders.OrderType, activeOnly: true);

                return View(doc);
            }
        }

        // POST so'rovi: CompanyOrders/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(long id)
        {
            using (var db = new DBTHSNEntities())
            {
                var code = await db.ADM_COMPANY_ORDERS
                    .Where(o => o.DocumentID == id && o.IsDeleted != true && o.DOC_DOCUMENTS.IsDeleted != true)
                    .Select(o => o.DOC_DOCUMENTS.Code)
                    .FirstOrDefaultAsync();

                if (code != null)
                {
                    // Set-based: hujjat, buyruq va fayllar blob'larini yuklamasdan (DOC-P06)
                    await DocumentService.SoftDeleteAsync(db, id);

                    LogHelper.LogToDatabase(CurrentUserName, "CompanyOrders", "Delete", $"Buyruq o'chirildi: {code}");

                    TempData["Success"] = "Buyruq muvaffaqiyatli o'chirildi";
                }
            }

            return RedirectToAction("Index");
        }

        // POST so'rovi: CompanyOrders/GenerateDoc/5 — templateId: Details'da tanlangan shablon
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GenerateDoc(long id, int? templateId)
        {
            using (var db = new DBTHSNEntities())
            {
                var doc = await db.DOC_DOCUMENTS
                    .Include(d => d.ADM_COMPANY_ORDERS)
                    .Include(d => d.HR_ORG_UNITS)
                    .FirstOrDefaultAsync(d => d.ID == id && d.IsDeleted != true);

                if (doc == null || doc.ADM_COMPANY_ORDERS == null || doc.ADM_COMPANY_ORDERS.IsDeleted == true)
                {
                    return HttpNotFound();
                }

                var order = doc.ADM_COMPANY_ORDERS;

                // Tanlangan (yoki yagona/eng yangi faol) shablon — shablonlar bazada (DOC_TEMPLATES)
                var resolvedTemplateId = await DocTemplateStore.ResolveActiveAsync(db, DocPlaceholders.OrderType, templateId);
                if (resolvedTemplateId == null)
                {
                    TempData["ErrorMessage"] = templateId.HasValue
                        ? "Tanlangan shablon topilmadi yoki arxivlangan."
                        : "Faol shablon yo'q. \"Shablonlar\" sahifasida Word shablon yuklang.";
                    return RedirectToAction("Details", new { id });
                }

                var data = DocTemplateDataBuilder.ForOrder(doc, order, doc.HR_ORG_UNITS?.Name, await CompanySettings.DirectorNameAsync(db));

                GeneratedDocument generated;
                try
                {
                    generated = TemplateService.Generate(resolvedTemplateId.Value, data);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("CompanyOrders GenerateDoc {0}: {1}", id, ex);
                    TempData["ErrorMessage"] = "Hujjat yaratib bo'lmadi: " +
                        (ex is InvalidOperationException ? ex.Message : "shablon faylini tekshiring (Word'da ochilishi kerak).");
                    return RedirectToAction("Details", new { id });
                }

                // Yangi asosiy versiya (VersionNo MAX va IsMain UPDATE SQL'da — blob'lar o'qilmaydi, DOC-P05)
                DOC_FILES file;
                using (var tx = db.Database.BeginTransaction())
                {
                    file = await DocFileService.AddMainVersionAsync(db, doc.ID, generated.Content,
                        $"{order.OrderNo}_{order.OrderDate:yyyyMMdd}" + generated.FileExtension,
                        generated.FileExtension, generated.MimeType, CurrentUserId);
                    doc.TemplateID = resolvedTemplateId.Value;
                    await db.SaveChangesAsync();
                    tx.Commit();
                }

                LogHelper.LogToDatabase(CurrentUserName, "CompanyOrders", "GenerateDoc", $"Buyruq uchun hujjat yaratildi: {order.OrderNo}");

                TempData["Success"] = $"Hujjat yaratildi (versiya {file.VersionNo}) va yuklab olinmoqda.";
                if (generated.UnknownPlaceholders.Count > 0)
                {
                    TempData["Warning"] = "Shablonda tanilmagan maydonlar o'zgarishsiz qoldi: " +
                        string.Join(", ", generated.UnknownPlaceholders.Select(p => "{{" + p + "}}")) +
                        ". To'g'ri nomlar \"Shablonlar\" sahifasida.";
                }
                // Details sahifasi faylni avtomatik yuklab beradi
                TempData["DownloadFileId"] = file.ID;
                return RedirectToAction("Details", new { id });
            }
        }

        // POST so'rovi: CompanyOrders/ChangeStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangeStatus(long id, string statusCode)
        {
            using (var db = new DBTHSNEntities())
            {
                // Hujjat turi, o'chirilmaganligi va ruxsat etilgan o'tish tekshiriladi (DOC-B08)
                var result = await DocumentService.ChangeStatusAsync(db, id, DocumentService.CompanyOrderType, statusCode, CurrentUserId);
                if (result == null)
                {
                    return HttpNotFound();
                }

                if (!result.Success)
                {
                    TempData["ErrorMessage"] = result.Error;
                    return RedirectToAction("Details", new { id });
                }

                LogHelper.LogToDatabase(CurrentUserName, "CompanyOrders", "ChangeStatus",
                    $"Buyruq holati '{result.StatusName}' ga o'zgartirildi: {result.DocumentCode}");

                TempData["Success"] = $"Holat '{result.StatusName}' ga o'zgartirildi";
                return RedirectToAction("Details", new { id });
            }
        }

        // Olish so'rovi: CompanyOrders/DownloadFile/5
        public async Task<ActionResult> DownloadFile(long fileId)
        {
            using (var db = new DBTHSNEntities())
            {
                var file = await DocFileAccess.FindAsync(db, fileId, DocFileAccess.Owner.CompanyOrder);
                if (file == null || file.FileContent == null)
                {
                    return HttpNotFound();
                }

                // DOC_FILES qatori o'zgarmaydi (yangi versiya = yangi ID) — brauzer keshlay oladi
                Response.Cache.SetCacheability(HttpCacheability.Private);
                Response.Cache.SetMaxAge(TimeSpan.FromDays(1));
                return File(file.FileContent, file.MimeType ?? "application/octet-stream", file.FileName);
            }
        }

        // AJAX so'rovi: Suggest text (ruxsat — [Permission], POST = o'zgartirish; avval tekshiruv yo'q edi, DOC-S04)
        [HttpPost]
        [ValidateAntiForgeryHeader]
        public async Task<JsonResult> SuggestText(string orderTypeCode, string orderDate, int? orgUnitId, string topic, string[] employeeNames)
        {
            try
            {
                string orgUnitName = null;
                if (orgUnitId.HasValue)
                {
                    using (var db = new DBTHSNEntities())
                    {
                        orgUnitName = await db.HR_ORG_UNITS
                            .Where(o => o.ID == orgUnitId.Value)
                            .Select(o => o.Name)
                            .FirstOrDefaultAsync();
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

                // Async — avval .Result bilan kutilardi (AI yoqilganda deadlock, DOC-B09)
                var suggestedText = await TextSuggestion.SuggestOrderBodyAsync(meta);
                return Json(new { success = true, text = suggestedText });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("CompanyOrders SuggestText: {0}", ex);
                return Json(new { success = false, error = "Matn taklif qilib bo'lmadi." });
            }
        }
    }
}
