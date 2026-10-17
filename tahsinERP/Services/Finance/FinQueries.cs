using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services.Finance
{
    /// <summary>
    /// Finance hisobotlari uchun umumiy so'rovlar (avval FinDashboardController ichida 6–15 marta takrorlangan):
    ///  - faol tranzaksiyalar va "real pul" filtri (invoys mirror yozuvlarisiz, D1);
    ///  - hisob qoldiqlari — har hisobga alohida SUM o'rniga bitta GROUP BY (D2, FIN-P02);
    ///  - oylik kategoriyalari, tasdiqlangan payroll qatorlari, oylik to'lovlari (D5, D6);
    ///  - asosiy valyuta (eng ko'p ishlatilgan CurrencyID, D12).
    /// Hammasi o'qish uchun (AsNoTracking).
    /// </summary>
    public static class FinQueries
    {
        public const string PurchaseInvoiceMirror = "PurchaseInvoice";
        public const string SalesInvoiceMirror = "SalesInvoice";

        /// <summary>O'chirilmagan tranzaksiyalar (o'qish uchun).</summary>
        public static IQueryable<FIN_TRANSACTIONS> Active(DBTHSNEntities db)
        {
            return db.FIN_TRANSACTIONS.AsNoTracking().Where(t => t.IsDeleted != true);
        }

        /// <summary>
        /// Real pul harakati: PurchaseInvoice/SalesInvoice "mirror" (majburiyat) yozuvlarisiz.
        /// </summary>
        public static IQueryable<FIN_TRANSACTIONS> RealCash(this IQueryable<FIN_TRANSACTIONS> query)
        {
            return query.Where(t => (t.SourceDocType != PurchaseInvoiceMirror && t.SourceDocType != SalesInvoiceMirror)
                                    || t.SourceDocType == null || t.SourceDocType == "");
        }

        public static IQueryable<FIN_TRANSACTIONS> InPeriod(this IQueryable<FIN_TRANSACTIONS> query, DateTime from, DateTime to)
        {
            return query.Where(t => t.TxnDate >= from && t.TxnDate <= to);
        }

        /// <summary>
        /// Hisoblar qoldig'i bitta GROUP BY bilan: Σ(Direction==1 ? +Amount : −Amount).
        /// <paramref name="asOf"/> berilsa TxnDate ≤ asOf, <paramref name="fromExclusive"/> berilsa TxnDate &gt; from.
        /// Harakati yo'q hisob natijada bo'lmaydi (0 deb hisoblang).
        /// </summary>
        public static Dictionary<int, decimal> AccountBalances(DBTHSNEntities db, ICollection<int> accountIds, DateTime? asOf = null, DateTime? fromExclusive = null)
        {
            if (accountIds == null || accountIds.Count == 0)
                return new Dictionary<int, decimal>();

            var ids = accountIds.Distinct().ToList();
            var q = Active(db).Where(t => ids.Contains(t.AccountID));
            if (asOf.HasValue)
            {
                var to = asOf.Value;
                q = q.Where(t => t.TxnDate <= to);
            }
            if (fromExclusive.HasValue)
            {
                var from = fromExclusive.Value;
                q = q.Where(t => t.TxnDate > from);
            }

            return q.GroupBy(t => t.AccountID)
                .Select(g => new { AccountID = g.Key, Balance = g.Sum(t => t.Direction == 1 ? t.Amount : -t.Amount) })
                .ToList()
                .ToDictionary(x => x.AccountID, x => x.Balance);
        }

        public static decimal SumBalance(Dictionary<int, decimal> balances, IEnumerable<int> accountIds)
        {
            decimal total = 0m;
            foreach (var id in accountIds)
            {
                if (balances.TryGetValue(id, out var b)) total += b;
            }
            return total;
        }

        /// <summary>
        /// Hisob × sana bo'yicha kunlik yig'indilar (≤ toDate). Bir so'rov bilan har hisobning qoldig'ini,
        /// istalgan sanadan keyingi harakatini va davr kirim/chiqimini xotirada hisoblash uchun
        /// (avval har hisobga butun tarixi ToList qilinardi — FIN-P02/P07).
        /// </summary>
        public static Dictionary<int, List<AccountDayTotal>> AccountDailyTotals(DBTHSNEntities db, ICollection<int> accountIds, DateTime toDate)
        {
            if (accountIds == null || accountIds.Count == 0)
                return new Dictionary<int, List<AccountDayTotal>>();

            var ids = accountIds.Distinct().ToList();
            return Active(db)
                .Where(t => ids.Contains(t.AccountID) && t.TxnDate <= toDate)
                .GroupBy(t => new { t.AccountID, t.TxnDate })
                .Select(g => new
                {
                    g.Key.AccountID,
                    g.Key.TxnDate,
                    In = g.Sum(t => t.Direction == 1 ? t.Amount : 0m),
                    Out = g.Sum(t => t.Direction == 2 ? t.Amount : 0m),
                    Net = g.Sum(t => t.Direction == 1 ? t.Amount : -t.Amount)
                })
                .ToList()
                .GroupBy(x => x.AccountID)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => new AccountDayTotal { TxnDate = x.TxnDate, In = x.In, Out = x.Out, Net = x.Net }).ToList());
        }

        /// <summary>Nomida "oylik" bo'lgan faol FIN_CATEGORIES (D5).</summary>
        public static List<int> SalaryCategoryIds(DBTHSNEntities db)
        {
            return db.FIN_CATEGORIES
                .Where(c => c.IsActive &&
                            (c.IsDeleted == null || c.IsDeleted == false) &&
                            c.Name != null &&
                            c.Name.ToLower().Contains("oylik"))
                .Select(c => c.ID)
                .ToList();
        }

        public static bool IsSalaryLiabilityCategory(string categoryName)
        {
            return !string.IsNullOrWhiteSpace(categoryName) && categoryName.Trim().ToLower().Contains("oylik");
        }

        /// <summary>Oylik to'lovlari: chiqim, [from; to], "oylik" kategoriyasi yoki xodim (CP=6) yoki HR_PAYROLL.</summary>
        public static IQueryable<FIN_TRANSACTIONS> PayrollPayments(DBTHSNEntities db, List<int> salaryCategoryIds, DateTime fromDate, DateTime toDate)
        {
            var query = Active(db)
                .Where(t => t.Direction == 2 &&
                            t.TxnDate >= fromDate &&
                            t.TxnDate <= toDate &&
                            (t.IsDeleted == null || t.IsDeleted == false));

            if (salaryCategoryIds == null || salaryCategoryIds.Count == 0)
            {
                return query.Where(t => t.CounterpartyType == 6 || t.SourceDocType == "HR_PAYROLL");
            }

            return query.Where(t => salaryCategoryIds.Contains(t.CategoryID) ||
                                    t.CounterpartyType == 6 ||
                                    t.SourceDocType == "HR_PAYROLL");
        }

        /// <summary>Tasdiqlangan (Approved/Posted), o'chirilmagan payroll qatorlari; ApprovedDT majburiy (D6).</summary>
        public static IQueryable<HR_PAYROLL_LINES> ApprovedPayrollLines(DBTHSNEntities db)
        {
            return db.HR_PAYROLL_LINES.AsNoTracking()
                .Where(l => (l.IsDeleted == null || l.IsDeleted == false) &&
                            l.HR_PAYROLL_HEADERS != null &&
                            (l.HR_PAYROLL_HEADERS.IsDeleted == null || l.HR_PAYROLL_HEADERS.IsDeleted == false) &&
                            (l.HR_PAYROLL_HEADERS.Status == "Approved" || l.HR_PAYROLL_HEADERS.Status == "Posted") &&
                            l.HR_PAYROLL_HEADERS.ApprovedDT.HasValue);
        }

        /// <summary>
        /// Eng ko'p ishlatilgan valyuta (teng bo'lsa — birinchi uchragani). Tranzaksiyalarni xotiraga olmasdan, SQL'da.
        /// </summary>
        public static int? MostUsedCurrencyId(IQueryable<FIN_TRANSACTIONS> query)
        {
            var top = query
                .GroupBy(t => t.CurrencyID)
                .Select(g => new { CurrencyID = g.Key, Count = g.Count(), FirstID = g.Min(t => t.ID) })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.FirstID)
                .FirstOrDefault();
            return top?.CurrencyID;
        }

        /// <summary>Asosiy valyuta qisqa nomi; tranzaksiya bo'lmasa — bazadagi birinchi valyuta, u ham bo'lmasa "UZS".</summary>
        public static string PrimaryCurrencyShortname(DBTHSNEntities db, IQueryable<FIN_TRANSACTIONS> query)
        {
            var currencyId = MostUsedCurrencyId(query);
            CURRENCy currency = currencyId.HasValue
                ? db.CURRENCIES.AsNoTracking().FirstOrDefault(c => c.ID == currencyId.Value)
                : null;
            currency = currency ?? db.CURRENCIES.AsNoTracking().FirstOrDefault();
            return currency?.Shortname ?? "UZS";
        }
    }

    public sealed class AccountDayTotal
    {
        public DateTime TxnDate { get; set; }
        public decimal In { get; set; }
        public decimal Out { get; set; }
        public decimal Net { get; set; }
    }
}
