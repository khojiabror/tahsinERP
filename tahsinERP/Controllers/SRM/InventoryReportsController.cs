using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    public class InventoryReportsController : Controller
    {
        // GET: InventoryReports/Dashboard
        public async Task<ActionResult> Dashboard(int? warehouseId, int? categoryId, DateTime? fromDate, DateTime? toDate)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "InventoryReports", "Dashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Default date range: last 90 days
            fromDate = fromDate ?? DateTime.Now.AddDays(-90);
            toDate = toDate ?? DateTime.Now;

            using (var db = new DBTHSNEntities())
            {
                // Get current stock
                var currentStockQuery = db.PRODUCT_STOCKS
                    .Include(s => s.Warehouse)
                    .Include(s => s.PRODUCT)
                    .Include(s => s.PRODUCT.PRODUCT_CATEGORIES)
                    .Include(s => s.UNIT)
                    .Where(s => s.Amount > 0);

                if (warehouseId.HasValue)
                {
                    currentStockQuery = currentStockQuery.Where(s => s.WHID == warehouseId.Value);
                }

                if (categoryId.HasValue)
                {
                    currentStockQuery = currentStockQuery.Where(s => s.PRODUCT.CategoryID == categoryId.Value);
                }

                var currentStocks = await currentStockQuery.ToListAsync();

                // Get consumption history from EXPENSE_PRODUCTS
                var consumptionQuery = db.EXPENSE_PRODUCTS
                    .Include(ep => ep.EXPENS)
                    .Include(ep => ep.PRODUCT)
                    .Where(ep => ep.EXPENS.IssueDateTime >= fromDate.Value && 
                                ep.EXPENS.IssueDateTime <= toDate.Value &&
                                ep.EXPENS.IsDeleted != true);

                if (warehouseId.HasValue)
                {
                    consumptionQuery = consumptionQuery.Where(ep => ep.EXPENS.SenderWHID == warehouseId.Value);
                }

                var consumptions = await consumptionQuery.ToListAsync();

                // Calculate average daily consumption per product
                var daysDiff = (toDate.Value - fromDate.Value).TotalDays;
                if (daysDiff <= 0) daysDiff = 1;

                var consumptionByProduct = consumptions
                    .GroupBy(ep => ep.ProductID)
                    .Select(g => new
                    {
                        ProductID = g.Key,
                        TotalConsumption = g.Sum(ep => ep.Amount),
                        AverageDailyConsumption = g.Sum(ep => ep.Amount) / daysDiff
                    })
                    .ToDictionary(x => x.ProductID, x => x);

                // Calculate DOH for each product
                var stockWithDOH = currentStocks.Select(s => new
                {
                    Stock = s,
                    AverageDailyConsumption = consumptionByProduct.ContainsKey(s.ProductID) 
                        ? consumptionByProduct[s.ProductID].AverageDailyConsumption 
                        : 0,
                    DOH = consumptionByProduct.ContainsKey(s.ProductID) && consumptionByProduct[s.ProductID].AverageDailyConsumption > 0
                        ? s.Amount / consumptionByProduct[s.ProductID].AverageDailyConsumption
                        : (double?)null
                }).ToList();

                // Prepare dashboard data
                var dashboardData = new InventoryDashboardViewModel
                {
                    WarehouseId = warehouseId,
                    CategoryId = categoryId,
                    FromDate = fromDate.Value,
                    ToDate = toDate.Value,
                    StockItems = stockWithDOH.Select(s => new StockDOHItem
                    {
                        ProductID = s.Stock.ProductID,
                        ProductPNo = s.Stock.PRODUCT.PNo,
                        ProductName = s.Stock.PRODUCT.Name,
                        CategoryName = s.Stock.PRODUCT.PRODUCT_CATEGORIES?.Name ?? "-",
                        WarehouseName = s.Stock.Warehouse.WHName,
                        CurrentStock = s.Stock.Amount,
                        UnitName = s.Stock.UNIT.ShortName,
                        AverageDailyConsumption = s.AverageDailyConsumption,
                        DOH = s.DOH
                    }).ToList(),
                    HighDOHItems = stockWithDOH
                        .Where(s => s.DOH.HasValue && s.DOH.Value > 60)
                        .OrderByDescending(s => s.DOH)
                        .Take(10)
                        .Select(s => new StockDOHItem
                        {
                            ProductID = s.Stock.ProductID,
                            ProductPNo = s.Stock.PRODUCT.PNo,
                            ProductName = s.Stock.PRODUCT.Name,
                            CategoryName = s.Stock.PRODUCT.PRODUCT_CATEGORIES?.Name ?? "-",
                            WarehouseName = s.Stock.Warehouse.WHName,
                            CurrentStock = s.Stock.Amount,
                            UnitName = s.Stock.UNIT.ShortName,
                            AverageDailyConsumption = s.AverageDailyConsumption,
                            DOH = s.DOH
                        }).ToList(),
                    LowDOHItems = stockWithDOH
                        .Where(s => s.DOH.HasValue && s.DOH.Value < 30)
                        .OrderBy(s => s.DOH)
                        .Take(10)
                        .Select(s => new StockDOHItem
                        {
                            ProductID = s.Stock.ProductID,
                            ProductPNo = s.Stock.PRODUCT.PNo,
                            ProductName = s.Stock.PRODUCT.Name,
                            CategoryName = s.Stock.PRODUCT.PRODUCT_CATEGORIES?.Name ?? "-",
                            WarehouseName = s.Stock.Warehouse.WHName,
                            CurrentStock = s.Stock.Amount,
                            UnitName = s.Stock.UNIT.ShortName,
                            AverageDailyConsumption = s.AverageDailyConsumption,
                            DOH = s.DOH
                        }).ToList()
                };

                // Get inbound/outbound trends
                var inboundTrends = await db.INCOME_PRODUCTS
                    .Include(ip => ip.INCOME)
                    .Where(ip => ip.INCOME.IssueDateTime >= fromDate.Value && 
                                 ip.INCOME.IssueDateTime <= toDate.Value &&
                                 ip.INCOME.IsDeleted != true)
                    .GroupBy(ip => new { 
                        Date = DbFunctions.TruncateTime(ip.INCOME.IssueDateTime),
                        WarehouseId = ip.INCOME.ReceivingWHID
                    })
                    .Select(g => new
                    {
                        Date = g.Key.Date,
                        WarehouseId = g.Key.WarehouseId,
                        TotalAmount = g.Sum(ip => ip.Amount)
                    })
                    .ToListAsync();

                var outboundTrends = await db.EXPENSE_PRODUCTS
                    .Include(ep => ep.EXPENS)
                    .Where(ep => ep.EXPENS.IssueDateTime >= fromDate.Value && 
                                ep.EXPENS.IssueDateTime <= toDate.Value &&
                                ep.EXPENS.IsDeleted != true)
                    .GroupBy(ep => new { 
                        Date = DbFunctions.TruncateTime(ep.EXPENS.IssueDateTime),
                        WarehouseId = ep.EXPENS.SenderWHID
                    })
                    .Select(g => new
                    {
                        Date = g.Key.Date,
                        WarehouseId = g.Key.WarehouseId,
                        TotalAmount = g.Sum(ep => ep.Amount)
                    })
                    .ToListAsync();

                dashboardData.InboundTrends = inboundTrends.Select(t => new TrendData
                {
                    Date = t.Date.Value,
                    WarehouseId = t.WarehouseId,
                    Amount = t.TotalAmount
                }).ToList();

                dashboardData.OutboundTrends = outboundTrends.Select(t => new TrendData
                {
                    Date = t.Date.Value,
                    WarehouseId = t.WarehouseId,
                    Amount = t.TotalAmount
                }).ToList();

                // Stock distribution by category
                var stockByCategory = currentStocks
                    .GroupBy(s => s.PRODUCT.PRODUCT_CATEGORIES != null ? s.PRODUCT.PRODUCT_CATEGORIES.Name : "Kategoriyasi yo'q")
                    .Select(g => new
                    {
                        CategoryName = g.Key,
                        TotalStock = g.Sum(s => s.Amount),
                        ProductCount = g.Count()
                    })
                    .ToList();

                dashboardData.StockByCategory = stockByCategory.Select(c => new CategoryStockData
                {
                    CategoryName = c.CategoryName,
                    TotalStock = c.TotalStock,
                    ProductCount = c.ProductCount
                }).ToList();

                // ViewBag for filters
                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToListAsync(),
                    "ID", "WHName", warehouseId
                );

                ViewBag.Categories = new SelectList(
                    await db.PRODUCT_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(),
                    "ID", "Name", categoryId
                );

                return View(dashboardData);
            }
        }
    }

    // ViewModels
    public class InventoryDashboardViewModel
    {
        public int? WarehouseId { get; set; }
        public int? CategoryId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<StockDOHItem> StockItems { get; set; }
        public List<StockDOHItem> HighDOHItems { get; set; }
        public List<StockDOHItem> LowDOHItems { get; set; }
        public List<TrendData> InboundTrends { get; set; }
        public List<TrendData> OutboundTrends { get; set; }
        public List<CategoryStockData> StockByCategory { get; set; }

        public InventoryDashboardViewModel()
        {
            StockItems = new List<StockDOHItem>();
            HighDOHItems = new List<StockDOHItem>();
            LowDOHItems = new List<StockDOHItem>();
            InboundTrends = new List<TrendData>();
            OutboundTrends = new List<TrendData>();
            StockByCategory = new List<CategoryStockData>();
        }
    }

    public class StockDOHItem
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

    public class TrendData
    {
        public DateTime Date { get; set; }
        public int WarehouseId { get; set; }
        public double Amount { get; set; }
    }

    public class CategoryStockData
    {
        public string CategoryName { get; set; }
        public double TotalStock { get; set; }
        public int ProductCount { get; set; }
    }
}
