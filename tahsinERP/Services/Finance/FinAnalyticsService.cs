using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.ViewModels.Finance;

namespace tahsinERP.Services.Finance
{
    /// <summary>
    /// Pul oqimi va analitika hisobotlari (Cashflow, FinancialOverview, Category/Account/Direction analytics,
    /// AccountBalances, P&amp;L). Avval davrdagi barcha tranzaksiyalar Include'lar bilan xotiraga olinib,
    /// u yerda guruhlanardi (FIN-P06/P08/P09, lazy-load N+1) — endi agregatsiya SQL'da.
    /// FIN-B01: barcha jamilar so'mda — tranzaksiyalar (kategoriya, hisob, yo'nalish, valyuta, kun) guruhlarida
    /// kun kursi bilan o'giriladi (<see cref="FinMoneyQueries.UzsBuckets"/>); avval UZS va USD qo'shib yuborilardi.
    /// </summary>
    public static class FinAnalyticsService
    {
        #region Natija turlari (view'larda Json.Encode qilinadi — xossa nomlari o'zgarmasin)

        public sealed class DailyFlow
        {
            public string Date { get; set; }
            public decimal Inflow { get; set; }
            public decimal Outflow { get; set; }
        }

        public sealed class CashflowResult
        {
            public decimal Inflow { get; set; }
            public decimal Outflow { get; set; }
            public decimal CurrentBalance { get; set; }
            public string CurrencyShortname { get; set; }
            public List<DailyFlow> DailyData { get; set; }
        }

        public sealed class CategoryAmount
        {
            public string CategoryName { get; set; }
            public decimal Amount { get; set; }
        }

        public sealed class PeriodIncomeExpense
        {
            public string Period { get; set; }
            public decimal Income { get; set; }
            public decimal Expense { get; set; }
        }

        public sealed class OverviewResult
        {
            public decimal TotalIncome { get; set; }
            public decimal TotalExpense { get; set; }
            public decimal CashBalance { get; set; }
            public decimal BankBalance { get; set; }
            public List<CategoryAmount> TopExpenseCategories { get; set; }
            public List<CategoryAmount> TopIncomeCategories { get; set; }
            public List<PeriodIncomeExpense> MonthlyData { get; set; }
            public string CurrencyShortname { get; set; }
        }

        public sealed class CategoryBreakdownItem
        {
            public int CategoryID { get; set; }
            public string CategoryName { get; set; }
            public byte CategoryType { get; set; }
            public decimal Amount { get; set; }
            public int TransactionCount { get; set; }
        }

        public sealed class NamedAmount
        {
            public string Name { get; set; }
            public decimal Amount { get; set; }
        }

        public sealed class DirectionAnalysisResult
        {
            public List<NamedAmount> ByCategory { get; set; }
            public List<NamedAmount> ByAccount { get; set; }
            public List<MonthlyData> Monthly { get; set; }
            public decimal Total { get; set; }
            public string CurrencyShortname { get; set; }
        }

        #endregion

        private sealed class InOut
        {
            public decimal In { get; set; }
            public decimal Out { get; set; }
        }

        private static InOut Totals(List<TxnBucket> buckets)
        {
            return new InOut
            {
                In = buckets.Where(b => b.Direction == 1).Sum(b => b.Uzs),
                Out = buckets.Where(b => b.Direction == 2).Sum(b => b.Uzs)
            };
        }

        private static Dictionary<int, string> CategoryNames(DBTHSNEntities db)
        {
            return db.FIN_CATEGORIES.AsNoTracking().Select(c => new { c.ID, c.Name }).ToList().ToDictionary(c => c.ID, c => c.Name);
        }

        /// <summary>
        /// Naqd (IsCash) va bank (IsBank) faol hisoblari qoldig'i so'mda — har hisob qoldig'i o'z valyutasida
        /// bugungi kurs bilan o'giriladi (bitta GROUP BY, FIN-P02).
        /// </summary>
        private static void CashAndBankBalances(DBTHSNEntities db, out decimal cash, out decimal bank, out List<FIN_ACCOUNTS> cashAccounts, out List<FIN_ACCOUNTS> bankAccounts)
        {
            cashAccounts = db.FIN_ACCOUNTS.AsNoTracking().Where(a => a.IsCash && a.IsActive).OrderBy(a => a.ID).ToList();
            bankAccounts = db.FIN_ACCOUNTS.AsNoTracking().Where(a => a.IsBank && a.IsActive).OrderBy(a => a.ID).ToList();
            var ids = cashAccounts.Select(a => a.ID).Concat(bankAccounts.Select(a => a.ID)).ToList();
            var balances = FinQueries.AccountBalances(db, ids);
            var money = FinMoney.For(db);
            decimal Uzs(IEnumerable<FIN_ACCOUNTS> accounts) => accounts
                .Sum(a => balances.TryGetValue(a.ID, out var b) ? money.Uzs(b, a.CurrencyID, DateTime.Today) : 0m);
            cash = Uzs(cashAccounts);
            bank = Uzs(bankAccounts);
        }

