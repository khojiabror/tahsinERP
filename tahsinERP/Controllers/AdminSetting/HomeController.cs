using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels.Home;

namespace tahsinERP.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var userEmail = User.Identity.Name;
            if (CookieHelper.IsConfirmed(userEmail, HttpContext) == false)
            {
                Session["HasSeenCookieConsent"] = true;
                ViewBag.ShowCookieConsent = true;
            }
            else
            {
                ViewBag.ShowCookieConsent = false;
            }
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var primaryCurrency = db.FIN_TRANSACTIONS
                    .Where(t => t.IsDeleted == false || t.IsDeleted == null)
                    .Where(t => t.CURRENCy != null)
                    .GroupBy(t => t.CurrencyID)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.FirstOrDefault().CURRENCy)
                    .FirstOrDefault();

                if (primaryCurrency == null)
                {
                    primaryCurrency = db.CURRENCIES
                        .Where(c => c.IsDeleted != true)
                        .OrderBy(c => c.ID)
                        .FirstOrDefault();
                }

                ViewBag.CurrencyShortname = primaryCurrency?.Shortname ?? "";
            }
            return View();
        }

        // Finance Module - P&L Plan vs Fact Monthly
        [HttpGet]
        public JsonResult GetFinancePlanVsFact(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    // Olish plans
                    var plans = db.FIN_PL_PLANS
                        .Where(p => (!fromDate.HasValue || p.Year * 100 + p.Month >= fromDate.Value.Year * 100 + fromDate.Value.Month) &&
                                   (!toDate.HasValue || p.Year * 100 + p.Month <= toDate.Value.Year * 100 + toDate.Value.Month))
                        .GroupBy(p => new { p.Year, p.Month })
                        .Select(g => new
                        {
                            Year = g.Key.Year,
                            Month = g.Key.Month,
                            Plan = g.Sum(x => (double?)x.AmountPlan) ?? 0
                        })
                        .ToList();

                    // Olish actuals dan transactions - exclude liability transactions (faqat real transactions)
                    var transactions = db.FIN_TRANSACTIONS
                        .Include(t => t.FIN_CATEGORIES)
                        .Where(t => (!fromDate.HasValue || t.TxnDate >= fromDate.Value) &&
                                   (!toDate.HasValue || t.TxnDate <= toDate.Value) &&
                                   // Exclude liability transactions (PurchaseInvoice va SalesInvoice) - faqat show real transactions
                                   ((t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice") || t.SourceDocType == null || t.SourceDocType == ""))
                        .ToList();

                    // Olish PL items bilan mapped categories
                    var plItems = db.FIN_PL_ITEMS
                        .Where(i => i.IsActive)
                        .ToList();

                    var factByPeriod = new Dictionary<string, double>();

                    foreach (var plItem in plItems)
                    {
                        // Olish categories mapped ga this PL item
                        var categoryIds = db.FIN_CATEGORIES
                            .Where(c => c.FIN_PL_ITEMS.Any(pi => pi.ID == plItem.ID))
                            .Select(c => c.ID)
                            .ToList();

                        if (categoryIds.Count > 0)
                        {
                            var itemTransactions = transactions
                                .Where(t => categoryIds.Contains(t.CategoryID))
                                .ToList();

                            var itemAmounts = itemTransactions
                                .GroupBy(t => new { Year = t.TxnDate.Year, Month = t.TxnDate.Month })
                                .Select(g => new
                                {
                                    Year = g.Key.Year,
                                    Month = g.Key.Month,
                                    Amount = g.Sum(t => (double?)(plItem.Sign == 1 ? 
                                        (t.Direction == 1 ? (double)t.Amount : -(double)t.Amount) : 
                                        (t.Direction == 2 ? (double)t.Amount : -(double)t.Amount))) ?? 0
                                })
                                .ToList();

                            foreach (var itemAmount in itemAmounts)
                            {
                                var periodKey = $"{itemAmount.Year}-{itemAmount.Month:D2}";
                                if (!factByPeriod.ContainsKey(periodKey))
                                {
                                    factByPeriod[periodKey] = 0;
                                }
                                factByPeriod[periodKey] += itemAmount.Amount;
                            }
                        }
                    }

                    // Combine plans va facts
                    var allPeriods = plans.Select(p => $"{p.Year}-{p.Month:D2}")
                        .Union(factByPeriod.Keys)
                        .Distinct()
                        .OrderBy(p => p)
                        .ToList();

                    var result = allPeriods.Select(period => 
                    {
                        var parts = period.Split('-');
                        var year = int.Parse(parts[0]);
                        var month = int.Parse(parts[1]);
                        var periodStartDate = new DateTime(year, month, 1);

                        var plan = plans.FirstOrDefault(p => $"{p.Year}-{p.Month:D2}" == period)?.Plan ?? 0;
                        var fact = factByPeriod.ContainsKey(period) ? factByPeriod[period] : 0;
                        var delta = fact - plan;

                        return new
                        {
                            period = period,
                            plan = plan,
                            fact = fact,
                            delta = delta
                        };
                    }).ToList();

                    return Json(result, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Finance Module - P&L Fact Monthly
        [HttpGet]
        public JsonResult GetFinanceFactMonthly(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var query = db.V_FIN_PL_FACT_MONTHLY.AsQueryable();
                    
                    if (fromDate.HasValue)
                        query = query.Where(x => x.PeriodStartDate >= fromDate.Value);
                    if (toDate.HasValue)
                        query = query.Where(x => x.PeriodStartDate <= toDate.Value);

                    var data = query.OrderBy(x => x.PeriodStartDate).ThenBy(x => x.SortOrder).ToList();
                    
                    var result = data.GroupBy(x => new { x.ItemName, x.PeriodStartDate })
                        .Select(g => new
                        {
                            item = g.Key.ItemName,
                            period = g.Key.PeriodStartDate?.ToString("yyyy-MM") ?? "N/A",
                            amount = g.Sum(x => (double?)x.AmountFact) ?? 0
                        }).ToList();

                    return Json(result, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Finance Module - Cash Summary
        [HttpGet]
        public JsonResult GetFinanceCashSummary(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var from = fromDate ?? DateTime.Now.AddMonths(-6);
                    var to = toDate ?? DateTime.Now;
                    
                    // Olish cash va bank accounts faqat
                    var accounts = db.FIN_ACCOUNTS
                        .Where(a => (a.IsCash || a.IsBank) && a.IsActive)
                        .ToList();

                    var data = new List<object>();

                    foreach (var account in accounts)
                    {
                        // Opening balance (before dan date) - exclude liability transactions
                        var openingTransactions = db.FIN_TRANSACTIONS
                            .Where(t => t.AccountID == account.ID && 
                                       t.TxnDate < from &&
                                       // Exclude liability transactions
                                       ((t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice") || t.SourceDocType == null || t.SourceDocType == ""))
                            .ToList();

                        var openingBalance = openingTransactions
                            .Sum(t => (decimal?)(t.Direction == 1 ? t.Amount : -t.Amount)) ?? 0m;

                        // Income (within date range) - exclude liability transactions
                        var incomeTransactions = db.FIN_TRANSACTIONS
                            .Where(t => t.AccountID == account.ID && 
                                       t.Direction == 1 &&
                                       t.TxnDate >= from && 
                                       t.TxnDate <= to &&
                                       // Exclude liability transactions
                                       ((t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice") || t.SourceDocType == null || t.SourceDocType == ""))
                            .ToList();

                        var income = incomeTransactions
                            .Sum(t => (decimal?)t.Amount) ?? 0m;

                        // Expense (within date range) - exclude liability transactions
                        var expenseTransactions = db.FIN_TRANSACTIONS
                            .Where(t => t.AccountID == account.ID && 
                                       t.Direction == 2 &&
                                       t.TxnDate >= from && 
                                       t.TxnDate <= to &&
                                       // Exclude liability transactions
                                       ((t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice") || t.SourceDocType == null || t.SourceDocType == ""))
                            .ToList();

                        var expense = expenseTransactions
                            .Sum(t => (decimal?)t.Amount) ?? 0m;

                        var closingBalance = openingBalance + income - expense;

                        data.Add(new
                        {
                            accountName = account.Name ?? "",
                            accountCode = account.Code ?? "",
                            openingBalance = (double)openingBalance,
                            income = (double)income,
                            expense = (double)expense,
                            closingBalance = (double)closingBalance
                        });
                    }

                    return Json(data, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // SRM Module - Product Stocks Statistics
        [HttpGet]
        public JsonResult GetSRMMaterialRequirements()
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var data = (from stock in db.PRODUCT_STOCKS
                               join product in db.PRODUCTS on stock.ProductID equals product.ID
                               where product.IsDeleted == false
                               group stock by new { product.PNo, product.ID } into g
                               select new
                               {
                                   productNo = g.Key.PNo,
                                   totalStock = g.Sum(x => (double?)x.Amount) ?? 0,
                                   warehouseCount = g.Select(x => x.WHID).Distinct().Count()
                               })
                               .OrderByDescending(x => x.totalStock)
                               .Take(10)
                               .ToList();

                    return Json(data, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // CRM Module - Sales Orders Statistics
        [HttpGet]
        public JsonResult GetCRMSalesOrdersStats(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var query = db.S_ORDERS.Where(x => x.IsDeleted == false);
                    
                    if (fromDate.HasValue)
                        query = query.Where(x => x.IssuedDate >= fromDate.Value);
                    if (toDate.HasValue)
                        query = query.Where(x => x.IssuedDate <= toDate.Value);

                    // Avval ma'lumotlarni memory'ga yuklash
                    var data = query
                        .GroupBy(x => new { Year = x.IssuedDate.Year, Month = x.IssuedDate.Month })
                        .Select(g => new
                        {
                            Year = g.Key.Year,
                            Month = g.Key.Month,
                            orderCount = g.Count(),
                            totalAmount = g.Sum(x => (double?)x.Amount) ?? 0
                        })
                        .ToList()
                        // Keyin string formatlash (memory'da)
                        .Select(g => new
                        {
                            period = string.Format("{0}-{1:D2}", g.Year, g.Month),
                            orderCount = g.orderCount,
                            totalAmount = g.totalAmount
                        })
                        .OrderBy(x => x.period)
                        .ToList();

                    return Json(data, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // CRM Module - Customers Statistics
        [HttpGet]
        public JsonResult GetCRMCustomersStats()
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var totalCustomers = db.CUSTOMERS.Count();
                    var activeCustomers = db.CUSTOMERS.Count(); // Add IsActive filtrlash agar mavjud
                    var totalOrders = db.S_ORDERS.Count(x => x.IsDeleted == false);
                    var totalContracts = db.S_CONTRACTS.Count();

                    return Json(new
                    {
                        totalCustomers,
                        activeCustomers,
                        totalOrders,
                        totalContracts
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // MES Module - Production Plans Statistics
        [HttpGet]
        public JsonResult GetMESProductionStats(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var query = db.PRODUCTPLANS.Where(x => x.IsDeleted == false);
                    
                    if (fromDate.HasValue)
                        query = query.Where(x => x.StartDate >= fromDate.Value);
                    if (toDate.HasValue)
                        query = query.Where(x => x.StartDate <= toDate.Value);

                    // Avval ma'lumotlarni memory'ga yuklash
                    var data = query
                        .GroupBy(x => new { Year = x.StartDate.Year, Month = x.StartDate.Month })
                        .Select(g => new
                        {
                            Year = g.Key.Year,
                            Month = g.Key.Month,
                            planCount = g.Count(),
                            totalQuantity = g.Sum(x => (double?)x.Amount) ?? 0
                        })
                        .ToList()
                        // Keyin string formatlash (memory'da)
                        .Select(g => new
                        {
                            period = string.Format("{0}-{1:D2}", g.Year, g.Month),
                            planCount = g.planCount,
                            totalQuantity = g.totalQuantity
                        })
                        .OrderBy(x => x.period)
                        .ToList();

                    return Json(data, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // HR Module - Basic Statistics
        [HttpGet]
        public JsonResult GetHRStats()
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var totalEmployees = db.HR_EMPLOYEES.Count();
                    var activeEmployees = db.HR_EMPLOYEES.Count(e => e.IsActive);
                    var totalDepartments = db.HR_ORG_UNITS.Count(d => d.IsActive);

                    return Json(new
                    {
                        totalEmployees,
                        activeEmployees,
                        totalDepartments
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Tasks Module - Statistics
        [HttpGet]
        public JsonResult GetTasksStats()
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var totalTasks = db.APP_TASKS.Count(t => t.IsDeleted == false);
                    var completedTasks = db.APP_TASKS.Count(t => t.IsDeleted == false && 
                        t.APP_TASK_STATUS_REF != null && t.APP_TASK_STATUS_REF.Code == "DONE");
                    var pendingTasks = totalTasks - completedTasks;

                    return Json(new
                    {
                        totalTasks,
                        completedTasks,
                        pendingTasks
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // hujjatlar Module - Statistics
        [HttpGet]
        public JsonResult GetDocumentsStats()
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    // Date'ni query'dan tashqarida hisoblash
                    var oneMonthAgo = DateTime.Now.AddMonths(-1);
                    
                    var totalDocuments = db.DOC_DOCUMENTS.Count();
                    var recentDocuments = db.DOC_DOCUMENTS.Count(d => 
                        d.CreatedDT >= oneMonthAgo);

                    return Json(new
                    {
                        totalDocuments,
                        recentDocuments
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        public JsonResult CookieConfirm()
        {
            try
            {
                var userEmail = User.Identity.Name;
                CookieHelper.Confirm(userEmail, HttpContext);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public ActionResult About()
        {
            ViewBag.Message = "Ilova tavsif sahifasi.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Sizning aloqa sahifangiz.";

            return View();
        }
    }
}
