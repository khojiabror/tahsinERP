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
using tahsinERP.Services;

namespace tahsinERP.Controllers.MES
{
    public class ProductPlanController : Controller
    {
        // GET: ProductPlan
        public async Task<ActionResult> Index(int? productID, DateTime? fromDate, DateTime? toDate)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.PRODUCTPLANS
                    .Include(p => p.PRODUCT)
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
                        Product = p.PRODUCT
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

        // GET: ProductPlan/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var plan = db.PRODUCTPLANS
                    .Where(p => p.ID == id && (p.IsDeleted == false || p.IsDeleted == null))
                    .Include(p => p.PRODUCT)
                    .FirstOrDefault();

                if (plan == null)
                {
                    return HttpNotFound();
                }

                // Get daily plans
                var dailyPlans = db.PRODUCTPLANS_DAILY
                    .Where(d => d.PlanID == plan.ID)
                    .OrderBy(d => d.Day)
                    .ToList();

                // Get actual production data for comparison
                var planDates = dailyPlans.Select(d => d.Day.Date).ToList();
                // Load production data first, then filter in memory
                var allProductions = db.PRODUCT_PRODUCTION
                    .Where(p => p.ProductID == plan.ProductID)
                    .ToList();

                var actualProductions = allProductions
                    .Where(p => planDates.Contains(p.Date.Date))
                    .GroupBy(p => p.Date.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        DayShift = g.Sum(p => p.DayShift),
                        NightShift = g.Sum(p => p.NightShift),
                        Total = g.Sum(p => p.Total)
                    })
                    .ToDictionary(x => x.Date, x => x);

                // Create daily plan view models with actual data
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

                // Link to delivery schedules if product matches
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

