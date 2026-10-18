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
        #region Oylik

        /// <summary>Hisoblangan oylik qatorlari: ApprovedDT ∈ (opening; asOf].</summary>
        private static IQueryable<HR_PAYROLL_LINES> PayrollLines(DBTHSNEntities db, DateTime asOf, DateTime? openingDate)
        {
            var linesQuery = FinQueries.ApprovedPayrollLines(db)
                .Where(l => l.HR_PAYROLL_HEADERS.ApprovedDT.Value <= asOf);
            if (openingDate.HasValue)
            {
                var od = openingDate.Value;
                linesQuery = linesQuery.Where(l => l.HR_PAYROLL_HEADERS.ApprovedDT.Value > od);
            }
            return linesQuery;
        }

        /// <summary>Oylik to'lovlari: [opening+1 kun; asOf], "oylik" kategoriyasi / xodim (CP=6) / HR_PAYROLL.</summary>
        private static IQueryable<FIN_TRANSACTIONS> PayrollPaymentQuery(DBTHSNEntities db, DateTime asOf, DateTime? openingDate)
        {
            return FinQueries.PayrollPayments(
                db,
                FinQueries.SalaryCategoryIds(db),
                openingDate.HasValue ? openingDate.Value.AddDays(1) : DateTime.MinValue,
                asOf);
        }

        public sealed class EmployeePayrollDebt
        {
            public int EmployeeID { get; set; }
            public string EmployeeName { get; set; }
            public decimal OpeningTotal { get; set; }
            public decimal OpeningPaid { get; set; }
            public decimal Accrued { get; set; }
            public decimal Paid { get; set; }
            public decimal Calculated => OpeningTotal + Accrued;
            public decimal TotalPaid => OpeningPaid + Paid;
            public decimal Debt => Calculated - TotalPaid;
        }

        public sealed class PayrollBreakdown
        {
            public DateTime? OpeningDate { get; set; }
            public List<EmployeePayrollDebt> Employees { get; set; }
            /// <summary>Xodimga bog'lanmagan oylik to'lovlari ("oylik" kategoriyasi / HR_PAYROLL, CounterpartyID yo'q).</summary>
            public decimal UnattributedPaid { get; set; }
            /// <summary>Opening'dan keyingi oylik to'lovlari (balans tafsilotidagi harakatlar statistikasi uchun).</summary>
            public IQueryable<FIN_TRANSACTIONS> PaymentQuery { get; set; }
            public decimal OpeningNet => Employees.Sum(e => e.OpeningTotal - e.OpeningPaid);
            public decimal Accrued => Employees.Sum(e => e.Accrued);
            public decimal Paid => Employees.Sum(e => e.Paid) + UnattributedPaid;
            /// <summary>Oylik qarzi: opening + hisoblangan − barcha to'lovlar (manfiy bo'lsa 0).</summary>
            public decimal Total => Math.Max(0m, OpeningNet + Accrued - Paid);
        }

        /// <summary>
        /// Oylik qarzi (balans va ReportsHub uchun yagona funksiya, FIN-B18): oxirgi oylik opening'i
        /// (FIN_OPENING_CONTRACT_BALANCES, BalanceType 3) + undan keyin tasdiqlangan oylik − undan keyingi
        /// oylik to'lovlari ("oylik" kategoriyasi / CP=6 / HR_PAYROLL). Avval balans oylik opening'ini umuman
        /// olmasdi (subledger snapshot'i o'chirilardi), ReportsHub esa barcha davr oyligiga opening'ni qo'shib
        /// ikki marta sanardi va faqat CP=6 to'lovlarini ayirardi.
        /// </summary>
        public static PayrollBreakdown PayrollByEmployee(DBTHSNEntities db, CounterpartyBalanceService counterparties, DateTime asOf)
        {
            var openingDate = counterparties.LatestOpeningDate(asOf, CounterpartyBalanceService.BalanceTypePayroll);
            var byEmployee = new Dictionary<int, EmployeePayrollDebt>();
            EmployeePayrollDebt Get(int id)
            {
                if (!byEmployee.TryGetValue(id, out var e))
                {
                    e = new EmployeePayrollDebt { EmployeeID = id };
                    byEmployee[id] = e;
                }
                return e;
            }

            if (openingDate.HasValue)
            {
                foreach (var row in counterparties.OpeningContractBalances(openingDate.Value, CounterpartyBalanceService.BalanceTypePayroll)
                             .Where(x => x.CounterpartyID > 0))
                {
                    var e = Get(row.CounterpartyID);
                    e.OpeningTotal += row.TotalAmount;
                    e.OpeningPaid += row.PaidAmount;
                }
            }

            var accrued = PayrollLines(db, asOf, openingDate)
                .GroupBy(l => new { l.EmployeeID, Name = l.HR_EMPLOYEES.FullName })
                .Select(g => new { g.Key.EmployeeID, g.Key.Name, Amount = g.Sum(x => (decimal?)x.NetSalary) ?? 0m })
                .ToList();
            foreach (var a in accrued)
            {
                var e = Get(a.EmployeeID);
                e.Accrued += a.Amount;
                e.EmployeeName = e.EmployeeName ?? a.Name;
            }

            var paymentQuery = PayrollPaymentQuery(db, asOf, openingDate);
            // FIN-B01: to'lovlar so'mda (to'lov kunidagi kurs)
            var money = FinMoney.For(db);
            var payments = paymentQuery
                .GroupBy(t => new { EmployeeID = t.CounterpartyType == 6 ? t.CounterpartyID : null, t.CurrencyID, Day = DbFunctions.TruncateTime(t.TxnDate) })
                .Select(g => new { g.Key.EmployeeID, g.Key.CurrencyID, g.Key.Day, Amount = g.Sum(t => t.Amount) })
                .ToList()
                .Select(x => new { x.EmployeeID, Amount = money.Uzs(x.Amount, x.CurrencyID, x.Day.Value) })
                .ToList();
            decimal unattributed = 0m;
            foreach (var p in payments)
            {
                if (p.EmployeeID.HasValue && p.EmployeeID.Value > 0)
                    Get(p.EmployeeID.Value).Paid += p.Amount;
                else
                    unattributed += p.Amount;
            }

            var missingNames = byEmployee.Values.Where(e => e.EmployeeName == null).Select(e => e.EmployeeID).ToList();
            if (missingNames.Count > 0)
            {
                var names = db.HR_EMPLOYEES.AsNoTracking()
                    .Where(e => missingNames.Contains(e.ID))
                    .Select(e => new { e.ID, e.FullName })
                    .ToList();
                foreach (var n in names)
                    byEmployee[n.ID].EmployeeName = n.FullName;
            }
            foreach (var e in byEmployee.Values.Where(e => string.IsNullOrEmpty(e.EmployeeName)))
                e.EmployeeName = $"Xodim #{e.EmployeeID}";

            return new PayrollBreakdown
            {
                OpeningDate = openingDate,
                Employees = byEmployee.Values.ToList(),
                UnattributedPaid = unattributed,
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
        public static List<RecurringObligationRow> ActiveRecurringObligations(DBTHSNEntities db, int? balanceCategoryId = null, bool includeUnassigned = false)
        {
            try
            {
                const string baseSql = "SELECT ID, Name, MonthlyAmount, CurrencyID, StartYear, StartMonth FROM FIN_RECURRING_OBLIGATIONS WHERE (IsDeleted = 0 OR IsDeleted IS NULL) AND IsActive = 1";
                if (!balanceCategoryId.HasValue)
                    return db.Database.SqlQuery<RecurringObligationRow>(baseSql).ToList();
                return includeUnassigned
                    ? db.Database.SqlQuery<RecurringObligationRow>(baseSql + " AND (BalanceCategoryID = @p0 OR BalanceCategoryID IS NULL)", balanceCategoryId.Value).ToList()
                    : db.Database.SqlQuery<RecurringObligationRow>(baseSql + " AND BalanceCategoryID = @p0", balanceCategoryId.Value).ToList();
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
            // FIN-B01: so'mda (to'lov kunidagi kurs)
            var money = FinMoney.For(db);
            return q.GroupBy(t => new { ID = t.SourceDocID.Value, t.CurrencyID, Day = DbFunctions.TruncateTime(t.TxnDate) })
                .Select(g => new { g.Key.ID, g.Key.CurrencyID, g.Key.Day, Paid = g.Sum(t => t.Amount) })
                .ToList()
                .GroupBy(x => x.ID)
                .ToDictionary(g => g.Key, g => g.Sum(x => money.Uzs(x.Paid, x.CurrencyID, x.Day.Value)));
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

        /// <summary>
        /// Balans kategoriyasiga bog'langan doimiy harajatlar qarzi: Σ max(0, oylar × summa − to'lovlar).
        /// FIN-B17: kategoriyasi tanlanmagan (NULL) majburiyatlar "Doimiy harajatlar qarzi" kategoriyasiga
        /// tushadi (avval balansdan yo'qolib, faqat ReportsHub'da ko'rinardi).
        /// </summary>
        public static RecurringLiabilityResult RecurringObligationsLiability(DBTHSNEntities db, int balanceCategoryId, DateTime asOf, DateTime? openingDate, bool includeUnassigned = false)
        {
            var result = new RecurringLiabilityResult();
            var obligations = ActiveRecurringObligations(db, balanceCategoryId, includeUnassigned);
            var paidById = RecurringObligationPayments(db, obligations.Select(o => o.ID).ToList(), asOf, openingDate);
            var endDate = asOf.Date;
            foreach (var ob in obligations)
            {
                var startDate = new DateTime(ob.StartYear, ob.StartMonth, 1);
                var effectiveStart = openingDate.HasValue && openingDate.Value > startDate ? openingDate.Value : startDate;
                if (endDate < effectiveStart) continue;
                var monthsCount = AccruedMonths(effectiveStart, endDate);
                if (monthsCount < 1) continue;

                // FIN-B01: har oy hisobi o'sha oy boshidagi kurs bilan so'mda
                decimal accrued = 0m;
                for (int m = 0; m < monthsCount; m++)
                    accrued += FinMoney.For(db).Uzs(ob.MonthlyAmount, ob.CurrencyID, new DateTime(effectiveStart.Year, effectiveStart.Month, 1).AddMonths(m));
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
            /// <summary>Akt-sverki tuzatishlari (opening'dan keyingi): + qarzni oshiradi.</summary>
            public decimal Adjustments { get; set; }
            public IQueryable<FIN_TRANSACTIONS> PaymentQuery { get; set; }
            public decimal Amount => OpeningNet + (decimal)WaybillTotal - Paid + Adjustments;
        }

        /// <summary>
        /// Logistika qarzi — ekspeditor akt-sverkilari ("Barcha shartnomalar") yig'indisi bilan bir xil formula:
        /// opening (BalanceType=4, barcha qatorlar) + undan keyingi yo'l varaqalari − ekspeditor to'lovlari
        /// (F_WAYBILL_PAYMENTS) − qo'lda kiritilgan FIN to'lovlari (CP=4, chiqim, WaybillPayment ko'zgusi emas).
        /// Avval to'lovlar "m.tash"/"t.tash" kategoriyasi nomi bo'yicha FIN'dan olinardi: ular ichida ta'minotchi
        /// to'lovlari bor, FIN'ga yozilmagan ekspeditor to'lovlari esa yo'q edi (2025-12-31: barcha varaqalar to'langan,
        /// lekin balansda 83 mln qarz chiqardi).
        /// </summary>
        public static LogisticsLiabilityResult Logistics(DBTHSNEntities db, CounterpartyBalanceService counterparties, DateTime asOf)
        {
            var result = new LogisticsLiabilityResult();
            var logiOpening = counterparties.LatestOpeningDate(asOf, CounterpartyBalanceService.BalanceTypeLogistics);
            result.OpeningDate = logiOpening;
            result.OpeningNet = logiOpening.HasValue
                ? counterparties.OpeningContractBalances(logiOpening.Value, CounterpartyBalanceService.BalanceTypeLogistics).Sum(x => x.Amount)
                : 0m;

            // double ustunlar xotirada decimal'ga yig'iladi
            result.WaybillTotal = db.F_WAYBILLS.AsNoTracking()
                .Where(w => (w.IsDeleted == null || w.IsDeleted == false) && w.Amount.HasValue &&
                            (!logiOpening.HasValue || w.IssueDate > logiOpening.Value) &&
                            w.IssueDate <= asOf)
                .Select(w => w.Amount)
                .ToList()
                .Sum(a => a ?? 0d);

            // Ekspeditor to'lovlari: varaqa shu sanagacha chiqarilgan, to'lov opening'dan keyin (sanasizi opening'ga kirgan)
            var waybillPaid = db.F_WAYBILL_PAYMENTS.AsNoTracking()
                .Where(p => !p.IsDeleted &&
                            p.ForwarderID == p.F_WAYBILLS.ForwarderID &&
                            (p.F_WAYBILLS.IsDeleted == null || p.F_WAYBILLS.IsDeleted == false) &&
                            p.F_WAYBILLS.Amount.HasValue &&
                            p.F_WAYBILLS.IssueDate <= asOf &&
                            (p.PaymentDate ?? DateTime.MinValue) <= asOf &&
                            (!logiOpening.HasValue || (p.PaymentDate.HasValue && p.PaymentDate.Value > logiOpening.Value)))
                .Select(p => p.Payment_Amount)
                .ToList()
                .Sum(a => (decimal)a);

            result.PaymentQuery = FinQueries.Active(db)
                .Where(t => t.CounterpartyType == 4 &&
                            t.CounterpartyID.HasValue &&
                            t.Direction == 2 &&
                            t.TxnDate <= asOf &&
                            (!logiOpening.HasValue || t.TxnDate > logiOpening.Value) &&
                            (t.SourceDocType == null || t.SourceDocType != "WaybillPayment"));
            var manualPaid = result.PaymentQuery.Sum(t => (decimal?)t.Amount) ?? 0m;

            result.Paid = waybillPaid + manualPaid;
            result.Adjustments = counterparties.AdjustmentTotals(CounterpartyBalanceService.BalanceTypeLogistics, asOf, logiOpening, creditIncreases: true).Values.Sum();
            return result;
        }

        #endregion
    }
}
