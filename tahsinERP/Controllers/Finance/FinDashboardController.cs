using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Helpers;
using tahsinERP.Controllers;
using System.Linq.Expressions;

namespace tahsinERP.Controllers.Finance
{
    public class FinDashboardController : Controller
    {
        // Olish so'rovi: FinDashboard/Cashflow
        public ActionResult Cashflow(DateTime? fromDate, DateTime? toDate, int? accountID)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
            toDate = toDate ?? DateTime.Now;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = ActiveFinTransactions(db)
                    .Include(t => t.FIN_ACCOUNTS)
                    .Include(t => t.FIN_CATEGORIES)
                    .Include(t => t.CURRENCy)
                    .Where(t => t.TxnDate >= fromDate.Value && t.TxnDate <= toDate.Value)
                    // Exclude liability transactions (PurchaseInvoice va SalesInvoice) - faqat show real cash flow
                    .Where(t => (t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice") || t.SourceDocType == null || t.SourceDocType == "");

                if (accountID.HasValue)
                {
                    query = query.Where(t => t.AccountID == accountID.Value);
                }

                var transactions = query.ToList();

                // Calculate totals
                var inflow = transactions.Where(t => t.Direction == 1).Sum(t => (decimal?)t.Amount) ?? 0m;
                var outflow = transactions.Where(t => t.Direction == 2).Sum(t => (decimal?)t.Amount) ?? 0m;
                var netCashflow = inflow - outflow;

                // Olish current cash balance - faqat dan Cash va Bank accounts
                // Separate calculation uchun Cash (naqd pul) va Bank accounts
                var cashAccounts = db.FIN_ACCOUNTS
                    .Where(a => a.IsCash && a.IsActive)
                    .ToList();

                var bankAccounts = db.FIN_ACCOUNTS
                    .Where(a => a.IsBank && a.IsActive)
                    .ToList();

                // Calculate cash balance (naqd pul)
                var cashBalance = 0m;
                foreach (var account in cashAccounts)
                {
                    var accountBalance = ActiveFinTransactions(db)
                        .Where(t => t.AccountID == account.ID)
                        .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                    cashBalance += accountBalance;
                }

                // Calculate bank balance
                var bankBalance = 0m;
                foreach (var account in bankAccounts)
                {
                    var accountBalance = ActiveFinTransactions(db)
                        .Where(t => t.AccountID == account.ID)
                        .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                    bankBalance += accountBalance;
                }

                // Total current balance = Cash + Bank
                var currentBalance = cashBalance + bankBalance;

                // Olish primary currency (most used currency in transactions, yoki dan cash/bank accounts)
                var primaryCurrency = transactions
                    .Where(t => t.CURRENCy != null)
                    .GroupBy(t => t.CurrencyID)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.First().CURRENCy)
                    .FirstOrDefault();

                // Fallback: get currency dan cash/bank accounts agar no transactions
                if (primaryCurrency == null)
                {
                    var firstCashAccount = cashAccounts.FirstOrDefault() ?? bankAccounts.FirstOrDefault();
                    if (firstCashAccount != null)
                    {
                        primaryCurrency = db.CURRENCIES.Find(firstCashAccount.CurrencyID);
                    }
                }

                // Final fallback: get birinchi currency dan database
                if (primaryCurrency == null)
                {
                    primaryCurrency = db.CURRENCIES.FirstOrDefault();
                }

                var currencyShortname = primaryCurrency?.Shortname ?? "UZS";

                // Group by day
                var dailyData = transactions
                    .GroupBy(t => t.TxnDate.Date)
                    .Select(g => new
                    {
                        DateValue = g.Key, // Keep original date uchun sorting
                        Date = g.Key.ToString("dd.MM.yyyy"), // Format date as string uchun display
                        Inflow = g.Where(t => t.Direction == 1).Sum(t => (decimal?)t.Amount) ?? 0m,
                        Outflow = g.Where(t => t.Direction == 2).Sum(t => (decimal?)t.Amount) ?? 0m
                    })
                    .OrderBy(x => x.DateValue) // Sort by date value
                    .Select(x => new { x.Date, x.Inflow, x.Outflow }) // Remove DateValue dan final result
                    .ToList();

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.AccountID = accountID;
                ViewBag.AccountList = new SelectList(
                    db.FIN_ACCOUNTS.Where(a => a.IsActive).OrderBy(a => a.Name).ToList(),
                    "ID", "Name", accountID);
                ViewBag.Inflow = inflow;
                ViewBag.Outflow = outflow;
                ViewBag.NetCashflow = netCashflow;
                ViewBag.CurrentBalance = currentBalance;
                ViewBag.CurrencyShortname = currencyShortname;
                ViewBag.DailyData = dailyData;

                return View();
            }
        }

