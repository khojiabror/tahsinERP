using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Services
{
    public static class LogisticsMonthlyReportService
    {
        public static async Task<LogisticsMonthlyReportViewModel> BuildReportAsync(
            DBTHSNEntities db,
            int year,
            int month,
            int? forwarderId)
        {
            var periodStart = new DateTime(year, month, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);

            var waybills = await db.F_WAYBILLS
                .Where(w => w.IsDeleted == false && w.ForwarderID.HasValue)
                .Select(w => new
                {
                    w.ID,
                    w.ForwarderID,
                    Amount = w.Amount ?? 0,
                    w.IssueDate
                })
                .ToListAsync();

            var payments = await db.F_WAYBILL_PAYMENTS
                .Where(p => p.IsDeleted == false)
                .Select(p => new
                {
                    p.WaybillID,
                    p.ForwarderID,
                    Amount = p.Payment_Amount,
                    p.PaymentDate
                })
                .ToListAsync();

            var finPayments = await db.FIN_TRANSACTIONS
                .Where(t => t.CounterpartyType == 4
                    && t.Direction == 2
                    && (t.IsDeleted == null || t.IsDeleted == false)
                    && (t.SourceDocType == null || t.SourceDocType != "WaybillPayment")
                    && t.CounterpartyID.HasValue)
                .Select(t => new
                {
                    ForwarderID = t.CounterpartyID.Value,
                    Amount = (double)t.Amount,
                    PaymentDate = (DateTime?)t.TxnDate
                })
                .ToListAsync();

            var filteredWaybills = waybills
                .Where(w => !forwarderId.HasValue || w.ForwarderID == forwarderId.Value)
                .ToList();

            var waybillIdSet = new HashSet<int>(filteredWaybills.Select(w => w.ID));
            var filteredPayments = payments
                .Where(p =>
                    waybillIdSet.Contains(p.WaybillID) &&
                    (!forwarderId.HasValue || p.ForwarderID == forwarderId.Value))
                .ToList();

            var forwarderIds = filteredWaybills.Select(w => w.ForwarderID.Value).Distinct().ToList();
            var forwarderNames = await db.FORWARDERS
                .Where(f => f.IsDeleted == false && forwarderIds.Contains(f.ID))
                .Select(f => new { f.ID, f.ForwarderName })
                .ToListAsync();
            var nameMap = forwarderNames.ToDictionary(x => x.ID, x => x.ForwarderName ?? "Noma'lum");

            var rows = new List<LogisticsMonthlyReportRowViewModel>();
            foreach (var fid in forwarderIds.OrderBy(id => nameMap.ContainsKey(id) ? nameMap[id] : id.ToString()))
            {
                var wbForForwarder = filteredWaybills.Where(w => w.ForwarderID == fid).ToList();
                var payForForwarder = filteredPayments.Where(p => p.ForwarderID == fid).ToList();
                var finPayForForwarder = finPayments.Where(p => p.ForwarderID == fid).ToList();

                var openingFreight = wbForForwarder
                    .Where(w => w.IssueDate.HasValue && w.IssueDate.Value < periodStart)
                    .Sum(w => w.Amount);
                var openingPayment = payForForwarder
                    .Where(p => p.PaymentDate.HasValue && p.PaymentDate.Value < periodStart)
                    .Sum(p => p.Amount)
                    + finPayForForwarder
                    .Where(p => p.PaymentDate.HasValue && p.PaymentDate.Value < periodStart)
                    .Sum(p => p.Amount);
                var openingDebt = openingFreight - openingPayment;

                var freightInMonth = wbForForwarder
                    .Where(w => w.IssueDate.HasValue && w.IssueDate.Value >= periodStart && w.IssueDate.Value <= periodEnd)
                    .Sum(w => w.Amount);
                var tripsInMonth = wbForForwarder.Count(w =>
                    w.IssueDate.HasValue && w.IssueDate.Value >= periodStart && w.IssueDate.Value <= periodEnd);

                var paymentInMonth = payForForwarder
                    .Where(p => p.PaymentDate.HasValue && p.PaymentDate.Value >= periodStart && p.PaymentDate.Value <= periodEnd)
                    .Sum(p => p.Amount)
                    + finPayForForwarder
                    .Where(p => p.PaymentDate.HasValue && p.PaymentDate.Value >= periodStart && p.PaymentDate.Value <= periodEnd)
                    .Sum(p => p.Amount);

                rows.Add(new LogisticsMonthlyReportRowViewModel
                {
                    ForwarderId = fid,
                    ForwarderName = nameMap.ContainsKey(fid) ? nameMap[fid] : $"Forwarder #{fid}",
                    TripsInMonth = tripsInMonth,
                    OpeningDebt = openingDebt,
                    FreightInMonth = freightInMonth,
                    PaymentInMonth = paymentInMonth,
                    ClosingDebt = openingDebt + freightInMonth - paymentInMonth
                });
            }

            var allForwarders = await db.FORWARDERS
                .Where(f => f.IsDeleted == false)
                .OrderBy(f => f.ForwarderName)
                .Select(f => new SelectListItem
                {
                    Value = f.ID.ToString(),
                    Text = f.ForwarderName,
                    Selected = forwarderId.HasValue && f.ID == forwarderId.Value
                })
                .ToListAsync();

            var companyName = await db.COMPANIES.Select(c => c.Name).FirstOrDefaultAsync();
            var openingBalanceAdjustment = await GetLogisticsOpeningBalanceAdjustmentAsync(db, periodStart);

            var totalOpeningDebt = rows.Sum(r => r.OpeningDebt) + openingBalanceAdjustment;
            var totalFreight = rows.Sum(r => r.FreightInMonth);
            var totalPayment = rows.Sum(r => r.PaymentInMonth);
            var totalClosingDebt = totalOpeningDebt + totalFreight - totalPayment;

            return new LogisticsMonthlyReportViewModel
            {
                Year = year,
                Month = month,
                ForwarderId = forwarderId,
                ReportDate = DateTime.Now.ToString("dd.MM.yyyy"),
                CompanyName = companyName ?? "",
                Rows = rows,
                Totals = new LogisticsMonthlyReportTotalsViewModel
                {
                    TotalTrips = rows.Sum(r => r.TripsInMonth),
                    TotalOpeningDebt = totalOpeningDebt,
                    TotalFreight = totalFreight,
                    TotalPayment = totalPayment,
                    TotalClosingDebt = totalClosingDebt
                },
                Forwarders = allForwarders
            };
        }

        private static async Task<double> GetLogisticsOpeningBalanceAdjustmentAsync(DBTHSNEntities db, DateTime periodStart)
        {
            // OpeningBalance modulidan kiritilgan logistika bilan bog'liq balanslar
            // (nomida "logistik", "tashuv", "transport", "forwarder" kalit so'zlari bor kategoriyalar).
            var keywordFilteredCategoryIds = await db.FIN_BALANCE_CATEGORIES
                .Where(c => c.IsActive && c.IsDeleted != true &&
                            (((c.Name ?? "").ToLower().Contains("logistik")) ||
                             ((c.Name ?? "").ToLower().Contains("tashuv")) ||
                             ((c.Name ?? "").ToLower().Contains("transport")) ||
                             ((c.Name ?? "").ToLower().Contains("forwarder"))))
                .Select(c => c.ID)
                .ToListAsync();

            if (!keywordFilteredCategoryIds.Any())
            {
                return 0;
            }

            var adjustment = await db.FIN_BALANCE_SNAPSHOTS
                .Where(s => keywordFilteredCategoryIds.Contains(s.CategoryID) &&
                            s.SourceType == "OpeningBalance" &&
                            s.SnapshotDate < periodStart)
                .Select(s => (double?)s.Amount)
                .DefaultIfEmpty(0)
                .SumAsync();

            return adjustment ?? 0;
        }
    }
}
