using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers.MES
{
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
                var loadStart = effectiveStart.AddMonths(-6);
                var loadEnd = effectiveEnd.AddMonths(6);

                var linkedProductionIds = await (
                    from qi in db.QUALITY_INSPECTION
                    join pd in db.PRODUCTPLANS_DAILY on qi.ProductPlanDailyID equals pd.ID
                    where qi.IsDeleted == false
                          && qi.ProductionID.HasValue
                          && pd.Day >= effectiveStart
                          && pd.Day <= effectiveEnd
                    select qi.ProductionID.Value
                ).Distinct().ToListAsync();

                var productionCandidates = await db.PRODUCT_PRODUCTION
                    .Include(p => p.SHOP)
                    .Where(p => (p.IsDeleted == false || p.IsDeleted == null)
                        && ((p.Date >= loadStart && p.Date <= loadEnd)
                            || linkedProductionIds.Contains(p.ID)))
                    .ToListAsync();

                var attributionDateByProductionId = await BuildAttributionDateMapAsync(
                    db,
                    productionCandidates.Select(p => p.ID).ToList());

                var periodActual = productionCandidates
                    .Where(p =>
                    {
                        DateTime attributedDate;
                        if (!attributionDateByProductionId.TryGetValue(p.ID, out attributedDate))
                        {
                            attributedDate = p.Date.Date;
                        }
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
                        DateTime attributedDate;
                        if (!attributionDateByProductionId.TryGetValue(p.ID, out attributedDate))
                        {
                            attributedDate = p.Date.Date;
                        }
                        return attributedDate >= secondaryStart.Date && attributedDate <= secondaryEnd.Date;
                    })
                    .Sum(p => (double?)p.Total) ?? 0;

                viewModel.MonthPlanned = secondaryPlanned;
                viewModel.MonthActual = secondaryActual;
                viewModel.MonthPlanVsActualPercent = secondaryPlanned > 0 ? (secondaryActual / secondaryPlanned) * 100 : 0;

                // Daily Plan vs Actual Chart (selected period)
                var dailyPlans = await db.PRODUCTPLANS_DAILY
                    .Where(d => d.Day >= chartStartDate && d.Day <= chartEndDate && d.PRODUCTPLAN.IsDeleted == false)
                    .ToListAsync();

                var dailyActuals = productionCandidates
                    .Where(p =>
                    {
                        DateTime attributedDate;
                        if (!attributionDateByProductionId.TryGetValue(p.ID, out attributedDate))
                        {
                            attributedDate = p.Date.Date;
                        }
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
                        DateTime attributedDate;
                        if (!attributionDateByProductionId.TryGetValue(p.ID, out attributedDate))
                        {
                            attributedDate = p.Date.Date;
                        }
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
                        DateTime attributedDate;
                        if (!attributionDateByProductionId.TryGetValue(p.ID, out attributedDate))
                        {
                            attributedDate = p.Date.Date;
                        }
                        return attributedDate >= startDate.Date && attributedDate <= endDate.Date;
                    })
                    .GroupBy(p => new { p.ShopID, ShopName = p.SHOP != null ? p.SHOP.ShopName : null })
                    .Select(g => new { g.Key.ShopID, g.Key.ShopName, Total = g.Sum(p => p.Total) })
                    .ToList();

                viewModel.ProductionByShop = productionByShop.Select(s => new ProductionByShopData
                {
                    ShopID = s.ShopID,
                    ShopName = s.ShopName,
                    TotalProduction = s.Total
                }).ToList();

                // Quality Summary
                var qualityInspections = await db.QUALITY_INSPECTION
                    .Include(q => q.DEFECT_TYPES)
                    .Where(q => q.IsDeleted == false && q.InspectionDate >= startDate && q.InspectionDate <= endDate)
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
                        DefectName = (q.DEFECT_TYPES != null ? q.DEFECT_TYPES.DefectType : null)
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

                // Order Bottlenecks (remaining qty ga fulfill)
                var orders = await db.S_ORDERS
                    .Include(o => o.S_ORDER_PRODUCTS)
                    .Where(o => o.IsDeleted == false)
                    .ToListAsync();

                var bottlenecks = new List<OrderBottleneck>();

                foreach (var order in orders)
                {
                    foreach (var orderProduct in order.S_ORDER_PRODUCTS)
                    {
                        var producedQty = await db.PRODUCT_PRODUCTION
                            .Where(p => p.ProductID == orderProduct.ProductID
                                && (p.IsDeleted == false || p.IsDeleted == null))
                            .SumAsync(p => (double?)p.Total) ?? 0;

                        var inspectedQty = await db.QUALITY_INSPECTION
                            .Where(q => q.ProductID == orderProduct.ProductID 
                                && q.StatusCode == "APPROVED" 
                                && q.IsDeleted == false)
                            .SumAsync(q => (double?)q.QtyAccepted) ?? 0;

                        var invoicedQty = await db.SALES_INVOICE_LINES
                            .Where(l => l.ProductID == orderProduct.ProductID 
                                && l.SALES_INVOICES.OrderID == order.ID)
                            .SumAsync(l => (double?)l.Qty) ?? 0;

                        var remainingQty = (double)orderProduct.Amount - invoicedQty;

                        if (remainingQty > 0)
                        {
                            var product = await db.PRODUCTS.FindAsync(orderProduct.ProductID);
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
                                OrderID = order.ID,
                                OrderNo = order.OrderNo,
                                ProductID = orderProduct.ProductID,
                                ProductNo = product?.PNo ?? "",
                                OrderedQty = (double)orderProduct.Amount,
                                ProducedQty = producedQty,
                                InspectedQty = inspectedQty,
                                InvoicedQty = invoicedQty,
                                RemainingQty = remainingQty,
                                Note = note
                            });
                        }
                    }
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

        private static async Task<Dictionary<int, DateTime>> BuildAttributionDateMapAsync(DBTHSNEntities db, List<int> productionIds)
        {
            if (productionIds == null || productionIds.Count == 0)
            {
                return new Dictionary<int, DateTime>();
            }

            var links = await (
                from qi in db.QUALITY_INSPECTION
                join pd in db.PRODUCTPLANS_DAILY on qi.ProductPlanDailyID equals pd.ID
                where qi.IsDeleted == false
                      && qi.ProductionID.HasValue
                      && productionIds.Contains(qi.ProductionID.Value)
                select new
                {
                    ProductionID = qi.ProductionID.Value,
                    PlanDay = DbFunctions.TruncateTime(pd.Day),
                    Qty = (double?)qi.QtyInspected ?? 0
                }
            ).ToListAsync();

            return links
                .Where(x => x.PlanDay.HasValue)
                .GroupBy(x => x.ProductionID)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(x => x.PlanDay.Value)
                          .OrderByDescending(dayGroup => dayGroup.Sum(v => v.Qty))
                          .ThenByDescending(dayGroup => dayGroup.Key)
                          .Select(dayGroup => dayGroup.Key)
                          .FirstOrDefault());
        }
    }
}

