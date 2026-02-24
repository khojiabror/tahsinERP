using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels.Tannarx;

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

            // Olish months uchun dropdown
            var months = new System.Collections.Generic.List<SelectListItem>
            {
                new SelectListItem { Value = "1", Text = "Yanvar" },
                new SelectListItem { Value = "2", Text = "Fevral" },
                new SelectListItem { Value = "3", Text = "Mart" },
                new SelectListItem { Value = "4", Text = "Aprel" },
                new SelectListItem { Value = "5", Text = "May" },
                new SelectListItem { Value = "6", Text = "Iyun" },
                new SelectListItem { Value = "7", Text = "Iyul" },
                new SelectListItem { Value = "8", Text = "Avgust" },
                new SelectListItem { Value = "9", Text = "Sentabr" },
                new SelectListItem { Value = "10", Text = "Oktabr" },
                new SelectListItem { Value = "11", Text = "Noyabr" },
                new SelectListItem { Value = "12", Text = "Dekabr" }
            };
            ViewBag.Months = new SelectList(months, "Value", "Text", currentMonth);

            // Olish products uchun dropdown (finished goods faqat)
            // Tekshirish both PRODUCT.IsFinishedGood va PRODUCT_CATEGORIES.IsFinishedGood
            var products = await db.PRODUCTS
                .Include(p => p.PRODUCT_CATEGORIES)
                .Where(p => p.IsDeleted == false 
                    && (p.IsFinishedGood == true 
                        || (p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.IsFinishedGood == true)))
                .OrderBy(p => p.PNo)
                .ToListAsync();

            // Qo'shish "Barchasi" option ga the product list
            var productList = products.Select(p => new { p.ID, Name = $"{p.PNo} - {p.Name}" }).ToList();
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

            // Olish months uchun dropdown
            var months = new System.Collections.Generic.List<SelectListItem>
            {
                new SelectListItem { Value = "1", Text = "Yanvar" },
                new SelectListItem { Value = "2", Text = "Fevral" },
                new SelectListItem { Value = "3", Text = "Mart" },
                new SelectListItem { Value = "4", Text = "Aprel" },
                new SelectListItem { Value = "5", Text = "May" },
                new SelectListItem { Value = "6", Text = "Iyun" },
                new SelectListItem { Value = "7", Text = "Iyul" },
                new SelectListItem { Value = "8", Text = "Avgust" },
                new SelectListItem { Value = "9", Text = "Sentabr" },
                new SelectListItem { Value = "10", Text = "Oktabr" },
                new SelectListItem { Value = "11", Text = "Noyabr" },
                new SelectListItem { Value = "12", Text = "Dekabr" }
            };
            ViewBag.Months = new SelectList(months, "Value", "Text", month);

            // Olish products uchun dropdown (finished goods faqat)
            // Tekshirish both PRODUCT.IsFinishedGood va PRODUCT_CATEGORIES.IsFinishedGood
            var products = await db.PRODUCTS
                .Include(p => p.PRODUCT_CATEGORIES)
                .Where(p => p.IsDeleted == false 
                    && (p.IsFinishedGood == true 
                        || (p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.IsFinishedGood == true)))
                .OrderBy(p => p.PNo)
                .ToListAsync();

            // Qo'shish "Barchasi" option ga the product list
            var productList = products.Select(p => new { p.ID, Name = $"{p.PNo} - {p.Name}" }).ToList();
            ViewBag.Products = new SelectList(productList, "ID", "Name", productID);

            // Olish report ma'lumot
            var report = await _analyticsService.GetAnalyticsReportAsync(year, month, productID);

            return View(report);
        }

        // POST so'rovi: TannarxAnalytics/UpdateActualCosts
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UpdateActualCosts(int? year, int? month)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "Tannarx", "Edit"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            try
            {
                var recordsUpdated = await _analyticsService.UpdateActualCostsAsync(year, month);

                LogHelper.LogToDatabase(User.Identity.Name, "TannarxAnalytics", 
                    $"Actual costs yangilandi - Year: {year}, Month: {month}, Records: {recordsUpdated}");
                
                TempData["Success"] = $"{recordsUpdated} ta tannarx uchun actual costs yangilandi.";
            }
            catch (Exception ex)
            {
                LogHelper.LogToDatabase(User.Identity.Name, "TannarxAnalytics", 
                    $"Actual costs yangilashda xatolik: {ex.Message}");
                TempData["Error"] = $"Actual costs yangilashda xatolik: {ex.Message}";
            }

            return RedirectToAction("Dashboard", new { year = year, month = month });
        }

        // Olish so'rovi: TannarxAnalytics/ProductDetails
        public async Task<ActionResult> ProductDetails(int productID, int year, int month)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Tannarx"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var report = await _analyticsService.GetAnalyticsReportAsync(year, month, productID);
            var productReport = report.Reports.FirstOrDefault();

            if (productReport == null)
            {
                TempData["Error"] = "Ma'lumot topilmadi.";
                return RedirectToAction("Report");
            }

            return View(productReport);
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
