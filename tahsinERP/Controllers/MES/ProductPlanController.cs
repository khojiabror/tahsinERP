using System;
using System.Collections.Generic;
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

namespace tahsinERP.Controllers.MES
{
    public class ProductPlanController : Controller
    {
        // Olish so'rovi: ProductPlan
        public async Task<ActionResult> Index(int? productID, DateTime? fromDate, DateTime? toDate)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.PRODUCTPLANS
                    .Include(p => p.PRODUCT)
                    .Include(p => p.S_ORDERS)
                    .Where(p => p.IsDeleted == false)
                    .AsQueryable();

                if (productID.HasValue)
                {
                    query = query.Where(p => p.ProductID == productID.Value);
                }

                if (fromDate.HasValue)
                {
                    query = query.Where(p => p.StartDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(p => p.DueDate <= toDate.Value);
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
                    FromDate = fromDate ?? DateTime.Now.AddMonths(-3),
                    ToDate = toDate ?? DateTime.Now.AddMonths(3)
                };

                ViewBag.Products = new SelectList(
                    await db.PRODUCTS.Where(pr => pr.IsDeleted == false && pr.IsFinishedGood == true).OrderBy(pr => pr.PNo).ToListAsync(),
                    "ID", "PNo", productID);

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
                var plan = db.PRODUCTPLANS
                    .Where(p => p.ID == id && p.IsDeleted == false)
                    .Include(p => p.PRODUCT)
                    .Include(p => p.S_ORDERS)
                    .FirstOrDefault();

                if (plan == null)
                {
                    return HttpNotFound();
                }

                // Olish daily plans
                var dailyPlans = db.PRODUCTPLANS_DAILY
                    .Where(d => d.PlanID == plan.ID)
                    .OrderBy(d => d.Day)
                    .ToList();

                // Olish actual production ma'lumot uchun comparison
                var planDates = dailyPlans.Select(d => d.Day.Date).ToList();
                // Yuklash production ma'lumot birinchi, so'ng filtrlash in memory
                // Reja kuni: fakt sanasi yoki Comment dagi [RejaSana:yyyy-MM-dd] (ProductProductionPlanDateMeta)
                var allProductions = db.PRODUCT_PRODUCTION
                    .Where(p => p.ProductID == plan.ProductID
                        && (p.IsDeleted == false || p.IsDeleted == null))
                    .ToList();

                var actualProductions = AggregateActualProductionByPlanCalendarDay(planDates, allProductions);

                // Yaratish daily plan view models bilan actual ma'lumot
                var dailyPlanViewModels = dailyPlans.Select(d => new ProductPlanDailyViewModel
                {
                    ID = d.ID,
                    PlanID = d.PlanID,
                    Day = d.Day,
                    DayShift = d.DayShift,
                    NightShift = d.NightShift,
                    Total = d.Total,
                    ActualDayShift = actualProductions.ContainsKey(d.Day.Date) ? actualProductions[d.Day.Date].DayShift : (double?)null,
                    ActualNightShift = actualProductions.ContainsKey(d.Day.Date) ? actualProductions[d.Day.Date].NightShift : (double?)null,
                    ActualTotal = actualProductions.ContainsKey(d.Day.Date) ? actualProductions[d.Day.Date].Total : (double?)null
                }).ToList();

                // Link ga delivery schedules agar product matches
                var linkedSchedules = db.SORDER_DELIVERY_SCHEDULE
                    .Include(s => s.PRODUCT)
                    .Include(s => s.S_ORDERS)
                    .Where(s => s.ProductID == plan.ProductID
                        && s.DeliveryDate >= plan.StartDate
                        && s.DeliveryDate <= plan.DueDate)
                    .OrderBy(s => s.DeliveryDate)
                    .ToList();

                var viewModel = new ProductPlanViewModel
                {
                    ID = plan.ID,
                    ProductID = plan.ProductID,
                    TotalQty = plan.Amount,
                    StartDate = plan.StartDate,
                    EndDate = plan.DueDate,
                    IsDeleted = plan.IsDeleted,
                    Product = plan.PRODUCT,
                    DailyPlans = dailyPlanViewModels,
                    LinkedDeliverySchedules = linkedSchedules
                };
                viewModel.SalesOrderID = plan.SalesOrderID;
                viewModel.SalesOrderNo = plan.S_ORDERS?.OrderNo;

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

                var created = 0;
                var errors = new List<string>();
                foreach (var row in model.Rows)
                {
                    if (!row.ProductID.HasValue || !row.TotalQty.HasValue || !row.StartDate.HasValue || !row.EndDate.HasValue)
                    {
                        continue;
                    }

                    if (row.TotalQty.Value <= 0)
                    {
                        errors.Add("Miqdor 0 dan katta bo'lishi kerak.");
                        continue;
                    }

                    if (row.EndDate.Value < row.StartDate.Value)
                    {
                        errors.Add("Tugash sanasi boshlanish sanasidan kichik bo'lishi mumkin emas.");
                        continue;
                    }

                    var soValidation = await ValidateSalesOrderContainsProductAsync(db, row.SalesOrderID, row.ProductID.Value, row.TotalQty);
                    if (soValidation != null)
                    {
                        errors.Add(soValidation);
                        continue;
                    }

                    var overlappingPlan = await db.PRODUCTPLANS.FirstOrDefaultAsync(p => p.ProductID == row.ProductID.Value
                        && p.IsDeleted == false
                        && ((p.StartDate <= row.StartDate.Value && p.DueDate >= row.StartDate.Value)
                            || (p.StartDate <= row.EndDate.Value && p.DueDate >= row.EndDate.Value)
                            || (p.StartDate >= row.StartDate.Value && p.DueDate <= row.EndDate.Value)));
                    if (overlappingPlan != null)
                    {
                        errors.Add("Ushbu mahsulot uchun tanlangan muddatlarda allaqachon reja mavjud.");
                        continue;
                    }

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
                    await db.SaveChangesAsync();
                    await CreateDailyPlansAsync(db, plan.ID, row.StartDate.Value, row.EndDate.Value, row.TotalQty.Value);
                    created++;
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
                var plans = await db.PRODUCTPLANS
                    .Where(p => p.IsDeleted == false && p.StartDate.Year == y && p.StartDate.Month == m)
                    .OrderBy(p => p.StartDate)
                    .ThenBy(p => p.ProductID)
                    .ToListAsync();

                var vm = new ProductPlanGridEntryViewModel
                {
                    Year = y,
                    Month = m,
                    Rows = plans.Select(p => new ProductPlanGridRowViewModel
                    {
                        PlanID = p.ID,
                        ProductID = p.ProductID,
                        TotalQty = p.Amount,
                        StartDate = p.StartDate,
                        EndDate = p.DueDate,
                        SalesOrderID = p.SalesOrderID
                    }).ToList()
                };

                if (!vm.Rows.Any())
                {
                    vm.Rows.Add(new ProductPlanGridRowViewModel
                    {
                        StartDate = new DateTime(y, m, 1),
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

                foreach (var row in model.Rows)
                {
                    if (!row.ProductID.HasValue || !row.TotalQty.HasValue || !row.StartDate.HasValue || !row.EndDate.HasValue)
                    {
                        continue;
                    }
                    if (row.TotalQty.Value <= 0)
                    {
                        errors.Add("Miqdor 0 dan katta bo'lishi kerak.");
                        continue;
                    }
                    if (row.EndDate.Value < row.StartDate.Value)
                    {
                        errors.Add("Tugash sanasi boshlanish sanasidan kichik bo'lishi mumkin emas.");
                        continue;
                    }

                    var soValidation = await ValidateSalesOrderContainsProductAsync(db, row.SalesOrderID, row.ProductID.Value, row.TotalQty);
                    if (soValidation != null)
                    {
                        errors.Add(soValidation);
                        continue;
                    }

                    if (row.PlanID.HasValue)
                    {
                        var plan = await db.PRODUCTPLANS.FirstOrDefaultAsync(p => p.ID == row.PlanID.Value && p.IsDeleted == false);
                        if (plan == null)
                            continue;

                        var overlappingPlan = await db.PRODUCTPLANS.FirstOrDefaultAsync(p => p.ID != row.PlanID.Value
                            && p.ProductID == row.ProductID.Value
                            && p.IsDeleted == false
                            && ((p.StartDate <= row.StartDate.Value && p.DueDate >= row.StartDate.Value)
                                || (p.StartDate <= row.EndDate.Value && p.DueDate >= row.EndDate.Value)
                                || (p.StartDate >= row.StartDate.Value && p.DueDate <= row.EndDate.Value)));
                        if (overlappingPlan != null)
                        {
                            errors.Add("Ushbu mahsulot uchun tanlangan muddatlarda boshqa reja mavjud.");
                            continue;
                        }

                        plan.ProductID = row.ProductID.Value;
                        plan.Amount = row.TotalQty.Value;
                        plan.StartDate = row.StartDate.Value;
                        plan.DueDate = row.EndDate.Value;
                        plan.SalesOrderID = row.SalesOrderID;
                        db.Entry(plan).State = EntityState.Modified;
                        updated++;
                    }
                    else
                    {
                        var overlappingPlan = await db.PRODUCTPLANS.FirstOrDefaultAsync(p => p.ProductID == row.ProductID.Value
                            && p.IsDeleted == false
                            && ((p.StartDate <= row.StartDate.Value && p.DueDate >= row.StartDate.Value)
                                || (p.StartDate <= row.EndDate.Value && p.DueDate >= row.EndDate.Value)
                                || (p.StartDate >= row.StartDate.Value && p.DueDate <= row.EndDate.Value)));
                        if (overlappingPlan != null)
                        {
                            errors.Add("Ushbu mahsulot uchun tanlangan muddatlarda allaqachon reja mavjud.");
                            continue;
                        }

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
                        await db.SaveChangesAsync();
                        await CreateDailyPlansAsync(db, plan.ID, row.StartDate.Value, row.EndDate.Value, row.TotalQty.Value);
                        inserted++;
                    }
                }

                await db.SaveChangesAsync();
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

                        // Tekshirish uchun overlapping plans
                        var overlappingPlan = db.PRODUCTPLANS
                            .FirstOrDefault(p => p.ProductID == viewModel.ProductID
                                && p.IsDeleted == false
                                && ((p.StartDate <= viewModel.StartDate && p.DueDate >= viewModel.StartDate)
                                    || (p.StartDate <= viewModel.EndDate && p.DueDate >= viewModel.EndDate)
                                    || (p.StartDate >= viewModel.StartDate && p.DueDate <= viewModel.EndDate)));

                        if (overlappingPlan != null)
                        {
                            ModelState.AddModelError("", "Ushbu mahsulot uchun tanlangan muddatlarda allaqachon reja mavjud");
                            LoadProductDropDown(db, viewModel.ProductID);
                            LoadSalesOrderDropDown(db, viewModel.SalesOrderID);
                            return View(viewModel);
                        }

                        // Yaratish plan
                        var plan = new PRODUCTPLAN
                        {
                            ProductID = viewModel.ProductID,
                            Amount = viewModel.TotalQty,
                            StartDate = viewModel.StartDate,
                            DueDate = viewModel.EndDate,
                            IsDeleted = false,
                            SalesOrderID = viewModel.SalesOrderID
                        };

                        db.PRODUCTPLANS.Add(plan);
                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "ProductPlanController", $"ProductPlan yaratildi: ID {plan.ID}, ProductID {plan.ProductID}");

                        // Auto-yaratish PRODUCTPLANS_DAILY qatorlar uchun har bir day
                        var dayCount = (viewModel.EndDate - viewModel.StartDate).Days + 1;
                        var dailyQty = Math.Ceiling(viewModel.TotalQty / dayCount);
                        var currentDate = viewModel.StartDate;

                        for (int i = 0; i < dayCount; i++)
                        {
                            var dailyPlan = new PRODUCTPLANS_DAILY
                            {
                                PlanID = plan.ID,
                                Day = currentDate,
                                DayShift = dailyQty,
                                NightShift = 0,
                                Total = dailyQty
                            };

                            db.PRODUCTPLANS_DAILY.Add(dailyPlan);
                            currentDate = currentDate.AddDays(1);
                        }

                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "ProductPlanController", $"ProductPlanDaily yaratildi: Plan ID {plan.ID}, {dayCount} kunlik reja");

                        // Link ga SORDER_DELIVERY_SCHEDULE automatically agar product matches
                        var matchingSchedules = db.SORDER_DELIVERY_SCHEDULE
                            .Where(s => s.ProductID == viewModel.ProductID
                                && s.DeliveryDate >= viewModel.StartDate
                                && s.DeliveryDate <= viewModel.EndDate)
                            .ToList();

                        // Note: Matching schedules found but no database linking occurs, so no log needed

                        TempData["Success"] = "Ishlab chiqarish rejasi muvaffaqiyatli yaratildi";
                        return RedirectToAction("Details", new { id = plan.ID });
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
                var plan = db.PRODUCTPLANS.Find(id);
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
                    var orderNo = await db.S_ORDERS
                        .Where(o => o.ID == plan.SalesOrderID.Value)
                        .Select(o => o.OrderNo)
                        .FirstOrDefaultAsync();
                    viewModel.SalesOrderNo = orderNo;
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
                    var plan = db.PRODUCTPLANS.Find(viewModel.ID);
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

                        // Tekshirish uchun overlapping plans (excluding current plan)
                        var overlappingPlan = db.PRODUCTPLANS
                            .FirstOrDefault(p => p.ID != viewModel.ID
                                && p.ProductID == viewModel.ProductID
                                && p.IsDeleted == false
                                && ((p.StartDate <= viewModel.StartDate && p.DueDate >= viewModel.StartDate)
                                    || (p.StartDate <= viewModel.EndDate && p.DueDate >= viewModel.EndDate)
                                    || (p.StartDate >= viewModel.StartDate && p.DueDate <= viewModel.EndDate)));

                        if (overlappingPlan != null)
                        {
                            ModelState.AddModelError("", "Ushbu mahsulot uchun tanlangan muddatlarda boshqa reja mavjud");
                            LoadProductDropDown(db, viewModel.ProductID);
                            LoadSalesOrderDropDown(db, viewModel.SalesOrderID);
                            return View(viewModel);
                        }

                        // Yangilash plan
                        plan.ProductID = viewModel.ProductID;
                        plan.Amount = viewModel.TotalQty;
                        plan.StartDate = viewModel.StartDate;
                        plan.DueDate = viewModel.EndDate;
                        plan.SalesOrderID = viewModel.SalesOrderID;

                        db.Entry(plan).State = EntityState.Modified;
                        await db.SaveChangesAsync();

                        LogHelper.LogToDatabase(User.Identity.Name, "ProductPlanController", $"ProductPlan tahrirlandi: ID {viewModel.ID}");

                        TempData["Success"] = "Ishlab chiqarish rejasi muvaffaqiyatli yangilandi";
                        return RedirectToAction("Details", new { id = viewModel.ID });
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
                var plan = db.PRODUCTPLANS
                    .Include(p => p.PRODUCT)
                    .Include(p => p.S_ORDERS)
                    .FirstOrDefault(p => p.ID == id);

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
                db.Entry(plan).State = EntityState.Modified;
                SoftDeleteReferenceService.AfterProductPlanSoftDeleted(db, id);
                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "ProductPlanController", $"ProductPlan o'chirildi: ID {id}");

                TempData["Success"] = "Ishlab chiqarish rejasi muvaffaqiyatli o'chirildi";
                return RedirectToAction("Index");
            }
        }