        // Olish so'rovi: FinDashboard/PL
        public ActionResult PL(int? year, int? month)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            year = year ?? DateTime.Now.Year;
            month = month ?? DateTime.Now.Month;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var startDate = new DateTime(year.Value, month.Value, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                // Olish transactions uchun the period - exclude liability transactions (faqat real P&L)
                var transactions = ActiveFinTransactions(db)
                    .Include(t => t.FIN_CATEGORIES)
                    .Where(t => t.TxnDate >= startDate && t.TxnDate <= endDate)
                    // Exclude liability transactions (PurchaseInvoice va SalesInvoice) - faqat show real transactions
                    .Where(t => (t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice") || t.SourceDocType == null || t.SourceDocType == "")
                    .ToList();

                // Olish barcha PL items bilan their mapped categories
                var allPlItems = db.FIN_PL_ITEMS
                    .Include(i => i.FIN_PL_ITEMS1)
                    .Where(i => i.IsActive)
                    .OrderBy(i => i.SortOrder)
                    .ToList();

                // Olish barcha plans uchun the period
                var plans = db.FIN_PL_PLANS
                    .Where(p => p.Year == year.Value && p.Month == month.Value)
                    .ToDictionary(p => p.ItemID, p => p.AmountPlan);

                // yordamchi method ga calculate PL item ma'lumot
                PLItemData CalculateItemData(FIN_PL_ITEMS item)
                {
                    // Olish categories mapped ga this PL item
                    var categoryIds = db.FIN_CATEGORIES
                        .Where(c => c.FIN_PL_ITEMS.Any(pi => pi.ID == item.ID))
                        .Select(c => c.ID)
                        .ToList();

                    // Calculate actual amount
                    var actual = transactions
                        .Where(t => categoryIds.Contains(t.CategoryID))
                        .Sum(t => (decimal?)(item.Sign == 1 ? (t.Direction == 1 ? t.Amount : -t.Amount) : (t.Direction == 2 ? t.Amount : -t.Amount))) ?? 0m;

                    // Olish plan
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

                // Build hierarchical structure - faqat get parent items (ParentID == null)
                var plData = new List<PLItemData>();
                var parentItems = allPlItems.Where(i => i.ParentID == null).OrderBy(i => i.SortOrder).ToList();

                foreach (var parentItem in parentItems)
                {
                    var parentData = CalculateItemData(parentItem);
                    
                    // Olish va calculate children
                    var childItems = allPlItems.Where(i => i.ParentID == parentItem.ID).OrderBy(i => i.SortOrder).ToList();
                    foreach (var childItem in childItems)
                    {
                        var childData = CalculateItemData(childItem);
                        parentData.Children.Add(childData);
                        
                        // Qo'shish child's actual ga parent's actual (uchun summary)
                        parentData.Actual += childData.Actual;
                        parentData.Plan += childData.Plan;
                    }
                    
                    // Recalculate variance after adding children
                    parentData.Variance = parentData.Actual - parentData.Plan;
                    parentData.VariancePercent = parentData.Plan > 0 ? ((parentData.Variance / parentData.Plan) * 100) : 0;
                    
                    plData.Add(parentData);
                }

                ViewBag.Year = year;
                ViewBag.Month = month;
                var years = Enumerable.Range(DateTime.Now.Year - 5, 11).ToList();
                ViewBag.YearList = new SelectList(years, year);
                var months = Enumerable.Range(1, 12).Select(m => new { Value = m, Text = new DateTime(2000, m, 1).ToString("MMMM") }).ToList();
                ViewBag.MonthList = new SelectList(months, "Value", "Text", month);
                ViewBag.PLData = plData;

                return View();
            }
        }

        // Olish so'rovi: FinDashboard/BalanceSheet
        public ActionResult BalanceSheet(DateTime? asOfDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            asOfDate = asOfDate ?? DateTime.Now;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Olish balance categories
                var categories = db.FIN_BALANCE_CATEGORIES
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .ToList();

                // Faqat opening snapshotni bazaga olamiz.
                // Qolgan snapshotlar balansni freeze qilmasin; tizim har doim dinamik hisoblaydi.
                var openingSnapshots = db.FIN_BALANCE_SNAPSHOTS
                    .Where(s => s.SnapshotDate <= asOfDate.Value && s.SourceType == "OpeningBalance")
                    .GroupBy(s => s.CategoryID)
                    .Select(g => new
                    {
                        CategoryID = g.Key,
                        Latest = g.OrderByDescending(s => s.SnapshotDate).FirstOrDefault()
                    })
                    .ToDictionary(
                        x => x.CategoryID,
                        x => new CategorySnapshotInfo
                        {
                            Amount = x.Latest.Amount,
                            SnapshotDate = x.Latest.SnapshotDate,
                            IsOpening = true
                        });

                // Calculate dan transactions va accounts
                // Map accounts ga balance categories by name matching
                var accounts = db.FIN_ACCOUNTS.Where(a => a.IsActive).ToList();
                var accountBalances = new Dictionary<int, decimal>();
                
                foreach (var account in accounts)
                {
                    var balance = ActiveFinTransactions(db)
                        .Where(t => t.AccountID == account.ID && t.TxnDate <= asOfDate.Value)
                        .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                    accountBalances[account.ID] = balance;
                }

                // Yaratish mapping: Account -> Balance Category (bo'yicha name matching)
                // uchun proper implementation, you should yaratish a mapping jadval like FIN_ACCOUNT_BALANCE_CATEGORIES
                var accountToBalanceCategory = new Dictionary<int, int>();
                foreach (var account in accounts)
                {
                    var matchingCategory = categories.FirstOrDefault(c => 
                        (c.Name.Contains(account.Name) || account.Name.Contains(c.Name)) ||
                        (c.Name == "Naqd pul" && account.IsCash) ||
                        (c.Name == "Bank hisobi" && account.IsBank)
                    );
                    if (matchingCategory != null)
                    {
                        accountToBalanceCategory[account.ID] = matchingCategory.ID;
                    }
                }

                // Calculate balance ma'lumot based on CalculationType
                // 1 = Account-Based: Calculate dan account balances
                // 2 = Transaction-Based: Calculate dan transaction categories
                // 3 = hujjat-Based: Calculate dan hujjatlar (invoices, etc.)
                var balanceData = new List<BalanceCategoryData>();
                foreach (var category in categories)
                {
                    decimal amount = 0;
                    DateTime? openingDate = null;
                    if (openingSnapshots.TryGetValue(category.ID, out var openingSnapshotInfo))
                    {
                        amount = openingSnapshotInfo.Amount;
                        openingDate = openingSnapshotInfo.SnapshotDate.Date;
                    }

                        // Calculate based on CalculationType
                        switch (category.CalculationType)
                        {
                            case 1: // Account-Based
                                // Ta'minotchilardan qarzlar: hujjat asosida (P_Invoices − to'lovlar, FIFO)
                                // To'lovlar = FinTransactions (Direction=2, CounterpartyType=2 ta'minotchi) — hisobda emas, tranzaksiyada
                                if (category.Name == "Ta'minotchilardan qarzlar")
                                {
                                    var supplierSummary = CalculateSupplierOutstanding(db, asOfDate.Value, openingDate);
                                    amount += supplierSummary.Sum(s => s.Outstanding);
                                }
                                else if (category.Name == "Xaridorlardan haqlarimiz")
                                {
                                    var customerSummary = CalculateCustomerOutstanding(db, asOfDate.Value, openingDate);
                                    amount += customerSummary.Sum(c => c.Outstanding);
                                }
                                else
                                {
                                    var mappedAccountIds = db.FIN_ACCOUNTS
                                        .Where(a => a.BalanceCategoryID == category.ID && a.IsActive)
                                        .Select(a => a.ID)
                                        .ToList();
                                    if (mappedAccountIds.Count == 0)
                                    {
                                        mappedAccountIds = accountToBalanceCategory
                                            .Where(kvp => kvp.Value == category.ID)
                                            .Select(kvp => kvp.Key)
                                            .ToList();
                                    }
                                    if (mappedAccountIds.Count > 0)
                                    {
                                        var movement = ActiveFinTransactions(db)
                                            .Where(t => mappedAccountIds.Contains(t.AccountID) &&
                                                        t.TxnDate <= asOfDate.Value &&
                                                        (!openingDate.HasValue || t.TxnDate > openingDate.Value))
                                            .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                                        amount += movement;
                                    }
                                }
                                break;
                                
                            case 2: // Transaction-Based
                                // Olish categories mapped ga this balance category
                                var categoryIds = new List<int>();
                                try
                                {
                                    var categoryWithMappings = db.FIN_BALANCE_CATEGORIES
                                        .Include(c => c.FIN_CATEGORIES)
                                        .FirstOrDefault(c => c.ID == category.ID);
                                    
                                    if (categoryWithMappings?.FIN_CATEGORIES != null)
                                    {
                                        categoryIds = categoryWithMappings.FIN_CATEGORIES
                                            .Where(c => c.IsActive)
                                            .Select(c => c.ID)
                                            .ToList();
                                    }
                                }
                                catch
                                {
                                    // agar relationship doesn't work, use raw SQL ga query junction jadval
                                    try
                                    {
                                        var sql = @"
                                            SELECT CategoryID 
                                            FROM FIN_BALANCE_CATEGORY_CATEGORIES 
                                            WHERE BalanceCategoryID = {0}";
                                        var categoryIdList = db.Database.SqlQuery<int>(sql, category.ID).ToList();
                                        
                                        // Olish faol categories dan the junction jadval results
                                        categoryIds = db.FIN_CATEGORIES
                                            .Where(c => categoryIdList.Contains(c.ID) && c.IsActive)
                                            .Select(c => c.ID)
                                            .ToList();
                                    }
                                    catch
                                    {
                                        // agar junction jadval doesn't exist yoki query fails, return empty list
                                        categoryIds = new List<int>();
                                    }
                                }
                                
                                if (categoryIds.Count > 0)
                                {
                                    // Calculate dan transactions in mapped categories
                                    amount += ActiveFinTransactions(db)
                                        .Where(t => categoryIds.Contains(t.CategoryID) && 
                                                   t.TxnDate <= asOfDate.Value &&
                                                   (!openingDate.HasValue || t.TxnDate > openingDate.Value))
                                        .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                                }
                                break;
                                
                            case 3: // hujjat-Based
                                // Calculate dan hujjatlar based on category name
                                // NOTE: "Ta'minotchilardan qarzlar" uchun Account-Based (CalculationType = 1) ishlatish tavsiya etiladi
                                // Bu ERP standartlariga mos (SAP, Oracle, Microsoft Dynamics)
                                // Account-Based: Payables_5 account qoldig'i ga'g'ridan-ga'g'ri ishlatiladi
                                if (category.Name == "Ta'minotchilardan qarzlar")
                                {
                                    // Yechim 2: Account-Based ishlatish (hujjat-Based o'rniga)
                                    // Avval Account-Based orqali hisoblashga harakat qilamiz
                                    var payablesAccountIds = db.FIN_ACCOUNTS
                                        .Where(a => a.BalanceCategoryID == category.ID && a.IsActive)
                                        .Select(a => a.ID)
                                        .ToList();
                                    
                                    if (payablesAccountIds.Count > 0)
                                    {
                                        // Account-Based: Account qoldiqlarini yig'ish
                                        foreach (var accountId in payablesAccountIds)
                                        {
                                            if (accountBalances.ContainsKey(accountId))
                                            {
                                                // opening bo'lsa faqat opening sanasidan keyingi harakatlar
                                                if (openingDate.HasValue)
                                                {
                                                    var mv = ActiveFinTransactions(db)
                                                        .Where(t => t.AccountID == accountId &&
                                                                    t.TxnDate <= asOfDate.Value &&
                                                                    t.TxnDate > openingDate.Value)
                                                        .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                                                    amount += mv;
                                                }
                                                else
                                                {
                                                    amount += accountBalances[accountId];
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Fallback: hujjat-Based (eski usul, Account-Based sozlanmagan bo'lsa)
                                        // ga'lanmagan xarid invoyslari
                                        var unpaidInvoices = db.P_INVOICES
                                            .Where(i => i.InvoiceDate <= asOfDate.Value && 
                                                       (!openingDate.HasValue || i.InvoiceDate > openingDate.Value) &&
                                                       (i.IsDeleted == null || i.IsDeleted == false))
                                            .ToList();
                                        
                                        foreach (var invoice in unpaidInvoices)
                                        {
                                            // Calculate total payments uchun this invoice
                                            var totalPaid = ActiveFinTransactions(db)
                                                .Where(t => t.SourceDocType == "PurchaseInvoice" && 
                                                           t.SourceDocID == invoice.ID && 
                                                           t.TxnDate <= asOfDate.Value &&
                                                           (!openingDate.HasValue || t.TxnDate > openingDate.Value) &&
                                                           t.Direction == 1) // Payments (incoming ga supplier)
                                                .Sum(t => (decimal?)t.Amount) ?? 0m;
                                            
                                            var invoiceAmount = (decimal)invoice.Amount;
                                            
                                            // agar not fully paid, add unpaid amount
                                            if (totalPaid < invoiceAmount)
                                            {
                                                amount += (invoiceAmount - totalPaid);
                                            }
                                        }
                                    }
                                }
                                else if (category.Name == "Oyliklardan qarzimiz")
                                {
                                    // Approved payroll total minus salary expense transactions (category "Oylik")
                                    var payrollTotal = db.HR_PAYROLL_LINES
                                        .Where(l => (l.IsDeleted == null || l.IsDeleted == false) &&
                                                   l.HR_PAYROLL_HEADERS != null &&
                                                   (l.HR_PAYROLL_HEADERS.IsDeleted == null || l.HR_PAYROLL_HEADERS.IsDeleted == false) &&
                                                   (l.HR_PAYROLL_HEADERS.Status == "Approved" || l.HR_PAYROLL_HEADERS.Status == "Posted") &&
                                                   l.HR_PAYROLL_HEADERS.ApprovedDT.HasValue &&
                                                   l.HR_PAYROLL_HEADERS.ApprovedDT.Value <= asOfDate.Value &&
                                                   (!openingDate.HasValue || l.HR_PAYROLL_HEADERS.ApprovedDT.Value > openingDate.Value))
                                        .Sum(l => (decimal?)l.NetSalary) ?? 0m;

                                    var salaryCategoryIds = db.FIN_CATEGORIES
                                        .Where(c => c.IsActive && c.Name == "Oylik")
                                        .Select(c => c.ID)
                                        .ToList();

                                    var salaryExpenses = 0m;
                                    if (salaryCategoryIds.Count > 0)
                                    {
                                        salaryExpenses = ActiveFinTransactions(db)
                                            .Where(t => salaryCategoryIds.Contains(t.CategoryID) &&
                                                       t.Direction == 2 &&
                                                       t.TxnDate <= asOfDate.Value &&
                                                       (!openingDate.HasValue || t.TxnDate > openingDate.Value))
                                            .Sum(t => (decimal?)t.Amount) ?? 0m;
                                    }

                                    var payrollNet = payrollTotal - salaryExpenses;
                                    if (payrollNet < 0)
                                    {
                                        payrollNet = 0m;
                                    }
                                    amount += payrollNet;
                                }
                                else if (category.Name == "Xaridorlardan haqlarimiz")
                                {
                                    // Receivables: sales invoyslar - mijozdan tushumlar
                                    var customerSummary = CalculateCustomerOutstanding(db, asOfDate.Value, openingDate);
                                    amount += customerSummary.Sum(c => c.Outstanding);
                                }
                                else if (category.Name != null &&
                                         category.Name.ToLower().Contains("logistika") &&
                                         category.Name.ToLower().Contains("qarz"))
                                {
                                    var totalWaybillAmount = db.F_WAYBILLS
                                        .Where(w => w.IsDeleted == false && w.Amount.HasValue &&
                                                    (!openingDate.HasValue || w.IssueDate > openingDate.Value) &&
                                                    w.IssueDate <= asOfDate.Value)
                                        .Sum(w => (double?)w.Amount) ?? 0;

                                    var categoryNames = new[] { "m.tash", "t.tash" };
                                    var logisticsCategoryIds = db.FIN_CATEGORIES
                                        .Where(c => c.IsActive && categoryNames.Contains(c.Name.ToLower()))
                                        .Select(c => c.ID)
                                        .ToList();

                                    var paidAmount = 0m;
                                    if (logisticsCategoryIds.Count > 0)
                                    {
                                        paidAmount = ActiveFinTransactions(db)
                                            .Where(t => logisticsCategoryIds.Contains(t.CategoryID) &&
                                                        t.TxnDate <= asOfDate.Value &&
                                                        (!openingDate.HasValue || t.TxnDate > openingDate.Value) &&
                                                        (t.IsDeleted == null || t.IsDeleted == false) &&
                                                        t.Direction == 2)
                                            .Sum(t => (decimal?)t.Amount) ?? 0m;
                                    }

                                    amount += (decimal)totalWaybillAmount - paidAmount;
                                }
                                else if (category.Name == "Doimiy harajatlar qarzi")
                                {
                                    // Doimiy harajatlar: har oy shakllanadigan qarz, to'lovlar orqali kamayadi
                                    try
                                    {
                                        var obligations = db.Database.SqlQuery<RecurringObligationRow>(
                                            "SELECT ID, Name, MonthlyAmount, CurrencyID, StartYear, StartMonth FROM FIN_RECURRING_OBLIGATIONS WHERE (IsDeleted = 0 OR IsDeleted IS NULL) AND IsActive = 1 AND BalanceCategoryID = @p0",
                                            category.ID).ToList();
                                        foreach (var ob in obligations)
                                        {
                                            var startDate = new DateTime(ob.StartYear, ob.StartMonth, 1);
                                            var endDate = asOfDate.Value.Date;
                                            var effectiveStart = openingDate.HasValue && openingDate.Value > startDate
                                                ? openingDate.Value
                                                : startDate;
                                            if (endDate < effectiveStart) continue;
                                            var monthsCount = ((endDate.Year - effectiveStart.Year) * 12) + (endDate.Month - effectiveStart.Month) + 1;
                                            if (monthsCount < 1) continue;
                                            var accrued = ob.MonthlyAmount * monthsCount;
                                            var paid = ActiveFinTransactions(db)
                                                .Where(t => t.SourceDocType == "RecurringObligationPayment" &&
                                                            t.SourceDocID == ob.ID &&
                                                            t.TxnDate <= asOfDate.Value &&
                                                            (!openingDate.HasValue || t.TxnDate > openingDate.Value) &&
                                                            (t.IsDeleted == null || t.IsDeleted == false))
                                                .Sum(t => (decimal?)t.Amount) ?? 0m;
                                            var net = accrued - paid;
                                            if (net > 0) amount += net;
                                        }
                                    }
                                    catch
                                    {
                                        // Jadval mavjud emas yoki xato bo'lsa 0
                                    }
                                }
                                break;
                        }

                    balanceData.Add(new BalanceCategoryData
                    {
                        Category = category,
                        Amount = amount,
                        IsAssetColumn = BalanceCategoryDisplayHelper.IsAssetColumn(category)
                    });
                }

                ViewBag.AsOfDate = asOfDate;
                ViewBag.BalanceData = balanceData;

                return View();
            }
        }

        // Olish so'rovi: FinDashboard/BalanceSheetLiabilityDetails
        public ActionResult BalanceSheetLiabilityDetails(DateTime? asOfDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            asOfDate = asOfDate ?? DateTime.Now;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var details = BuildBalanceCategoryDetails(db, asOfDate.Value, new[] { 2, 3 }, false);

                ViewBag.AsOfDate = asOfDate;
                ViewBag.LiabilityDetails = details;
                ViewBag.TotalAmount = details.Sum(d => d.Amount);

                return View();
            }
        }

        // Olish so'rovi: FinDashboard/BalanceSheetAssetDetails
        public ActionResult BalanceSheetAssetDetails(DateTime? asOfDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            asOfDate = asOfDate ?? DateTime.Now;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Type=2 bo'lib qolgan operatsion aktivlar ham kirishi uchun 1 va 2 ni beramiz.
                var details = BuildBalanceCategoryDetails(db, asOfDate.Value, new[] { 1, 2 }, true);

                ViewBag.AsOfDate = asOfDate;
                ViewBag.AssetDetails = details;
                ViewBag.TotalAmount = details.Sum(d => d.Amount);

                return View();
            }
        }

        [HttpGet]
        public JsonResult BalanceCategoryMovements(int categoryId, DateTime asOfDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinDashboard"))
            {
                return Json(new { success = false, message = "Ruxsat yo'q" }, JsonRequestBehavior.AllowGet);
            }

            using (var db = new DBTHSNEntities())
            {
                var category = db.FIN_BALANCE_CATEGORIES.FirstOrDefault(c => c.ID == categoryId && c.IsActive);
                if (category == null)
                {
                    return Json(new { success = false, message = "Kategoriya topilmadi" }, JsonRequestBehavior.AllowGet);
                }

                var openingDate = db.FIN_BALANCE_SNAPSHOTS
                    .Where(s => s.CategoryID == categoryId && s.SourceType == "OpeningBalance" && s.SnapshotDate <= asOfDate)
                    .OrderByDescending(s => s.SnapshotDate)
                    .Select(s => (DateTime?)s.SnapshotDate)
                    .FirstOrDefault();

                var fromExclusive = openingDate;
                var top = 20;

                if (category.Name == "Ta'minotchilardan qarzlar")
                {
                    var materialCategoryIds = db.FIN_CATEGORIES
                        .Where(c => c.IsActive && c.Type == 2 && c.Name.ToLower().Contains("material"))
                        .Select(c => c.ID)
                        .ToList();

                    // P_INVOICES.Amount — double?; (decimal) cast SQL ga tarjima qilinmaydi — avval ToList, keyin memoryda cast.
                    var added = db.P_INVOICES
                        .Include(i => i.SUPPLIER)
                        .Where(i => i.InvoiceDate <= asOfDate &&
                                    (!fromExclusive.HasValue || i.InvoiceDate > fromExclusive.Value) &&
                                    (i.IsDeleted == null || i.IsDeleted == false))
                        .OrderByDescending(i => i.InvoiceDate)
                        .Take(top)
                        .ToList()
                        .Select(i => new
                        {
                            date = i.InvoiceDate,
                            doc = i.InvoiceNo,
                            amount = (decimal)(i.Amount ?? 0d),
                            note = i.SUPPLIER != null ? i.SUPPLIER.Name : ""
                        })
                        .ToList();

                    var subtracted = ActiveFinTransactions(db)
                        .Where(t => t.Direction == 2 &&
                                    t.TxnDate <= asOfDate &&
                                    (!fromExclusive.HasValue || t.TxnDate > fromExclusive.Value) &&
                                    ((t.CounterpartyType == 2 && t.CounterpartyID.HasValue) ||
                                     materialCategoryIds.Contains(t.CategoryID)))
                        .OrderByDescending(t => t.TxnDate)
                        .Take(top)
                        .Select(t => new
                        {
                            date = t.TxnDate,
                            doc = t.SourceDocType ?? "FinTransaction",
                            amount = t.Amount,
                            note = t.Purpose ?? t.Comment
                        })
                        .ToList();

                    return Json(new { success = true, added, subtracted }, JsonRequestBehavior.AllowGet);
                }

                if (category.Name == "Xaridorlardan haqlarimiz")
                {
                    var added = db.SALES_INVOICES
                        .Where(i => i.InvoiceDate <= asOfDate &&
                                    (!fromExclusive.HasValue || i.InvoiceDate > fromExclusive.Value))
                        .OrderByDescending(i => i.InvoiceDate)
                        .Take(top)
                        .Select(i => new
                        {
                            date = i.InvoiceDate,
                            doc = i.InvoiceNo,
                            amount = i.TotalAmount,
                            note = (i.CUSTOMER != null ? i.CUSTOMER.Name : "")
                        })
                        .ToList();

                    var salesCategoryIds = db.FIN_CATEGORIES
                        .Where(c => c.IsActive && c.Type == 1 && c.Name.Contains("Sotuv"))
                        .Select(c => c.ID)
                        .ToList();

                    var subtracted = ActiveFinTransactions(db)
                        .Where(t => t.Direction == 1 &&
                                    t.TxnDate <= asOfDate &&
                                    (!fromExclusive.HasValue || t.TxnDate > fromExclusive.Value) &&
                                    t.SourceDocType != "SalesInvoice" &&
                                    ((t.CounterpartyType == 1 && t.CounterpartyID.HasValue) ||
                                     salesCategoryIds.Contains(t.CategoryID)))
                        .OrderByDescending(t => t.TxnDate)
                        .Take(top)
                        .Select(t => new
                        {
                            date = t.TxnDate,
                            doc = t.SourceDocType ?? "FinTransaction",
                            amount = t.Amount,
                            note = t.Purpose ?? t.Comment
                        })
                        .ToList();

                    return Json(new { success = true, added, subtracted }, JsonRequestBehavior.AllowGet);
                }

                var mappedCategoryIds = db.FIN_CATEGORIES
                    .Where(fc => fc.IsActive && fc.FIN_BALANCE_CATEGORIES.Any(b => b.ID == categoryId))
                    .Select(fc => fc.ID)
                    .ToList();
                var mappedAccountIds = db.FIN_ACCOUNTS
                    .Where(a => a.IsActive && a.BalanceCategoryID == categoryId)
                    .Select(a => a.ID)
                    .ToList();

                var baseQuery = ActiveFinTransactions(db)
                    .Where(t => t.TxnDate <= asOfDate &&
                                (!fromExclusive.HasValue || t.TxnDate > fromExclusive.Value) &&
                                (mappedCategoryIds.Contains(t.CategoryID) || mappedAccountIds.Contains(t.AccountID)));

                var defaultAdded = baseQuery
                    .Where(t => t.Direction == 1)
                    .OrderByDescending(t => t.TxnDate)
                    .Take(top)
                    .Select(t => new { date = t.TxnDate, doc = t.SourceDocType ?? "FinTransaction", amount = t.Amount, note = t.Purpose ?? t.Comment })
                    .ToList();
                var defaultSubtracted = baseQuery
                    .Where(t => t.Direction != 1)
                    .OrderByDescending(t => t.TxnDate)
                    .Take(top)
                    .Select(t => new { date = t.TxnDate, doc = t.SourceDocType ?? "FinTransaction", amount = t.Amount, note = t.Purpose ?? t.Comment })
                    .ToList();

                return Json(new { success = true, added = defaultAdded, subtracted = defaultSubtracted }, JsonRequestBehavior.AllowGet);
            }
        }

        // Olish so'rovi: FinDashboard/ShareholderValue
        public ActionResult ShareholderValue(DateTime? asOfDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Shareholder"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            asOfDate = asOfDate ?? DateTime.Now;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Calculate company value
                var companyValue = CalculateCompanyValue(db, asOfDate.Value);

                // Olish shareholders bilan their current shares
                var shareholders = db.SHAREHOLDERS
                    .Where(s => s.IsActive)
                    .ToList();

                var shareholderValues = new List<ShareholderValueData>();
                foreach (var shareholder in shareholders)
                {
                    var currentShare = db.SHAREHOLDER_SHARE_PERIODS
                        .Where(sp => sp.ShareholderID == shareholder.ID &&
                            sp.FromDate <= asOfDate.Value &&
                            (sp.ToDate == null || sp.ToDate >= asOfDate.Value))
                        .OrderByDescending(sp => sp.FromDate)
                        .FirstOrDefault();

                    if (currentShare != null)
                    {
                        shareholderValues.Add(new ShareholderValueData
                        {
                            Shareholder = shareholder,
                            SharePercent = currentShare.SharePercent,
                            Value = companyValue * currentShare.SharePercent / 100
                        });
                    }
                }

                ViewBag.AsOfDate = asOfDate;
                ViewBag.CompanyValue = companyValue;
                ViewBag.ShareholderValues = shareholderValues;

                return View();
            }
        }

        private decimal CalculateCompanyValue(DBTHSNEntities db, DateTime asOfDate)
        {
            decimal value = 0;

            // Cash va Bank
            var cashAccounts = db.FIN_ACCOUNTS
                .Where(a => (a.IsCash || a.IsBank) && a.IsActive)
                .ToList();

            foreach (var account in cashAccounts)
            {
                var balance = ActiveFinTransactions(db)
                    .Where(t => t.AccountID == account.ID && t.TxnDate <= asOfDate)
                    .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                value += balance;
            }

            // Inventory (simplified - you may need ga calculate dan PRODUCT_STOCKS)
            // This is a placeholder

            // Receivables dan Sales Invoices (simplified - tekshirish agar invoice is paid)
            var receivables = db.SALES_INVOICES
                .Where(i => i.InvoiceDate <= asOfDate && (i.StatusCode == null || i.StatusCode != "Paid"))
                .ToList()
                .Sum(i => (decimal?)i.TotalAmount) ?? 0m;
            value += receivables;

            // Subtract Payables (simplified calculation)
            // Note: P_INVOICES.Amount is double, so we need ga convert ga decimal
            var payables = db.P_INVOICES
                .Where(i => i.InvoiceDate <= asOfDate && (i.IsDeleted == null || i.IsDeleted == false))
                .ToList()
                .Sum(i => (decimal?)(i.Amount ?? 0d)) ?? 0m;
            value -= payables;

            return value;
        }

        private static string GetCalculationTypeName(int? calculationType)
        {
            switch (calculationType)
            {
                case 1:
                    return "Account-Based";
                case 2:
                    return "Transaction-Based";
                case 3:
                    return "Document-Based";
                default:
                    return "Noma'lum";
            }
        }

        private static void PopulateTransactionStats(IQueryable<FIN_TRANSACTIONS> query, BalanceCategoryDetailData detail)
        {
            if (detail == null)
            {
                return;
            }

            detail.TransactionCount = query.Count();
            detail.AddedAmount = query.Where(t => t.Direction == 1).Sum(t => (decimal?)t.Amount) ?? 0m;
            detail.SubtractedAmount = query.Where(t => t.Direction != 1).Sum(t => (decimal?)t.Amount) ?? 0m;
        }

        private List<BalanceCategoryDetailData> BuildBalanceCategoryDetails(DBTHSNEntities db, DateTime asOfDate, int[] types, bool includeAssetColumn)
        {
            var typeSet = new HashSet<int>(types ?? new int[0]);
            var categories = db.FIN_BALANCE_CATEGORIES
                .Where(c => c.IsActive && typeSet.Contains(c.Type))
                .OrderBy(c => c.SortOrder)
                .ToList();

            var openingSnapshots = db.FIN_BALANCE_SNAPSHOTS
                .Where(s => s.SnapshotDate <= asOfDate && s.SourceType == "OpeningBalance")
                .GroupBy(s => s.CategoryID)
                .Select(g => new
                {
                    CategoryID = g.Key,
                    Latest = g.OrderByDescending(s => s.SnapshotDate).FirstOrDefault()
                })
                .ToDictionary(
                    x => x.CategoryID,
                    x => new CategorySnapshotInfo
                    {
                        Amount = x.Latest.Amount,
                        SnapshotDate = x.Latest.SnapshotDate,
                        IsOpening = true
                    });

            var accounts = db.FIN_ACCOUNTS.Where(a => a.IsActive).ToList();
            var accountBalances = new Dictionary<int, decimal>();
            foreach (var account in accounts)
            {
                var balance = ActiveFinTransactions(db)
                    .Where(t => t.AccountID == account.ID && t.TxnDate <= asOfDate)
                    .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                accountBalances[account.ID] = balance;
            }

            var accountToBalanceCategory = new Dictionary<int, int>();
            foreach (var account in accounts)
            {
                var matchingCategory = categories.FirstOrDefault(c =>
                    (c.Name.Contains(account.Name) || account.Name.Contains(c.Name)) ||
                    (c.Name == "Naqd pul" && account.IsCash) ||
                    (c.Name == "Bank hisobi" && account.IsBank)
                );
                if (matchingCategory != null)
                {
                    accountToBalanceCategory[account.ID] = matchingCategory.ID;
                }
            }

            var details = new List<BalanceCategoryDetailData>();
            foreach (var category in categories)
            {
                var isAssetColumn = BalanceCategoryDisplayHelper.IsAssetColumn(category);
                if (includeAssetColumn && !isAssetColumn)
                {
                    continue;
                }
                if (!includeAssetColumn && isAssetColumn)
                {
                    continue;
                }

                var detail = new BalanceCategoryDetailData
                {
                    Category = category,
                    CalculationTypeName = GetCalculationTypeName(category.CalculationType)
                };

                DateTime? openingDate = null;
                if (openingSnapshots.TryGetValue(category.ID, out var openingSnapshotInfo))
                {
                    detail.Amount = openingSnapshotInfo.Amount;
                    openingDate = openingSnapshotInfo.SnapshotDate.Date;
                    detail.Explanation = $"Opening balans ({openingDate.Value:dd.MM.yyyy}) + keyingi harakatlar bo'yicha";
                }

                // Ta'minotchilardan qarzlar: P_Invoices − to'lovlar (FinTransactions Direction=2, CounterpartyType=2), FIFO
                if (category.Name == "Ta'minotchilardan qarzlar")
                {
                    var supplierSummary = CalculateSupplierOutstanding(db, asOfDate, openingDate);
                    detail.Amount += supplierSummary.Sum(s => s.Outstanding);
                    detail.TransactionCount = supplierSummary.Sum(s => s.InvoiceCount + s.PaymentCount);
                    detail.AddedAmount = supplierSummary.Sum(s => s.InvoiceTotal);
                    detail.SubtractedAmount = supplierSummary.Sum(s => s.PaymentTotal);
                    detail.CalculationTypeName = "Document-Based";
                    detail.Explanation = openingDate.HasValue
                        ? $"Opening balans ({openingDate.Value:dd.MM.yyyy}) + invoys/to'lovlar (FIFO)"
                        : "Invoyslar va ta'minotchiga o'tkazilgan to'lovlar (FinTransactions, FIFO) bo'yicha hisoblandi";
                    details.Add(detail);
                    continue;
                }

                if (category.Name == "Xaridorlardan haqlarimiz")
                {
                    var customerSummary = CalculateCustomerOutstanding(db, asOfDate, openingDate);
                    detail.Amount = customerSummary.Sum(c => c.Outstanding);
                    detail.TransactionCount = customerSummary.Sum(c => c.InvoiceCount + c.ReceiptCount);
                    detail.AddedAmount = customerSummary.Sum(c => c.InvoiceTotal);
                    detail.SubtractedAmount = customerSummary.Sum(c => c.ReceiptTotal);
                    detail.CalculationTypeName = "Document-Based";
                    detail.Explanation = "Sotuv invoyslari va mijozdan tushumlar (FIFO) bo'yicha hisoblandi";
                    details.Add(detail);
                    continue;
                }

                switch (category.CalculationType)
                {
                    case 1:
                        var mappedAccountIds = db.FIN_ACCOUNTS
                            .Where(a => a.BalanceCategoryID == category.ID && a.IsActive)
                            .Select(a => a.ID)
                            .ToList();

                        if (mappedAccountIds.Count == 0)
                        {
                            mappedAccountIds = accountToBalanceCategory
                                .Where(kvp => kvp.Value == category.ID)
                                .Select(kvp => kvp.Key)
                                .ToList();
                        }

                        foreach (var accountId in mappedAccountIds)
                        {
                            if (accountBalances.ContainsKey(accountId))
                            {
                                detail.Amount += accountBalances[accountId];
                            }
                        }

                        if (mappedAccountIds.Count > 0)
                        {
                            var accountTxnQuery = ActiveFinTransactions(db)
                                .Where(t => mappedAccountIds.Contains(t.AccountID) &&
                                            t.TxnDate <= asOfDate &&
                                            (!openingDate.HasValue || t.TxnDate > openingDate.Value));
                            PopulateTransactionStats(accountTxnQuery, detail);
                            detail.Amount += accountTxnQuery.Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                            detail.Explanation = openingDate.HasValue
                                ? $"Opening balans + hisob harakatlari: {mappedAccountIds.Count} ta hisob"
                                : $"Hisoblar bo'yicha hisoblandi: {mappedAccountIds.Count} ta hisob";
                        }
                        else
                        {
                            detail.Explanation = "Hisoblar moslanmadi";
                        }
                        break;

                    case 2:
                        var categoryIds = new List<int>();
                        try
                        {
                            var categoryWithMappings = db.FIN_BALANCE_CATEGORIES
                                .Include(c => c.FIN_CATEGORIES)
                                .FirstOrDefault(c => c.ID == category.ID);

                            if (categoryWithMappings?.FIN_CATEGORIES != null)
                            {
                                categoryIds = categoryWithMappings.FIN_CATEGORIES
                                    .Where(c => c.IsActive)
                                    .Select(c => c.ID)
                                    .ToList();
                            }
                        }
                        catch
                        {
                            try
                            {
                                var sql = @"
                                            SELECT CategoryID 
                                            FROM FIN_BALANCE_CATEGORY_CATEGORIES 
                                            WHERE BalanceCategoryID = {0}";
                                var categoryIdList = db.Database.SqlQuery<int>(sql, category.ID).ToList();

                                categoryIds = db.FIN_CATEGORIES
                                    .Where(c => categoryIdList.Contains(c.ID) && c.IsActive)
                                    .Select(c => c.ID)
                                    .ToList();
                            }
                            catch
                            {
                                categoryIds = new List<int>();
                            }
                        }

                        if (categoryIds.Count > 0)
                        {
                            var txnQuery = ActiveFinTransactions(db)
                                .Where(t => categoryIds.Contains(t.CategoryID) &&
                                           t.TxnDate <= asOfDate &&
                                           (!openingDate.HasValue || t.TxnDate > openingDate.Value));
                            detail.Amount += txnQuery
                                .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                            PopulateTransactionStats(txnQuery, detail);
                            detail.Explanation = openingDate.HasValue
                                ? $"Opening balans + kategoriyalar harakati: {categoryIds.Count} ta kategoriya"
                                : $"Kategoriyalar bo'yicha hisoblandi: {categoryIds.Count} ta kategoriya";
                        }
                        else
                        {
                            detail.Explanation = "Kategoriya moslanmadi";
                        }
                        break;

                    case 3:
                        if (category.Name == "Ta'minotchilardan qarzlar")
                        {
                            var supplierSummary = CalculateSupplierOutstanding(db, asOfDate);
                            detail.Amount = supplierSummary.Sum(s => s.Outstanding);
                            detail.TransactionCount = supplierSummary.Sum(s => s.InvoiceCount + s.PaymentCount);
                            detail.AddedAmount = supplierSummary.Sum(s => s.InvoiceTotal);
                            detail.SubtractedAmount = supplierSummary.Sum(s => s.PaymentTotal);
                            detail.Explanation = "Invoyslar va to'lovlar bo'yicha (allocation/FIFO)";
                        }
                        else if (category.Name == "Oyliklardan qarzimiz")
                        {
                            var payrollTotal = db.HR_PAYROLL_LINES
                                .Where(l => (l.IsDeleted == null || l.IsDeleted == false) &&
                                           l.HR_PAYROLL_HEADERS != null &&
                                           (l.HR_PAYROLL_HEADERS.IsDeleted == null || l.HR_PAYROLL_HEADERS.IsDeleted == false) &&
                                           (l.HR_PAYROLL_HEADERS.Status == "Approved" || l.HR_PAYROLL_HEADERS.Status == "Posted") &&
                                           l.HR_PAYROLL_HEADERS.ApprovedDT.HasValue &&
                                           l.HR_PAYROLL_HEADERS.ApprovedDT.Value <= asOfDate &&
                                           (!openingDate.HasValue || l.HR_PAYROLL_HEADERS.ApprovedDT.Value > openingDate.Value))
                                .Sum(l => (decimal?)l.NetSalary) ?? 0m;

                            var salaryCategoryIds = db.FIN_CATEGORIES
                                .Where(c => c.IsActive && c.Name == "Oylik")
                                .Select(c => c.ID)
                                .ToList();

                            var salaryExpenses = 0m;
                            var salaryTxnQuery = GetPayrollPaymentQuery(
                                db,
                                salaryCategoryIds,
                                openingDate.HasValue ? openingDate.Value.AddDays(1) : DateTime.MinValue,
                                asOfDate);
                            salaryExpenses = salaryTxnQuery.Sum(t => (decimal?)t.Amount) ?? 0m;
                            PopulateTransactionStats(salaryTxnQuery, detail);

                            detail.Amount += payrollTotal - salaryExpenses;
                            if (detail.Amount < 0)
                            {
                                detail.Amount = 0m;
                            }

                            detail.Explanation = $"Oyliklar bo'yicha: hisoblangan {payrollTotal:N2}, to'langan {salaryExpenses:N2}";
                        }
                        else if (category.Name == "Xaridorlardan haqlarimiz")
                        {
                            var customerSummary = CalculateCustomerOutstanding(db, asOfDate, openingDate);
                            detail.Amount = customerSummary.Sum(c => c.Outstanding);
                            detail.TransactionCount = customerSummary.Sum(c => c.InvoiceCount + c.ReceiptCount);
                            detail.AddedAmount = customerSummary.Sum(c => c.InvoiceTotal);
                            detail.SubtractedAmount = customerSummary.Sum(c => c.ReceiptTotal);
                            detail.Explanation = "Sotuv invoyslari va mijozdan tushumlar (FIFO) bo'yicha hisoblandi";
                        }
                        else if (category.Name != null &&
                                 category.Name.ToLower().Contains("logistika") &&
                                 category.Name.ToLower().Contains("qarz"))
                        {
                            var totalWaybillAmount = db.F_WAYBILLS
                                .Where(w => w.IsDeleted == false && w.Amount.HasValue &&
                                            w.IssueDate <= asOfDate)
                                .Sum(w => (double?)w.Amount) ?? 0;

                            var categoryNames = new[] { "m.tash", "t.tash" };
                            var logisticsCategoryIds = db.FIN_CATEGORIES
                                .Where(c => c.IsActive && categoryNames.Contains(c.Name.ToLower()))
                                .Select(c => c.ID)
                                .ToList();

                            var paidAmount = 0m;
                            if (logisticsCategoryIds.Count > 0)
                            {
                                var logisticsTxnQuery = ActiveFinTransactions(db)
                                    .Where(t => logisticsCategoryIds.Contains(t.CategoryID) &&
                                                t.TxnDate <= asOfDate &&
                                                (t.IsDeleted == null || t.IsDeleted == false));
                                paidAmount = logisticsTxnQuery
                                    .Where(t => t.Direction == 2)
                                    .Sum(t => (decimal?)t.Amount) ?? 0m;
                                PopulateTransactionStats(logisticsTxnQuery, detail);
                            }

                            detail.Amount = (decimal)totalWaybillAmount - paidAmount;
                            detail.Explanation = $"Yo'l varaqalari bo'yicha: {totalWaybillAmount:N2}, to'lovlar {paidAmount:N2}";
                        }
                        break;
                }

                details.Add(detail);
            }

            return details;
        }

        private IQueryable<FIN_TRANSACTIONS> GetPayrollPaymentQuery(DBTHSNEntities db, List<int> salaryCategoryIds, DateTime fromDate, DateTime toDate)
        {
            var query = ActiveFinTransactions(db)
                .Where(t => t.Direction == 2 &&
                            t.TxnDate >= fromDate &&
                            t.TxnDate <= toDate &&
                            (t.IsDeleted == null || t.IsDeleted == false));

            if (salaryCategoryIds == null || salaryCategoryIds.Count == 0)
            {
                return query.Where(t => t.CounterpartyType == 6);
            }

            return query.Where(t => salaryCategoryIds.Contains(t.CategoryID) || t.CounterpartyType == 6);
        }

        private class SupplierOutstandingData
        {
            public int SupplierID { get; set; }
            public decimal Outstanding { get; set; }
            public int InvoiceCount { get; set; }
            public int PaymentCount { get; set; }
            public decimal InvoiceTotal { get; set; }
            public decimal PaymentTotal { get; set; }
        }

        private class SupplierInvoiceWork
        {
            public int ID { get; set; }
            public int SupplierID { get; set; }
            public DateTime InvoiceDate { get; set; }
            public decimal Amount { get; set; }
        }

        private class CategorySnapshotInfo
        {
            public decimal Amount { get; set; }
            public DateTime SnapshotDate { get; set; }
            public bool IsOpening { get; set; }
        }

        private List<SupplierOutstandingData> CalculateSupplierOutstanding(DBTHSNEntities db, DateTime asOfDate, DateTime? fromExclusive = null)
        {
            var invoicesQuery = db.P_INVOICES
                .Where(i => i.InvoiceDate <= asOfDate &&
                           (i.IsDeleted == null || i.IsDeleted == false));

            // Openingdan keyingi davrda supplier qarzini aniqlash:
            // 1) invoys sanasi openingdan keyin bo'lsa
            // 2) yoki invoys oldin yozilgan bo'lsa ham, unga bog'langan ombor kirimi openingdan keyin qabul qilingan bo'lsa
            if (fromExclusive.HasValue)
            {
                var fromDate = fromExclusive.Value;
                invoicesQuery = invoicesQuery.Where(i =>
                    i.InvoiceDate > fromDate ||
                    db.INCOMES.Any(inc =>
                        inc.InvoiceID == i.ID &&
                        inc.IssueDateTime > fromDate &&
                        inc.IssueDateTime <= asOfDate &&
                        (inc.IsDeleted == null || inc.IsDeleted == false)));
            }

            var invoices = invoicesQuery
                .Select(i => new
                {
                    i.ID,
                    i.SupplierID,
                    i.InvoiceDate,
                    i.Amount
                })
                .AsEnumerable()
                .Select(i => new
                {
                    i.ID,
                    i.SupplierID,
                    i.InvoiceDate,
                    Amount = Convert.ToDecimal(i.Amount)
                })
                .ToList();

            // InvoiceID bo'lmagan ombor kirimlari ham supplier qarzini oshiradi (naqdga olingan kirimlar).
            // Double-count bo'lmasligi uchun faqat InvoiceID == null bo'lgan kirimlarni qo'shamiz.
            var unlinkedIncomes = db.INCOMES
                .Where(inc => inc.InvoiceID == null &&
                              inc.IssueDateTime <= asOfDate &&
                              (!fromExclusive.HasValue || inc.IssueDateTime > fromExclusive.Value) &&
                              (inc.IsDeleted == null || inc.IsDeleted == false) &&
                              inc.TotalPrice.HasValue &&
                              inc.TotalPrice.Value > 0)
                .Select(inc => new
                {
                    inc.ID,
                    inc.IssueDateTime,
                    Amount = inc.TotalPrice.Value
                })
                .ToList();

            var invoiceWork = invoices
                .Select(i => new SupplierInvoiceWork
                {
                    ID = i.ID,
                    SupplierID = i.SupplierID,
                    InvoiceDate = i.InvoiceDate,
                    Amount = i.Amount
                })
                .ToList();

            // Supplier aniqlanmagan kirimlar uchun 0-bucket ishlatamiz.
            // BalanceSheet yig'indisida bu ham ta'minotchilar qarziga qo'shiladi.
            invoiceWork.AddRange(unlinkedIncomes.Select(inc => new SupplierInvoiceWork
            {
                ID = -inc.ID, // haqiqiy invoys IDlari bilan to'qnashmasin
                SupplierID = 0,
                InvoiceDate = inc.IssueDateTime,
                Amount = Convert.ToDecimal(inc.Amount)
            }));

            var materialCategoryIds = db.FIN_CATEGORIES
                .Where(c => c.IsActive && c.Type == 2 && c.Name.ToLower().Contains("material"))
                .Select(c => c.ID)
                .ToList();

            var paymentCandidates = ActiveFinTransactions(db)
                .Where(t => t.TxnDate <= asOfDate &&
                            (!fromExclusive.HasValue || t.TxnDate > fromExclusive.Value) &&
                            t.Direction == 2 &&
                            t.SourceDocType != "PurchaseInvoice" &&
                            (t.IsDeleted == null || t.IsDeleted == false))
                .Select(t => new
                {
                    t.ID,
                    t.CounterpartyType,
                    t.CounterpartyID,
                    t.CategoryID,
                    t.TxnDate,
                    t.Amount,
                    t.SourceDocType,
                    t.SourceDocID
                })
                .ToList();

            var linkedPurchaseInvoiceIds = paymentCandidates
                .Where(p => p.SourceDocID.HasValue)
                .Select(p => p.SourceDocID.Value)
                .Distinct()
                .ToList();

            var invoiceSupplierMap = linkedPurchaseInvoiceIds.Count == 0
                ? new Dictionary<int, int>()
                : db.P_INVOICES
                    .Where(i => linkedPurchaseInvoiceIds.Contains(i.ID))
                    .Select(i => new { i.ID, i.SupplierID })
                    .ToList()
                    .ToDictionary(x => x.ID, x => x.SupplierID);

            var payments = paymentCandidates
                .Where(p =>
                    (p.CounterpartyType == 2 && p.CounterpartyID.HasValue) ||
                    materialCategoryIds.Contains(p.CategoryID))
                .Select(p =>
                {
                    var supplierId = 0;
                    if (p.CounterpartyType == 2 && p.CounterpartyID.HasValue)
                    {
                        supplierId = p.CounterpartyID.Value;
                    }
                    else if (p.SourceDocID.HasValue && invoiceSupplierMap.TryGetValue(p.SourceDocID.Value, out var mappedSupplierId))
                    {
                        supplierId = mappedSupplierId;
                    }

                    return new
                    {
                        p.ID,
                        SupplierID = supplierId,
                        p.TxnDate,
                        p.Amount,
                        // Invoysga bog'langan to'lov: SourceDocType = PurchaseInvoicePayment, SourceDocID = invoys ID
                        InvoiceID = (p.SourceDocType == "PurchaseInvoicePayment") ? p.SourceDocID : null
                    };
                })
                .ToList();

            var invoiceRemaining = invoiceWork.ToDictionary(i => i.ID, i => i.Amount);
            var invoiceBySupplier = invoiceWork
                .GroupBy(i => i.SupplierID)
                .ToDictionary(g => g.Key, g => g.OrderBy(i => i.InvoiceDate).Select(i => i.ID).ToList());

            var paymentRemainders = new List<(int SupplierID, DateTime TxnDate, decimal Amount)>();
            var unappliedPaymentsBySupplier = new Dictionary<int, decimal>();

            foreach (var payment in payments)
            {
                var remaining = payment.Amount;
                if (payment.InvoiceID.HasValue && invoiceRemaining.ContainsKey(payment.InvoiceID.Value))
                {
                    var invoiceBalance = invoiceRemaining[payment.InvoiceID.Value];
                    var applied = Math.Min(invoiceBalance, remaining);
                    invoiceRemaining[payment.InvoiceID.Value] = invoiceBalance - applied;
                    remaining -= applied;
                }

                if (remaining > 0)
                {
                    paymentRemainders.Add((payment.SupplierID, payment.TxnDate, remaining));
                }
            }

            foreach (var group in paymentRemainders.GroupBy(p => p.SupplierID))
            {
                if (!invoiceBySupplier.ContainsKey(group.Key))
                {
                    // Supplier aniqlanmagan material to'lovlari: global FIFO bo'yicha eng eski ochiq invoyslardan ayiramiz.
                    if (group.Key == 0)
                    {
                        var globalInvoiceQueue = new Queue<int>(invoiceWork.OrderBy(i => i.InvoiceDate).Select(i => i.ID));
                        foreach (var payment in group.OrderBy(p => p.TxnDate))
                        {
                            var remaining = payment.Amount;
                            while (remaining > 0 && globalInvoiceQueue.Count > 0)
                            {
                                var invoiceId = globalInvoiceQueue.Peek();
                                var invoiceBalance = invoiceRemaining[invoiceId];
                                if (invoiceBalance <= 0)
                                {
                                    globalInvoiceQueue.Dequeue();
                                    continue;
                                }

                                var applied = Math.Min(invoiceBalance, remaining);
                                invoiceRemaining[invoiceId] = invoiceBalance - applied;
                                remaining -= applied;
                                if (invoiceRemaining[invoiceId] <= 0)
                                {
                                    globalInvoiceQueue.Dequeue();
                                }
                            }
                        }
                    }
                    else
                    {
                        unappliedPaymentsBySupplier[group.Key] = (unappliedPaymentsBySupplier.ContainsKey(group.Key)
                            ? unappliedPaymentsBySupplier[group.Key]
                            : 0m) + group.Sum(x => x.Amount);
                    }
                    continue;
                }

                var invoiceQueue = new Queue<int>(invoiceBySupplier[group.Key]);
                foreach (var payment in group.OrderBy(p => p.TxnDate))
                {
                    var remaining = payment.Amount;
                    while (remaining > 0 && invoiceQueue.Count > 0)
                    {
                        var invoiceId = invoiceQueue.Peek();
                        var invoiceBalance = invoiceRemaining[invoiceId];
                        if (invoiceBalance <= 0)
                        {
                            invoiceQueue.Dequeue();
                            continue;
                        }

                        var applied = Math.Min(invoiceBalance, remaining);
                        invoiceRemaining[invoiceId] = invoiceBalance - applied;
                        remaining -= applied;
                        if (invoiceRemaining[invoiceId] <= 0)
                        {
                            invoiceQueue.Dequeue();
                        }
                    }
                    if (remaining > 0)
                    {
                        unappliedPaymentsBySupplier[group.Key] = (unappliedPaymentsBySupplier.ContainsKey(group.Key)
                            ? unappliedPaymentsBySupplier[group.Key]
                            : 0m) + remaining;
                    }
                }
            }

            var invoiceTotalsBySupplier = invoiceWork
                .GroupBy(i => i.SupplierID)
                .ToDictionary(g => g.Key, g => new
                {
                    Total = g.Sum(x => x.Amount),
                    Count = g.Count()
                });

            var paymentTotalsBySupplier = payments
                .GroupBy(p => p.SupplierID)
                .ToDictionary(g => g.Key, g => new
                {
                    Total = g.Sum(x => x.Amount),
                    Count = g.Count()
                });

            var summary = new List<SupplierOutstandingData>();
            var supplierIds = invoiceTotalsBySupplier.Keys
                .Union(paymentTotalsBySupplier.Keys)
                .Union(unappliedPaymentsBySupplier.Keys)
                .ToList();

            foreach (var supplierId in supplierIds)
            {
                var outstanding = invoiceWork
                    .Where(i => i.SupplierID == supplierId)
                    .Sum(i => invoiceRemaining[i.ID]);

                if (unappliedPaymentsBySupplier.TryGetValue(supplierId, out var unapplied))
                {
                    outstanding -= unapplied;
                }

                var invoiceInfo = invoiceTotalsBySupplier.ContainsKey(supplierId)
                    ? invoiceTotalsBySupplier[supplierId]
                    : new { Total = 0m, Count = 0 };
                var paymentInfo = paymentTotalsBySupplier.ContainsKey(supplierId)
                    ? paymentTotalsBySupplier[supplierId]
                    : new { Total = 0m, Count = 0 };

                summary.Add(new SupplierOutstandingData
                {
                    SupplierID = supplierId,
                    Outstanding = outstanding,
                    InvoiceCount = invoiceInfo.Count,
                    PaymentCount = paymentInfo.Count,
                    InvoiceTotal = invoiceInfo.Total,
                    PaymentTotal = paymentInfo.Total
                });
            }

            return summary;
        }

        private class CustomerOutstandingData
        {
            public int CustomerID { get; set; }
            public decimal Outstanding { get; set; }
            public int InvoiceCount { get; set; }
            public int ReceiptCount { get; set; }
            public decimal InvoiceTotal { get; set; }
            public decimal ReceiptTotal { get; set; }
        }

        private List<CustomerOutstandingData> CalculateCustomerOutstanding(DBTHSNEntities db, DateTime asOfDate, DateTime? fromExclusive = null)
        {
            var invoices = db.SALES_INVOICES
                .Where(i => i.InvoiceDate <= asOfDate &&
                            (!fromExclusive.HasValue || i.InvoiceDate > fromExclusive.Value))
                .Select(i => new
                {
                    ID = (int)i.ID,
                    i.CustomerID,
                    i.InvoiceDate,
                    Amount = i.TotalAmount
                })
                .ToList();

            var salesCategoryIds = db.FIN_CATEGORIES
                .Where(c => c.IsActive && c.Type == 1 && c.Name.Contains("Sotuv"))
                .Select(c => c.ID)
                .ToList();

            var receiptCandidates = ActiveFinTransactions(db)
                .Where(t => t.TxnDate <= asOfDate &&
                            (!fromExclusive.HasValue || t.TxnDate > fromExclusive.Value) &&
                            t.Direction == 1 &&
                            t.SourceDocType != "SalesInvoice" &&
                            (t.IsDeleted == null || t.IsDeleted == false))
                .Select(t => new
                {
                    t.ID,
                    t.CounterpartyType,
                    t.CounterpartyID,
                    t.CategoryID,
                    t.TxnDate,
                    t.Amount,
                    t.SourceDocType,
                    t.SourceDocID
                })
                .ToList();

            var linkedInvoiceIds = receiptCandidates
                .Where(r => r.SourceDocID.HasValue)
                .Select(r => (long)r.SourceDocID.Value)
                .Distinct()
                .ToList();

            var invoiceCustomerMap = linkedInvoiceIds.Count == 0
                ? new Dictionary<long, int>()
                : db.SALES_INVOICES
                    .Where(i => linkedInvoiceIds.Contains(i.ID))
                    .Select(i => new { i.ID, i.CustomerID })
                    .ToList()
                    .ToDictionary(x => x.ID, x => x.CustomerID);

            var receipts = receiptCandidates
                .Where(r =>
                    (r.CounterpartyType == 1 && r.CounterpartyID.HasValue) ||
                    salesCategoryIds.Contains(r.CategoryID))
                .Select(r =>
                {
                    var customerId = 0;
                    if (r.CounterpartyType == 1 && r.CounterpartyID.HasValue)
                    {
                        customerId = r.CounterpartyID.Value;
                    }
                    else if (r.SourceDocID.HasValue && invoiceCustomerMap.TryGetValue((long)r.SourceDocID.Value, out var mappedCustomerId))
                    {
                        customerId = mappedCustomerId;
                    }

                    return new
                    {
                        r.ID,
                        CustomerID = customerId,
                        r.TxnDate,
                        r.Amount,
                        InvoiceID = (r.SourceDocType == "SalesInvoicePayment") ? r.SourceDocID : null
                    };
                })
                .ToList();

            var invoiceRemaining = invoices.ToDictionary(i => i.ID, i => i.Amount);
            var invoiceByCustomer = invoices
                .GroupBy(i => i.CustomerID)
                .ToDictionary(g => g.Key, g => g.OrderBy(i => i.InvoiceDate).Select(i => i.ID).ToList());

            var receiptRemainders = new List<(int CustomerID, DateTime TxnDate, decimal Amount)>();
            var unappliedReceiptsByCustomer = new Dictionary<int, decimal>();
            foreach (var receipt in receipts)
            {
                var remaining = receipt.Amount;
                if (receipt.InvoiceID.HasValue && invoiceRemaining.ContainsKey(receipt.InvoiceID.Value))
                {
                    var invoiceBalance = invoiceRemaining[receipt.InvoiceID.Value];
                    var applied = Math.Min(invoiceBalance, remaining);
                    invoiceRemaining[receipt.InvoiceID.Value] = invoiceBalance - applied;
                    remaining -= applied;
                }

                if (remaining > 0)
                {
                    receiptRemainders.Add((receipt.CustomerID, receipt.TxnDate, remaining));
                }
            }

            foreach (var group in receiptRemainders.GroupBy(p => p.CustomerID))
            {
                if (!invoiceByCustomer.ContainsKey(group.Key))
                {
                    // Customer aniqlanmagan "Sotuv" kirimlari: umumiy FIFO bo'yicha eng eski ochiq invoyslardan ayiramiz.
                    if (group.Key == 0)
                    {
                        var globalInvoiceQueue = new Queue<int>(invoices.OrderBy(i => i.InvoiceDate).Select(i => i.ID));
                        foreach (var receipt in group.OrderBy(p => p.TxnDate))
                        {
                            var remaining = receipt.Amount;
                            while (remaining > 0 && globalInvoiceQueue.Count > 0)
                            {
                                var invoiceId = globalInvoiceQueue.Peek();
                                var invoiceBalance = invoiceRemaining[invoiceId];
                                if (invoiceBalance <= 0)
                                {
                                    globalInvoiceQueue.Dequeue();
                                    continue;
                                }

                                var applied = Math.Min(invoiceBalance, remaining);
                                invoiceRemaining[invoiceId] = invoiceBalance - applied;
                                remaining -= applied;
                                if (invoiceRemaining[invoiceId] <= 0)
                                {
                                    globalInvoiceQueue.Dequeue();
                                }
                            }
                        }
                    }
                    else
                    {
                        unappliedReceiptsByCustomer[group.Key] = (unappliedReceiptsByCustomer.ContainsKey(group.Key)
                            ? unappliedReceiptsByCustomer[group.Key]
                            : 0m) + group.Sum(x => x.Amount);
                    }
                    continue;
                }

                var invoiceQueue = new Queue<int>(invoiceByCustomer[group.Key]);
                foreach (var receipt in group.OrderBy(p => p.TxnDate))
                {
                    var remaining = receipt.Amount;
                    while (remaining > 0 && invoiceQueue.Count > 0)
                    {
                        var invoiceId = invoiceQueue.Peek();
                        var invoiceBalance = invoiceRemaining[invoiceId];
                        if (invoiceBalance <= 0)
                        {
                            invoiceQueue.Dequeue();
                            continue;
                        }

                        var applied = Math.Min(invoiceBalance, remaining);
                        invoiceRemaining[invoiceId] = invoiceBalance - applied;
                        remaining -= applied;
                        if (invoiceRemaining[invoiceId] <= 0)
                        {
                            invoiceQueue.Dequeue();
                        }
                    }
                    if (remaining > 0)
                    {
                        unappliedReceiptsByCustomer[group.Key] = (unappliedReceiptsByCustomer.ContainsKey(group.Key)
                            ? unappliedReceiptsByCustomer[group.Key]
                            : 0m) + remaining;
                    }
                }
            }

            var invoiceTotalsByCustomer = invoices
                .GroupBy(i => i.CustomerID)
                .ToDictionary(g => g.Key, g => new
                {
                    Total = g.Sum(x => x.Amount),
                    Count = g.Count()
                });

            var receiptTotalsByCustomer = receipts
                .GroupBy(p => p.CustomerID)
                .ToDictionary(g => g.Key, g => new
                {
                    Total = g.Sum(x => x.Amount),
                    Count = g.Count()
                });

            var summary = new List<CustomerOutstandingData>();
            var customerIds = invoiceTotalsByCustomer.Keys
                .Union(receiptTotalsByCustomer.Keys)
                .Union(unappliedReceiptsByCustomer.Keys)
                .ToList();

            foreach (var customerId in customerIds)
            {
                var outstanding = invoices
                    .Where(i => i.CustomerID == customerId)
                    .Sum(i => invoiceRemaining[i.ID]);

                if (unappliedReceiptsByCustomer.TryGetValue(customerId, out var unapplied))
                {
                    outstanding -= unapplied;
                }

                var invoiceInfo = invoiceTotalsByCustomer.ContainsKey(customerId)
                    ? invoiceTotalsByCustomer[customerId]
                    : new { Total = 0m, Count = 0 };
                var receiptInfo = receiptTotalsByCustomer.ContainsKey(customerId)
                    ? receiptTotalsByCustomer[customerId]
                    : new { Total = 0m, Count = 0 };

                summary.Add(new CustomerOutstandingData
                {
                    CustomerID = customerId,
                    Outstanding = outstanding,
                    InvoiceCount = invoiceInfo.Count,
                    ReceiptCount = receiptInfo.Count,
                    InvoiceTotal = invoiceInfo.Total,
                    ReceiptTotal = receiptInfo.Total
                });
            }

            return summary;
        }

        // Olish so'rovi: FinDashboard/FinancialOverview
        public ActionResult FinancialOverview(DateTime? fromDate, DateTime? toDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
            toDate = toDate ?? DateTime.Now;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Olish summary statistics - exclude liability transactions (faqat real cash flow)
                var transactions = ActiveFinTransactions(db)
                    .Where(t => t.TxnDate >= fromDate.Value && t.TxnDate <= toDate.Value)
                    // Exclude liability transactions (PurchaseInvoice va SalesInvoice) - faqat show real transactions
                    .Where(t => (t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice") || t.SourceDocType == null || t.SourceDocType == "")
                    .ToList();

                var totalIncome = transactions.Where(t => t.Direction == 1).Sum(t => (decimal?)t.Amount) ?? 0m;
                var totalExpense = transactions.Where(t => t.Direction == 2).Sum(t => (decimal?)t.Amount) ?? 0m;
                var netCashflow = totalIncome - totalExpense;

                // Olish account balances
                var cashAccounts = db.FIN_ACCOUNTS.Where(a => a.IsCash && a.IsActive).ToList();
                var bankAccounts = db.FIN_ACCOUNTS.Where(a => a.IsBank && a.IsActive).ToList();
                
                var cashBalance = 0m;
                foreach (var account in cashAccounts)
                {
                    cashBalance += ActiveFinTransactions(db)
                        .Where(t => t.AccountID == account.ID)
                        .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                }

                var bankBalance = 0m;
                foreach (var account in bankAccounts)
                {
                    bankBalance += ActiveFinTransactions(db)
                        .Where(t => t.AccountID == account.ID)
                        .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                }

                // Top categories by expense
                var topExpenseCategories = transactions
                    .Where(t => t.Direction == 2 && t.FIN_CATEGORIES != null)
                    .GroupBy(t => new { t.CategoryID, CategoryName = t.FIN_CATEGORIES.Name })
                    .Select(g => new { g.Key.CategoryName, Amount = g.Sum(t => (decimal?)t.Amount) ?? 0m })
                    .OrderByDescending(x => x.Amount)
                    .Take(10)
                    .ToList();

                // Top categories by income
                var topIncomeCategories = transactions
                    .Where(t => t.Direction == 1 && t.FIN_CATEGORIES != null)
                    .GroupBy(t => new { t.CategoryID, CategoryName = t.FIN_CATEGORIES.Name })
                    .Select(g => new { g.Key.CategoryName, Amount = g.Sum(t => (decimal?)t.Amount) ?? 0m })
                    .OrderByDescending(x => x.Amount)
                    .Take(10)
                    .ToList();

                // Monthly trend
                var monthlyData = transactions
                    .GroupBy(t => new { Year = t.TxnDate.Year, Month = t.TxnDate.Month })
                    .Select(g => new
                    {
                        Period = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Income = g.Where(t => t.Direction == 1).Sum(t => (decimal?)t.Amount) ?? 0m,
                        Expense = g.Where(t => t.Direction == 2).Sum(t => (decimal?)t.Amount) ?? 0m
                    })
                    .OrderBy(x => x.Period)
                    .ToList();

                // Olish primary currency
                var primaryCurrency = transactions
                    .Where(t => t.CURRENCy != null)
                    .GroupBy(t => t.CurrencyID)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.First().CURRENCy)
                    .FirstOrDefault() ?? db.CURRENCIES.FirstOrDefault();

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.TotalIncome = totalIncome;
                ViewBag.TotalExpense = totalExpense;
                ViewBag.NetCashflow = netCashflow;
                ViewBag.CashBalance = cashBalance;
                ViewBag.BankBalance = bankBalance;
                ViewBag.TopExpenseCategories = topExpenseCategories;
                ViewBag.TopIncomeCategories = topIncomeCategories;
                ViewBag.MonthlyData = monthlyData;
                ViewBag.CurrencyShortname = primaryCurrency?.Shortname ?? "UZS";

                return View();
            }
        }

        // Olish so'rovi: FinDashboard/CategoryAnalytics
        public ActionResult CategoryAnalytics(DateTime? fromDate, DateTime? toDate, int? categoryID)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
            toDate = toDate ?? DateTime.Now;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = ActiveFinTransactions(db)
                    .Include(t => t.FIN_CATEGORIES)
                    .Where(t => t.TxnDate >= fromDate.Value && t.TxnDate <= toDate.Value)
                    // Exclude liability transactions - faqat show real transactions
                    .Where(t => (t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice") || t.SourceDocType == null || t.SourceDocType == "");

                if (categoryID.HasValue)
                {
                    query = query.Where(t => t.CategoryID == categoryID.Value);
                }

                var transactions = query.ToList();

                // Category breakdown by expense
                var expenseByCategory = transactions
                    .Where(t => t.Direction == 2 && t.FIN_CATEGORIES != null)
                    .GroupBy(t => new { t.CategoryID, CategoryName = t.FIN_CATEGORIES.Name, CategoryType = t.FIN_CATEGORIES.Type })
                    .Select(g => new
                    {
                        CategoryID = g.Key.CategoryID,
                        CategoryName = g.Key.CategoryName,
                        CategoryType = g.Key.CategoryType,
                        Amount = g.Sum(t => (decimal?)t.Amount) ?? 0m,
                        TransactionCount = g.Count()
                    })
                    .OrderByDescending(x => x.Amount)
                    .ToList();

                // Category breakdown by income
                var incomeByCategory = transactions
                    .Where(t => t.Direction == 1 && t.FIN_CATEGORIES != null)
                    .GroupBy(t => new { t.CategoryID, CategoryName = t.FIN_CATEGORIES.Name, CategoryType = t.FIN_CATEGORIES.Type })
                    .Select(g => new
                    {
                        CategoryID = g.Key.CategoryID,
                        CategoryName = g.Key.CategoryName,
                        CategoryType = g.Key.CategoryType,
                        Amount = g.Sum(t => (decimal?)t.Amount) ?? 0m,
                        TransactionCount = g.Count()
                    })
                    .OrderByDescending(x => x.Amount)
                    .ToList();

                // Daily trend by category
                var dailyTrend = transactions
                    .Where(t => t.FIN_CATEGORIES != null)
                    .GroupBy(t => new { Date = t.TxnDate.Date, CategoryName = t.FIN_CATEGORIES.Name, Direction = t.Direction })
                    .Select(g => new
                    {
                        Date = g.Key.Date.ToString("yyyy-MM-dd"),
                        CategoryName = g.Key.CategoryName,
                        Direction = g.Key.Direction,
                        Amount = g.Sum(t => (decimal?)t.Amount) ?? 0m
                    })
                    .OrderBy(x => x.Date)
                    .ToList();

                // Olish primary currency
                var primaryCurrency = transactions
                    .Where(t => t.CURRENCy != null)
                    .GroupBy(t => t.CurrencyID)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.First().CURRENCy)
                    .FirstOrDefault() ?? db.CURRENCIES.FirstOrDefault();

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.CategoryID = categoryID;
                ViewBag.CategoryList = new SelectList(
                    db.FIN_CATEGORIES.Where(c => c.IsActive).OrderBy(c => c.Name).ToList(),
                    "ID", "Name", categoryID);
                ViewBag.ExpenseByCategory = expenseByCategory;
                ViewBag.IncomeByCategory = incomeByCategory;
                ViewBag.DailyTrend = dailyTrend;
                ViewBag.CurrencyShortname = primaryCurrency?.Shortname ?? "UZS";

                return View();
            }
        }

        // Olish so'rovi: FinDashboard/AccountAnalytics
        public ActionResult AccountAnalytics(DateTime? fromDate, DateTime? toDate, int? accountID)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
            toDate = toDate ?? DateTime.Now;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = ActiveFinTransactions(db)
                    .Include(t => t.FIN_ACCOUNTS)
                    .Include(t => t.CURRENCy)
                    .Where(t => t.TxnDate >= fromDate.Value && t.TxnDate <= toDate.Value)
                    // Exclude liability transactions - faqat show real transactions
                    .Where(t => (t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice") || t.SourceDocType == null || t.SourceDocType == "");

                if (accountID.HasValue)
                {
                    query = query.Where(t => t.AccountID == accountID.Value);
                }

                var transactions = query.ToList();

                // Account usage breakdown
                var accountUsage = transactions
                    .Where(t => t.FIN_ACCOUNTS != null && !string.IsNullOrEmpty(t.FIN_ACCOUNTS.Name))
                    .GroupBy(t => new
                    {
                        t.AccountID, 
                        AccountName = t.FIN_ACCOUNTS.Name ?? "", 
                        AccountCode = t.FIN_ACCOUNTS.Code ?? "", 
                        IsCash = t.FIN_ACCOUNTS.IsCash, 
                        IsBank = t.FIN_ACCOUNTS.IsBank 
                    })
                    .Select(g => new AccountUsageData
                    {
                        AccountID = g.Key.AccountID,
                        AccountName = g.Key.AccountName ?? "",
                        AccountCode = g.Key.AccountCode ?? "",
                        AccountType = g.Key.IsCash ? "Naqd pul" : (g.Key.IsBank ? "Bank" : "Boshqa"),
                        TotalInflow = g.Where(t => t.Direction == 1).Sum(t => (decimal?)t.Amount) ?? 0m,
                        TotalOutflow = g.Where(t => t.Direction == 2).Sum(t => (decimal?)t.Amount) ?? 0m,
                        TransactionCount = g.Count(),
                        NetAmount = (g.Where(t => t.Direction == 1).Sum(t => (decimal?)t.Amount) ?? 0m) - (g.Where(t => t.Direction == 2).Sum(t => (decimal?)t.Amount) ?? 0m)
                    })
                    .OrderByDescending(x => Math.Abs(x.NetAmount))
                    .ToList();

                // Daily account activity
                var dailyAccountActivity = transactions
                    .Where(t => t.FIN_ACCOUNTS != null)
                    .GroupBy(t => new { Date = t.TxnDate.Date, AccountName = t.FIN_ACCOUNTS.Name })
                    .Select(g => new
                    {
                        Date = g.Key.Date.ToString("yyyy-MM-dd"),
                        AccountName = g.Key.AccountName,
                        Inflow = g.Where(t => t.Direction == 1).Sum(t => (decimal?)t.Amount) ?? 0m,
                        Outflow = g.Where(t => t.Direction == 2).Sum(t => (decimal?)t.Amount) ?? 0m
                    })
                    .OrderBy(x => x.Date)
                    .ToList();

                // Olish primary currency
                var primaryCurrency = transactions
                    .Where(t => t.CURRENCy != null)
                    .GroupBy(t => t.CurrencyID)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.First().CURRENCy)
                    .FirstOrDefault() ?? db.CURRENCIES.FirstOrDefault();

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.AccountID = accountID;
                ViewBag.AccountList = new SelectList(
                    db.FIN_ACCOUNTS.Where(a => a.IsActive).OrderBy(a => a.Name).ToList(),
                    "ID", "Name", accountID);
                ViewBag.AccountUsage = accountUsage;
                ViewBag.DailyAccountActivity = dailyAccountActivity;
                ViewBag.CurrencyShortname = primaryCurrency?.Shortname ?? "UZS";

                return View();
            }
        }

