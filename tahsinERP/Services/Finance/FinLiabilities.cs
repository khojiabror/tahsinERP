using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services.Finance
{
    /// <summary>
    /// Balans va ReportsHub uchun majburiyat hisoblari (D6, D7, D8):
    /// oylik qarzi, doimiy harajatlar (FIN_RECURRING_OBLIGATIONS), logistika qarzi.
    /// </summary>
    public static class FinLiabilities
    {
        public const string RecurringObligationCategoryName = "Doimiy harajatlar qarzi";

        #region Oylik

        public sealed class PayrollLiabilityResult
        {
            public decimal Accrued { get; set; }
            public decimal Paid { get; set; }
            public IQueryable<FIN_TRANSACTIONS> PaymentQuery { get; set; }
        }

        /// <summary>Tasdiqlangan oylik (ApprovedDT ∈ (opening; asOf]) va oylik to'lovlari ([opening+1 kun; asOf]).</summary>
        public static PayrollLiabilityResult Payroll(DBTHSNEntities db, DateTime asOf, DateTime? openingDate)
        {
            var linesQuery = FinQueries.ApprovedPayrollLines(db)
                .Where(l => l.HR_PAYROLL_HEADERS.ApprovedDT.Value <= asOf);
            if (openingDate.HasValue)
            {
                var od = openingDate.Value;
                linesQuery = linesQuery.Where(l => l.HR_PAYROLL_HEADERS.ApprovedDT.Value > od);
            }
            var accrued = linesQuery.Sum(l => (decimal?)l.NetSalary) ?? 0m;

            var paymentQuery = FinQueries.PayrollPayments(
                db,
                FinQueries.SalaryCategoryIds(db),
                openingDate.HasValue ? openingDate.Value.AddDays(1) : DateTime.MinValue,
                asOf);

            return new PayrollLiabilityResult
            {
                Accrued = accrued,
                Paid = paymentQuery.Sum(t => (decimal?)t.Amount) ?? 0m,
                PaymentQuery = paymentQuery
            };
        }

        #endregion

        #region Doimiy harajatlar

        /// <summary>FIN_RECURRING_OBLIGATIONS qatori (jadval EDMX'da yo'q — xom SQL).</summary>
        public class RecurringObligationRow
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public decimal MonthlyAmount { get; set; }
            public int CurrencyID { get; set; }
            public int StartYear { get; set; }
            public byte StartMonth { get; set; }
        }

        /// <summary>
        /// Faol majburiyatlar. Jadval bazada bo'lmasa (Scripts/Create_FIN_RECURRING_OBLIGATIONS.sql bajarilmagan) — bo'sh ro'yxat.
        /// </summary>
        public static List<RecurringObligationRow> ActiveRecurringObligations(DBTHSNEntities db, int? balanceCategoryId = null)
        {
            try
            {
                const string baseSql = "SELECT ID, Name, MonthlyAmount, CurrencyID, StartYear, StartMonth FROM FIN_RECURRING_OBLIGATIONS WHERE (IsDeleted = 0 OR IsDeleted IS NULL) AND IsActive = 1";
                return balanceCategoryId.HasValue
                    ? db.Database.SqlQuery<RecurringObligationRow>(baseSql + " AND BalanceCategoryID = @p0", balanceCategoryId.Value).ToList()
                    : db.Database.SqlQuery<RecurringObligationRow>(baseSql).ToList();
            }
            catch (System.Data.SqlClient.SqlException ex)
            {
                Trace.TraceWarning("FIN_RECURRING_OBLIGATIONS o'qilmadi: " + ex.Message);
                return new List<RecurringObligationRow>();
            }
        }

        /// <summary>Majburiyatlar bo'yicha to'lovlar (SourceDocType = RecurringObligationPayment), bitta GROUP BY.</summary>
        public static Dictionary<int, decimal> RecurringObligationPayments(DBTHSNEntities db, ICollection<int> obligationIds, DateTime asOf, DateTime? fromExclusive)
        {
            if (obligationIds == null || obligationIds.Count == 0)
                return new Dictionary<int, decimal>();

            var ids = obligationIds.ToList();
            var q = FinQueries.Active(db)
                .Where(t => t.SourceDocType == "RecurringObligationPayment" &&
                            t.SourceDocID.HasValue &&
                            ids.Contains(t.SourceDocID.Value) &&
                            t.TxnDate <= asOf);
            if (fromExclusive.HasValue)
            {
                var f = fromExclusive.Value;
                q = q.Where(t => t.TxnDate > f);
            }
            return q.GroupBy(t => t.SourceDocID.Value)
                .Select(g => new { ID = g.Key, Paid = g.Sum(t => t.Amount) })
                .ToList()
                .ToDictionary(x => x.ID, x => x.Paid);
        }

        public static int AccruedMonths(DateTime startMonth, DateTime endDate)
        {
            return ((endDate.Year - startMonth.Year) * 12) + (endDate.Month - startMonth.Month) + 1;
        }

        public sealed class RecurringLiabilityResult
        {
            public decimal Amount { get; set; }
            public decimal Accrued { get; set; }
            public decimal Paid { get; set; }
            public int Count { get; set; }
        }

        /// <summary>Balans kategoriyasiga bog'langan doimiy harajatlar qarzi: Σ max(0, oylar × summa − to'lovlar).</summary>
        public static RecurringLiabilityResult RecurringObligationsLiability(DBTHSNEntities db, int balanceCategoryId, DateTime asOf, DateTime? openingDate)
        {
            var result = new RecurringLiabilityResult();
            var obligations = ActiveRecurringObligations(db, balanceCategoryId);
            var paidById = RecurringObligationPayments(db, obligations.Select(o => o.ID).ToList(), asOf, openingDate);
            var endDate = asOf.Date;
            foreach (var ob in obligations)
            {
                var startDate = new DateTime(ob.StartYear, ob.StartMonth, 1);
                var effectiveStart = openingDate.HasValue && openingDate.Value > startDate ? openingDate.Value : startDate;
                if (endDate < effectiveStart) continue;
                var monthsCount = AccruedMonths(effectiveStart, endDate);
                if (monthsCount < 1) continue;

                var accrued = ob.MonthlyAmount * monthsCount;
                var paid = paidById.TryGetValue(ob.ID, out var p) ? p : 0m;
                result.Accrued += accrued;
                result.Paid += paid;
                result.Count++;
                var net = accrued - paid;
                if (net > 0) result.Amount += net;
            }
            return result;
        }

        #endregion

        #region Logistika

        public sealed class LogisticsLiabilityResult
        {
            public DateTime? OpeningDate { get; set; }
            public decimal OpeningNet { get; set; }
            public double WaybillTotal { get; set; }
            public decimal Paid { get; set; }
            public IQueryable<FIN_TRANSACTIONS> PaymentQuery { get; set; }
            public decimal Amount => OpeningNet + (decimal)WaybillTotal - Paid;
        }

        /// <summary>Logistika qarzi: opening (BalanceType=4) + yo'l varaqalari − "m.tash"/"t.tash" chiqimlari.</summary>
        public static LogisticsLiabilityResult Logistics(DBTHSNEntities db, CounterpartyBalanceService counterparties, DateTime asOf)
        {
            var result = new LogisticsLiabilityResult();
            var logiOpening = counterparties.LatestOpeningDate(asOf, CounterpartyBalanceService.BalanceTypeLogistics);
            result.OpeningDate = logiOpening;
            result.OpeningNet = logiOpening.HasValue
                ? counterparties.OpeningContractBalances(logiOpening.Value, CounterpartyBalanceService.BalanceTypeLogistics).Where(x => x.Amount > 0).Sum(x => x.Amount)
                : 0m;

            result.WaybillTotal = db.F_WAYBILLS
                .Where(w => w.IsDeleted == false && w.Amount.HasValue &&
                            (!logiOpening.HasValue || w.IssueDate > logiOpening.Value) &&
                            w.IssueDate <= asOf)
                .Sum(w => (double?)w.Amount) ?? 0;

            var categoryNames = new[] { "m.tash", "t.tash" };
            var logisticsCategoryIds = db.FIN_CATEGORIES
                .Where(c => c.IsActive && categoryNames.Contains(c.Name.ToLower()))
                .Select(c => c.ID)
                .ToList();

            if (logisticsCategoryIds.Count > 0)
            {
                result.PaymentQuery = FinQueries.Active(db)
                    .Where(t => logisticsCategoryIds.Contains(t.CategoryID) &&
                                t.TxnDate <= asOf &&
                                (!logiOpening.HasValue || t.TxnDate > logiOpening.Value) &&
                                (t.IsDeleted == null || t.IsDeleted == false));
                result.Paid = result.PaymentQuery
                    .Where(t => t.Direction == 2)
                    .Sum(t => (decimal?)t.Amount) ?? 0m;
            }

            return result;
        }

        #endregion
    }
}
