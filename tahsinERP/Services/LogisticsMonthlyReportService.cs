using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.ViewModels;

namespace tahsinERP.Services
{
    public static class LogisticsMonthlyReportService
    {
        public static async Task<LogisticsMonthlyReportViewModel> BuildReportAsync(
            DBTHSNEntities db,
            int year,
            int month,
            int? forwarderId,
            LogisticsMoney money = null)
        {
            var periodStart = new DateTime(year, month, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);

            // LOG-P05: davr kesimida SQL'da guruhlangan 3 ta so'rov (avval butun tarix xotiraga yuklanardi).
            // LOG-B04: qarz ta'rifi WaybillLedgerService'da — boshqa ekranlar bilan bir xil.
            // LOG-B13: summalar so'mda (har hujjat o'z sanasidagi CBU kursi bilan).
            var balances = WaybillLedgerService.GetForwarderBalances(db, periodStart, periodEnd, forwarderId, money);

            var forwarderIds = balances.Keys.ToList();
            var nameMap = await db.FORWARDERS.AsNoTracking()
                .Where(f => forwarderIds.Contains(f.ID))
                .Select(f => new { f.ID, f.ForwarderName })
                .ToDictionaryAsync(x => x.ID, x => x.ForwarderName ?? "Noma'lum");

            var rows = balances.Values
                .Where(b => Math.Abs(b.OpeningDebt) > 0.005m || b.FreightInPeriod != 0 || b.PaidInPeriod != 0 || b.TripsInPeriod > 0)
                .Select(b => new LogisticsMonthlyReportRowViewModel
                {
                    ForwarderId = b.ForwarderID,
                    ForwarderName = nameMap.ContainsKey(b.ForwarderID) ? nameMap[b.ForwarderID] : $"Forwarder #{b.ForwarderID}",
                    TripsInMonth = b.TripsInPeriod,
                    OpeningDebt = b.OpeningDebt,
                    FreightInMonth = b.FreightInPeriod,
                    PaymentInMonth = b.PaidInPeriod,
                    ClosingDebt = b.ClosingDebt
                })
                .OrderBy(r => r.ForwarderName)
                .ToList();

            var allForwarders = ReferenceDataCache.Get(ReferenceDataCache.Forwarders)
                .Select(f => new SelectListItem
                {
                    Value = f.ID.ToString(),
                    Text = f.Name,
                    Selected = forwarderId.HasValue && f.ID == forwarderId.Value
                })
                .ToList();

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

        private static async Task<decimal> GetLogisticsOpeningBalanceAdjustmentAsync(DBTHSNEntities db, DateTime periodStart)
        {
            // OpeningBalance modulidan kiritilgan logistika qarzi — balans kategoriyasi SystemCode = LOGISTICS_DEBT
            // (avval nomida "logistik/tashuv/transport/forwarder" bor kategoriyalar; nom faqat ko'rsatish uchun).
            const string logisticsDebt = SystemCodes.BalanceCategory.LogisticsDebt;
            var logisticsCategoryIds = await db.FIN_BALANCE_CATEGORIES
                .Where(c => c.IsActive && c.IsDeleted != true && c.SystemCode == logisticsDebt)
                .Select(c => c.ID)
                .ToListAsync();

            if (!logisticsCategoryIds.Any())
            {
                return 0;
            }

            var adjustment = await db.FIN_BALANCE_SNAPSHOTS
                .Where(s => logisticsCategoryIds.Contains(s.CategoryID) &&
                            s.SourceType == "OpeningBalance" &&
                            s.SnapshotDate < periodStart)
                .Select(s => (decimal?)s.Amount)
                .DefaultIfEmpty(0)
                .SumAsync();

            return adjustment ?? 0;
        }
    }
}