        // Olish so'rovi: FinDashboard/ExpenseAnalysis
        public ActionResult ExpenseAnalysis(DateTime? fromDate, DateTime? toDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
            toDate = toDate ?? DateTime.Now;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var expenses = ActiveFinTransactions(db)
                    .Include(t => t.FIN_CATEGORIES)
                    .Include(t => t.FIN_ACCOUNTS)
                    .Where(t => t.Direction == 2 && t.TxnDate >= fromDate.Value && t.TxnDate <= toDate.Value)
                    // Exclude liability transactions (PurchaseInvoice) - faqat show real expenses
                    .Where(t => (t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice") || t.SourceDocType == null || t.SourceDocType == "")
                    .ToList();

                // Expense by category (pie chart ma'lumot)
                var expenseByCategory = expenses
                    .Where(t => t.FIN_CATEGORIES != null)
                    .GroupBy(t => new { t.CategoryID, CategoryName = t.FIN_CATEGORIES.Name })
                    .Select(g => new ExpenseByCategoryData
                    {
                        CategoryName = g.Key.CategoryName ?? "",
                        Amount = g.Sum(t => (decimal?)t.Amount) ?? 0m,
                        Percentage = 0m // Will calculate below
                    })
                    .OrderByDescending(x => x.Amount)
                    .ToList();

                var totalExpense = expenseByCategory.Sum(x => x.Amount);
                if (totalExpense > 0)
                {
                    foreach (var item in expenseByCategory)
                    {
                        item.Percentage = (item.Amount / totalExpense) * 100;
                    }
                }

                // Expense by account
                var expenseByAccount = expenses
                    .Where(t => t.FIN_ACCOUNTS != null)
                    .GroupBy(t => new { t.AccountID, AccountName = t.FIN_ACCOUNTS.Name })
                    .Select(g => new ExpenseByAccountData
                    {
                        AccountName = g.Key.AccountName ?? "",
                        Amount = g.Sum(t => (decimal?)t.Amount) ?? 0m
                    })
                    .OrderByDescending(x => x.Amount)
                    .ToList();

                // Monthly expense trend
                var monthlyExpense = expenses
                    .GroupBy(t => new { Year = t.TxnDate.Year, Month = t.TxnDate.Month })
                    .Select(g => new MonthlyData
                    {
                        Period = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Amount = g.Sum(t => (decimal?)t.Amount) ?? 0m
                    })
                    .OrderBy(x => x.Period)
                    .ToList();

                // Olish primary currency
                var primaryCurrency = expenses
                    .Where(t => t.CURRENCy != null)
                    .GroupBy(t => t.CurrencyID)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.First().CURRENCy)
                    .FirstOrDefault() ?? db.CURRENCIES.FirstOrDefault();

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.ExpenseByCategory = expenseByCategory;
                ViewBag.ExpenseByAccount = expenseByAccount;
                ViewBag.MonthlyExpense = monthlyExpense;
                ViewBag.TotalExpense = totalExpense;
                ViewBag.CurrencyShortname = primaryCurrency?.Shortname ?? "UZS";

                return View();
            }
        }

