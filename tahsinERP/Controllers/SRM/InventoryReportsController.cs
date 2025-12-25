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
    /// Inventory Reports for Production Warehouse
    /// Reports: DOH (Days on Hand), Coverage, Plan fulfillment
    /// </summary>
    public class InventoryReportsController : Controller
    {
        // GET: InventoryReports/Dashboard
        public async Task<ActionResult> Dashboard(int? warehouseId, int? categoryId, DateTime? fromDate, DateTime? toDate)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "InventoryReports", "Dashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var fromDateValue = fromDate ?? DateTime.Now.AddDays(-30);
                var toDateValue = toDate ?? DateTime.Now;

                var model = new InventoryDashboardViewModel
                {
                    FromDate = fromDateValue,
                    ToDate = toDateValue,
                    HighDOHItems = new List<InventoryDashboardItem>(),
                    LowDOHItems = new List<InventoryDashboardItem>(),
                    StockItems = new List<InventoryDashboardItem>(),
                    StockByCategory = new List<CategoryStockSummary>(),
                    InboundTrends = new List<TrendData>(),
                    OutboundTrends = new List<TrendData>()
                };

                // Get warehouse IDs first (as simple list of integers)
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
                        StockByCategory = new List<CategoryStockSummary>(),
                        InboundTrends = new List<TrendData>(),
                        OutboundTrends = new List<TrendData>()
                    });
                }

                // Get all stocks
                var stocks = await db.PRODUCT_STOCKS
                    .Include(s => s.PRODUCT)
                    .Include(s => s.PRODUCT.PRODUCT_CATEGORIES)
                    .Include(s => s.UNIT)
                    .Include(s => s.Warehouse)
                    .Where(s => warehouseIds.Contains(s.WHID) && s.Amount > 0)
                    .ToListAsync();

                // Filter by category if specified
                if (categoryId.HasValue)
                {
                    stocks = stocks.Where(s => s.PRODUCT != null && s.PRODUCT.CategoryID == categoryId.Value).ToList();
                }

                // Calculate consumption from EXPENSES (outbound)
                var expenses = await db.EXPENSES
                    .Include(e => e.EXPENSE_PRODUCTS)
                    .Where(e => e.IssueDateTime >= fromDateValue && e.IssueDateTime <= toDateValue && e.IsDeleted != true)
                    .ToListAsync();

                var totalDays = (toDateValue - fromDateValue).TotalDays;
                if (totalDays <= 0) totalDays = 1;

                foreach (var stock in stocks)
                {
                    if (stock.PRODUCT == null) continue;

                    // Calculate average daily consumption from expenses
                    var productExpenses = expenses
                        .SelectMany(e => e.EXPENSE_PRODUCTS)
                        .Where(ep => ep.ProductID == stock.ProductID && ep.EXPENS.SenderWHID == stock.WHID)
                        .ToList();

                    var totalConsumption = productExpenses.Sum(ep => ep.Amount);
                    var avgDailyConsumption = totalConsumption / totalDays;
                    var doh = avgDailyConsumption > 0 ? stock.Amount / avgDailyConsumption : (double?)null;

                    var item = new InventoryDashboardItem
                    {
                        ProductID = stock.ProductID,
                        ProductPNo = stock.PRODUCT.PNo ?? "N/A",
                        ProductName = stock.PRODUCT.Name ?? "N/A",
                        CategoryName = stock.PRODUCT.PRODUCT_CATEGORIES?.Name ?? "N/A",
                        WarehouseName = stock.Warehouse?.WHName ?? "N/A",
                        CurrentStock = stock.Amount,
                        UnitName = stock.UNIT?.ShortName ?? "N/A",
                        AverageDailyConsumption = avgDailyConsumption,
                        DOH = doh
                    };

                    model.StockItems.Add(item);

                    // Categorize by DOH
                    if (doh.HasValue)
                    {
                        if (doh.Value > 60)
                        {
                            model.HighDOHItems.Add(item);
                        }
                        else if (doh.Value < 30)
                        {
                            model.LowDOHItems.Add(item);
                        }
                    }
                }

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

                // Prepare ViewBag
                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != true).ToListAsync(),
                    "ID", "WHName", warehouseId);
                ViewBag.Categories = new SelectList(
                    await db.PRODUCT_CATEGORIES.ToListAsync(),
                    "ID", "Name", categoryId);

                return View(model);
            }
        }

        // GET: InventoryReports/ProductionWH
        public async Task<ActionResult> ProductionWH(int? warehouseId, DateTime? reportDate)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "InventoryReports", "ProductionWH"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var reportDateValue = reportDate ?? DateTime.Now;

                // Get Production warehouse
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
        /// Generates inventory report for Production warehouse
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

            // Get all stocks in Production warehouse
            var stocks = await db.PRODUCT_STOCKS
                .Include(s => s.PRODUCT)
                .Include(s => s.PRODUCT.PRODUCT_CATEGORIES)
                .Include(s => s.UNIT)
                .Where(s => s.WHID == warehouseId && s.Amount > 0)
                    .ToListAsync();

            // Get active production plans
            var activePlans = await db.PRODUCTPLANS
                .Include(p => p.PRODUCT)
                .Where(p => p.IsDeleted == false &&
                           p.StartDate <= reportDate &&
                           p.DueDate >= reportDate)
                    .ToListAsync();

            var consumptionService = new ProductionConsumptionService(db);

            foreach (var stock in stocks)
            {
                // Calculate average daily consumption from active plans
                var materialPlans = activePlans
                    .Where(p => 
                    {
                        // Check if this material is used in the plan's BOM
                        // This is a simplified check - in production, you'd query BOM/NORMLINES
                        return true; // Placeholder - should check BOM relationship
                    })
                    .ToList();

                double totalPlannedQty = 0;
                int totalDays = 0;

                foreach (var plan in materialPlans)
                {
                    var days = (plan.DueDate - plan.StartDate).Days + 1;
                    totalDays += days;
                    // Get material requirement for this plan
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

        // GET: InventoryReports/Export
        public async Task<ActionResult> Export(int warehouseId, DateTime reportDate)
        {
            using (var db = new DBTHSNEntities())
            {
                var report = await GenerateInventoryReportAsync(warehouseId, reportDate, db);
                
                // TODO: Implement Excel export
                // For now, return JSON
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
        public double? DOH { get; set; }
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
