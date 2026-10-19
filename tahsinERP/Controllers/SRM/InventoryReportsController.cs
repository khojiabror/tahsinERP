using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.Services.DlocFifo;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Inventory Reports uchun Production Warehouse
    /// Reports: DOH (Days on Hand), Coverage, Plan fulfillment
    /// </summary>
    public class InventoryReportsController : Controller
    {
        private const int DohCardLimit = 50;

        // Olish so'rovi: InventoryReports/Dashboard
        // Barcha agregatlar SQL'da (SRM-P01/P08): avval butun tarixdagi kirim/chiqim qatorlari va to'liq
        // EXPENSES entity'lari xotiraga yuklanib, O(G×E) tsikl bilan hisoblanardi.
        public async Task<ActionResult> Dashboard(int? warehouseId, int? categoryId, DateTime? fromDate, DateTime? toDate, int? runnerDays, int? runnerTopN, string fifoScope, string productType)
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
                var productTypeValue = string.IsNullOrWhiteSpace(productType) ? null : productType.ToLowerInvariant();

                ViewBag.Warehouses = ReferenceDataCache.SelectList(ReferenceDataCache.Warehouses, warehouseId);
                ViewBag.Categories = ReferenceDataCache.SelectList(ReferenceDataCache.ProductCategories, categoryId);
                ViewBag.RunnerDays = runnerDaysValue;
                ViewBag.RunnerTopN = runnerTopNValue;
                ViewBag.FifoScope = fifoScopeValue;
                ViewBag.ProductType = productType;

                var model = new InventoryDashboardViewModel
                {
                    FromDate = fromDateValue,
                    ToDate = toDateValue,
                    HighDOHItems = new List<InventoryDashboardItem>(),
                    LowDOHItems = new List<InventoryDashboardItem>(),
                    StockItems = new List<InventoryDashboardItem>(),
                    HighRunnerItems = new List<InventoryDashboardItem>(),
                    LowRunnerItems = new List<InventoryDashboardItem>(),
                    FifoHeuristicAlerts = new List<InventoryFifoViolationItem>(),
                    DlocAlerts = new List<DlocCapacityAlertRow>(),
                    StockByCategory = new List<CategoryStockSummary>(),
                    InboundTrends = new List<TrendData>(),
                    OutboundTrends = new List<TrendData>()
                };

                var warehouseIds = warehouseId.HasValue
                    ? new List<int> { warehouseId.Value }
                    : ReferenceDataCache.Get(ReferenceDataCache.Warehouses).Select(w => w.ID).ToList();

                if (!warehouseIds.Any())
                {
                    ViewBag.Error = "Ombor topilmadi";
                    return View(model);
                }

                // Qoldiqlar: filtrlar SQL'da, faqat kerakli ustunlar (Include'li to'liq entity o'rniga)
                var stockQuery = db.PRODUCT_STOCKS.AsNoTracking()
                    .Where(s => warehouseIds.Contains(s.WHID) && s.Amount > 0);
                if (categoryId.HasValue)
                    stockQuery = stockQuery.Where(s => s.PRODUCT.CategoryID == categoryId.Value);
                if (productTypeValue == "finished")
                    stockQuery = stockQuery.Where(s => s.PRODUCT.PRODUCT_CATEGORIES.IsFinishedGood);
                else if (productTypeValue == "raw")
                    stockQuery = stockQuery.Where(s => s.PRODUCT.IsRawMaterial == true || s.PRODUCT.PRODUCT_CATEGORIES.IsRawMaterial);

                // Tartib aniq: avval SQL'ning tasodifiy tartibiga (UX_PRODUCT_STOCKS_WH_Product skani) tayanardi
                var stocks = await stockQuery
                    .OrderBy(s => s.WHID).ThenBy(s => s.ProductID)
                    .Select(s => new
                    {
                        s.WHID,
                        s.ProductID,
                        s.Amount,
                        PNo = s.PRODUCT.PNo,
                        Name = s.PRODUCT.Name,
                        CategoryName = s.PRODUCT.PRODUCT_CATEGORIES.Name,
                        UnitName = s.UNIT.ShortName,
                        WarehouseName = s.Warehouse.WHName
                    })
                    .ToListAsync();

                var totalDays = (toDateValue - fromDateValue).TotalDays;
                if (totalDays <= 0) totalDays = 1;

                // Rejadagi sarf (PRODUCTPLANS_DAILY) — mahsulot bo'yicha SQL'da yig'iladi
                var plannedByProduct = (await db.PRODUCTPLANS_DAILY
                    .Where(d => d.Day >= fromDateValue && d.Day <= toDateValue && d.PRODUCTPLAN.IsDeleted == false)
                    .GroupBy(d => d.PRODUCTPLAN.ProductID)
                    .Select(g => new { ProductID = g.Key, Total = g.Sum(x => x.Total) })
                    .ToListAsync())
                    .ToDictionary(x => x.ProductID, x => (double)x.Total);

                // Haqiqiy sarf (chiqimlar) — (mahsulot, ombor) bo'yicha SQL'da yig'iladi
                var actualByProductWarehouse = await SumExpensesAsync(db, warehouseIds, fromDateValue, toDateValue);

                foreach (var stock in stocks)
                {
                    double plannedTotal;
                    plannedByProduct.TryGetValue(stock.ProductID, out plannedTotal);
                    var avgDailyPlanned = plannedTotal / totalDays;
                    var plannedDoh = avgDailyPlanned > 0 ? (double)stock.Amount / avgDailyPlanned : (double?)null;

                    double actualTotal;
                    actualByProductWarehouse.TryGetValue(Tuple.Create(stock.ProductID, stock.WHID), out actualTotal);
                    var avgDailyActual = actualTotal / totalDays;
                    var actualDoh = avgDailyActual > 0 ? (double)stock.Amount / avgDailyActual : (double?)null;

                    var item = new InventoryDashboardItem
                    {
                        ProductID = stock.ProductID,
                        ProductPNo = stock.PNo ?? "N/A",
                        ProductName = stock.Name ?? "N/A",
                        CategoryName = stock.CategoryName ?? "N/A",
                        WarehouseName = stock.WarehouseName ?? "N/A",
                        CurrentStock = (double)stock.Amount,
                        UnitName = stock.UnitName ?? "N/A",
                        AverageDailyConsumption = avgDailyActual,
                        PlannedAverageDailyConsumption = avgDailyPlanned,
                        PlannedDOH = plannedDoh,
                        ActualDOH = actualDoh
                    };

                    model.StockItems.Add(item);

                    if (plannedDoh.HasValue)
                    {
                        if (plannedDoh.Value > 60)
                            model.HighDOHItems.Add(item);
                        else if (plannedDoh.Value < 30)
                            model.LowDOHItems.Add(item);
                    }
                }

                // Low/High runner: tanlangan oyna bo'yicha o'rtacha sarf (lug'at bilan, avval O(N×M) FirstOrDefault)
                var now = DateTime.Now;
                var runnerTotals = await SumExpensesAsync(db, warehouseIds, now.AddDays(-runnerDaysValue), now);
                var runnerDaysDouble = (double)runnerDaysValue;
                var runnerItems = stocks
                    .Select(s =>
                    {
                        double total;
                        runnerTotals.TryGetValue(Tuple.Create(s.ProductID, s.WHID), out total);
                        return new InventoryDashboardItem
                        {
                            ProductID = s.ProductID,
                            ProductPNo = s.PNo ?? "N/A",
                            ProductName = s.Name ?? "N/A",
                            CategoryName = s.CategoryName ?? "N/A",
                            WarehouseName = s.WarehouseName ?? "N/A",
                            CurrentStock = (double)s.Amount,
                            UnitName = s.UnitName ?? "N/A",
                            AverageDailyConsumption = total / runnerDaysDouble
                        };
                    })
                    .Where(i => i.AverageDailyConsumption > 0)
                    .OrderByDescending(i => i.AverageDailyConsumption)
                    .ToList();

                model.HighRunnerItems = runnerItems.Take(runnerTopNValue).ToList();
                model.LowRunnerItems = runnerItems.OrderBy(i => i.AverageDailyConsumption).Take(runnerTopNValue).ToList();

                // FIFO: DLOC jurnali + soddalashtirilgan (heuristik) ogohlantirishlar
                var fifoWarehouseIds = fifoScopeValue == "selected"
                    ? (warehouseId.HasValue ? new List<int> { warehouseId.Value } : new List<int>())
                    : warehouseIds;

                if (fifoScopeValue == "selected" && !warehouseId.HasValue)
                {
                    ViewBag.FifoScopeWarning = "FIFO uchun ombor tanlanmagan. 'Tanlangan ombor' rejimida natija chiqmaydi.";
                }

                if (DlocFifoSchema.IsEnabled(db))
                {
                    var dlocAlerts = DlocFifoReporting.GetDlocCapacityAlerts(db, fifoWarehouseIds);
                    model.DlocAlerts = dlocAlerts;
                    model.DlocBelowMinCount = dlocAlerts.Count(a => a.AlertType == "BELOW_MIN");
                    model.DlocNearMaxCount = dlocAlerts.Count(a => a.AlertType == "NEAR_MAX");
                }

                if (fifoWarehouseIds.Any())
                {
                    var fifoSet = new HashSet<int>(fifoWarehouseIds);
                    model.FifoHeuristicAlerts = (await db.Database.SqlQuery<FifoHeuristicRow>(FifoHeuristicSql).ToListAsync())
                        .Where(r => fifoSet.Contains(r.WHID))
                        .Select(r => new InventoryFifoViolationItem
                        {
                            ProductPNo = r.ProductPNo ?? "N/A",
                            ProductName = r.ProductName ?? "N/A",
                            WarehouseName = r.WarehouseName ?? "N/A",
                            OldestIncomeDate = r.FirstDate,
                            LatestExpenseDate = r.LastExpense,
                            RemainingFromOldest = r.FirstAmount - r.SinceFirst,
                            ReasonCode = "HEURISTIC"
                        })
                        .ToList();
                }

                // Yuqori/past DOH kartalari sahifalashsiz jadval — eng muhim 50 tasi tartiblab ko'rsatiladi
                // (to'liq ro'yxat pastdagi DataTable'da). Avval barcha mos qatorlar tartibsiz chiqarilardi.
                ViewBag.HighDohTotal = model.HighDOHItems.Count;
                ViewBag.LowDohTotal = model.LowDOHItems.Count;
                model.HighDOHItems = model.HighDOHItems.OrderByDescending(i => i.PlannedDOH).Take(DohCardLimit).ToList();
                model.LowDOHItems = model.LowDOHItems.OrderBy(i => i.PlannedDOH).Take(DohCardLimit).ToList();

                model.StockByCategory = stocks
                    .Where(s => s.CategoryName != null)
                    .GroupBy(s => s.CategoryName)
                    .Select(g => new CategoryStockSummary
                    {
                        CategoryName = g.Key,
                        TotalStock = (double)(g.Sum(s => s.Amount))
                    })
                    .ToList();

                // Kirim/chiqim trendlari — ombor, kategoriya va tur filtrlari bilan (SRM-B20: avval barcha omborlar)
                var inboundQuery = db.INCOME_PRODUCTS.AsNoTracking()
                    .Where(ip => ip.INCOME.IssueDateTime >= fromDateValue && ip.INCOME.IssueDateTime <= toDateValue
                        && ip.INCOME.IsDeleted != true && warehouseIds.Contains(ip.INCOME.ReceivingWHID));
                var outboundQuery = db.EXPENSE_PRODUCTS.AsNoTracking()
                    .Where(ep => ep.EXPENS.IssueDateTime >= fromDateValue && ep.EXPENS.IssueDateTime <= toDateValue
                        && ep.EXPENS.IsDeleted != true && warehouseIds.Contains(ep.EXPENS.SenderWHID));
                if (categoryId.HasValue)
                {
                    inboundQuery = inboundQuery.Where(ip => ip.PRODUCT.CategoryID == categoryId.Value);
                    outboundQuery = outboundQuery.Where(ep => ep.PRODUCT.CategoryID == categoryId.Value);
                }
                if (productTypeValue == "finished")
                {
                    inboundQuery = inboundQuery.Where(ip => ip.PRODUCT.PRODUCT_CATEGORIES.IsFinishedGood);
                    outboundQuery = outboundQuery.Where(ep => ep.PRODUCT.PRODUCT_CATEGORIES.IsFinishedGood);
                }
                else if (productTypeValue == "raw")
                {
                    inboundQuery = inboundQuery.Where(ip => ip.PRODUCT.IsRawMaterial == true || ip.PRODUCT.PRODUCT_CATEGORIES.IsRawMaterial);
                    outboundQuery = outboundQuery.Where(ep => ep.PRODUCT.IsRawMaterial == true || ep.PRODUCT.PRODUCT_CATEGORIES.IsRawMaterial);
                }

                var inboundData = await inboundQuery
                    .GroupBy(ip => DbFunctions.TruncateTime(ip.INCOME.IssueDateTime))
                    .Select(g => new { Date = g.Key, Amount = g.Sum(ip => ip.Amount) })
                    .ToListAsync();
                var outboundData = await outboundQuery
                    .GroupBy(ep => DbFunctions.TruncateTime(ep.EXPENS.IssueDateTime))
                    .Select(g => new { Date = g.Key, Amount = g.Sum(ep => ep.Amount) })
                    .ToListAsync();

                model.InboundTrends = inboundData.OrderBy(d => d.Date).Select(d => new TrendData { Date = d.Date ?? fromDateValue, Amount = (double)d.Amount }).ToList();
                model.OutboundTrends = outboundData.OrderBy(d => d.Date).Select(d => new TrendData { Date = d.Date ?? fromDateValue, Amount = (double)d.Amount }).ToList();

                return View(model);
            }
        }

        /// <summary>Chiqimlar (mahsulot, yuboruvchi ombor) bo'yicha jami — SQL'da GROUP BY.</summary>
        private static async Task<Dictionary<Tuple<int, int>, double>> SumExpensesAsync(
            DBTHSNEntities db, List<int> warehouseIds, DateTime from, DateTime to)
        {
            var rows = await db.EXPENSE_PRODUCTS.AsNoTracking()
                .Where(ep => ep.EXPENS.IssueDateTime >= from && ep.EXPENS.IssueDateTime <= to
                    && ep.EXPENS.IsDeleted != true && warehouseIds.Contains(ep.EXPENS.SenderWHID))
                .GroupBy(ep => new { ep.ProductID, ep.EXPENS.SenderWHID })
                .Select(g => new { g.Key.ProductID, g.Key.SenderWHID, Total = g.Sum(x => x.Amount) })
                .ToListAsync();
            return rows.ToDictionary(r => Tuple.Create(r.ProductID, r.SenderWHID), r => (double)r.Total);
        }

        private class FifoHeuristicRow
        {
            public int ProductID { get; set; }
            public int WHID { get; set; }
            public string ProductPNo { get; set; }
            public string ProductName { get; set; }
            public string WarehouseName { get; set; }
            public DateTime FirstDate { get; set; }
            public double FirstAmount { get; set; }
            public double SinceFirst { get; set; }
            public DateTime LastExpense { get; set; }
        }

        /// <summary>
        /// Soddalashtirilgan FIFO ogohlantirishi (avvalgi xotiradagi algoritm bilan bir xil mantiq, endi SQL'da):
        /// (mahsulot, ombor) uchun kamida 2 ta kirim qatori bor, eng eski kirimdan beri chiqimlar uning
        /// miqdoridan kam va ikkinchi kirimdan keyin chiqim bo'lgan — ya'ni yangi partiya eskisidan oldin ishlatilmoqda.
        /// </summary>
        private const string FifoHeuristicSql = @"