        // Olish so'rovi: FinDashboard/IncomeAnalysis
        public ActionResult IncomeAnalysis(DateTime? fromDate, DateTime? toDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
            toDate = toDate ?? DateTime.Now;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var incomes = ActiveFinTransactions(db)
                    .Include(t => t.FIN_CATEGORIES)
                    .Include(t => t.FIN_ACCOUNTS)
                    .Where(t => t.Direction == 1 && t.TxnDate >= fromDate.Value && t.TxnDate <= toDate.Value)
                    // Exclude liability transactions (SalesInvoice) - faqat show real income
                    .Where(t => (t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice") || t.SourceDocType == null || t.SourceDocType == "")
                    .ToList();

                // Income by category (pie chart ma'lumot)
                var incomeByCategory = incomes
                    .Where(t => t.FIN_CATEGORIES != null)
                    .GroupBy(t => new { t.CategoryID, CategoryName = t.FIN_CATEGORIES.Name })
                    .Select(g => new IncomeByCategoryData
                    {
                        CategoryName = g.Key.CategoryName ?? "",
                        Amount = g.Sum(t => (decimal?)t.Amount) ?? 0m,
                        Percentage = 0m // Will calculate below
                    })
                    .OrderByDescending(x => x.Amount)
                    .ToList();

                var totalIncome = incomeByCategory.Sum(x => x.Amount);
                if (totalIncome > 0)
                {
                    foreach (var item in incomeByCategory)
                    {
                        item.Percentage = (item.Amount / totalIncome) * 100;
                    }
                }

                // Income by account
                var incomeByAccount = incomes
                    .Where(t => t.FIN_ACCOUNTS != null)
                    .GroupBy(t => new { t.AccountID, AccountName = t.FIN_ACCOUNTS.Name })
                    .Select(g => new IncomeByAccountData
                    {
                        AccountName = g.Key.AccountName ?? "",
                        Amount = g.Sum(t => (decimal?)t.Amount) ?? 0m
                    })
                    .OrderByDescending(x => x.Amount)
                    .ToList();

                // Monthly income trend
                var monthlyIncome = incomes
                    .GroupBy(t => new { Year = t.TxnDate.Year, Month = t.TxnDate.Month })
                    .Select(g => new MonthlyData
                    {
                        Period = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Amount = g.Sum(t => (decimal?)t.Amount) ?? 0m
                    })
                    .OrderBy(x => x.Period)
                    .ToList();

                // Olish primary currency
                var primaryCurrency = incomes
                    .Where(t => t.CURRENCy != null)
                    .GroupBy(t => t.CurrencyID)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.First().CURRENCy)
                    .FirstOrDefault() ?? db.CURRENCIES.FirstOrDefault();

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.IncomeByCategory = incomeByCategory;
                ViewBag.IncomeByAccount = incomeByAccount;
                ViewBag.MonthlyIncome = monthlyIncome;
                ViewBag.TotalIncome = totalIncome;
                ViewBag.CurrencyShortname = primaryCurrency?.Shortname ?? "UZS";

                return View();
            }
        }

