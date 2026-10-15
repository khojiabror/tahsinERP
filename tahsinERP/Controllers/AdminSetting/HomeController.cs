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
                                   t.IsDeleted != true &&
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
                                       t.IsDeleted != true &&
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
                                       t.IsDeleted != true &&
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
                                       t.IsDeleted != true &&
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

        // Executive dashboard - cross-module leadership insights
        [HttpGet]
        public JsonResult GetExecutiveDashboard(DateTime? fromDate, DateTime? toDate, int? warehouseId)
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var to = toDate ?? DateTime.Today;
                    var from = fromDate ?? to.AddMonths(-1);
                    var trendFrom = new DateTime(to.Year, to.Month, 1).AddMonths(-11);
                    var sales60From = from.AddDays(-60);
                    var sales30From = to.AddDays(-30);
                    var selectedWarehouseId = warehouseId;

                    var warehouses = db.WAREHOUSES
                        .Where(w => w.IsDeleted != true)
                        .OrderBy(w => w.WHName)
                        .Select(w => new { id = w.ID, name = w.WHName })
                        .ToList();

                    var rawProductIds = db.PRODUCTS
                        .Where(p => p.IsDeleted == false &&
                                   (p.IsRawMaterial == true ||
                                   (p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.IsRawMaterial)))
                        .Select(p => p.ID)
                        .ToList();

                    var contracts = db.S_CONTRACTS
                        .Where(x => x.IsDeleted != true && x.IssuedDate >= from && x.IssuedDate <= to)
                        .Select(x => new { x.ID, x.IssuedDate, Amount = (double?)x.Amount ?? 0d })
                        .ToList();

                    var orders = db.S_ORDERS
                        .Where(x => x.IsDeleted != true && x.IssuedDate >= from && x.IssuedDate <= to)
                        .Select(x => new { x.ID, x.ContractID, x.IssuedDate, Amount = (double?)x.Amount ?? 0d })
                        .ToList();

                    var invoices = db.SALES_INVOICES
                        .Where(x => x.InvoiceDate >= from && x.InvoiceDate <= to)
                        .Select(x => new { x.ID, x.OrderID, x.InvoiceDate, Amount = (double?)x.TotalAmount ?? 0d })
                        .ToList();

                    var plans = db.PRODUCTPLANS
                        .Where(x => x.IsDeleted == false && x.StartDate >= from && x.StartDate <= to)
                        .Select(x => new { x.ProductID, x.StartDate, PlannedQty = (double?)x.Amount ?? 0d })
                        .ToList();

                    var productions = db.PRODUCT_PRODUCTION
                        .Where(x => x.IsDeleted != true && x.Date >= from && x.Date <= to)
                        .Select(x => new { x.ProductID, x.Date, ProducedQty = (double?)x.Total ?? 0d })
                        .ToList();

                    var tasks = db.APP_TASKS
                        .Where(t => t.IsDeleted == false)
                        .Select(t => new
                        {
                            t.ID,
                            t.DueDate,
                            StatusCode = t.APP_TASK_STATUS_REF != null ? t.APP_TASK_STATUS_REF.Code : null
                        })
                        .ToList();

                    var stockQuery = db.PRODUCT_STOCKS
                        .Where(x => rawProductIds.Contains(x.ProductID));
                    if (selectedWarehouseId.HasValue)
                    {
                        stockQuery = stockQuery.Where(x => x.WHID == selectedWarehouseId.Value);
                    }

                    var stockByProduct = stockQuery
                        .GroupBy(x => x.ProductID)
                        .Select(g => new
                        {
                            ProductID = g.Key,
                            StockQty = g.Sum(x => (double?)x.Amount) ?? 0d
                        })
                        .ToList();

                    var consumptionQuery = db.EXPENSE_PRODUCTS
                        .Where(x => x.EXPENS.IsDeleted != true &&
                                    x.EXPENS.IssueDateTime >= sales60From &&
                                    x.EXPENS.IssueDateTime <= to &&
                                    rawProductIds.Contains(x.ProductID));
                    if (selectedWarehouseId.HasValue)
                    {
                        consumptionQuery = consumptionQuery.Where(x => x.EXPENS.SenderWHID == selectedWarehouseId.Value);
                    }

                    var consumptionLines = consumptionQuery
                        .Select(x => new
                        {
                            x.ProductID,
                            Amount = (double?)x.Amount ?? 0d,
                            Date = x.EXPENS.IssueDateTime
                        })
                        .ToList();

                    var contractAmount = contracts.Sum(x => x.Amount);
                    var orderAmount = orders.Sum(x => x.Amount);
                    var invoiceAmount = invoices.Sum(x => x.Amount);
                    var plannedQty = plans.Sum(x => x.PlannedQty);
                    var producedQty = productions.Sum(x => x.ProducedQty);

                    var salesConversionRate = contractAmount > 0 ? (orderAmount / contractAmount) * 100d : 0d;
                    var orderFulfillmentRate = orderAmount > 0 ? (invoiceAmount / orderAmount) * 100d : 0d;
                    var productionExecutionRate = plannedQty > 0 ? (producedQty / plannedQty) * 100d : 0d;

                    var totalTasks = tasks.Count;
                    var completedTasks = tasks.Count(t => string.Equals(t.StatusCode, "DONE", StringComparison.OrdinalIgnoreCase));
                    var overdueTasks = tasks.Count(t =>
                        t.DueDate.HasValue &&
                        t.DueDate.Value.Date < to.Date &&
                        !string.Equals(t.StatusCode, "DONE", StringComparison.OrdinalIgnoreCase));
                    var taskCompletionRate = totalTasks > 0 ? (completedTasks * 100d / totalTasks) : 0d;

                    var avgDailyConsumptionQty = consumptionLines
                        .Where(x => x.Date >= sales30From)
                        .Sum(x => (double?)x.Amount) ?? 0d;
                    avgDailyConsumptionQty = avgDailyConsumptionQty / 30d;

                    var totalStockQty = stockByProduct.Sum(x => x.StockQty);
                    var inventoryCoverageDays = avgDailyConsumptionQty > 0 ? totalStockQty / avgDailyConsumptionQty : 0d;
                    var dohDays = inventoryCoverageDays;

                    var productNames = db.PRODUCTS
                        .Where(p => p.IsDeleted == false)
                        .Select(p => new { p.ID, Name = (p.Name ?? p.PNo) })
                        .ToDictionary(p => p.ID, p => p.Name);

                    var sold60ByProduct = consumptionLines
                        .GroupBy(x => x.ProductID)
                        .ToDictionary(
                            g => g.Key,
                            g => (g.Sum(x => (double?)x.Amount) ?? 0d) / 60d
                        );

                    var stockCoverage = stockByProduct
                        .Select(s =>
                        {
                            var avgDemand = sold60ByProduct.ContainsKey(s.ProductID) ? sold60ByProduct[s.ProductID] : 0d;
                            var days = avgDemand > 0 ? s.StockQty / avgDemand : 0d;
                            return new
                            {
                                productId = s.ProductID,
                                product = productNames.ContainsKey(s.ProductID) ? productNames[s.ProductID] : ("#" + s.ProductID),
                                stockQty = Math.Round(s.StockQty, 2),
                                avgDailyDemand = Math.Round(avgDemand, 2),
                                coverageDays = Math.Round(days, 1)
                            };
                        })
                        .OrderBy(x => x.coverageDays == 0 ? double.MaxValue : x.coverageDays)
                        .Take(12)
                        .ToList();

                    var trendMonths = Enumerable.Range(0, 12)
                        .Select(i => trendFrom.AddMonths(i))
                        .Select(d => d.ToString("yyyy-MM"))
                        .ToList();

                    var trend = trendMonths.Select(m =>
                    {
                        var contractM = contracts.Where(x => x.IssuedDate.ToString("yyyy-MM") == m).Sum(x => x.Amount);
                        var orderM = orders.Where(x => x.IssuedDate.ToString("yyyy-MM") == m).Sum(x => x.Amount);
                        var invoiceM = invoices.Where(x => x.InvoiceDate.ToString("yyyy-MM") == m).Sum(x => x.Amount);
                        var planM = plans.Where(x => x.StartDate.ToString("yyyy-MM") == m).Sum(x => x.PlannedQty);
                        var productionM = productions.Where(x => x.Date.ToString("yyyy-MM") == m).Sum(x => x.ProducedQty);
                        return new
                        {
                            period = m,
                            contractAmount = Math.Round(contractM, 2),
                            orderAmount = Math.Round(orderM, 2),
                            invoiceAmount = Math.Round(invoiceM, 2),
                            plannedQty = Math.Round(planM, 2),
                            producedQty = Math.Round(productionM, 2)
                        };
                    }).ToList();

                    var insights = new List<object>();
                    if (salesConversionRate < 65) insights.Add(new { level = "high", text = "Savdo konversiyasi past. Shartnoma -> buyurtma jarayonini kuchaytirish kerak." });
                    if (orderFulfillmentRate < 75) insights.Add(new { level = "high", text = "Buyurtma bajarilishi sekin. Invoicing va logistika bottleneck bor." });
                    if (productionExecutionRate < 80) insights.Add(new { level = "medium", text = "Ishlab chiqarish reja bajarilishi me'yordan past." });
                    if (inventoryCoverageDays < 20) insights.Add(new { level = "high", text = "DOH juda past. Stockout riski bor." });
                    if (inventoryCoverageDays > 90) insights.Add(new { level = "medium", text = "DOH yuqori. Zaxira aylanishi sust." });
                    if (overdueTasks > 0) insights.Add(new { level = "medium", text = "Muddati o'tgan vazifalar mavjud, ijro nazoratini kuchaytiring." });
                    if (!insights.Any()) insights.Add(new { level = "good", text = "Asosiy KPIlar nazorat ostida. Hozirgi trend barqaror." });

                    return Json(new
                    {
                        kpis = new
                        {
                            contractAmount = Math.Round(contractAmount, 2),
                            orderAmount = Math.Round(orderAmount, 2),
                            invoiceAmount = Math.Round(invoiceAmount, 2),
                            salesConversionRate = Math.Round(salesConversionRate, 1),
                            orderFulfillmentRate = Math.Round(orderFulfillmentRate, 1),
                            productionExecutionRate = Math.Round(productionExecutionRate, 1),
                            taskCompletionRate = Math.Round(taskCompletionRate, 1),
                            totalTasks,
                            completedTasks,
                            overdueTasks,
                            dohDays = Math.Round(dohDays, 1),
                            coverageDays = Math.Round(inventoryCoverageDays, 1),
                            plannedQty = Math.Round(plannedQty, 2),
                            producedQty = Math.Round(producedQty, 2)
                        },
                        trend,
                        stockCoverage,
                        insights,
                        warehouses,
                        selectedWarehouseId
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
