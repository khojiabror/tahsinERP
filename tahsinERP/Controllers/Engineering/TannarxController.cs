using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels.Tannarx;
using tahsinERP.Helpers;

namespace tahsinERP.Controllers.Engineering
{
    [Authorize]
    public class TannarxController : BaseController
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private TannarxService _tannarxService;

        public TannarxController()
        {
            _tannarxService = new TannarxService(db);
        }

        // Olish so'rovi: Tannarx
        public async Task<ActionResult> Index(int? year, int? month, int? productID)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Tannarx"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Default filter: selected year/month (month is required for generation/count logic)
            var currentYear = year ?? DateTime.Now.Year;
            var currentMonth = month ?? DateTime.Now.Month;

            ViewBag.Year = currentYear;
            ViewBag.Month = currentMonth;
            ViewBag.ProductID = productID;

            // Olish years uchun dropdown (last 5 years ga next 5 years)
            var years = Enumerable.Range(DateTime.Now.Year - 5, 11).ToList();
            ViewBag.Years = new SelectList(years, currentYear);

            ViewBag.Months = UzMonthNames.SelectList(currentMonth);

            // Tayyor mahsulotlar (proyeksiya, "PNo - Name")
            var products = await MesLookups.FinishedGoodItemsAsync(db);

            ViewBag.Products = new SelectList(products, "ID", "Name", productID);

            // Olish tannarx records uchun selected year, month, va product
            var query = db.TANNARXes.AsNoTracking()
                .Include(t => t.PRODUCT)
                .Where(t => t.Year == currentYear 
                    && t.Month == currentMonth
                    && t.IsDeleted == false);

            if (productID.HasValue)
            {
                query = query.Where(t => t.ProductID == productID.Value);
            }

            var tannarxList = await query
                .OrderBy(t => t.PRODUCT.PNo)
                .ToListAsync();

            // Olish count of finished goods products that have norms uchun this month
            // Use TannarxService method uchun consistent logic
            int finishedGoodsWithNorms;
            int totalTannarxForMonth;

            if (productID.HasValue)
            {
                // Product filter tanlangan bo'lsa, hamma count faqat shu product uchun bo'ladi
                var hasNormForSelectedProduct = await _tannarxService.HasNormsForMonthAsync(productID.Value, currentYear, currentMonth);
                finishedGoodsWithNorms = hasNormForSelectedProduct ? 1 : 0;
                totalTannarxForMonth = await db.TANNARXes
                    .Where(t => t.Year == currentYear
                        && t.Month == currentMonth
                        && t.ProductID == productID.Value
                        && !t.IsDeleted)
                    .CountAsync();
            }
            else
            {
                // Product filter yo'q bo'lsa, count normasi bor barcha mahsulotlar bo'yicha bo'ladi
                var normProductIds = await _tannarxService.GetFinishedGoodsWithNormsProductIdsAsync(currentYear, currentMonth);
                finishedGoodsWithNorms = normProductIds.Count;
                totalTannarxForMonth = await db.TANNARXes
                    .Where(t => t.Year == currentYear
                        && t.Month == currentMonth
                        && normProductIds.Contains(t.ProductID)
                        && !t.IsDeleted)
                    .CountAsync();
            }

            var missingTannarxCount = Math.Max(0, finishedGoodsWithNorms - totalTannarxForMonth);

            ViewBag.FinishedGoodsWithNormsCount = finishedGoodsWithNorms;
            ViewBag.TannarxCount = totalTannarxForMonth;
            ViewBag.MissingTannarxCount = missingTannarxCount;
            ViewBag.ShowGenerateButton = missingTannarxCount > 0;

            // Map ga ViewModel
            var viewModelList = tannarxList.Select(TannarxMapper.Map<TannarxIndexViewModel>).ToList();

