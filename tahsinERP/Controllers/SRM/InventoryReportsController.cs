using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Inventory Reports uchun Production Warehouse
    /// Reports: DOH (Days on Hand), Coverage, Plan fulfillment
    /// </summary>
    public class InventoryReportsController : Controller
    {
        // Olish so'rovi: InventoryReports/Dashboard
        public async Task<ActionResult> Dashboard(int? warehouseId, int? categoryId, DateTime? fromDate, DateTime? toDate, int? runnerDays, int? runnerTopN, string fifoScope)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "InventoryReports", "Dashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var fromDateValue = fromDate ?? DateTime.Now.AddDays(-30);
                var toDateValue = toDate ?? DateTime.Now;
                var runnerDaysValue = runnerDays.HasValue && runnerDays.Value > 0 ? runnerDays.Value : 30;
                var runnerTopNValue = runnerTopN.HasValue && runnerTopN.Value > 0 ? runnerTopN.Value : 10;
                if (runnerTopNValue > 50) runnerTopNValue = 50;
                var fifoScopeValue = string.IsNullOrWhiteSpace(fifoScope) ? "all" : fifoScope.ToLowerInvariant();

                var model = new InventoryDashboardViewModel
                {
                    FromDate = fromDateValue,
                    ToDate = toDateValue,
                    HighDOHItems = new List<InventoryDashboardItem>(),
                    LowDOHItems = new List<InventoryDashboardItem>(),
                    StockItems = new List<InventoryDashboardItem>(),
                    HighRunnerItems = new List<InventoryDashboardItem>(),
                    LowRunnerItems = new List<InventoryDashboardItem>(),
                    FifoViolations = new List<InventoryFifoViolationItem>(),
                    StockByCategory = new List<CategoryStockSummary>(),
                    InboundTrends = new List<TrendData>(),
                    OutboundTrends = new List<TrendData>()
                };

                // Olish warehouse IDs birinchi (as simple list of integers)
                var warehouseIds = warehouseId.HasValue
                    ? new List<int> { warehouseId.Value }
                    : await db.WAREHOUSES.Where(w => w.IsDeleted != true).Select(w => w.ID).ToListAsync();

                if (!warehouseIds.Any())
                {
                    ViewBag.Error = "Ombor topilmadi";
                    return View(new InventoryDashboardViewModel
                    {
                        FromDate = fromDateValue,
                        ToDate = toDateValue,
                        HighDOHItems = new List<InventoryDashboardItem>(),
                        LowDOHItems = new List<InventoryDashboardItem>(),
                        StockItems = new List<InventoryDashboardItem>(),
                        HighRunnerItems = new List<InventoryDashboardItem>(),
                        LowRunnerItems = new List<InventoryDashboardItem>(),
                        FifoViolations = new List<InventoryFifoViolationItem>(),
                        StockByCategory = new List<CategoryStockSummary>(),
                        InboundTrends = new List<TrendData>(),
                        OutboundTrends = new List<TrendData>()
                    });
                }

                // Olish barcha stocks
                var stocks = await db.PRODUCT_STOCKS
                    .Include(s => s.PRODUCT)
                    .Include(s => s.PRODUCT.PRODUCT_CATEGORIES)
                    .Include(s => s.UNIT)
                    .Include(s => s.Warehouse)
                    .Where(s => warehouseIds.Contains(s.WHID) && s.Amount > 0)
                    .ToListAsync();

                // Filtrlash bo'yicha category agar specified
                if (categoryId.HasValue)
                {
                    stocks = stocks.Where(s => s.PRODUCT != null && s.PRODUCT.CategoryID == categoryId.Value).ToList();
                }

                var totalDays = (toDateValue - fromDateValue).TotalDays;
                if (totalDays <= 0) totalDays = 1;

                // Calculate planned consumption dan production plan (PRODUCTPLANS_DAILY)
                var plannedDaily = await db.PRODUCTPLANS_DAILY
                    .Where(d => d.Day >= fromDateValue && d.Day <= toDateValue && d.PRODUCTPLAN.IsDeleted == false)
                    .Select(d => new { d.PRODUCTPLAN.ProductID, d.Total })
                    .ToListAsync();

                var plannedByProduct = plannedDaily
                    .GroupBy(x => x.ProductID)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Total));

                // Calculate actual consumption dan expenses (warehouse-specific)
                var expenses = await db.EXPENSES
                    .Include(e => e.EXPENSE_PRODUCTS)
                    .Where(e => e.IssueDateTime >= fromDateValue && e.IssueDateTime <= toDateValue && e.IsDeleted != true)
                    .ToListAsync();

                var expenseByProductWarehouse = expenses
                    .SelectMany(e => e.EXPENSE_PRODUCTS.Select(ep => new
                    {
                        ep.ProductID,
                        WarehouseId = e.SenderWHID,
                        Amount = ep.Amount
                    }))
                    .Where(x => warehouseIds.Contains(x.WarehouseId))
                    .GroupBy(x => new { x.ProductID, x.WarehouseId })
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

                foreach (var stock in stocks)
                {
                    if (stock.PRODUCT == null) continue;

                    // Calculate average daily consumption dan production plan
                    var plannedTotal = plannedByProduct.ContainsKey(stock.ProductID)
                        ? plannedByProduct[stock.ProductID]
                        : 0;
                    var avgDailyPlanned = plannedTotal / totalDays;
                    var plannedDoh = avgDailyPlanned > 0 ? stock.Amount / avgDailyPlanned : (double?)null;

                    var expenseKey = new { stock.ProductID, WarehouseId = stock.WHID };
                    var actualTotal = expenseByProductWarehouse.ContainsKey(expenseKey)
                        ? expenseByProductWarehouse[expenseKey]
                        : 0;
                    var avgDailyActual = actualTotal / totalDays;
                    var actualDoh = avgDailyActual > 0 ? stock.Amount / avgDailyActual : (double?)null;

                    var item = new InventoryDashboardItem
                    {
                        ProductID = stock.ProductID,
                        ProductPNo = stock.PRODUCT.PNo ?? "N/A",
                        ProductName = stock.PRODUCT.Name ?? "N/A",
                        CategoryName = stock.PRODUCT.PRODUCT_CATEGORIES?.Name ?? "N/A",
                        WarehouseName = stock.Warehouse?.WHName ?? "N/A",
                        CurrentStock = stock.Amount,
                        UnitName = stock.UNIT?.ShortName ?? "N/A",
                        AverageDailyConsumption = avgDailyActual,
                        PlannedAverageDailyConsumption = avgDailyPlanned,
                        PlannedDOH = plannedDoh,
                        ActualDOH = actualDoh
                    };

                    model.StockItems.Add(item);

                    // Categorize by DOH
                    if (plannedDoh.HasValue)
                    {
                        if (plannedDoh.Value > 60)
                        {
                            model.HighDOHItems.Add(item);
                        }
                        else if (plannedDoh.Value < 30)
                        {
                            model.LowDOHItems.Add(item);
                        }
                    }
                }

                // Low/High runner by average consumption (actual) over selected window
                var runnerFrom = DateTime.Now.AddDays(-runnerDaysValue);
                var runnerExpenses = await db.EXPENSES
                    .Include(e => e.EXPENSE_PRODUCTS)
                    .Where(e => e.IssueDateTime >= runnerFrom && e.IssueDateTime <= DateTime.Now && e.IsDeleted != true)
                    .ToListAsync();

                var runnerTotals = runnerExpenses
                    .SelectMany(e => e.EXPENSE_PRODUCTS.Select(ep => new
                    {
                        ep.ProductID,
                        WarehouseId = e.SenderWHID,
                        Amount = ep.Amount
                    }))
                    .Where(x => warehouseIds.Contains(x.WarehouseId))
                    .GroupBy(x => new { x.ProductID, x.WarehouseId })
                    .Select(g => new { g.Key.ProductID, g.Key.WarehouseId, Total = g.Sum(x => x.Amount) })
                    .ToList();

                var runnerDaysDouble = (double)runnerDaysValue;
                var runnerItems = stocks
                    .Select(s =>
                    {
                        var total = runnerTotals.FirstOrDefault(t => t.ProductID == s.ProductID && t.WarehouseId == s.WHID)?.Total ?? 0;
                        var avg = total / runnerDaysDouble;
                        return new InventoryDashboardItem
                        {
                            ProductID = s.ProductID,
                            ProductPNo = s.PRODUCT?.PNo ?? "N/A",
                            ProductName = s.PRODUCT?.Name ?? "N/A",
                            CategoryName = s.PRODUCT?.PRODUCT_CATEGORIES?.Name ?? "N/A",
                            WarehouseName = s.Warehouse?.WHName ?? "N/A",
                            CurrentStock = s.Amount,
                            UnitName = s.UNIT?.ShortName ?? "N/A",
                            AverageDailyConsumption = avg
                        };
                    })
                    .Where(i => i.AverageDailyConsumption > 0)
                    .OrderByDescending(i => i.AverageDailyConsumption)
                    .ToList();

                model.HighRunnerItems = runnerItems.Take(runnerTopNValue).ToList();
                model.LowRunnerItems = runnerItems.OrderBy(i => i.AverageDailyConsumption).Take(runnerTopNValue).ToList();

                // Simplified FIFO violation report
                var fifoWarehouseIds = fifoScopeValue == "selected"
                    ? (warehouseId.HasValue ? new List<int> { warehouseId.Value } : new List<int>())
                    : warehouseIds;

                if (fifoScopeValue == "selected" && !warehouseId.HasValue)
                {
                    ViewBag.FifoScopeWarning = "FIFO uchun ombor tanlanmagan. 'Tanlangan ombor' rejimida natija chiqmaydi.";
                }

                var incomeLines = await db.INCOME_PRODUCTS
                    .Include(ip => ip.INCOME)
                    .Where(ip => ip.INCOME.IsDeleted != true && ip.INCOME.ReceivingWHID != 0)
                    .Select(ip => new
                    {
                        ip.ProductID,
                        WarehouseId = ip.INCOME.ReceivingWHID,
                        ip.Amount,
                        Date = ip.INCOME.IssueDateTime
                    })
                    .Where(x => fifoWarehouseIds.Contains(x.WarehouseId))
                    .ToListAsync();

                var expenseLines = await db.EXPENSE_PRODUCTS
                    .Include(ep => ep.EXPENS)
                    .Where(ep => ep.EXPENS.IsDeleted != true)
                    .Select(ep => new
                    {
                        ep.ProductID,
                        WarehouseId = ep.EXPENS.SenderWHID,
                        ep.Amount,
                        Date = ep.EXPENS.IssueDateTime
                    })
                    .Where(x => fifoWarehouseIds.Contains(x.WarehouseId))
                    .ToListAsync();

                var fifoViolations = new List<InventoryFifoViolationItem>();
                var fifoGroups = incomeLines
                    .GroupBy(x => new { x.ProductID, x.WarehouseId })
                    .ToList();

                foreach (var group in fifoGroups)
                {
                    var incomes = group.OrderBy(x => x.Date).ToList();
                    if (incomes.Count < 2) continue;

                    var expensesForGroup = expenseLines
                        .Where(e => e.ProductID == group.Key.ProductID && e.WarehouseId == group.Key.WarehouseId)
                        .OrderBy(e => e.Date)
                        .ToList();

                    if (!expensesForGroup.Any()) continue;

                    var firstIncome = incomes.First();
                    var secondIncome = incomes.Skip(1).First();

                    var totalExpensesSinceFirst = expensesForGroup
                        .Where(e => e.Date >= firstIncome.Date)
                        .Sum(e => e.Amount);

                    var expensesAfterSecond = expensesForGroup.Any(e => e.Date >= secondIncome.Date);

                    if (totalExpensesSinceFirst < firstIncome.Amount && expensesAfterSecond)
                    {
                        var product = stocks.FirstOrDefault(s => s.ProductID == group.Key.ProductID && s.WHID == group.Key.WarehouseId)?.PRODUCT;
                        var warehouse = stocks.FirstOrDefault(s => s.ProductID == group.Key.ProductID && s.WHID == group.Key.WarehouseId)?.Warehouse;

                        fifoViolations.Add(new InventoryFifoViolationItem
                        {
                            ProductPNo = product?.PNo ?? "N/A",
                            ProductName = product?.Name ?? "N/A",
                            WarehouseName = warehouse?.WHName ?? "N/A",
                            OldestIncomeDate = firstIncome.Date,
                            LatestExpenseDate = expensesForGroup.Last().Date,
                            RemainingFromOldest = firstIncome.Amount - totalExpensesSinceFirst
                        });
                    }
                }

                model.FifoViolations = fifoViolations;

                // Calculate stock by category
                model.StockByCategory = stocks
                    .Where(s => s.PRODUCT != null && s.PRODUCT.PRODUCT_CATEGORIES != null)
                    .GroupBy(s => s.PRODUCT.PRODUCT_CATEGORIES.Name)
                    .Select(g => new CategoryStockSummary
                    {
                        CategoryName = g.Key,
                        TotalStock = g.Sum(s => s.Amount)
                    })
                    .ToList();

                // Calculate inbound/outbound trends
                var inboundData = await db.INCOMES
                    .Include(i => i.INCOME_PRODUCTS)
                    .Where(i => i.IssueDateTime >= fromDateValue && i.IssueDateTime <= toDateValue && i.IsDeleted != true)
                    .GroupBy(i => DbFunctions.TruncateTime(i.IssueDateTime))
                    .Select(g => new { Date = g.Key, Amount = g.SelectMany(i => i.INCOME_PRODUCTS).Sum(ip => ip.Amount) })
                    .ToListAsync();

                var outboundData = await db.EXPENSES
                    .Include(e => e.EXPENSE_PRODUCTS)
                    .Where(e => e.IssueDateTime >= fromDateValue && e.IssueDateTime <= toDateValue && e.IsDeleted != true)
                    .GroupBy(e => DbFunctions.TruncateTime(e.IssueDateTime))
                    .Select(g => new { Date = g.Key, Amount = g.SelectMany(e => e.EXPENSE_PRODUCTS).Sum(ep => ep.Amount) })
                    .ToListAsync();

                model.InboundTrends = inboundData.Select(d => new TrendData { Date = d.Date ?? fromDateValue, Amount = d.Amount }).ToList();
                model.OutboundTrends = outboundData.Select(d => new TrendData { Date = d.Date ?? fromDateValue, Amount = d.Amount }).ToList();

                // Tayyorlash ViewBag
                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != true).ToListAsync(),
                    "ID", "WHName", warehouseId);
                ViewBag.Categories = new SelectList(
                    await db.PRODUCT_CATEGORIES.ToListAsync(),
                    "ID", "Name", categoryId);
                ViewBag.RunnerDays = runnerDaysValue;
                ViewBag.RunnerTopN = runnerTopNValue;
                ViewBag.FifoScope = fifoScopeValue;

                return View(model);
            }
        }

        // Olish so'rovi: InventoryReports/ProductionWH
        public async Task<ActionResult> ProductionWH(int? warehouseId, DateTime? reportDate)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "InventoryReports", "ProductionWH"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var reportDateValue = reportDate ?? DateTime.Now;

                // Olish Production warehouse
                var productionWarehouse = warehouseId.HasValue
                    ? await db.WAREHOUSES.FindAsync(warehouseId.Value)
                    : db.WAREHOUSES.FirstOrDefault(w => w.WHName.Contains("Production") || w.WHName.Contains("Ishlab chiqarish"));

                if (productionWarehouse == null)
                {
                    productionWarehouse = db.WAREHOUSES.FirstOrDefault();
                }

                if (productionWarehouse == null)
                {
                    ViewBag.Error = "Production ombor topilmadi";
                    return View(new InventoryReportViewModel());
                }

                var report = await GenerateInventoryReportAsync(productionWarehouse.ID, reportDateValue, db);

                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != false).ToListAsync(),
                    "ID", "WHName", productionWarehouse.ID);
                ViewBag.ReportDate = reportDateValue;

                return View(report);
            }
        }

        /// <summary>
        /// Generates inventory report uchun Production warehouse
        /// </summary>
        private async Task<InventoryReportViewModel> GenerateInventoryReportAsync(
            int warehouseId, 
            DateTime reportDate, 
            DBTHSNEntities db)
        {
            var report = new InventoryReportViewModel
            {
                WarehouseID = warehouseId,
                ReportDate = reportDate,
                Items = new List<InventoryReportItem>()
                };

            // Olish barcha stocks in Production warehouse
            var stocks = await db.PRODUCT_STOCKS
                .Include(s => s.PRODUCT)
                .Include(s => s.PRODUCT.PRODUCT_CATEGORIES)
                .Include(s => s.UNIT)
                .Where(s => s.WHID == warehouseId && s.Amount > 0)
                    .ToListAsync();

            // Olish faol production plans
            var activePlans = await db.PRODUCTPLANS
                .Include(p => p.PRODUCT)
                .Where(p => p.IsDeleted == false &&
                           p.StartDate <= reportDate &&
                           p.DueDate >= reportDate)
                    .ToListAsync();

            var consumptionService = new ProductionConsumptionService(db);

            foreach (var stock in stocks)
            {
                // Calculate average daily consumption dan faol plans
                var materialPlans = activePlans
                    .Where(p => 
                    {
                        // Tekshirish agar this material is used in the plan's BOM
                        // This is a simplified tekshirish - in production, you'd query BOM/NORMLINES
                        return true; // Placeholder - should tekshirish BOM relationship
                    })
                    .ToList();

                double totalPlannedQty = 0;
                int totalDays = 0;

                foreach (var plan in materialPlans)
                {
                    var days = (plan.DueDate - plan.StartDate).Days + 1;
                    totalDays += days;
                    // Olish material requirement uchun this plan
                    var requirements = await consumptionService.CalculateRequiredRawMaterialsAsync(
                        plan.ProductID, plan.Amount);
                    var materialReq = requirements.FirstOrDefault(r => r.MaterialID == stock.ProductID);
                    if (materialReq != null)
                    {
                        totalPlannedQty += materialReq.TotalConsumption;
            }
        }

                double avgDailyConsumption = totalDays > 0 ? totalPlannedQty / totalDays : 0;
                double doh = avgDailyConsumption > 0 ? stock.Amount / avgDailyConsumption : 0;

                // Calculate coverage (how many days the stock will last)
                double coverage = doh;

                // Calculate plan fulfillment (percentage of planned quantity available)
                double planFulfillment = 0;
                if (totalPlannedQty > 0)
                {
                    planFulfillment = (stock.Amount / totalPlannedQty) * 100;
                    if (planFulfillment > 100) planFulfillment = 100;
                }

                report.Items.Add(new InventoryReportItem
                {
                    ProductID = stock.ProductID,
                    ProductPNo = stock.PRODUCT?.PNo ?? "N/A",
                    ProductName = stock.PRODUCT?.Name ?? "N/A",
                    CurrentStock = stock.Amount,
                    UnitID = stock.UnitID,
                    UnitName = stock.UNIT?.ShortName ?? "N/A",
                    AvgDailyConsumption = Math.Round(avgDailyConsumption, 2),
                    DaysOnHand = Math.Round(doh, 1),
                    Coverage = Math.Round(coverage, 1),
                    PlanFulfillment = Math.Round(planFulfillment, 1),
                    IsRawMaterial = stock.PRODUCT?.IsRawMaterial ?? false,
                    RequiresCutting = stock.PRODUCT?.PRODUCT_CATEGORIES?.RequiresCutting ?? false
                });
            }

            return report;
        }

        // Olish so'rovi: InventoryReports/Export
        public async Task<ActionResult> Export(int warehouseId, DateTime reportDate)
        {
            using (var db = new DBTHSNEntities())
            {
                var report = await GenerateInventoryReportAsync(warehouseId, reportDate, db);
                
                // TODO: Implement Excel export
                // uchun now, return JSON
                return Json(report, JsonRequestBehavior.AllowGet);
            }
        }
    }

    /// <summary>
    /// Inventory report view model
    /// </summary>
    public class InventoryReportViewModel
    {
        public int WarehouseID { get; set; }
        public DateTime ReportDate { get; set; }
        public List<InventoryReportItem> Items { get; set; }
    }

    public class InventoryReportItem
    {
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public double CurrentStock { get; set; }
        public int UnitID { get; set; }
        public string UnitName { get; set; }
        public double AvgDailyConsumption { get; set; }
        public double DaysOnHand { get; set; }
        public double Coverage { get; set; }
        public double PlanFulfillment { get; set; }
        public bool IsRawMaterial { get; set; }
        public bool RequiresCutting { get; set; }
    }

    /// <summary>
    /// Inventory Dashboard ViewModel
    /// </summary>
    public class InventoryDashboardViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<InventoryDashboardItem> HighDOHItems { get; set; }
        public List<InventoryDashboardItem> LowDOHItems { get; set; }
        public List<InventoryDashboardItem> StockItems { get; set; }
        public List<InventoryDashboardItem> HighRunnerItems { get; set; }
        public List<InventoryDashboardItem> LowRunnerItems { get; set; }
        public List<InventoryFifoViolationItem> FifoViolations { get; set; }
        public List<CategoryStockSummary> StockByCategory { get; set; }
        public List<TrendData> InboundTrends { get; set; }
        public List<TrendData> OutboundTrends { get; set; }
    }

    public class InventoryDashboardItem
    {
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public string CategoryName { get; set; }
        public string WarehouseName { get; set; }
        public double CurrentStock { get; set; }
        public string UnitName { get; set; }
        public double AverageDailyConsumption { get; set; }
        public double PlannedAverageDailyConsumption { get; set; }
        public double? PlannedDOH { get; set; }
        public double? ActualDOH { get; set; }
    }

    public class InventoryFifoViolationItem
    {
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public string WarehouseName { get; set; }
        public DateTime OldestIncomeDate { get; set; }
        public DateTime LatestExpenseDate { get; set; }
        public double RemainingFromOldest { get; set; }
    }

    public class CategoryStockSummary
    {
        public string CategoryName { get; set; }
        public double TotalStock { get; set; }
    }

    public class TrendData
    {
        public DateTime Date { get; set; }
        public double Amount { get; set; }
    }
}
