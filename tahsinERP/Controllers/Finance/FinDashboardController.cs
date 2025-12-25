using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Controllers;
using System.Linq.Expressions;

namespace tahsinERP.Controllers.Finance
{
    public class FinDashboardController : Controller
    {
        // GET: FinDashboard/Cashflow
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
                var query = db.FIN_TRANSACTIONS
                    .Include(t => t.FIN_ACCOUNTS)
                    .Include(t => t.FIN_CATEGORIES)
                    .Include(t => t.CURRENCy)
                    .Where(t => t.TxnDate >= fromDate.Value && t.TxnDate <= toDate.Value);

                if (accountID.HasValue)
                {
                    query = query.Where(t => t.AccountID == accountID.Value);
                }

                var transactions = query.ToList();

                // Calculate totals
                var inflow = transactions.Where(t => t.Direction == 1).Sum(t => (decimal?)t.Amount) ?? 0m;
                var outflow = transactions.Where(t => t.Direction == 2).Sum(t => (decimal?)t.Amount) ?? 0m;
                var netCashflow = inflow - outflow;

                // Get current cash balance - only from Cash and Bank accounts
                // Separate calculation for Cash (naqd pul) and Bank accounts
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
                    var accountBalance = db.FIN_TRANSACTIONS
                        .Where(t => t.AccountID == account.ID)
                        .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                    cashBalance += accountBalance;
                }

                // Calculate bank balance
                var bankBalance = 0m;
                foreach (var account in bankAccounts)
                {
                    var accountBalance = db.FIN_TRANSACTIONS
                        .Where(t => t.AccountID == account.ID)
                        .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                    bankBalance += accountBalance;
                }

                // Total current balance = Cash + Bank
                var currentBalance = cashBalance + bankBalance;

