using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Controllers;

namespace tahsinERP.Controllers.SRM
{
    /// <summary>
    /// Controller uchun transferring finished goods dan Production Warehouse ga Sales Warehouse
    /// </summary>
    public class ProductionToSalesTransferController : Controller
    {
        // Olish so'rovi: ProductionToSalesTransfer
        public async Task<ActionResult> Index(DateTime? fromDate, DateTime? toDate, int? productID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = db.EXPENSES
                    .Include(e => e.Warehouse) // Sender (Production WH)
                    .Include(e => e.Warehouse1) // Receiver (Sales WH)
                    .Include(e => e.EXPENSE_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(e => e.EXPENSE_PRODUCTS.Select(p => p.UNIT))
                    .Where(e => e.IsDeleted == false 
                        && (e.DocNo.StartsWith("TRNSF_") || e.DocNo.Contains("TRANSFER-")))
                    .AsQueryable();

                if (fromDate.HasValue)
                {
                    query = query.Where(e => e.IssueDateTime >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(e => e.IssueDateTime <= toDate.Value);
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
                        .Where(p => p.IsDeleted == false && p.IsFinishedGood == true)
                        .OrderBy(p => p.PNo)
                        .ToListAsync(),
                    "ID", "PNo", productID);

                ViewBag.FromDate = fromDate ?? DateTime.Now.AddDays(-30);
                ViewBag.ToDate = toDate ?? DateTime.Now;

                return View(transfers);
            }
        }

        // Olish so'rovi: ProductionToSalesTransfer/yaratish
        public async Task<ActionResult> Create()
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
                        // Tekshirish: lot tanlanishi (yoki "lotsiz o'tkazish"), quality, va stock
                        foreach (var line in lines.Where(l => l.Quantity > 0))
                        {
                            var isLotsiz = string.Equals(line.LotAllocations?.Trim(), "LOTSIZ", StringComparison.OrdinalIgnoreCase)
                                || string.IsNullOrWhiteSpace(line.LotAllocations);
                            var allocations = isLotsiz ? new List<LotAllocation>() : ProductionLotService.ParseLotAllocations(line.LotAllocations);

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

                            if (!isLotsiz && !allocations.Any())
                            {
                                var product = db.PRODUCTS.Find(line.ProductID);
                                ModelState.AddModelError("", 
                                    $"Mahsulot {product?.PNo ?? line.ProductID.ToString()} uchun lot (ishlab chiqarish) tanlang yoki \"Lotsiz o'tkazish\" ni tanlang.");
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

                        // DocNo: use GetNextTransferDocNo when empty (TRNSF_ddmmyy_N format)
                        var transferDocNo = viewModel.TransferDocNo;
                        if (string.IsNullOrWhiteSpace(transferDocNo))
                        {
                            transferDocNo = WarehouseFlowService.GetNextTransferDocNo(db, viewModel.TransferDate);
                        }

                        // Process har bir transfer line
                        foreach (var line in lines.Where(l => l.Quantity > 0))
                        {
                            var isLotsiz = string.Equals(line.LotAllocations?.Trim(), "LOTSIZ", StringComparison.OrdinalIgnoreCase)
                                || string.IsNullOrWhiteSpace(line.LotAllocations);
                            var allocations = isLotsiz ? null : ProductionLotService.ParseLotAllocations(line.LotAllocations);
                            if (allocations != null && !allocations.Any()) allocations = null;
                            WarehouseFlowService.TransferProductionToSales(
                                line.ProductID,
                                line.Quantity,
                                db,
                                User.Identity.Name,
                                transferDocNo,
                                documentBytes,
                                viewModel.Description,
                                allocations,
                                viewModel.TransferDate
                            );
                        }

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
                    .Include(e => e.WRHS_EXPENSE_DOCS)
                    .FirstOrDefaultAsync(e => e.ID == id);

                if (expense == null)
                {
                    return HttpNotFound();
                }

                // Olish corresponding INCOME hujjat (INCOME with INCOME_PRODUCTS and UNIT)
                var income = await db.INCOMES
                    .Include(i => i.INCOME_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(i => i.INCOME_PRODUCTS.Select(p => p.UNIT))
                    .Include(i => i.WRHS_INCOME_DOCS)
                    .FirstOrDefaultAsync(i => i.DocNo.Contains(expense.DocNo) && i.ReceivingWHID == expense.ReceiverWhID);

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

    // ViewModels
    public class ProductionToSalesTransferViewModel
    {
        public DateTime TransferDate { get; set; }
        public string TransferDocNo { get; set; }
        public int ProductionWHID { get; set; }
        public int SalesWHID { get; set; }
        public string Description { get; set; }
    }

    public class TransferLineViewModel
    {
        public int ProductID { get; set; }
        public double Quantity { get; set; }
        public int? IncomeID { get; set; }
        /// <summary>ProdID:123:7|ProdID:124:3</summary>
        public string LotAllocations { get; set; }
    }
}