                return View(viewModel);
            }
        }

        // GET: ProductPlan/Create
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var viewModel = new ProductPlanViewModel();
                ViewBag.Products = new SelectList(
                    db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).OrderBy(p => p.PNo).ToList(),
                    "ID", "PNo");
                return View(viewModel);
            }
        }

        // POST: ProductPlan/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ProductPlanViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        // Validate date range
                        if (viewModel.EndDate < viewModel.StartDate)
                        {
                            ModelState.AddModelError("", "Tugash sanasi boshlanish sanasidan kichik bo'lishi mumkin emas");
                            ViewBag.Products = new SelectList(
                                db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).OrderBy(p => p.PNo).ToList(),
                                "ID", "PNo", viewModel.ProductID);
                            return View(viewModel);
                        }

                        // Check for overlapping plans
                        var overlappingPlan = db.PRODUCTPLANS
                            .FirstOrDefault(p => p.ProductID == viewModel.ProductID
                                && p.IsDeleted == false
                                && ((p.StartDate <= viewModel.StartDate && p.DueDate >= viewModel.StartDate)
                                    || (p.StartDate <= viewModel.EndDate && p.DueDate >= viewModel.EndDate)
                                    || (p.StartDate >= viewModel.StartDate && p.DueDate <= viewModel.EndDate)));

                        if (overlappingPlan != null)
                        {
                            ModelState.AddModelError("", "Ushbu mahsulot uchun tanlangan muddatlarda allaqachon reja mavjud");
                            ViewBag.Products = new SelectList(
                                db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).OrderBy(p => p.PNo).ToList(),
                                "ID", "PNo", viewModel.ProductID);
                            return View(viewModel);
                        }

                        // Create plan
                        var plan = new PRODUCTPLAN
                        {
                            ProductID = viewModel.ProductID,
                            Amount = viewModel.TotalQty,
                            StartDate = viewModel.StartDate,
                            DueDate = viewModel.EndDate,
                            IsDeleted = false
                        };

                        db.PRODUCTPLANS.Add(plan);
                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "ProductPlanController", $"ProductPlan yaratildi: ID {plan.ID}, ProductID {plan.ProductID}");

                        // Auto-generate PRODUCTPLANS_DAILY rows for each day
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

                        db.SaveChanges();

                        LogHelper.LogToDatabase(User.Identity.Name, "ProductPlanController", $"ProductPlanDaily yaratildi: Plan ID {plan.ID}, {dayCount} kunlik reja");

                        // Link to SORDER_DELIVERY_SCHEDULE automatically if product matches
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
                ViewBag.Products = new SelectList(
                    db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).OrderBy(p => p.PNo).ToList(),
                    "ID", "PNo", viewModel.ProductID);
            }

            return View(viewModel);
        }

        // GET: ProductPlan/Edit/5
        public ActionResult Edit(int? id)
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
                    IsDeleted = plan.IsDeleted
                };

                ViewBag.Products = new SelectList(
                    db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).OrderBy(p => p.PNo).ToList(),
                    "ID", "PNo", viewModel.ProductID);

                return View(viewModel);
            }
        }

        // POST: ProductPlan/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(ProductPlanViewModel viewModel)
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
                        // Validate date range
                        if (viewModel.EndDate < viewModel.StartDate)
                        {
                            ModelState.AddModelError("", "Tugash sanasi boshlanish sanasidan kichik bo'lishi mumkin emas");
                            ViewBag.Products = new SelectList(
                                db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).OrderBy(p => p.PNo).ToList(),
                                "ID", "PNo", viewModel.ProductID);
                            return View(viewModel);
                        }

                        // Check for overlapping plans (excluding current plan)
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
                            ViewBag.Products = new SelectList(
                                db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).OrderBy(p => p.PNo).ToList(),
                                "ID", "PNo", viewModel.ProductID);
                            return View(viewModel);
                        }

                        // Update plan
                        plan.ProductID = viewModel.ProductID;
                        plan.Amount = viewModel.TotalQty;
                        plan.StartDate = viewModel.StartDate;
                        plan.DueDate = viewModel.EndDate;

                        db.Entry(plan).State = EntityState.Modified;
                        db.SaveChanges();

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
                ViewBag.Products = new SelectList(
                    db.PRODUCTS.Where(p => p.IsDeleted == false && p.IsFinishedGood == true).OrderBy(p => p.PNo).ToList(),
                    "ID", "PNo", viewModel.ProductID);
            }

            return View(viewModel);
        }

        // GET: ProductPlan/Delete/5
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
                    .FirstOrDefault(p => p.ID == id);

                if (plan == null)
                {
                    return HttpNotFound();
                }

                return View(plan);
            }
        }

        // POST: ProductPlan/Delete/5
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
                db.SaveChanges();

                LogHelper.LogToDatabase(User.Identity.Name, "ProductPlanController", $"ProductPlan o'chirildi: ID {id}");

                TempData["Success"] = "Ishlab chiqarish rejasi muvaffaqiyatli o'chirildi";
                return RedirectToAction("Index");
            }
        }

        // POST: ProductPlan/UpdateDailyPlan - Inline editable daily grid
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

        // GET: ProductPlan/GetDailyGridPartial/5 - Partial view for daily grid
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

                // Get actual production data
                var planDates = dailyPlans.Select(d => d.Day.Date).ToList();
                // Load production data first, then filter in memory
                var allProductions = db.PRODUCT_PRODUCTION
                    .Where(p => p.ProductID == plan.ProductID)
                    .ToList();

                var actualProductions = allProductions
                    .Where(p => planDates.Contains(p.Date.Date))
                    .GroupBy(p => p.Date.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        DayShift = g.Sum(p => p.DayShift),
                        NightShift = g.Sum(p => p.NightShift),
                        Total = g.Sum(p => p.Total)
                    })
                    .ToDictionary(x => x.Date, x => x);

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

        // POST: ProductPlan/CalculateRequiredRawMaterials
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

        // GET: ProductPlan/RawMaterialsPreview/5
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
    }
}
