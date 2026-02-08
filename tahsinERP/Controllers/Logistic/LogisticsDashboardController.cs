using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Logistics Dashboard - Analytics, KPIs, and Reports
    /// </summary>
    public class LogisticsDashboardController : Controller
    {
        // GET: LogisticsDashboard
        public async Task<ActionResult> Index(DateTime? startDate, DateTime? endDate, int? forwarderId, byte? direction)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Default to current month if not specified
            if (!startDate.HasValue)
                startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!endDate.HasValue)
                endDate = DateTime.Now;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.F_WAYBILLS
                    .Include(w => w.FORWARDER)
                    .Include(w => w.CURRENCy)
                    .Where(w => w.IsDeleted == false && 
                        w.IssueDate >= startDate.Value && 
                        w.IssueDate <= endDate.Value);

                if (forwarderId.HasValue)
                {
                    query = query.Where(w => w.ForwarderID == forwarderId.Value);
                }

                if (direction.HasValue)
                {
                    query = query.Where(w => w.Direction == direction.Value);
                }

                var waybills = await query.ToListAsync();

                // Calculate KPIs
                var totalCost = waybills.Where(w => w.Amount.HasValue).Sum(w => w.Amount.Value);
                var totalTonnage = waybills.Where(w => w.GrWeight.HasValue).Sum(w => w.GrWeight.Value);
                var totalCBM = waybills.Where(w => w.CBM.HasValue).Sum(w => w.CBM.Value);
                var totalDistance = waybills.Where(w => w.DistanceKm.HasValue).Sum(w => w.DistanceKm.Value);
                var totalTrips = waybills.Count;

                var costPerTon = totalTonnage > 0 ? totalCost / totalTonnage : 0;
                var costPerKm = totalDistance > 0 ? totalCost / totalDistance : 0;
                var costPerTrip = totalTrips > 0 ? totalCost / totalTrips : 0;
                var costPerCBM = totalCBM > 0 ? totalCost / totalCBM : 0;

                // Calculate unpaid amount
                var waybillIds = waybills.Select(w => w.ID).ToList();
                var paymentTotals = await db.F_WAYBILL_PAYMENTS
                    .Where(p => waybillIds.Contains(p.WaybillID) && p.IsDeleted == false)
                    .GroupBy(p => p.WaybillID)
                    .Select(g => new { WaybillID = g.Key, TotalPaid = g.Sum(p => p.Payment_Amount) })
                    .ToListAsync();

                var paymentLookup = paymentTotals.ToDictionary(x => x.WaybillID, x => x.TotalPaid);
                var totalPaid = paymentTotals.Sum(p => p.TotalPaid);
                var unpaidAmount = totalCost - totalPaid;

                // Calculate logistics cost as % of sales (for outbound waybills)
                var outboundWaybills = waybills.Where(w => w.Direction == 1 && w.SalesInvoiceID.HasValue).ToList();
                var salesInvoiceIds = outboundWaybills.Select(w => w.SalesInvoiceID.Value).Distinct().ToList();
                var totalSalesValue = await db.SALES_INVOICES
                    .Where(i => salesInvoiceIds.Contains(i.ID))
                    .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;
                var logisticsCostPercent = totalSalesValue > 0 ? (double)(totalCost / (double)totalSalesValue * 100) : 0;

                // Direction breakdown
                var inboundCost = waybills.Where(w => w.Direction == 0 && w.Amount.HasValue).Sum(w => w.Amount.Value);
                var outboundCost = waybills.Where(w => w.Direction == 1 && w.Amount.HasValue).Sum(w => w.Amount.Value);
                var internalCost = waybills.Where(w => w.Direction == 2 && w.Amount.HasValue).Sum(w => w.Amount.Value);

                // Forwarder breakdown
                var forwarderStats = waybills
                    .Where(w => w.ForwarderID.HasValue && w.Amount.HasValue)
                    .GroupBy(w => w.ForwarderID)
                    .Select(g => new ForwarderDashboardStatViewModel
                    {
                        ForwarderID = g.Key,
                        ForwarderName = g.First().FORWARDER?.ForwarderName ?? "Unknown",
                        TripCount = g.Count(),
                        TotalCost = g.Sum(w => w.Amount.Value),
                        TotalTonnage = g.Where(w => w.GrWeight.HasValue).Sum(w => w.GrWeight.Value),
                        TotalPaid = g.Sum(w => paymentLookup.ContainsKey(w.ID) ? paymentLookup[w.ID] : 0)
                    })
                    .OrderByDescending(f => f.TotalCost)
                    .Take(10)
                    .ToList();

                // Lane performance
                var laneStats = waybills
                    .Where(w => !string.IsNullOrEmpty(w.FromLocation) && !string.IsNullOrEmpty(w.ToLocation) && w.Amount.HasValue)
                    .GroupBy(w => new { w.FromLocation, w.ToLocation })
                    .Select(g => new LaneDashboardStatViewModel
                    {
                        FromLocation = g.Key.FromLocation,
                        ToLocation = g.Key.ToLocation,
                        TripCount = g.Count(),
                        TotalCost = g.Sum(w => w.Amount.Value),
                        TotalTonnage = g.Where(w => w.GrWeight.HasValue).Sum(w => w.GrWeight.Value),
                        TotalDistance = g.Where(w => w.DistanceKm.HasValue).Sum(w => w.DistanceKm.Value),
                        AvgCostPerTon = g.Where(w => w.GrWeight.HasValue && w.GrWeight > 0).Any() ?
                            g.Where(w => w.GrWeight.HasValue && w.GrWeight > 0).Average(w => w.Amount.Value / w.GrWeight.Value) : 0,
                        AvgCostPerKm = g.Where(w => w.DistanceKm.HasValue && w.DistanceKm > 0).Any() ?
                            g.Where(w => w.DistanceKm.HasValue && w.DistanceKm > 0).Average(w => w.Amount.Value / w.DistanceKm.Value) : 0
                    })
                    .OrderByDescending(l => l.TripCount)
                    .Take(10)
                    .ToList();

                // Monthly trend
                var monthlyTrend = waybills
                    .Where(w => w.Amount.HasValue)
                    .GroupBy(w => new { Year = w.IssueDate.Value.Year, Month = w.IssueDate.Value.Month })
                    .Select(g => new
                    {
                        Period = $"{g.Key.Year}-{g.Key.Month:D2}",
                        TotalCost = g.Sum(w => w.Amount.Value),
                        TripCount = g.Count()
                    })
                    .OrderBy(m => m.Period)
                    .ToList();

                ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
                ViewBag.Forwarders = new SelectList(await db.FORWARDERS.Where(f => f.IsDeleted == false).ToListAsync(), "ID", "ForwarderName", forwarderId);
                ViewBag.Directions = new SelectList(new[]
                {
                    new { Value = (byte?)0, Text = "Kiruvchi (Inbound)" },
                    new { Value = (byte?)1, Text = "Chiquvchi (Outbound)" },
                    new { Value = (byte?)2, Text = "Ichki (Internal)" }
                }, "Value", "Text", direction);

                ViewBag.TotalCost = totalCost;
                ViewBag.TotalTonnage = totalTonnage;
                ViewBag.TotalCBM = totalCBM;
                ViewBag.TotalDistance = totalDistance;
                ViewBag.TotalTrips = totalTrips;
                ViewBag.CostPerTon = costPerTon;
                ViewBag.CostPerKm = costPerKm;
                ViewBag.CostPerTrip = costPerTrip;
                ViewBag.CostPerCBM = costPerCBM;
                ViewBag.UnpaidAmount = unpaidAmount;
                ViewBag.LogisticsCostPercent = logisticsCostPercent;
                ViewBag.InboundCost = inboundCost;
                ViewBag.OutboundCost = outboundCost;
                ViewBag.InternalCost = internalCost;
                ViewBag.ForwarderStats = forwarderStats;
                ViewBag.LaneStats = laneStats;
                ViewBag.MonthlyTrend = monthlyTrend;

                return View();
            }
        }

        /// <summary>
        /// Lane Performance Report
        /// </summary>
        public async Task<ActionResult> LanePerformance(DateTime? startDate, DateTime? endDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (!startDate.HasValue)
                startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!endDate.HasValue)
                endDate = DateTime.Now;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var waybills = await db.F_WAYBILLS
                    .Include(w => w.FORWARDER)
                    .Where(w => w.IsDeleted == false &&
                        w.IssueDate >= startDate.Value &&
                        w.IssueDate <= endDate.Value &&
                        !string.IsNullOrEmpty(w.FromLocation) &&
                        !string.IsNullOrEmpty(w.ToLocation))
                    .ToListAsync();

                var lanePerformance = waybills
                    .GroupBy(w => new { w.FromLocation, w.ToLocation })
                    .Select(g => new LanePerformanceViewModel
                    {
                        FromLocation = g.Key.FromLocation,
                        ToLocation = g.Key.ToLocation,
                        TripCount = g.Count(),
                        TotalTonnage = g.Where(w => w.GrWeight.HasValue).Sum(w => w.GrWeight.Value),
                        TotalCBM = g.Where(w => w.CBM.HasValue).Sum(w => w.CBM.Value),
                        TotalCost = g.Where(w => w.Amount.HasValue).Sum(w => w.Amount.Value),
                        TotalDistance = g.Where(w => w.DistanceKm.HasValue).Sum(w => w.DistanceKm.Value),
                        AvgCostPerTon = g.Where(w => w.GrWeight.HasValue && w.GrWeight > 0 && w.Amount.HasValue).Any() ?
                            g.Where(w => w.GrWeight.HasValue && w.GrWeight > 0 && w.Amount.HasValue)
                            .Average(w => w.Amount.Value / w.GrWeight.Value) : 0,
                        AvgCostPerKm = g.Where(w => w.DistanceKm.HasValue && w.DistanceKm > 0 && w.Amount.HasValue).Any() ?
                            g.Where(w => w.DistanceKm.HasValue && w.DistanceKm > 0 && w.Amount.HasValue)
                            .Average(w => w.Amount.Value / w.DistanceKm.Value) : 0,
                        MainForwarder = g.Where(w => w.ForwarderID.HasValue)
                            .GroupBy(w => w.ForwarderID)
                            .OrderByDescending(fg => fg.Count())
                            .FirstOrDefault()?.First().FORWARDER?.ForwarderName ?? "Unknown"
                    })
                    .OrderByDescending(l => l.TripCount)
                    .ToList();

                ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
                ViewBag.LanePerformance = lanePerformance;

                return View();
            }
        }

        /// <summary>
        /// Reconciliation Report - Waybills without invoices or payments
        /// </summary>
        public async Task<ActionResult> Reconciliation()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Waybills without invoices
                var waybillsWithoutInvoices = await db.F_WAYBILLS
                    .Include(w => w.FORWARDER)
                    .Include(w => w.F_CONTRACTS.FORWARDER)
                    .Where(w => w.IsDeleted == false &&
                        !w.InvoiceID.HasValue &&
                        !w.SalesInvoiceID.HasValue)
                    .ToListAsync();

                // Waybills without payments
                var allWaybills = await db.F_WAYBILLS
                    .Include(w => w.FORWARDER)
                    .Include(w => w.F_CONTRACTS.FORWARDER)
                    .Where(w => w.IsDeleted == false && w.Amount.HasValue)
                    .ToListAsync();

                var waybillIds = allWaybills.Select(w => w.ID).ToList();
                var paidWaybillIds = await db.F_WAYBILL_PAYMENTS
                    .Where(p => waybillIds.Contains(p.WaybillID) && p.IsDeleted == false)
                    .Select(p => p.WaybillID)
                    .Distinct()
                    .ToListAsync();

                var waybillsWithoutPayments = allWaybills
                    .Where(w => !paidWaybillIds.Contains(w.ID))
                    .ToList();

                // Invoices with missing freight cost
                var salesInvoices = await db.SALES_INVOICES
                    .Include(i => i.CUSTOMER)
                    .Where(i => !db.F_WAYBILLS.Any(w => w.SalesInvoiceID == i.ID && w.IsDeleted == false))
                    .ToListAsync();

                ViewBag.WaybillsWithoutInvoices = waybillsWithoutInvoices;
                ViewBag.WaybillsWithoutPayments = waybillsWithoutPayments;
                ViewBag.SalesInvoicesWithoutFreight = salesInvoices;

                return View();
            }
        }

        /// <summary>
        /// Forwarder Performance Comparison
        /// </summary>
        public async Task<ActionResult> ForwarderComparison(DateTime? startDate, DateTime? endDate, string laneFrom, string laneTo)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (!startDate.HasValue)
                startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!endDate.HasValue)
                endDate = DateTime.Now;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.F_WAYBILLS
                    .Include(w => w.FORWARDER)
                    .Where(w => w.IsDeleted == false &&
                        w.IssueDate >= startDate.Value &&
                        w.IssueDate <= endDate.Value);

                if (!string.IsNullOrEmpty(laneFrom))
                {
                    query = query.Where(w => w.FromLocation == laneFrom);
                }

                if (!string.IsNullOrEmpty(laneTo))
                {
                    query = query.Where(w => w.ToLocation == laneTo);
                }

                var waybills = await query.ToListAsync();

                var forwarderComparison = waybills
                    .Where(w => w.ForwarderID.HasValue)
                    .GroupBy(w => w.ForwarderID)
                    .Select(g => new ForwarderComparisonViewModel
                    {
                        ForwarderID = g.Key,
                        ForwarderName = g.First().FORWARDER?.ForwarderName ?? "Unknown",
                        TripCount = g.Count(),
                        TotalCost = g.Where(w => w.Amount.HasValue).Sum(w => w.Amount.Value),
                        TotalTonnage = g.Where(w => w.GrWeight.HasValue).Sum(w => w.GrWeight.Value),
                        TotalDistance = g.Where(w => w.DistanceKm.HasValue).Sum(w => w.DistanceKm.Value),
                        AvgCostPerTon = g.Where(w => w.GrWeight.HasValue && w.GrWeight > 0 && w.Amount.HasValue).Any() ?
                            g.Where(w => w.GrWeight.HasValue && w.GrWeight > 0 && w.Amount.HasValue)
                            .Average(w => w.Amount.Value / w.GrWeight.Value) : 0,
                        AvgCostPerKm = g.Where(w => w.DistanceKm.HasValue && w.DistanceKm > 0 && w.Amount.HasValue).Any() ?
                            g.Where(w => w.DistanceKm.HasValue && w.DistanceKm > 0 && w.Amount.HasValue)
                            .Average(w => w.Amount.Value / w.DistanceKm.Value) : 0,
                        OnTimeDeliveries = g.Count(w => w.PlannedDeliveryDate.HasValue && w.ActualDeliveryDate.HasValue && w.ActualDeliveryDate <= w.PlannedDeliveryDate),
                        TotalDeliveries = g.Count(w => w.PlannedDeliveryDate.HasValue && w.ActualDeliveryDate.HasValue),
                        OTIFPercentage = g.Count(w => w.PlannedDeliveryDate.HasValue && w.ActualDeliveryDate.HasValue) > 0 ?
                            (double)g.Count(w => w.PlannedDeliveryDate.HasValue && w.ActualDeliveryDate.HasValue && w.ActualDeliveryDate <= w.PlannedDeliveryDate) /
                            g.Count(w => w.PlannedDeliveryDate.HasValue && w.ActualDeliveryDate.HasValue) * 100 : 0
                    })
                    .OrderByDescending(f => f.TripCount)
                    .ToList();

                ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
                ViewBag.LaneFrom = laneFrom;
                ViewBag.LaneTo = laneTo;
                ViewBag.ForwarderComparison = forwarderComparison;

                return View();
            }
        }
    }
}
