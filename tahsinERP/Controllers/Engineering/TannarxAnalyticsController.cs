using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels.Tannarx;
using tahsinERP.Helpers;

namespace tahsinERP.Controllers.Engineering
{
    [Authorize]
    public class TannarxAnalyticsController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private TannarxAnalyticsService _analyticsService;

        public TannarxAnalyticsController()
        {
            _analyticsService = new TannarxAnalyticsService(db);
        }

        // Olish so'rovi: TannarxAnalytics/Dashboard
        public async Task<ActionResult> Dashboard(int? year, int? month, int? productID)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Tannarx"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // standart ga current year va month agar not provided
            var currentYear = year ?? DateTime.Now.Year;
            var currentMonth = month ?? DateTime.Now.Month;

            ViewBag.Year = currentYear;
            ViewBag.Month = currentMonth;
            ViewBag.ProductID = productID;

            // Olish years uchun dropdown
            var years = Enumerable.Range(DateTime.Now.Year - 2, 5).ToList();
            ViewBag.Years = new SelectList(years, currentYear);

            ViewBag.Months = UzMonthNames.SelectList(currentMonth);

            // Tayyor mahsulotlar (proyeksiya, "PNo - Name")
            var products = await MesLookups.FinishedGoodItemsAsync(db);

            // Qo'shish "Barchasi" option ga the product list
            var productList = products.Select(p => new { p.ID, p.Name }).ToList();
            ViewBag.Products = new SelectList(productList, "ID", "Name", productID);

            // Olish dashboard ma'lumot
            var dashboard = await _analyticsService.GetDashboardDataAsync(currentYear, currentMonth, productID);

