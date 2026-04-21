using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using tahsinERP.Models;
using tahsinERP.ViewModels.Tannarx;

namespace tahsinERP.Services
{
    public class TannarxAnalyticsService
    {
        private readonly DBTHSNEntities _db;
        private static readonly Regex ProductionIdRegex = new Regex(@"ProductionID:(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex BuyTokenRegex = new Regex(@"(?:Buy#|B:)\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public TannarxAnalyticsService(DBTHSNEntities db)
        {
            _db = db;
        }

        /// <summary>
        /// Update actual costs for all tannarx records or specific year/month
        /// </summary>
        public async Task<int> UpdateActualCostsAsync(int? year = null, int? month = null)
        {
            try
            {
                _db.Database.CommandTimeout = 180;
                // SP oxirida SELECT @@ROWCOUNT qaytaradi; ExecuteSqlCommand ko'pincha -1 beradi
                var sql = "EXEC SP_TANNARX_UPDATE_ACTUAL_COSTS @Year, @Month";
                var yearParam = new SqlParameter("@Year", (object)year ?? DBNull.Value);
                var monthParam = new SqlParameter("@Month", (object)month ?? DBNull.Value);

                var result = await _db.Database.SqlQuery<int>(sql, yearParam, monthParam)
                    .FirstOrDefaultAsync();

                // Material fakti: normadan emas, real ishlab chiqarish sarfi (EXPENSE_PRODUCTS.TotalPrice)
                // orqali qayta hisoblanadi. Bu sarf FIFO kirim narxlaridan (purchase/invoice/income) keladi.
                await UpdateActualMaterialPriceFromProductionFlowAsync(year, month);
                await UpdateActualCostBreakdownByUserRulesAsync(year, month);

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"Actual costs yangilashda xatolik: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get dashboard data for specific year and month
        /// </summary>
        public async Task<TannarxAnalyticsDashboardViewModel> GetDashboardDataAsync(int year, int month, int? productID = null)
        {
            var dashboard = new TannarxAnalyticsDashboardViewModel
            {
                Year = year,
                Month = month,
                MonthString = GetMonthName(month)
            };

            // Get all tannarx records for the month
            var query = _db.TANNARXes
                .Include(t => t.PRODUCT)
                .Where(t => t.Year == year 
                    && t.Month == month 
                    && !t.IsDeleted);

            if (productID.HasValue)
            {
                query = query.Where(t => t.ProductID == productID.Value);
            }

            var tannarxList = await query.ToListAsync();

            dashboard.TotalProducts = tannarxList.Count;
            dashboard.ProductsWithActualData = tannarxList.Count(t => 
                t.ActualMaterialPrice.HasValue || t.ActualLaborCost.HasValue);

            // Calculate totals
            dashboard.TotalPlannedMaterialPrice = tannarxList.Sum(t => t.MaterialPrice);
            dashboard.TotalActualMaterialPrice = tannarxList.Sum(t => t.ActualMaterialPrice ?? 0);
            dashboard.MaterialPriceVariance = dashboard.TotalActualMaterialPrice - dashboard.TotalPlannedMaterialPrice;
            dashboard.MaterialPriceVariancePercent = dashboard.TotalPlannedMaterialPrice != 0
                ? (dashboard.MaterialPriceVariance / dashboard.TotalPlannedMaterialPrice) * 100
                : 0;

            dashboard.TotalPlannedLaborCost = tannarxList.Sum(t => t.LaborCost);
            dashboard.TotalActualLaborCost = tannarxList.Sum(t => t.ActualLaborCost ?? 0);
            dashboard.LaborCostVariance = dashboard.TotalActualLaborCost - dashboard.TotalPlannedLaborCost;
            dashboard.LaborCostVariancePercent = dashboard.TotalPlannedLaborCost != 0
                ? (dashboard.LaborCostVariance / dashboard.TotalPlannedLaborCost) * 100
                : 0;

            dashboard.TotalPlannedEXWWithVAT = tannarxList.Sum(t => t.SetPriceEXWWithVAT ?? t.SalesPriceEXWWithVAT);
            dashboard.TotalActualEXWWithVAT = tannarxList.Sum(t => t.ActualSalesPriceEXWWithVAT ?? 0);
            dashboard.EXWVariance = dashboard.TotalActualEXWWithVAT - dashboard.TotalPlannedEXWWithVAT;
            dashboard.EXWVariancePercent = dashboard.TotalPlannedEXWWithVAT != 0
                ? (dashboard.EXWVariance / dashboard.TotalPlannedEXWWithVAT) * 100
                : 0;

            dashboard.TotalPlannedCIPWithVAT = tannarxList.Sum(t => t.SetPriceCIPWithVAT ?? t.SalesPriceCIPWithVAT);
            dashboard.TotalActualCIPWithVAT = tannarxList.Sum(t => t.ActualSalesPriceCIPWithVAT ?? 0);
            dashboard.CIPVariance = dashboard.TotalActualCIPWithVAT - dashboard.TotalPlannedCIPWithVAT;
            dashboard.CIPVariancePercent = dashboard.TotalPlannedCIPWithVAT != 0
                ? (dashboard.CIPVariance / dashboard.TotalPlannedCIPWithVAT) * 100
                : 0;

            // Top variance products
            dashboard.TopVarianceProducts = tannarxList
                .Where(t => t.ActualSalesPriceEXWWithVAT.HasValue)
                .Select(t => new TannarxProductVarianceViewModel
                {
                    ProductID = t.ProductID,
                    ProductPNo = t.PRODUCT?.PNo ?? "",
                    ProductName = t.PRODUCT?.Name ?? "",
                    PlannedPrice = t.SetPriceEXWWithVAT ?? t.SalesPriceEXWWithVAT,
                    ActualPrice = t.ActualSalesPriceEXWWithVAT ?? 0,
                    Variance = (t.ActualSalesPriceEXWWithVAT ?? 0) - (t.SetPriceEXWWithVAT ?? t.SalesPriceEXWWithVAT),
                    VariancePercent = (t.SetPriceEXWWithVAT ?? t.SalesPriceEXWWithVAT) != 0
                        ? (((t.ActualSalesPriceEXWWithVAT ?? 0) - (t.SetPriceEXWWithVAT ?? t.SalesPriceEXWWithVAT)) 
                            / (t.SetPriceEXWWithVAT ?? t.SalesPriceEXWWithVAT)) * 100
                        : 0
                })
                .OrderByDescending(p => Math.Abs(p.Variance))
                .Take(10)
                .ToList();

            // Monthly trends (last 12 months)
            dashboard.MonthlyTrends = await GetMonthlyTrendsAsync(year, month, productID);

            return dashboard;
        }

        /// <summary>
        /// Get monthly trends for the last 12 months for a specific product (or all products if productID is null)
        /// </summary>
        private async Task<List<TannarxMonthlyTrendViewModel>> GetMonthlyTrendsAsync(int currentYear, int currentMonth, int? productID = null)
        {
            var trends = new List<TannarxMonthlyTrendViewModel>();
            var date = new DateTime(currentYear, currentMonth, 1);

            for (int i = 11; i >= 0; i--)
            {
                var trendDate = date.AddMonths(-i);
                var year = trendDate.Year;
                var month = trendDate.Month;

                var query = _db.TANNARXes
                    .Where(t => t.Year == year 
                        && t.Month == month 
                        && !t.IsDeleted);

                // Filter by productID if specified
                if (productID.HasValue)
                {
                    query = query.Where(t => t.ProductID == productID.Value);
                }

                var tannarxList = await query.ToListAsync();

                // If productID is specified, get only the first record (single product)
                // Otherwise, sum all products
                if (productID.HasValue && tannarxList.Any())
                {
                    var tannarx = tannarxList.First();
                    trends.Add(new TannarxMonthlyTrendViewModel
                    {
                        Year = year,
                        Month = month,
                        MonthString = GetMonthName(month),
                        PlannedTotal = tannarx.SetPriceEXWWithVAT ?? tannarx.SalesPriceEXWWithVAT,
                        ActualTotal = tannarx.ActualSalesPriceEXWWithVAT ?? 0,
                        Variance = (tannarx.ActualSalesPriceEXWWithVAT ?? 0) - (tannarx.SetPriceEXWWithVAT ?? tannarx.SalesPriceEXWWithVAT)
                    });
                }
                else if (!productID.HasValue)
                {
                    // Sum all products only if productID is not specified
                    trends.Add(new TannarxMonthlyTrendViewModel
                    {
                        Year = year,
                        Month = month,
                        MonthString = GetMonthName(month),
                        PlannedTotal = tannarxList.Sum(t => t.SetPriceEXWWithVAT ?? t.SalesPriceEXWWithVAT),
                        ActualTotal = tannarxList.Sum(t => t.ActualSalesPriceEXWWithVAT ?? 0),
                        Variance = tannarxList.Sum(t => (t.ActualSalesPriceEXWWithVAT ?? 0) - (t.SetPriceEXWWithVAT ?? t.SalesPriceEXWWithVAT))
                    });
                }
                else
                {
                    // No data for this month for the selected product
                    trends.Add(new TannarxMonthlyTrendViewModel
                    {
                        Year = year,
                        Month = month,
                        MonthString = GetMonthName(month),
                        PlannedTotal = 0,
                        ActualTotal = 0,
                        Variance = 0
                    });
                }
            }

            return trends;
        }

        /// <summary>
        /// Get detailed analytics report for products
        /// </summary>
        public async Task<TannarxAnalyticsReportListViewModel> GetAnalyticsReportAsync(
            int? year = null, 
            int? month = null, 
            int? productID = null)
        {
            var reportList = new TannarxAnalyticsReportListViewModel
            {
                Year = year,
                Month = month,
                Reports = new List<TannarxAnalyticsReportViewModel>()
            };

            var query = _db.TANNARXes
                .Include(t => t.PRODUCT)
                .Where(t => !t.IsDeleted);

            if (year.HasValue)
                query = query.Where(t => t.Year == year.Value);
            if (month.HasValue)
                query = query.Where(t => t.Month == month.Value);
            if (productID.HasValue)
                query = query.Where(t => t.ProductID == productID.Value);

            var tannarxList = await query
                .OrderBy(t => t.PRODUCT.PNo)
                .ThenBy(t => t.Year)
                .ThenBy(t => t.Month)
                .ToListAsync();
            var includeMaterialNote = productID.HasValue;

            foreach (var tannarx in tannarxList)
            {
                var report = new TannarxAnalyticsReportViewModel
                {
                    Year = tannarx.Year,
                    Month = tannarx.Month,
                    ProductID = tannarx.ProductID,
                    ProductPNo = tannarx.PRODUCT?.PNo ?? "",
                    ProductName = tannarx.PRODUCT?.Name ?? "",
                    
                    // Planned values
                    PlannedMaterialPrice = tannarx.MaterialPrice,
                    PlannedMaterialTransportCost = tannarx.MaterialTransportCost,
                    PlannedLaborCost = tannarx.LaborCost,
                    PlannedTaxAndBank = tannarx.TaxAndBank,
                    PlannedAdministrativeExpenses = tannarx.AdministrativeExpenses,
                    PlannedOtherExpenses = tannarx.OtherExpenses,
                    PlannedProfit = tannarx.Profit,
                    PlannedEXWWithoutVAT = tannarx.SalesPriceEXWWithoutVAT,
                    PlannedEXWWithVAT = tannarx.SalesPriceEXWWithVAT,
                    PlannedSetPriceEXWWithVAT = tannarx.SetPriceEXWWithVAT ?? tannarx.SalesPriceEXWWithVAT,
                    PlannedFinishedProductTransportCost = tannarx.FinishedProductTransportCost ?? 0,
                    PlannedCIPWithoutVAT = tannarx.SalesPriceCIPWithoutVAT,
                    PlannedCIPWithVAT = tannarx.SalesPriceCIPWithVAT,
                    PlannedSetPriceCIPWithVAT = tannarx.SetPriceCIPWithVAT ?? tannarx.SalesPriceCIPWithVAT,
                    
                    // Actual values
                    ActualMaterialPrice = tannarx.ActualMaterialPrice ?? 0,
                    ActualMaterialTransportCost = tannarx.ActualMaterialTransportCost ?? 0,
                    ActualLaborCost = tannarx.ActualLaborCost ?? 0,
                    ActualTaxAndBank = tannarx.ActualTaxAndBank ?? 0,
                    ActualAdministrativeExpenses = tannarx.ActualAdministrativeExpenses ?? 0,
                    ActualOtherExpenses = tannarx.ActualOtherExpenses ?? 0,
                    ActualProfit = tannarx.ActualProfit ?? 0,
                    ActualEXWWithoutVAT = tannarx.ActualSalesPriceEXWWithoutVAT ?? 0,
                    ActualEXWWithVAT = tannarx.ActualSalesPriceEXWWithVAT ?? 0,
                    ActualCIPWithoutVAT = tannarx.ActualSalesPriceCIPWithoutVAT ?? 0,
                    ActualCIPWithVAT = tannarx.ActualSalesPriceCIPWithVAT ?? 0,
                    ActualFinishedProductTransportCost = tannarx.ActualFinishedProductTransportCost ?? 0
                };

                if (includeMaterialNote)
                {
                    report.ActualMaterialCalculationNote = await BuildActualMaterialCalculationNoteAsync(
                        tannarx.ProductID,
                        tannarx.Year,
                        tannarx.Month);
                }

                // Calculate variances
                report.MaterialPriceVariance = report.ActualMaterialPrice - report.PlannedMaterialPrice;
                report.MaterialPriceVariancePercent = report.PlannedMaterialPrice != 0
                    ? (report.MaterialPriceVariance / report.PlannedMaterialPrice) * 100
                    : 0;

                report.MaterialTransportVariance = report.ActualMaterialTransportCost - report.PlannedMaterialTransportCost;
                report.MaterialTransportVariancePercent = report.PlannedMaterialTransportCost != 0
                    ? (report.MaterialTransportVariance / report.PlannedMaterialTransportCost) * 100
                    : 0;

                report.LaborCostVariance = report.ActualLaborCost - report.PlannedLaborCost;
                report.LaborCostVariancePercent = report.PlannedLaborCost != 0
                    ? (report.LaborCostVariance / report.PlannedLaborCost) * 100
                    : 0;

                report.AdminExpensesVariance = report.ActualAdministrativeExpenses - report.PlannedAdministrativeExpenses;
                report.AdminExpensesVariancePercent = report.PlannedAdministrativeExpenses != 0
                    ? (report.AdminExpensesVariance / report.PlannedAdministrativeExpenses) * 100
                    : 0;

                report.EXWVariance = report.ActualEXWWithVAT - report.PlannedSetPriceEXWWithVAT;
                report.EXWVariancePercent = report.PlannedSetPriceEXWWithVAT != 0
                    ? (report.EXWVariance / report.PlannedSetPriceEXWWithVAT) * 100
                    : 0;

                report.CIPVariance = report.ActualCIPWithVAT - report.PlannedSetPriceCIPWithVAT;
                report.CIPVariancePercent = report.PlannedSetPriceCIPWithVAT != 0
                    ? (report.CIPVariance / report.PlannedSetPriceCIPWithVAT) * 100
                    : 0;

                report.FinishedProductTransportVariance = report.ActualFinishedProductTransportCost - report.PlannedFinishedProductTransportCost;
                report.FinishedProductTransportVariancePercent = report.PlannedFinishedProductTransportCost != 0
                    ? (report.FinishedProductTransportVariance / report.PlannedFinishedProductTransportCost) * 100
                    : 0;

                reportList.Reports.Add(report);
            }

            // Calculate summary
            reportList.Summary = new TannarxAnalyticsSummaryViewModel
            {
                TotalProducts = reportList.Reports.Count,
                ProductsWithActualData = reportList.Reports.Count(r => r.ActualEXWWithVAT > 0),
                TotalPlannedCost = reportList.Reports.Sum(r => r.PlannedSetPriceEXWWithVAT),
                TotalActualCost = reportList.Reports.Sum(r => r.ActualEXWWithVAT),
                TotalVariance = reportList.Reports.Sum(r => r.EXWVariance),
                TotalVariancePercent = reportList.Reports.Any(r => r.PlannedSetPriceEXWWithVAT > 0)
                    ? (reportList.Reports.Sum(r => r.EXWVariance) / reportList.Reports.Sum(r => r.PlannedSetPriceEXWWithVAT)) * 100
                    : 0,
                AverageVariancePercent = reportList.Reports.Any(r => r.PlannedSetPriceEXWWithVAT > 0)
                    ? reportList.Reports.Average(r => r.EXWVariancePercent)
                    : 0
            };

            return reportList;
        }

        /// <summary>
        /// Get month name in Uzbek
        /// </summary>
        private string GetMonthName(int month)
        {
            var monthNames = new Dictionary<int, string>
            {
                { 1, "Yanvar" }, { 2, "Fevral" }, { 3, "Mart" },
                { 4, "Aprel" }, { 5, "May" }, { 6, "Iyun" },
                { 7, "Iyul" }, { 8, "Avgust" }, { 9, "Sentabr" },
                { 10, "Oktabr" }, { 11, "Noyabr" }, { 12, "Dekabr" }
            };

            return monthNames.ContainsKey(month) ? monthNames[month] : month.ToString();
        }

        /// <summary>
        /// Recalculate ActualMaterialPrice using sales->purchase chain (user rule):
        /// Product -> SalesOrders -> linked PurchaseOrders -> (Invoice products in month OR Income products in month).
        /// </summary>
        private async Task UpdateActualMaterialPriceFromProductionFlowAsync(int? year, int? month)
        {
            var tannarxQuery = _db.TANNARXes.Where(t => !t.IsDeleted);
            if (year.HasValue)
                tannarxQuery = tannarxQuery.Where(t => t.Year == year.Value);
            if (month.HasValue)
                tannarxQuery = tannarxQuery.Where(t => t.Month == month.Value);

            var tannarxRows = await tannarxQuery.ToListAsync();
            if (!tannarxRows.Any())
                return;

            var yearMonthPairs = tannarxRows
                .Select(t => new { t.Year, t.Month })
                .Distinct()
                .ToList();

            foreach (var ym in yearMonthPairs)
            {
                var rowsInMonth = tannarxRows.Where(t => t.Year == ym.Year && t.Month == ym.Month).ToList();
                foreach (var t in rowsInMonth)
                {
                    t.ActualMaterialPrice = await CalculateActualMaterialUnitPriceFromSalesPurchaseFlowAsync(t.ProductID, ym.Year, ym.Month);
                }
            }

            await _db.SaveChangesAsync();
        }

        private static int? ParseProductionId(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return null;
            var m = ProductionIdRegex.Match(description);
            if (!m.Success)
                return null;
            return int.TryParse(m.Groups[1].Value, out var id) ? id : (int?)null;
        }

        /// <summary>
        /// User rule-based actual costs:
        /// 1) Tax/Bank + Other: month FIN_TRANSACTIONS (Direction=2) total / month total production qty.
        /// 2) A Labor: piece-rate amount for product/month / product month production qty.
        /// 3) C Administrative: hourly payroll month total / month total production qty.
        /// </summary>
        private async Task UpdateActualCostBreakdownByUserRulesAsync(int? year, int? month)
        {
            var tannarxQuery = _db.TANNARXes.Where(t => !t.IsDeleted);
            if (year.HasValue)
                tannarxQuery = tannarxQuery.Where(t => t.Year == year.Value);
            if (month.HasValue)
                tannarxQuery = tannarxQuery.Where(t => t.Month == month.Value);

            var tannarxRows = await tannarxQuery.ToListAsync();
            if (!tannarxRows.Any())
                return;

            var ymPairs = tannarxRows
                .Select(t => new { t.Year, t.Month })
                .Distinct()
                .ToList();

            foreach (var ym in ymPairs)
            {
                var rowsInMonth = tannarxRows.Where(t => t.Year == ym.Year && t.Month == ym.Month).ToList();
                var productIds = rowsInMonth.Select(t => t.ProductID).Distinct().ToList();

                var productionRows = await _db.PRODUCT_PRODUCTION
                    .Where(p => (p.IsDeleted == false || p.IsDeleted == null)
                        && p.Date.Year == ym.Year
                        && p.Date.Month == ym.Month
                        && productIds.Contains(p.ProductID))
                    .Select(p => new { p.ProductID, p.Total })
                    .ToListAsync();

                var totalProdQtyMonth = productionRows.Sum(x => (decimal)x.Total);
                var prodQtyByProduct = productionRows
                    .GroupBy(x => x.ProductID)
                    .ToDictionary(g => g.Key, g => g.Sum(x => (decimal)x.Total));

                var start = new DateTime(ym.Year, ym.Month, 1);
                var end = start.AddMonths(1);
                var finOutTotal = await _db.FIN_TRANSACTIONS
                    .Where(f => (f.IsDeleted == false || f.IsDeleted == null)
                        && f.Direction == 2
                        && f.TxnDate >= start
                        && f.TxnDate < end)
                    .SumAsync(f => (decimal?)f.Amount) ?? 0m;
                var sharedPerUnit = totalProdQtyMonth > 0 ? (finOutTotal / totalProdQtyMonth) : 0m;

                var pieceEntries = await _db.HR_PIECE_RATE_WORK_ENTRIES
                    .Include(e => e.HR_EMPLOYEES)
                    .Where(e => e.IsDeleted != true
                        && e.WorkDate >= start
                        && e.WorkDate < end
                        && productIds.Contains(e.ProductID))
                    .ToListAsync();

                var productOps = pieceEntries
                    .Select(e => new { e.ProductID, e.OperationID })
                    .Distinct()
                    .ToList();
                var productOpIds = productOps.Select(x => (int?)x.OperationID).Distinct().ToList();
                var pieceProductIds = productOps.Select(x => x.ProductID).Distinct().ToList();

                var tariffs = new List<HR_PIECE_RATE_TARIFFS>();
                if (pieceProductIds.Any() && productOpIds.Any())
                {
                    tariffs = await _db.HR_PIECE_RATE_TARIFFS
                        .Where(t => t.IsDeleted != true
                            && t.IsActive
                            && pieceProductIds.Contains(t.ProductID)
                            && productOpIds.Contains(t.OperationID))
                        .ToListAsync();
                }

                decimal ResolveRate(HR_PIECE_RATE_WORK_ENTRIES entry)
                {
                    var positionId = entry.HR_EMPLOYEES?.PositionID;
                    var direct = tariffs
                        .Where(t => t.ProductID == entry.ProductID
                            && t.OperationID == entry.OperationID
                            && t.EffectiveFrom <= entry.WorkDate
                            && (t.EffectiveTo == null || t.EffectiveTo >= entry.WorkDate)
                            && (positionId.HasValue ? t.PositionID == positionId.Value : t.PositionID == null))
                        .OrderByDescending(t => t.PositionID.HasValue)
                        .ThenByDescending(t => t.EffectiveFrom)
                        .FirstOrDefault();
                    if (direct != null)
                        return direct.RatePerUnit;

                    var general = tariffs
                        .Where(t => t.ProductID == entry.ProductID
                            && t.OperationID == entry.OperationID
                            && t.PositionID == null
                            && t.EffectiveFrom <= entry.WorkDate
                            && (t.EffectiveTo == null || t.EffectiveTo >= entry.WorkDate))
                        .OrderByDescending(t => t.EffectiveFrom)
                        .FirstOrDefault();
                    return general?.RatePerUnit ?? 0m;
                }

                var pieceAmountByProduct = pieceEntries
                    .GroupBy(e => e.ProductID)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(e => e.Quantity * ResolveRate(e)));

                var hourlyPayrollTotal = await _db.HR_PAYROLL_LINES
                    .Where(l => l.IsDeleted != true
                        && l.HR_PAYROLL_HEADERS.IsDeleted != true
                        && l.HR_PAYROLL_HEADERS.Year == ym.Year
                        && l.HR_PAYROLL_HEADERS.Month == ym.Month
                        && l.HR_PAYROLL_HEADERS.PayrollType != "PieceRate")
                    .SumAsync(l => (decimal?)(l.GrossSalary ?? l.NetSalary)) ?? 0m;
                var adminPerUnit = totalProdQtyMonth > 0 ? (hourlyPayrollTotal / totalProdQtyMonth) : 0m;

                foreach (var t in rowsInMonth)
                {
                    var prodQty = prodQtyByProduct.ContainsKey(t.ProductID) ? prodQtyByProduct[t.ProductID] : 0m;
                    var pieceAmt = pieceAmountByProduct.ContainsKey(t.ProductID) ? pieceAmountByProduct[t.ProductID] : 0m;
                    t.ActualLaborCost = (prodQty > 0 && pieceAmt > 0) ? (pieceAmt / prodQty) : 0m;
                    t.ActualTaxAndBank = sharedPerUnit;
                    t.ActualOtherExpenses = sharedPerUnit;
                    t.ActualAdministrativeExpenses = adminPerUnit;
                }
            }

            await _db.SaveChangesAsync();
        }

        private async Task<string> BuildActualMaterialCalculationNoteAsync(int productId, int year, int month)
        {
            try
            {
                var start = new DateTime(year, month, 1);
                var end = start.AddMonths(1);

                var productions = await _db.PRODUCT_PRODUCTION
                    .Where(p => (p.IsDeleted == false || p.IsDeleted == null)
                        && p.ProductID == productId
                        && p.Date >= start
                        && p.Date < end)
                    .Select(p => new { p.ID, p.Total, p.Date })
                    .OrderBy(p => p.Date)
                    .ToListAsync();

                var totalProduced = productions.Sum(x => (decimal)x.Total);
                if (!productions.Any() || totalProduced <= 0)
                {
                    return "Bu oy uchun ishlab chiqarish yozuvi topilmadi, fakt material narxi 0 deb olindi.";
                }

                var salesOrderIds = await GetSalesOrderIdsByFinishedProductAsync(productId);
                var purchaseOrderIds = await GetPurchaseOrderIdsBySalesOrderTokensAsync(salesOrderIds);

                if (!purchaseOrderIds.Any())
                {
                    return $"Ishlab chiqarish soni: {totalProduced:N2}. Ushbu mahsulotga bog'langan SalesOrder -> PurchaseOrder topilmadi, fakt material narxi 0 deb olindi.";
                }

                var invoicesInMonth = await _db.P_INVOICES
                    .Where(i => i.IsDeleted != true
                        && i.OrderID.HasValue
                        && purchaseOrderIds.Contains(i.OrderID.Value)
                        && i.InvoiceDate >= start
                        && i.InvoiceDate < end)
                    .Select(i => i.ID)
                    .ToListAsync();

                var invoiceRows = await _db.P_INVOICE_PRODUCTS
                    .Include(x => x.PRODUCT)
                    .Where(x => invoicesInMonth.Contains(x.InvoiceID))
                    .ToListAsync();

                var invoiceAmount = invoiceRows.Sum(x => (decimal)x.TotalPrice);
                var invoiceMaterialRows = invoiceRows
                    .GroupBy(x => x.ProductID)
                    .Select(g => new
                    {
                        Code = g.First().PRODUCT?.PNo ?? g.Key.ToString(),
                        Name = g.First().PRODUCT?.Name ?? "",
                        Qty = g.Sum(x => (decimal)x.Quantity),
                        Avg = g.Sum(x => (decimal)x.Quantity) > 0 ? g.Sum(x => (decimal)x.TotalPrice) / g.Sum(x => (decimal)x.Quantity) : 0m,
                        Amount = g.Sum(x => (decimal)x.TotalPrice),
                        Source = "Invoice"
                    })
                    .ToList();

                var incomeRows = new List<INCOME_PRODUCTS>();
                decimal incomeAmount = 0m;
                if (!invoicesInMonth.Any())
                {
                    var incomesInMonth = await _db.INCOMES
                        .Where(i => (i.IsDeleted == false || i.IsDeleted == null)
                            && i.InvoiceID.HasValue
                            && i.IssueDateTime >= start
                            && i.IssueDateTime < end
                            && _db.P_INVOICES.Any(pi => pi.ID == i.InvoiceID.Value
                                && pi.OrderID.HasValue
                                && purchaseOrderIds.Contains(pi.OrderID.Value)))
                        .Select(i => i.ID)
                        .ToListAsync();

                    incomeRows = await _db.INCOME_PRODUCTS
                        .Include(x => x.PRODUCT)
                        .Where(x => incomesInMonth.Contains(x.IncomeID))
                        .ToListAsync();
                    incomeAmount = incomeRows.Sum(x => (decimal)(x.TotalPrice ?? ((x.PiecePrice ?? 0) * x.Amount)));
                }

                var incomeMaterialRows = incomeRows
                    .GroupBy(x => x.ProductID)
                    .Select(g => new
                    {
                        Code = g.First().PRODUCT?.PNo ?? g.Key.ToString(),
                        Name = g.First().PRODUCT?.Name ?? "",
                        Qty = g.Sum(x => (decimal)x.Amount),
                        Avg = g.Sum(x => (decimal)x.Amount) > 0 ? g.Sum(x => (decimal)(x.TotalPrice ?? ((x.PiecePrice ?? 0) * x.Amount))) / g.Sum(x => (decimal)x.Amount) : 0m,
                        Amount = g.Sum(x => (decimal)(x.TotalPrice ?? ((x.PiecePrice ?? 0) * x.Amount))),
                        Source = "Income"
                    })
                    .ToList();

                var totalMaterial = invoiceAmount > 0 ? invoiceAmount : incomeAmount;
                var linkedSalesOrderIds = await GetSalesOrderIdsByPurchaseOrderLinksAsync(purchaseOrderIds);
                var totalSalesOrderQty = await GetTotalSalesOrderProductQtyAsync(linkedSalesOrderIds);
                var denominatorQty = totalSalesOrderQty > 0 ? totalSalesOrderQty : totalProduced;
                var unitFact = denominatorQty > 0 ? (totalMaterial / denominatorQty) : 0m;

                var lines = new List<string>
                {
                    $"Ishlab chiqarilgan miqdor: {totalProduced:N2}",
                    $"Bog'langan SalesOrderlar: {salesOrderIds.Count}, PurchaseOrderlar: {purchaseOrderIds.Count}",
                    $"PO orqali topilgan umumiy SalesOrderlar: {linkedSalesOrderIds.Count}",
                    $"Oydagi Invoice soni: {invoicesInMonth.Count}",
                    invoiceAmount > 0
                        ? $"Material summasi manbasi: Purchase Invoice ({invoiceAmount:N2})"
                        : $"Material summasi manbasi: Income ({incomeAmount:N2})",
                    "Materiallar kesimida (fakt):"
                };

                var selectedRows = invoiceAmount > 0
                    ? invoiceMaterialRows.Cast<dynamic>().ToList()
                    : incomeMaterialRows.Cast<dynamic>().ToList();
                lines.AddRange(selectedRows.Select(m =>
                    $"- [{m.Source}] {m.Code} {m.Name}: miqdor {m.Qty:N2}, o'rtacha narx {m.Avg:N2}, summa {m.Amount:N2}"));

                lines.Add($"Jami fakt material summasi: {totalMaterial:N2}");
                lines.Add($"SalesOrderlar bo'yicha jami mahsulot soni: {totalSalesOrderQty:N2}");
                lines.Add($"Fakt material narxi (1 dona): {unitFact:N2} = {totalMaterial:N2} / {denominatorQty:N2}");
                return string.Join(Environment.NewLine, lines);
            }
            catch (Exception ex)
            {
                return $"Material fakt izohini hisoblashda xatolik: {ex.Message}";
            }
        }

        private async Task<decimal> CalculateActualMaterialUnitPriceFromSalesPurchaseFlowAsync(int productId, int year, int month)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);

            var producedQtyDouble = await _db.PRODUCT_PRODUCTION
                .Where(p => (p.IsDeleted == false || p.IsDeleted == null)
                    && p.ProductID == productId
                    && p.Date >= start
                    && p.Date < end)
                .SumAsync(p => (double?)p.Total) ?? 0d;
            var producedQty = (decimal)producedQtyDouble;
            if (producedQty <= 0)
                return 0m;

            var salesOrderIds = await GetSalesOrderIdsByFinishedProductAsync(productId);
            if (!salesOrderIds.Any())
                return 0m;

            var purchaseOrderIds = await GetPurchaseOrderIdsBySalesOrderTokensAsync(salesOrderIds);
            if (!purchaseOrderIds.Any())
                return 0m;

            var invoiceIds = await _db.P_INVOICES
                .Where(i => i.IsDeleted != true
                    && i.OrderID.HasValue
                    && purchaseOrderIds.Contains(i.OrderID.Value)
                    && i.InvoiceDate >= start
                    && i.InvoiceDate < end)
                .Select(i => i.ID)
                .ToListAsync();

            decimal totalMaterial = 0m;
            if (invoiceIds.Any())
            {
                var totalMaterialDouble = await _db.P_INVOICE_PRODUCTS
                    .Where(ip => invoiceIds.Contains(ip.InvoiceID))
                    .SumAsync(ip => (double?)ip.TotalPrice) ?? 0d;
                totalMaterial = (decimal)totalMaterialDouble;
            }
            else
            {
                var incomeIds = await _db.INCOMES
                    .Where(i => (i.IsDeleted == false || i.IsDeleted == null)
                        && i.InvoiceID.HasValue
                        && i.IssueDateTime >= start
                        && i.IssueDateTime < end
                        && _db.P_INVOICES.Any(pi => pi.ID == i.InvoiceID.Value
                            && pi.OrderID.HasValue
                            && purchaseOrderIds.Contains(pi.OrderID.Value)))
                    .Select(i => i.ID)
                    .ToListAsync();

                if (incomeIds.Any())
                {
                    var totalMaterialDouble = await _db.INCOME_PRODUCTS
                        .Where(ip => incomeIds.Contains(ip.IncomeID))
                        .SumAsync(ip => (double?)(ip.TotalPrice ?? ((ip.PiecePrice ?? 0) * ip.Amount))) ?? 0d;
                    totalMaterial = (decimal)totalMaterialDouble;
                }
            }

            var linkedSalesOrderIds = await GetSalesOrderIdsByPurchaseOrderLinksAsync(purchaseOrderIds);
            var totalSalesOrderQty = await GetTotalSalesOrderProductQtyAsync(linkedSalesOrderIds);
            var denominatorQty = totalSalesOrderQty > 0 ? totalSalesOrderQty : producedQty;

            return (totalMaterial > 0 && denominatorQty > 0) ? (totalMaterial / denominatorQty) : 0m;
        }

        private async Task<List<int>> GetSalesOrderIdsByFinishedProductAsync(int productId)
        {
            return await _db.S_ORDER_PRODUCTS
                .Where(sop => sop.ProductID == productId
                    && sop.S_ORDERS.IsDeleted != true)
                .Select(sop => sop.OrderID)
                .Distinct()
                .ToListAsync();
        }

        private async Task<List<int>> GetPurchaseOrderIdsBySalesOrderTokensAsync(List<int> salesOrderIds)
        {
            if (salesOrderIds == null || !salesOrderIds.Any())
                return new List<int>();

            var salesOrderNos = await _db.S_ORDERS
                .Where(so => salesOrderIds.Contains(so.ID) && so.IsDeleted != true)
                .Select(so => so.OrderNo)
                .ToListAsync();

            var buyTokens = salesOrderNos
                .Select(ExtractBuyTokenFromOrderNo)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();
            if (!buyTokens.Any())
                return new List<int>();

            var candidates = await _db.P_ORDER_PRODUCTS
                .Where(pop => pop.P_ORDERS.IsDeleted != true
                    && pop.Priority != null
                    && (pop.Priority.Contains("Buy#") || pop.Priority.Contains("B:")))
                .Select(pop => new { pop.OrderID, pop.Priority })
                .ToListAsync();

            return candidates
                .Where(c => buyTokens.Any(t =>
                    c.Priority.IndexOf("Buy#" + t, StringComparison.OrdinalIgnoreCase) >= 0
                    || c.Priority.IndexOf("B:" + t, StringComparison.OrdinalIgnoreCase) >= 0))
                .Select(c => c.OrderID)
                .Distinct()
                .ToList();
        }

        private async Task<List<int>> GetSalesOrderIdsByPurchaseOrderLinksAsync(List<int> purchaseOrderIds)
        {
            if (purchaseOrderIds == null || !purchaseOrderIds.Any())
                return new List<int>();

            var poPriorities = await _db.P_ORDER_PRODUCTS
                .Where(pop => purchaseOrderIds.Contains(pop.OrderID)
                    && pop.Priority != null
                    && (pop.Priority.Contains("Buy#") || pop.Priority.Contains("B:")))
                .Select(pop => pop.Priority)
                .ToListAsync();

            var buyTokens = poPriorities
                .SelectMany(ExtractBuyTokensFromPriority)
                .Distinct()
                .ToList();
            if (!buyTokens.Any())
                return new List<int>();

            var salesOrders = await _db.S_ORDERS
                .Where(so => so.IsDeleted != true)
                .Select(so => new { so.ID, so.OrderNo })
                .ToListAsync();

            return salesOrders
                .Where(so => buyTokens.Contains(ExtractBuyTokenFromOrderNo(so.OrderNo)))
                .Select(so => so.ID)
                .Distinct()
                .ToList();
        }

        private async Task<decimal> GetTotalSalesOrderProductQtyAsync(List<int> salesOrderIds)
        {
            if (salesOrderIds == null || !salesOrderIds.Any())
                return 0m;

            var total = await _db.S_ORDER_PRODUCTS
                .Where(sop => salesOrderIds.Contains(sop.OrderID))
                .SumAsync(sop => (double?)sop.Amount) ?? 0d;
            return (decimal)total;
        }

        private static List<string> ExtractBuyTokensFromPriority(string priority)
        {
            if (string.IsNullOrWhiteSpace(priority))
                return new List<string>();
            return BuyTokenRegex.Matches(priority)
                .Cast<Match>()
                .Where(m => m.Success && m.Groups.Count > 1)
                .Select(m => m.Groups[1].Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .ToList();
        }

        private static string ExtractBuyTokenFromOrderNo(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return null;
            var digits = new string(orderNo.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
                return null;
            return digits.Length <= 6 ? digits : digits.Substring(digits.Length - 6);
        }
    }
}
