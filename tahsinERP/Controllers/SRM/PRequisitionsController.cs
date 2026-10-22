using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.I18n;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.Services.Procurement;
using tahsinERP.ViewModels;
using tahsinERP.ViewModels.PRequisition;
using tahsinERP.Helpers;

namespace tahsinERP.Controllers.SRM
{
    public class PRequisitionsController : BaseController
    {
        private const string Module = "PRequisitions";

        // Olish so'rovi: PRequisitions — sahifa faqat filtr va jadval qobig'i; qatorlar IndexData orqali (SRM-P03)
        public ActionResult Index(string statusFilter = "")
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, Module, "Index"))
            {
                return NotAuthorized();
            }

            ViewBag.StatusFilter = statusFilter;
            ViewBag.ChangePermit = CanChange(Module);
            ViewBag.StatusList = new SelectList(new[]
            {
                new { Value = "", Text = L.T("All") },
                new { Value = "Draft", Text = L.T("Srm_ReqStatus_Draft") },
                new { Value = "Submitted", Text = L.T("Srm_ReqStatus_Submitted") },
                new { Value = "Approved", Text = L.T("Srm_ReqStatus_Approved") },
                new { Value = "Rejected", Text = L.T("Srm_ReqStatus_Rejected") },
                new { Value = "ConvertedToOrder", Text = L.T("Srm_ReqStatus_ConvertedToOrder") }
            }, "Value", "Text", statusFilter);
            return View();
        }

        /// <summary>Talabnomalar ro'yxati qatori (proyeksiya; qatorlar soni va summasi SQL'da).</summary>
        public sealed class PRequisitionIndexRow
        {
            public int ID { get; set; }
            public string RequisitionNo { get; set; }
            public DateTime RequisitionDate { get; set; }
            public string DepartmentName { get; set; }
            public string RequesterName { get; set; }
            public string Status { get; set; }
            public int LineCount { get; set; }
            public double? Total { get; set; }
        }

        private static IQueryable<PRequisitionIndexRow> BuildIndexQuery(DBTHSNEntities db, string statusFilter)
        {
            var query = db.P_REQUISITIONS.AsNoTracking().Where(r => r.IsDeleted != true);

            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(r => r.Status == statusFilter);
            }

            // Bo'lim va so'rovchi nomi — navigatsiya yo'q, korrelyatsiyalangan so'rov (SRM-B19)
            return query.Select(r => new PRequisitionIndexRow
            {
                ID = r.ID,
                RequisitionNo = r.RequisitionNo,
                RequisitionDate = r.RequisitionDate,
                DepartmentName = db.HR_ORG_UNITS.Where(d => d.ID == r.DepartmentID).Select(d => d.Name).FirstOrDefault(),
                RequesterName = db.HR_EMPLOYEES.Where(e => e.ID == r.RequesterID).Select(e => e.FullName).FirstOrDefault(),
                Status = r.Status,
                LineCount = r.P_REQUISITION_PRODUCTS.Count(),
                Total = (double?)(r.P_REQUISITION_PRODUCTS.Sum(p => p.Amount))
            });
        }

        private static IQueryable<PRequisitionIndexRow> SearchIndex(IQueryable<PRequisitionIndexRow> query, string s)
        {
            return query.Where(r => r.RequisitionNo.Contains(s) || r.DepartmentName.Contains(s)
                || r.RequesterName.Contains(s) || r.Status.Contains(s));
        }

        private static IOrderedQueryable<PRequisitionIndexRow> OrderIndex(IQueryable<PRequisitionIndexRow> query, int column, bool desc)
        {
            switch (column)
            {
                case 0: return ServerTablePage.By(query, r => r.RequisitionNo, desc).ThenByDescending(r => r.ID);
                case 2: return ServerTablePage.By(query, r => r.DepartmentName, desc).ThenByDescending(r => r.ID);
                case 3: return ServerTablePage.By(query, r => r.RequesterName, desc).ThenByDescending(r => r.ID);
                case 4: return ServerTablePage.By(query, r => r.Status, desc).ThenByDescending(r => r.ID);
                case 5: return ServerTablePage.By(query, r => r.LineCount, desc).ThenByDescending(r => r.ID);
                case 6: return ServerTablePage.By(query, r => r.Total, desc).ThenByDescending(r => r.ID);
                default: return desc ? query.OrderByDescending(r => r.RequisitionDate).ThenByDescending(r => r.ID)
                                     : query.OrderBy(r => r.RequisitionDate).ThenBy(r => r.ID);
            }
        }

        // Olish so'rovi: PRequisitions/IndexData (DataTables server-side)
        [HttpGet]
        public async Task<ActionResult> IndexData(string statusFilter = "")
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, Module, "Index"))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 1);
            using (var db = new DBTHSNEntities())
            {
                var result = await ServerTablePage.LoadAsync(BuildIndexQuery(db, statusFilter), page, SearchIndex, OrderIndex);

                return Json(new
                {
                    draw = page.Draw,
                    recordsTotal = result.Total,
                    recordsFiltered = result.Filtered,
                    data = result.Rows.Select(r => new
                    {
                        id = r.ID,
                        requisitionNo = r.RequisitionNo,
                        date = r.RequisitionDate.ToString("dd.MM.yyyy"),
                        department = r.DepartmentName,
                        requester = r.RequesterName,
                        status = r.Status,
                        lineCount = r.LineCount,
                        total = (r.Total ?? 0).ToString("N2")
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // Olish so'rovi: PRequisitions/ExportExcel — filtr va qidiruv bo'yicha barcha yozuvlar
        [HttpGet]
        public async Task<ActionResult> ExportExcel(string statusFilter = "", string q = null)
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, Module, "Index"))
            {
                return NotAuthorized();
            }

            using (var db = new DBTHSNEntities())
            {
                var query = BuildIndexQuery(db, statusFilter);
                if (!string.IsNullOrWhiteSpace(q))
                {
                    query = SearchIndex(query, q.Trim());
                }

                var rows = await OrderIndex(query, 1, true).ToListAsync();
                var bytes = ExcelExport.Create(L.T("Menu_PurchaseRequests"), rows,
                    ExcelExport.Col<PRequisitionIndexRow>(L.T("Srm_RequisitionNo"), r => r.RequisitionNo),
                    ExcelExport.Col<PRequisitionIndexRow>(L.T("Date"), r => r.RequisitionDate, "dd.MM.yyyy"),
                    ExcelExport.Col<PRequisitionIndexRow>(L.T("Srm_Department"), r => r.DepartmentName),
                    ExcelExport.Col<PRequisitionIndexRow>(L.T("Srm_Requester"), r => r.RequesterName),
                    ExcelExport.Col<PRequisitionIndexRow>(L.T("Status"), r => RequisitionText.Status(r.Status)),
                    ExcelExport.Col<PRequisitionIndexRow>(L.T("Srm_ProductCount"), r => r.LineCount),
                    ExcelExport.Col<PRequisitionIndexRow>(L.T("Srm_TotalAmount"), r => r.Total ?? 0, "#,##0.00"));

                return File(bytes, ExcelExport.ContentType, L.T("Srm_FilePurchaseRequisitions") + $"_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
        }

        // Olish so'rovi: PRequisitions/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, Module, "Details"))
            {
                return NotAuthorized();
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var requisition = await db.P_REQUISITIONS
                    .AsNoTracking()
                    .Include(r => r.P_REQUISITION_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(r => r.P_REQUISITION_PRODUCTS.Select(p => p.UNIT))
                    .Include(r => r.P_REQUISITION_APPROVALS)
                    .FirstOrDefaultAsync(r => r.ID == id && r.IsDeleted != true);

                if (requisition == null)
                {
                    return HttpNotFound();
                }

                // Faqat shu talabnomaning bo'limi va so'rovchisi (avval barcha faol bo'lim/xodimlar yuklanardi)
                ViewBag.Departments = await db.HR_ORG_UNITS.AsNoTracking()
                    .Where(d => d.ID == requisition.DepartmentID)
                    .ToListAsync();
                ViewBag.Requesters = await db.HR_EMPLOYEES.AsNoTracking()
                    .Where(e => e.ID == requisition.RequesterID)
                    .ToListAsync();

                return View(requisition);
            }
        }

        // Olish so'rovi: PRequisitions/yaratish
        public async Task<ActionResult> Create()
        {
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            using (var db = new DBTHSNEntities())
            {
                await PopulateFormLookupsAsync(db, null);
                return View();
            }
        }

        // POST so'rovi: PRequisitions/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(PRequisitionViewModel model)
        {
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    try
                    {
                        P_REQUISITIONS requisition = null;

                        // Sarlavha, qatorlar va tasdiqlash bosqichlari — bitta tranzaksiyada (SRM-B12)
                        using (var tx = db.Database.BeginTransaction())
                        {
                            var number = await ResolveRequisitionNoAsync(db, model.RequisitionNo);
                            if (number != null)
                            {
                                requisition = new P_REQUISITIONS
                                {
                                    RequisitionNo = number,
                                    RequisitionDate = model.RequisitionDate,
                                    DepartmentID = model.DepartmentID,
                                    RequesterID = model.RequesterID,
                                    ContactPhone = model.ContactPhone,
                                    Purpose = model.Purpose,
                                    // Holat mijozdan olinmaydi: yangi talabnoma doim qoralama
                                    Status = "Draft",
                                    IsDeleted = false,
                                    CreatedByUserID = CurrentUserId,
                                    CreatedDT = DateTime.Now
                                };
                                db.P_REQUISITIONS.Add(requisition);

                                foreach (var product in model.RequisitionProducts ?? new List<PRequisitionProductViewModel>())
                                {
                                    requisition.P_REQUISITION_PRODUCTS.Add(new P_REQUISITION_PRODUCTS
                                    {
                                        ProductID = product.ProductID,
                                        Quantity = (decimal)product.Quantity,
                                        UnitID = product.UnitID,
                                        EstimatedPrice = (decimal?)product.EstimatedPrice,
                                        Amount = (decimal?)(product.EstimatedPrice.HasValue ? product.EstimatedPrice.Value * product.Quantity : (double?)null),
                                        Priority = product.Priority ?? "Medium",
                                        RequiredDate = product.RequiredDate,
                                        Description = product.Description
                                    });
                                }

                                AddDefaultApprovalWorkflow(requisition);

                                await db.SaveChangesAsync();
                                tx.Commit();
                            }
                        }

                        if (requisition != null)
                        {
                            LogHelper.LogToDatabase(CurrentUserName, Module, $"Create - {requisition.RequisitionNo} talabnoma yaratildi");
                            return RedirectToAction("Index");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddDbErrors(ex);
                    }
                }
            }

            using (var db = new DBTHSNEntities())
            {
                await PopulateFormLookupsAsync(db, model);
            }

            return View(model);
        }

        // Olish so'rovi: PRequisitions/ConvertToOrder/5
        public async Task<ActionResult> ConvertToOrder(int? id)
        {
            if (!RoleHelper.IsChangePermitted(CurrentUserName, Module, "ConvertToOrder"))
            {
                return NotAuthorized();
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var status = await db.P_REQUISITIONS
                    .Where(r => r.ID == id && r.IsDeleted != true)
                    .Select(r => r.Status)
                    .FirstOrDefaultAsync();

                if (status == null)
                {
                    return HttpNotFound();
                }

                if (status != "Approved")
                {
                    TempData["ErrorMessage"] = L.T("Srm_OnlyApprovedToOrder");
                    return RedirectToAction("Details", new { id = id });
                }

                // Talabnoma ma'lumotlari bilan POrders/yaratish sahifasiga yo'naltirish
                return RedirectToAction("Create", "POrders", new { requisitionId = id });
            }
        }

        // ---------- tasdiqlash oqimi (SRM-B04) ----------
        // Biznes qarori (2026-10-17): bitta tasdiq. Draft -> Yuborish (Submitted) -> PRequisitions
        // o'zgartirish huquqi bor foydalanuvchi Tasdiqlaydi (Approved) yoki sabab bilan Rad etadi (Rejected).
        // P_REQUISITION_APPROVALS'dagi barcha bosqichlar bir vaqtda yopiladi.

        // POST so'rovi: PRequisitions/Submit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> Submit(int id)
        {
            return ChangeStatusAsync(id, "Draft", "Submitted", null,
                L.T("Srm_RequisitionSubmitted"), L.T("Srm_OnlyDraftSubmit"));
        }

        // POST so'rovi: PRequisitions/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> Approve(int id)
        {
            return ChangeStatusAsync(id, "Submitted", "Approved", null,
                L.T("Srm_RequisitionApproved"), L.T("Srm_OnlySubmittedApprove"));
        }

        // POST so'rovi: PRequisitions/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> Reject(int id, string reason)
        {
            return ChangeStatusAsync(id, "Submitted", "Rejected", reason,
                L.T("Srm_RequisitionRejected"), L.T("Srm_OnlySubmittedReject"));
        }

        private async Task<ActionResult> ChangeStatusAsync(int id, string expectedStatus, string newStatus, string reason,
            string successMessage, string wrongStatusMessage)
        {
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            if (newStatus == "Rejected" && reason == null)
            {
                TempData["ErrorMessage"] = L.T("Srm_EnterRejectReason");
                return RedirectToAction("Details", new { id });
            }

            using (var db = new DBTHSNEntities())
            {
                var requisition = await db.P_REQUISITIONS
                    .Include(r => r.P_REQUISITION_APPROVALS)
                    .FirstOrDefaultAsync(r => r.ID == id && r.IsDeleted != true);
                if (requisition == null)
                {
                    return HttpNotFound();
                }

                if (requisition.Status != expectedStatus)
                {
                    TempData["ErrorMessage"] = wrongStatusMessage;
                    return RedirectToAction("Details", new { id });
                }

                var now = DateTime.Now;
                var userId = CurrentUserId;
                requisition.Status = newStatus;
                requisition.ModifiedDT = now;
                requisition.ModifiedByUserID = userId;

                if (newStatus == "Approved" || newStatus == "Rejected")
                {
                    if (newStatus == "Approved")
                    {
                        requisition.ApprovedByID = userId;
                        requisition.ApprovedDate = now;
                        requisition.RejectedByID = null;
                        requisition.RejectionReason = null;
                    }
                    else
                    {
                        requisition.RejectedByID = userId;
                        requisition.RejectionReason = reason;
                    }

                    var approverName = RoleHelper.GetUserFullName(CurrentUserName) ?? CurrentUserName;
                    if (approverName != null && approverName.Length > 200) approverName = approverName.Substring(0, 200);
                    foreach (var step in requisition.P_REQUISITION_APPROVALS)
                    {
                        step.Status = newStatus;
                        step.ApproverID = userId;
                        step.ApproverName = approverName;
                        step.ApprovalDate = now;
                        step.Comments = reason;
                    }
                }

                await db.SaveChangesAsync();

                LogHelper.LogToDatabase(CurrentUserName, Module, $"{newStatus} - {requisition.RequisitionNo} talabnoma holati: {expectedStatus} -> {newStatus}");
            }

            TempData["SuccessMessage"] = successMessage;
            return RedirectToAction("Details", new { id });
        }

        // ---------- yordamchilar ----------

        /// <summary>Create formasi spravochniklari (GET va xato yo'li uchun bitta joy).</summary>
        private async Task PopulateFormLookupsAsync(DBTHSNEntities db, PRequisitionViewModel model)
        {
            var employees = await db.HR_EMPLOYEES.AsNoTracking()
                .Where(e => e.IsActive == true)
                .OrderBy(e => e.FullName)
                .Select(e => new { e.ID, e.FullName })
                .ToListAsync();
            ViewBag.RequesterList = new SelectList(employees, "ID", "FullName", model?.RequesterID);

            var departments = await db.HR_ORG_UNITS.AsNoTracking()
                .Where(d => d.IsActive == true)
                .OrderBy(d => d.Name)
                .Select(d => new { d.ID, d.Name })
                .ToListAsync();
            ViewBag.DepartmentList = new SelectList(departments, "ID", "Name", model?.DepartmentID);

            // Bilvosita xarid mahsulotlari (uskuna, asbob, sarf materiallari) — keshdan, proyeksiya (SRM-P01)
            var products = await ProcurementLookups.GetPurchasableProductsAsync(
                db, ProcurementLookups.IndirectPurchase, model?.RequisitionProducts?.Select(p => p.ProductID));
            ViewBag.ProductList = ProcurementLookups.ProductSelectList(products);
            ViewBag.UnitList = ProcurementLookups.Units();

            ViewBag.PriorityList = new SelectList(new[]
            {
                new { Value = "High", Text = L.T("Srm_Priority_High") },
                new { Value = "Medium", Text = L.T("Srm_Priority_Medium") },
                new { Value = "Low", Text = L.T("Srm_Priority_Low") }
            }, "Value", "Text", "Medium");
        }

        /// <summary>
        /// Talabnoma raqami: bo'sh bo'lsa PR-{yil}-NNNN (qulf bilan, o'chirilganlar ham hisobga olinadi — SRM-B14);
        /// qo'lda kiritilsa — barcha talabnomalarda (o'chirilganlar ham, bazada UNIQUE) takrorlanmasligi tekshiriladi.Xato bo'lsa null.
        /// </summary>
        private async Task<string> ResolveRequisitionNoAsync(DBTHSNEntities db, string requested)
        {
            var number = string.IsNullOrWhiteSpace(requested) ? null : requested.Trim();
            if (number == null)
            {
                return await DocumentNumberService.NextAsync(db, "P_REQUISITIONS", "RequisitionNo", $"PR-{DateTime.Now.Year}-", 4);
            }

            if (await db.P_REQUISITIONS.AnyAsync(r => r.RequisitionNo == number))
            {
                ModelState.AddModelError("RequisitionNo", L.T("Srm_RequisitionNoExists"));
                return null;
            }

            return number;
        }

        /// <summary>Standart tasdiqlash bosqichlari (Pending holatida).</summary>
        private static void AddDefaultApprovalWorkflow(P_REQUISITIONS requisition)
        {
            var roles = new[] { "Requester", "DepartmentHead", "Financier", "PurchaseManager" };
            for (int i = 0; i < roles.Length; i++)
            {
                requisition.P_REQUISITION_APPROVALS.Add(new P_REQUISITION_APPROVALS
                {
                    Role = roles[i],
                    Status = "Pending",
                    SortOrder = i + 1
                });
            }
        }
    }
}