        public static CashflowResult Cashflow(DBTHSNEntities db, DateTime fromDate, DateTime toDate, int? accountID)
        {
            var query = FinQueries.Active(db).InPeriod(fromDate, toDate).RealCash();
            if (accountID.HasValue)
            {
                var acc = accountID.Value;
                query = query.Where(t => t.AccountID == acc);
            }

            var buckets = query.UzsBuckets(db);
            var totals = Totals(buckets);
            CashAndBankBalances(db, out var cash, out var bank, out _, out _);

            var daily = buckets
                .GroupBy(b => b.Day)
                .OrderBy(g => g.Key)
                .Select(g => new DailyFlow
                {
                    Date = g.Key.ToString("dd.MM.yyyy"),
                    Inflow = g.Where(b => b.Direction == 1).Sum(b => b.Uzs),
                    Outflow = g.Where(b => b.Direction == 2).Sum(b => b.Uzs)
                })
                .ToList();

            return new CashflowResult
            {
                Inflow = totals.In,
                Outflow = totals.Out,
                CurrentBalance = cash + bank,
                CurrencyShortname = FinMoney.For(db).BaseShortname,
                DailyData = daily
            };
        }

        public static OverviewResult FinancialOverview(DBTHSNEntities db, DateTime fromDate, DateTime toDate)
        {
            var buckets = FinQueries.Active(db).InPeriod(fromDate, toDate).RealCash().UzsBuckets(db);
            var totals = Totals(buckets);
            CashAndBankBalances(db, out var cash, out var bank, out _, out _);
            var names = CategoryNames(db);

            List<CategoryAmount> TopCategories(byte direction)
            {
                return buckets
                    .Where(b => b.Direction == direction)
                    .GroupBy(b => b.CategoryID)
                    .Select(g => new CategoryAmount { CategoryName = names.TryGetValue(g.Key, out var n) ? n : null, Amount = g.Sum(b => b.Uzs) })
                    .OrderByDescending(x => x.Amount)
                    .Take(10)
                    .ToList();
            }

            var monthly = buckets
                .GroupBy(b => new { b.Day.Year, b.Day.Month })
                .Select(g => new PeriodIncomeExpense
                {
                    Period = $"{g.Key.Year}-{g.Key.Month:D2}",
                    Income = g.Where(b => b.Direction == 1).Sum(b => b.Uzs),
                    Expense = g.Where(b => b.Direction == 2).Sum(b => b.Uzs)
                })
                .OrderBy(x => x.Period)
                .ToList();

            return new OverviewResult
            {
                TotalIncome = totals.In,
                TotalExpense = totals.Out,
                CashBalance = cash,
                BankBalance = bank,
                TopExpenseCategories = TopCategories(2),
                TopIncomeCategories = TopCategories(1),
                MonthlyData = monthly,
                CurrencyShortname = FinMoney.For(db).BaseShortname
            };
        }

        /// <summary>Kategoriyalar kesimida chiqim (direction=2) yoki kirim (1): summa (so'mda) va soni.</summary>
        public static List<CategoryBreakdownItem> CategoryBreakdown(DBTHSNEntities db, DateTime fromDate, DateTime toDate, int? categoryID, byte direction)
        {
            var query = FinQueries.Active(db).InPeriod(fromDate, toDate).RealCash().Where(t => t.Direction == direction);
            if (categoryID.HasValue)
            {
                var cat = categoryID.Value;
                query = query.Where(t => t.CategoryID == cat);
            }

            var categories = db.FIN_CATEGORIES.AsNoTracking().Select(c => new { c.ID, c.Name, c.Type }).ToList().ToDictionary(c => c.ID);
            return query.UzsBuckets(db)
                .GroupBy(b => b.CategoryID)
                .Select(g => new CategoryBreakdownItem
                {
                    CategoryID = g.Key,
                    CategoryName = categories.TryGetValue(g.Key, out var c) ? c.Name : null,
                    CategoryType = categories.TryGetValue(g.Key, out var c2) ? c2.Type : (byte)0,
                    Amount = g.Sum(b => b.Uzs),
                    TransactionCount = g.Sum(b => b.Count)
                })
                .OrderByDescending(x => x.Amount)
                .ToList();
        }

