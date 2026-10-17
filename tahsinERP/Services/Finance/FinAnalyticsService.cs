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

        private static InOut Totals(IQueryable<FIN_TRANSACTIONS> q)
        {
            return q.GroupBy(t => 1)
                .Select(g => new InOut
                {
                    In = g.Sum(t => t.Direction == 1 ? t.Amount : 0m),
                    Out = g.Sum(t => t.Direction == 2 ? t.Amount : 0m)
                })
                .FirstOrDefault() ?? new InOut();
        }

        /// <summary>Naqd (IsCash) va bank (IsBank) faol hisoblari qoldig'i — bitta GROUP BY (FIN-P02).</summary>
        private static void CashAndBankBalances(DBTHSNEntities db, out decimal cash, out decimal bank, out List<FIN_ACCOUNTS> cashAccounts, out List<FIN_ACCOUNTS> bankAccounts)
        {
            cashAccounts = db.FIN_ACCOUNTS.AsNoTracking().Where(a => a.IsCash && a.IsActive).OrderBy(a => a.ID).ToList();
            bankAccounts = db.FIN_ACCOUNTS.AsNoTracking().Where(a => a.IsBank && a.IsActive).OrderBy(a => a.ID).ToList();
            var ids = cashAccounts.Select(a => a.ID).Concat(bankAccounts.Select(a => a.ID)).ToList();
            var balances = FinQueries.AccountBalances(db, ids);
            cash = FinQueries.SumBalance(balances, cashAccounts.Select(a => a.ID));
            bank = FinQueries.SumBalance(balances, bankAccounts.Select(a => a.ID));
        }

        public static CashflowResult Cashflow(DBTHSNEntities db, DateTime fromDate, DateTime toDate, int? accountID)
        {
            var query = FinQueries.Active(db).InPeriod(fromDate, toDate).RealCash();
            if (accountID.HasValue)
            {
                var acc = accountID.Value;
                query = query.Where(t => t.AccountID == acc);
            }

            var totals = Totals(query);
            CashAndBankBalances(db, out var cash, out var bank, out var cashAccounts, out var bankAccounts);

            // Asosiy valyuta: davrdagi eng ko'p ishlatilgani, bo'lmasa birinchi naqd/bank hisobining valyutasi.
            var currencyId = FinQueries.MostUsedCurrencyId(query);
            if (!currencyId.HasValue)
            {
                var firstAccount = cashAccounts.FirstOrDefault() ?? bankAccounts.FirstOrDefault();
                currencyId = firstAccount?.CurrencyID;
            }
            var currency = currencyId.HasValue ? db.CURRENCIES.AsNoTracking().FirstOrDefault(c => c.ID == currencyId.Value) : null;
            currency = currency ?? db.CURRENCIES.AsNoTracking().FirstOrDefault();

            var daily = query
                .GroupBy(t => DbFunctions.TruncateTime(t.TxnDate))
                .Select(g => new
                {
                    Day = g.Key,
                    Inflow = g.Sum(t => t.Direction == 1 ? t.Amount : 0m),
                    Outflow = g.Sum(t => t.Direction == 2 ? t.Amount : 0m)
                })
                .OrderBy(x => x.Day)
                .ToList()
                .Select(x => new DailyFlow { Date = x.Day.Value.ToString("dd.MM.yyyy"), Inflow = x.Inflow, Outflow = x.Outflow })
                .ToList();

            return new CashflowResult
            {
                Inflow = totals.In,
                Outflow = totals.Out,
                CurrentBalance = cash + bank,
                CurrencyShortname = currency?.Shortname ?? "UZS",
                DailyData = daily
            };
        }

        public static OverviewResult FinancialOverview(DBTHSNEntities db, DateTime fromDate, DateTime toDate)
        {
            var query = FinQueries.Active(db).InPeriod(fromDate, toDate).RealCash();
            var totals = Totals(query);
            CashAndBankBalances(db, out var cash, out var bank, out _, out _);

            List<CategoryAmount> TopCategories(byte direction)
            {
                return query
                    .Where(t => t.Direction == direction)
                    .GroupBy(t => new { t.CategoryID, CategoryName = t.FIN_CATEGORIES.Name })
                    .Select(g => new { g.Key.CategoryName, Amount = g.Sum(t => t.Amount) })
                    .OrderByDescending(x => x.Amount)
                    .Take(10)
                    .ToList()
                    .Select(x => new CategoryAmount { CategoryName = x.CategoryName, Amount = x.Amount })
                    .ToList();
            }

            var monthly = query
                .GroupBy(t => new { t.TxnDate.Year, t.TxnDate.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Income = g.Sum(t => t.Direction == 1 ? t.Amount : 0m),
                    Expense = g.Sum(t => t.Direction == 2 ? t.Amount : 0m)
                })
                .ToList()
                .Select(x => new PeriodIncomeExpense { Period = $"{x.Year}-{x.Month:D2}", Income = x.Income, Expense = x.Expense })
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
                CurrencyShortname = FinQueries.PrimaryCurrencyShortname(db, query)
            };
        }

        /// <summary>Kategoriyalar kesimida chiqim (direction=2) yoki kirim (1): summa va soni.</summary>
        public static List<CategoryBreakdownItem> CategoryBreakdown(DBTHSNEntities db, DateTime fromDate, DateTime toDate, int? categoryID, byte direction)
        {
            var query = FinQueries.Active(db).InPeriod(fromDate, toDate).RealCash().Where(t => t.Direction == direction);
            if (categoryID.HasValue)
            {
                var cat = categoryID.Value;
                query = query.Where(t => t.CategoryID == cat);
            }

            return query
                .GroupBy(t => new { t.CategoryID, CategoryName = t.FIN_CATEGORIES.Name, CategoryType = t.FIN_CATEGORIES.Type })
                .Select(g => new CategoryBreakdownItem
                {
                    CategoryID = g.Key.CategoryID,
                    CategoryName = g.Key.CategoryName,
                    CategoryType = g.Key.CategoryType,
                    Amount = g.Sum(t => t.Amount),
                    TransactionCount = g.Count()
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

            currencyShortname = FinQueries.PrimaryCurrencyShortname(db, query);

            return query
                .Where(t => t.FIN_ACCOUNTS.Name != null && t.FIN_ACCOUNTS.Name != "")
                .GroupBy(t => new { t.AccountID, t.FIN_ACCOUNTS.Name, t.FIN_ACCOUNTS.Code, t.FIN_ACCOUNTS.IsCash, t.FIN_ACCOUNTS.IsBank })
                .Select(g => new
                {
                    g.Key.AccountID,
                    g.Key.Name,
                    g.Key.Code,
                    g.Key.IsCash,
                    g.Key.IsBank,
                    In = g.Sum(t => t.Direction == 1 ? t.Amount : 0m),
                    Out = g.Sum(t => t.Direction == 2 ? t.Amount : 0m),
                    Count = g.Count()
                })
                .ToList()
                .Select(x => new AccountUsageData
                {
                    AccountID = x.AccountID,
                    AccountName = x.Name ?? "",
                    AccountCode = x.Code ?? "",
                    AccountType = x.IsCash ? "Naqd pul" : (x.IsBank ? "Bank" : "Boshqa"),
                    TotalInflow = x.In,
                    TotalOutflow = x.Out,
                    TransactionCount = x.Count,
                    NetAmount = x.In - x.Out
                })
                .OrderByDescending(x => Math.Abs(x.NetAmount))
                .ToList();
        }

        /// <summary>Chiqim (2) yoki kirim (1) tahlili: kategoriya, hisob va oy kesimida (D20).</summary>
        public static DirectionAnalysisResult DirectionAnalysis(DBTHSNEntities db, DateTime fromDate, DateTime toDate, byte direction)
        {
            var query = FinQueries.Active(db).Where(t => t.Direction == direction).InPeriod(fromDate, toDate).RealCash();

            var byCategory = query
                .GroupBy(t => new { t.CategoryID, t.FIN_CATEGORIES.Name })
                .Select(g => new { g.Key.Name, Amount = g.Sum(t => t.Amount) })
                .OrderByDescending(x => x.Amount)
                .ToList()
                .Select(x => new NamedAmount { Name = x.Name ?? "", Amount = x.Amount })
                .ToList();

            var byAccount = query
                .GroupBy(t => new { t.AccountID, t.FIN_ACCOUNTS.Name })
                .Select(g => new { g.Key.Name, Amount = g.Sum(t => t.Amount) })
                .OrderByDescending(x => x.Amount)
                .ToList()
                .Select(x => new NamedAmount { Name = x.Name ?? "", Amount = x.Amount })
                .ToList();

            var monthly = query
                .GroupBy(t => new { t.TxnDate.Year, t.TxnDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(t => t.Amount) })
                .ToList()
                .Select(x => new MonthlyData { Period = $"{x.Year}-{x.Month:D2}", Amount = x.Amount })
                .OrderBy(x => x.Period)
                .ToList();

            return new DirectionAnalysisResult
            {
                ByCategory = byCategory,
                ByAccount = byAccount,
                Monthly = monthly,
                Total = byCategory.Sum(x => x.Amount),
                CurrencyShortname = FinQueries.PrimaryCurrencyShortname(db, query)
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

            var sums = FinQueries.Active(db)
                .Where(t => t.TxnDate >= startDate && t.TxnDate <= endDate)
                .RealCash()
                .GroupBy(t => new { t.CategoryID, t.Direction })
                .Select(g => new { g.Key.CategoryID, g.Key.Direction, Amount = g.Sum(t => t.Amount) })
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

            var plans = db.FIN_PL_PLANS
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
