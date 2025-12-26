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

namespace tahsinERP.Controllers.Engineering
{
    [Authorize]
    public class TannarxController : Controller
    {
        private DBTHSNEntities db = new DBTHSNEntities();
        private TannarxService _tannarxService;

        public TannarxController()
        {
            _tannarxService = new TannarxService(db);
        }

        // GET: Tannarx
        public async Task<ActionResult> Index(int? year, int? month, int? productID)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Tannarx"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Default to current year and month if not provided
            var currentYear = year ?? DateTime.Now.Year;
            var currentMonth = month ?? DateTime.Now.Month;

            ViewBag.Year = currentYear;
            ViewBag.Month = currentMonth;
            ViewBag.ProductID = productID;

            // Get years for dropdown (last 5 years to next 5 years)
            var years = Enumerable.Range(DateTime.Now.Year - 5, 11).ToList();
            ViewBag.Years = new SelectList(years, currentYear);

            // Get months for dropdown
            var months = new List<SelectListItem>
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

            // Get products for dropdown (finished goods only)
            // Check both PRODUCT.IsFinishedGood and PRODUCT_CATEGORIES.IsFinishedGood
            var products = await db.PRODUCTS
                .Include(p => p.PRODUCT_CATEGORIES)
                .Where(p => p.IsDeleted == false 
                    && (p.IsFinishedGood == true 
                        || (p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.IsFinishedGood == true)))
                .OrderBy(p => p.PNo)
                .ToListAsync();

            ViewBag.Products = new SelectList(
                products.Select(p => new { p.ID, Name = $"{p.PNo} - {p.Name}" }),
                "ID", "Name", productID);

            // Get tannarx records for selected year, month, and product
            var query = db.TANNARXes
                .Include(t => t.PRODUCT)
                .Include(t => t.PRODUCT.PRODUCT_CATEGORIES)
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

            // Get count of finished goods products that have norms for this month
            // Use TannarxService method for consistent logic
            var finishedGoodsWithNorms = await _tannarxService.GetFinishedGoodsWithNormsCountAsync(currentYear, currentMonth);

            ViewBag.FinishedGoodsWithNormsCount = finishedGoodsWithNorms;
            ViewBag.TannarxCount = tannarxList.Count;
            ViewBag.ShowGenerateButton = tannarxList.Count == 0 || tannarxList.Count < finishedGoodsWithNorms;

            // Map to ViewModel
            var viewModelList = tannarxList.Select(t => new TannarxIndexViewModel
            {
                ID = t.ID,
                ProductID = t.ProductID,
                ProductPNo = t.PRODUCT?.PNo ?? "",
                ProductName = t.PRODUCT?.Name ?? "",
                ProductType = t.PRODUCT?.Type ?? "",
                Dimensions = $"{t.PRODUCT?.Length ?? 0}x{t.PRODUCT?.Width ?? 0}x{t.PRODUCT?.Height ?? 0}",
                Year = t.Year,
                Month = t.Month,
                MonthString = t.MonthString,
                MaterialPrice = t.MaterialPrice,
                MaterialTransportCost = t.MaterialTransportCost,
                LaborCost = t.LaborCost,
                TaxAndBank = t.TaxAndBank,
                AdministrativeExpenses = t.AdministrativeExpenses,
                OtherExpenses = t.OtherExpenses,
                Profit = t.Profit,
                SalesPriceEXWWithoutVAT = t.SalesPriceEXWWithoutVAT,
                SalesPriceEXWWithVAT = t.SalesPriceEXWWithVAT,
                SetPriceEXWWithVAT = t.SetPriceEXWWithVAT,
                FinishedProductTransportCost = t.FinishedProductTransportCost,
                SalesPriceCIPWithoutVAT = t.SalesPriceCIPWithoutVAT,
                SalesPriceCIPWithVAT = t.SalesPriceCIPWithVAT,
                SetPriceCIPWithVAT = t.SetPriceCIPWithVAT
            }).ToList();

            return View(viewModelList);
        }

        // POST: Tannarx/Generate
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
                var currentUser = await db.USERS
                    .FirstOrDefaultAsync(u => u.Uname == User.Identity.Name);

                var generatedCount = await _tannarxService.GenerateTannarxForMonthAsync(
                    year, month, currentUser?.ID);

                if (generatedCount > 0)
                {
                    LogHelper.LogToDatabase(User.Identity.Name, "Tannarx", 
                        $"{generatedCount} ta tannarx yaratildi - {year}-{month}");
                    TempData["Success"] = $"{generatedCount} ta tannarx muvaffaqiyatli yaratildi.";
                }
                else
                {
                    TempData["Info"] = "Yangi tannarx yaratilmadi. Barcha mahsulotlar uchun tannarx allaqachon mavjud.";
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

        // GET: Tannarx/Edit/5
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

            var viewModel = new TannarxEditViewModel
            {
                ID = tannarx.ID,
                ProductID = tannarx.ProductID,
                ProductPNo = tannarx.PRODUCT?.PNo ?? "",
                ProductName = tannarx.PRODUCT?.Name ?? "",
                ProductType = tannarx.PRODUCT?.Type ?? "",
                Dimensions = $"{tannarx.PRODUCT?.Length ?? 0}x{tannarx.PRODUCT?.Width ?? 0}x{tannarx.PRODUCT?.Height ?? 0}",
                Year = tannarx.Year,
                Month = tannarx.Month,
                MonthString = tannarx.MonthString,
                MaterialPrice = tannarx.MaterialPrice,
                MaterialTransportCost = tannarx.MaterialTransportCost,
                LaborCost = tannarx.LaborCost,
                TaxAndBank = tannarx.TaxAndBank,
                AdministrativeExpenses = tannarx.AdministrativeExpenses,
                OtherExpenses = tannarx.OtherExpenses,
                Profit = tannarx.Profit,
                TaxAndBankPercent = tannarx.TaxAndBankPercent ?? 5m,
                AdministrativeExpensesPercent = tannarx.AdministrativeExpensesPercent ?? 5m,
                OtherExpensesPercent = tannarx.OtherExpensesPercent ?? 5m,
                ProfitPercent = tannarx.ProfitPercent ?? 10m,
                SalesPriceEXWWithoutVAT = tannarx.SalesPriceEXWWithoutVAT,
                SalesPriceEXWWithVAT = tannarx.SalesPriceEXWWithVAT,
                SetPriceEXWWithVAT = tannarx.SetPriceEXWWithVAT,
                FinishedProductTransportCost = tannarx.FinishedProductTransportCost,
                SalesPriceCIPWithoutVAT = tannarx.SalesPriceCIPWithoutVAT,
                SalesPriceCIPWithVAT = tannarx.SalesPriceCIPWithVAT,
                SetPriceCIPWithVAT = tannarx.SetPriceCIPWithVAT
            };

            return View(viewModel);
        }

