using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.Services.Procurement;
using tahsinERP.ViewModels.PRequisition;

namespace tahsinERP.Controllers.SRM
{
    public class PRequisitionsController : BaseController
    {
        private const string Module = "PRequisitions";

        // Olish so'rovi: PRequisitions
        public async Task<ActionResult> Index(string statusFilter = "")
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, Module, "Index"))
            {
                return NotAuthorized();
            }

            using (var db = new DBTHSNEntities())
            {
                var query = db.P_REQUISITIONS
                    .AsNoTracking()
                    .Include(r => r.P_REQUISITION_PRODUCTS)
                    .Where(r => r.IsDeleted != true);

                if (!string.IsNullOrEmpty(statusFilter))
                {
                    query = query.Where(r => r.Status == statusFilter);
                }

                var requisitions = await query
                    .OrderByDescending(r => r.RequisitionDate)
                    .ToListAsync();

                // Bo'lim va so'rovchi nomlari — faqat sahifadagi ID'lar bo'yicha. View ularni kutardi,
                // lekin controller hech qachon bermagani uchun ustunlar doim bo'sh edi (SRM-B19).
                var departmentIds = requisitions.Where(r => r.DepartmentID.HasValue).Select(r => r.DepartmentID.Value).Distinct().ToList();
                var requesterIds = requisitions.Select(r => r.RequesterID).Distinct().ToList();
                ViewBag.DepartmentsDict = await db.HR_ORG_UNITS.AsNoTracking()
                    .Where(d => departmentIds.Contains(d.ID))
                    .ToDictionaryAsync(d => d.ID, d => d.Name);
                ViewBag.EmployeesDict = await db.HR_EMPLOYEES.AsNoTracking()
                    .Where(e => requesterIds.Contains(e.ID))
                    .ToDictionaryAsync(e => e.ID, e => e.FullName);

                ViewBag.StatusFilter = statusFilter;
                ViewBag.ChangePermit = CanChange(Module);
                ViewBag.StatusList = new SelectList(new[]
                {
                    new { Value = "", Text = "All" },
                    new { Value = "Draft", Text = "Draft" },
                    new { Value = "Submitted", Text = "Submitted" },
                    new { Value = "Approved", Text = "Approved" },
                    new { Value = "Rejected", Text = "Rejected" },
                    new { Value = "ConvertedToOrder", Text = "Converted to Order" }
                }, "Value", "Text", statusFilter);

                return View(requisitions);
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
                                        Quantity = product.Quantity,
                                        UnitID = product.UnitID,
                                        EstimatedPrice = product.EstimatedPrice,
                                        Amount = product.EstimatedPrice.HasValue ? product.EstimatedPrice.Value * product.Quantity : (double?)null,
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
                        var errorMessage = $"Xatolik: {ex.Message}";
                        for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
                        {
                            errorMessage += $" | Ichki xatolik: {inner.Message}";
                        }
                        ModelState.AddModelError("", errorMessage);
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
                    TempData["ErrorMessage"] = "Faqat tasdiqlangan talabnomani xarid buyurtmasiga aylantirish mumkin.";
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
                "Talabnoma tasdiqlashga yuborildi.", "Faqat qoralama (Draft) talabnomani yuborish mumkin.");
        }

        // POST so'rovi: PRequisitions/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> Approve(int id)
        {
            return ChangeStatusAsync(id, "Submitted", "Approved", null,
                "Talabnoma tasdiqlandi.", "Faqat yuborilgan (Submitted) talabnomani tasdiqlash mumkin.");
        }

        // POST so'rovi: PRequisitions/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> Reject(int id, string reason)
        {
            return ChangeStatusAsync(id, "Submitted", "Rejected", reason,
                "Talabnoma rad etildi.", "Faqat yuborilgan (Submitted) talabnomani rad etish mumkin.");
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
                TempData["ErrorMessage"] = "Rad etish sababini kiriting.";
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
                new { Value = "High", Text = "Yuqori" },
                new { Value = "Medium", Text = "O'rta" },
                new { Value = "Low", Text = "Past" }
            }, "Value", "Text", "Medium");
        }

        /// <summary>
        /// Talabnoma raqami: bo'sh bo'lsa PR-{yil}-NNNN (qulf bilan, o'chirilganlar ham hisobga olinadi — SRM-B14);
        /// qo'lda kiritilsa — faol talabnomalarda takrorlanmasligi tekshiriladi. Xato bo'lsa null.
        /// </summary>
        private async Task<string> ResolveRequisitionNoAsync(DBTHSNEntities db, string requested)
        {
            var number = string.IsNullOrWhiteSpace(requested) ? null : requested.Trim();
            if (number == null)
            {
                return await DocumentNumberService.NextAsync(db, "P_REQUISITIONS", "RequisitionNo", $"PR-{DateTime.Now.Year}-", 4);
            }

            if (await db.P_REQUISITIONS.AnyAsync(r => r.IsDeleted != true && r.RequisitionNo == number))
            {
                ModelState.AddModelError("RequisitionNo", "Bu raqamli talabnoma allaqachon mavjud.");
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
