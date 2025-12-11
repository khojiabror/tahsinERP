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

                // Get current cash balance
                var cashAccounts = db.FIN_ACCOUNTS
                    .Where(a => (a.IsCash || a.IsBank) && a.IsActive)
                    .ToList();

                var currentBalance = 0m;
                foreach (var account in cashAccounts)
                {
                    var accountBalance = db.FIN_TRANSACTIONS
                        .Where(t => t.AccountID == account.ID)
                        .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;
                    currentBalance += accountBalance;
                }

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

                // Calculate from transactions if snapshots don't exist
                var balanceData = new List<BalanceCategoryData>();
                foreach (var category in categories)
                {
                    decimal amount = 0;
                    if (snapshots.ContainsKey(category.ID))
                    {
                        amount = snapshots[category.ID];
                    }
                    else
                    {
                        // Calculate from transactions (simplified - you may need more complex logic)
                        // This is a placeholder - actual calculation depends on category type
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