        // Olish so'rovi: FinDashboard/AccountBalances
        public ActionResult AccountBalances(DateTime? fromDate, DateTime? toDate, int? accountType)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
            toDate = toDate ?? DateTime.Now;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Olish barcha faol accounts bilan currency va balance category
                var accountsQuery = db.FIN_ACCOUNTS
                    .Include(a => a.CURRENCy)
                    .Include(a => a.FIN_BALANCE_CATEGORIES)
                    .Where(a => a.IsActive);

                // Filtrlash bo'yicha Asset (Type = 1) yoki Liability (Type = 2)
                if (accountType.HasValue)
                {
                    if (accountType.Value == 1) // Asset (Aktiv)
                    {
                        accountsQuery = accountsQuery.Where(a => 
                            a.FIN_BALANCE_CATEGORIES != null && 
                            a.FIN_BALANCE_CATEGORIES.Type == 1);
                    }
                    else if (accountType.Value == 2) // Liability (Majburiyat)
                    {
                        accountsQuery = accountsQuery.Where(a => 
                            a.FIN_BALANCE_CATEGORIES != null && 
                            a.FIN_BALANCE_CATEGORIES.Type == 2);
                    }
                }

                var accounts = accountsQuery
                    .OrderBy(a => a.Code)
                    .ToList();

