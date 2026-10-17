using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Filters;

namespace tahsinERP.Controllers.MES
{
    [Permission]
    public class MESDashboardController : Controller
    {
        // Olish so'rovi: MES/Dashboard
        public async Task<ActionResult> Dashboard(int? year, int? month, int? day)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var viewModel = new MESDashboardViewModel();
                var today = DateTime.Now.Date;

                var isInitialLoad = !year.HasValue && !month.HasValue && !day.HasValue;
                var selectedYear = year ?? today.Year;
                int? selectedMonth = month;
                int? selectedDay = day;

                if (isInitialLoad)
                {
                    selectedMonth = today.Month;
                    selectedDay = today.Day;
                }

                DateTime startDate;
                DateTime endDate;
                string primaryLabel;
                string secondaryLabel;

                if (selectedMonth.HasValue && selectedDay.HasValue)
                {
                    startDate = new DateTime(selectedYear, selectedMonth.Value, selectedDay.Value);
                    endDate = startDate;
                    primaryLabel = "Tanlangan kun";
                    secondaryLabel = "Tanlangan oy";
                }
                else if (selectedMonth.HasValue)
                {
                    startDate = new DateTime(selectedYear, selectedMonth.Value, 1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                    primaryLabel = "Tanlangan oy";
                    secondaryLabel = "Tanlangan yil";
                }
                else
                {
                    startDate = new DateTime(selectedYear, 1, 1);
                    endDate = new DateTime(selectedYear, 12, 31);
                    primaryLabel = "Tanlangan yil";
                    secondaryLabel = "Tanlangan yil";
                }

                // Primary period KPIs
                var periodPlanned = await db.PRODUCTPLANS_DAILY
                    .Where(d => d.Day >= startDate && d.Day <= endDate && d.PRODUCTPLAN.IsDeleted == false)
                    .SumAsync(d => (double?)d.Total) ?? 0;

                DateTime secondaryStart;
                DateTime secondaryEnd;
                if (selectedMonth.HasValue && selectedDay.HasValue)
                {
                    secondaryStart = new DateTime(selectedYear, selectedMonth.Value, 1);
                    secondaryEnd = secondaryStart.AddMonths(1).AddDays(-1);
                }
                else
                {
                    secondaryStart = new DateTime(selectedYear, 1, 1);
                    secondaryEnd = new DateTime(selectedYear, 12, 31);
                }

                var chartStartDate = startDate;
                var chartEndDate = endDate;

                var effectiveStart = new[] { startDate, secondaryStart, chartStartDate }.Min();
                var effectiveEnd = new[] { endDate, secondaryEnd, chartEndDate }.Max();
                // Yagona attribution qoidasi: reja kuni = PlanDate ?? Date (ProductPlan va hisobotlar bilan bir xil, MES-B20).
                // Avval ±6 oylik oyna va eski QUALITY_INSPECTION.ProductPlanDailyID bog'lanishi ishlatilardi.
                var loadEndExclusive = effectiveEnd.Date.AddDays(1);
                var productionCandidates = await db.PRODUCT_PRODUCTION.AsNoTracking()
                    .Where(p => (p.IsDeleted == false || p.IsDeleted == null)
                        && (p.PlanDate ?? p.Date) >= effectiveStart
                        && (p.PlanDate ?? p.Date) < loadEndExclusive)
                    .Select(p => new ProductionFact
                    {
                        ID = p.ID,
                        ShopID = p.ShopID,
                        ShopName = p.SHOP.ShopName,
                        PlanDay = p.PlanDate ?? p.Date,
                        Total = p.Total
                    })
                    .ToListAsync();

                var periodActual = productionCandidates
                    .Where(p =>
                    {
                        var attributedDate = p.PlanDay.Date;
                        return attributedDate >= startDate.Date && attributedDate <= endDate.Date;
                    })
                    .Sum(p => (double?)p.Total) ?? 0;

                viewModel.TodayPlanned = periodPlanned;
                viewModel.TodayActual = periodActual;
                viewModel.TodayPlanVsActualPercent = periodPlanned > 0 ? (periodActual / periodPlanned) * 100 : 0;

                // Secondary period KPIs (month yoki year summary)
                var secondaryPlanned = await db.PRODUCTPLANS_DAILY
                    .Where(d => d.Day >= secondaryStart && d.Day <= secondaryEnd && d.PRODUCTPLAN.IsDeleted == false)
                    .SumAsync(d => (double?)d.Total) ?? 0;

                var secondaryActual = productionCandidates
                    .Where(p =>
                    {
                        var attributedDate = p.PlanDay.Date;
                        return attributedDate >= secondaryStart.Date && attributedDate <= secondaryEnd.Date;
                    })
                    .Sum(p => (double?)p.Total) ?? 0;

                viewModel.MonthPlanned = secondaryPlanned;
                viewModel.MonthActual = secondaryActual;
                viewModel.MonthPlanVsActualPercent = secondaryPlanned > 0 ? (secondaryActual / secondaryPlanned) * 100 : 0;

                // Daily Plan vs Actual Chart (selected period)
                var dailyPlans = await db.PRODUCTPLANS_DAILY.AsNoTracking()
                    .Where(d => d.Day >= chartStartDate && d.Day <= chartEndDate && d.PRODUCTPLAN.IsDeleted == false)
                    .Select(d => new { d.Day, d.Total })
                    .ToListAsync();

                var dailyActuals = productionCandidates
                    .Where(p =>
                    {
                        var attributedDate = p.PlanDay.Date;
                        return attributedDate >= chartStartDate.Date && attributedDate <= chartEndDate.Date;
                    })
                    .ToList();

                // Group by date in memory (after ToListAsync)
                var dailyPlansGrouped = dailyPlans
                    .GroupBy(d => d.Day.Date)
                    .Select(g => new { Date = g.Key, Planned = g.Sum(d => d.Total) })
                    .ToList();

                var dailyActualsGrouped = dailyActuals
                    .GroupBy(p =>
                    {
                        var attributedDate = p.PlanDay.Date;
                        return attributedDate;
                    })
                    .Select(g => new { Date = g.Key, Actual = g.Sum(p => p.Total) })
                    .ToList();

                var allDates = dailyPlansGrouped.Select(d => d.Date)
                    .Union(dailyActualsGrouped.Select(a => a.Date))
                    .Distinct()
                    .OrderBy(d => d)
                    .ToList();

                viewModel.DailyPlanVsActual = allDates.Select(date => new DailyPlanVsActualData
                {
                    Date = date,
                    Planned = dailyPlansGrouped.FirstOrDefault(d => d.Date == date)?.Planned ?? 0,
                    Actual = dailyActualsGrouped.FirstOrDefault(a => a.Date == date)?.Actual ?? 0
                }).ToList();

                // Production by Shop (attribution sanasi — reja kuni bilan mos)
                var productionByShop = productionCandidates
                    .Where(p =>
                    {
                        var attributedDate = p.PlanDay.Date;
                        return attributedDate >= startDate.Date && attributedDate <= endDate.Date;
                    })
                    .GroupBy(p => new { p.ShopID, p.ShopName })
                    .Select(g => new { g.Key.ShopID, g.Key.ShopName, Total = g.Sum(p => p.Total) })
                    .ToList();

                viewModel.ProductionByShop = productionByShop.Select(s => new ProductionByShopData
                {
                    ShopID = s.ShopID,
                    ShopName = s.ShopName,
                    TotalProduction = s.Total
                }).ToList();

                // Quality Summary
                // endDate — kun boshi (00:00); oxirgi kun tekshiruvlari ham kirishi uchun < endDate+1
                var qualityEndExclusive = endDate.Date.AddDays(1);
                var qualityInspections = await db.QUALITY_INSPECTION.AsNoTracking()
                    .Where(q => q.IsDeleted == false && q.InspectionDate >= startDate && q.InspectionDate < qualityEndExclusive)
                    .Select(q => new
                    {
                        q.StatusCode,
                        q.QtyAccepted,
                        q.QtyRejected,
                        q.DefectTypeID,
                        DefectName = q.DEFECT_TYPES.DefectType
                    })
                    .ToListAsync();

                viewModel.TotalInspections = qualityInspections.Count;
                viewModel.ApprovedInspections = qualityInspections.Count(q => q.StatusCode == "APPROVED");
                // NOTE: amalda ko'p holatda tekshiruv APPROVED bo'lib turib ham QtyRejected > 0 (qisman brak) bo'lishi mumkin.
                // Shuning uchun "RejectedInspections" ni brak chiqqan tekshiruvlar soni sifatida hisoblaymiz.
                viewModel.RejectedInspections = qualityInspections.Count(q => q.QtyRejected > 0 || q.StatusCode == "REJECTED");
                viewModel.TotalAcceptedQty = (double)(qualityInspections.Sum(q => (decimal?)q.QtyAccepted) ?? 0);
                viewModel.TotalRejectedQty = (double)(qualityInspections.Sum(q => (decimal?)q.QtyRejected) ?? 0);
                var totalInspectedQty = viewModel.TotalAcceptedQty + viewModel.TotalRejectedQty;
                viewModel.FTQPercent = totalInspectedQty > 0
                    ? (double?)Math.Round((viewModel.TotalAcceptedQty / totalInspectedQty) * 100, 1)
                    : null;

                viewModel.TopDefects = qualityInspections
                    .Where(q => q.QtyRejected > 0 && q.DefectTypeID.HasValue)
                    .GroupBy(q => new
                    {
                        q.DefectTypeID,
                        q.DefectName
                    })
                    .Select(g => new TopDefectData
                    {
                        DefectTypeID = g.Key.DefectTypeID,
                        DefectName = string.IsNullOrWhiteSpace(g.Key.DefectName) ? ("Nuqson #" + g.Key.DefectTypeID) : g.Key.DefectName,
                        RejectedQty = (double)g.Sum(x => x.QtyRejected),
                        InspectionCount = g.Count()
                    })
                    .OrderByDescending(x => x.RejectedQty)
                    .ThenByDescending(x => x.InspectionCount)
                    .Take(3)
                    .ToList();

                // Order Bottlenecks (remaining qty ga fulfill).
                // Avval har buyurtma qatoriga 4 ta so'rov ketardi (N+1); endi 4 ta guruhlangan so'rov.
                var orderLines = await db.S_ORDER_PRODUCTS.AsNoTracking()
                    .Where(op => op.S_ORDERS.IsDeleted == false)
                    .Select(op => new
                    {
                        op.OrderID,
                        op.S_ORDERS.OrderNo,
                        op.ProductID,
                        PNo = op.PRODUCT.PNo,
                        op.Amount
                    })
                    .ToListAsync();

                var invoicedByOrderProduct = (await db.SALES_INVOICE_LINES.AsNoTracking()
                        .Where(l => l.SALES_INVOICES.OrderID.HasValue)
                        .GroupBy(l => new { OrderID = l.SALES_INVOICES.OrderID.Value, l.ProductID })
                        .Select(g => new { g.Key.OrderID, g.Key.ProductID, Qty = g.Sum(x => x.Qty) })
                        .ToListAsync())
                    .ToDictionary(x => Tuple.Create(x.OrderID, x.ProductID), x => (double)x.Qty);

                var openLines = orderLines
                    .Select(l =>
                    {
                        double invoiced;
                        invoicedByOrderProduct.TryGetValue(Tuple.Create(l.OrderID, l.ProductID), out invoiced);
                        return new { Line = l, InvoicedQty = invoiced, RemainingQty = l.Amount - invoiced };
                    })
                    .Where(x => x.RemainingQty > 0)
                    .ToList();

                var openProductIds = openLines.Select(x => x.Line.ProductID).Distinct().ToList();

                var producedByProduct = openProductIds.Count == 0
                    ? new Dictionary<int, double>()
                    : (await db.PRODUCT_PRODUCTION.AsNoTracking()
                            .Where(p => openProductIds.Contains(p.ProductID) && (p.IsDeleted == false || p.IsDeleted == null))
                            .GroupBy(p => p.ProductID)
                            .Select(g => new { ProductID = g.Key, Qty = g.Sum(x => x.Total) })
                            .ToListAsync())
                        .ToDictionary(x => x.ProductID, x => x.Qty);

                var inspectedByProduct = openProductIds.Count == 0
                    ? new Dictionary<int, double>()
                    : (await db.QUALITY_INSPECTION.AsNoTracking()
                            .Where(q => openProductIds.Contains(q.ProductID) && q.StatusCode == "APPROVED" && q.IsDeleted == false)
                            .GroupBy(q => q.ProductID)
                            .Select(g => new { ProductID = g.Key, Qty = g.Sum(x => x.QtyAccepted) })
                            .ToListAsync())
                        .ToDictionary(x => x.ProductID, x => (double)x.Qty);

                var bottlenecks = new List<OrderBottleneck>();

                foreach (var item in openLines)
                {
                    var line = item.Line;
                    double producedQty;
                    producedByProduct.TryGetValue(line.ProductID, out producedQty);
                    double inspectedQty;
                    inspectedByProduct.TryGetValue(line.ProductID, out inspectedQty);
                    var remainingQty = item.RemainingQty;

                    var needToProduce = Math.Max(0, remainingQty - producedQty);
                    var needToInspect = Math.Max(0, remainingQty - inspectedQty);
                    string note;
                    if (needToProduce > 0)
                    {
                        note = $"Ishlab chiqarish kerak: {needToProduce:N3}";
                    }
                    else if (needToInspect > 0)
                    {
                        note = $"Sifat nazoratdan o'tkazish kerak: {needToInspect:N3}";
                    }
                    else
                    {
                        note = $"Sotuv hujjatini qilish / jo'natish mumkin: {remainingQty:N3}";
                    }

                    bottlenecks.Add(new OrderBottleneck
                    {
                        OrderID = line.OrderID,
                        OrderNo = line.OrderNo,
                        ProductID = line.ProductID,
                        ProductNo = line.PNo ?? "",
                        OrderedQty = line.Amount,
                        ProducedQty = producedQty,
                        InspectedQty = inspectedQty,
                        InvoicedQty = item.InvoicedQty,
                        RemainingQty = remainingQty,
                        Note = note
                    });
                }

                viewModel.OrderBottlenecks = bottlenecks
                    .OrderByDescending(b => b.RemainingQty)
                    .Take(10)
                    .ToList();

                ViewBag.SelectedYear = selectedYear;
                ViewBag.SelectedMonth = selectedMonth;
                ViewBag.SelectedDay = selectedDay;
                ViewBag.PrimaryLabel = primaryLabel;
                ViewBag.SecondaryLabel = secondaryLabel;
                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;

                return View(viewModel);
            }
        }

        private sealed class ProductionFact
        {
            public int ID { get; set; }
            public int ShopID { get; set; }
            public string ShopName { get; set; }
            public DateTime PlanDay { get; set; }
            public double Total { get; set; }
        }
    }
}
