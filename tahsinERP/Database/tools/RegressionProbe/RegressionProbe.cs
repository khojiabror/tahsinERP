// Regressiya solishtiruv harness'i (float → decimal rejasi, DB_FLOAT_DECIMAL_PLAN.md 1-bosqich).
// Balans/Moliya, Tannarx, ombor qoldig'i, CRM/SRM va logistika natijalarini JSON'ga yozadi;
// "oldin" va "keyin" JSON'lari "compare" rejimida raqamli tolerans bilan solishtiriladi.
//
//   RegressionProbe.exe run <out.json> [user]
//   RegressionProbe.exe compare <before.json> <after.json> [tolerance=0.01]
//
// Baza: TAHSINERP_DB muhit o'zgaruvchisi (Run-RegressionProbe.ps1 o'rnatadi). Faqat o'qiydi — hech narsa yozmaydi.
// Kompilyatsiya va ishga tushirish: Run-RegressionProbe.ps1.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Finance;
using tahsinERP.Services.Stock;

class RegressionProbe
{
    static readonly HashSet<Type> EntityTypes = new HashSet<Type>(
        typeof(DBTHSNEntities).GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0]));

    static bool IsEntity(Type t)
    {
        if (t == null) return false;
        if (EntityTypes.Contains(t)) return true;
        return t.Namespace == "System.Data.Entity.DynamicProxies" && EntityTypes.Contains(t.BaseType);
    }

    static Type ElementType(Type t)
    {
        if (t == typeof(string)) return t;
        if (t.IsArray) return t.GetElementType();
        if (t.IsGenericType && typeof(IEnumerable).IsAssignableFrom(t)) return t.GetGenericArguments().Last();
        return t;
    }

    /// <summary>
    /// Entity'larning navigatsiya xossalari (lazy load, sikl) va binar ustunlar yozilmaydi;
    /// view model ichidagi entity ro'yxatlari esa skalyar ustunlari bilan yoziladi.
    /// </summary>
    sealed class ProbeResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization ms)
        {
            var p = base.CreateProperty(member, ms);
            var t = p.PropertyType;
            if (t == typeof(byte[])) p.ShouldSerialize = _ => false;
            else if (IsEntity(member.DeclaringType) && (IsEntity(ElementType(t)) || IsEntity(t))) p.ShouldSerialize = _ => false;
            else if (typeof(SelectList).IsAssignableFrom(t) || typeof(IEnumerable<SelectListItem>).IsAssignableFrom(t)) p.ShouldSerialize = _ => false;
            if (p.PropertyName == "SystemCode") p.ShouldSerialize = _ => false;
            return p;
        }
    }

    static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        ContractResolver = new ProbeResolver(),
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        Formatting = Formatting.Indented,
        Culture = CultureInfo.InvariantCulture,
        Error = (s, e) => { e.ErrorContext.Handled = true; }
    };

    static string _user = "khojiabror";
    static readonly Dictionary<string, object> Result = new Dictionary<string, object>();

    static void Put(string key, Func<object> f)
    {
        object v;
        try
        {
            v = f();
            if (v is Task) v = Unwrap((Task)v);
            // Har bo'limni darhol JToken'ga — keyingi kontekst yopilganda lazy proxy xatolari bo'lmasin
            v = JToken.Parse(JsonConvert.SerializeObject(v, Settings));
        }
        catch (Exception ex)
        {
            var e = ex; while (e.InnerException != null) e = e.InnerException;
            v = "ERROR: " + e.GetType().Name + ": " + e.Message;
        }
        Result[key] = v;
        Console.WriteLine("  " + key + (v is string && ((string)v).StartsWith("ERROR") ? "  <- " + v : ""));
    }

    static object Unwrap(Task t)
    {
        t.GetAwaiter().GetResult();
        var rp = t.GetType().GetProperty("Result");
        return rp == null ? null : rp.GetValue(t);
    }

    // ---------------- Controller chaqiruvi (soxta HttpContext, Developer foydalanuvchi) ----------------

    static object Action<T>(Func<T, object> call) where T : Controller, new()
    {
        var http = new HttpContext(new HttpRequest("", "http://localhost/", ""), new HttpResponse(TextWriter.Null));
        http.User = new GenericPrincipal(new GenericIdentity(_user, "Forms"), new string[0]);
        HttpContext.Current = http;
        var wrapper = new HttpContextWrapper(http);
        var rd = new RouteData();
        rd.Values["controller"] = typeof(T).Name.Replace("Controller", "");
        rd.Values["action"] = "Index";
        var c = new T();
        c.ControllerContext = new ControllerContext(new RequestContext(wrapper, rd), c);
        c.Url = new UrlHelper(c.ControllerContext.RequestContext, RouteTable.Routes);
        try
        {
            var r = call(c);
            if (r is Task) r = Unwrap((Task)r);
            return Describe(r);
        }
        finally { c.Dispose(); }
    }

    static object Describe(object r)
    {
        var view = r as ViewResultBase;
        if (view != null)
        {
            var vd = new SortedDictionary<string, object>();
            foreach (var kv in view.ViewData)
            {
                if (kv.Value is SelectList || kv.Value is IEnumerable<SelectListItem>) continue;
                vd[kv.Key] = kv.Value;
            }
            return new { Model = view.ViewData.Model, ViewData = vd };
        }
        var json = r as JsonResult;
        if (json != null) return new { Json = json.Data };
        var redirect = r as RedirectToRouteResult;
        if (redirect != null) return "REDIRECT " + string.Join(",", redirect.RouteValues.Select(x => x.Key + "=" + x.Value));
        var status = r as HttpStatusCodeResult;
        if (status != null) return "HTTP " + status.StatusCode + " " + status.StatusDescription;
        return r == null ? null : r.GetType().Name;
    }

    // ---------------- Bo'limlar ----------------

    static readonly DateTime Today = new DateTime(2026, 10, 18);
    static readonly DateTime[] BalanceDates = { new DateTime(2025, 12, 31), new DateTime(2026, 6, 30), Today };
    static readonly int[][] Months = { new[] { 2025, 12 }, new[] { 2026, 3 }, new[] { 2026, 5 } };

    static void Balance()
    {
        foreach (var d in BalanceDates)
        {
            var key = d.ToString("yyyy-MM-dd");
            using (var db = new DBTHSNEntities())
            {
                var svc = new BalanceSheetService(db);
                Put("balance/" + key, () => svc.Compute(d).Select(x => new
                {
                    x.Category.ID, x.Category.Name, x.Amount, x.IsAssetColumn, x.TransactionCount, x.AddedAmount, x.SubtractedAmount, x.CalculationTypeName, x.Explanation
                }).ToList());
                Put("balance/liabilityDetails " + key, () => svc.LiabilityDetails(d).Select(x => new { x.Category.ID, x.Amount }).ToList());
                Put("balance/assetDetails " + key, () => svc.AssetDetails(d).Select(x => new { x.Category.ID, x.Amount }).ToList());
                foreach (var id in db.FIN_BALANCE_CATEGORIES.Where(c => c.IsActive).Select(c => c.ID).ToList())
                    Put("balance/movements " + key + " cat" + id, () => svc.Movements(id, d));
            }
            using (var db = new DBTHSNEntities())
                Put("finance/reportsHub " + key, () => new LiabilityReportsService(db).ReportsHub(d));
        }
        using (var db = new DBTHSNEntities())
        {
            Put("finance/liabilities 2026", () => new LiabilityReportsService(db).Liabilities(new DateTime(2026, 1, 1), Today));
            Put("finance/overview 2026", () => FinAnalyticsService.FinancialOverview(db, new DateTime(2026, 1, 1), Today));
            Put("finance/pl 2026-09", () => FinAnalyticsService.ProfitAndLoss(db, 2026, 9));
            Put("finance/accountBalances", () => FinAnalyticsService.AccountBalances(db, new DateTime(2025, 1, 1), Today, null));
        }
    }

    static void Tannarx()
    {
        var engineType = typeof(TannarxService).Assembly.GetType("tahsinERP.Services.Tannarx.TannarxActualCostEngine", true);
        foreach (var ym in Months)
        {
            int y = ym[0], m = ym[1];
            var key = y + "-" + m.ToString("00");
            using (var db = new DBTHSNEntities())
            {
                var svc = new TannarxAnalyticsService(db);
                Put("tannarx/monthSources " + key, () => svc.GetMonthSourcesAsync(y, m));
                Put("tannarx/dashboard " + key, () => svc.GetDashboardDataAsync(y, m));
                Put("tannarx/report " + key, () => svc.GetAnalyticsReportAsync(y, m));
            }
            using (var db = new DBTHSNEntities())
            {
                // Fakt tannarx dvigateli (faqat hisoblaydi, yozmaydi) — tashuv xarajati F_WAYBILLS'dan
                Put("tannarx/actualEngine " + key, () =>
                {
                    var rows = db.TANNARXes.Where(t => !t.IsDeleted && t.Year == y && t.Month == m).ToList();
                    var engine = Activator.CreateInstance(engineType, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new object[] { db }, null);
                    var method = engineType.GetMethod("CalculateMonthAsync", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    return Unwrap((Task)method.Invoke(engine, new object[] { y, m, rows }));
                });
            }
            Put("tannarx/controllerIndex " + key, () => Action<tahsinERP.Controllers.Engineering.TannarxController>(c => c.Index(y, m, null)));
        }
    }

    static void Stock()
    {
        List<int> warehouses, products;
        using (var db = new DBTHSNEntities())
        {
            warehouses = db.WAREHOUSES.Where(w => w.IsDeleted != true).Select(w => w.ID).OrderBy(x => x).ToList();
            products = db.PRODUCT_STOCKS.Select(s => s.ProductID).Distinct().OrderBy(x => x).ToList();
        }
        foreach (var wh in warehouses)
        {
            using (var db = new DBTHSNEntities())
                Put("stock/onHand wh" + wh, () => StockLedger.GetOnHand(db, wh, products).Where(kv => kv.Value != 0).OrderBy(kv => kv.Key).ToList());
            foreach (var ym in new[] { new[] { 2026, 5 }, new[] { 2026, 9 } })
                foreach (var mode in new[] { "quantity", "amount" })
                    using (var db = new DBTHSNEntities())
                        Put("stock/monthly wh" + wh + " " + ym[0] + "-" + ym[1] + " " + mode,
                            () => WarehouseMonthlyReportService.BuildReportAsync(db, wh, ym[0], ym[1], null, mode));
        }
    }

    static void CrmSrm()
    {
        var from = new DateTime(2025, 1, 1);
        Put("crm/dashboard default", () => Action<tahsinERP.Controllers.CRMDashboardController>(c => c.Index(null, null)));
        Put("crm/dashboard 2025-", () => Action<tahsinERP.Controllers.CRMDashboardController>(c => c.Index(from, Today)));
        Put("srm/dashboard default", () => Action<tahsinERP.Controllers.SRMDashboardController>(c => c.Index(null, null)));
        Put("srm/dashboard 2025-", () => Action<tahsinERP.Controllers.SRMDashboardController>(c => c.Index(from, Today)));
        foreach (var d in BalanceDates)
            using (var db = new DBTHSNEntities())
            {
                var svc = new CounterpartyBalanceService(db);
                Put("counterparty/suppliers " + d.ToString("yyyy-MM-dd"), () => svc.Suppliers(d));
                Put("counterparty/customers " + d.ToString("yyyy-MM-dd"), () => svc.Customers(d));
            }
    }

    static void Logistics()
    {
        var from = new DateTime(2024, 1, 1);
        Put("logistics/dashboard default", () => Action<tahsinERP.Controllers.LogisticsDashboardController>(c => c.Index(null, null, null, null)));
        Put("logistics/dashboard all", () => Action<tahsinERP.Controllers.LogisticsDashboardController>(c => c.Index(from, Today, null, null)));
        Put("logistics/dashboard in", () => Action<tahsinERP.Controllers.LogisticsDashboardController>(c => c.Index(from, Today, null, (byte)2)));
        Put("logistics/lanes", () => Action<tahsinERP.Controllers.LogisticsDashboardController>(c => c.LanePerformance(from, Today)));
        Put("logistics/reconciliation", () => Action<tahsinERP.Controllers.LogisticsDashboardController>(c => c.Reconciliation(from, Today)));
        Put("logistics/forwarderComparison", () => Action<tahsinERP.Controllers.LogisticsDashboardController>(c => c.ForwarderComparison(from, Today, null, null)));
        foreach (var ym in new[] { new[] { 2025, 12 }, new[] { 2026, 3 }, new[] { 2026, 5 } })
            Put("logistics/monthly " + ym[0] + "-" + ym[1], () => Action<tahsinERP.Controllers.LogisticsDashboardController>(c => c.MonthlyReport(ym[0], ym[1], null)));

        Put("logistics/waybills", () => Action<tahsinERP.Controllers.WaybillController>(c => c.Index(null, null, null, null, null, null, null, "date_desc", 1, 1000)));
        Put("logistics/payments", () => Action<tahsinERP.Controllers.Logistic.WaybillPaymentController>(c => c.Index(1, 1000)));
        Put("logistics/contracts", () => Action<tahsinERP.Controllers.FContractController>(c => c.Index(null)));
        Put("logistics/tariffs", () => Action<tahsinERP.Controllers.FContractTariffController>(c => c.Index(null)));
        Put("logistics/forwarders", () => Action<tahsinERP.Controllers.ForwarderController>(c => c.Index()));

        List<int> forwarders, waybills, payments, contracts;
        using (var db = new DBTHSNEntities())
        {
            forwarders = db.FORWARDERS.Select(f => f.ID).OrderBy(x => x).ToList();
            waybills = db.F_WAYBILLS.Select(w => w.ID).OrderBy(x => x).ToList();
            payments = db.F_WAYBILL_PAYMENTS.Select(w => w.ID).OrderBy(x => x).ToList();
            contracts = db.F_CONTRACTS.Select(w => w.ID).OrderBy(x => x).ToList();
        }
        foreach (var id in forwarders)
            Put("logistics/forwarder " + id, () => Action<tahsinERP.Controllers.ForwarderController>(c => c.Details(id)));
        foreach (var id in contracts)
        {
            Put("logistics/contract " + id, () => Action<tahsinERP.Controllers.FContractController>(c => c.Details(id)));
            Put("logistics/tariffsByContract " + id, () => Action<tahsinERP.Controllers.WaybillLookupController>(c => c.GetTariffsByContract(id)));
        }
        foreach (var id in waybills.Take(40))
            Put("logistics/waybill " + id, () => Action<tahsinERP.Controllers.WaybillController>(c => c.Details(id)));
        foreach (var id in payments.Take(40))
            Put("logistics/payment " + id, () => Action<tahsinERP.Controllers.Logistic.WaybillPaymentController>(c => c.Details(id)));

        using (var db = new DBTHSNEntities())
        {
            var money = new LogisticsMoney(db);
            Put("logistics/forwarderBalances all", () => WaybillLedgerService.GetForwarderBalances(db, null, null, null, money).OrderBy(kv => kv.Key).ToList());
            Put("logistics/forwarderBalances 2026", () => WaybillLedgerService.GetForwarderBalances(db, new DateTime(2026, 1, 1), Today, null, money).OrderBy(kv => kv.Key).ToList());
            Put("logistics/paidLookup", () => WaybillLedgerService.GetPaidLookup(db, WaybillLedgerService.ActiveWaybills(db).Select(w => w.ID), null, money).OrderBy(kv => kv.Key).ToList());
            Put("logistics/totals", () => LogisticsAnalyticsService.GetTotals(WaybillLedgerService.ActiveWaybills(db), money));
        }
    }

    // ---------------- Solishtirish ----------------

    static int Compare(string beforeFile, string afterFile, decimal tol)
    {
        var a = JObject.Parse(File.ReadAllText(beforeFile));
        var b = JObject.Parse(File.ReadAllText(afterFile));
        var rounding = new List<string>();
        var failures = new List<string>();
        foreach (var key in a.Properties().Select(p => p.Name).Union(b.Properties().Select(p => p.Name)))
            Walk(a[key], b[key], key, tol, rounding, failures);
        Console.WriteLine("bo'limlar: {0} / {1}; yaxlitlash farqlari (<= {2}): {3}; FARQLAR: {4}",
            a.Count, b.Count, tol, rounding.Count, failures.Count);
        foreach (var r in rounding.Take(200)) Console.WriteLine("  ~ " + r);
        foreach (var f in failures.Take(500)) Console.WriteLine("  ! " + f);
        return failures.Count == 0 ? 0 : 1;
    }

    static bool IsNumber(JToken t) => t != null && (t.Type == JTokenType.Integer || t.Type == JTokenType.Float);

    static void Walk(JToken x, JToken y, string path, decimal tol, List<string> rounding, List<string> failures)
    {
        if (x == null || y == null || x.Type == JTokenType.Null || y.Type == JTokenType.Null)
        {
            bool xn = x == null || x.Type == JTokenType.Null, yn = y == null || y.Type == JTokenType.Null;
            if (xn != yn) failures.Add(path + ": " + Short(x) + " -> " + Short(y));
            return;
        }
        if (IsNumber(x) && IsNumber(y))
        {
            decimal dx, dy;
            try { dx = x.Value<decimal>(); dy = y.Value<decimal>(); }
            catch (OverflowException) { if (x.Value<double>() != y.Value<double>()) failures.Add(path + ": " + x + " -> " + y); return; }
            var diff = Math.Abs(dx - dy);
            if (diff == 0) return;
            (diff <= tol ? rounding : failures).Add(path + ": " + dx + " -> " + dy + " (farq " + diff + ")");
            return;
        }
        if (x.Type != y.Type) { failures.Add(path + ": tur " + x.Type + " -> " + y.Type + " (" + Short(x) + " -> " + Short(y) + ")"); return; }
        if (x is JObject)
        {
            var ox = (JObject)x; var oy = (JObject)y;
            foreach (var k in ox.Properties().Select(p => p.Name).Union(oy.Properties().Select(p => p.Name)))
                Walk(ox[k], oy[k], path + "/" + k, tol, rounding, failures);
            return;
        }
        if (x is JArray)
        {
            var ax = (JArray)x; var ay = (JArray)y;
            if (ax.Count != ay.Count) failures.Add(path + ": uzunlik " + ax.Count + " -> " + ay.Count);
            for (int i = 0; i < Math.Min(ax.Count, ay.Count); i++) Walk(ax[i], ay[i], path + "[" + i + "]", tol, rounding, failures);
            return;
        }
        if (!JToken.DeepEquals(x, y)) failures.Add(path + ": " + Short(x) + " -> " + Short(y));
    }

    static string Short(JToken t)
    {
        var s = t == null ? "null" : t.ToString(Formatting.None);
        return s.Length > 160 ? s.Substring(0, 160) + "…" : s;
    }

    static int Main(string[] args)
    {
        if (args.Length >= 3 && args[0] == "compare")
            return Compare(args[1], args[2], args.Length > 3 ? decimal.Parse(args[3], CultureInfo.InvariantCulture) : 0.01m);
        if (args.Length < 2 || args[0] != "run")
        {
            Console.Error.WriteLine("RegressionProbe.exe run <out.json> [user] | compare <before.json> <after.json> [tolerance]");
            return 2;
        }
        if (args.Length > 2) _user = args[2];
        System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        Balance();
        Tannarx();
        Stock();
        CrmSrm();
        Logistics();
        var errors = Result.Count(kv => kv.Value is string && ((string)kv.Value).StartsWith("ERROR"));
        File.WriteAllText(args[1], JsonConvert.SerializeObject(new SortedDictionary<string, object>(Result), Settings));
        Console.WriteLine("yozildi: {0} ({1} bo'lim, {2} xato)", args[1], Result.Count, errors);
        return 0;
    }
}