        public static List<AccountUsageData> AccountUsage(DBTHSNEntities db, DateTime fromDate, DateTime toDate, int? accountID, out string currencyShortname)
        {
            var query = FinQueries.Active(db).InPeriod(fromDate, toDate).RealCash();
            if (accountID.HasValue)
            {
                var acc = accountID.Value;
                query = query.Where(t => t.AccountID == acc);
            }

            currencyShortname = FinMoney.For(db).BaseShortname;
            var accounts = db.FIN_ACCOUNTS.AsNoTracking()
                .Where(a => a.Name != null && a.Name != "")
                .Select(a => new { a.ID, a.Name, a.Code, a.IsCash, a.IsBank })
                .ToList()
                .ToDictionary(a => a.ID);

            return query.UzsBuckets(db)
                .Where(b => accounts.ContainsKey(b.AccountID))
                .GroupBy(b => b.AccountID)
                .Select(g =>
                {
                    var a = accounts[g.Key];
                    var inflow = g.Where(b => b.Direction == 1).Sum(b => b.Uzs);
                    var outflow = g.Where(b => b.Direction == 2).Sum(b => b.Uzs);
                    return new AccountUsageData
                    {
                        AccountID = a.ID,
                        AccountName = a.Name ?? "",
                        AccountCode = a.Code ?? "",
                        AccountType = a.IsCash ? "Naqd pul" : (a.IsBank ? "Bank" : "Boshqa"),
                        TotalInflow = inflow,
                        TotalOutflow = outflow,
                        TransactionCount = g.Sum(b => b.Count),
                        NetAmount = inflow - outflow
                    };
                })
                .OrderByDescending(x => Math.Abs(x.NetAmount))
                .ToList();
        }

        /// <summary>Chiqim (2) yoki kirim (1) tahlili: kategoriya, hisob va oy kesimida, so'mda (D20).</summary>
        public static DirectionAnalysisResult DirectionAnalysis(DBTHSNEntities db, DateTime fromDate, DateTime toDate, byte direction)
        {
            var buckets = FinQueries.Active(db).Where(t => t.Direction == direction).InPeriod(fromDate, toDate).RealCash().UzsBuckets(db);
            var categoryNames = CategoryNames(db);
            var accountNames = db.FIN_ACCOUNTS.AsNoTracking().Select(a => new { a.ID, a.Name }).ToList().ToDictionary(a => a.ID, a => a.Name);

            var byCategory = buckets
                .GroupBy(b => b.CategoryID)
                .Select(g => new NamedAmount { Name = (categoryNames.TryGetValue(g.Key, out var n) ? n : null) ?? "", Amount = g.Sum(b => b.Uzs) })
                .OrderByDescending(x => x.Amount)
                .ToList();

            var byAccount = buckets
                .GroupBy(b => b.AccountID)
                .Select(g => new NamedAmount { Name = (accountNames.TryGetValue(g.Key, out var n) ? n : null) ?? "", Amount = g.Sum(b => b.Uzs) })
                .OrderByDescending(x => x.Amount)
                .ToList();

            var monthly = buckets
                .GroupBy(b => new { b.Day.Year, b.Day.Month })
                .Select(g => new MonthlyData { Period = $"{g.Key.Year}-{g.Key.Month:D2}", Amount = g.Sum(b => b.Uzs) })
                .OrderBy(x => x.Period)
                .ToList();

            return new DirectionAnalysisResult
            {
                ByCategory = byCategory,
                ByAccount = byAccount,
                Monthly = monthly,
                Total = byCategory.Sum(x => x.Amount),
                CurrencyShortname = FinMoney.For(db).BaseShortname
            };
        }