;WITH Inc AS (
    SELECT ip.ProductID, i.ReceivingWHID AS WHID, i.IssueDateTime AS D, ip.Amount,
           ROW_NUMBER() OVER (PARTITION BY ip.ProductID, i.ReceivingWHID ORDER BY i.IssueDateTime, ip.ID) AS rn
    FROM dbo.INCOME_PRODUCTS ip
    JOIN dbo.INCOMES i ON i.ID = ip.IncomeID
    WHERE ISNULL(i.IsDeleted, 0) = 0 AND i.ReceivingWHID <> 0
), G AS (
    SELECT ProductID, WHID,
           MAX(CASE WHEN rn = 1 THEN D END)      AS FirstDate,
           MAX(CASE WHEN rn = 1 THEN Amount END) AS FirstAmount,
           MAX(CASE WHEN rn = 2 THEN D END)      AS SecondDate
    FROM Inc
    WHERE rn <= 2
    GROUP BY ProductID, WHID
    HAVING COUNT(*) = 2
), X AS (
    SELECT g.ProductID, g.WHID,
           SUM(CASE WHEN e.IssueDateTime >= g.FirstDate THEN ep.Amount ELSE 0 END) AS SinceFirst,
           MAX(e.IssueDateTime) AS LastExpense,
           MAX(CASE WHEN e.IssueDateTime >= g.SecondDate THEN 1 ELSE 0 END) AS AfterSecond
    FROM G g
    JOIN dbo.EXPENSES e ON e.SenderWHID = g.WHID AND e.IsDeleted = 0
    JOIN dbo.EXPENSE_PRODUCTS ep ON ep.ExpenseID = e.ID AND ep.ProductID = g.ProductID
    GROUP BY g.ProductID, g.WHID
)
SELECT g.ProductID, g.WHID, p.PNo AS ProductPNo, p.Name AS ProductName, w.WHName AS WarehouseName,
       g.FirstDate, CAST(g.FirstAmount AS FLOAT) AS FirstAmount,
       CAST(ISNULL(x.SinceFirst, 0) AS FLOAT) AS SinceFirst, x.LastExpense
