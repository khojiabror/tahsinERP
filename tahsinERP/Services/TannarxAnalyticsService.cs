using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;
using tahsinERP.ViewModels.Tannarx;

namespace tahsinERP.Services
{
    public class TannarxAnalyticsService
    {
        private readonly DBTHSNEntities _db;

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
                var sql = "EXEC SP_TANNARX_UPDATE_ACTUAL_COSTS @Year, @Month";
                var yearParam = new SqlParameter("@Year", (object)year ?? DBNull.Value);
                var monthParam = new SqlParameter("@Month", (object)month ?? DBNull.Value);

                var result = await _db.Database.ExecuteSqlCommandAsync(
                    sql,
                    yearParam, monthParam);

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
                    ActualCIPWithVAT = tannarx.ActualSalesPriceCIPWithVAT ?? 0
                };

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
    }
}