        /// <summary>
        /// Hisoblar qoldig'i: opening bo'lsa — hisob opening'i (hisob kesimida kiritilmagan bo'lsa kategoriya / hisoblar soni)
        /// + opening sanasidan keyingi harakatlar (balans kabi); aks holda toDate gacha barcha harakatlar.
        /// Avval barcha hisoblar tarixi to'liq entity sifatida xotiraga olinardi (FIN-P07).
        /// </summary>
        public static List<AccountBalanceData> AccountBalances(DBTHSNEntities db, DateTime fromDate, DateTime toDate, int? accountType)
        {
            var accountsQuery = db.FIN_ACCOUNTS.AsNoTracking()
                .Include(a => a.CURRENCy)
                .Where(a => a.IsActive);

            if (accountType == 1)
            {
                accountsQuery = accountsQuery.Where(a => a.FIN_BALANCE_CATEGORIES != null && a.FIN_BALANCE_CATEGORIES.Type == 1);
            }
            else if (accountType == 2)
            {
                accountsQuery = accountsQuery.Where(a => a.FIN_BALANCE_CATEGORIES != null && a.FIN_BALANCE_CATEGORIES.Type == 2);
            }

            var accounts = accountsQuery.OrderBy(a => a.Code).ToList();
            var daily = FinQueries.AccountDailyTotals(db, accounts.Select(a => a.ID).ToList(), toDate);

            var openingSnapshots = db.FIN_BALANCE_SNAPSHOTS.AsNoTracking()
                .Where(s => s.SnapshotDate <= toDate &&
                            (s.SourceType == "OpeningBalance" || s.SourceType == null || s.SourceType == ""))
                .OrderBy(s => s.ID)
                .ToList()
                .GroupBy(s => s.CategoryID)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.SnapshotDate).First());

