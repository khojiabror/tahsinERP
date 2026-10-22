using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.I18n;
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
                    throw new Exception(L.T("Mes_TanA_Err_YearMonthRequired"));
                }

                _db.Database.CommandTimeout = 180;
                // Yagona engine — C# (TannarxActualCostEngine). SP_TANNARX_UPDATE_ACTUAL_COSTS endi chaqirilmaydi:
                // u avval barcha fakt ustunlarni yozar, C# esa ularni qayta yozib, faqat SP ning ActualProfit
                // qiymatini (QQS bilan narx − xarajat) olib qolardi — natijada fakt EXW/CIP noto'g'ri edi.
                return await RecalculateActualCostsAsync(year.Value, month.Value);
            }
            catch (Exception ex)
            {
                throw new Exception(L.T("Mes_TanA_Err_UpdateActual", ex.Message), ex);
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
            LastUpdateWarnings.Clear();
            LastUpdateWarnings.AddRange(BuildMonthWarnings(monthResult));
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

        // ---------------- Oy xarajatlari manbalari (reconciliation) ----------------

        /// <summary>
        /// Oy bo'yicha barcha xarajat manbalari: qancha tortildi, qancha mahsulotlarga taqsimlandi, nima chiqarildi va nima uchun.
        /// Hech narsa yozmaydi — "Fakt xarajatlarni yangilash" bilan bir xil engine natijasini ko'rsatadi.
        /// </summary>
        public async Task<TannarxMonthSourcesViewModel> GetMonthSourcesAsync(int year, int month)
        {
            var vm = new TannarxMonthSourcesViewModel { Year = year, Month = month, MonthString = GetMonthName(month) };

            var rowsInMonth = await _db.TANNARXes
                .AsNoTracking()
                .Include(t => t.PRODUCT)
                .Where(t => !t.IsDeleted && t.Year == year && t.Month == month)
                .ToListAsync();

            var engine = new TannarxActualCostEngine(_db);
            var m = await engine.CalculateMonthAsync(year, month, rowsInMonth);
            vm.MonthProducedQty = m.MonthProducedQty;
            vm.BaseCurrencyCode = m.Finance.BaseCurrencyCode;

            var produced = m.Products.Values.Where(p => p.ProducedQty > 0).ToList();
            decimal Allocated(Func<TannarxProductActualResult, decimal> perUnit) => produced.Sum(p => perUnit(p) * p.ProducedQty);

            foreach (var row in rowsInMonth.GroupBy(r => r.ProductID).Select(g => g.First()).OrderBy(r => r.PRODUCT?.PNo))
            {
                var p = m.Products[row.ProductID];
                vm.Products.Add(new TannarxProductSourceRow
                {
                    ProductID = row.ProductID,
                    ProductPNo = row.PRODUCT?.PNo ?? "",
                    ProductName = row.PRODUCT?.Name ?? "",
                    ProducedQty = p.ProducedQty,
                    MaterialPath = p.Material?.Path,
                    MaterialMethod = p.Material?.MethodCode,
                    PathAttempts = string.Join("; ", p.PathAttempts),
                    Material = p.ProducedQty > 0 ? p.Material.UnitCost : 0m,
                    MaterialTransport = p.MaterialTransportPerUnit,
                    Labor = p.LaborPerUnit,
                    TaxBank = p.TaxBankPerUnit,
                    Admin = p.AdminPerUnit,
                    Other = p.OtherPerUnit,
                    FinishedTransport = p.FinishedTransportPerUnit
                });
            }

            foreach (var g in produced.GroupBy(p => p.Material.Path).OrderBy(g => g.Key))
            {
                vm.MaterialPathCounts[g.Key] = g.Count();
                vm.Elements.Add(new TannarxCostElementSourceRow
                {
                    Element = L.T("Mes_Materials"),
                    Source = MaterialPathTitle(g.Key),
                    SourceTotal = g.Sum(p => p.Material.SourceAmount),
                    AllocatedTotal = g.Sum(p => p.Material.UnitCost * p.ProducedQty),
                    Note = L.T("Mes_TanA_ProductsCount", g.Count())
                });
            }
            vm.Elements.Add(new TannarxCostElementSourceRow
            {
                Element = L.T("Mes_Materials"),
                Source = L.T("Mes_TanA_Src_FinanceMaterialPool"),
                SourceTotal = m.Finance.Material,
                AllocatedTotal = produced.Where(p => p.Material.Path == "PATH3").Sum(p => p.Material.SourceAmount),
                Note = L.T("Mes_TanA_Note_FinanceMaterialRest")
            });
            vm.Elements.Add(new TannarxCostElementSourceRow
            {
                Element = L.T("Mes_TanA_El_MaterialTransport"),
                Source = L.T("Mes_TanA_Src_IncomingWaybills", m.MaterialWaybillNos.Count),
                SourceTotal = m.MaterialTransportPool,
                AllocatedTotal = Allocated(p => p.MaterialTransportPerUnit),
                Note = m.MaterialTransportByQtyShare ? L.T("Mes_TanA_Note_ByQtyShare") : L.T("Mes_TanA_Note_ByMaterialShare")
            });
            vm.Elements.Add(new TannarxCostElementSourceRow
            {
                Element = L.T("Mes_TanA_El_Labor"),
                Source = L.T("Mes_TanA_Src_PieceRate"),
                SourceTotal = m.PieceRateLaborTotal,
                AllocatedTotal = Allocated(p => p.LaborPerUnit)
            });
            vm.Elements.Add(new TannarxCostElementSourceRow
            {
                Element = L.T("Mes_Tan_Bucket_TaxBank"),
                Source = "Finance",
                SourceTotal = m.Finance.TaxBank,
                AllocatedTotal = Allocated(p => p.TaxBankPerUnit),
                Note = L.T("Mes_TanA_Note_MonthPerAllQty")
            });
            vm.Elements.Add(new TannarxCostElementSourceRow
            {
                Element = L.T("Mes_TanA_El_Admin"),
                Source = L.T("Mes_TanA_Src_HourlyPay"),
                SourceTotal = m.AdminTotal,
                AllocatedTotal = Allocated(p => p.AdminPerUnit),
                Note = L.T("Mes_TanA_Note_MonthPerAllQty")
            });
            vm.Elements.Add(new TannarxCostElementSourceRow
            {
                Element = L.T("Mes_Tan_Bucket_Other"),
                Source = L.T("Mes_TanA_Src_FinanceOther"),
                SourceTotal = m.Finance.Other,
                AllocatedTotal = Allocated(p => p.OtherPerUnit),
                Note = L.T("Mes_TanA_Note_MonthPerAllQty")
            });
            vm.Elements.Add(new TannarxCostElementSourceRow
            {
                Element = L.T("Mes_TanA_El_FinishedTransport"),
                Source = L.T("Mes_TanA_Src_SalesWaybills"),
                AllocatedTotal = Allocated(p => p.FinishedTransportPerUnit)
            });

            vm.FinanceLines = m.Finance.Lines.Select(l => new TannarxFinanceSourceRow
            {
                CategoryName = l.CategoryName,
                Bucket = l.Bucket,
                Reason = l.Reason,
                Amount = l.Amount,
                TransactionCount = l.TransactionCount,
                Included = l.Bucket != "Chiqarildi"
            }).ToList();

            if (m.ProducedWithoutTannarx.Any())
            {
                var ids = m.ProducedWithoutTannarx.Keys.ToList();
                var names = await _db.PRODUCTS.Where(p => ids.Contains(p.ID)).Select(p => new { p.ID, p.PNo, p.Name }).ToListAsync();
                vm.ProducedWithoutTannarx = names
                    .Select(p => new TannarxProducedWithoutTannarxRow { ProductID = p.ID, ProductPNo = p.PNo, ProductName = p.Name, ProducedQty = m.ProducedWithoutTannarx[p.ID] })
                    .OrderBy(p => p.ProductPNo)
                    .ToList();
            }
            vm.Warnings.AddRange(BuildMonthWarnings(m));

            return vm;
        }

        /// <summary>Fakt hisobidan keyingi ogohlantirishlar ("Fakt xarajatlarni yangilash" dan keyin ham ko'rsatiladi).</summary>
        private static List<string> BuildMonthWarnings(TannarxMonthActualResult m)
        {
            var warnings = new List<string>();
            var produced = m.Products.Values.Where(p => p.ProducedQty > 0).ToList();

            var forced = produced.Count(p => p.Material.Path == "PLAN");
            if (forced > 0)
                warnings.Add(L.T("Mes_TanA_Warn_PlanPriceUsed", forced));
            var partial = produced.Count(p => p.Material.MethodCode == "WAREHOUSE_ISSUE_FIFO_PARTIAL");
            if (partial > 0)
                warnings.Add(L.T("Mes_TanA_Warn_PartialIssue", partial));
            var zeroPriceIssues = produced.Where(p => p.WarehouseIssue != null && p.WarehouseIssue.ZeroPriceLineCount > 0).ToList();
            if (zeroPriceIssues.Any())
                warnings.Add(L.T("Mes_TanA_Warn_ZeroPriceLines", zeroPriceIssues.Count, zeroPriceIssues.Sum(p => p.WarehouseIssue.ZeroPriceLineCount)));
            if (m.ProducedWithoutTannarx.Any())
                warnings.Add(L.T("Mes_TanA_Warn_ProducedWithoutTannarx", m.ProducedWithoutTannarx.Count, m.ProducedWithoutTannarx.Values.Sum()));
            if (m.Finance.BaseCurrencyCode == null)
                warnings.Add(L.T("Mes_TanA_Warn_NoUzs"));
            if (m.Finance.SkippedForeignCount > 0)
                warnings.Add(L.T("Mes_TanA_Warn_SkippedForeign", m.Finance.SkippedForeignCount, string.Join(", ", m.Finance.SkippedForeignByCurrency.Select(kv => $"{kv.Key}: {kv.Value:N2}"))));
            return warnings;
        }

        /// <summary>Oxirgi "Fakt xarajatlarni yangilash" ogohlantirishlari (controller TempData ga chiqaradi).</summary>
        public List<string> LastUpdateWarnings { get; } = new List<string>();

        // ---------------- Finance kategoriya → tannarx guruhi ----------------

        public async Task<TannarxFinanceCategoryMapViewModel> GetFinanceCategoryMapAsync()
        {
            var store = new TannarxFinanceCategoryMapStore(_db);
            var vm = new TannarxFinanceCategoryMapViewModel { TableExists = await store.TableExistsAsync() };
            var manual = await store.LoadAsync();

            var categories = await _db.FIN_CATEGORIES
                .Where(c => c.IsDeleted != true)
                .Select(c => new TannarxFinanceCategoryClassifier.CategoryRow { ID = c.ID, Name = c.Name, Type = c.Type, ParentID = c.ParentID, AnalyticsGroup = c.AnalyticsGroup })
                .ToListAsync();
            var extra = await _db.FIN_CATEGORIES
                .Where(c => c.IsDeleted != true && c.Type == TannarxFinanceCategoryClassifier.ExpenseCategoryType)
                .Select(c => new { c.ID, c.IsActive, c.SortOrder, TxnCount = c.FIN_TRANSACTIONS.Count(t => t.IsDeleted != true) })
                .ToDictionaryAsync(c => c.ID);
            var names = categories.ToDictionary(c => c.ID, c => c.Name);

            var auto = TannarxFinanceCategoryClassifier.Classify(categories);
            var effective = TannarxFinanceCategoryClassifier.Classify(categories, manual);

            vm.Rows = categories
                .Where(c => c.Type == TannarxFinanceCategoryClassifier.ExpenseCategoryType)
                .Select(c => new TannarxFinanceCategoryMapRow
                {
                    CategoryID = c.ID,
                    Name = c.Name,
                    ParentName = c.ParentID.HasValue && names.ContainsKey(c.ParentID.Value) ? names[c.ParentID.Value] : null,
                    IsActive = extra[c.ID].IsActive,
                    AutoBucket = TannarxFinanceCategoryClassifier.BucketTitle(auto[c.ID].Bucket),
                    AutoReason = auto[c.ID].Reason,
                    ManualBucket = manual.TryGetValue(c.ID, out var b) ? (int?)b : null,
                    EffectiveBucket = TannarxFinanceCategoryClassifier.BucketTitle(effective[c.ID].Bucket),
                    TransactionCount = extra[c.ID].TxnCount
                })
                .OrderBy(r => r.ParentName ?? r.Name)
                .ThenBy(r => r.ParentName == null ? 0 : 1)
                .ThenBy(r => r.Name)
                .ToList();
            return vm;
        }

        /// <param name="selection">CategoryID → tanlov ("" = avtomatik, 0..3 = guruh).</param>
        public async Task SaveFinanceCategoryMapAsync(IDictionary<int, string> selection, int? userId)
        {
            var expenseIds = new HashSet<int>(await _db.FIN_CATEGORIES
                .Where(c => c.IsDeleted != true && c.Type == TannarxFinanceCategoryClassifier.ExpenseCategoryType)
                .Select(c => c.ID)
                .ToListAsync());

            var map = new Dictionary<int, TannarxFinanceBucket>();
            foreach (var kv in selection)
            {
                if (!expenseIds.Contains(kv.Key) || string.IsNullOrWhiteSpace(kv.Value))
                    continue;
                if (!int.TryParse(kv.Value, out var v) || v < 0 || v > 3)
                    throw new ArgumentException(L.T("Mes_TanA_Err_InvalidBucket", kv.Value));
                map[kv.Key] = (TannarxFinanceBucket)v;
            }

            await new TannarxFinanceCategoryMapStore(_db).SaveAsync(map, userId);
        }

        private static string MaterialPathTitle(string path)
        {
            switch (path)
            {
                case "PATH0": return L.T("Mes_TanA_Path0");
                case "PATH1": return L.T("Mes_TanA_Path1");
                case "PATH2": return L.T("Mes_TanA_Path2");
                case "PATH3": return L.T("Mes_TanA_Path3");
                case "PLAN": return L.T("Mes_TanA_PathPlan");
                default: return path;
            }
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

                if (r.ProducedQty > 0 && r.Material.Path == "PLAN")
                    report.ActualMaterialWarning = L.T("Mes_TanA_MatWarn_Plan");
                else if (r.Material.MethodCode == "WAREHOUSE_ISSUE_FIFO_PARTIAL")
                    report.ActualMaterialWarning = L.T("Mes_TanA_MatWarn_Partial", r.Material.CoveragePercent);
                else if (r.WarehouseIssue != null && r.WarehouseIssue.ZeroPriceLineCount > 0 && r.Material.Path == "PATH0")
                    report.ActualMaterialWarning = L.T("Mes_TanA_MatWarn_ZeroPrice", r.WarehouseIssue.ZeroPriceLineCount);
            }
            catch (Exception ex)
            {
                report.ActualMaterialCalculationNote = L.T("Mes_TanA_Err_BuildNote", ex.Message);
            }
        }

        private static string PeriodLine(int year, int month)
        {
            return L.T("Mes_TanA_N_Period", $"01.{month:D2}.{year} - {DateTime.DaysInMonth(year, month):D2}.{month:D2}.{year}");
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
                L.T("Mes_TanA_N_ProducedQty", r.ProducedQty),
                L.T("Mes_TanA_N_Rule", $"{d.Path} ({d.MethodCode})"),
                L.T("Mes_TanA_N_ReasonCode", d.ReasonCode)
            };
            if (r.ProducedQty <= 0)
            {
                lines.Add(L.T("Mes_TanA_N_NoProduction"));
                return string.Join(Environment.NewLine, lines);
            }

            lines.Add(L.T("Mes_TanA_N_Algorithms", string.Join("; ", r.PathAttempts)));
            var issue = r.WarehouseIssue;
            if (issue != null)
            {
                lines.Add(L.T("Mes_TanA_N_Path0Issue", issue.ExpenseCount, issue.LineCount, issue.Amount, issue.CoveredProductionCount, issue.ProductionCount, issue.CoveredQty) +
                          (issue.ZeroPriceLineCount > 0 ? L.T("Mes_TanA_N_ZeroPriceLines", issue.ZeroPriceLineCount) : ""));
            }
            else
            {
                lines.Add(L.T("Mes_TanA_N_Path0None"));
            }
            lines.Add(L.T("Mes_TanA_N_BomComponents", r.BomComponentCount));
            lines.Add(L.T("Mes_TanA_N_SalesOrders", JoinOrDash(r.SalesOrderNos)));
            lines.Add(L.T("Mes_TanA_N_PurchaseOrders", JoinOrDash(r.PurchaseOrderNos)));
            lines.Add(L.T("Mes_TanA_N_PurchaseInvoices", JoinOrDash(r.PurchaseInvoiceNos)));
            lines.Add(L.T("Mes_TanA_N_PiHeader", r.PiHeaderAmount));
            lines.Add(L.T("Mes_TanA_N_PiLines", r.PiLinesAmount));
            lines.Add(L.T("Mes_TanA_N_PiMatched", r.PiMatchedAmount));
            lines.Add(L.T("Mes_TanA_N_PiNotInBom", r.PiLinesAmount - r.PiMatchedAmount));
            if (r.PiSharedLineCount > 0)
            {
                lines.Add(L.T("Mes_TanA_N_PiShared", r.PiSharedLineCount, r.PiMatchedAllocatedAmount));
            }
            lines.Add($"Coverage: {d.CoveragePercent:P2}");
            lines.Add($"Reconcile delta: {d.ReconcileDelta:P2}");
            if (d.Path == "PATH3")
            {
                lines.Add(L.T("Mes_TanA_N_FinanceMaterial", month.Finance.BaseCurrencyCode ?? L.T("Mes_TanA_N_AllCurrencies"), month.Finance.Material, JoinOrDash(month.Finance.MaterialCategories)));
                lines.Add(L.T("Mes_TanA_N_AllocPlanShare"));
            }
            lines.Add(L.T("Mes_TanA_N_FormulaMaterial", d.SourceAmount, d.DenominatorQty));
            lines.Add(L.T("Mes_TanA_N_MaterialUnitCost", d.UnitCost, L.Sum));

            if (d.MethodCode != null && d.MethodCode.Contains("FORCED"))
            {
                lines.Add(L.T("Mes_TanA_N_ForcedFallback"));
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string BuildFinishedTransportNote(TannarxMonthActualResult month, TannarxProductActualResult r)
        {
            return string.Join(Environment.NewLine, new[]
            {
                PeriodLine(month.Year, month.Month),
                L.T("Mes_TanA_N_ProducedQty", r.ProducedQty),
                L.T("Mes_TanA_N_Rule", "PATH1 (SalesInvoice -> Waybill, V1/V2/V3)"),
                L.T("Mes_TanA_N_SalesInvoiceCount", r.SalesInvoiceNos.Count),
                L.T("Mes_TanA_N_SalesInvoices", JoinOrDash(r.SalesInvoiceNos)),
                L.T("Mes_TanA_N_LinkedWaybills", JoinOrDash(r.FinishedWaybillNos)),
                L.T("Mes_TanA_N_WaybillTotal", r.FinishedWaybillAmount, L.Sum),
                L.T("Mes_TanA_N_AllocatedToProduct", r.FinishedTransportAllocated, L.Sum),
                L.T("Mes_TanA_N_FormulaFinished", r.ProducedQty),
                L.T("Mes_TanA_N_Result", r.FinishedTransportPerUnit, L.Sum)
            });
        }

        private static string BuildMaterialTransportNote(TannarxMonthActualResult month, TannarxProductActualResult r)
        {
            return string.Join(Environment.NewLine, new[]
            {
                PeriodLine(month.Year, month.Month),
                L.T("Mes_TanA_N_ProducedQty", r.ProducedQty),
                L.T("Mes_TanA_N_Rule", L.T("Mes_TanA_N_RuleMatTransport")),
                "    " + (month.MaterialTransportByQtyShare
                    ? L.T("Mes_TanA_N_MatTransportByQty")
                    : L.T("Mes_TanA_N_MatTransportByAmount")),
                L.T("Mes_TanA_N_Waybills", JoinOrDash(month.MaterialWaybillNos)),
                L.T("Mes_TanA_N_MatTransportPool", month.MaterialTransportPool, L.Sum),
                L.T("Mes_TanA_N_MatTransportShare", r.MaterialTransportShare, r.MaterialTransportAllocated, L.Sum),
                L.T("Mes_TanA_N_FormulaMatTransport", r.MaterialTransportAllocated, r.ProducedQty),
                L.T("Mes_TanA_N_Result", r.MaterialTransportPerUnit, L.Sum)
            });
        }

        private static string BuildOverheadNote(TannarxMonthActualResult month, TannarxProductActualResult r)
        {
            var fin = month.Finance;
            var lines = new List<string>
            {
                PeriodLine(month.Year, month.Month),
                L.T("Mes_TanA_N_OverheadSource"),
                L.T("Mes_TanA_N_Currency", fin.BaseCurrencyCode != null ? L.T("Mes_TanA_N_OnlyCurrency", fin.BaseCurrencyCode) : L.T("Mes_TanA_N_NoUzsAllAdded")),
                L.T("Mes_TanA_N_MonthProducedQty", month.MonthProducedQty),
                L.T("Mes_TanA_N_TaxBank", fin.TaxBank, JoinOrDash(fin.TaxBankCategories)),
                "    " + L.T("Mes_TanA_N_PerUnit", r.TaxBankPerUnit),
                L.T("Mes_TanA_N_Other", fin.Other, JoinOrDash(fin.OtherCategories)),
                "    " + L.T("Mes_TanA_N_PerUnit", r.OtherPerUnit),
                L.T("Mes_TanA_N_Admin", month.AdminTotal, r.AdminPerUnit),
                L.T("Mes_TanA_N_Labor", r.LaborAmount, r.LaborPerUnit),
                L.T("Mes_TanA_N_AllocBcd")
            };
            if (fin.SkippedForeignCount > 0)
            {
                lines.Add(L.T("Mes_TanA_N_SkippedForeign", fin.SkippedForeignCount, string.Join(", ", fin.SkippedForeignByCurrency.Select(kv => $"{kv.Key}: {kv.Value:N2}"))));
            }
            return string.Join(Environment.NewLine, lines);
        }
    }
}
