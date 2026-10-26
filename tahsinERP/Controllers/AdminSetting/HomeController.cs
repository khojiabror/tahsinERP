using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.I18n;
using tahsinERP.Models;
using tahsinERP.ViewModels.Home;

namespace tahsinERP.Controllers
{
    public class HomeController : Controller
    {
        // Asosiy valyuta (dashboard summalari yonida ko'rsatiladi) — FIN_TRANSACTIONS bo'yicha GROUP BY
        // har bosh sahifa ochilishida emas, 30 daqiqada bir marta hisoblanadi.
        private const string PrimaryCurrencyCacheKey = "Home_PrimaryCurrencyShortname";

        public ActionResult Index()
        {
            var shortname = System.Web.HttpRuntime.Cache.Get(tahsinERP.Configuration.Tenancy.TenantContext.CacheKey(PrimaryCurrencyCacheKey)) as string;
            if (shortname == null)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    shortname = db.FIN_TRANSACTIONS
                        .Where(t => t.IsDeleted == false || t.IsDeleted == null)
                        .Where(t => t.CURRENCy != null)
                        .GroupBy(t => t.CurrencyID)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.FirstOrDefault().CURRENCy.Shortname)
                        .FirstOrDefault()
                        ?? db.CURRENCIES
                            .Where(c => c.IsDeleted != true)
                            .OrderBy(c => c.ID)
                            .Select(c => c.Shortname)
                            .FirstOrDefault()
                        ?? "";
                }
                System.Web.HttpRuntime.Cache.Insert(tahsinERP.Configuration.Tenancy.TenantContext.CacheKey(PrimaryCurrencyCacheKey), shortname, null,
                    DateTime.Now.AddMinutes(30), System.Web.Caching.Cache.NoSlidingExpiration);
            }
            ViewBag.CurrencyShortname = shortname;
            var userName = User?.Identity?.Name;   // testda HttpContext yo'q
            var sections = HomeSections.For(userName);
            ViewBag.Sections = sections;
            ViewBag.ModuleTiles = HomeSections.Tiles(userName);
            return View();
        }

        // Executive dashboard - cross-module leadership insights
        [HttpGet]
        public JsonResult GetExecutiveDashboard(DateTime? fromDate, DateTime? toDate, int? warehouseId)
        {
            // Faqat paketda bor va foydalanuvchi ko'ra oladigan modullar hisoblanadi (sinovda tanlangan modullar — kalitdagi Edition)
            var sec = HomeSections.For(User.Identity.Name);
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
                        .Where(w => sec.Stock && w.IsDeleted != true)
                        .OrderBy(w => w.WHName)
                        .Select(w => new { id = w.ID, name = w.WHName })
                        .ToList();

                    var rawProductIds = db.PRODUCTS
                        .Where(p => sec.Stock && p.IsDeleted == false &&
                                   (p.IsRawMaterial == true ||
                                   (p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.IsRawMaterial)))
                        .Select(p => p.ID)
                        .ToList();

                    // Davr oxiri inklyuziv: "to" kunining oxirigacha (avval oxirgi kun deyarli tushib qolardi)
                    var toExcl = to.Date.AddDays(1);

                    var contractsQ = db.S_CONTRACTS.Where(x => x.IsDeleted != true);
                    var ordersQ = db.S_ORDERS.Where(x => x.IsDeleted != true);
                    var invoicesQ = db.SALES_INVOICES.Where(x => x.StatusCode == null ||
                        (x.StatusCode != "CANCELED" && x.StatusCode != "CANCELLED"));
                    var plansQ = db.PRODUCTPLANS.Where(x => x.IsDeleted == false);
                    var productionsQ = db.PRODUCT_PRODUCTION.Where(x => x.IsDeleted != true);

                    // KPI: davr bo'yicha yig'indilar SQL'da
                    if (!sec.Sales) { contractsQ = contractsQ.Where(x => false); ordersQ = ordersQ.Where(x => false); invoicesQ = invoicesQ.Where(x => false); }
                    if (!sec.Production) { plansQ = plansQ.Where(x => false); productionsQ = productionsQ.Where(x => false); }
                    var contractAmount = contractsQ.Where(x => x.IssuedDate >= from && x.IssuedDate < toExcl).Sum(x => (double?)x.Amount) ?? 0d;
                    var orderAmount = ordersQ.Where(x => x.IssuedDate >= from && x.IssuedDate < toExcl).Sum(x => (double?)x.Amount) ?? 0d;
                    var invoiceAmount = invoicesQ.Where(x => x.InvoiceDate >= from && x.InvoiceDate < toExcl).Sum(x => (double?)x.TotalAmount) ?? 0d;
                    var plannedQty = plansQ.Where(x => x.StartDate >= from && x.StartDate < toExcl).Sum(x => (double?)x.Amount) ?? 0d;
                    var producedQty = productionsQ.Where(x => x.Date >= from && x.Date < toExcl).Sum(x => (double?)x.Total) ?? 0d;

                    // Topshiriqlar: barcha qatorlarni yuklash o'rniga SQL COUNT
                    var tasksQ = db.APP_TASKS.Where(t => sec.Tasks && t.IsDeleted == false);
                    var totalTasks = tasksQ.Count();
                    var completedTasks = tasksQ.Count(t => t.APP_TASK_STATUS_REF != null && t.APP_TASK_STATUS_REF.Code == "DONE");
                    var overdueTasks = tasksQ.Count(t => t.DueDate.HasValue && t.DueDate.Value < to.Date &&
                        (t.APP_TASK_STATUS_REF == null || t.APP_TASK_STATUS_REF.Code != "DONE"));

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
                                    x.EXPENS.IssueDateTime < toExcl &&
                                    rawProductIds.Contains(x.ProductID));
                    if (selectedWarehouseId.HasValue)
                    {
                        consumptionQuery = consumptionQuery.Where(x => x.EXPENS.SenderWHID == selectedWarehouseId.Value);
                    }

                    // Mahsulot bo'yicha 60 va 30 kunlik sarf SQL'da guruhlanadi
                    var consumptionByProduct = consumptionQuery
                        .GroupBy(x => x.ProductID)
                        .Select(g => new
                        {
                            ProductID = g.Key,
                            Qty60 = g.Sum(x => (double?)x.Amount) ?? 0d,
                            Qty30 = g.Where(x => x.EXPENS.IssueDateTime >= sales30From).Sum(x => (double?)x.Amount) ?? 0d
                        })
                        .ToList();

                    var salesConversionRate = contractAmount > 0 ? (orderAmount / contractAmount) * 100d : 0d;
                    var orderFulfillmentRate = orderAmount > 0 ? (invoiceAmount / orderAmount) * 100d : 0d;
                    var productionExecutionRate = plannedQty > 0 ? (producedQty / plannedQty) * 100d : 0d;
                    var taskCompletionRate = totalTasks > 0 ? (completedTasks * 100d / totalTasks) : 0d;

                    var avgDailyConsumptionQty = consumptionByProduct.Sum(x => x.Qty30) / 30d;

                    var totalStockQty = stockByProduct.Sum(x => x.StockQty);
                    var inventoryCoverageDays = avgDailyConsumptionQty > 0 ? totalStockQty / avgDailyConsumptionQty : 0d;
                    var dohDays = inventoryCoverageDays;

                    var sold60ByProduct = consumptionByProduct.ToDictionary(x => x.ProductID, x => x.Qty60 / 60d);

                    var coverageRows = stockByProduct
                        .Select(s =>
                        {
                            double avgDemand;
                            sold60ByProduct.TryGetValue(s.ProductID, out avgDemand);
                            var days = avgDemand > 0 ? s.StockQty / avgDemand : 0d;
                            return new { s.ProductID, s.StockQty, AvgDemand = avgDemand, Days = days };
                        })
                        .OrderBy(x => x.Days == 0 ? double.MaxValue : x.Days)
                        .Take(12)
                        .ToList();

                    // Nomlar faqat ko'rsatiladigan 12 ta mahsulot uchun (avval butun PRODUCTS lug'ati yuklanardi)
                    var coverageIds = coverageRows.Select(x => x.ProductID).ToList();
                    var productNames = db.PRODUCTS
                        .Where(p => coverageIds.Contains(p.ID))
                        .Select(p => new { p.ID, Name = (p.Name ?? p.PNo) })
                        .ToDictionary(p => p.ID, p => p.Name);

                    var stockCoverage = coverageRows
                        .Select(x => new
                        {
                            productId = x.ProductID,
                            product = productNames.ContainsKey(x.ProductID) ? productNames[x.ProductID] : ("#" + x.ProductID),
                            stockQty = Math.Round(x.StockQty, 2),
                            avgDailyDemand = Math.Round(x.AvgDemand, 2),
                            coverageDays = Math.Round(x.Days, 1)
                        })
                        .ToList();

                    // 12 oylik trend o'z davri (trendFrom..to) bo'yicha, oy kesimida SQL'da.
                    // Avval trend faqat tanlangan davr (standart 1 oy) ma'lumotidan qurilib, 11 oy doim 0 edi.
                    var contractTrend = contractsQ.Where(x => x.IssuedDate >= trendFrom && x.IssuedDate < toExcl)
                        .GroupBy(x => new { x.IssuedDate.Year, x.IssuedDate.Month })
                        .Select(g => new { g.Key.Year, g.Key.Month, V = g.Sum(x => (double?)x.Amount) ?? 0d }).ToList();
                    var orderTrend = ordersQ.Where(x => x.IssuedDate >= trendFrom && x.IssuedDate < toExcl)
                        .GroupBy(x => new { x.IssuedDate.Year, x.IssuedDate.Month })
                        .Select(g => new { g.Key.Year, g.Key.Month, V = g.Sum(x => (double?)x.Amount) ?? 0d }).ToList();
                    var invoiceTrend = invoicesQ.Where(x => x.InvoiceDate >= trendFrom && x.InvoiceDate < toExcl)
                        .GroupBy(x => new { x.InvoiceDate.Year, x.InvoiceDate.Month })
                        .Select(g => new { g.Key.Year, g.Key.Month, V = g.Sum(x => (double?)x.TotalAmount) ?? 0d }).ToList();
                    var planTrend = plansQ.Where(x => x.StartDate >= trendFrom && x.StartDate < toExcl)
                        .GroupBy(x => new { x.StartDate.Year, x.StartDate.Month })
                        .Select(g => new { g.Key.Year, g.Key.Month, V = g.Sum(x => (double?)x.Amount) ?? 0d }).ToList();
                    var productionTrend = productionsQ.Where(x => x.Date >= trendFrom && x.Date < toExcl)
                        .GroupBy(x => new { x.Date.Year, x.Date.Month })
                        .Select(g => new { g.Key.Year, g.Key.Month, V = g.Sum(x => (double?)x.Total) ?? 0d }).ToList();

                    var trend = Enumerable.Range(0, 12)
                        .Select(i => trendFrom.AddMonths(i))
                        .Select(d => new
                        {
                            period = d.ToString("yyyy-MM"),
                            contractAmount = Math.Round(contractTrend.Where(x => x.Year == d.Year && x.Month == d.Month).Sum(x => x.V), 2),
                            orderAmount = Math.Round(orderTrend.Where(x => x.Year == d.Year && x.Month == d.Month).Sum(x => x.V), 2),
                            invoiceAmount = Math.Round(invoiceTrend.Where(x => x.Year == d.Year && x.Month == d.Month).Sum(x => x.V), 2),
                            plannedQty = Math.Round(planTrend.Where(x => x.Year == d.Year && x.Month == d.Month).Sum(x => x.V), 2),
                            producedQty = Math.Round(productionTrend.Where(x => x.Year == d.Year && x.Month == d.Month).Sum(x => x.V), 2)
                        })
                        .ToList();

                    // signallar faqat ko'rsatilayotgan bo'limlar bo'yicha (tanlanmagan modulning 0% i "xavf" emas)
                    var insights = new List<object>();
                    if (sec.Sales && salesConversionRate < 65) insights.Add(new { level = "high", text = L.T("Core_InsightLowConversion") });
                    if (sec.Sales && orderFulfillmentRate < 75) insights.Add(new { level = "high", text = L.T("Core_InsightSlowFulfillment") });
                    if (sec.Production && productionExecutionRate < 80) insights.Add(new { level = "medium", text = L.T("Core_InsightLowProduction") });
                    if (sec.Stock && inventoryCoverageDays < 20) insights.Add(new { level = "high", text = L.T("Core_InsightDohLow") });
                    if (sec.Stock && inventoryCoverageDays > 90) insights.Add(new { level = "medium", text = L.T("Core_InsightDohHigh") });
                    if (sec.Tasks && overdueTasks > 0) insights.Add(new { level = "medium", text = L.T("Core_InsightOverdueTasks") });
                    if (!insights.Any()) insights.Add(new { level = "good", text = L.T("Core_InsightAllGood") });

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
                        selectedWarehouseId,
                        sections = new { sales = sec.Sales, production = sec.Production, tasks = sec.Tasks, stock = sec.Stock }
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                // Ichki xabar foydalanuvchiga ko'rsatilmaydi (CORE-S06)
                System.Diagnostics.Trace.TraceError("[Home.GetExecutiveDashboard] " + ex);
                return Json(new { error = L.T("Core_DashboardLoadError") }, JsonRequestBehavior.AllowGet);
            }
        }
    }

    /// <summary>
    /// Bosh sahifa bo'limlari: faqat litsenziya paketida bor (sinovda — tanlangan modullar, "TRIAL:Srm,Crm") va foydalanuvchi
    /// roli ko'ra oladigan modullar. RoleHelper.IsViewPermitted ichida EditionGate tekshiruvi bor — menyu bilan bir xil qoida.
    /// </summary>
    public sealed class HomeSections
    {
        public bool Sales { get; private set; }
        public bool Production { get; private set; }
        public bool Tasks { get; private set; }
        /// <summary>Xomashyo zaxirasi (DOH, qoplash kunlari): ombor hisobotlari yoki ishlab chiqarish.</summary>
        public bool Stock { get; private set; }
        public bool Any { get { return Sales || Production || Tasks || Stock; } }

        public static HomeSections For(string user)
        {
            var s = new HomeSections
            {
                Sales = RoleHelper.IsViewPermitted(user, "CRMDashboard") || RoleHelper.IsViewPermitted(user, "SOrder") || RoleHelper.IsViewPermitted(user, "SalesInvoices"),
                Production = RoleHelper.IsViewPermitted(user, "MESDashboard"),
                Tasks = RoleHelper.IsViewPermitted(user, "TaskDashboard"),
            };
            s.Stock = s.Production || RoleHelper.IsViewPermitted(user, "InventoryReports");
            return s;
        }

        /// <summary>Modul dashboard'lariga tezkor kirish — ochiq va ruxsat etilgan modullar, paket tartibida.</summary>
        public static List<HomeModuleTile> Tiles(string user)
        {
            var all = new[]
            {
                new HomeModuleTile("Supplier", "/SRMDashboard", "bx-cart", "Menu_DashSrm"),
                new HomeModuleTile("SOrder", "/CRMDashboard", "bx-line-chart", "Menu_DashCrm"),
                new HomeModuleTile("TaskDashboard", "/TaskDashboard", "bx-task", "Menu_DashTasks"),
                new HomeModuleTile("MESDashboard", "/MESDashboard/Dashboard", "bx-cog", "Menu_DashMes"),
                new HomeModuleTile("Tannarx", "/TannarxAnalytics/Dashboard", "bx-calculator", "Menu_DashCost"),
                new HomeModuleTile("FinDashboard", "/FinDashboard/FinancialOverview", "bx-wallet", "Core_HomeTileFinance"),
                new HomeModuleTile("HrDashboard", "/HrDashboard", "bx-group", "Menu_DashHr"),
                new HomeModuleTile("Forwarder", "/LogisticsDashboard", "bx-car", "Menu_DashLogistics"),
                new HomeModuleTile("OfficialLetters", "/OfficialLetters", "bx-envelope", "Core_HomeTileDocs"),
                new HomeModuleTile("DstDashboard", "/Distribution/DstDashboard", "bx-store", "Core_HomeTileDistribution"),
                new HomeModuleTile("SiteHome", "/Site/SiteHome", "bx-globe", "Core_HomeTileSite"),
            };
            return all.Where(t => RoleHelper.IsViewPermitted(user, t.Controller)).ToList();
        }
    }

    public sealed class HomeModuleTile
    {
        /// <param name="controller">ko'rish huquqi va paket tekshiriladigan PERMISSIONMODULES.Module (menyudagi kabi)</param>
        public HomeModuleTile(string controller, string url, string icon, string titleKey)
        {
            Controller = controller; Url = url; Icon = icon; TitleKey = titleKey;
        }
        public string Controller { get; private set; }
        public string Url { get; private set; }
        public string Icon { get; private set; }
        public string TitleKey { get; private set; }
    }
}