FROM G g
JOIN X x ON x.ProductID = g.ProductID AND x.WHID = g.WHID
LEFT JOIN dbo.PRODUCTS p ON p.ID = g.ProductID
LEFT JOIN dbo.WAREHOUSES w ON w.ID = g.WHID
WHERE x.LastExpense IS NOT NULL
  AND ISNULL(x.SinceFirst, 0) < g.FirstAmount
  AND x.AfterSecond = 1";

        /// <summary>
        /// Ombor oylik hisoboti (MRP topshirig'i) — kirim/chiqim va qoldiqlar. Barcha omborlar uchun.
        /// Ma'lumotlar INCOME/EXPENSE dan dynamic hisoblanadi. Chop etish uchun mo'ljallangan.
        /// </summary>
        public async Task<ActionResult> MonthlyReport(int? warehouseId, int? year, int? month, string viewMode)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "InventoryReports", "Dashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var mode = string.IsNullOrWhiteSpace(viewMode) || viewMode != "amount" ? "quantity" : "amount";

            using (var db = new DBTHSNEntities())
            {
                var warehouses = await db.WAREHOUSES.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToListAsync();
                if (!warehouses.Any())
                {
                    var emptyModel = new WarehouseMonthlyReportViewModel
                    {
                        Year = year ?? DateTime.Today.Year,
                        Month = month ?? DateTime.Today.Month,
                        ViewMode = mode,
                        ReportDate = DateTime.Now.ToString("dd.MM.yyyy"),
                        CompanyName = db.COMPANIES.Any() ? db.COMPANIES.FirstOrDefault()?.Name : "",
                        Rows = new List<WarehouseMonthlyReportRowViewModel>(),
                        Totals = new WarehouseMonthlyReportTotalsViewModel(),
                        Warehouses = new List<SelectListItem>()
                    };
                    return View(emptyModel);
                }

                var whId = warehouseId ?? warehouses.First().ID;
                var y = year ?? DateTime.Today.Year;
                var m = month ?? DateTime.Today.Month;
                if (m < 1) m = 1;
                if (m > 12) m = 12;

                var companyName = db.COMPANIES.Any() ? db.COMPANIES.FirstOrDefault()?.Name : null;
                var model = await WarehouseMonthlyReportService.BuildReportAsync(db, whId, y, m, companyName, mode);
                if (model == null)
                {
                    var wh = warehouses.FirstOrDefault(w => w.ID == whId);
                    model = new WarehouseMonthlyReportViewModel
                    {
                        SelectedWarehouseId = whId,
                        SelectedWarehouseName = wh?.WHName,
                        Year = y,
                        Month = m,
                        ViewMode = mode,
                        ReportDate = DateTime.Now.ToString("dd.MM.yyyy"),
                        CompanyName = companyName ?? "",
                        Rows = new List<WarehouseMonthlyReportRowViewModel>(),
                        Totals = new WarehouseMonthlyReportTotalsViewModel(),
                        Warehouses = warehouses.Select(w => new SelectListItem { Value = w.ID.ToString(), Text = w.WHName, Selected = w.ID == whId }).ToList()
                    };
                }

                return View(model);
            }
        }
    }
}