                // Get primary currency (most used currency in transactions, or from cash/bank accounts)
                var primaryCurrency = transactions
                    .Where(t => t.CURRENCy != null)
                    .GroupBy(t => t.CurrencyID)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.First().CURRENCy)
                    .FirstOrDefault();

                // Fallback: get currency from cash/bank accounts if no transactions
                if (primaryCurrency == null)
                {
                    var firstCashAccount = cashAccounts.FirstOrDefault() ?? bankAccounts.FirstOrDefault();
                    if (firstCashAccount != null)
                    {
                        primaryCurrency = db.CURRENCIES.Find(firstCashAccount.CurrencyID);
                    }
                }

                // Final fallback: get first currency from database
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
                        DateValue = g.Key, // Keep original date for sorting
                        Date = g.Key.ToString("dd.MM.yyyy"), // Format date as string for display
                        Inflow = g.Where(t => t.Direction == 1).Sum(t => (decimal?)t.Amount) ?? 0m,
                        Outflow = g.Where(t => t.Direction == 2).Sum(t => (decimal?)t.Amount) ?? 0m
                    })
                    .OrderBy(x => x.DateValue) // Sort by date value
                    .Select(x => new { x.Date, x.Inflow, x.Outflow }) // Remove DateValue from final result
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

        // GET: FinDashboard/PL
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

                // Get transactions for the period
                var transactions = db.FIN_TRANSACTIONS
                    .Include(t => t.FIN_CATEGORIES)
                    .Where(t => t.TxnDate >= startDate && t.TxnDate <= endDate)
                    .ToList();

                // Get PL items with their mapped categories
                var plItems = db.FIN_PL_ITEMS
                    .Where(i => i.IsActive)
                    .OrderBy(i => i.SortOrder)
                    .ToList();

                var plData = new List<PLItemData>();
                foreach (var item in plItems)
                {
                    // Get categories mapped to this PL item
                    var categoryIds = db.FIN_CATEGORIES
                        .Where(c => c.FIN_PL_ITEMS.Any(pi => pi.ID == item.ID))
                        .Select(c => c.ID)
                        .ToList();

                    // Calculate actual amount
                    var actual = transactions
                        .Where(t => categoryIds.Contains(t.CategoryID))
                        .Sum(t => (decimal?)(item.Sign == 1 ? (t.Direction == 1 ? t.Amount : -t.Amount) : (t.Direction == 2 ? t.Amount : -t.Amount))) ?? 0m;

                    // Get plan
                    var plan = db.FIN_PL_PLANS
                        .FirstOrDefault(p => p.Year == year.Value && p.Month == month.Value && p.ItemID == item.ID);

                    plData.Add(new PLItemData
                    {
                        Item = item,
                        Actual = actual,
                        Plan = plan?.AmountPlan ?? 0,
                        Variance = actual - (plan?.AmountPlan ?? 0),
                        VariancePercent = plan?.AmountPlan > 0 ? ((actual - plan.AmountPlan) / plan.AmountPlan * 100) : 0
                    });
                }

                // Get plans
                var plans = db.FIN_PL_PLANS
                    .Where(p => p.Year == year.Value && p.Month == month.Value)
                    .ToDictionary(p => p.ItemID, p => p.AmountPlan);

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

        // GET: FinDashboard/BalanceSheet
        public ActionResult BalanceSheet(DateTime? asOfDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "FinDashboard"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            asOfDate = asOfDate ?? DateTime.Now;

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Get balance categories
                var categories = db.FIN_BALANCE_CATEGORIES
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .ToList();

                // Get snapshots for the date
                var snapshots = db.FIN_BALANCE_SNAPSHOTS
                    .Where(s => s.SnapshotDate <= asOfDate.Value)
                    .GroupBy(s => s.CategoryID)
                    .Select(g => new
                    {
                        CategoryID = g.Key,
                        Amount = g.OrderByDescending(s => s.SnapshotDate).FirstOrDefault().Amount
                    })
                    .ToDictionary(x => x.CategoryID, x => x.Amount);

                // Calculate from transactions and accounts
                // Map accounts to balance categories by name matching
                var accounts = db.FIN_ACCOUNTS.Where(a => a.IsActive).ToList();
                var accountBalances = new Dictionary<int, decimal>();
                
                foreach (var account in accounts)
                {
                    var balance = db.FIN_TRANSACTIONS
                        .Where(t => t.AccountID == account.ID && t.TxnDate <= asOfDate.Value)
                        .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                    accountBalances[account.ID] = balance;
                }

                // Create mapping: Account -> Balance Category (by name matching)
                // For proper implementation, you should create a mapping table like FIN_ACCOUNT_BALANCE_CATEGORIES
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

                // Calculate balance data based on CalculationType
                // 1 = Account-Based: Calculate from account balances
                // 2 = Transaction-Based: Calculate from transaction categories
                // 3 = Document-Based: Calculate from documents (invoices, etc.)
                var balanceData = new List<BalanceCategoryData>();
                foreach (var category in categories)
                {
                    decimal amount = 0;
                    
                    // First check snapshots (highest priority)
                    if (snapshots.ContainsKey(category.ID))
                    {
                        amount = snapshots[category.ID];
                    }
                    else
                    {
                        // Calculate based on CalculationType
                        switch (category.CalculationType)
                        {
                            case 1: // Account-Based
                                // Get accounts mapped to this balance category
                                var mappedAccountIds = db.FIN_ACCOUNTS
                                    .Where(a => a.BalanceCategoryID == category.ID && a.IsActive)
                                    .Select(a => a.ID)
                                    .ToList();
                                
                                // Fallback to old name matching if BalanceCategoryID is not set
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
                                        amount += accountBalances[accountId];
                                    }
                                }
                                break;
                                
                            case 2: // Transaction-Based
                                // Get categories mapped to this balance category
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
                                    // If relationship doesn't work, use raw SQL to query junction table
                                    try
                                    {
                                        var sql = @"
                                            SELECT CategoryID 
                                            FROM FIN_BALANCE_CATEGORY_CATEGORIES 
                                            WHERE BalanceCategoryID = {0}";
                                        var categoryIdList = db.Database.SqlQuery<int>(sql, category.ID).ToList();
                                        
                                        // Get active categories from the junction table results
                                        categoryIds = db.FIN_CATEGORIES
                                            .Where(c => categoryIdList.Contains(c.ID) && c.IsActive)
                                            .Select(c => c.ID)
                                            .ToList();
                                    }
                                    catch
                                    {
                                        // If junction table doesn't exist or query fails, return empty list
                                        categoryIds = new List<int>();
                                    }
                                }
                                
                                if (categoryIds.Count > 0)
                                {
                                    // Calculate from transactions in mapped categories
                                    amount = db.FIN_TRANSACTIONS
                                        .Where(t => categoryIds.Contains(t.CategoryID) && 
                                                   t.TxnDate <= asOfDate.Value)
                                        .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                                }
                                break;
                                
                            case 3: // Document-Based
                                // Calculate from documents based on category name
                                if (category.Name == "Ta'minotchilardan qarzlar")
                                {
                                    // To'lanmagan xarid invoyslari
                                    var unpaidInvoices = db.P_INVOICES
                                        .Where(i => i.InvoiceDate <= asOfDate.Value && 
                                                   (i.IsDeleted == null || i.IsDeleted == false))
                                        .ToList();
                                    
                                    foreach (var invoice in unpaidInvoices)
                                    {
                                        // Calculate total payments for this invoice
                                        var totalPaid = db.FIN_TRANSACTIONS
                                            .Where(t => t.SourceDocType == "PurchaseInvoice" && 
                                                       t.SourceDocID == invoice.ID && 
                                                       t.TxnDate <= asOfDate.Value &&
                                                       t.Direction == 1) // Payments (incoming to supplier)
                                            .Sum(t => (decimal?)t.Amount) ?? 0m;
                                        
                                        var invoiceAmount = (decimal)invoice.Amount;
                                        
                                        // If not fully paid, add unpaid amount
                                        if (totalPaid < invoiceAmount)
                                        {
                                            amount += (invoiceAmount - totalPaid);
                                        }
                                    }
                                }
                                else if (category.Name == "Xaridorlardan haqlarimiz")
                                {
                                    // To'lanmagan sotuv invoyslari
                                    var unpaidInvoices = db.SALES_INVOICES
                                        .Where(i => i.InvoiceDate <= asOfDate.Value && 
                                                   (i.StatusCode == null || i.StatusCode != "Paid"))
                                        .ToList();
                                    
                                    foreach (var invoice in unpaidInvoices)
                                    {
                                        // Calculate total payments for this invoice
                                        var totalPaid = db.FIN_TRANSACTIONS
                                            .Where(t => t.SourceDocType == "SalesInvoice" && 
                                                       t.SourceDocID == invoice.ID && 
                                                       t.TxnDate <= asOfDate.Value &&
                                                       t.Direction == 1) // Payments (incoming from customer)
                                            .Sum(t => (decimal?)t.Amount) ?? 0m;
                                        
                                        var invoiceAmount = invoice.TotalAmount;
                                        
                                        // If not fully paid, add unpaid amount
                                        if (totalPaid < invoiceAmount)
                                        {
                                            amount += (invoiceAmount - totalPaid);
                                        }
                                    }
                                }
                                break;
                        }
                    }

                    balanceData.Add(new BalanceCategoryData
                    {
                        Category = category,
                        Amount = amount
                    });
                }

                ViewBag.AsOfDate = asOfDate;
                ViewBag.BalanceData = balanceData;

                return View();
            }
        }

        // GET: FinDashboard/ShareholderValue
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

                // Get shareholders with their current shares
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

            // Cash and Bank
            var cashAccounts = db.FIN_ACCOUNTS
                .Where(a => (a.IsCash || a.IsBank) && a.IsActive)
                .ToList();

            foreach (var account in cashAccounts)
            {
                var balance = db.FIN_TRANSACTIONS
                    .Where(t => t.AccountID == account.ID && t.TxnDate <= asOfDate)
                    .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                value += balance;
            }

            // Inventory (simplified - you may need to calculate from PRODUCT_STOCKS)
            // This is a placeholder

            // Receivables from Sales Invoices (simplified - check if invoice is paid)
            var receivables = db.SALES_INVOICES
                .Where(i => i.InvoiceDate <= asOfDate && (i.StatusCode == null || i.StatusCode != "Paid"))
                .ToList()
                .Sum(i => (decimal?)i.TotalAmount) ?? 0m;
            value += receivables;

            // Subtract Payables (simplified calculation)
            // Note: P_INVOICES.Amount is double, so we need to convert to decimal
            var payables = db.P_INVOICES
                .Where(i => i.InvoiceDate <= asOfDate && (i.IsDeleted == null || i.IsDeleted == false))
                .ToList()
                .Sum(i => (decimal?)(decimal)i.Amount) ?? 0m;
            value -= payables;

            return value;
        }
    }

    public class PLItemData
    {
        public FIN_PL_ITEMS Item { get; set; }
        public decimal Actual { get; set; }
        public decimal Plan { get; set; }
        public decimal Variance { get; set; }
        public decimal VariancePercent { get; set; }
    }

    public class BalanceCategoryData
    {
        public FIN_BALANCE_CATEGORIES Category { get; set; }
        public decimal Amount { get; set; }
    }

    public class ShareholderValueData
    {
        public SHAREHOLDER Shareholder { get; set; }
        public decimal SharePercent { get; set; }
        public decimal Value { get; set; }
    }
}
