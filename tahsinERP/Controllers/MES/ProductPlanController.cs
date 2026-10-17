using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Controllers;
using tahsinERP.Helpers;
using tahsinERP.Services;
using tahsinERP.Filters;

namespace tahsinERP.Controllers.MES
{
    [Permission]
    public class ProductPlanController : BaseController
    {
        private const string OverlapMessage = "Ushbu mahsulot uchun tanlangan muddatlarda allaqachon reja mavjud.";
        private const string OverlapOtherMessage = "Ushbu mahsulot uchun tanlangan muddatlarda boshqa reja mavjud.";

        // Olish so'rovi: ProductPlan
        public async Task<ActionResult> Index(int? productID, DateTime? fromDate, DateTime? toDate)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Sahifada ko'rsatiladigan standart oraliq (−3 / +3 oy) so'rovga ham qo'llanadi —
                // avval parametrsiz ochilganda butun jadval yuklanardi (MES-P18).
                var effectiveFrom = (fromDate ?? DateTime.Now.AddMonths(-3)).Date;
                var effectiveTo = (toDate ?? DateTime.Now.AddMonths(3)).Date;
                var toExclusive = effectiveTo.AddDays(1);

                var query = db.PRODUCTPLANS.AsNoTracking()
                    .Include(p => p.PRODUCT)
                    .Include(p => p.S_ORDERS)
                    .Where(p => p.IsDeleted == false
                        && p.StartDate >= effectiveFrom
                        && p.DueDate < toExclusive);

                if (productID.HasValue)
                {
                    query = query.Where(p => p.ProductID == productID.Value);
                }

                var plans = await query
                    .OrderByDescending(p => p.StartDate)
                    .ThenBy(p => p.PRODUCT.PNo)
                    .ToListAsync();

                var viewModel = new ProductPlanListViewModel
                {
                    Plans = plans.Select(p => new ProductPlanViewModel
                    {
                        ID = p.ID,
                        ProductID = p.ProductID,
                        TotalQty = p.Amount,
                        StartDate = p.StartDate,
                        EndDate = p.DueDate,
                        IsDeleted = p.IsDeleted,
                        Product = p.PRODUCT,
                        SalesOrderID = p.SalesOrderID,
                        SalesOrderNo = p.S_ORDERS?.OrderNo
                    }).ToList(),
                    ProductID = productID,
                    FromDate = effectiveFrom,
                    ToDate = effectiveTo
                };

                LoadProductDropDown(db, productID);

