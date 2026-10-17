using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;
using tahsinERP.Services.Tannarx;
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
                if (!year.HasValue || !month.HasValue)
                {
                    throw new Exception("Fakt xarajatlarni yangilash uchun yil va oy majburiy.");
                }

                _db.Database.CommandTimeout = 180;
                // Yagona engine — C# (TannarxActualCostEngine). SP_TANNARX_UPDATE_ACTUAL_COSTS endi chaqirilmaydi:
                // u avval barcha fakt ustunlarni yozar, C# esa ularni qayta yozib, faqat SP ning ActualProfit
                // qiymatini (QQS bilan narx − xarajat) olib qolardi — natijada fakt EXW/CIP noto'g'ri edi.
                return await RecalculateActualCostsAsync(year.Value, month.Value);
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
            var first = date.AddMonths(-11);
            var firstKey = first.Year * 12 + first.Month;
            var lastKey = currentYear * 12 + currentMonth;

            // 12 ta ketma-ket so'rov o'rniga bitta guruhlangan so'rov (MES-P08).
            var query = _db.TANNARXes
                .Where(t => !t.IsDeleted
                    && t.Year * 12 + t.Month >= firstKey
                    && t.Year * 12 + t.Month <= lastKey);
            if (productID.HasValue)
                query = query.Where(t => t.ProductID == productID.Value);

            var sums = await query
                .GroupBy(t => new { t.Year, t.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Planned = g.Sum(t => t.SetPriceEXWWithVAT ?? t.SalesPriceEXWWithVAT),
                    Actual = g.Sum(t => t.ActualSalesPriceEXWWithVAT ?? 0)
                })
                .ToListAsync();
            var byKey = sums.ToDictionary(s => s.Year * 12 + s.Month);

            for (int i = 11; i >= 0; i--)
            {
                var trendDate = date.AddMonths(-i);
                byKey.TryGetValue(trendDate.Year * 12 + trendDate.Month, out var s);
                var planned = s?.Planned ?? 0m;
                var actual = s?.Actual ?? 0m;
                trends.Add(new TannarxMonthlyTrendViewModel
                {
                    Year = trendDate.Year,
                    Month = trendDate.Month,
                    MonthString = GetMonthName(trendDate.Month),
                    PlannedTotal = planned,
                    ActualTotal = actual,
                    Variance = actual - planned
                });
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
                    ActualCIPWithVAT = tannarx.ActualSalesPriceCIPWithVAT ?? 0,
                    ActualFinishedProductTransportCost = tannarx.ActualFinishedProductTransportCost ?? 0
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
        /// Bitta mahsulot/oy uchun hisobot qatori + fakt hisob izohlari.
        /// Izohlar faqat shu yerda tuziladi (oldin Report da productID bo'lsa har qatorga ~30 so'rov ketardi — MES-P08).
        /// </summary>
        public async Task<TannarxAnalyticsReportViewModel> GetProductDetailsAsync(int productID, int year, int month)
        {
            var report = await GetAnalyticsReportAsync(year, month, productID);
            var row = report.Reports.FirstOrDefault();
            if (row != null)
                await FillCalculationNotesAsync(row, productID, year, month);
            return row;
        }

        /// <summary>
        /// Get month name in Uzbek
        /// </summary>
        private static string GetMonthName(int month)
        {
            return tahsinERP.Helpers.UzMonthNames.Get(month);
        }


        /// <summary>
        /// Bitta oy uchun fakt qiymatlarni TannarxActualCostEngine bilan hisoblab, TANNARX qatorlariga yozadi.
        /// </summary>
        private async Task<int> RecalculateActualCostsAsync(int year, int month)
        {
            var rowsInMonth = await _db.TANNARXes
                .Where(t => !t.IsDeleted && t.Year == year && t.Month == month)
                .ToListAsync();
            if (!rowsInMonth.Any())
                return 0;

            var engine = new TannarxActualCostEngine(_db);
            var monthResult = await engine.CalculateMonthAsync(year, month, rowsInMonth);
            var auditByTannarx = await LoadAuditRowsAsync(rowsInMonth.Select(r => r.ID).ToList());
            var now = DateTime.Now;

            foreach (var row in rowsInMonth)
            {
                var r = monthResult.Products[row.ProductID];
                if (r.ProducedQty <= 0)
                {
                    // O'sha oyda ishlab chiqarilmagan mahsulot uchun fakt hisoblanmaydi:
                    // barcha fakt ustunlar tozalanadi (oldin transport/EXW/CIP/foyda eski SP qiymatida qolib ketardi).
                    ClearActuals(row);
                }
                else
                {
                    row.ActualMaterialPrice = r.Material.UnitCost;
                    row.ActualMaterialTransportCost = r.MaterialTransportPerUnit;
                    row.ActualLaborCost = r.LaborPerUnit;
                    row.ActualAdministrativeExpenses = r.AdminPerUnit;
                    row.ActualTaxAndBank = r.TaxBankPerUnit;
                    row.ActualOtherExpenses = r.OtherPerUnit;
                    row.ActualFinishedProductTransportCost = r.FinishedTransportPerUnit;

                    // Biznes qarori (2026-10): fakt foyda = rejadagi foyda; fakt EXW = fakt xarajatlar + reja foyda,
                    // shunda EXW farqi = xarajatlar farqi. (Oldin SP yozgan "narx(QQS bilan) − xarajat" olinib,
                    // QQS ikki marta qo'shilardi.)
                    row.ActualProfit = row.Profit;

                    var exwWithoutVat =
                        r.Material.UnitCost +
                        r.MaterialTransportPerUnit +
                        r.LaborPerUnit +
                        r.TaxBankPerUnit +
                        r.AdminPerUnit +
                        r.OtherPerUnit +
                        row.Profit;
                    row.ActualSalesPriceEXWWithoutVAT = exwWithoutVat;
                    row.ActualSalesPriceEXWWithVAT = exwWithoutVat * TannarxActualCostEngine.VatMultiplier;

                    var cipWithoutVat = exwWithoutVat + r.FinishedTransportPerUnit;
                    row.ActualSalesPriceCIPWithoutVAT = cipWithoutVat;
                    row.ActualSalesPriceCIPWithVAT = cipWithoutVat * TannarxActualCostEngine.VatMultiplier;
                }
                row.ModifiedDT = now;

                if (auditByTannarx != null)
                    UpsertAudit(auditByTannarx, row.ID, r.Material, now);
            }

            await _db.SaveChangesAsync();
            return rowsInMonth.Count;
        }

        private static void ClearActuals(TANNARX row)
        {
            row.ActualMaterialPrice = null;
            row.ActualMaterialTransportCost = null;
            row.ActualLaborCost = null;
            row.ActualAdministrativeExpenses = null;
            row.ActualTaxAndBank = null;
            row.ActualOtherExpenses = null;
            row.ActualFinishedProductTransportCost = null;
            row.ActualProfit = null;
            row.ActualSalesPriceEXWWithoutVAT = null;
            row.ActualSalesPriceEXWWithVAT = null;
            row.ActualSalesPriceCIPWithoutVAT = null;
            row.ActualSalesPriceCIPWithVAT = null;
        }

        /// <summary>
        /// TANNARX_ACTUAL_AUDIT ba'zi bazalarda bo'lmasligi mumkin (oldingi MERGE ham OBJECT_ID bilan himoyalangan edi).
        /// Jadval yo'q bo'lsa null qaytadi va audit yozilmaydi.
        /// </summary>
        private async Task<Dictionary<int, TANNARX_ACTUAL_AUDIT>> LoadAuditRowsAsync(List<int> tannarxIds)
        {
            var tableId = await _db.Database
                .SqlQuery<int?>("SELECT OBJECT_ID(N'dbo.TANNARX_ACTUAL_AUDIT', N'U')")
                .FirstOrDefaultAsync();
            if (!tableId.HasValue)
                return null;

            var rows = await _db.TANNARX_ACTUAL_AUDIT
                .Where(a => tannarxIds.Contains(a.TannarxID))
                .ToListAsync();
            return rows
                .GroupBy(a => a.TannarxID)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.ID).First());
        }

        private void UpsertAudit(Dictionary<int, TANNARX_ACTUAL_AUDIT> existing, int tannarxId, MaterialDecision decision, DateTime now)
        {
            if (!existing.TryGetValue(tannarxId, out var audit))
            {
                audit = new TANNARX_ACTUAL_AUDIT { TannarxID = tannarxId };
                _db.TANNARX_ACTUAL_AUDIT.Add(audit);
                existing[tannarxId] = audit;
            }

            audit.SelectedPath = decision.Path;
            audit.MethodCode = decision.MethodCode;
            audit.ConfidenceScore = decision.ConfidenceScore;
            audit.CoveragePercent = decision.CoveragePercent;
            audit.ReconcileDelta = decision.ReconcileDelta;
            audit.SourceAmount = decision.SourceAmount;
            audit.DenominatorQty = decision.DenominatorQty;
            audit.ReasonCode = decision.ReasonCode;
            audit.UpdatedAt = now;
        }

        // ---------------- Hisob izohlari (faqat ProductDetails, bitta mahsulot) ----------------

        private async Task FillCalculationNotesAsync(TannarxAnalyticsReportViewModel report, int productId, int year, int monthNo)
        {
            try
            {
                var rowsInMonth = await _db.TANNARXes
                    .AsNoTracking()
                    .Where(t => !t.IsDeleted && t.Year == year && t.Month == monthNo)
                    .ToListAsync();
                var engine = new TannarxActualCostEngine(_db);
                var month = await engine.CalculateMonthAsync(year, monthNo, rowsInMonth);
                if (!month.Products.TryGetValue(productId, out var r))
                    return;

                report.ActualMaterialCalculationNote = BuildMaterialNote(month, r);
                report.ActualMaterialTransportCalculationNote = BuildMaterialTransportNote(month, r);
                report.ActualFinishedTransportCalculationNote = BuildFinishedTransportNote(month, r);
                report.ActualOverheadCalculationNote = BuildOverheadNote(month, r);
            }
            catch (Exception ex)
            {
                report.ActualMaterialCalculationNote = $"Fakt hisob izohini tuzishda xatolik: {ex.Message}";
            }
        }

        private static string PeriodLine(int year, int month)
        {
            return $"Period: 01.{month:D2}.{year} - {DateTime.DaysInMonth(year, month):D2}.{month:D2}.{year}";
        }

        private static string JoinOrDash(IEnumerable<string> values)
        {
            var list = values?.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToList() ?? new List<string>();
            return list.Any() ? string.Join(", ", list) : "-";
        }

        private static string BuildMaterialNote(TannarxMonthActualResult month, TannarxProductActualResult r)
        {
            var d = r.Material;
            var lines = new List<string>
            {
                PeriodLine(month.Year, month.Month),
                $"Ishlab chiqarilgan maxsulot(shu product) soni: {r.ProducedQty:N2} ta",
                $"Hisoblanish qoidasi: {d.Path} ({d.MethodCode})",
                $"Tanlash sababi kodi: {d.ReasonCode}"
            };
            if (r.ProducedQty <= 0)
            {
                lines.Add("Shu oyda ishlab chiqarish yo'q — fakt xarajat hisoblanmaydi.");
                return string.Join(Environment.NewLine, lines);
            }

            lines.Add($"Algoritmlar (tartib bilan): {string.Join("; ", r.PathAttempts)}");
            var issue = r.WarehouseIssue;
            if (issue != null)
            {
                lines.Add($"PATH0 — ombordan chiqim: {issue.ExpenseCount} hujjat, {issue.LineCount} qator, summa {issue.Amount:N2}; " +
                          $"ishlab chiqarish yozuvlari {issue.CoveredProductionCount}/{issue.ProductionCount}, qamrab olingan miqdor {issue.CoveredQty:N2}" +
                          (issue.ZeroPriceLineCount > 0 ? $"; narxsiz (0) qatorlar: {issue.ZeroPriceLineCount}" : ""));
            }
            else
            {
                lines.Add("PATH0 — ombordan chiqim: shu oy ishlab chiqarishiga bog'langan chiqim hujjati yo'q");
            }
            lines.Add($"BOM komponentlar soni: {r.BomComponentCount}");
            lines.Add($"Savdo buyurtmasi: {JoinOrDash(r.SalesOrderNos)}");
            lines.Add($"Unga bog'langan Xarid buyurtmasi: {JoinOrDash(r.PurchaseOrderNos)}");
            lines.Add($"Xarid buyurtmasiga bog'langan Invoice (shu oy): {JoinOrDash(r.PurchaseInvoiceNos)}");
            lines.Add($"PI Header jami summasi: {r.PiHeaderAmount:N2}");
            lines.Add($"PI line jami summasi: {r.PiLinesAmount:N2}");
            lines.Add($"Bu maxsulot BOMiga mos material summasi: {r.PiMatchedAmount:N2}");
            lines.Add($"BOMga kirmaydigan line summasi (hisobga olinmadi): {(r.PiLinesAmount - r.PiMatchedAmount):N2}");
            if (r.PiSharedLineCount > 0)
            {
                lines.Add($"Boshqa mahsulotlar bilan umumiy PI qatorlari: {r.PiSharedLineCount} ta — ishlab chiqarilgan miqdor ulushida bo'lindi; shu mahsulotga: {r.PiMatchedAllocatedAmount:N2}");
            }
            lines.Add($"Coverage: {d.CoveragePercent:P2}");
            lines.Add($"Reconcile delta: {d.ReconcileDelta:P2}");
            if (d.Path == "PATH3")
            {
                lines.Add($"Finance 'Material' xarajatlari (oy jami, {month.Finance.BaseCurrencyCode ?? "barcha valyuta"}): {month.Finance.Material:N2}; kategoriyalar: {JoinOrDash(month.Finance.MaterialCategories)}");
                lines.Add("Taqsimlash: reja material narxi × ishlab chiqarilgan miqdor ulushida.");
            }
            lines.Add($"Formula: Fakt material (1 dona) = {d.SourceAmount:N2} / {d.DenominatorQty:N2}");
            lines.Add($"Fakt material narxi (1 dona): {d.UnitCost:N2} so'm");

            if (d.MethodCode != null && d.MethodCode.Contains("FORCED"))
            {
                lines.Add("Izoh: PATH0/PATH1/PATH2/PATH3 manbasi yetarli bo'lmagani uchun policy bo'yicha majburiy fallback qo'llandi (NONE yo'q).");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string BuildFinishedTransportNote(TannarxMonthActualResult month, TannarxProductActualResult r)
        {
            return string.Join(Environment.NewLine, new[]
            {
                PeriodLine(month.Year, month.Month),
                $"Ishlab chiqarilgan maxsulot(shu product) soni: {r.ProducedQty:N2} ta",
                "Hisoblanish qoidasi: PATH1 (SalesInvoice -> Waybill, V1/V2/V3)",
                $"Shu product bor SalesInvoice lar soni: {r.SalesInvoiceNos.Count}",
                $"SalesInvoice lar: {JoinOrDash(r.SalesInvoiceNos)}",
                $"Ulangan Waybill lar (shu oy, ichki emas): {JoinOrDash(r.FinishedWaybillNos)}",
                $"Waybill jami summasi: {r.FinishedWaybillAmount:N2} so'm",
                $"Shu mahsulotga ajratilgan: {r.FinishedTransportAllocated:N2} so'm",
                $"Formula: Fakt tayyor maxsulot tashuvi (1 dona) = ajratilgan tashuv summasi / {r.ProducedQty:N2}",
                $"Natija: {r.FinishedTransportPerUnit:N2} so'm"
            });
        }

        private static string BuildMaterialTransportNote(TannarxMonthActualResult month, TannarxProductActualResult r)
        {
            return string.Join(Environment.NewLine, new[]
            {
                PeriodLine(month.Year, month.Month),
                $"Ishlab chiqarilgan maxsulot(shu product) soni: {r.ProducedQty:N2} ta",
                "Hisoblanish qoidasi: PATH1 (BOM materiallari -> PI -> Waybill, V1/V2/V3)",
                $"BOM komponentlar soni: {r.BomComponentCount}",
                $"Savdo buyurtmasi: {JoinOrDash(r.SalesOrderNos)}",
                $"Bog'langan Xarid buyurtmasi: {JoinOrDash(r.PurchaseOrderNos)}",
                $"Bog'langan Xarid invoice: {JoinOrDash(r.PurchaseInvoiceNos)}",
                $"Bog'langan Waybill (shu oyda berilgan): {JoinOrDash(r.MaterialWaybillNos)}",
                $"PI line jami qty: {r.PiTotalQty:N2}, BOMga mos qty: {r.PiMatchedQty:N2}",
                $"PI line jami summa: {r.PiLinesAmount:N2}, BOMga mos summa: {r.PiMatchedAmount:N2}",
                $"Waybill jami summasi: {r.MaterialWaybillAmount:N2} so'm",
                $"Shu mahsulotga ajratilgan: {r.MaterialTransportAllocated:N2} so'm",
                $"Formula: Fakt material tashuvi (1 dona) = ajratilgan tashuv summasi / {r.ProducedQty:N2}",
                $"Natija: {r.MaterialTransportPerUnit:N2} so'm"
            });
        }

        private static string BuildOverheadNote(TannarxMonthActualResult month, TannarxProductActualResult r)
        {
            var fin = month.Finance;
            var lines = new List<string>
            {
                PeriodLine(month.Year, month.Month),
                "Manba: Finance tranzaksiyalari (chiqim − shu kategoriyaga qaytgan kirim); xarid/sotuv invoysi hisoblash yozuvlari olinmaydi.",
                $"Valyuta: {(fin.BaseCurrencyCode != null ? "faqat " + fin.BaseCurrencyCode : "UZS topilmadi — barcha valyutalar qo'shildi")}",
                $"Oy bo'yicha jami ishlab chiqarilgan (barcha mahsulotlar): {month.MonthProducedQty:N2} ta",
                $"B — Soliq va bank: {fin.TaxBank:N2}; kategoriyalar: {JoinOrDash(fin.TaxBankCategories)}",
                $"    1 donaga: {r.TaxBankPerUnit:N2}",
                $"D — Boshqa: {fin.Other:N2}; kategoriyalar: {JoinOrDash(fin.OtherCategories)}",
                $"    1 donaga: {r.OtherPerUnit:N2}",
                $"C — Administrativ (soatbay ish haqi): {month.AdminTotal:N2}; 1 donaga: {r.AdminPerUnit:N2}",
                $"A — Ish haqi (ishbay, tarif × miqdor): {r.LaborAmount:N2}; 1 donaga: {r.LaborPerUnit:N2}",
                "Taqsimlash: B, C, D — oy jami / barcha mahsulotlar ishlab chiqarilgan miqdori (1 donaga teng)."
            };
            if (fin.SkippedForeignCount > 0)
            {
                lines.Add($"Hisobga olinmagan boshqa valyutadagi tranzaksiyalar: {fin.SkippedForeignCount} ta ({string.Join(", ", fin.SkippedForeignByCurrency.Select(kv => $"{kv.Key}: {kv.Value:N2}"))}) — konvertatsiya qilinmaydi.");
            }
            return string.Join(Environment.NewLine, lines);
        }
    }
}