                // Opening balans: FIN_BALANCE_SNAPSHOTS (Opening balans sahifasi) — kategoriya bo'yicha
                var accountIds = accounts.Select(a => a.ID).ToList();
                var allTxnsByAccount = accountIds.Count == 0
                    ? new Dictionary<int, List<FIN_TRANSACTIONS>>()
                    : ActiveFinTransactions(db)
                        .Where(t => t.TxnDate <= toDate.Value && accountIds.Contains(t.AccountID))
                        .ToList()
                        .GroupBy(t => t.AccountID)
                        .ToDictionary(g => g.Key, g => g.ToList());

                // So'nggi opening snapshot har bir kategoriya uchun (toDate gacha)
                var openingSnapshots = db.FIN_BALANCE_SNAPSHOTS
                    .Where(s => s.SnapshotDate <= toDate.Value)
                    .ToList()
                    .Where(s => s.SourceType == "OpeningBalance" || string.IsNullOrEmpty(s.SourceType))
                    .GroupBy(s => s.CategoryID)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.SnapshotDate).First());

                var accountCountByCategory = accounts
                    .Where(a => a.BalanceCategoryID.HasValue)
                    .GroupBy(a => a.BalanceCategoryID.Value)
                    .ToDictionary(g => g.Key, g => g.Count());

                var accountBalances = new List<AccountBalanceData>();

                foreach (var account in accounts)
                {
                    if (!allTxnsByAccount.TryGetValue(account.ID, out var allTransactions))
                    {
                        allTransactions = new List<FIN_TRANSACTIONS>();
                    }

                    decimal qoldiq;
                    var usesOpening = false;

                    // Hisob FIN_BALANCE_CATEGORIES ga bog'langan va shu kategoriya uchun opening snapshot bo'lsa:
                    // qoldiq = (snapshot / shu kategoriyadagi hisoblar soni) + opening sanasidan boshlab tranzaksiyalar
                    // (openingdan oldingi tranzaksiyalar hisobga olinmaydi — migratsiya boshlang'ich nuqtasi)
                    if (account.BalanceCategoryID.HasValue &&
                        openingSnapshots.TryGetValue(account.BalanceCategoryID.Value, out var snapRow))
                    {
                        var snapDate = snapRow.SnapshotDate.Date;
                        var cnt = accountCountByCategory.TryGetValue(account.BalanceCategoryID.Value, out var c)
                            ? Math.Max(1, c)
                            : 1;
                        var allocated = snapRow.Amount / cnt;

                        var txnAfterOpening = allTransactions
                            .Where(t => t.TxnDate >= snapDate)
                            .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;

                        qoldiq = allocated + txnAfterOpening;
                        usesOpening = true;
                    }
                    else
                    {
                        qoldiq = allTransactions
                            .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                    }

                    // Vaqt oralig'idagi kirim/chiqim (faqat ko'rsatish uchun)
                    var periodTransactions = allTransactions
                        .Where(t => t.TxnDate >= fromDate.Value &&
                                   t.TxnDate <= toDate.Value)
                        .ToList();

                    var kirim = periodTransactions
                        .Where(t => t.Direction == 1)
                        .Sum(t => (decimal?)t.Amount) ?? 0m;

                    var chiqim = periodTransactions
                        .Where(t => t.Direction == 2)
                        .Sum(t => (decimal?)t.Amount) ?? 0m;

                    // Olish currency uchun this account
                    var currency = account.CURRENCy;
                    if (currency == null && account.CurrencyID > 0)
                    {
                        currency = db.CURRENCIES.Find(account.CurrencyID);
                    }
                    var currencyShortname = currency?.Shortname ?? "UZS";

                    accountBalances.Add(new AccountBalanceData
                    {
                        AccountID = account.ID,
                        AccountCode = account.Code ?? "",
                        AccountName = account.Name ?? "",
                        OwnerName = account.OwnerName ?? "-",
                        CurrencyShortname = currencyShortname,
                        Kirim = kirim,
                        Chiqim = chiqim,
                        Qoldiq = qoldiq,
                        IncludesOpeningBalance = usesOpening
                    });
                }

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.AccountBalances = accountBalances;
                ViewBag.AccountType = accountType;

                return View();
            }
        }

        // Olish so'rovi: FinDashboard/LiabilitiesDashboard
        public ActionResult LiabilitiesDashboard(DateTime? fromDate, DateTime? toDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
            toDate = toDate ?? DateTime.Now;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Olish faqat liability transactions (PurchaseInvoice va SalesInvoice)
                var payablesTransactions = ActiveFinTransactions(db)
                    .Include(t => t.FIN_ACCOUNTS)
                    .Include(t => t.FIN_CATEGORIES)
                    .Include(t => t.CURRENCy)
                    .Where(t => t.SourceDocType == "PurchaseInvoice" && 
                               t.TxnDate >= fromDate.Value && 
                               t.TxnDate <= toDate.Value)
                    .ToList();

                var receivablesTransactions = ActiveFinTransactions(db)
                    .Include(t => t.FIN_ACCOUNTS)
                    .Include(t => t.FIN_CATEGORIES)
                    .Include(t => t.CURRENCy)
                    .Where(t => t.SourceDocType == "SalesInvoice" && 
                               t.TxnDate >= fromDate.Value && 
                               t.TxnDate <= toDate.Value)
                    .ToList();

                var supplierSummary = CalculateSupplierOutstanding(db, toDate.Value);

                // Payables summary (invoice - payments, allocation/FIFO)
                var totalPayables = supplierSummary.Sum(s => s.Outstanding);

                // Receivables summary
                var totalReceivables = receivablesTransactions
                    .Sum(t => (decimal?)t.Amount) ?? 0m;

                // Payroll liability (approved payrolls minus salary expenses)
                var salaryCategoryIds = db.FIN_CATEGORIES
                    .Where(c => c.IsActive && c.Name == "Oylik")
                    .Select(c => c.ID)
                    .ToList();

                var payrollApprovedTotal = db.HR_PAYROLL_LINES
                    .Where(l => (l.IsDeleted == null || l.IsDeleted == false) &&
                               l.HR_PAYROLL_HEADERS != null &&
                               (l.HR_PAYROLL_HEADERS.IsDeleted == null || l.HR_PAYROLL_HEADERS.IsDeleted == false) &&
                               (l.HR_PAYROLL_HEADERS.Status == "Approved" || l.HR_PAYROLL_HEADERS.Status == "Posted") &&
                               l.HR_PAYROLL_HEADERS.ApprovedDT.HasValue &&
                               l.HR_PAYROLL_HEADERS.ApprovedDT.Value >= fromDate.Value &&
                               l.HR_PAYROLL_HEADERS.ApprovedDT.Value <= toDate.Value)
                    .Sum(l => (decimal?)l.NetSalary) ?? 0m;

                var salaryExpenseTotal = GetPayrollPaymentQuery(db, salaryCategoryIds, fromDate.Value, toDate.Value)
                        .Sum(t => (decimal?)t.Amount) ?? 0m;

                var payrollLiability = payrollApprovedTotal - salaryExpenseTotal;
                if (payrollLiability < 0)
                {
                    payrollLiability = 0m;
                }

                // Monthly payroll liability trend
                var monthlyPayrollLiability = new List<MonthlyData>();
                var monthCursor = new DateTime(fromDate.Value.Year, fromDate.Value.Month, 1);
                var endMonth = new DateTime(toDate.Value.Year, toDate.Value.Month, 1);

                while (monthCursor <= endMonth)
                {
                    var monthStart = monthCursor;
                    var monthEnd = monthCursor.AddMonths(1).AddDays(-1);
                    if (monthEnd > toDate.Value.Date)
                    {
                        monthEnd = toDate.Value.Date;
                    }

                    var monthPayrollTotal = db.HR_PAYROLL_LINES
                        .Where(l => (l.IsDeleted == null || l.IsDeleted == false) &&
                                   l.HR_PAYROLL_HEADERS != null &&
                                   (l.HR_PAYROLL_HEADERS.IsDeleted == null || l.HR_PAYROLL_HEADERS.IsDeleted == false) &&
                                   (l.HR_PAYROLL_HEADERS.Status == "Approved" || l.HR_PAYROLL_HEADERS.Status == "Posted") &&
                                   l.HR_PAYROLL_HEADERS.ApprovedDT.HasValue &&
                                   l.HR_PAYROLL_HEADERS.ApprovedDT.Value >= monthStart &&
                                   l.HR_PAYROLL_HEADERS.ApprovedDT.Value <= monthEnd)
                        .Sum(l => (decimal?)l.NetSalary) ?? 0m;

                    var monthSalaryExpenses = GetPayrollPaymentQuery(db, salaryCategoryIds, monthStart, monthEnd)
                            .Sum(t => (decimal?)t.Amount) ?? 0m;

                    var monthLiability = monthPayrollTotal - monthSalaryExpenses;
                    if (monthLiability < 0)
                    {
                        monthLiability = 0m;
                    }

                    monthlyPayrollLiability.Add(new MonthlyData
                    {
                        Period = $"{monthStart.Year}-{monthStart.Month:D2}",
                        Amount = monthLiability
                    });

                    monthCursor = monthCursor.AddMonths(1);
                }

                // Payables by supplier (invoice - payments, allocation/FIFO)
                var supplierIds = supplierSummary.Select(s => s.SupplierID).ToList();
                var supplierNames = db.SUPPLIERS
                    .Where(s => supplierIds.Contains(s.ID))
                    .ToDictionary(s => s.ID, s => s.Name);

                var payablesBySupplier = supplierSummary
                    .Select(s => new
                    {
                        SupplierName = supplierNames.ContainsKey(s.SupplierID) ? supplierNames[s.SupplierID] : $"Supplier #{s.SupplierID}",
                        Amount = s.Outstanding,
                        TransactionCount = s.InvoiceCount + s.PaymentCount
                    })
                    .OrderByDescending(x => x.Amount)
                    .ToList();

                // Receivables by customer (counterparty)
                var receivablesByCustomer = receivablesTransactions
                    .Where(t => !string.IsNullOrEmpty(t.CounterpartyName))
                    .GroupBy(t => t.CounterpartyName)
                    .Select(g => new
                    {
                        CustomerName = g.Key,
                        Amount = g.Sum(t => (decimal?)t.Amount) ?? 0m,
                        TransactionCount = g.Count()
                    })
                    .OrderByDescending(x => x.Amount)
                    .ToList();

                // Monthly payables trend (month-end outstanding)
                var monthlyPayables = new List<MonthlyData>();
                var payablesCursor = new DateTime(fromDate.Value.Year, fromDate.Value.Month, 1);
                var payablesEndMonth = new DateTime(toDate.Value.Year, toDate.Value.Month, 1);
                while (payablesCursor <= payablesEndMonth)
                {
                    var monthEndDate = payablesCursor.AddMonths(1).AddDays(-1);
                    if (monthEndDate > toDate.Value.Date)
                    {
                        monthEndDate = toDate.Value.Date;
                    }

                    var monthSummary = CalculateSupplierOutstanding(db, monthEndDate);
                    monthlyPayables.Add(new MonthlyData
                    {
                        Period = $"{payablesCursor.Year}-{payablesCursor.Month:D2}",
                        Amount = monthSummary.Sum(s => s.Outstanding)
                    });

                    payablesCursor = payablesCursor.AddMonths(1);
                }

                // Monthly receivables trend
                var monthlyReceivables = receivablesTransactions
                    .GroupBy(t => new { Year = t.TxnDate.Year, Month = t.TxnDate.Month })
                    .Select(g => new MonthlyData
                    {
                        Period = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Amount = g.Sum(t => (decimal?)t.Amount) ?? 0m
                    })
                    .OrderBy(x => x.Period)
                    .ToList();

                // Olish account balances uchun Payables va Receivables
                var payablesAccounts = db.FIN_ACCOUNTS
                    .Where(a => (a.Code.StartsWith("PAYABLES_") || a.Name.Contains("Kreditorlar")) && a.IsActive)
                    .ToList();

                var receivablesAccounts = db.FIN_ACCOUNTS
                    .Where(a => (a.Code.StartsWith("RECEIVABLES_") || a.Name.Contains("Debitorlar")) && a.IsActive)
                    .ToList();

                var payablesAccountBalances = new List<AccountBalanceData>();
                foreach (var account in payablesAccounts)
                {
                    var allTransactions = ActiveFinTransactions(db)
                        .Where(t => t.AccountID == account.ID && t.TxnDate <= toDate.Value)
                        .ToList();

                    var qoldiq = allTransactions
                        .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;

                    var periodTransactions = allTransactions
                        .Where(t => t.TxnDate >= fromDate.Value && t.TxnDate <= toDate.Value)
                        .ToList();

                    var kirim = periodTransactions
                        .Where(t => t.Direction == 1)
                        .Sum(t => (decimal?)t.Amount) ?? 0m;

                    var chiqim = periodTransactions
                        .Where(t => t.Direction == 2)
                        .Sum(t => (decimal?)t.Amount) ?? 0m;

                    var currency = account.CURRENCy;
                    if (currency == null && account.CurrencyID > 0)
                    {
                        currency = db.CURRENCIES.Find(account.CurrencyID);
                    }
                    var currencyShortname = currency?.Shortname ?? "UZS";

                    payablesAccountBalances.Add(new AccountBalanceData
                    {
                        AccountID = account.ID,
                        AccountCode = account.Code ?? "",
                        AccountName = account.Name ?? "",
                        OwnerName = account.OwnerName ?? "-",
                        CurrencyShortname = currencyShortname,
                        Kirim = kirim,
                        Chiqim = chiqim,
                        Qoldiq = qoldiq
                    });
                }

                var receivablesAccountBalances = new List<AccountBalanceData>();
                foreach (var account in receivablesAccounts)
                {
                    var allTransactions = ActiveFinTransactions(db)
                        .Where(t => t.AccountID == account.ID && t.TxnDate <= toDate.Value)
                        .ToList();

                    var qoldiq = allTransactions
                        .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;

                    var periodTransactions = allTransactions
                        .Where(t => t.TxnDate >= fromDate.Value && t.TxnDate <= toDate.Value)
                        .ToList();

                    var kirim = periodTransactions
                        .Where(t => t.Direction == 1)
                        .Sum(t => (decimal?)t.Amount) ?? 0m;

                    var chiqim = periodTransactions
                        .Where(t => t.Direction == 2)
                        .Sum(t => (decimal?)t.Amount) ?? 0m;

                    var currency = account.CURRENCy;
                    if (currency == null && account.CurrencyID > 0)
                    {
                        currency = db.CURRENCIES.Find(account.CurrencyID);
                    }
                    var currencyShortname = currency?.Shortname ?? "UZS";

                    receivablesAccountBalances.Add(new AccountBalanceData
                    {
                        AccountID = account.ID,
                        AccountCode = account.Code ?? "",
                        AccountName = account.Name ?? "",
                        OwnerName = account.OwnerName ?? "-",
                        CurrencyShortname = currencyShortname,
                        Kirim = kirim,
                        Chiqim = chiqim,
                        Qoldiq = qoldiq
                    });
                }

                // Olish primary currency
                var primaryCurrency = payablesTransactions
                    .Concat(receivablesTransactions)
                    .Where(t => t.CURRENCy != null)
                    .GroupBy(t => t.CurrencyID)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.First().CURRENCy)
                    .FirstOrDefault() ?? db.CURRENCIES.FirstOrDefault();

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.TotalPayables = totalPayables;
                ViewBag.TotalReceivables = totalReceivables;
                ViewBag.PayrollLiability = payrollLiability;
                ViewBag.PayablesBySupplier = payablesBySupplier;
                ViewBag.ReceivablesByCustomer = receivablesByCustomer;
                ViewBag.MonthlyPayables = monthlyPayables;
                ViewBag.MonthlyReceivables = monthlyReceivables;
                ViewBag.MonthlyPayrollLiability = monthlyPayrollLiability;
                ViewBag.PayablesAccountBalances = payablesAccountBalances;
                ViewBag.ReceivablesAccountBalances = receivablesAccountBalances;
                ViewBag.CurrencyShortname = primaryCurrency?.Shortname ?? "UZS";
                ViewBag.PassiveBalanceStats = BuildBalanceCategoryDetails(db, toDate.Value, new[] { 2, 3 }, false);

                return View();
            }
        }

        private static IQueryable<FIN_TRANSACTIONS> ActiveFinTransactions(DBTHSNEntities db)
        {
            return db.FIN_TRANSACTIONS.Where(t => t.IsDeleted != true);
        }
    }

    public class AccountBalanceData
    {
        public int AccountID { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public string OwnerName { get; set; }
        public string CurrencyShortname { get; set; }
        public decimal Kirim { get; set; } // Period kirim (vaqt oralig'idagi)
        public decimal Chiqim { get; set; } // Period chiqim (vaqt oralig'idagi)
        public decimal Qoldiq { get; set; } // Cumulative balance (barcha vaqt uchun, toDate gacha)

        /// <summary>
        /// true bo'lsa, qoldiq opening balans + opening sanasidan keyingi tranzaksiyalar bo'yicha hisoblangan.
        /// </summary>
        public bool IncludesOpeningBalance { get; set; }
    }

    public class AccountUsageData
    {
        public int AccountID { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public string AccountType { get; set; }
        public decimal TotalInflow { get; set; }
        public decimal TotalOutflow { get; set; }
        public int TransactionCount { get; set; }
        public decimal NetAmount { get; set; }
    }

    public class ExpenseByCategoryData
    {
        public string CategoryName { get; set; }
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class ExpenseByAccountData
    {
        public string AccountName { get; set; }
        public decimal Amount { get; set; }
    }

    public class IncomeByCategoryData
    {
        public string CategoryName { get; set; }
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class IncomeByAccountData
    {
        public string AccountName { get; set; }
        public decimal Amount { get; set; }
    }

    public class MonthlyData
    {
        public string Period { get; set; }
        public decimal Amount { get; set; }
    }

    public class PLItemData
    {
        public FIN_PL_ITEMS Item { get; set; }
        public decimal Actual { get; set; }
        public decimal Plan { get; set; }
        public decimal Variance { get; set; }
        public decimal VariancePercent { get; set; }
        public List<PLItemData> Children { get; set; }
    }

    public class BalanceCategoryData
    {
        public FIN_BALANCE_CATEGORIES Category { get; set; }
        public decimal Amount { get; set; }

        /// <summary>
        /// true = "Aktivlar" ustuni (chap); false = "Passivlar va Kapital" (o'ng).
        /// </summary>
        public bool IsAssetColumn { get; set; }
    }

    public class BalanceCategoryDetailData
    {
        public FIN_BALANCE_CATEGORIES Category { get; set; }
        public decimal Amount { get; set; }
        public string CalculationTypeName { get; set; }
        public int TransactionCount { get; set; }
        public decimal AddedAmount { get; set; }
        public decimal SubtractedAmount { get; set; }
        public string Explanation { get; set; }
    }

    public class ShareholderValueData
    {
        public SHAREHOLDER Shareholder { get; set; }
        public decimal SharePercent { get; set; }
        public decimal Value { get; set; }
    }

    /// <summary>FIN_RECURRING_OBLIGATIONS jadvalidan SqlQuery uchun qator.</summary>
    public class RecurringObligationRow
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public decimal MonthlyAmount { get; set; }
        public int CurrencyID { get; set; }
        public int StartYear { get; set; }
        public byte StartMonth { get; set; }
    }
}
