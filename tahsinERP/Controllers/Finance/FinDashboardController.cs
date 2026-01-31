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
                    .Where(t => t.TxnDate >= fromDate.Value && t.TxnDate <= toDate.Value)
                    // Exclude liability transactions (PurchaseInvoice and SalesInvoice) - only show real cash flow
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

                // Get transactions for the period - exclude liability transactions (only real P&L)
                var transactions = db.FIN_TRANSACTIONS
                    .Include(t => t.FIN_CATEGORIES)
                    .Where(t => t.TxnDate >= startDate && t.TxnDate <= endDate)
                    // Exclude liability transactions (PurchaseInvoice and SalesInvoice) - only show real transactions
                    .Where(t => (t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice") || t.SourceDocType == null || t.SourceDocType == "")
                    .ToList();

                // Get all PL items with their mapped categories
                var allPlItems = db.FIN_PL_ITEMS
                    .Include(i => i.FIN_PL_ITEMS1)
                    .Where(i => i.IsActive)
                    .OrderBy(i => i.SortOrder)
                    .ToList();

                // Get all plans for the period
                var plans = db.FIN_PL_PLANS
                    .Where(p => p.Year == year.Value && p.Month == month.Value)
                    .ToDictionary(p => p.ItemID, p => p.AmountPlan);

                // Helper method to calculate PL item data
                PLItemData CalculateItemData(FIN_PL_ITEMS item)
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

                // Build hierarchical structure - only get parent items (ParentID == null)
                var plData = new List<PLItemData>();
                var parentItems = allPlItems.Where(i => i.ParentID == null).OrderBy(i => i.SortOrder).ToList();

                foreach (var parentItem in parentItems)
                {
                    var parentData = CalculateItemData(parentItem);
                    
                    // Get and calculate children
                    var childItems = allPlItems.Where(i => i.ParentID == parentItem.ID).OrderBy(i => i.SortOrder).ToList();
                    foreach (var childItem in childItems)
                    {
                        var childData = CalculateItemData(childItem);
                        parentData.Children.Add(childData);
                        
                        // Add child's actual to parent's actual (for summary)
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
                                // NOTE: "Ta'minotchilardan qarzlar" uchun Account-Based (CalculationType = 1) ishlatish tavsiya etiladi
                                // Bu ERP standartlariga mos (SAP, Oracle, Microsoft Dynamics)
                                // Account-Based: Payables_5 account qoldig'i to'g'ridan-to'g'ri ishlatiladi
                                if (category.Name == "Ta'minotchilardan qarzlar")
                                {
                                    // Yechim 2: Account-Based ishlatish (Document-Based o'rniga)
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
                                                amount += accountBalances[accountId];
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Fallback: Document-Based (eski usul, Account-Based sozlanmagan bo'lsa)
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
                                }
                                else if (category.Name == "Xaridorlardan haqlarimiz")
                                {
                                    // Yechim 2: Account-Based ishlatish (Document-Based o'rniga)
                                    // Avval Account-Based orqali hisoblashga harakat qilamiz
                                    var receivablesAccountIds = db.FIN_ACCOUNTS
                                        .Where(a => a.BalanceCategoryID == category.ID && a.IsActive)
                                        .Select(a => a.ID)
                                        .ToList();
                                    
                                    if (receivablesAccountIds.Count > 0)
                                    {
                                        // Account-Based: Account qoldiqlarini yig'ish
                                        foreach (var accountId in receivablesAccountIds)
                                        {
                                            if (accountBalances.ContainsKey(accountId))
                                            {
                                                amount += accountBalances[accountId];
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Fallback: Document-Based (eski usul, Account-Based sozlanmagan bo'lsa)
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

        // GET: FinDashboard/FinancialOverview
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
                // Get summary statistics - exclude liability transactions (only real cash flow)
                var transactions = db.FIN_TRANSACTIONS
                    .Where(t => t.TxnDate >= fromDate.Value && t.TxnDate <= toDate.Value)
                    // Exclude liability transactions (PurchaseInvoice and SalesInvoice) - only show real transactions
                    .Where(t => (t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice") || t.SourceDocType == null || t.SourceDocType == "")
                    .ToList();

                var totalIncome = transactions.Where(t => t.Direction == 1).Sum(t => (decimal?)t.Amount) ?? 0m;
                var totalExpense = transactions.Where(t => t.Direction == 2).Sum(t => (decimal?)t.Amount) ?? 0m;
                var netCashflow = totalIncome - totalExpense;

                // Get account balances
                var cashAccounts = db.FIN_ACCOUNTS.Where(a => a.IsCash && a.IsActive).ToList();
                var bankAccounts = db.FIN_ACCOUNTS.Where(a => a.IsBank && a.IsActive).ToList();
                
                var cashBalance = 0m;
                foreach (var account in cashAccounts)
                {
                    cashBalance += db.FIN_TRANSACTIONS
                        .Where(t => t.AccountID == account.ID)
                        .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                }

                var bankBalance = 0m;
                foreach (var account in bankAccounts)
                {
                    bankBalance += db.FIN_TRANSACTIONS
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

                // Get primary currency
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

        // GET: FinDashboard/CategoryAnalytics
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
                var query = db.FIN_TRANSACTIONS
                    .Include(t => t.FIN_CATEGORIES)
                    .Where(t => t.TxnDate >= fromDate.Value && t.TxnDate <= toDate.Value)
                    // Exclude liability transactions - only show real transactions
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

                // Get primary currency
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

        // GET: FinDashboard/AccountAnalytics
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
                var query = db.FIN_TRANSACTIONS
                    .Include(t => t.FIN_ACCOUNTS)
                    .Include(t => t.CURRENCy)
                    .Where(t => t.TxnDate >= fromDate.Value && t.TxnDate <= toDate.Value)
                    // Exclude liability transactions - only show real transactions
                    .Where(t => (t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice") || t.SourceDocType == null || t.SourceDocType == "");

                if (accountID.HasValue)
                {
                    query = query.Where(t => t.AccountID == accountID.Value);
                }

                var transactions = query.ToList();

                // Account usage breakdown
                var accountUsage = transactions
                    .Where(t => t.FIN_ACCOUNTS != null && !string.IsNullOrEmpty(t.FIN_ACCOUNTS.Name))
                    .GroupBy(t => new { 
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

                // Get primary currency
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

        // GET: FinDashboard/ExpenseAnalysis
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
                var expenses = db.FIN_TRANSACTIONS
                    .Include(t => t.FIN_CATEGORIES)
                    .Include(t => t.FIN_ACCOUNTS)
                    .Where(t => t.Direction == 2 && t.TxnDate >= fromDate.Value && t.TxnDate <= toDate.Value)
                    // Exclude liability transactions (PurchaseInvoice) - only show real expenses
                    .Where(t => (t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice") || t.SourceDocType == null || t.SourceDocType == "")
                    .ToList();

                // Expense by category (pie chart data)
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

                // Get primary currency
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

        // GET: FinDashboard/IncomeAnalysis
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
                var incomes = db.FIN_TRANSACTIONS
                    .Include(t => t.FIN_CATEGORIES)
                    .Include(t => t.FIN_ACCOUNTS)
                    .Where(t => t.Direction == 1 && t.TxnDate >= fromDate.Value && t.TxnDate <= toDate.Value)
                    // Exclude liability transactions (SalesInvoice) - only show real income
                    .Where(t => (t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice") || t.SourceDocType == null || t.SourceDocType == "")
                    .ToList();

                // Income by category (pie chart data)
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

                // Get primary currency
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

        // GET: FinDashboard/AccountBalances
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
                // Get all active accounts with currency and balance category
                var accountsQuery = db.FIN_ACCOUNTS
                    .Include(a => a.CURRENCy)
                    .Include(a => a.FIN_BALANCE_CATEGORIES)
                    .Where(a => a.IsActive);

                // Filter by Asset (Type = 1) or Liability (Type = 2)
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

                var accountBalances = new List<AccountBalanceData>();

                foreach (var account in accounts)
                {
                    // Yechim 1: Account qoldig'i hisoblashda barcha transactionlar (toDate gacha)
                    // Cumulative balance - barcha vaqt uchun, faqat toDate gacha
                    var allTransactions = db.FIN_TRANSACTIONS
                        .Where(t => t.AccountID == account.ID && 
                                   t.TxnDate <= toDate.Value)
                        .ToList();

                    // Account qoldig'i = barcha transactionlar yig'indisi (cumulative)
                    var qoldiq = allTransactions
                        .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;

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

                    // Get currency for this account
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
                        Qoldiq = qoldiq
                    });
                }

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.AccountBalances = accountBalances;
                ViewBag.AccountType = accountType;

                return View();
            }
        }

        // GET: FinDashboard/LiabilitiesDashboard
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
                // Get only liability transactions (PurchaseInvoice and SalesInvoice)
                var payablesTransactions = db.FIN_TRANSACTIONS
                    .Include(t => t.FIN_ACCOUNTS)
                    .Include(t => t.FIN_CATEGORIES)
                    .Include(t => t.CURRENCy)
                    .Where(t => t.SourceDocType == "PurchaseInvoice" && 
                               t.TxnDate >= fromDate.Value && 
                               t.TxnDate <= toDate.Value)
                    .ToList();

                var receivablesTransactions = db.FIN_TRANSACTIONS
                    .Include(t => t.FIN_ACCOUNTS)
                    .Include(t => t.FIN_CATEGORIES)
                    .Include(t => t.CURRENCy)
                    .Where(t => t.SourceDocType == "SalesInvoice" && 
                               t.TxnDate >= fromDate.Value && 
                               t.TxnDate <= toDate.Value)
                    .ToList();

                // Payables summary
                var totalPayables = payablesTransactions
                    .Sum(t => (decimal?)t.Amount) ?? 0m;

                // Receivables summary
                var totalReceivables = receivablesTransactions
                    .Sum(t => (decimal?)t.Amount) ?? 0m;

                // Payables by supplier (counterparty)
                var payablesBySupplier = payablesTransactions
                    .Where(t => !string.IsNullOrEmpty(t.CounterpartyName))
                    .GroupBy(t => t.CounterpartyName)
                    .Select(g => new
                    {
                        SupplierName = g.Key,
                        Amount = g.Sum(t => (decimal?)t.Amount) ?? 0m,
                        TransactionCount = g.Count()
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

                // Monthly payables trend
                var monthlyPayables = payablesTransactions
                    .GroupBy(t => new { Year = t.TxnDate.Year, Month = t.TxnDate.Month })
                    .Select(g => new MonthlyData
                    {
                        Period = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Amount = g.Sum(t => (decimal?)t.Amount) ?? 0m
                    })
                    .OrderBy(x => x.Period)
                    .ToList();

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

                // Get account balances for Payables and Receivables
                var payablesAccounts = db.FIN_ACCOUNTS
                    .Where(a => (a.Code.StartsWith("PAYABLES_") || a.Name.Contains("Kreditorlar")) && a.IsActive)
                    .ToList();

                var receivablesAccounts = db.FIN_ACCOUNTS
                    .Where(a => (a.Code.StartsWith("RECEIVABLES_") || a.Name.Contains("Debitorlar")) && a.IsActive)
                    .ToList();

                var payablesAccountBalances = new List<AccountBalanceData>();
                foreach (var account in payablesAccounts)
                {
                    var allTransactions = db.FIN_TRANSACTIONS
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
                    var allTransactions = db.FIN_TRANSACTIONS
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

                // Get primary currency
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
                ViewBag.PayablesBySupplier = payablesBySupplier;
                ViewBag.ReceivablesByCustomer = receivablesByCustomer;
                ViewBag.MonthlyPayables = monthlyPayables;
                ViewBag.MonthlyReceivables = monthlyReceivables;
                ViewBag.PayablesAccountBalances = payablesAccountBalances;
                ViewBag.ReceivablesAccountBalances = receivablesAccountBalances;
                ViewBag.CurrencyShortname = primaryCurrency?.Shortname ?? "UZS";

                return View();
            }
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
    }

    public class ShareholderValueData
    {
        public SHAREHOLDER Shareholder { get; set; }
        public decimal SharePercent { get; set; }
        public decimal Value { get; set; }
    }
}
