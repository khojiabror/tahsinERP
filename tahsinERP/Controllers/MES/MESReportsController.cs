using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers.MES
{
    /// <summary>
    /// MES oylik hisobotlar — ishlab chiqarish va sifat. Dynamic ma'lumot, print qilish uchun.
    /// </summary>
    public class MESReportsController : Controller
    {
        /// <summary>
        /// Ishlab chiqarish oylik hisoboti: reja vs fakt, mahsulot bo'yicha, subtotal/total, qoldiq.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> ProductionMonthly(int? year, int? month)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "ProductPlan") &&
                !tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "ProductProduction"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var today = DateTime.Today;
            var selectedYear = year ?? today.Year;
            var selectedMonth = month ?? today.Month;
            var startDate = new DateTime(selectedYear, selectedMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            using (var db = new DBTHSNEntities())
            {
                var productionWH = WarehouseFlowService.GetProductionWarehouse(db);
                int? productionWHID = productionWH?.ID;

                // Reja: PRODUCTPLANS_DAILY (oy ichidagi kunlik reja) — mahsulot bo'yicha
                var planByProduct = await db.PRODUCTPLANS_DAILY
                    .Include(d => d.PRODUCTPLAN)
                    .Where(d => d.Day >= startDate && d.Day <= endDate && d.PRODUCTPLAN != null && d.PRODUCTPLAN.IsDeleted == false)
                    .GroupBy(d => d.PRODUCTPLAN.ProductID)
                    .Select(g => new { ProductID = g.Key, Reja = g.Sum(d => d.Total) })
                    .ToListAsync();

                // Fakt: PRODUCT_PRODUCTION (oy ichidagi ishlab chiqarish)
                var actualByProduct = await db.PRODUCT_PRODUCTION
                    .Where(p => p.Date >= startDate && p.Date <= endDate && p.IsDeleted == false)
                    .GroupBy(p => p.ProductID)
                    .Select(g => new { ProductID = g.Key, Fakt = g.Sum(p => p.Total) })
                    .ToListAsync();

                var productIds = planByProduct.Select(x => x.ProductID)
                    .Union(actualByProduct.Select(x => x.ProductID))
                    .Distinct()
                    .ToList();

                var products = await db.PRODUCTS
                    .Where(p => productIds.Contains(p.ID) && p.IsDeleted == false)
                    .OrderBy(p => p.PNo)
                    .ToListAsync();

                var stockDict = new Dictionary<int, double>();
                if (productionWHID.HasValue)
                {
                    var stocks = await db.PRODUCT_STOCKS
                        .Where(s => s.WHID == productionWHID.Value && productIds.Contains(s.ProductID))
                        .ToListAsync();
                    foreach (var s in stocks)
                        stockDict[s.ProductID] = s.Amount;
                }

                var rows = new List<ProductionMonthlyReportRow>();
                foreach (var p in products)
                {
                    var reja = planByProduct.FirstOrDefault(x => x.ProductID == p.ID)?.Reja ?? 0;
                    var fakt = actualByProduct.FirstOrDefault(x => x.ProductID == p.ID)?.Fakt ?? 0;
                    var farq = fakt - reja;
                    var qoldiq = stockDict.ContainsKey(p.ID) ? stockDict[p.ID] : 0;
                    rows.Add(new ProductionMonthlyReportRow
                    {
                        ProductID = p.ID,
                        ProductCode = p.PNo ?? "",
                        ProductName = p.Name ?? "",
                        Reja = reja,
                        Fakt = fakt,
                        Farq = farq,
                        Qoldiq = qoldiq
                    });
                }

                var viewModel = new ProductionMonthlyReportViewModel
                {
                    Year = selectedYear,
                    Month = selectedMonth,
                    StartDate = startDate,
                    EndDate = endDate,
                    Rows = rows.OrderBy(r => r.ProductCode).ToList(),
                    TotalReja = rows.Sum(r => r.Reja),
                    TotalFakt = rows.Sum(r => r.Fakt),
                    TotalFarq = rows.Sum(r => r.Farq)
                };

                ViewBag.Years = Enumerable.Range(today.Year - 2, 5).ToList();
                ViewBag.Months = Enumerable.Range(1, 12).ToList();
                return View(viewModel);
            }
        }

        /// <summary>
        /// Sifat oylik hisoboti: tekshirilgan vs ishlab chiqarilgan, qabul/rad, FTQ, eng ko'p uchraydigan nuqsonlar.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> QualityMonthly(int? year, int? month)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "QualityInspection"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var today = DateTime.Today;
            var selectedYear = year ?? today.Year;
            var selectedMonth = month ?? today.Month;
            var startDate = new DateTime(selectedYear, selectedMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            using (var db = new DBTHSNEntities())
            {
                // Ishlab chiqarilgan (oy davomida) — mahsulot bo'yicha
                var producedByProduct = await db.PRODUCT_PRODUCTION
                    .Where(p => p.Date >= startDate && p.Date <= endDate && p.IsDeleted == false)
                    .GroupBy(p => p.ProductID)
                    .Select(g => new { ProductID = g.Key, Produced = g.Sum(x => x.Total) })
                    .ToListAsync();

                // Sifat tekshiruvi — mahsulot bo'yicha
                var inspections = await db.QUALITY_INSPECTION
                    .Include(q => q.PRODUCT)
                    .Include(q => q.DEFECT_TYPES)
                    .Where(q => q.IsDeleted == false && q.InspectionDate >= startDate && q.InspectionDate <= endDate)
                    .ToListAsync();

                var byProduct = inspections
                    .GroupBy(q => q.ProductID)
                    .Select(g => new QualityMonthlyReportRow
                    {
                        ProductID = g.Key,
                        ProductCode = g.First().PRODUCT?.PNo ?? g.Key.ToString(),
                        ProductName = g.First().PRODUCT?.Name ?? "",
                        TekshiruvlarSoni = g.Count(),
                        TekshirilganMiqdor = (double)g.Sum(x => x.QtyInspected),
                        QabulQilingan = (double)g.Sum(x => x.QtyAccepted),
                        RadEtilgan = (double)g.Sum(x => x.QtyRejected)
                    })
                    .ToList();

                foreach (var row in byProduct)
                {
                    var produced = producedByProduct.FirstOrDefault(x => x.ProductID == row.ProductID)?.Produced ?? 0;
                    row.IshlabChiqarilgan = produced;
                    row.FTQPercent = row.TekshirilganMiqdor > 0
                        ? Math.Round((row.QabulQilingan / row.TekshirilganMiqdor) * 100, 1)
                        : (double?)null;
                }

                var topDefects = inspections
                    .Where(q => q.QtyRejected > 0 && q.DefectTypeID.HasValue)
                    .GroupBy(q => new { q.DefectTypeID, Name = q.DEFECT_TYPES != null ? q.DEFECT_TYPES.DefectType : null })
                    .Select(g => new TopDefectRow
                    {
                        DefectTypeID = g.Key.DefectTypeID,
                        DefectName = string.IsNullOrWhiteSpace(g.Key.Name) ? ("Nuqson #" + g.Key.DefectTypeID) : g.Key.Name,
                        RejectedQty = (double)g.Sum(x => x.QtyRejected),
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.RejectedQty)
                    .ThenByDescending(x => x.Count)
                    .Take(10)
                    .ToList();

                var totalInspected = inspections.Sum(q => (double?)q.QtyInspected) ?? 0;
                var totalAccepted = inspections.Sum(q => (double?)q.QtyAccepted) ?? 0;
                var totalRejected = inspections.Sum(q => (double?)q.QtyRejected) ?? 0;
                var ftqGlobal = totalInspected > 0 ? Math.Round((totalAccepted / totalInspected) * 100, 1) : (double?)null;

                var viewModel = new QualityMonthlyReportViewModel
                {
                    Year = selectedYear,
                    Month = selectedMonth,
                    StartDate = startDate,
                    EndDate = endDate,
                    Rows = byProduct.OrderBy(r => r.ProductCode).ToList(),
                    TopDefects = topDefects,
                    TotalTekshirilgan = totalInspected,
                    TotalQabul = totalAccepted,
                    TotalRad = totalRejected,
                    FTQPercent = ftqGlobal
                };

                ViewBag.Years = Enumerable.Range(today.Year - 2, 5).ToList();
                ViewBag.Months = Enumerable.Range(1, 12).ToList();
                return View(viewModel);
            }
        }
    }
}