            return View(viewModelList);
        }

        // POST so'rovi: Tannarx/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Generate(int year, int month)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "Tannarx", "Create"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            try
            {
                var generatedCount = await _tannarxService.GenerateTannarxForMonthAsync(
                    year, month, RoleHelper.GetUserId(CurrentUserName));

                if (generatedCount > 0)
                {
                    LogHelper.LogToDatabase(User.Identity.Name, "Tannarx", 
                        $"{generatedCount} ta tannarx yaratildi - {year}-{month}");
                    TempData["Success"] = $"{generatedCount} ta tannarx muvaffaqiyatli yaratildi.";
                }
                else
                {
                    var normProductIds = await _tannarxService.GetFinishedGoodsWithNormsProductIdsAsync(year, month);
                    var existingCount = await db.TANNARXes
                        .Where(t => t.Year == year
                            && t.Month == month
                            && !t.IsDeleted
                            && normProductIds.Contains(t.ProductID))
                        .CountAsync();

                    if (!normProductIds.Any())
                    {
                        TempData["Info"] = "Ushbu oy uchun norma kiritilgan mahsulot topilmadi.";
                    }
                    else if (existingCount >= normProductIds.Count)
                    {
                        TempData["Info"] = "Yangi tannarx yaratilmadi. Norma mavjud mahsulotlar uchun tannarx allaqachon yaratilgan.";
                    }
                    else
                    {
                        TempData["Info"] = "Yangi tannarx yaratilmadi. Iltimos, norma davri va mahsulot ma'lumotlarini tekshiring.";
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogToDatabase(User.Identity.Name, "Tannarx", 
                    $"Tannarx yaratishda xatolik: {ex.Message}");
                TempData["Error"] = $"Tannarx yaratishda xatolik yuz berdi: {ex.Message}";
            }

            return RedirectToAction("Index", new { year = year, month = month });
        }

        // Olish so'rovi: Tannarx/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "Tannarx", "Edit"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var tannarx = await db.TANNARXes
                .Include(t => t.PRODUCT)
                .Include(t => t.PRODUCT.PRODUCT_CATEGORIES)
                .FirstOrDefaultAsync(t => t.ID == id && t.IsDeleted == false);

            if (tannarx == null)
            {
                return HttpNotFound();
            }

            var viewModel = TannarxMapper.Map<TannarxEditViewModel>(tannarx);
            viewModel.TaxAndBankPercent = tannarx.TaxAndBankPercent ?? 5m;
            viewModel.AdministrativeExpensesPercent = tannarx.AdministrativeExpensesPercent ?? 5m;
            viewModel.OtherExpensesPercent = tannarx.OtherExpensesPercent ?? 5m;
            viewModel.ProfitPercent = tannarx.ProfitPercent ?? 10m;

            return View(viewModel);
        }

        // POST so'rovi: Tannarx/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(TannarxEditViewModel viewModel, string refreshMaterial)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "Tannarx", "Edit"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Edit sahifasidan "Normadan yangilash" tugmasi.
            // Material narxi tahrirlanmaydi, shu oy normasi bo'yicha qayta hisoblanadi.
            if (string.Equals(refreshMaterial, "1", StringComparison.Ordinal))
            {
                var tannarxForRefresh = await db.TANNARXes
                    .FirstOrDefaultAsync(t => t.ID == viewModel.ID && t.IsDeleted == false);

                if (tannarxForRefresh == null)
                {
                    TempData["Error"] = "Tannarx topilmadi.";
                    return RedirectToAction("Index");
                }

                var monthString = $"{tannarxForRefresh.Year}-{tannarxForRefresh.Month:D2}";
                var materialPrice = await _tannarxService.CalculateMaterialPriceAsync(tannarxForRefresh.ProductID, monthString);
                tannarxForRefresh.MaterialPrice = materialPrice;
                tannarxForRefresh.MaterialTransportCost = _tannarxService.CalculateMaterialTransportCost(materialPrice);
                tannarxForRefresh.ModifiedDT = DateTime.Now;

                tannarxForRefresh.ModifiedByUserID = RoleHelper.GetUserId(CurrentUserName);

                if (tannarxForRefresh.SetPriceEXWWithVAT.HasValue)
                {
                    _tannarxService.RecalculatePricesFromSetPriceEXW(tannarxForRefresh);
                }
                if (tannarxForRefresh.FinishedProductTransportCost.HasValue)
                {
                    _tannarxService.RecalculateCIPPrices(tannarxForRefresh);
                }

                await db.SaveChangesAsync();
                TempData["Success"] = "Material narxi normadan yangilandi.";
                return RedirectToAction("Edit", new { id = viewModel.ID });
            }

            if (!ModelState.IsValid)
            {
                var tannarx = await db.TANNARXes
                    .Include(t => t.PRODUCT)
                    .FirstOrDefaultAsync(t => t.ID == viewModel.ID && t.IsDeleted == false);

                if (tannarx != null)
                {
                    viewModel.ProductPNo = tannarx.PRODUCT?.PNo ?? "";
                    viewModel.ProductName = tannarx.PRODUCT?.Name ?? "";
                }

                return View(viewModel);
            }

            try
            {
                var tannarx = await db.TANNARXes
                    .FirstOrDefaultAsync(t => t.ID == viewModel.ID && t.IsDeleted == false);

                if (tannarx == null)
                {
                    TempData["Error"] = "Tannarx topilmadi.";
                    return RedirectToAction("Index");
                }

                // Yangilash fields
                tannarx.LaborCost = viewModel.LaborCost;
                tannarx.TaxAndBankPercent = viewModel.TaxAndBankPercent;
                tannarx.AdministrativeExpensesPercent = viewModel.AdministrativeExpensesPercent;
                tannarx.OtherExpensesPercent = viewModel.OtherExpensesPercent;
                tannarx.ProfitPercent = viewModel.ProfitPercent;
                tannarx.SetPriceEXWWithVAT = viewModel.SetPriceEXWWithVAT;
                tannarx.FinishedProductTransportCost = viewModel.FinishedProductTransportCost;
                tannarx.SetPriceCIPWithVAT = viewModel.SetPriceCIPWithVAT;
                tannarx.ModifiedByUserID = RoleHelper.GetUserId(CurrentUserName);
                tannarx.ModifiedDT = DateTime.Now;

                // Recalculate prices based on SetPriceEXWWithVAT
                if (tannarx.SetPriceEXWWithVAT.HasValue)
                {
                    _tannarxService.RecalculatePricesFromSetPriceEXW(tannarx);
                }

                // Recalculate CIP prices agar FinishedProductTransportCost changed
                if (viewModel.FinishedProductTransportCost.HasValue)
                {
                    _tannarxService.RecalculateCIPPrices(tannarx);
                }

                await db.SaveChangesAsync();

                LogHelper.LogToDatabase(User.Identity.Name, "Tannarx", 
                    $"Tannarx yangilandi - ProductID: {tannarx.ProductID}, Year: {tannarx.Year}, Month: {tannarx.Month}");
                TempData["Success"] = "Tannarx muvaffaqiyatli yangilandi.";

                return RedirectToAction("Index", new { year = tannarx.Year, month = tannarx.Month });
            }
            catch (Exception ex)
            {
                LogHelper.LogToDatabase(User.Identity.Name, "Tannarx", 
                    $"Tannarx yangilashda xatolik: {ex.Message}");
                TempData["Error"] = $"Tannarx yangilashda xatolik yuz berdi: {ex.Message}";
                return View(viewModel);
            }
        }

        // Olish so'rovi: Tannarx/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Tannarx"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var tannarx = await db.TANNARXes
                .Include(t => t.PRODUCT)
                .Include(t => t.PRODUCT.PRODUCT_CATEGORIES)
                .Include(t => t.USER)
                .Include(t => t.USER1)
                .FirstOrDefaultAsync(t => t.ID == id && t.IsDeleted == false);

            if (tannarx == null)
            {
                return HttpNotFound();
            }

            var viewModel = TannarxMapper.Map<TannarxDetailsViewModel>(tannarx);
            viewModel.TaxAndBankPercent = tannarx.TaxAndBankPercent;
            viewModel.AdministrativeExpensesPercent = tannarx.AdministrativeExpensesPercent;
            viewModel.OtherExpensesPercent = tannarx.OtherExpensesPercent;
            viewModel.ProfitPercent = tannarx.ProfitPercent;
            viewModel.CreatedBy = tannarx.USER?.FullName ?? tannarx.USER?.Uname ?? "";
            viewModel.CreatedDT = tannarx.CreatedDT;
            viewModel.ModifiedBy = tannarx.USER1?.FullName ?? tannarx.USER1?.Uname ?? "";
            viewModel.ModifiedDT = tannarx.ModifiedDT;

            return View(viewModel);
        }

        // Olish so'rovi: Tannarx/o'chirish/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "Tannarx", "Delete"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var tannarx = await db.TANNARXes
                .Include(t => t.PRODUCT)
                .FirstOrDefaultAsync(t => t.ID == id && t.IsDeleted == false);

            if (tannarx == null)
            {
                return HttpNotFound();
            }

            var viewModel = TannarxMapper.Map<TannarxDetailsViewModel>(tannarx);

            return View(viewModel);
        }

        // POST so'rovi: Tannarx/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "Tannarx", "Delete"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            try
            {
                var tannarx = await db.TANNARXes
                    .FirstOrDefaultAsync(t => t.ID == id && t.IsDeleted == false);

                if (tannarx == null)
                {
                    TempData["Error"] = "Tannarx topilmadi.";
                    return RedirectToAction("Index");
                }

                // Soft o'chirish
                tannarx.IsDeleted = true;
                tannarx.ModifiedDT = DateTime.Now;
                tannarx.ModifiedByUserID = RoleHelper.GetUserId(CurrentUserName);

                await db.SaveChangesAsync();

                LogHelper.LogToDatabase(User.Identity.Name, "Tannarx", 
                    $"Tannarx o'chirildi - ProductID: {tannarx.ProductID}, Year: {tannarx.Year}, Month: {tannarx.Month}");
                TempData["Success"] = "Tannarx muvaffaqiyatli o'chirildi.";

                return RedirectToAction("Index", new { year = tannarx.Year, month = tannarx.Month });
            }
            catch (Exception ex)
            {
                LogHelper.LogToDatabase(User.Identity.Name, "Tannarx", 
                    $"Tannarx o'chirishda xatolik: {ex.Message}");
                TempData["Error"] = $"Tannarx o'chirishda xatolik yuz berdi: {ex.Message}";
                return RedirectToAction("Index");
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