        // POST so'rovi: ProductPlan/UpdateDailyPlan - Inline editable daily grid
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
                db.Entry(plan).State = EntityState.Modified;
                await SoftDeleteReferenceService.AfterProductPlanSoftDeletedAsync(db, id);
                await db.SaveChangesAsync();

                TempData["Success"] = "Reja o'chirildi";
                return RedirectToAction("GridEdit", new { year, month });
            }
        }

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
                    foreach (var item in dailyPlans)
                    {
                        var dailyPlan = db.PRODUCTPLANS_DAILY.Find(item.ID);
                        if (dailyPlan != null && dailyPlan.PlanID == planID)
                        {
                            dailyPlan.DayShift = item.DayShift;
                            dailyPlan.NightShift = item.NightShift;
                            dailyPlan.Total = item.DayShift + item.NightShift; // Auto-calculate
                            db.Entry(dailyPlan).State = EntityState.Modified;
                        }
                    }

                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "ProductPlanController", $"Kunlik reja yangilandi: Plan ID {planID}");

                    return Json(new { success = true, message = "Kunlik reja muvaffaqiyatli yangilandi" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Xatolik: {ex.Message}" });
                }
            }
        }

        // Olish so'rovi: ProductPlan/GetDailyGridPartial/5 - Partial view uchun daily grid
        public ActionResult GetDailyGridPartial(int planID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var plan = db.PRODUCTPLANS.Find(planID);
                if (plan == null)
                {
                    return HttpNotFound();
                }

                var dailyPlans = db.PRODUCTPLANS_DAILY
                    .Where(d => d.PlanID == planID)
                    .OrderBy(d => d.Day)
                    .ToList();

                // Olish actual production ma'lumot
                var planDates = dailyPlans.Select(d => d.Day.Date).ToList();
                var allProductions = db.PRODUCT_PRODUCTION
                    .Where(p => p.ProductID == plan.ProductID
                        && (p.IsDeleted == false || p.IsDeleted == null))
                    .ToList();

                var actualProductions = AggregateActualProductionByPlanCalendarDay(planDates, allProductions);

                var dailyPlanViewModels = dailyPlans.Select(d => new ProductPlanDailyViewModel
                {
                    ID = d.ID,
                    PlanID = d.PlanID,
                    Day = d.Day,
                    DayShift = d.DayShift,
                    NightShift = d.NightShift,
                    Total = d.Total,
                    ActualDayShift = actualProductions.ContainsKey(d.Day.Date) ? actualProductions[d.Day.Date].DayShift : (double?)null,
                    ActualNightShift = actualProductions.ContainsKey(d.Day.Date) ? actualProductions[d.Day.Date].NightShift : (double?)null,
                    ActualTotal = actualProductions.ContainsKey(d.Day.Date) ? actualProductions[d.Day.Date].Total : (double?)null
                }).ToList();

                return PartialView("_DailyGridPartial", dailyPlanViewModels);
            }
        }

        // POST so'rovi: ProductPlan/CalculateRequiredRawMaterials
        [HttpPost]
        public async Task<ActionResult> CalculateRequiredRawMaterials(int planId)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var plan = await db.PRODUCTPLANS
                    .Include(p => p.PRODUCT)
                    .FirstOrDefaultAsync(p => p.ID == planId && p.IsDeleted == false);

                if (plan == null)
                {
                    return Json(new { success = false, message = "Reja topilmadi" });
                }

                try
                {
                    var consumptionService = new ProductionConsumptionService(db);
                    var requirements = await consumptionService.CalculateRequiredRawMaterialsAsync(
                        plan.ProductID, plan.Amount);

                    var result = requirements.Select(r => new
                    {
                        MaterialID = r.MaterialID,
                        MaterialPNo = r.MaterialPNo,
                        MaterialName = r.MaterialName,
                        RequiredQtyPieces = Math.Round(r.RequiredQtyPieces, 2),
                        RequiredQtyMeters = Math.Round(r.RequiredQtyMeters, 2),
                        WasteQty = Math.Round(r.WasteQty, 2),
                        TotalConsumption = Math.Round(r.TotalConsumption, 2),
                        UnitID = r.UnitID,
                        RequiresCutting = r.RequiresCutting
                    }).ToList();

                    return Json(new { success = true, data = result, planQty = plan.Amount });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Xatolik: {ex.Message}" });
                }
            }
        }

        // Olish so'rovi: ProductPlan/RawMaterialsPreview/5
        public async Task<ActionResult> RawMaterialsPreview(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var plan = await db.PRODUCTPLANS
                    .Include(p => p.PRODUCT)
                    .FirstOrDefaultAsync(p => p.ID == id && p.IsDeleted == false);

                if (plan == null)
                {
                    return HttpNotFound();
                }

                var consumptionService = new ProductionConsumptionService(db);
                var requirements = await consumptionService.CalculateRequiredRawMaterialsAsync(
                    plan.ProductID, plan.Amount);

                ViewBag.Plan = plan;
                ViewBag.Requirements = requirements;

                return View(requirements);
            }
        }

        private async Task<string> ValidateRawMaterialStockAsync(int productId, double producedQty, DBTHSNEntities db)
        {
            var productionWarehouse = WarehouseFlowService.GetProductionWarehouse(db);
            if (productionWarehouse == null)
            {
                return "Ishlab chiqarish ombori topilmadi";
            }

            var consumptionService = new ProductionConsumptionService(db);
            var requirements = await consumptionService.CalculateRequiredRawMaterialsAsync(productId, producedQty);
            if (requirements == null || requirements.Count == 0)
            {
                return null;
            }

            foreach (var req in requirements)
            {
                var stock = await db.PRODUCT_STOCKS
                    .Include(s => s.UNIT)
                    .FirstOrDefaultAsync(s => s.WHID == productionWarehouse.ID && s.ProductID == req.MaterialID);

                if (stock == null)
                {
                    return $"Ishlab chiqarish omborida mahsulot qoldig'i topilmadi: {req.MaterialPNo ?? req.MaterialID.ToString()}";
                }

                double requiredQtyToCompare = req.TotalConsumption;
                string requiredUnitName = "";
                bool stockInKg = false;
                if (stock.UNIT != null)
                {
                    var unitName = (stock.UNIT.UnitName ?? "") + " " + (stock.UNIT.ShortName ?? "");
                    stockInKg = unitName.IndexOf("kg", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                var availableUnitName = (stock.UNIT != null) ? (stock.UNIT.ShortName ?? stock.UNIT.UnitName ?? "").Trim() : "";

                // Ombor birligi kg va mahsulotda Weight bo'lsa, talab donada bo'lishi mumkin — kg ga o'tkazamiz
                if (!req.TotalConsumptionIsInKg && stockInKg)
                {
                    var material = await db.PRODUCTS
                        .FirstOrDefaultAsync(p => p.ID == req.MaterialID);
                    if (material != null && material.Weight.HasValue && material.Weight.Value > 0)
                    {
                        requiredQtyToCompare = req.TotalConsumption * material.Weight.Value;
                        requiredUnitName = "kg";
                    }
                }
                if (string.IsNullOrEmpty(requiredUnitName))
                {
                    var reqUnit = await db.UNITS.FindAsync(req.UnitID);
                    requiredUnitName = (reqUnit != null) ? (reqUnit.ShortName ?? reqUnit.UnitName ?? "").Trim() : "";
                }
                if (req.TotalConsumptionIsInKg && stockInKg)
                {
                    requiredUnitName = "kg";
                }

                if (stock.Amount < requiredQtyToCompare)
                {
                    var availableWithUnit = string.IsNullOrEmpty(availableUnitName)
                        ? stock.Amount.ToString("N2")
                        : $"{stock.Amount:N2} {availableUnitName}";
                    var requiredWithUnit = string.IsNullOrEmpty(requiredUnitName)
                        ? requiredQtyToCompare.ToString("N2")
                        : $"{requiredQtyToCompare:N2} {requiredUnitName}";
                    return
                        $"Ishlab chiqarish omborida yetarli qoldiq yo'q. Mahsulot: {req.MaterialPNo ?? req.MaterialID.ToString()}, {availableWithUnit} omborda mavjud, {requiredWithUnit} kerak.";
                }
            }

            return null;
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

        private void LoadProductDropDown(DBTHSNEntities db, int? selectedProductId)
        {
            ViewBag.Products = new SelectList(
                db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).OrderBy(p => p.PNo).ToList(),
                "ID",
                "PNo",
                selectedProductId);
        }

        private void LoadSalesOrderDropDown(DBTHSNEntities db, int? selectedSalesOrderId)
        {
            var salesOrders = db.S_ORDERS
                .Where(o => o.IsDeleted != true)
                .OrderByDescending(o => o.IssuedDate)
                .Take(500)
                .ToList()
                .Select(o => new
                {
                    o.ID,
                    Name = $"{o.OrderNo} ({o.IssuedDate:dd.MM.yyyy})"
                })
                .ToList();

            ViewBag.SalesOrders = new SelectList(salesOrders, "ID", "Name", selectedSalesOrderId);
        }

        private async Task CreateDailyPlansAsync(DBTHSNEntities db, int planId, DateTime startDate, DateTime endDate, double totalQty)
        {
            var dayCount = (endDate - startDate).Days + 1;
            if (dayCount <= 0)
                return;
            var dailyQty = Math.Ceiling(totalQty / dayCount);
            var currentDate = startDate;
            for (int i = 0; i < dayCount; i++)
            {
                db.PRODUCTPLANS_DAILY.Add(new PRODUCTPLANS_DAILY
                {
                    PlanID = planId,
                    Day = currentDate,
                    DayShift = dailyQty,
                    NightShift = 0,
                    Total = dailyQty
                });
                currentDate = currentDate.AddDays(1);
            }
            await db.SaveChangesAsync();
        }

        private async Task<string> ValidateSalesOrderContainsProductAsync(DBTHSNEntities db, int? salesOrderId, int productId, double? planQty = null)
        {
            if (!salesOrderId.HasValue)
                return null;

            var lines = await db.S_ORDER_PRODUCTS
                .Where(x => x.OrderID == salesOrderId.Value && x.ProductID == productId)
                .ToListAsync();
            var exists = lines.Any();
            if (!exists)
                return "Xatolik: ushbu savdo buyurtmasida tanlangan mahsulot yo'q.";

            if (planQty.HasValue)
            {
                var orderedQty = lines.Sum(x => x.Amount);
                if (planQty.Value > orderedQty)
                {
                    return $"Xatolik: reja miqdori ({planQty.Value:N2}) savdo buyurtmasi miqdoridan ({orderedQty:N2}) oshib ketdi.";
                }
            }

            return null;
        }
    }
}
