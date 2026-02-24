using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels.PRequisition;

namespace tahsinERP.Controllers.SRM
{
    public class PRequisitionsController : Controller
    {
        // Olish so'rovi: PRequisitions
        public async Task<ActionResult> Index(string statusFilter = "")
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "PRequisitions", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var query = db.P_REQUISITIONS
                    .Include(r => r.P_REQUISITION_PRODUCTS)
                    .Where(r => r.IsDeleted != true)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(statusFilter))
                {
                    query = query.Where(r => r.Status == statusFilter);
                }

                var requisitions = await query
                    .OrderByDescending(r => r.RequisitionDate)
                    .ToListAsync();

                ViewBag.StatusFilter = statusFilter;
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
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "PRequisitions", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var requisition = await db.P_REQUISITIONS
                    .Include(r => r.P_REQUISITION_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(r => r.P_REQUISITION_PRODUCTS.Select(p => p.UNIT))
                    .Include(r => r.P_REQUISITION_APPROVALS)
                    .FirstOrDefaultAsync(r => r.ID == id && r.IsDeleted != true);

                if (requisition == null)
                {
                    return HttpNotFound();
                }

                // Yuklash departments va employees uchun display
                var departments = await db.HR_ORG_UNITS
                    .Where(d => d.IsActive == true)
                    .ToListAsync();
                ViewBag.Departments = departments;

                var employees = await db.HR_EMPLOYEES
                    .Where(e => e.IsActive == true)
                    .ToListAsync();
                ViewBag.Requesters = employees;

                return View(requisition);
            }
        }

        // Olish so'rovi: PRequisitions/yaratish
        public async Task<ActionResult> Create()
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "PRequisitions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                // Olish employees uchun requester dropdown
                var employees = await db.HR_EMPLOYEES
                    .Where(e => e.IsActive == true)
                    .OrderBy(e => e.FullName)
                    .ToListAsync();
                ViewBag.RequesterList = new SelectList(employees, "ID", "FullName");

                // Olish departments
                var departments = await db.HR_ORG_UNITS
                    .Where(d => d.IsActive == true)
                    .OrderBy(d => d.Name)
                    .ToListAsync();
                ViewBag.DepartmentList = new SelectList(departments, "ID", "Name");

                // Olish products uchun indirect purchase (equipment, tools, consumables, auxiliary)
                var indirectProducts = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => p.IsDeleted != true &&
                                (p.PRODUCT_CATEGORIES == null || 
                                 (p.PRODUCT_CATEGORIES.IsFinishedGood == false && 
                                  (p.PRODUCT_CATEGORIES.IsConsumable == true || 
                                   p.PRODUCT_CATEGORIES.AllowPurchase == true))))
                    .OrderBy(p => p.PNo)
                    .ToListAsync();

                var productSelectList = indirectProducts.Select(p => new SelectListItem
                {
                    Value = p.ID.ToString(),
                    Text = $"{p.PNo} - {p.Name ?? ""}"
                }).ToList();

                ViewBag.ProductList = new SelectList(productSelectList, "Value", "Text");

                // Olish units
                ViewBag.UnitList = new SelectList(
                    await db.UNITS.ToListAsync(),
                    "ID", "ShortName"
                );

                // Olish priority options
                ViewBag.PriorityList = new SelectList(new[]
                {
                    new { Value = "High", Text = "High" },
                    new { Value = "Medium", Text = "Medium" },
                    new { Value = "Low", Text = "Low" }
                }, "Value", "Text", "Medium");

                return View();
            }
        }

        // POST so'rovi: PRequisitions/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(PRequisitionViewModel model)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "PRequisitions"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    try
                    {
                        // RequisitionNo berilmagan bo'lsa, avtomatik yaratish
                        if (string.IsNullOrEmpty(model.RequisitionNo))
                        {
                            model.RequisitionNo = await GenerateRequisitionNo(db);
                        }

                        var userId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);

                        // Talabnoma sarlavhasini yaratish
                        var requisition = new P_REQUISITIONS
                        {
                            RequisitionNo = model.RequisitionNo,
                            RequisitionDate = model.RequisitionDate,
                            DepartmentID = model.DepartmentID,
                            RequesterID = model.RequesterID,
                            ContactPhone = model.ContactPhone,
                            Purpose = model.Purpose,
                            Status = model.Status ?? "Draft",
                            IsDeleted = false,
                            CreatedByUserID = userId,
                            CreatedDT = DateTime.Now
                        };

                        db.P_REQUISITIONS.Add(requisition);
                        await db.SaveChangesAsync();

                        // Qo'shish requisition products
                        if (model.RequisitionProducts != null && model.RequisitionProducts.Any())
                        {
                            foreach (var product in model.RequisitionProducts)
                            {
                                var requisitionProduct = new P_REQUISITION_PRODUCTS
                                {
                                    RequisitionID = requisition.ID,
                                    ProductID = product.ProductID,
                                    Quantity = product.Quantity,
                                    UnitID = product.UnitID,
                                    EstimatedPrice = product.EstimatedPrice,
                                    Amount = product.EstimatedPrice.HasValue ? product.EstimatedPrice.Value * product.Quantity : null,
                                    Priority = product.Priority ?? "Medium",
                                    RequiredDate = product.RequiredDate,
                                    Description = product.Description
                                };

                                db.P_REQUISITION_PRODUCTS.Add(requisitionProduct);
                            }

                            await db.SaveChangesAsync();
                        }

                        // Standart tasdiqlash jarayonini yaratish
                        await CreateDefaultApprovalWorkflow(db, requisition.ID);

                        // Amalni logga yozish
                        LogHelper.LogToDatabase(User.Identity.Name, "PRequisitions", $"Create - {requisition.RequisitionNo} talabnoma yaratildi");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"Xatolik: {ex.Message}";
                        var innerEx = ex.InnerException;
                        while (innerEx != null)
                        {
                            errorMessage += $" | Ichki xatolik: {innerEx.Message}";
                            innerEx = innerEx.InnerException;
                        }
                        ModelState.AddModelError("", errorMessage);
                    }
                }
            }

            // Xatolikda ViewBag qiymatlarini qayta ga'ldirish
            using (var db = new DBTHSNEntities())
            {
                var employees = await db.HR_EMPLOYEES
                    .Where(e => e.IsActive == true)
                    .OrderBy(e => e.FullName)
                    .ToListAsync();
                ViewBag.RequesterList = new SelectList(employees, "ID", "FullName", model.RequesterID);

                var departments = await db.HR_ORG_UNITS
                    .Where(d => d.IsActive == true)
                    .OrderBy(d => d.Name)
                    .ToListAsync();
                ViewBag.DepartmentList = new SelectList(departments, "ID", "Name", model.DepartmentID);

                var indirectProducts = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => p.IsDeleted != true &&
                                (p.PRODUCT_CATEGORIES == null || 
                                 (p.PRODUCT_CATEGORIES.IsFinishedGood == false && 
                                  (p.PRODUCT_CATEGORIES.IsConsumable == true || 
                                   p.PRODUCT_CATEGORIES.AllowPurchase == true))))
                    .OrderBy(p => p.PNo)
                    .ToListAsync();

                var productSelectList = indirectProducts.Select(p => new SelectListItem
                {
                    Value = p.ID.ToString(),
                    Text = $"{p.PNo} - {p.Name ?? ""}"
                }).ToList();

                ViewBag.ProductList = new SelectList(productSelectList, "Value", "Text");

                ViewBag.UnitList = new SelectList(
                    await db.UNITS.ToListAsync(),
                    "ID", "ShortName"
                );

                ViewBag.PriorityList = new SelectList(new[]
                {
                    new { Value = "High", Text = "Yuqori" },
                    new { Value = "Medium", Text = "O'rta" },
                    new { Value = "Low", Text = "Past" }
                }, "Value", "Text", "Medium");
            }

            return View(model);
        }

        // Olish so'rovi: PRequisitions/ConvertToOrder/5
        public async Task<ActionResult> ConvertToOrder(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "PRequisitions", "ConvertToOrder"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var requisition = await db.P_REQUISITIONS
                    .Include(r => r.P_REQUISITION_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(r => r.P_REQUISITION_PRODUCTS.Select(p => p.UNIT))
                    .FirstOrDefaultAsync(r => r.ID == id && r.IsDeleted != true);

                if (requisition == null)
                {
                    return HttpNotFound();
                }

                if (requisition.Status != "Approved")
                {
                    TempData["ErrorMessage"] = "Faqat tasdiqlangan talabnomani xarid buyurtmasiga aylantirish mumkin.";
                    return RedirectToAction("Details", new { id = id });
                }

                // Talabnoma ma'lumotlari bilan POrders/yaratish sahifasiga yo'naltirish
                return RedirectToAction("Create", "POrders", new { requisitionId = id });
            }
        }

        // Yordamchi metod: RequisitionNo yaratish
        private async Task<string> GenerateRequisitionNo(DBTHSNEntities db)
        {
            var year = DateTime.Now.Year;
            var prefix = $"PR-{year}-";

            var lastRequisition = await db.P_REQUISITIONS
                .Where(r => r.RequisitionNo.StartsWith(prefix))
                .OrderByDescending(r => r.RequisitionNo)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastRequisition != null)
            {
                var lastNumberStr = lastRequisition.RequisitionNo.Substring(prefix.Length);
                if (int.TryParse(lastNumberStr, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D4}";
        }

        // Yordamchi metod: standart tasdiqlash jarayonini yaratish
        private async Task CreateDefaultApprovalWorkflow(DBTHSNEntities db, int requisitionId)
        {
            var approvals = new List<P_REQUISITION_APPROVALS>
            {
                new P_REQUISITION_APPROVALS
                {
                    RequisitionID = requisitionId,
                    Role = "Requester",
                    Status = "Pending",
                    SortOrder = 1
                },
                new P_REQUISITION_APPROVALS
                {
                    RequisitionID = requisitionId,
                    Role = "DepartmentHead",
                    Status = "Pending",
                    SortOrder = 2
                },
                new P_REQUISITION_APPROVALS
                {
                    RequisitionID = requisitionId,
                    Role = "Financier",
                    Status = "Pending",
                    SortOrder = 3
                },
                new P_REQUISITION_APPROVALS
                {
                    RequisitionID = requisitionId,
                    Role = "PurchaseManager",
                    Status = "Pending",
                    SortOrder = 4
                }
            };

            foreach (var approval in approvals)
            {
                db.P_REQUISITION_APPROVALS.Add(approval);
            }

            await db.SaveChangesAsync();
        }
    }
}