        // POST: Tannarx/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(TannarxEditViewModel viewModel)
        {
            if (!RoleHelper.IsChangePermitted(User.Identity.Name, "Tannarx", "Edit"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
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

                var currentUser = await db.USERS
                    .FirstOrDefaultAsync(u => u.Uname == User.Identity.Name);

                // Update fields
                tannarx.LaborCost = viewModel.LaborCost;
                tannarx.TaxAndBankPercent = viewModel.TaxAndBankPercent;
                tannarx.AdministrativeExpensesPercent = viewModel.AdministrativeExpensesPercent;
                tannarx.OtherExpensesPercent = viewModel.OtherExpensesPercent;
                tannarx.ProfitPercent = viewModel.ProfitPercent;
                tannarx.SetPriceEXWWithVAT = viewModel.SetPriceEXWWithVAT;
                tannarx.FinishedProductTransportCost = viewModel.FinishedProductTransportCost;
                tannarx.SetPriceCIPWithVAT = viewModel.SetPriceCIPWithVAT;
                tannarx.ModifiedByUserID = currentUser?.ID;
                tannarx.ModifiedDT = DateTime.Now;

                // Recalculate prices based on SetPriceEXWWithVAT
                if (tannarx.SetPriceEXWWithVAT.HasValue)
                {
                    _tannarxService.RecalculatePricesFromSetPriceEXW(tannarx);
                }

                // Recalculate CIP prices if FinishedProductTransportCost changed
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

        // GET: Tannarx/Details/5
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

            var viewModel = new TannarxDetailsViewModel
            {
                ID = tannarx.ID,
                ProductID = tannarx.ProductID,
                ProductPNo = tannarx.PRODUCT?.PNo ?? "",
                ProductName = tannarx.PRODUCT?.Name ?? "",
                ProductType = tannarx.PRODUCT?.Type ?? "",
                Dimensions = $"{tannarx.PRODUCT?.Length ?? 0}x{tannarx.PRODUCT?.Width ?? 0}x{tannarx.PRODUCT?.Height ?? 0}",
                Year = tannarx.Year,
                Month = tannarx.Month,
                MonthString = tannarx.MonthString,
                MaterialPrice = tannarx.MaterialPrice,
                MaterialTransportCost = tannarx.MaterialTransportCost,
                LaborCost = tannarx.LaborCost,
                TaxAndBank = tannarx.TaxAndBank,
                AdministrativeExpenses = tannarx.AdministrativeExpenses,
                OtherExpenses = tannarx.OtherExpenses,
                Profit = tannarx.Profit,
                TaxAndBankPercent = tannarx.TaxAndBankPercent,
                AdministrativeExpensesPercent = tannarx.AdministrativeExpensesPercent,
                OtherExpensesPercent = tannarx.OtherExpensesPercent,
                ProfitPercent = tannarx.ProfitPercent,
                SalesPriceEXWWithoutVAT = tannarx.SalesPriceEXWWithoutVAT,
                SalesPriceEXWWithVAT = tannarx.SalesPriceEXWWithVAT,
                SetPriceEXWWithVAT = tannarx.SetPriceEXWWithVAT,
                FinishedProductTransportCost = tannarx.FinishedProductTransportCost,
                SalesPriceCIPWithoutVAT = tannarx.SalesPriceCIPWithoutVAT,
                SalesPriceCIPWithVAT = tannarx.SalesPriceCIPWithVAT,
                SetPriceCIPWithVAT = tannarx.SetPriceCIPWithVAT,
                CreatedBy = tannarx.USER?.FullName ?? tannarx.USER?.Uname ?? "",
                CreatedDT = tannarx.CreatedDT,
                ModifiedBy = tannarx.USER1?.FullName ?? tannarx.USER1?.Uname ?? "",
                ModifiedDT = tannarx.ModifiedDT
            };

            return View(viewModel);
        }

        // GET: Tannarx/Delete/5
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

            var viewModel = new TannarxDetailsViewModel
            {
                ID = tannarx.ID,
                ProductID = tannarx.ProductID,
                ProductPNo = tannarx.PRODUCT?.PNo ?? "",
                ProductName = tannarx.PRODUCT?.Name ?? "",
                Year = tannarx.Year,
                Month = tannarx.Month,
                MonthString = tannarx.MonthString
            };

            return View(viewModel);
        }

        // POST: Tannarx/Delete/5
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

                // Soft delete
                tannarx.IsDeleted = true;
                tannarx.ModifiedDT = DateTime.Now;
                var currentUser = await db.USERS
                    .FirstOrDefaultAsync(u => u.Uname == User.Identity.Name);
                tannarx.ModifiedByUserID = currentUser?.ID;

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
