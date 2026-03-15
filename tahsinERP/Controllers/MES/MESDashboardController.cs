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

                var periodActual = await db.PRODUCT_PRODUCTION
                    .Where(p => p.Date >= startDate && p.Date <= endDate)
                    .SumAsync(p => (double?)p.Total) ?? 0;

                viewModel.TodayPlanned = periodPlanned;
                viewModel.TodayActual = periodActual;
                viewModel.TodayPlanVsActualPercent = periodPlanned > 0 ? (periodActual / periodPlanned) * 100 : 0;

                // Secondary period KPIs (month yoki year summary)
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

                var secondaryPlanned = await db.PRODUCTPLANS_DAILY
                    .Where(d => d.Day >= secondaryStart && d.Day <= secondaryEnd && d.PRODUCTPLAN.IsDeleted == false)
                    .SumAsync(d => (double?)d.Total) ?? 0;

                var secondaryActual = await db.PRODUCT_PRODUCTION
                    .Where(p => p.Date >= secondaryStart && p.Date <= secondaryEnd)
                    .SumAsync(p => (double?)p.Total) ?? 0;

                viewModel.MonthPlanned = secondaryPlanned;
                viewModel.MonthActual = secondaryActual;
                viewModel.MonthPlanVsActualPercent = secondaryPlanned > 0 ? (secondaryActual / secondaryPlanned) * 100 : 0;

                // Daily Plan vs Actual Chart (selected period)
                var chartStartDate = startDate;
                var chartEndDate = endDate;
                var dailyPlans = await db.PRODUCTPLANS_DAILY
                    .Where(d => d.Day >= chartStartDate && d.Day <= chartEndDate && d.PRODUCTPLAN.IsDeleted == false)
                    .ToListAsync();

                var dailyActuals = await db.PRODUCT_PRODUCTION
                    .Where(p => p.Date >= chartStartDate && p.Date <= chartEndDate)
                    .ToListAsync();

                // Group by date in memory (after ToListAsync)
                var dailyPlansGrouped = dailyPlans
                    .GroupBy(d => d.Day.Date)
                    .Select(g => new { Date = g.Key, Planned = g.Sum(d => d.Total) })
                    .ToList();

                var dailyActualsGrouped = dailyActuals
                    .GroupBy(p => p.Date.Date)
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

                // Production by Shop
                var productionByShop = await db.PRODUCT_PRODUCTION
                    .Where(p => p.Date >= startDate && p.Date <= endDate)
                    .GroupBy(p => new { p.ShopID, p.SHOP.ShopName })
                    .Select(g => new { g.Key.ShopID, g.Key.ShopName, Total = g.Sum(p => p.Total) })
                    .ToListAsync();

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
                            .Where(p => p.ProductID == orderProduct.ProductID)
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
    }
}

