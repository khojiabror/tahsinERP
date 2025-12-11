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
        // GET: MES/Dashboard
        public async Task<ActionResult> Dashboard()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var viewModel = new MESDashboardViewModel();
                var today = DateTime.Now.Date;
                var monthStart = new DateTime(today.Year, today.Month, 1);

                // Today's KPIs
                var todayPlanned = await db.PRODUCTPLANS_DAILY
                    .Where(d => DbFunctions.TruncateTime(d.Day) == today && d.PRODUCTPLAN.IsDeleted == false)
                    .SumAsync(d => (double?)d.Total) ?? 0;

                var todayActual = await db.PRODUCT_PRODUCTION
                    .Where(p => DbFunctions.TruncateTime(p.Date) == today)
                    .SumAsync(p => (double?)p.Total) ?? 0;

                viewModel.TodayPlanned = todayPlanned;
                viewModel.TodayActual = todayActual;
                viewModel.TodayPlanVsActualPercent = todayPlanned > 0 ? (todayActual / todayPlanned) * 100 : 0;

                // Month's KPIs
                var monthPlanned = await db.PRODUCTPLANS_DAILY
                    .Where(d => d.Day >= monthStart && d.Day <= today && d.PRODUCTPLAN.IsDeleted == false)
                    .SumAsync(d => (double?)d.Total) ?? 0;

                var monthActual = await db.PRODUCT_PRODUCTION
                    .Where(p => p.Date >= monthStart && p.Date <= today)
                    .SumAsync(p => (double?)p.Total) ?? 0;

                viewModel.MonthPlanned = monthPlanned;
                viewModel.MonthActual = monthActual;
                viewModel.MonthPlanVsActualPercent = monthPlanned > 0 ? (monthActual / monthPlanned) * 100 : 0;

                // Daily Plan vs Actual Chart (last 30 days)
                var chartStartDate = today.AddDays(-30);
                var dailyPlans = await db.PRODUCTPLANS_DAILY
                    .Where(d => d.Day >= chartStartDate && d.Day <= today && d.PRODUCTPLAN.IsDeleted == false)
                    .ToListAsync();

                var dailyActuals = await db.PRODUCT_PRODUCTION
                    .Where(p => p.Date >= chartStartDate && p.Date <= today)
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
                    .Where(p => p.Date >= monthStart && p.Date <= today)
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
                    .Where(q => q.IsDeleted == false && q.InspectionDate >= monthStart && q.InspectionDate <= today)
                    .ToListAsync();

                viewModel.TotalInspections = qualityInspections.Count;
                viewModel.ApprovedInspections = qualityInspections.Count(q => q.StatusCode == "APPROVED");
                viewModel.RejectedInspections = qualityInspections.Count(q => q.StatusCode == "REJECTED");
                viewModel.TotalAcceptedQty = (double)(qualityInspections.Sum(q => (decimal?)q.QtyAccepted) ?? 0);
                viewModel.TotalRejectedQty = (double)(qualityInspections.Sum(q => (decimal?)q.QtyRejected) ?? 0);

                // Order Bottlenecks (remaining qty to fulfill)
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
                                RemainingQty = remainingQty
                            });
                        }
                    }
                }

                viewModel.OrderBottlenecks = bottlenecks
                    .OrderByDescending(b => b.RemainingQty)
                    .Take(10)
                    .ToList();

                return View(viewModel);
            }
        }
    }
}