                return View(viewModel);
            }
        }

        // Olish so'rovi: ProductPlan/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var plan = await db.PRODUCTPLANS.AsNoTracking()
                    .Where(p => p.ID == id && p.IsDeleted == false)
                    .Include(p => p.PRODUCT)
                    .Include(p => p.S_ORDERS)
                    .FirstOrDefaultAsync();

                if (plan == null)
                {
                    return HttpNotFound();
                }

                // Link ga delivery schedules agar product matches
                var linkedSchedules = await db.SORDER_DELIVERY_SCHEDULE.AsNoTracking()
                    .Include(s => s.PRODUCT)
                    .Include(s => s.S_ORDERS)
                    .Where(s => s.ProductID == plan.ProductID
                        && s.DeliveryDate >= plan.StartDate
                        && s.DeliveryDate <= plan.DueDate)
                    .OrderBy(s => s.DeliveryDate)
                    .ToListAsync();

                var viewModel = new ProductPlanViewModel
                {
                    ID = plan.ID,
                    ProductID = plan.ProductID,
                    TotalQty = plan.Amount,
                    StartDate = plan.StartDate,
                    EndDate = plan.DueDate,
                    IsDeleted = plan.IsDeleted,
                    Product = plan.PRODUCT,
                    DailyPlans = await BuildDailyPlanViewModelsAsync(db, plan.ID, plan.ProductID),
                    LinkedDeliverySchedules = linkedSchedules,
                    SalesOrderID = plan.SalesOrderID,
                    SalesOrderNo = plan.S_ORDERS?.OrderNo
                };

                return View(viewModel);
            }
        }

        // Olish so'rovi: ProductPlan/yaratish
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var viewModel = new ProductPlanViewModel();
                LoadProductDropDown(db, viewModel.ProductID);
                LoadSalesOrderDropDown(db, viewModel.SalesOrderID);
                return View(viewModel);
            }
        }

        // Olish so'rovi: ProductPlan/GridCreate
        public ActionResult GridCreate(int? year, int? month)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var y = year ?? DateTime.Now.Year;
                var m = month ?? DateTime.Now.Month;
                var vm = new ProductPlanGridEntryViewModel
                {
                    Year = y,
                    Month = m
                };
                vm.Rows.Add(new ProductPlanGridRowViewModel
                {
                    StartDate = new DateTime(y, m, 1),
                    EndDate = new DateTime(y, m, DateTime.DaysInMonth(y, m))
                });
                LoadProductDropDown(db, null);
                LoadSalesOrderDropDown(db, null);
                return View(vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GridCreate(ProductPlanGridEntryViewModel model)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (model.Rows == null || !model.Rows.Any())
                {
                    TempData["Error"] = "Saqlash uchun qator yo'q";
                    LoadProductDropDown(db, null);
                    LoadSalesOrderDropDown(db, null);
                    return View(model);
                }

                // Hammasi bitta tranzaksiyada: avval barcha qatorlar tekshiriladi (bir-biri bilan ustma-ust tushishi ham),
                // so'ng yaroqli qatorlar birga saqlanadi (avval qatorma-qator saqlanib, xatoda yarim natija qolardi, MES-P25/B10).
                var errors = new List<string>();
                var created = 0;
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var accepted = new List<ProductPlanGridRowViewModel>();
                    foreach (var row in model.Rows)
                    {
                        if (!row.ProductID.HasValue || !row.TotalQty.HasValue || !row.StartDate.HasValue || !row.EndDate.HasValue)
                        {
                            continue;
                        }

                        var error = await ValidateGridRowAsync(db, row, null, accepted);
                        if (error != null)
                        {
                            errors.Add(error);
                            continue;
                        }
                        accepted.Add(row);
                    }

                    foreach (var row in accepted)
                    {
                        var plan = new PRODUCTPLAN
                        {
                            ProductID = row.ProductID.Value,
                            Amount = row.TotalQty.Value,
                            StartDate = row.StartDate.Value,
                            DueDate = row.EndDate.Value,
                            IsDeleted = false,
                            SalesOrderID = row.SalesOrderID
                        };
                        db.PRODUCTPLANS.Add(plan);
                        AddDailyPlans(db, plan, row.StartDate.Value, row.EndDate.Value, row.TotalQty.Value);
                        created++;
                    }

                    await db.SaveChangesAsync();
                    transaction.Commit();
                }

                if (created > 0)
                {
                    TempData["Success"] = $"{created} ta reja saqlandi.";
                }
                if (errors.Any())
                {
                    TempData["Error"] = string.Join("<br/>", errors.Distinct());
                }
                return RedirectToAction("GridCreate", new { year = model.Year, month = model.Month });
            }
        }

        // Olish so'rovi: ProductPlan/GridEdit
        public async Task<ActionResult> GridEdit(int? year, int? month)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var y = year ?? DateTime.Now.Year;
                var m = month ?? DateTime.Now.Month;
                var monthStart = new DateTime(y, m, 1);
                var monthEnd = monthStart.AddMonths(1);
                var plans = await db.PRODUCTPLANS.AsNoTracking()
                    .Where(p => p.IsDeleted == false && p.StartDate >= monthStart && p.StartDate < monthEnd)
                    .OrderBy(p => p.StartDate)
                    .ThenBy(p => p.ProductID)
                    .Select(p => new ProductPlanGridRowViewModel
                    {
                        PlanID = p.ID,
                        ProductID = p.ProductID,
                        TotalQty = p.Amount,
                        StartDate = p.StartDate,
                        EndDate = p.DueDate,
                        SalesOrderID = p.SalesOrderID
                    })
                    .ToListAsync();

                var vm = new ProductPlanGridEntryViewModel
                {
                    Year = y,
                    Month = m,
                    Rows = plans
                };

                if (!vm.Rows.Any())
                {
                    vm.Rows.Add(new ProductPlanGridRowViewModel
                    {
                        StartDate = monthStart,
                        EndDate = new DateTime(y, m, DateTime.DaysInMonth(y, m))
                    });
                }

                LoadProductDropDown(db, null);
                LoadSalesOrderDropDown(db, null);
                return View(vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GridEdit(ProductPlanGridEntryViewModel model)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                if (model.Rows == null || !model.Rows.Any())
                {
                    TempData["Error"] = "Saqlash uchun qator yo'q";
                    return RedirectToAction("GridEdit", new { year = model.Year, month = model.Month });
                }

                var inserted = 0;
                var updated = 0;
                var errors = new List<string>();

                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var accepted = new List<ProductPlanGridRowViewModel>();
                    var planIds = model.Rows.Where(r => r.PlanID.HasValue).Select(r => r.PlanID.Value).Distinct().ToList();
                    var existingPlans = await db.PRODUCTPLANS
                        .Where(p => planIds.Contains(p.ID) && p.IsDeleted == false)
                        .ToDictionaryAsync(p => p.ID);

                    foreach (var row in model.Rows)
                    {
                        if (!row.ProductID.HasValue || !row.TotalQty.HasValue || !row.StartDate.HasValue || !row.EndDate.HasValue)
                        {
                            continue;
                        }
                        if (row.PlanID.HasValue && !existingPlans.ContainsKey(row.PlanID.Value))
                        {
                            continue;
                        }

                        var error = await ValidateGridRowAsync(db, row, row.PlanID, accepted);
                        if (error != null)
                        {
                            errors.Add(error);
                            continue;
                        }
                        accepted.Add(row);
                    }

                    foreach (var row in accepted)
                    {
                        if (row.PlanID.HasValue)
                        {
                            var plan = existingPlans[row.PlanID.Value];
                            var scheduleChanged = plan.StartDate != row.StartDate.Value
                                || plan.DueDate != row.EndDate.Value
                                || Math.Abs(plan.Amount - row.TotalQty.Value) > 1e-9;

                            plan.ProductID = row.ProductID.Value;
                            plan.Amount = row.TotalQty.Value;
                            plan.StartDate = row.StartDate.Value;
                            plan.DueDate = row.EndDate.Value;
                            plan.SalesOrderID = row.SalesOrderID;

                            // Muddat/miqdor o'zgarsa kunlik reja qayta taqsimlanadi (avval eski holicha qolardi, MES-B09)
                            if (scheduleChanged)
                            {
                                await RegenerateDailyPlansAsync(db, plan);
                            }
                            updated++;
                        }
                        else
                        {
                            var plan = new PRODUCTPLAN
                            {
                                ProductID = row.ProductID.Value,
                                Amount = row.TotalQty.Value,
                                StartDate = row.StartDate.Value,
                                DueDate = row.EndDate.Value,
                                IsDeleted = false,
                                SalesOrderID = row.SalesOrderID
                            };
                            db.PRODUCTPLANS.Add(plan);
                            AddDailyPlans(db, plan, row.StartDate.Value, row.EndDate.Value, row.TotalQty.Value);
                            inserted++;
                        }
                    }

                    await db.SaveChangesAsync();
                    transaction.Commit();
                }

                if (updated > 0 || inserted > 0)
                {
                    TempData["Success"] = $"Yangilandi: {updated}, qo'shildi: {inserted}";
                }
                if (errors.Any())
                {
                    TempData["Error"] = string.Join("<br/>", errors.Distinct());
                }
                return RedirectToAction("GridEdit", new { year = model.Year, month = model.Month });
            }
        }

        // POST so'rovi: ProductPlan/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(ProductPlanViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        // Tekshirish date range
                        if (viewModel.EndDate < viewModel.StartDate)
                        {
                            ModelState.AddModelError("", "Tugash sanasi boshlanish sanasidan kichik bo'lishi mumkin emas");
                            LoadProductDropDown(db, viewModel.ProductID);
                            LoadSalesOrderDropDown(db, viewModel.SalesOrderID);
                            return View(viewModel);
                        }

                        var salesOrderValidation = await ValidateSalesOrderContainsProductAsync(db, viewModel.SalesOrderID, viewModel.ProductID, viewModel.TotalQty);
                        if (salesOrderValidation != null)
                        {
                            ModelState.AddModelError(nameof(viewModel.SalesOrderID), salesOrderValidation);
                            LoadProductDropDown(db, viewModel.ProductID);
                            LoadSalesOrderDropDown(db, viewModel.SalesOrderID);
                            return View(viewModel);
                        }

                        PRODUCTPLAN plan;
                        // Ustma-ust tushish tekshiruvi va yozuv bitta (SERIALIZABLE) tranzaksiyada — parallel saqlashda
                        // ikkita bir xil davrli reja paydo bo'lmasin (MES-B10)
                        using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                        {
                            if (await FindOverlappingPlanAsync(db, viewModel.ProductID, viewModel.StartDate, viewModel.EndDate, null) != null)
                            {
                                transaction.Rollback();
                                ModelState.AddModelError("", OverlapMessage);
                                LoadProductDropDown(db, viewModel.ProductID);
                                LoadSalesOrderDropDown(db, viewModel.SalesOrderID);
                                return View(viewModel);
                            }

                            plan = new PRODUCTPLAN
                            {
                                ProductID = viewModel.ProductID,
                                Amount = viewModel.TotalQty,
                                StartDate = viewModel.StartDate,
                                DueDate = viewModel.EndDate,
                                IsDeleted = false,
                                SalesOrderID = viewModel.SalesOrderID
                            };
                            db.PRODUCTPLANS.Add(plan);

                            // Auto-yaratish PRODUCTPLANS_DAILY qatorlar uchun har bir day (umumiy metod — dublikat #12)
                            AddDailyPlans(db, plan, viewModel.StartDate, viewModel.EndDate, viewModel.TotalQty);

                            await db.SaveChangesAsync();
                            transaction.Commit();
                        }

                        LogHelper.LogToDatabase(User.Identity.Name, "ProductPlanController",
                            $"ProductPlan yaratildi: ID {plan.ID}, ProductID {plan.ProductID}, {(viewModel.EndDate - viewModel.StartDate).Days + 1} kunlik reja");

                        TempData["Success"] = "Ishlab chiqarish rejasi muvaffaqiyatli yaratildi";
                        return RedirectToAction("Details", new { id = plan.ID });
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", BuildErrorMessage(ex));
                    }
                }
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                LoadProductDropDown(db, viewModel.ProductID);
                LoadSalesOrderDropDown(db, viewModel.SalesOrderID);
            }

            return View(viewModel);
        }

        // Olish so'rovi: ProductPlan/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var plan = await db.PRODUCTPLANS.AsNoTracking().FirstOrDefaultAsync(p => p.ID == id && p.IsDeleted == false);
                if (plan == null)
                {
                    return HttpNotFound();
                }

                var viewModel = new ProductPlanViewModel
                {
                    ID = plan.ID,
                    ProductID = plan.ProductID,
                    TotalQty = plan.Amount,
                    StartDate = plan.StartDate,
                    EndDate = plan.DueDate,
                    IsDeleted = plan.IsDeleted,
                    SalesOrderID = plan.SalesOrderID
                };
                if (plan.SalesOrderID.HasValue)
                {
                    viewModel.SalesOrderNo = await db.S_ORDERS
                        .Where(o => o.ID == plan.SalesOrderID.Value)
                        .Select(o => o.OrderNo)
                        .FirstOrDefaultAsync();
                }

                LoadProductDropDown(db, viewModel.ProductID);
                LoadSalesOrderDropDown(db, viewModel.SalesOrderID);

                return View(viewModel);
            }
        }

        // POST so'rovi: ProductPlan/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(ProductPlanViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var plan = db.PRODUCTPLANS.FirstOrDefault(p => p.ID == viewModel.ID && p.IsDeleted == false);
                    if (plan == null)
                    {
                        return HttpNotFound();
                    }

                    try
                    {
                        // Tekshirish date range
                        if (viewModel.EndDate < viewModel.StartDate)
                        {
                            ModelState.AddModelError("", "Tugash sanasi boshlanish sanasidan kichik bo'lishi mumkin emas");
                            LoadProductDropDown(db, viewModel.ProductID);
                            LoadSalesOrderDropDown(db, viewModel.SalesOrderID);
                            return View(viewModel);
                        }

                        var salesOrderValidation = await ValidateSalesOrderContainsProductAsync(db, viewModel.SalesOrderID, viewModel.ProductID, viewModel.TotalQty);
                        if (salesOrderValidation != null)
                        {
                            ModelState.AddModelError(nameof(viewModel.SalesOrderID), salesOrderValidation);
                            LoadProductDropDown(db, viewModel.ProductID);
                            LoadSalesOrderDropDown(db, viewModel.SalesOrderID);
                            return View(viewModel);
                        }

                        using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                        {
                            // Tekshirish uchun overlapping plans (excluding current plan)
                            if (await FindOverlappingPlanAsync(db, viewModel.ProductID, viewModel.StartDate, viewModel.EndDate, viewModel.ID) != null)
                            {
                                transaction.Rollback();
                                ModelState.AddModelError("", OverlapOtherMessage);
                                LoadProductDropDown(db, viewModel.ProductID);
                                LoadSalesOrderDropDown(db, viewModel.SalesOrderID);
                                return View(viewModel);
                            }

                            var scheduleChanged = plan.StartDate != viewModel.StartDate
                                || plan.DueDate != viewModel.EndDate
                                || Math.Abs(plan.Amount - viewModel.TotalQty) > 1e-9;

                            // Yangilash plan
                            plan.ProductID = viewModel.ProductID;
                            plan.Amount = viewModel.TotalQty;
                            plan.StartDate = viewModel.StartDate;
                            plan.DueDate = viewModel.EndDate;
                            plan.SalesOrderID = viewModel.SalesOrderID;

                            // Muddat/miqdor o'zgarsa kunlik reja qayta taqsimlanadi — dashboard/hisobotlar kunlik
                            // qatorlarni yig'adi va header bilan farq qilib qolardi (MES-B09)
                            if (scheduleChanged)
                            {
                                await RegenerateDailyPlansAsync(db, plan);
                            }

                            await db.SaveChangesAsync();
                            transaction.Commit();

                            if (scheduleChanged)
                            {
                                TempData["Info"] = "Muddat yoki miqdor o'zgargani uchun kunlik reja qayta taqsimlandi.";
                            }
                        }

                        LogHelper.LogToDatabase(User.Identity.Name, "ProductPlanController", $"ProductPlan tahrirlandi: ID {viewModel.ID}");

                        TempData["Success"] = "Ishlab chiqarish rejasi muvaffaqiyatli yangilandi";
                        return RedirectToAction("Details", new { id = viewModel.ID });
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", BuildErrorMessage(ex));
                    }
                }
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                LoadProductDropDown(db, viewModel.ProductID);
                LoadSalesOrderDropDown(db, viewModel.SalesOrderID);
            }

            return View(viewModel);
        }

        // Olish so'rovi: ProductPlan/o'chirish/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var plan = db.PRODUCTPLANS.AsNoTracking()
                    .Include(p => p.PRODUCT)
                    .Include(p => p.S_ORDERS)
                    .FirstOrDefault(p => p.ID == id && p.IsDeleted == false);

                if (plan == null)
                {
                    return HttpNotFound();
                }

                return View(plan);
            }
        }

        // POST so'rovi: ProductPlan/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var plan = db.PRODUCTPLANS.Find(id);
                if (plan == null)
                {
                    return HttpNotFound();
                }

                plan.IsDeleted = true;
                SoftDeleteReferenceService.AfterProductPlanSoftDeleted(db, id);
                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "ProductPlanController", $"ProductPlan o'chirildi: ID {id}");

                TempData["Success"] = "Ishlab chiqarish rejasi muvaffaqiyatli o'chirildi";
                return RedirectToAction("Index");
            }
        }

        // POST so'rovi: ProductPlan/DeleteGridRow
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteGridRow(int id, int year, int month)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var plan = await db.PRODUCTPLANS.FirstOrDefaultAsync(p => p.ID == id && p.IsDeleted == false);
                if (plan == null)
                {
                    TempData["Error"] = "Reja topilmadi";
                    return RedirectToAction("GridEdit", new { year, month });
                }

                plan.IsDeleted = true;
                await SoftDeleteReferenceService.AfterProductPlanSoftDeletedAsync(db, id);
                await db.SaveChangesAsync();

                TempData["Success"] = "Reja o'chirildi";
                return RedirectToAction("GridEdit", new { year, month });
            }
        }

        // POST so'rovi: ProductPlan/UpdateDailyPlan - Inline editable daily grid
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateDailyPlan(int planID, List<ProductPlanDailyViewModel> dailyPlans)
        {
            if (dailyPlans == null || !dailyPlans.Any())
            {
                return Json(new { success = false, message = "Ma'lumotlar topilmadi" });
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                try
                {
                    // Bitta so'rov (avval har qatorga Find, MES-P26)
                    var ids = dailyPlans.Select(d => d.ID).Distinct().ToList();
                    var rows = db.PRODUCTPLANS_DAILY
                        .Where(d => ids.Contains(d.ID) && d.PlanID == planID)
                        .ToDictionary(d => d.ID);

                    foreach (var item in dailyPlans)
                    {
                        if (rows.TryGetValue(item.ID, out var dailyPlan))
                        {
                            dailyPlan.DayShift = item.DayShift;
                            dailyPlan.NightShift = item.NightShift;
                            dailyPlan.Total = item.DayShift + item.NightShift; // Auto-calculate
                        }
                    }

                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "ProductPlanController", $"Kunlik reja yangilandi: Plan ID {planID}");

                    return Json(new { success = true, message = "Kunlik reja muvaffaqiyatli yangilandi" });
                }
                catch (Exception ex)
                {
                    return JsonError(ex, "Kunlik rejani saqlashda xatolik yuz berdi.");
                }
            }
        }

        // Olish so'rovi: ProductPlan/GetDailyGridPartial/5 - Partial view uchun daily grid
        public async Task<ActionResult> GetDailyGridPartial(int planID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var plan = await db.PRODUCTPLANS.AsNoTracking()
                    .Where(p => p.ID == planID)
                    .Select(p => new { p.ID, p.ProductID })
                    .FirstOrDefaultAsync();
                if (plan == null)
                {
                    return HttpNotFound();
                }

                return PartialView("_DailyGridPartial", await BuildDailyPlanViewModelsAsync(db, plan.ID, plan.ProductID));
            }
        }

        /// <summary>
        /// Kunlik reja + fakt (Details va GetDailyGridPartial uchun umumiy — dublikat #13).
        /// Fakt: shu kunda fakt sanasi bo'lgan yoki [RejaSana:...] shu kunga teng ishlab chiqarishlar.
        /// Avval mahsulotning BUTUN ishlab chiqarish tarixi yuklanardi (MES-P04); endi reja oralig'i ±31 kun
        /// va reja oylaridagi [RejaSana:yyyy-MM-...] izohlari bo'yicha SQL'da filtrlanadi.
        /// </summary>
        private static async Task<List<ProductPlanDailyViewModel>> BuildDailyPlanViewModelsAsync(DBTHSNEntities db, int planId, int productId)
        {
            var dailyPlans = await db.PRODUCTPLANS_DAILY.AsNoTracking()
                .Where(d => d.PlanID == planId)
                .OrderBy(d => d.Day)
                .ToListAsync();

            var actualProductions = new Dictionary<DateTime, (double DayShift, double NightShift, double Total)>();
            if (dailyPlans.Any())
            {
                var firstDay = dailyPlans.First().Day.Date;
                var lastDay = dailyPlans.Last().Day.Date;
                var windowStart = firstDay.AddDays(-31);
                var windowEnd = lastDay.AddDays(32);
                var monthTokens = new List<string>();
                for (var m = new DateTime(firstDay.Year, firstDay.Month, 1); m <= lastDay; m = m.AddMonths(1))
                {
                    monthTokens.Add("[RejaSana:" + m.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture) + "-");
                }

                var productions = await db.PRODUCT_PRODUCTION.AsNoTracking()
                    .Where(p => p.ProductID == productId && (p.IsDeleted == false || p.IsDeleted == null))
                    .Where(ProductionDateOrPlanMonthPredicate(windowStart, windowEnd, monthTokens))
                    .ToListAsync();

                actualProductions = AggregateActualProductionByPlanCalendarDay(dailyPlans.Select(d => d.Day.Date).ToList(), productions);
            }

            return dailyPlans.Select(d =>
            {
                var hasActual = actualProductions.TryGetValue(d.Day.Date, out var actual);
                return new ProductPlanDailyViewModel
                {
                    ID = d.ID,
                    PlanID = d.PlanID,
                    Day = d.Day,
                    DayShift = d.DayShift,
                    NightShift = d.NightShift,
                    Total = d.Total,
                    ActualDayShift = hasActual ? actual.DayShift : (double?)null,
                    ActualNightShift = hasActual ? actual.NightShift : (double?)null,
                    ActualTotal = hasActual ? actual.Total : (double?)null
                };
            }).ToList();
        }

        /// <summary>p.Date ∈ [start, end) YOKI Comment reja oylaridan birining tokenini o'z ichiga oladi.</summary>
        private static System.Linq.Expressions.Expression<Func<PRODUCT_PRODUCTION, bool>> ProductionDateOrPlanMonthPredicate(
            DateTime start, DateTime end, List<string> monthTokens)
        {
            var p = System.Linq.Expressions.Expression.Parameter(typeof(PRODUCT_PRODUCTION), "p");
            var date = System.Linq.Expressions.Expression.Property(p, nameof(PRODUCT_PRODUCTION.Date));
            System.Linq.Expressions.Expression body = System.Linq.Expressions.Expression.AndAlso(
                System.Linq.Expressions.Expression.GreaterThanOrEqual(date, System.Linq.Expressions.Expression.Constant(start)),
                System.Linq.Expressions.Expression.LessThan(date, System.Linq.Expressions.Expression.Constant(end)));

            var comment = System.Linq.Expressions.Expression.Property(p, nameof(PRODUCT_PRODUCTION.Comment));
            var containsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) });
            foreach (var token in monthTokens)
            {
                var hasToken = System.Linq.Expressions.Expression.AndAlso(
                    System.Linq.Expressions.Expression.NotEqual(comment, System.Linq.Expressions.Expression.Constant(null, typeof(string))),
                    System.Linq.Expressions.Expression.Call(comment, containsMethod, System.Linq.Expressions.Expression.Constant(token)));
                body = System.Linq.Expressions.Expression.OrElse(body, hasToken);
            }

            return System.Linq.Expressions.Expression.Lambda<Func<PRODUCT_PRODUCTION, bool>>(body, p);
        }

        /// <summary>
        /// Har bir reja kalendar kuni uchun: shu kunda fakt sanasi bo'lgan yoki [RejaSana:...] shu kunga teng bo'lgan
        /// PRODUCT_PRODUCTION qatorlarini yig'adi (kunlik ishlab chiqarish ekrani bilan bir xil bog'lanish).
        /// </summary>
        private static Dictionary<DateTime, (double DayShift, double NightShift, double Total)> AggregateActualProductionByPlanCalendarDay(
            List<DateTime> planDates,
            List<PRODUCT_PRODUCTION> productions)
        {
            var result = new Dictionary<DateTime, (double DayShift, double NightShift, double Total)>();
            foreach (var day in planDates.Select(d => d.Date).Distinct())
            {
                var rows = productions.Where(p => ProductProductionPlanDateMeta.MatchesCalendarDay(p, day)).ToList();
                if (rows.Count == 0)
                {
                    continue;
                }

                result[day] = (
                    rows.Sum(p => p.DayShift),
                    rows.Sum(p => p.NightShift),
                    rows.Sum(p => p.Total));
            }

            return result;
        }

        /// <summary>Ustma-ust tushadigan (yopiq oraliqlar) faol reja — dublikat #11 (5 nusxa edi).</summary>
        private static Task<PRODUCTPLAN> FindOverlappingPlanAsync(DBTHSNEntities db, int productId, DateTime start, DateTime end, int? excludePlanId)
        {
            return db.PRODUCTPLANS.FirstOrDefaultAsync(p => p.ProductID == productId
                && p.IsDeleted == false
                && (!excludePlanId.HasValue || p.ID != excludePlanId.Value)
                && p.StartDate <= end
                && p.DueDate >= start);
        }

        /// <summary>
        /// Grid qatori tekshiruvi: miqdor, sana, savdo buyurtmasi, bazadagi va shu so'rovdagi boshqa qatorlar bilan
        /// ustma-ust tushish. Xato bo'lsa — matn, aks holda null.
        /// </summary>
        private static async Task<string> ValidateGridRowAsync(DBTHSNEntities db, ProductPlanGridRowViewModel row, int? excludePlanId, List<ProductPlanGridRowViewModel> acceptedRows)
        {
            if (row.TotalQty.Value <= 0)
            {
                return "Miqdor 0 dan katta bo'lishi kerak.";
            }

            if (row.EndDate.Value < row.StartDate.Value)
            {
                return "Tugash sanasi boshlanish sanasidan kichik bo'lishi mumkin emas.";
            }

            var soValidation = await ValidateSalesOrderContainsProductAsync(db, row.SalesOrderID, row.ProductID.Value, row.TotalQty);
            if (soValidation != null)
            {
                return soValidation;
            }

            // Shu so'rovdagi boshqa qatorlar bilan (avval tekshirilmasdi)
            if (acceptedRows.Any(a => a.ProductID == row.ProductID
                && a.StartDate.Value <= row.EndDate.Value
                && a.EndDate.Value >= row.StartDate.Value))
            {
                return "Bir xil mahsulot uchun jadvalda ustma-ust tushadigan qatorlar bor.";
            }

            // Bazadagi boshqa rejalar bilan (shu so'rovda tahrirlanayotgan rejalar ham bazadagi eski holati bilan solishtiriladi)
            var overlapping = await FindOverlappingPlanAsync(db, row.ProductID.Value, row.StartDate.Value, row.EndDate.Value, excludePlanId);
            if (overlapping != null && !acceptedRows.Any(a => a.PlanID == overlapping.ID))
            {
                return excludePlanId.HasValue ? OverlapOtherMessage : OverlapMessage;
            }

            return null;
        }

        private static void LoadProductDropDown(DBTHSNEntities db, int? selectedProductId, dynamic viewBag)
        {
            viewBag.Products = new SelectList(
                db.PRODUCTS.AsNoTracking()
                    .Where(p => p.IsDeleted == false && p.IsFinishedGood == true)
                    .OrderBy(p => p.PNo)
                    .Select(p => new { p.ID, p.PNo })
                    .ToList(),
                "ID",
                "PNo",
                selectedProductId);
        }

        private void LoadProductDropDown(DBTHSNEntities db, int? selectedProductId)
        {
            LoadProductDropDown(db, selectedProductId, ViewBag);
        }

        private void LoadSalesOrderDropDown(DBTHSNEntities db, int? selectedSalesOrderId)
        {
            var salesOrders = db.S_ORDERS.AsNoTracking()
                .Where(o => o.IsDeleted != true)
                .OrderByDescending(o => o.IssuedDate)
                .Take(500)
                .Select(o => new { o.ID, o.OrderNo, o.IssuedDate })
                .ToList()
                .Select(o => new
                {
                    o.ID,
                    Name = $"{o.OrderNo} ({o.IssuedDate:dd.MM.yyyy})"
                })
                .ToList();

            ViewBag.SalesOrders = new SelectList(salesOrders, "ID", "Name", selectedSalesOrderId);
        }

        /// <summary>Kunlik reja qatorlari: miqdor kunlarga teng (yuqoriga yaxlitlab) taqsimlanadi.</summary>
        private static void AddDailyPlans(DBTHSNEntities db, PRODUCTPLAN plan, DateTime startDate, DateTime endDate, double totalQty)
        {
            var dayCount = (endDate.Date - startDate.Date).Days + 1;
            if (dayCount <= 0)
                return;
            var dailyQty = Math.Ceiling(totalQty / dayCount);
            for (int i = 0; i < dayCount; i++)
            {
                db.PRODUCTPLANS_DAILY.Add(new PRODUCTPLANS_DAILY
                {
                    PRODUCTPLAN = plan,
                    Day = startDate.AddDays(i),
                    DayShift = dailyQty,
                    NightShift = 0,
                    Total = dailyQty
                });
            }
        }

        /// <summary>
        /// Reja muddati/miqdori o'zgarganda kunlik qatorlarni qayta taqsimlaydi. Mavjud kun qatorlari (ID saqlanadi —
        /// eski QUALITY_INSPECTION.ProductPlanDailyID bog'lanishlari buzilmaydi) yangilanadi, yangi kunlar qo'shiladi;
        /// oraliqdan tashqaridagi qatorlar o'chiriladi, sifat nazoratiga bog'langanlari esa 0 qilinadi.
        /// Qo'lda kiritilgan kunlik o'zgarishlar qayta yoziladi.
        /// </summary>
        private static async Task RegenerateDailyPlansAsync(DBTHSNEntities db, PRODUCTPLAN plan)
        {
            var existing = await db.PRODUCTPLANS_DAILY.Where(d => d.PlanID == plan.ID).ToListAsync();
            var dayCount = (plan.DueDate.Date - plan.StartDate.Date).Days + 1;
            var dailyQty = dayCount > 0 ? Math.Ceiling(plan.Amount / dayCount) : 0;

            var byDay = existing.GroupBy(d => d.Day.Date).ToDictionary(g => g.Key, g => g.ToList());
            var keptIds = new HashSet<int>();
            for (int i = 0; i < dayCount; i++)
            {
                var day = plan.StartDate.Date.AddDays(i);
                if (byDay.TryGetValue(day, out var rowsForDay))
                {
                    var row = rowsForDay.First();
                    row.DayShift = dailyQty;
                    row.NightShift = 0;
                    row.Total = dailyQty;
                    keptIds.Add(row.ID);
                }
                else
                {
                    db.PRODUCTPLANS_DAILY.Add(new PRODUCTPLANS_DAILY
                    {
                        PlanID = plan.ID,
                        Day = day,
                        DayShift = dailyQty,
                        NightShift = 0,
                        Total = dailyQty
                    });
                }
            }

            var obsolete = existing.Where(d => !keptIds.Contains(d.ID)).ToList();
            if (obsolete.Count == 0)
                return;

            var obsoleteIds = obsolete.Select(d => d.ID).ToList();
            var referencedIds = new HashSet<int>(await db.QUALITY_INSPECTION
                .Where(q => q.ProductPlanDailyID.HasValue && obsoleteIds.Contains(q.ProductPlanDailyID.Value))
                .Select(q => q.ProductPlanDailyID.Value)
                .Distinct()
                .ToListAsync());

            foreach (var row in obsolete)
            {
                if (referencedIds.Contains(row.ID))
                {
                    row.DayShift = 0;
                    row.NightShift = 0;
                    row.Total = 0;
                }
                else
                {
                    db.PRODUCTPLANS_DAILY.Remove(row);
                }
            }
        }

        private static async Task<string> ValidateSalesOrderContainsProductAsync(DBTHSNEntities db, int? salesOrderId, int productId, double? planQty = null)
        {
            if (!salesOrderId.HasValue)
                return null;

            var amounts = await db.S_ORDER_PRODUCTS
                .Where(x => x.OrderID == salesOrderId.Value && x.ProductID == productId)
                .Select(x => x.Amount)
                .ToListAsync();
            if (!amounts.Any())
                return "Xatolik: ushbu savdo buyurtmasida tanlangan mahsulot yo'q.";

            if (planQty.HasValue)
            {
                var orderedQty = amounts.Sum();
                if (planQty.Value > orderedQty)
                {
                    return $"Xatolik: reja miqdori ({planQty.Value:N2}) savdo buyurtmasi miqdoridan ({orderedQty:N2}) oshib ketdi.";
                }
            }

            return null;
        }

        private static string BuildErrorMessage(Exception ex)
        {
            var errorMessage = $"Xatolik: {ex.Message}";
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
                errorMessage += $" | Ichki xatolik: {inner.Message}";
            return errorMessage;
        }
    }
}
