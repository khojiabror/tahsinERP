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
                    .Where(e => e.IsDeleted == false 
                        && e.DocNo.Contains("TRANSFER-"))
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
                ViewBag.Products = new SelectList(
                    await db.PRODUCTS
                        .Where(p => p.IsDeleted == false && p.IsFinishedGood == true)
                        .OrderBy(p => p.PNo)
                        .ToListAsync(),
                    "ID", "PNo");

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
            if (ModelState.IsValid && lines != null && lines.Any(l => l.Quantity > 0))
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        // Tekshirish stock availability uchun barcha products
                        foreach (var line in lines.Where(l => l.Quantity > 0))
                        {
                            var availableStock = WarehouseFlowService.GetProductionStock(line.ProductID, db);
                            if (availableStock < line.Quantity)
                            {
                                var product = db.PRODUCTS.Find(line.ProductID);
                                ModelState.AddModelError("", 
                                    $"Mahsulot {product?.PNo ?? line.ProductID.ToString()} uchun ishlab chiqarish omborida yetarli qoldiq yo'q. " +
                                    $"Mavjud: {availableStock}, Talab qilinmoqda: {line.Quantity}");
                                
                                // Reload ViewBag
                                var productionWH = WarehouseFlowService.GetProductionWarehouse(db);
                                var salesWH = WarehouseFlowService.GetSalesWarehouse(db);
                                ViewBag.ProductionWH = productionWH;
                                ViewBag.SalesWH = salesWH;
                                ViewBag.Products = new SelectList(
                                    db.PRODUCTS
                                        .Where(p => p.IsDeleted == false && p.IsFinishedGood == true)
                                        .OrderBy(p => p.PNo)
                                        .ToList(),
                                    "ID", "PNo");
                                return View(viewModel);
                            }
                        }

                        // O'qish hujjat agar provided
                        byte[] documentBytes = null;
                        if (transferDocument != null && transferDocument.ContentLength > 0)
                        {
                            documentBytes = new byte[transferDocument.ContentLength];
                            transferDocument.InputStream.Read(documentBytes, 0, transferDocument.ContentLength);
                        }

                        // Process har bir transfer line
                        foreach (var line in lines.Where(l => l.Quantity > 0))
                        {
                            WarehouseFlowService.TransferProductionToSales(
                                line.ProductID,
                                line.Quantity,
                                db,
                                User.Identity.Name,
                                viewModel.TransferDocNo,
                                documentBytes,
                                viewModel.Description
                            );
                        }

                        LogHelper.LogToDatabase(User.Identity.Name, "ProductionToSalesTransfer", "Create",
                            $"Ishlab chiqarishdan sotuv omboriga o'tkazma yaratildi. DocNo: {viewModel.TransferDocNo}");

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

            // Reload ViewBag on xatolik
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var productionWH = WarehouseFlowService.GetProductionWarehouse(db);
                var salesWH = WarehouseFlowService.GetSalesWarehouse(db);
                ViewBag.ProductionWH = productionWH;
                ViewBag.SalesWH = salesWH;
                ViewBag.Products = new SelectList(
                    db.PRODUCTS
                        .Where(p => p.IsDeleted == false && p.IsFinishedGood == true)
                        .OrderBy(p => p.PNo)
                        .ToList(),
                    "ID", "PNo");
            }

            return View(viewModel);
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

                // Olish corresponding INCOME hujjat
                var income = await db.INCOMES
                    .Include(i => i.INCOME_PRODUCTS)
                    .Include(i => i.WRHS_INCOME_DOCS)
                    .FirstOrDefaultAsync(i => i.DocNo.Contains(expense.DocNo) && i.ReceivingWHID == expense.ReceiverWhID);

                ViewBag.CorrespondingIncome = income;

                return View(expense);
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
    }
}