            var accountCountByCategory = accounts
                .Where(a => a.BalanceCategoryID.HasValue)
                .GroupBy(a => a.BalanceCategoryID.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            // FIN-B16: hisob kesimidagi opening'lar (SourceID = AccountID), sana bo'yicha
            var accountOpenings = db.FIN_BALANCE_SNAPSHOTS.AsNoTracking()
                .Where(s => s.SnapshotDate <= toDate && s.SourceType == OpeningBalanceService.AccountOpeningSourceType && s.SourceID.HasValue)
                .Select(s => new { s.ID, AccountID = s.SourceID.Value, s.CategoryID, s.SnapshotDate, s.Amount })
                .ToList();
            var accountOpeningByKey = accountOpenings
                .GroupBy(s => new { s.AccountID, Date = s.SnapshotDate.Date })
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ID).First().Amount);
            var categoryDatesWithAccounts = new HashSet<string>(accountOpenings.Select(s => s.CategoryID + "|" + s.SnapshotDate.Date.ToString("yyyyMMdd")));

            var result = new List<AccountBalanceData>();
            foreach (var account in accounts)
            {
                var days = daily.TryGetValue(account.ID, out var d) ? d : new List<AccountDayTotal>();

                decimal qoldiq;
                var usesOpening = false;
                var approximate = false;
                if (account.BalanceCategoryID.HasValue &&
                    openingSnapshots.TryGetValue(account.BalanceCategoryID.Value, out var snapRow))
                {
                    var snapDate = snapRow.SnapshotDate.Date;
                    decimal opening;
                    if (categoryDatesWithAccounts.Contains(account.BalanceCategoryID.Value + "|" + snapDate.ToString("yyyyMMdd")))
                    {
                        // Shu opening'da hisoblar bo'yicha qoldiq kiritilgan — hisobniki (kiritilmagan hisob — 0)
                        opening = accountOpeningByKey.TryGetValue(new { AccountID = account.ID, Date = snapDate }, out var a) ? a : 0m;
                    }
                    else
                    {
                        // Eski opening (faqat kategoriya summasi): hisoblar soniga teng bo'linadi — taxminiy
                        var cnt = accountCountByCategory.TryGetValue(account.BalanceCategoryID.Value, out var c) ? Math.Max(1, c) : 1;
                        opening = snapRow.Amount / cnt;
                        approximate = cnt > 1;
                    }
                    // FIN-B16: balans kabi opening sanasidan KEYINGI harakatlar (avval >= — opening kuni ikki marta)
                    qoldiq = opening + days.Where(x => x.TxnDate > snapDate).Sum(x => x.Net);
                    usesOpening = true;
                }
                else
                {
                    qoldiq = days.Sum(x => x.Net);
                }

                var period = days.Where(x => x.TxnDate >= fromDate && x.TxnDate <= toDate).ToList();
                result.Add(new AccountBalanceData
                {
                    AccountID = account.ID,
                    AccountCode = account.Code ?? "",
                    AccountName = account.Name ?? "",
                    OwnerName = account.OwnerName ?? "-",
                    CurrencyShortname = account.CURRENCy?.Shortname ?? "UZS",
                    Kirim = period.Sum(x => x.In),
                    Chiqim = period.Sum(x => x.Out),
                    Qoldiq = qoldiq,
                    IncludesOpeningBalance = usesOpening,
                    IsOpeningApproximate = approximate
                });
            }

            return result;
        }

        /// <summary>
        /// Hisoblar qoldig'i (toDate gacha) va davrdagi kirim/chiqim — opening'siz (Liabilities sahifasidagi
        /// kreditor/debitor hisoblari uchun).
        /// </summary>
        public static List<AccountBalanceData> PlainAccountBalances(DBTHSNEntities db, List<FIN_ACCOUNTS> accounts, DateTime fromDate, DateTime toDate)
        {
            var daily = FinQueries.AccountDailyTotals(db, accounts.Select(a => a.ID).ToList(), toDate);
            return accounts.Select(account =>
            {
                var days = daily.TryGetValue(account.ID, out var d) ? d : new List<AccountDayTotal>();
                var period = days.Where(x => x.TxnDate >= fromDate && x.TxnDate <= toDate).ToList();
                return new AccountBalanceData
                {
                    AccountID = account.ID,
                    AccountCode = account.Code ?? "",
                    AccountName = account.Name ?? "",
                    OwnerName = account.OwnerName ?? "-",
                    CurrencyShortname = account.CURRENCy?.Shortname ?? "UZS",
                    Kirim = period.Sum(x => x.In),
                    Chiqim = period.Sum(x => x.Out),
                    Qoldiq = days.Sum(x => x.Net)
                };
            }).ToList();
        }

        /// <summary>
        /// P&amp;L: har PL bandi uchun bog'langan kategoriyalar bo'yicha fakt (Sign=1: kirim +, aks holda chiqim +) va reja.
        /// Avval har band uchun alohida FIN_CATEGORIES so'rovi (N+1) va xotirada yig'indi (FIN-P09).
        /// </summary>
        public static List<PLItemData> ProfitAndLoss(DBTHSNEntities db, int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // FIN-B01: so'mda (kun kursi)
            var sums = FinQueries.Active(db)
                .Where(t => t.TxnDate >= startDate && t.TxnDate <= endDate)
                .RealCash()
                .UzsBuckets(db)
                .GroupBy(b => new { b.CategoryID, b.Direction })
                .Select(g => new { g.Key.CategoryID, g.Key.Direction, Amount = g.Sum(b => b.Uzs) })
                .ToList();

            var allPlItems = db.FIN_PL_ITEMS.AsNoTracking()
                .Where(i => i.IsActive)
                .OrderBy(i => i.SortOrder)
                .ToList();

            var categoriesByItem = db.FIN_PL_ITEMS
                .Where(i => i.IsActive)
                .Select(i => new { i.ID, Cats = i.FIN_CATEGORIES.Select(c => c.ID) })
                .ToList()
                .ToDictionary(x => x.ID, x => new HashSet<int>(x.Cats));

            var plans = db.FIN_PL_PLANS.AsNoTracking()
                .Where(p => p.Year == year && p.Month == month && p.IsDeleted != true)
                .ToDictionary(p => p.ItemID, p => p.AmountPlan);

            PLItemData CalculateItemData(FIN_PL_ITEMS item)
            {
                var cats = categoriesByItem.TryGetValue(item.ID, out var set) ? set : new HashSet<int>();
                var actual = sums
                    .Where(s => cats.Contains(s.CategoryID))
                    .Sum(s => item.Sign == 1
                        ? (s.Direction == 1 ? s.Amount : -s.Amount)
                        : (s.Direction == 2 ? s.Amount : -s.Amount));
                var planAmount = plans.ContainsKey(item.ID) ? plans[item.ID] : 0m;

                return new PLItemData
                {
                    Item = item,
                    Actual = actual,
                    Plan = planAmount,
                    Variance = actual - planAmount,
                    VariancePercent = planAmount > 0 ? ((actual - planAmount) / planAmount * 100) : 0,
                    Children = new List<PLItemData>()
                };
            }

            var plData = new List<PLItemData>();
            foreach (var parentItem in allPlItems.Where(i => i.ParentID == null).OrderBy(i => i.SortOrder))
            {
                var parentData = CalculateItemData(parentItem);
                foreach (var childItem in allPlItems.Where(i => i.ParentID == parentItem.ID).OrderBy(i => i.SortOrder))
                {
                    var childData = CalculateItemData(childItem);
                    parentData.Children.Add(childData);
                    parentData.Actual += childData.Actual;
                    parentData.Plan += childData.Plan;
                }

                parentData.Variance = parentData.Actual - parentData.Plan;
                parentData.VariancePercent = parentData.Plan > 0 ? ((parentData.Variance / parentData.Plan) * 100) : 0;
                plData.Add(parentData);
            }

            return plData;
        }
    }
}
