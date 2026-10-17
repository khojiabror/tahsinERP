using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Logistika boshqaruv paneli va hisobotlari. Agregatsiya SQL'da — LogisticsAnalyticsService
    /// va WaybillLedgerService (LOG-P07, LOG-D04); controller faqat view model'ni shakllantiradi.
    /// </summary>
    [Permission]
    public class LogisticsDashboardController : BaseController
    {
        /// <summary>Solishtirish hisobotida har bo'limda ko'rsatiladigan maksimal qatorlar.</summary>
        private const int ReconciliationRowLimit = 500;

        public async Task<ActionResult> Index(DateTime? startDate, DateTime? endDate, int? forwarderId, byte? direction)
        {
            var from = startDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var to = endDate ?? DateTime.Today;
            var toExclusive = to.Date.AddDays(1);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = WaybillLedgerService.ActiveWaybills(db)
                    .Where(w => w.IssueDate >= from && w.IssueDate < toExclusive);
                if (forwarderId.HasValue)
                    query = query.Where(w => w.ForwarderID == forwarderId.Value);
                if (direction.HasValue)
                    query = query.Where(w => w.Direction == direction.Value);

                var totals = LogisticsAnalyticsService.GetTotals(query);

                // To'langan: split'lar bilan (LOG-B04) + davrdagi qo'lda kiritilgan FIN to'lovlar
                var paymentLookup = WaybillLedgerService.GetPaidLookup(db, query.Select(w => w.ID));
                var finQuery = WaybillLedgerService.ManualForwarderPayments(db)
                    .Where(t => t.TxnDate >= from && t.TxnDate < toExclusive);
                if (forwarderId.HasValue)
                    finQuery = finQuery.Where(t => t.CounterpartyID == forwarderId.Value);
                var finByForwarder = (await finQuery
                        .GroupBy(t => t.CounterpartyID.Value)
                        .Select(g => new { ForwarderID = g.Key, Total = g.Sum(t => t.Amount) })
                        .ToListAsync())
                    .ToDictionary(x => x.ForwarderID, x => (double)x.Total);
                var totalPaid = paymentLookup.Values.Sum() + finByForwarder.Values.Sum();

                // Logistika xarajati sotuvga nisbatan (chiquvchi, sotuv invoysi bog'langan)
                var totalSalesValue = await db.SALES_INVOICES
                    .Where(i => query.Any(w => w.Direction == 1 && w.SalesInvoiceID == i.ID))
                    .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;
                var outboundWithInvoiceCost = await query
                    .Where(w => w.Direction == 1 && w.SalesInvoiceID.HasValue)
                    .SumAsync(w => w.Amount) ?? 0;
                var logisticsCostPercent = totalSalesValue > 0 ? outboundWithInvoiceCost / (double)totalSalesValue * 100 : 0;

                // Yuk tashuvchilar (top 10) — to'langan summa yo'l varaqalari bo'yicha + FIN
                var forwarderStats = LogisticsAnalyticsService.GetForwarderStats(db, query, 10);
                var statIds = forwarderStats.Select(f => f.ForwarderID.Value).ToList();
                var waybillForwarder = await query
                    .Where(w => w.ForwarderID.HasValue && statIds.Contains(w.ForwarderID.Value))
                    .Select(w => new { w.ID, ForwarderID = w.ForwarderID.Value })
                    .ToListAsync();
                foreach (var stat in forwarderStats)
                {
                    var fid = stat.ForwarderID.Value;
                    double fin;
                    finByForwarder.TryGetValue(fid, out fin);
                    stat.TotalPaid = waybillForwarder.Where(w => w.ForwarderID == fid)
                        .Sum(w => WaybillLedgerService.PaidOf(paymentLookup, w.ID)) + fin;
                }
                forwarderStats = forwarderStats.OrderByDescending(f => f.TotalCost).ToList();

                ViewBag.StartDate = from.ToString("yyyy-MM-dd");
                ViewBag.EndDate = to.ToString("yyyy-MM-dd");
                ViewBag.Forwarders = ReferenceDataCache.SelectList(ReferenceDataCache.Forwarders, forwarderId);
                ViewBag.Directions = new SelectList(new[]
                {
                    new { Value = (byte?)0, Text = "Kiruvchi (Inbound)" },
                    new { Value = (byte?)1, Text = "Chiquvchi (Outbound)" },
                    new { Value = (byte?)2, Text = "Ichki (Internal)" }
                }, "Value", "Text", direction);

                ViewBag.CurrencyCodes = WaybillCurrencyCodes(query);
                ViewBag.TotalCost = totals.Cost;
                ViewBag.TotalTonnage = totals.Tonnage;
                ViewBag.TotalCBM = totals.Cbm;
                ViewBag.TotalDistance = totals.Distance;
                ViewBag.TotalTrips = totals.Trips;
                ViewBag.CostPerTon = totals.Tonnage > 0 ? totals.Cost / totals.Tonnage : 0;
                ViewBag.CostPerKm = totals.Distance > 0 ? totals.Cost / totals.Distance : 0;
                ViewBag.CostPerTrip = totals.Trips > 0 ? totals.Cost / totals.Trips : 0;
                ViewBag.CostPerCBM = totals.Cbm > 0 ? totals.Cost / totals.Cbm : 0;
                ViewBag.UnpaidAmount = totals.Cost - totalPaid;
                ViewBag.LogisticsCostPercent = logisticsCostPercent;
                ViewBag.InboundCost = totals.InboundCost;
                ViewBag.OutboundCost = totals.OutboundCost;
                ViewBag.InternalCost = totals.InternalCost;
                ViewBag.ForwarderStats = forwarderStats;
                ViewBag.LaneStats = LogisticsAnalyticsService.GetLaneStats(db, query, 10);

                return View();
            }
        }

        /// <summary>Logistika oylik hisoboti (yuk tashuvchilar bo'yicha).</summary>
        public async Task<ActionResult> MonthlyReport(int? year, int? month, int? forwarderId)
        {
            var y = year ?? DateTime.Today.Year;
            var m = Math.Min(Math.Max(month ?? DateTime.Today.Month, 1), 12);

            using (var db = new DBTHSNEntities())
            {
                var model = await LogisticsMonthlyReportService.BuildReportAsync(db, y, m, forwarderId);
                var periodStart = new DateTime(y, m, 1);
                var periodEnd = periodStart.AddMonths(1);
                ViewBag.CurrencyCodes = WaybillCurrencyCodes(WaybillLedgerService.ActiveWaybills(db)
                    .Where(w => w.IssueDate >= periodStart && w.IssueDate < periodEnd
                                && (!forwarderId.HasValue || w.ForwarderID == forwarderId.Value)));
                return View(model);
            }
        }

        public ActionResult LanePerformance(DateTime? startDate, DateTime? endDate)
        {
            var from = startDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var to = endDate ?? DateTime.Today;
            var toExclusive = to.Date.AddDays(1);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = WaybillLedgerService.ActiveWaybills(db)
                    .Where(w => w.IssueDate >= from && w.IssueDate < toExclusive);

                ViewBag.StartDate = from.ToString("yyyy-MM-dd");
                ViewBag.EndDate = to.ToString("yyyy-MM-dd");
                ViewBag.LanePerformance = LogisticsAnalyticsService.GetLaneStats(db, query, withMainForwarder: true);
                return View();
            }
        }

        /// <summary>
        /// Solishtirish: invoyssiz va to'lovsiz yo'l varaqalari, yuk tashish xarajati kiritilmagan sotuv invoyslari.
        /// LOG-P06: sana oralig'i (standart — joriy chorak), SQL'da NOT EXISTS, proyeksiya va qatorlar chegarasi.
        /// </summary>
        public async Task<ActionResult> Reconciliation(DateTime? startDate, DateTime? endDate)
        {
            var today = DateTime.Today;
            var from = startDate ?? new DateTime(today.Year, (today.Month - 1) / 3 * 3 + 1, 1);
            var to = endDate ?? today;
            var toExclusive = to.Date.AddDays(1);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var inPeriod = WaybillLedgerService.ActiveWaybills(db)
                    .Where(w => w.IssueDate >= from && w.IssueDate < toExclusive);

                var withoutInvoices = inPeriod.Where(w => !w.InvoiceID.HasValue && !w.SalesInvoiceID.HasValue);
                // To'lovsiz: split to'lovlar ham hisobga olinadi (LOG-B04)
                var withoutPayments = WaybillLedgerService.WithoutAnyPayment(db, inPeriod.Where(w => w.Amount.HasValue));
                var invoicesWithoutFreight = db.SALES_INVOICES
                    .Where(i => i.InvoiceDate >= from && i.InvoiceDate < toExclusive
                                && !db.F_WAYBILLS.Any(w => w.SalesInvoiceID == i.ID && w.IsDeleted != true));

                ViewBag.WaybillsWithoutInvoicesCount = await withoutInvoices.CountAsync();
                ViewBag.WaybillsWithoutPaymentsCount = await withoutPayments.CountAsync();
                ViewBag.SalesInvoicesWithoutFreightCount = await invoicesWithoutFreight.CountAsync();

                ViewBag.WaybillsWithoutInvoices = await ProjectWaybills(withoutInvoices);
                ViewBag.WaybillsWithoutPayments = await ProjectWaybills(withoutPayments);
                ViewBag.SalesInvoicesWithoutFreight = await invoicesWithoutFreight
                    .OrderByDescending(i => i.InvoiceDate)
                    .Take(ReconciliationRowLimit)
                    .Select(i => new LogisticsReconciliationRowViewModel
                    {
                        ID = i.ID,
                        DocumentNo = i.InvoiceNo,
                        PartyName = i.CUSTOMER.Name,
                        Date = i.InvoiceDate,
                        Amount = (double)i.TotalAmount,
                        Currency = i.CURRENCy.Shortname
                    })
                    .ToListAsync();

                ViewBag.RowLimit = ReconciliationRowLimit;
                ViewBag.StartDate = from.ToString("yyyy-MM-dd");
                ViewBag.EndDate = to.ToString("yyyy-MM-dd");
                return View();
            }
        }

        public ActionResult ForwarderComparison(DateTime? startDate, DateTime? endDate, string laneFrom, string laneTo)
        {
            var from = startDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var to = endDate ?? DateTime.Today;
            var toExclusive = to.Date.AddDays(1);

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = WaybillLedgerService.ActiveWaybills(db)
                    .Where(w => w.IssueDate >= from && w.IssueDate < toExclusive);
                if (!string.IsNullOrEmpty(laneFrom))
                    query = query.Where(w => w.FromLocation == laneFrom);
                if (!string.IsNullOrEmpty(laneTo))
                    query = query.Where(w => w.ToLocation == laneTo);

                ViewBag.StartDate = from.ToString("yyyy-MM-dd");
                ViewBag.EndDate = to.ToString("yyyy-MM-dd");
                ViewBag.LaneFrom = laneFrom;
                ViewBag.LaneTo = laneTo;
                ViewBag.ForwarderComparison = LogisticsAnalyticsService.GetForwarderStats(db, query);
                return View();
            }
        }

        private static Task<List<LogisticsReconciliationRowViewModel>> ProjectWaybills(IQueryable<F_WAYBILLS> waybills)
        {
            return waybills
                .OrderByDescending(w => w.IssueDate)
                .Take(ReconciliationRowLimit)
                .Select(w => new LogisticsReconciliationRowViewModel
                {
                    ID = w.ID,
                    DocumentNo = w.WaybillNo,
                    PartyName = w.FORWARDER != null ? w.FORWARDER.ForwarderName : w.F_CONTRACTS.FORWARDER.ForwarderName,
                    Date = w.IssueDate,
                    Amount = w.Amount,
                    Currency = w.CURRENCy.Shortname
                })
                .ToListAsync();
        }

        /// <summary>LOG-B13: yo'l varaqalari valyutalari (bir nechta bo'lsa view ogohlantiradi).</summary>
        private static List<string> WaybillCurrencyCodes(IQueryable<F_WAYBILLS> waybills)
        {
            return waybills.Select(w => w.CurrencyID).Distinct().ToList()
                .Select(id => id.HasValue
                    ? (ReferenceDataCache.NameOf(ReferenceDataCache.Currencies, id) ?? "#" + id)
                    : "valyutasiz")
                .OrderBy(x => x)
                .ToList();
        }
    }
}
