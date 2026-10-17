using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Services;
using tahsinERP.Controllers;
using tahsinERP.Filters;

namespace tahsinERP.Controllers.SRM
{
    /// <summary>
    /// Controller uchun transferring finished goods dan Production Warehouse ga Sales Warehouse
    /// </summary>
    [Permission]
    public class ProductionToSalesTransferController : Controller
    {
        // Olish so'rovi: ProductionToSalesTransfer
        public async Task<ActionResult> Index(DateTime? fromDate, DateTime? toDate, int? productID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // SRM-P04: sahifada ko'rsatiladigan standart 30 kunlik oraliq endi so'rovga ham qo'llanadi
                // (avval sana bo'sh bo'lsa butun tarix Include'lar bilan yuklanardi); AsNoTracking.
                var from = (fromDate ?? DateTime.Today.AddDays(-30)).Date;
                var query = db.EXPENSES
                    .AsNoTracking()
                    .Include(e => e.Warehouse) // Sender (Production WH)
                    .Include(e => e.Warehouse1) // Receiver (Sales WH)
                    .Include(e => e.EXPENSE_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(e => e.EXPENSE_PRODUCTS.Select(p => p.UNIT))
                    .Where(e => e.IsDeleted == false
                        && (e.DocNo.StartsWith("TRNSF_") || e.DocNo.Contains("TRANSFER-"))
                        && e.IssueDateTime >= from);

                if (toDate.HasValue)
                {
                    var toExclusive = toDate.Value.Date.AddDays(1);
                    query = query.Where(e => e.IssueDateTime < toExclusive);
                }

                if (productID.HasValue)
                {
                    query = query.Where(e => e.EXPENSE_PRODUCTS.Any(p => p.ProductID == productID.Value));
                }

                var transfers = await query
                    .OrderByDescending(e => e.IssueDateTime)
                    .ToListAsync();

                ViewBag.Products = new SelectList(
                    await db.PRODUCTS
                        .AsNoTracking()
                        .Where(p => p.IsDeleted == false && p.IsFinishedGood == true)
                        .OrderBy(p => p.PNo)
                        .Select(p => new { p.ID, p.PNo })
                        .ToListAsync(),
                    "ID", "PNo", productID);

                ViewBag.FromDate = from;
                ViewBag.ToDate = toDate ?? DateTime.Now;

                return View(transfers);
            }
        }

        // Olish so'rovi: ProductionToSalesTransfer/yaratish
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var productionWH = WarehouseFlowService.GetProductionWarehouse(db);
                var salesWH = WarehouseFlowService.GetSalesWarehouse(db);

                if (productionWH == null || salesWH == null)
                {
                    TempData["ErrorMessage"] = "Ishlab chiqarish yoki sotuv ombori topilmadi. Avval omborlarni sozlash kerak.";
                    return RedirectToAction("Index");
                }

                ViewBag.ProductionWH = productionWH;
                ViewBag.SalesWH = salesWH;
                ViewBag.Products = BuildProductionWarehouseProductSelectList(db);

                var viewModel = new ProductionToSalesTransferViewModel
                {
                    TransferDate = DateTime.Now,
                    ProductionWHID = productionWH.ID,
                    SalesWHID = salesWH.ID
                };

