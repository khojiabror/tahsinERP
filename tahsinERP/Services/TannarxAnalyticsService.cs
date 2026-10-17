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
        private const decimal Path1CoverageThreshold = 0.85m;
        private const decimal Path2CoverageThreshold = 0.80m;
        private const decimal Path3CoverageThreshold = 0.60m;
        private const decimal ReconcileThreshold = 0.03m;

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
                if (!year.HasValue || !month.HasValue)
                {
                    throw new Exception("Fakt xarajatlarni yangilash uchun yil va oy majburiy.");
                }

                _db.Database.CommandTimeout = 180;
                // SP oxirida SELECT @@ROWCOUNT qaytaradi; ExecuteSqlCommand ko'pincha -1 beradi
                var sql = "EXEC SP_TANNARX_UPDATE_ACTUAL_COSTS @Year, @Month";
                var yearParam = new SqlParameter("@Year", (object)year ?? DBNull.Value);
                var monthParam = new SqlParameter("@Month", (object)month ?? DBNull.Value);

                var result = await _db.Database.SqlQuery<int>(sql, yearParam, monthParam)
                    .FirstOrDefaultAsync();
                await RecalculateActualCostsWithSingleEngineAsync(year, month);

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
                    report.ActualFinishedTransportCalculationNote = await BuildFinishedTransportCalculationNoteAsync(
                        tannarx.ProductID,
                        tannarx.Year,
                        tannarx.Month);
                    report.ActualMaterialTransportCalculationNote = await BuildMaterialTransportCalculationNoteAsync(
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
        private static string GetMonthName(int month)
        {
            return tahsinERP.Helpers.UzMonthNames.Get(month);
        }

        private async Task RecalculateActualCostsWithSingleEngineAsync(int? year, int? month)
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
                if (!rowsInMonth.Any())
                    continue;

                var start = new DateTime(ym.Year, ym.Month, 1);
                var end = start.AddMonths(1);
                var productIds = rowsInMonth.Select(t => t.ProductID).Distinct().ToList();
                var producedQtyByProduct = await GetProducedQtyByProductAsync(start, end, productIds);
                // Overhead (tax+bank, boshqa, admin) uchun denominator:
                // o'sha oyda ishlab chiqarilgan barcha mahsulotlar jami miqdori bo'lishi kerak.
                var monthProducedQty = await GetProducedQtyAllProductsAsync(start, end);

                var materialFinTotal = await GetMaterialFinanceTotalAsync(start, end);
                var plannedMaterialSum = rowsInMonth.Sum(r => r.MaterialPrice);
                var taxBankTotal = await GetTaxBankFinanceTotalAsync(start, end);
                var otherTotal = await GetOtherFinanceTotalAsync(start, end);

                var laborByProduct = await CalculateActualLaborPerUnitByProductAsync(ym.Year, ym.Month, start, end, productIds, producedQtyByProduct);
                var adminPerUnit = await CalculateActualAdministrativePerUnitAsync(ym.Year, ym.Month, monthProducedQty);
                var taxBankPerUnit = monthProducedQty > 0 ? (taxBankTotal / monthProducedQty) : 0m;
                var otherPerUnit = monthProducedQty > 0 ? (otherTotal / monthProducedQty) : 0m;

                foreach (var row in rowsInMonth)
                {
                    var producedQty = producedQtyByProduct.ContainsKey(row.ProductID) ? producedQtyByProduct[row.ProductID] : 0m;
                    if (producedQty <= 0)
                    {
                        // Talab bo'yicha: o'sha oyda ishlab chiqarilmagan mahsulotlar uchun fakt xarajat hisoblanmaydi.
                        // Oldingi noto'g'ri qiymatlar qolib ketmasligi uchun nolga tushiramiz.
                        row.ActualMaterialPrice = 0m;
                        row.ActualLaborCost = 0m;
                        row.ActualAdministrativeExpenses = 0m;
                        row.ActualTaxAndBank = 0m;
                        row.ActualOtherExpenses = 0m;
                        await UpsertMaterialAuditAsync(row, MaterialDecision.Empty("NO_PRODUCTION_SKIPPED"));
                        continue;
                    }

                    var materialDecision = await CalculateMaterialBySelectorAsync(row.ProductID, ym.Year, ym.Month, producedQty, materialFinTotal, plannedMaterialSum, row.MaterialPrice);
                    var finishedTransportPerUnit = await CalculateActualFinishedProductTransportPerUnitAsync(row.ProductID, start, end, producedQty);
                    var materialTransportPerUnit = await CalculateActualMaterialTransportPerUnitAsync(row.ProductID, start, end, producedQty);

                    row.ActualMaterialPrice = materialDecision.UnitCost;
                    row.ActualMaterialTransportCost = materialTransportPerUnit;
                    row.ActualLaborCost = laborByProduct.ContainsKey(row.ProductID) ? laborByProduct[row.ProductID] : 0m;
                    row.ActualAdministrativeExpenses = adminPerUnit;
                    row.ActualTaxAndBank = taxBankPerUnit;
                    row.ActualOtherExpenses = otherPerUnit;
                    row.ActualFinishedProductTransportCost = finishedTransportPerUnit;

                    // Rejadagi formula bilan bir xil oqimda fakt EXW/CIP ni qayta hisoblaymiz.
                    var actualProfit = row.ActualProfit ?? row.Profit;
                    row.ActualProfit = actualProfit;

                    var actualExwWithoutVat =
                        row.ActualMaterialPrice.GetValueOrDefault() +
                        row.ActualMaterialTransportCost.GetValueOrDefault() +
                        row.ActualLaborCost.GetValueOrDefault() +
                        row.ActualTaxAndBank.GetValueOrDefault() +
                        row.ActualAdministrativeExpenses.GetValueOrDefault() +
                        row.ActualOtherExpenses.GetValueOrDefault() +
                        actualProfit;
                    row.ActualSalesPriceEXWWithoutVAT = actualExwWithoutVat;
                    row.ActualSalesPriceEXWWithVAT = actualExwWithoutVat * 1.12m;

                    var actualCipWithoutVat = actualExwWithoutVat + row.ActualFinishedProductTransportCost.GetValueOrDefault();
                    row.ActualSalesPriceCIPWithoutVAT = actualCipWithoutVat;
                    row.ActualSalesPriceCIPWithVAT = actualCipWithoutVat * 1.12m;

                    await UpsertMaterialAuditAsync(row, materialDecision);
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

        private async Task<Dictionary<int, decimal>> GetProducedQtyByProductAsync(DateTime start, DateTime end, List<int> productIds)
        {
            var productionRows = await _db.PRODUCT_PRODUCTION
                .Where(p => (p.IsDeleted == false || p.IsDeleted == null)
                    && p.Date >= start
                    && p.Date < end
                    && productIds.Contains(p.ProductID))
                .Select(p => new { p.ProductID, p.Total })
                .ToListAsync();

            return productionRows
                .GroupBy(x => x.ProductID)
                .ToDictionary(g => g.Key, g => g.Sum(x => (decimal)x.Total));
        }

        private async Task<decimal> GetProducedQtyAllProductsAsync(DateTime start, DateTime end)
        {
            var totals = await _db.PRODUCT_PRODUCTION
                .Where(p => (p.IsDeleted == false || p.IsDeleted == null)
                    && p.Date >= start
                    && p.Date < end)
                .Select(p => (double?)p.Total)
                .ToListAsync();

            return totals.Sum(x => Convert.ToDecimal(x ?? 0d));
        }

        private async Task<List<int>> GetBomComponentIdsAsync(int productId)
        {
            var bomHeaderId = await _db.BOM_HEADERS
                .Where(h => h.ProductID == productId && h.IsActive == true)
                .OrderByDescending(h => h.ID)
                .Select(h => (int?)h.ID)
                .FirstOrDefaultAsync();

            if (!bomHeaderId.HasValue)
            {
                bomHeaderId = await _db.BOM_HEADERS
                    .Where(h => h.ProductID == productId)
                    .OrderByDescending(h => h.ID)
                    .Select(h => (int?)h.ID)
                    .FirstOrDefaultAsync();
            }

            if (!bomHeaderId.HasValue)
                return new List<int>();

            return await _db.BOM_LINES
                .Where(l => l.BOMID == bomHeaderId.Value && l.ComponentID.HasValue)
                .Select(l => l.ComponentID.Value)
                .Distinct()
                .ToListAsync();
        }

        private async Task<Dictionary<int, decimal>> CalculateActualLaborPerUnitByProductAsync(
            int year,
            int month,
            DateTime start,
            DateTime end,
            List<int> productIds,
            Dictionary<int, decimal> producedQtyByProduct)
        {
            var result = productIds.ToDictionary(id => id, _ => 0m);
            var fallbackMap = await CalculateLaborFromTimesheetTariffFallbackAsync(start, end, productIds);

            foreach (var productId in productIds)
            {
                var producedQty = producedQtyByProduct.ContainsKey(productId) ? producedQtyByProduct[productId] : 0m;
                if (producedQty <= 0)
                {
                    result[productId] = 0m;
                    continue;
                }

                var amount = fallbackMap.ContainsKey(productId) ? fallbackMap[productId] : 0m;
                result[productId] = amount > 0 ? amount / producedQty : 0m;
            }

            return result;
        }

        private async Task<Dictionary<int, decimal>> CalculateLaborFromTimesheetTariffFallbackAsync(DateTime start, DateTime end, List<int> productIds)
        {
            var pieceEntries = await _db.HR_PIECE_RATE_WORK_ENTRIES
                .Include(e => e.HR_EMPLOYEES)
                .Where(e => e.IsDeleted != true
                    && e.WorkDate >= start
                    && e.WorkDate < end
                    && productIds.Contains(e.ProductID))
                .ToListAsync();

            if (!pieceEntries.Any())
                return productIds.ToDictionary(id => id, _ => 0m);

            var productOps = pieceEntries
                .Select(e => new { e.ProductID, e.OperationID })
                .Distinct()
                .ToList();
            var productOpIds = productOps.Select(x => (int?)x.OperationID).Distinct().ToList();
            var pieceProductIds = productOps.Select(x => x.ProductID).Distinct().ToList();

            var tariffs = await _db.HR_PIECE_RATE_TARIFFS
                .Where(t => t.IsDeleted != true
                    && t.IsActive
                    && pieceProductIds.Contains(t.ProductID)
                    && productOpIds.Contains(t.OperationID))
                .ToListAsync();

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

            return pieceEntries
                .GroupBy(e => e.ProductID)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(e => e.Quantity * ResolveRate(e)));
        }

        private async Task<decimal> CalculateActualAdministrativePerUnitAsync(int year, int month, decimal monthProducedQty)
        {
            if (monthProducedQty <= 0)
                return 0m;

            var approvedHourly = await _db.HR_PAYROLL_LINES
                .Where(l => l.IsDeleted != true
                    && l.HR_PAYROLL_HEADERS.IsDeleted != true
                    && l.HR_PAYROLL_HEADERS.Year == year
                    && l.HR_PAYROLL_HEADERS.Month == month
                    && l.HR_PAYROLL_HEADERS.PayrollType != "PieceRate"
                    && (l.HR_PAYROLL_HEADERS.Status == "Approved" || l.HR_PAYROLL_HEADERS.Status == "Posted"))
                .Select(l => l.GrossSalary ?? l.NetSalary)
                .ToListAsync();
            var approvedHourlyTotal = approvedHourly.Sum(x => x ?? 0m);

            if (approvedHourlyTotal > 0)
                return approvedHourlyTotal / monthProducedQty;

            var fallbackHourly = await _db.HR_TIMESHEET_LINES
                .Include(l => l.HR_EMPLOYEES)
                .Where(l => l.IsDeleted != true
                    && l.HR_TIMESHEET_HEADERS.IsDeleted != true
                    && l.HR_TIMESHEET_HEADERS.Year == year
                    && l.HR_TIMESHEET_HEADERS.Month == month)
                .ToListAsync();
            var fallbackHourlyAmount = fallbackHourly.Sum(l =>
            {
                var hours = l.TotalWorkedHours ?? 0m;
                var baseSalary = l.HR_EMPLOYEES?.BaseSalary ?? 0m;
                var estimatedHourlyRate = baseSalary > 0 ? baseSalary / 160m : 0m;
                return hours * estimatedHourlyRate;
            });

            return fallbackHourlyAmount > 0 ? fallbackHourlyAmount / monthProducedQty : 0m;
        }

        private async Task<decimal> GetMaterialFinanceTotalAsync(DateTime start, DateTime end)
        {
            var amounts = await _db.FIN_TRANSACTIONS
                .Where(f => (f.IsDeleted == false || f.IsDeleted == null)
                    && f.Direction == 2
                    && f.TxnDate >= start
                    && f.TxnDate < end
                    && f.FIN_CATEGORIES != null
                    && f.FIN_CATEGORIES.Name != null
                    && f.FIN_CATEGORIES.Name.ToLower().Contains("material"))
                .Select(f => f.Amount)
                .ToListAsync();
            return amounts.Sum(x => Convert.ToDecimal(x));
        }

        private async Task<decimal> GetTaxBankFinanceTotalAsync(DateTime start, DateTime end)
        {
            var amounts = await _db.FIN_TRANSACTIONS
                .Where(f => (f.IsDeleted == false || f.IsDeleted == null)
                    && f.Direction == 2
                    && f.TxnDate >= start
                    && f.TxnDate < end
                    && f.FIN_CATEGORIES != null
                    && f.FIN_CATEGORIES.Name != null
                    && (f.FIN_CATEGORIES.Name.ToLower().Contains("soliq") || f.FIN_CATEGORIES.Name.ToLower().Contains("bank")))
                .Select(f => f.Amount)
                .ToListAsync();
            return amounts.Sum(x => Convert.ToDecimal(x));
        }

        private async Task<decimal> GetOtherFinanceTotalAsync(DateTime start, DateTime end)
        {
            var amounts = await _db.FIN_TRANSACTIONS
                .Where(f => (f.IsDeleted == false || f.IsDeleted == null)
                    && f.Direction == 2
                    && f.TxnDate >= start
                    && f.TxnDate < end
                    && f.FIN_CATEGORIES != null
                    && f.FIN_CATEGORIES.Name != null
                    && f.FIN_CATEGORIES.Name.ToLower().Contains("boshqa"))
                .Select(f => f.Amount)
                .ToListAsync();
            return amounts.Sum(x => Convert.ToDecimal(x));
        }

        private async Task<MaterialDecision> CalculateMaterialBySelectorAsync(
            int productId,
            int year,
            int month,
            decimal producedQty,
            decimal monthMaterialFinTotal,
            decimal monthPlannedMaterialTotal,
            decimal productPlannedMaterial)
        {
            if (producedQty <= 0)
            {
                return MaterialDecision.Empty("NO_PRODUCTION");
            }

            var path2 = await EvaluatePath2IncomeFallbackAsync(productId, year, month, producedQty);
            if (path2.Passed)
                return path2;

            var path1 = await EvaluatePath1SalesPurchaseInvoiceAsync(productId, year, month, producedQty);
            if (path1.Passed)
                return path1;

            var path3 = EvaluatePath3FinanceAllocation(producedQty, monthMaterialFinTotal, monthPlannedMaterialTotal, productPlannedMaterial);
            if (path3.Passed)
                return path3;

            // NONE bo'lmasin: PATH2 default policy bo'yicha doim qiymat qaytaramiz.
            if (productPlannedMaterial > 0)
            {
                return MaterialDecision.Pass(
                    "PATH2",
                    "INCOME_FALLBACK_FORCED_PLANNED",
                    productPlannedMaterial,
                    productPlannedMaterial * producedQty,
                    producedQty,
                    0m,
                    0m);
            }

            return MaterialDecision.Pass(
                "PATH2",
                "INCOME_FALLBACK_FORCED_ZERO",
                0m,
                0m,
                producedQty,
                0m,
                0m);
        }

        private async Task<MaterialDecision> EvaluatePath1SalesPurchaseInvoiceAsync(int productId, int year, int month, decimal producedQty)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);
            var bomComponentIds = await GetBomComponentIdsAsync(productId);
            if (!bomComponentIds.Any())
                return MaterialDecision.Fail("PATH1", "NO_BOM_COMPONENTS");

            var salesOrderIds = await GetSalesOrderIdsByFinishedProductInPeriodAsync(productId, start, end);
            var purchaseOrderIds = await GetPurchaseOrderIdsBySalesOrderTokensAsync(salesOrderIds);
            if (!purchaseOrderIds.Any())
                return MaterialDecision.Fail("PATH1", "NO_PO_LINK");

            var invoiceHeaders = await _db.P_INVOICES
                .Where(i => i.IsDeleted != true
                    && i.OrderID.HasValue
                    && purchaseOrderIds.Contains(i.OrderID.Value)
                    && i.InvoiceDate >= start
                    && i.InvoiceDate < end)
                .Select(i => new { i.ID, i.Amount })
                .ToListAsync();

            if (!invoiceHeaders.Any())
                return MaterialDecision.Fail("PATH1", "NO_PI_DOCS");

            var invoiceIds = invoiceHeaders.Select(x => x.ID).ToList();
            var invoiceLines = await _db.P_INVOICE_PRODUCTS
                .Where(x => invoiceIds.Contains(x.InvoiceID))
                .Select(x => new { x.InvoiceID, x.ProductID, x.TotalPrice })
                .ToListAsync();

            var bomMatchedLines = invoiceLines
                .Where(x => bomComponentIds.Contains(x.ProductID))
                .ToList();
            if (!bomMatchedLines.Any())
                return MaterialDecision.Fail("PATH1", "NO_BOM_MATCH_IN_PI");

            var coveredInvoiceCount = invoiceHeaders.Count(h => bomMatchedLines.Any(l => l.InvoiceID == h.ID));
            var coverage = invoiceHeaders.Count == 0 ? 0m : (decimal)coveredInvoiceCount / invoiceHeaders.Count;
            var headerAmount = invoiceHeaders.Sum(x => Convert.ToDecimal(x.Amount));
            var linesAmount = bomMatchedLines.Sum(x => Convert.ToDecimal(x.TotalPrice));
            var reconcile = headerAmount != 0 ? Math.Abs(linesAmount - headerAmount) / Math.Abs(headerAmount) : 0m;
            var pass = coverage >= Path1CoverageThreshold && reconcile <= ReconcileThreshold;
            if (!pass)
            {
                // User policy: default by PATH1.
                // Agar PATH1 zanjirida summa mavjud bo'lsa, strict threshold o'tmasa ham PATH1 ni tanlaymiz.
                var fallbackAmount = linesAmount > 0 ? linesAmount : headerAmount;
                if (fallbackAmount > 0 && producedQty > 0)
                {
                    var fallbackUnitCost = fallbackAmount / producedQty;
                    return MaterialDecision.Pass("PATH1", "SO_PO_PI_RELAXED", fallbackUnitCost, fallbackAmount, producedQty, coverage, reconcile);
                }

                return MaterialDecision.Fail("PATH1", "COVERAGE_OR_RECONCILE_FAIL", coverage, reconcile);
            }

            var unitCost = producedQty > 0 ? linesAmount / producedQty : 0m;
            return MaterialDecision.Pass("PATH1", "SO_PO_PI", unitCost, linesAmount, producedQty, coverage, reconcile);
        }

        private async Task<MaterialDecision> EvaluatePath2IncomeFallbackAsync(int productId, int year, int month, decimal producedQty)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);
            var bomComponentIds = await GetBomComponentIdsAsync(productId);
            if (!bomComponentIds.Any())
                return MaterialDecision.Fail("PATH2", "NO_BOM_COMPONENTS");

            var salesOrderIds = await GetSalesOrderIdsByFinishedProductInPeriodAsync(productId, start, end);
            var purchaseOrderIds = await GetPurchaseOrderIdsBySalesOrderTokensAsync(salesOrderIds);
            if (!purchaseOrderIds.Any())
                return MaterialDecision.Fail("PATH2", "NO_PO_LINK");

            var invoiceIds = await _db.P_INVOICES
                .Where(i => i.IsDeleted != true
                    && i.OrderID.HasValue
                    && purchaseOrderIds.Contains(i.OrderID.Value))
                .Select(i => i.ID)
                .ToListAsync();
            if (!invoiceIds.Any())
                return MaterialDecision.Fail("PATH2", "NO_SOURCE_INVOICES");

            var incomes = await _db.INCOMES
                .Where(i => (i.IsDeleted == false || i.IsDeleted == null)
                    && i.InvoiceID.HasValue
                    && invoiceIds.Contains(i.InvoiceID.Value)
                    && i.IssueDateTime >= start
                    && i.IssueDateTime < end)
                .Select(i => new { i.ID, i.TotalPrice })
                .ToListAsync();
            if (!incomes.Any())
                return MaterialDecision.Fail("PATH2", "NO_INCOME_DOCS");

            var incomeIds = incomes.Select(i => i.ID).ToList();
            var incomeLines = await _db.INCOME_PRODUCTS
                .Where(x => incomeIds.Contains(x.IncomeID))
                .Select(x => new
                {
                    x.IncomeID,
                    x.ProductID,
                    x.TotalPrice,
                    x.PiecePrice,
                    x.Amount
                })
                .ToListAsync();

            var bomMatchedIncomeLines = incomeLines
                .Where(x => bomComponentIds.Contains(x.ProductID))
                .ToList();
            if (!bomMatchedIncomeLines.Any())
                return MaterialDecision.Fail("PATH2", "NO_BOM_MATCH_INCOME");

            var coveredIncomeCount = incomes.Count(h => bomMatchedIncomeLines.Any(l => l.IncomeID == h.ID));
            var coverage = incomes.Count == 0 ? 0m : (decimal)coveredIncomeCount / incomes.Count;
            var headerAmount = incomes.Sum(x => Convert.ToDecimal(x.TotalPrice ?? 0));
            var linesAmount = bomMatchedIncomeLines.Sum(x => Convert.ToDecimal(x.TotalPrice ?? ((x.PiecePrice ?? 0) * x.Amount)));
            var reconcile = headerAmount != 0 ? Math.Abs(linesAmount - headerAmount) / Math.Abs(headerAmount) : 0m;
            var pass = coverage >= Path2CoverageThreshold && reconcile <= ReconcileThreshold;
            if (!pass)
            {
                return MaterialDecision.Fail("PATH2", "COVERAGE_OR_RECONCILE_FAIL", coverage, reconcile);
            }

            var unitCost = producedQty > 0 ? linesAmount / producedQty : 0m;
            return MaterialDecision.Pass("PATH2", "INCOME_FALLBACK", unitCost, linesAmount, producedQty, coverage, reconcile);
        }

        private MaterialDecision EvaluatePath3FinanceAllocation(
            decimal producedQty,
            decimal monthMaterialFinTotal,
            decimal monthPlannedMaterialTotal,
            decimal productPlannedMaterial)
        {
            if (producedQty <= 0 || monthMaterialFinTotal <= 0 || monthPlannedMaterialTotal <= 0 || productPlannedMaterial < 0)
                return MaterialDecision.Fail("PATH3", "NO_DRIVER_OR_NO_FIN");

            var share = productPlannedMaterial / monthPlannedMaterialTotal;
            if (share < Path3CoverageThreshold)
                return MaterialDecision.Fail("PATH3", "INSUFFICIENT_DRIVER_COVERAGE", share, 0m);

            var allocated = monthMaterialFinTotal * share;
            var unitCost = allocated / producedQty;
            return MaterialDecision.Pass("PATH3", "FIN_ALLOC_BY_PLANNED_SHARE", unitCost, allocated, producedQty, share, 0m);
        }

        private async Task UpsertMaterialAuditAsync(TANNARX row, MaterialDecision decision)
        {
            const string sql = @"
IF OBJECT_ID('dbo.TANNARX_ACTUAL_AUDIT', 'U') IS NOT NULL
BEGIN
    MERGE dbo.TANNARX_ACTUAL_AUDIT AS target
    USING (SELECT @TannarxID AS TannarxID) AS source
    ON target.TannarxID = source.TannarxID
    WHEN MATCHED THEN
        UPDATE SET
            SelectedPath = @SelectedPath,
            MethodCode = @MethodCode,
            ConfidenceScore = @ConfidenceScore,
            CoveragePercent = @CoveragePercent,
            ReconcileDelta = @ReconcileDelta,
            SourceAmount = @SourceAmount,
            DenominatorQty = @DenominatorQty,
            ReasonCode = @ReasonCode,
            UpdatedAt = GETDATE()
    WHEN NOT MATCHED THEN
        INSERT (TannarxID, SelectedPath, MethodCode, ConfidenceScore, CoveragePercent, ReconcileDelta, SourceAmount, DenominatorQty, ReasonCode, UpdatedAt)
        VALUES (@TannarxID, @SelectedPath, @MethodCode, @ConfidenceScore, @CoveragePercent, @ReconcileDelta, @SourceAmount, @DenominatorQty, @ReasonCode, GETDATE());
END";

            await _db.Database.ExecuteSqlCommandAsync(
                sql,
                new SqlParameter("@TannarxID", row.ID),
                new SqlParameter("@SelectedPath", (object)decision.Path ?? DBNull.Value),
                new SqlParameter("@MethodCode", (object)decision.MethodCode ?? DBNull.Value),
                new SqlParameter("@ConfidenceScore", (object)decision.ConfidenceScore ?? DBNull.Value),
                new SqlParameter("@CoveragePercent", decision.CoveragePercent),
                new SqlParameter("@ReconcileDelta", decision.ReconcileDelta),
                new SqlParameter("@SourceAmount", decision.SourceAmount),
                new SqlParameter("@DenominatorQty", decision.DenominatorQty),
                new SqlParameter("@ReasonCode", (object)decision.ReasonCode ?? DBNull.Value)
            );
        }

        private async Task<decimal> CalculateActualFinishedProductTransportPerUnitAsync(int productId, DateTime start, DateTime end, decimal producedQty)
        {
            if (producedQty <= 0)
                return 0m;
            var productInvoiceIds = await GetSalesInvoiceIdsForProductInPeriodAsync(productId, start, end);
            if (!productInvoiceIds.Any())
                return 0m;

            var waybills = await _db.F_WAYBILLS
                .Where(w => w.IsDeleted != true
                    && w.SalesInvoiceID.HasValue
                    && productInvoiceIds.Contains(w.SalesInvoiceID.Value)
                    && w.IssueDate.HasValue
                    && w.IssueDate.Value >= start
                    && w.IssueDate.Value < end)
                .Select(w => new { InvoiceID = w.SalesInvoiceID.Value, w.Amount })
                .ToListAsync();
            if (!waybills.Any())
                return 0m;

            var lines = await _db.SALES_INVOICE_LINES
                .Where(l => productInvoiceIds.Contains(l.InvoiceID))
                .Select(l => new { l.InvoiceID, l.ProductID, l.Qty, l.LineAmount })
                .ToListAsync();

            decimal allocatedTotal = 0m;
            foreach (var invoiceId in productInvoiceIds)
            {
                var wbSum = waybills
                    .Where(w => w.InvoiceID == invoiceId)
                    .Sum(w => Convert.ToDecimal(w.Amount ?? 0d));
                if (wbSum <= 0)
                    continue;

                var invLines = lines.Where(x => x.InvoiceID == invoiceId).ToList();
                if (!invLines.Any())
                    continue;

                var totalQty = invLines.Sum(x => x.Qty);
                var productQty = invLines.Where(x => x.ProductID == productId).Sum(x => x.Qty);
                if (totalQty > 0 && productQty > 0)
                {
                    allocatedTotal += wbSum * (productQty / totalQty); // Version-1 (qty share)
                    continue;
                }

                var totalAmount = invLines.Sum(x => x.LineAmount ?? 0m);
                var productAmount = invLines.Where(x => x.ProductID == productId).Sum(x => x.LineAmount ?? 0m);
                if (totalAmount > 0 && productAmount > 0)
                {
                    allocatedTotal += wbSum * (productAmount / totalAmount); // Version-2 (value share)
                    continue;
                }

                allocatedTotal += wbSum / invLines.Count; // Version-3 (equal split)
            }

            return allocatedTotal > 0 ? allocatedTotal / producedQty : 0m;
        }

        private async Task<List<long>> GetSalesInvoiceIdsForProductInPeriodAsync(int productId, DateTime start, DateTime end)
        {
            var byLine = await _db.SALES_INVOICE_LINES
                .Where(l => l.ProductID == productId
                    && l.SALES_INVOICES.InvoiceDate >= start
                    && l.SALES_INVOICES.InvoiceDate < end
                    && l.SALES_INVOICES.StatusCode != "CANCELED"
                    && l.SALES_INVOICES.StatusCode != "DRAFT")
                .Select(l => l.InvoiceID)
                .Distinct()
                .ToListAsync();
            if (byLine.Any())
                return byLine;

            // Fallback: product SO lariga bog'langan sales invoice lar (line mapping xatolarida ham chainni ushlash uchun).
            var soIds = await GetSalesOrderIdsByFinishedProductInPeriodAsync(productId, start, end);
            if (!soIds.Any())
                return new List<long>();

            return await _db.SALES_INVOICES
                .Where(i => i.OrderID.HasValue
                    && soIds.Contains(i.OrderID.Value)
                    && i.InvoiceDate >= start
                    && i.InvoiceDate < end
                    && i.StatusCode != "CANCELED"
                    && i.StatusCode != "DRAFT")
                .Select(i => i.ID)
                .Distinct()
                .ToListAsync();
        }

        private async Task<decimal> CalculateActualMaterialTransportPerUnitAsync(int productId, DateTime start, DateTime end, decimal producedQty)
        {
            if (producedQty <= 0)
                return 0m;

            var bomComponentIds = await GetBomComponentIdsAsync(productId);
            if (!bomComponentIds.Any())
                return 0m;

            var salesOrderIds = await GetSalesOrderIdsByFinishedProductInPeriodAsync(productId, start, end);
            var purchaseOrderIds = await GetPurchaseOrderIdsBySalesOrderTokensAsync(salesOrderIds);
            if (!purchaseOrderIds.Any())
                return 0m;

            var piIdsInPeriod = await _db.P_INVOICES
                .Where(i => i.IsDeleted != true
                    && i.OrderID.HasValue
                    && purchaseOrderIds.Contains(i.OrderID.Value)
                    && i.InvoiceDate >= start
                    && i.InvoiceDate < end)
                .Select(i => i.ID)
                .ToListAsync();
            if (!piIdsInPeriod.Any())
                return 0m;

            var waybillInvoiceIds = await _db.F_WAYBILLS
                .Where(w => w.IsDeleted != true
                    && w.InvoiceID.HasValue
                    && piIdsInPeriod.Contains(w.InvoiceID.Value)
                    && w.IssueDate.HasValue
                    && w.IssueDate.Value >= start
                    && w.IssueDate.Value < end)
                .Select(w => w.InvoiceID.Value)
                .Distinct()
                .ToListAsync();
            if (!waybillInvoiceIds.Any())
                return 0m;

            var invoiceLines = await _db.P_INVOICE_PRODUCTS
                .Where(l => waybillInvoiceIds.Contains(l.InvoiceID))
                .Select(l => new { l.InvoiceID, l.ProductID, l.Quantity, l.TotalPrice })
                .ToListAsync();
            var waybills = await _db.F_WAYBILLS
                .Where(w => w.IsDeleted != true
                    && w.InvoiceID.HasValue
                    && waybillInvoiceIds.Contains(w.InvoiceID.Value))
                .Select(w => new { InvoiceID = w.InvoiceID.Value, w.Amount })
                .ToListAsync();
            if (!invoiceLines.Any() || !waybills.Any())
                return 0m;

            decimal allocatedTotal = 0m;
            foreach (var invoiceId in waybillInvoiceIds)
            {
                var wbSum = waybills
                    .Where(w => w.InvoiceID == invoiceId)
                    .Sum(w => Convert.ToDecimal(w.Amount ?? 0d));
                if (wbSum <= 0)
                    continue;

                var lines = invoiceLines.Where(x => x.InvoiceID == invoiceId).ToList();
                if (!lines.Any())
                    continue;

                var totalQty = lines.Sum(x => Convert.ToDecimal(x.Quantity));
                var bomQty = lines
                    .Where(x => bomComponentIds.Contains(x.ProductID))
                    .Sum(x => Convert.ToDecimal(x.Quantity));
                if (totalQty > 0 && bomQty > 0)
                {
                    allocatedTotal += wbSum * (bomQty / totalQty); // Version-1 (qty share, user rule)
                    continue;
                }

                var totalAmount = lines.Sum(x => Convert.ToDecimal(x.TotalPrice));
                var bomAmount = lines
                    .Where(x => bomComponentIds.Contains(x.ProductID))
                    .Sum(x => Convert.ToDecimal(x.TotalPrice));
                if (totalAmount > 0 && bomAmount > 0)
                {
                    allocatedTotal += wbSum * (bomAmount / totalAmount); // Version-2 (value share)
                    continue;
                }

                var matchedCount = lines.Count(x => bomComponentIds.Contains(x.ProductID));
                if (matchedCount > 0)
                {
                    allocatedTotal += wbSum * (Convert.ToDecimal(matchedCount) / lines.Count); // Version-3 (line count share)
                }
            }

            return allocatedTotal > 0 ? allocatedTotal / producedQty : 0m;
        }

        private async Task<string> BuildActualMaterialCalculationNoteAsync(int productId, int year, int month)
        {
            try
            {
                var start = new DateTime(year, month, 1);
                var end = start.AddMonths(1);
                var producedQty = await _db.PRODUCT_PRODUCTION
                    .Where(p => (p.IsDeleted == false || p.IsDeleted == null)
                        && p.ProductID == productId
                        && p.Date >= start
                        && p.Date < end)
                    .SumAsync(p => (double?)p.Total) ?? 0d;
                var producedQtyDecimal = Convert.ToDecimal(producedQty);

                var rowsInMonth = await _db.TANNARXes
                    .Where(t => !t.IsDeleted && t.Year == year && t.Month == month)
                    .ToListAsync();
                var monthPlannedMaterialTotal = rowsInMonth.Sum(x => x.MaterialPrice);
                var productPlanned = rowsInMonth.FirstOrDefault(x => x.ProductID == productId)?.MaterialPrice ?? 0m;
                var monthMaterialFinTotal = await GetMaterialFinanceTotalAsync(start, end);
                var bomComponentIds = await GetBomComponentIdsAsync(productId);
                var salesOrderIds = await GetSalesOrderIdsByFinishedProductInPeriodAsync(productId, start, end);
                var purchaseOrderIds = await GetPurchaseOrderIdsBySalesOrderTokensAsync(salesOrderIds);
                var poNos = await _db.P_ORDERS.Where(p => purchaseOrderIds.Contains(p.ID)).Select(p => p.OrderNo).ToListAsync();

                var invoiceHeaders = await _db.P_INVOICES
                    .Where(i => i.IsDeleted != true
                        && i.OrderID.HasValue
                        && purchaseOrderIds.Contains(i.OrderID.Value)
                        && i.InvoiceDate >= start
                        && i.InvoiceDate < end)
                    .Select(i => new { i.ID, i.InvoiceNo, i.Amount })
                    .ToListAsync();
                var invoiceIds = invoiceHeaders.Select(x => x.ID).ToList();

                var piLines = await _db.P_INVOICE_PRODUCTS
                    .Where(x => invoiceIds.Contains(x.InvoiceID))
                    .Select(x => new { x.InvoiceID, x.ProductID, x.TotalPrice })
                    .ToListAsync();
                var piMatched = piLines.Where(x => bomComponentIds.Contains(x.ProductID)).ToList();
                var piMatchedAmount = piMatched.Sum(x => Convert.ToDecimal(x.TotalPrice));
                var piAllAmount = piLines.Sum(x => Convert.ToDecimal(x.TotalPrice));
                var piHeaderAmount = invoiceHeaders.Sum(x => Convert.ToDecimal(x.Amount));
                var piCoverageDocs = invoiceHeaders.Count(h => piMatched.Any(l => l.InvoiceID == h.ID));

                var decision = await CalculateMaterialBySelectorAsync(
                    productId,
                    year,
                    month,
                    producedQtyDecimal,
                    monthMaterialFinTotal,
                    monthPlannedMaterialTotal,
                    productPlanned);
                var soNos = await _db.S_ORDERS
                    .Where(s => salesOrderIds.Contains(s.ID))
                    .Select(s => s.OrderNo)
                    .ToListAsync();

                var noteLines = new List<string>
                {
                    $"Period: 01.{month:D2}.{year} - {DateTime.DaysInMonth(year, month):D2}.{month:D2}.{year}",
                    $"Ishlab chiqarilgan maxsulot(shu product) soni: {producedQtyDecimal:N2} ta",
                    $"Hisoblanish qoidasi: {decision.Path} ({decision.MethodCode})",
                    $"Tanlash sababi kodi: {decision.ReasonCode}",
                    $"BOM komponentlar soni: {bomComponentIds.Count}",
                    $"Savdo buyurtmasi: {(soNos.Any() ? string.Join(", ", soNos) : "-")}",
                    $"Unga bog'langan Xarid buyurtmasi: {(poNos.Any() ? string.Join(", ", poNos) : "-")}",
                    $"Xarid buyurtmasiga bog'langan Invoice: {(invoiceHeaders.Any() ? string.Join(", ", invoiceHeaders.Select(x => x.InvoiceNo)) : "-")}",
                    $"PI Header jami summasi: {piHeaderAmount:N2} so'm",
                    $"PI line jami summasi: {piAllAmount:N2} so'm",
                    $"Bu maxsulot BOMiga mos material summasi: {piMatchedAmount:N2} so'm",
                    $"BOMga kirmaydigan line summasi (hisobga olinmadi): {(piAllAmount - piMatchedAmount):N2} so'm",
                    $"Coverage: {piCoverageDocs}/{invoiceHeaders.Count} ({decision.CoveragePercent:P2})",
                    $"Reconcile delta: {decision.ReconcileDelta:P2}",
                    $"Formula: Fakt material (1 dona) = {decision.SourceAmount:N2} / {decision.DenominatorQty:N2}",
                    $"Fakt material narxi (1 dona): {decision.UnitCost:N2} so'm"
                };

                if (decision.MethodCode.Contains("FORCED"))
                {
                    noteLines.Add("Izoh: PATH1/PATH2/PATH3 manbasi yetarli bo'lmagani uchun policy bo'yicha majburiy fallback qo'llandi (NONE yo'q).");
                }

                return string.Join(Environment.NewLine, noteLines);
            }
            catch (Exception ex)
            {
                return $"Material fakt izohini hisoblashda xatolik: {ex.Message}";
            }
        }

        private async Task<string> BuildFinishedTransportCalculationNoteAsync(int productId, int year, int month)
        {
            try
            {
                var start = new DateTime(year, month, 1);
                var end = start.AddMonths(1);
                var producedQty = await _db.PRODUCT_PRODUCTION
                    .Where(p => (p.IsDeleted == false || p.IsDeleted == null)
                        && p.ProductID == productId
                        && p.Date >= start
                        && p.Date < end)
                    .SumAsync(p => (double?)p.Total) ?? 0d;
                var producedQtyDecimal = Convert.ToDecimal(producedQty);
                var productInvoiceIds = await GetSalesInvoiceIdsForProductInPeriodAsync(productId, start, end);

                var salesInvoiceNos = await _db.SALES_INVOICES
                    .Where(i => productInvoiceIds.Contains(i.ID))
                    .Select(i => i.InvoiceNo)
                    .ToListAsync();

                var waybills = await _db.F_WAYBILLS
                    .Where(w => w.IsDeleted != true
                        && w.SalesInvoiceID.HasValue
                        && productInvoiceIds.Contains(w.SalesInvoiceID.Value)
                        && w.IssueDate.HasValue
                        && w.IssueDate.Value >= start
                        && w.IssueDate.Value < end)
                    .Select(w => new { w.WaybillNo, InvoiceID = w.SalesInvoiceID.Value, w.Amount })
                    .ToListAsync();

                var totalWb = waybills.Sum(w => Convert.ToDecimal(w.Amount ?? 0d));
                var perUnit = producedQtyDecimal > 0 ? await CalculateActualFinishedProductTransportPerUnitAsync(productId, start, end, producedQtyDecimal) : 0m;

                return string.Join(Environment.NewLine, new[]
                {
                    $"Period: 01.{month:D2}.{year} - {DateTime.DaysInMonth(year, month):D2}.{month:D2}.{year}",
                    $"Ishlab chiqarilgan maxsulot(shu product) soni: {producedQtyDecimal:N2} ta",
                    "Hisoblanish qoidasi: PATH1 (SalesInvoice -> Waybill, V1/V2/V3)",
                    $"Shu product bor SalesInvoice lar soni: {productInvoiceIds.Count}",
                    $"SalesInvoice lar: {(salesInvoiceNos.Any() ? string.Join(", ", salesInvoiceNos) : "-")}",
                    $"Ulangan Waybill lar: {(waybills.Any() ? string.Join(", ", waybills.Select(x => x.WaybillNo).Distinct()) : "-")}",
                    $"Waybill jami summasi: {totalWb:N2} so'm",
                    $"Formula: Fakt tayyor maxsulot tashuvi (1 dona) = ajratilgan tashuv summasi / {producedQtyDecimal:N2}",
                    $"Natija: {perUnit:N2} so'm"
                });
            }
            catch (Exception ex)
            {
                return $"Tayyor mahsulot tashuvi izohida xatolik: {ex.Message}";
            }
        }

        private async Task<string> BuildMaterialTransportCalculationNoteAsync(int productId, int year, int month)
        {
            try
            {
                var start = new DateTime(year, month, 1);
                var end = start.AddMonths(1);
                var producedQty = await _db.PRODUCT_PRODUCTION
                    .Where(p => (p.IsDeleted == false || p.IsDeleted == null)
                        && p.ProductID == productId
                        && p.Date >= start
                        && p.Date < end)
                    .SumAsync(p => (double?)p.Total) ?? 0d;
                var producedQtyDecimal = Convert.ToDecimal(producedQty);
                var bomComponentIds = await GetBomComponentIdsAsync(productId);

                var salesOrderIds = await GetSalesOrderIdsByFinishedProductInPeriodAsync(productId, start, end);
                var salesOrderNos = await _db.S_ORDERS
                    .Where(s => salesOrderIds.Contains(s.ID))
                    .Select(s => s.OrderNo)
                    .ToListAsync();

                var purchaseOrderIds = await GetPurchaseOrderIdsBySalesOrderTokensAsync(salesOrderIds);
                var purchaseOrderNos = await _db.P_ORDERS
                    .Where(p => purchaseOrderIds.Contains(p.ID))
                    .Select(p => p.OrderNo)
                    .ToListAsync();

                var piHeaders = await _db.P_INVOICES
                    .Where(i => i.IsDeleted != true
                        && i.OrderID.HasValue
                        && purchaseOrderIds.Contains(i.OrderID.Value)
                        && i.InvoiceDate >= start
                        && i.InvoiceDate < end)
                    .Select(i => new { i.ID, i.InvoiceNo })
                    .ToListAsync();
                var piIds = piHeaders.Select(i => i.ID).ToList();

                var waybills = await _db.F_WAYBILLS
                    .Where(w => w.IsDeleted != true
                        && w.InvoiceID.HasValue
                        && piIds.Contains(w.InvoiceID.Value)
                        && w.IssueDate.HasValue
                        && w.IssueDate.Value >= start
                        && w.IssueDate.Value < end)
                    .Select(w => new { w.WaybillNo, InvoiceID = w.InvoiceID.Value, w.Amount })
                    .ToListAsync();

                var piLineRows = await _db.P_INVOICE_PRODUCTS
                    .Where(l => piIds.Contains(l.InvoiceID))
                    .Select(l => new { l.InvoiceID, l.ProductID, l.Quantity, l.TotalPrice })
                    .ToListAsync();

                var matchedRows = piLineRows.Where(x => bomComponentIds.Contains(x.ProductID)).ToList();
                var matchedQty = matchedRows.Sum(x => Convert.ToDecimal(x.Quantity));
                var totalQty = piLineRows.Sum(x => Convert.ToDecimal(x.Quantity));
                var matchedAmount = matchedRows.Sum(x => Convert.ToDecimal(x.TotalPrice));
                var totalAmount = piLineRows.Sum(x => Convert.ToDecimal(x.TotalPrice));
                var totalWaybillAmount = waybills.Sum(w => Convert.ToDecimal(w.Amount ?? 0d));

                var perUnit = producedQtyDecimal > 0 ? await CalculateActualMaterialTransportPerUnitAsync(productId, start, end, producedQtyDecimal) : 0m;
                return string.Join(Environment.NewLine, new[]
                {
                    $"Period: 01.{month:D2}.{year} - {DateTime.DaysInMonth(year, month):D2}.{month:D2}.{year}",
                    $"Ishlab chiqarilgan maxsulot(shu product) soni: {producedQtyDecimal:N2} ta",
                    "Hisoblanish qoidasi: PATH1 (BOM materiallari -> PI -> Waybill, V1/V2/V3)",
                    $"BOM komponentlar soni: {bomComponentIds.Count}",
                    $"Savdo buyurtmasi: {(salesOrderNos.Any() ? string.Join(", ", salesOrderNos) : "-")}",
                    $"Bog'langan Xarid buyurtmasi: {(purchaseOrderNos.Any() ? string.Join(", ", purchaseOrderNos) : "-")}",
                    $"Bog'langan Xarid invoice: {(piHeaders.Any() ? string.Join(", ", piHeaders.Select(x => x.InvoiceNo)) : "-")}",
                    $"Bog'langan Waybill: {(waybills.Any() ? string.Join(", ", waybills.Select(x => x.WaybillNo).Distinct()) : "-")}",
                    $"PI line jami qty: {totalQty:N2}, BOMga mos qty: {matchedQty:N2}",
                    $"PI line jami summa: {totalAmount:N2} so'm, BOMga mos summa: {matchedAmount:N2} so'm",
                    $"Waybill jami summasi: {totalWaybillAmount:N2} so'm",
                    $"Formula: Fakt material tashuvi (1 dona) = ajratilgan tashuv summasi / {producedQtyDecimal:N2}",
                    $"Natija: {perUnit:N2} so'm"
                });
            }
            catch (Exception ex)
            {
                return $"Material tashuvi izohida xatolik: {ex.Message}";
            }
        }

        private async Task<List<int>> GetSalesOrderIdsByFinishedProductInPeriodAsync(int productId, DateTime start, DateTime end)
        {
            return await _db.S_ORDER_PRODUCTS
                .Where(sop => sop.ProductID == productId
                    && sop.S_ORDERS.IsDeleted != true
                    && sop.S_ORDERS.IssuedDate >= start
                    && sop.S_ORDERS.IssuedDate < end)
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

        private class MaterialDecision
        {
            public bool Passed { get; private set; }
            public string Path { get; private set; }
            public string MethodCode { get; private set; }
            public string ConfidenceScore { get; private set; }
            public decimal CoveragePercent { get; private set; }
            public decimal ReconcileDelta { get; private set; }
            public decimal SourceAmount { get; private set; }
            public decimal DenominatorQty { get; private set; }
            public decimal UnitCost { get; private set; }
            public string ReasonCode { get; private set; }

            public static MaterialDecision Pass(string path, string methodCode, decimal unitCost, decimal sourceAmount, decimal denominatorQty, decimal coverage, decimal reconcile)
            {
                return new MaterialDecision
                {
                    Passed = true,
                    Path = path,
                    MethodCode = methodCode,
                    CoveragePercent = coverage,
                    ReconcileDelta = reconcile,
                    SourceAmount = sourceAmount,
                    DenominatorQty = denominatorQty,
                    UnitCost = unitCost,
                    ReasonCode = "OK",
                    ConfidenceScore = (coverage >= 0.8m && reconcile <= 0.02m) ? "HIGH" : "MEDIUM"
                };
            }

            public static MaterialDecision Fail(string path, string reasonCode, decimal coverage = 0m, decimal reconcile = 0m)
            {
                return new MaterialDecision
                {
                    Passed = false,
                    Path = path,
                    MethodCode = path,
                    CoveragePercent = coverage,
                    ReconcileDelta = reconcile,
                    SourceAmount = 0m,
                    DenominatorQty = 0m,
                    UnitCost = 0m,
                    ReasonCode = reasonCode,
                    ConfidenceScore = "LOW"
                };
            }

            public static MaterialDecision Empty(string reasonCode)
            {
                return new MaterialDecision
                {
                    Passed = false,
                    Path = "NONE",
                    MethodCode = "NONE",
                    CoveragePercent = 0m,
                    ReconcileDelta = 0m,
                    SourceAmount = 0m,
                    DenominatorQty = 0m,
                    UnitCost = 0m,
                    ReasonCode = reasonCode,
                    ConfidenceScore = "LOW"
                };
            }
        }

    }
}