            return View(dashboard);
        }

        // Olish so'rovi: TannarxAnalytics/Report
        public async Task<ActionResult> Report(int? year, int? month, int? productID)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Tannarx"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.ProductID = productID;

            // Olish years uchun dropdown
            var years = Enumerable.Range(DateTime.Now.Year - 2, 5).ToList();
            ViewBag.Years = new SelectList(years, year);

            ViewBag.Months = UzMonthNames.SelectList(month);

            // Tayyor mahsulotlar (proyeksiya, "PNo - Name")
            var products = await MesLookups.FinishedGoodItemsAsync(db);

            // Qo'shish "Barchasi" option ga the product list
            var productList = products.Select(p => new { p.ID, p.Name }).ToList();
            ViewBag.Products = new SelectList(productList, "ID", "Name", productID);

            // Olish report ma'lumot
            var report = await _analyticsService.GetAnalyticsReportAsync(year, month, productID);

            return View(report);
        }

        // POST so'rovi: TannarxAnalytics/UpdateActualCosts
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UpdateActualCosts(int? year, int? month, int? productID, string returnTo)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "Tannarx", "Edit"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (!year.HasValue || !month.HasValue)
            {
                TempData["Error"] = "Fakt xarajatlarni yangilash uchun yil va oy tanlanishi shart.";
                if (string.Equals(returnTo, "Report", System.StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction("Report", new { year = year, month = month, productID = productID });
                return RedirectToAction("Dashboard", new { year = year, month = month, productID = productID });
            }

            try
            {
                var recordsUpdated = await _analyticsService.UpdateActualCostsAsync(year, month);

                LogHelper.LogToDatabase(User.Identity.Name, "TannarxAnalytics", 
                    $"Actual costs yangilandi - Year: {year}, Month: {month}, Records: {recordsUpdated}");
                
                TempData["Success"] = $"{recordsUpdated} ta tannarx yozuvi uchun fakt xarajatlar yangilandi.";
                if (_analyticsService.LastUpdateWarnings.Any())
                    TempData["Warning"] = string.Join("\n", _analyticsService.LastUpdateWarnings);
            }
            catch (Exception ex)
            {
                LogHelper.LogToDatabase(User.Identity.Name, "TannarxAnalytics", 
                    $"Actual costs yangilashda xatolik: {ex.Message}");
                TempData["Error"] = $"Fakt xarajatlarni yangilashda xatolik: {ex.Message}";
            }

            if (string.Equals(returnTo, "Report", System.StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Report", new { year = year, month = month, productID = productID });

            return RedirectToAction("Dashboard", new { year = year, month = month, productID = productID });
        }

        // Olish so'rovi: TannarxAnalytics/MonthSources — oy xarajatlari qayerdan tortildi va qanday taqsimlandi
        public async Task<ActionResult> MonthSources(int? year, int? month)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Tannarx"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var y = year ?? DateTime.Now.Year;
            var m = month ?? DateTime.Now.Month;
            ViewBag.Years = new SelectList(Enumerable.Range(DateTime.Now.Year - 2, 5).ToList(), y);
            ViewBag.Months = UzMonthNames.SelectList(m);

            try
            {
                var model = await _analyticsService.GetMonthSourcesAsync(y, m);
                return View(model);
            }
            catch (Exception ex)
            {
                LogHelper.LogToDatabase(User.Identity.Name, "TannarxAnalytics",
                    $"MonthSources xatolik: Year={y}, Month={m}, Error={ex.Message}");
                TempData["Error"] = $"Oy manbalarini hisoblashda xatolik: {ex.Message}";
                return RedirectToAction("Dashboard", new { year = y, month = m });
            }
        }

        // Olish so'rovi: TannarxAnalytics/FinanceCategories — Finance xarajat kategoriyalarini tannarx guruhiga bog'lash
        public async Task<ActionResult> FinanceCategories(int? year, int? month)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Tannarx"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            ViewBag.Year = year;
            ViewBag.Month = month;
            var model = await _analyticsService.GetFinanceCategoryMapAsync();
            return View(model);
        }

        // POST so'rovi: TannarxAnalytics/FinanceCategories
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FinanceCategories(FormCollection form, int? year, int? month)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "Tannarx", "Edit"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            try
            {
                var selection = new System.Collections.Generic.Dictionary<int, string>();
                foreach (string key in form.AllKeys.Where(k => k != null && k.StartsWith("bucket_")))
                {
                    if (int.TryParse(key.Substring("bucket_".Length), out var categoryId))
                        selection[categoryId] = form[key];
                }

                await _analyticsService.SaveFinanceCategoryMapAsync(selection, UserHelper.GetUserId(User.Identity.Name));
                LogHelper.LogToDatabase(User.Identity.Name, "TannarxAnalytics",
                    $"Finance kategoriya guruhlari saqlandi: {selection.Count(kv => !string.IsNullOrWhiteSpace(kv.Value))} ta qo'lda belgilangan");
                TempData["Success"] = "Kategoriya guruhlari saqlandi. Tannarxga ta'sir qilishi uchun oy bo'yicha \"Fakt xarajatlarni yangilash\" ni bosing.";
            }
            catch (Exception ex)
            {
                LogHelper.LogToDatabase(User.Identity.Name, "TannarxAnalytics", $"Finance kategoriya guruhlarini saqlashda xatolik: {ex.Message}");
                TempData["Error"] = $"Saqlashda xatolik: {ex.Message}";
            }

            return RedirectToAction("FinanceCategories", new { year, month });
        }

        // Olish so'rovi: TannarxAnalytics/ProductDetails
        public async Task<ActionResult> ProductDetails(int productID, int year, int month)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Tannarx"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }
            try
            {
                var productReport = await _analyticsService.GetProductDetailsAsync(productID, year, month);

                if (productReport == null)
                {
                    TempData["Error"] = "Ma'lumot topilmadi.";
                    return RedirectToAction("Report");
                }

                return View(productReport);
            }
            catch (Exception ex)
            {
                LogHelper.LogToDatabase(User.Identity.Name, "TannarxAnalytics",
                    $"ProductDetails xatolik: ProductID={productID}, Year={year}, Month={month}, Error={ex.Message}");
                TempData["Error"] = $"ProductDetails ochishda xatolik: {ex.Message}";
                return RedirectToAction("Report", new { year, month, productID });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