                return View(viewModel);
            }
        }

        // POST so'rovi: ProductionToSalesTransfer/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ProductionToSalesTransferViewModel viewModel, 
            List<TransferLineViewModel> lines, HttpPostedFileBase transferDocument)
        {
            // Transfer date max = today (server validation)
            if (viewModel.TransferDate.Date > DateTime.Today)
            {
                ModelState.AddModelError("TransferDate", "O'tkazma sanasi bugungi kunidan oshmasligi kerak.");
            }

            if (ModelState.IsValid && lines != null && lines.Any(l => l.Quantity > 0))
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        // Tekshirish: lot tanlanishi majburiy, quality, va stock
                        foreach (var line in lines.Where(l => l.Quantity > 0))
                        {
                            var allocations = string.IsNullOrWhiteSpace(line.LotAllocations)
                                ? new List<LotAllocation>()
                                : ProductionLotService.ParseLotAllocations(line.LotAllocations);

                            var availableStock = WarehouseFlowService.GetProductionStock(line.ProductID, db);
                            if (availableStock < line.Quantity)
                            {
                                var product = db.PRODUCTS.Find(line.ProductID);
                                ModelState.AddModelError("", 
                                    $"Mahsulot {product?.PNo ?? line.ProductID.ToString()} uchun ishlab chiqarish omborida yetarli qoldiq yo'q. " +
                                    $"Mavjud: {availableStock}, Talab qilinmoqda: {line.Quantity}");
                                ReloadViewBag(db, viewModel);
                                return View(viewModel);
                            }

                            if (!allocations.Any())
                            {
                                var product = db.PRODUCTS.Find(line.ProductID);
                                ModelState.AddModelError("", 
                                    $"Mahsulot {product?.PNo ?? line.ProductID.ToString()} uchun sifat nazoratidan o'tgan lot tanlash majburiy.");
                                ReloadViewBag(db, viewModel);
                                return View(viewModel);
                            }

                            if (allocations.Any())
                            {
                                var allocSum = allocations.Sum(a => a.Quantity);
                                if (Math.Abs(allocSum - line.Quantity) > 0.001)
                                {
                                    ModelState.AddModelError("", $"Mahsulot uchun tanlangan lotlar yig'indisi ({allocSum}) kiritilgan miqdor ({line.Quantity})ga mos kelmadi.");
                                    ReloadViewBag(db, viewModel);
                                    return View(viewModel);
                                }
                                if (!ProductionLotService.AllLotsPassedQuality(allocations, line.ProductID, db, out var qErr))
                                {
                                    ModelState.AddModelError("", qErr);
                                    ReloadViewBag(db, viewModel);
                                    return View(viewModel);
                                }
                            }
                        }

                        // O'qish hujjat agar provided
                        byte[] documentBytes = null;
                        if (transferDocument != null && transferDocument.ContentLength > 0)
                        {
                            documentBytes = new byte[transferDocument.ContentLength];
                            transferDocument.InputStream.Read(documentBytes, 0, transferDocument.ContentLength);
                        }

                        // Barcha qatorlar BITTA chiqim + BITTA kirim hujjatida, bitta tranzaksiyada (SRM-B10).
                        // Hujjat raqami (bo'sh bo'lsa) tranzaksiya ichida qulf bilan beriladi.
                        var transferLines = lines
                            .Where(l => l.Quantity > 0)
                            .Select(l => new WarehouseFlowService.ProductionTransferLine
                            {
                                ProductID = l.ProductID,
                                Quantity = l.Quantity,
                                LotAllocations = ProductionLotService.ParseLotAllocations(l.LotAllocations)
                            })
                            .ToList();

                        var expense = WarehouseFlowService.TransferProductionToSales(
                            db,
                            transferLines,
                            User.Identity.Name,
                            string.IsNullOrWhiteSpace(viewModel.TransferDocNo) ? null : viewModel.TransferDocNo.Trim(),
                            documentBytes,
                            viewModel.Description,
                            viewModel.TransferDate);
                        var transferDocNo = expense.DocNo;

                        LogHelper.LogToDatabase(User.Identity.Name, "ProductionToSalesTransfer", "Create",
                            $"Ishlab chiqarishdan sotuv omboriga o'tkazma yaratildi. Hujjat raqami: {transferDocNo}");

                        TempData["Success"] = "O'tkazma muvaffaqiyatli yaratildi";
                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"Xatolik: {ex.Message}";
                        var innerEx = ex.InnerException;
                        while (innerEx != null)
                        {
                            errorMessage += $" | Ichki xatolik: {innerEx.Message}";
                            innerEx = innerEx.InnerException;
                        }
                        ModelState.AddModelError("", errorMessage);
                    }
                }
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                ReloadViewBag(db, viewModel);
            }

            return View(viewModel);
        }

        private void ReloadViewBag(DBTHSNEntities db, ProductionToSalesTransferViewModel viewModel)
        {
            ViewBag.ProductionWH = WarehouseFlowService.GetProductionWarehouse(db);
            ViewBag.SalesWH = WarehouseFlowService.GetSalesWarehouse(db);
            ViewBag.Products = BuildProductionWarehouseProductSelectList(db);
        }

        /// <summary>
        /// Faqat ishlab chiqarish omborida mavjud bo'lgan tayyor mahsulotlar (Mavjud: X formatida).
        /// </summary>
        private static System.Web.Mvc.SelectList BuildProductionWarehouseProductSelectList(DBTHSNEntities db, int? selectedProductId = null)
        {
            var productionWH = WarehouseFlowService.GetProductionWarehouse(db);
            if (productionWH == null)
                return new SelectList(new List<SelectListItem>(), "Value", "Text", selectedProductId);

            var products = db.PRODUCT_STOCKS
                .Include(s => s.PRODUCT)
                .Where(s => s.WHID == productionWH.ID
                    && s.Amount > 0
                    && s.PRODUCT.IsDeleted == false
                    && s.PRODUCT.IsFinishedGood == true)
                .OrderBy(s => s.PRODUCT.PNo)
                .ToList();

            var selectItems = products.Select(p => new SelectListItem
            {
                Value = p.ProductID.ToString(),
                Text = $"{p.PRODUCT.PNo} - {p.PRODUCT.Name} (Mavjud: {p.Amount})"
            }).ToList();

            return new SelectList(selectItems, "Value", "Text", selectedProductId);
        }

        // Olish so'rovi: ProductionToSalesTransfer/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var expense = await db.EXPENSES
                    .Include(e => e.Warehouse)
                    .Include(e => e.Warehouse1)
                    .Include(e => e.EXPENSE_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(e => e.EXPENSE_PRODUCTS.Select(p => p.UNIT))
                    .FirstOrDefaultAsync(e => e.ID == id);

                if (expense == null)
                {
                    return HttpNotFound();
                }

                // Ilovalar: faqat ID (fayl blob'i yuklanmaydi — SRM-P12)
                ViewBag.DocIds = await db.WRHS_EXPENSE_DOCS.Where(d => d.ExpenseID == expense.ID).Select(d => d.ID).ToListAsync();

                // Olish corresponding INCOME hujjat (INCOME with INCOME_PRODUCTS and UNIT)
                // Bog'langan kirim izohdagi "EXPENS ID: n" bo'yicha aniq topiladi
                // (avval DocNo.Contains ishlatilardi: TRNSF_..._1 → TRNSF_..._10-IN ga ham mos kelardi).
                var expenseMarker = "EXPENS ID: " + expense.ID;
                var incomeCandidates = await db.INCOMES
                    .Include(i => i.INCOME_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(i => i.INCOME_PRODUCTS.Select(p => p.UNIT))
                    .Where(i => i.ReceivingWHID == expense.ReceiverWhID && i.IsDeleted != true
                        && (i.Description.Contains(expenseMarker) || i.DocNo == expense.DocNo + "-IN"))
                    .ToListAsync();
                var income = incomeCandidates.FirstOrDefault(i =>
                                 tahsinERP.Services.Stock.DocumentMarkers.Matches(i.Description, "EXPENS ID: ", expense.ID))
                             ?? (incomeCandidates.Count == 1 ? incomeCandidates[0] : null);

                ViewBag.CorrespondingIncome = income;

                return View(expense);
            }
        }

        /// <summary>
        /// Production lots (PRODUCT_PRODUCTION) - faqat sifat nazoratidan o'tganlar, FIFO.
        /// </summary>
        [HttpGet]
        public JsonResult GetAvailableProductionLots(int productID)
        {
            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var lots = ProductionLotService.GetAvailableProductionLots(productID, db);
                    var data = lots.Select(l => new
                    {
                        l.ProductionID,
                        l.Date,
                        l.Total,
                        l.AcceptedQty,
                        l.RemainingAmount,
                        l.DisplayText
                    }).ToList();
                    return Json(new { success = true, data }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // AJAX so'rovi: Get available stock in Production Warehouse
        [HttpGet]
        public JsonResult GetProductionStock(int productID)
        {
            try
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var stock = WarehouseFlowService.GetProductionStock(productID, db);
                    var product = db.PRODUCTS.Find(productID);
                    
                    return Json(new 
                    { 
                        success = true, 
                        stock = stock,
                        productNo = product?.PNo,
                        productName = product?.Name
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
