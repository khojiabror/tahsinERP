using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Services;

namespace tahsinERP.Controllers.SRM
{
    public class ExpensesController : Controller
    {
        private const string LinkedExpenseIdMarker = "EXPENSE_ID:";

        // Olish so'rovi: Expenses
        // status: null/"" = barchasi, "pending" = Kutilmoqda, "sent" = Yuborilgan
        public async Task<ActionResult> Index(int? warehouseId, string status)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Expenses", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                var expensesQuery = db.EXPENSES
                    .Include(e => e.Warehouse)
                    .Include(e => e.Warehouse1)
                    .Include(e => e.CURRENCy)
                    .Where(e => e.IsDeleted != true);

                // Ombona bo'yicha filtr
                if (warehouseId.HasValue)
                {
                    expensesQuery = expensesQuery.Where(e => e.SenderWHID == warehouseId.Value);
                }

                // Status bo'yicha filtr (default: barchasi)
                if (status == "pending")
                {
                    expensesQuery = expensesQuery.Where(e => e.SendStatus != true);
                }
                else if (status == "sent")
                {
                    expensesQuery = expensesQuery.Where(e => e.SendStatus == true);
                }

                var expenses = await expensesQuery
                    .OrderByDescending(e => e.IssueDateTime)
                    .ToListAsync();

                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToListAsync(),
                    "ID", "WHName", warehouseId
                );
                ViewBag.WarehouseId = warehouseId;
                ViewBag.Status = status ?? "";
                ViewBag.AssignedWarehouseIds = assignedWarehouseIds;
                ViewBag.ChangePermit = tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses");
                return View(expenses);
            }
        }

        // Olish so'rovi: Expenses/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Expenses", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var expense = await db.EXPENSES
                    .Include(e => e.Warehouse)
                    .Include(e => e.Warehouse1)
                    .Include(e => e.CURRENCy)
                    .Include(e => e.EXPENSE_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(e => e.EXPENSE_PRODUCTS.Select(p => p.UNIT))
                    .Include(e => e.WRHS_EXPENSE_DOCS)
                    .FirstOrDefaultAsync(e => e.ID == id && e.IsDeleted != true);

                if (expense == null)
                {
                    return HttpNotFound();
                }

                // View barcha uchun ochiq; Edit/Delete faqat MRP uchun
                var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                ViewBag.CanEdit = tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses")
                    && assignedWarehouseId.HasValue
                    && expense.SenderWHID == assignedWarehouseId.Value
                    && expense.SendStatus != true;

                return View(expense);
            }
        }

        // Olish so'rovi: Expenses/yaratish
        public async Task<ActionResult> Create()
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                if (!assignedWarehouseId.HasValue)
                {
                    ViewBag.NoWarehouseError = "Sizga biriktirilgan ombor mavjud emas";
                    return View();
                }

                ViewBag.SenderWarehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.ID == assignedWarehouseId.Value).OrderBy(w => w.WHName).ToListAsync(),
                    "ID", "WHName"
                );

                ViewBag.ReceiverWarehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToListAsync(),
                    "ID", "WHName"
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname"
                );

                ViewBag.Units = new SelectList(
                    await db.UNITS.ToListAsync(),
                    "ID", "ShortName"
                );

                return View();
            }
        }

        // FIFO: Berilgan ombor+mahsulot uchun mavjud Income lotlar (FIFO tartibida)
        [HttpPost]
        public async Task<JsonResult> GetAvailableIncomeLots(int warehouseId, int productId)
        {
            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                    if (assignedWarehouseIds.Any() && !assignedWarehouseIds.Contains(warehouseId))
                        return Json(new { success = false, message = "Ushbu omborga kirish huquqi yo'q" });

                    var lots = await LotTrackingService.GetAvailableIncomeLotsAsync(warehouseId, productId, db);
                    var data = lots.Select(l => new
                    {
                        l.IncomeID,
                        l.DocNo,
                        l.IssueDateTime,
                        l.RemainingAmount,
                        l.DisplayText
                    }).ToList();
                    return Json(new { success = true, data });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST so'rovi: Expenses/GetAvailableProducts
        [HttpPost]
        public async Task<JsonResult> GetAvailableProducts(int warehouseId, int? receiverWarehouseId)
        {
            using (var db = new DBTHSNEntities())
            {
                var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                if (!assignedWarehouseId.HasValue)
                {
                    return Json(new { success = false, message = "Sizga biriktirilgan ombor mavjud emas" });
                }

                if (warehouseId != assignedWarehouseId.Value)
                {
                    return Json(new { success = false, message = "Siz faqat biriktirilgan ombor bo'yicha chiqim qila olasiz." });
                }

                var productsQuery = db.PRODUCT_STOCKS
                    .Include(s => s.PRODUCT)
                    .Include(s => s.UNIT)
                    .Where(s => s.WHID == warehouseId && s.Amount > 0)
                    .AsQueryable();

                var productionWH = WarehouseFlowService.GetProductionWarehouse(db);
                var salesWH = WarehouseFlowService.GetSalesWarehouse(db);

                var isProductionToSales = productionWH != null
                    && salesWH != null
                    && warehouseId == productionWH.ID
                    && receiverWarehouseId.HasValue
                    && receiverWarehouseId.Value == salesWH.ID;

                Dictionary<int, decimal> approvedRemaining = null;
                if (isProductionToSales)
                {
                    var approvedByProduct = await db.QUALITY_INSPECTION
                        .Where(q => q.StatusCode == "APPROVED" && q.IsDeleted == false)
                        .GroupBy(q => q.ProductID)
                        .Select(g => new { ProductID = g.Key, Qty = g.Sum(x => (decimal?)x.QtyAccepted) ?? 0 })
                        .ToDictionaryAsync(x => x.ProductID, x => x.Qty);

                    var transferredByProduct = await db.EXPENSE_PRODUCTS
                        .Where(ep => ep.EXPENS.SenderWHID == productionWH.ID
                            && ep.EXPENS.ReceiverWhID == salesWH.ID
                            && ep.EXPENS.IsDeleted == false)
                        .GroupBy(ep => ep.ProductID)
                        .Select(g => new { ProductID = g.Key, Qty = g.Sum(x => (decimal?)x.Amount) ?? 0 })
                        .ToDictionaryAsync(x => x.ProductID, x => x.Qty);

                    approvedRemaining = approvedByProduct
                        .ToDictionary(k => k.Key, k => k.Value - (transferredByProduct.ContainsKey(k.Key) ? transferredByProduct[k.Key] : 0));
                }

                var products = await productsQuery
                    .Select(s => new
                    {
                        ProductID = s.ProductID,
                        ProductPNo = s.PRODUCT.PNo,
                        ProductName = s.PRODUCT.Name,
                        AvailableStock = s.Amount,
                        UnitID = s.UnitID,
                        UnitName = s.UNIT.ShortName
                    })
                    .ToListAsync();

                if (isProductionToSales && approvedRemaining != null)
                {
                    products = products
                        .Select(p => new
                        {
                            p.ProductID,
                            p.ProductPNo,
                            p.ProductName,
                            AvailableStock = Math.Max(0, Math.Min((double)(approvedRemaining.ContainsKey(p.ProductID) ? approvedRemaining[p.ProductID] : 0), p.AvailableStock)),
                            p.UnitID,
                            p.UnitName
                        })
                        .Where(p => p.AvailableStock > 0)
                        .ToList();
                }

                return Json(new { success = true, data = products });
            }
        }

        // POST so'rovi: Expenses/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(ViewModels.ExpenseCreateViewModel model, HttpPostedFileBase expenseFile)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    using (var transaction = db.Database.BeginTransaction())
                    {
                        try
                        {
                            var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                            if (!assignedWarehouseId.HasValue)
                            {
                                TempData["Error"] = "Sizga biriktirilgan ombor mavjud emas";
                                return RedirectToAction("Index");
                            }

                            if (model.SenderWHID != assignedWarehouseId.Value)
                            {
                                TempData["Error"] = "Siz faqat biriktirilgan ombordan chiqim qilishingiz mumkin.";
                                return RedirectToAction("Index");
                            }

                            if (model.ExpenseProducts == null)
                            {
                                model.ExpenseProducts = new List<ViewModels.ExpenseProductViewModel>();
                            }

                            // Tekshirish stock availability
                            foreach (var product in model.ExpenseProducts)
                            {
                                var stock = await db.PRODUCT_STOCKS
                                    .FirstOrDefaultAsync(s => s.WHID == model.SenderWHID && s.ProductID == product.ProductID);

                                if (stock == null || stock.Amount < product.Amount)
                                {
                                    ModelState.AddModelError("", $"Mahsulot {product.ProductPNo} uchun yetarli qoldiq yo'q. Mavjud: {(stock?.Amount ?? 0)}, Talab: {product.Amount}");
                                    transaction.Rollback();
                                    goto RepopulateViewBag;
                                }
                            }

                            var productionWH = WarehouseFlowService.GetProductionWarehouse(db);
                            var salesWH = WarehouseFlowService.GetSalesWarehouse(db);

                            var isProductionToSales = productionWH != null
                                && salesWH != null
                                && model.SenderWHID == productionWH.ID
                                && model.ReceiverWhID.HasValue
                                && model.ReceiverWhID.Value == salesWH.ID;

                            if (isProductionToSales)
                            {
                                var approvedByProduct = await db.QUALITY_INSPECTION
                                    .Where(q => q.StatusCode == "APPROVED" && q.IsDeleted == false)
                                    .GroupBy(q => q.ProductID)
                                    .Select(g => new { ProductID = g.Key, Qty = g.Sum(x => (decimal?)x.QtyAccepted) ?? 0 })
                                    .ToDictionaryAsync(x => x.ProductID, x => x.Qty);

                                var transferredByProduct = await db.EXPENSE_PRODUCTS
                                    .Where(ep => ep.EXPENS.SenderWHID == productionWH.ID
                                        && ep.EXPENS.ReceiverWhID == salesWH.ID
                                        && ep.EXPENS.IsDeleted == false)
                                    .GroupBy(ep => ep.ProductID)
                                    .Select(g => new { ProductID = g.Key, Qty = g.Sum(x => (decimal?)x.Amount) ?? 0 })
                                    .ToDictionaryAsync(x => x.ProductID, x => x.Qty);

                                foreach (var product in model.ExpenseProducts)
                                {
                                    var approved = approvedByProduct.ContainsKey(product.ProductID)
                                        ? approvedByProduct[product.ProductID]
                                        : 0;
                                    var transferred = transferredByProduct.ContainsKey(product.ProductID)
                                        ? transferredByProduct[product.ProductID]
                                        : 0;
                                    var remaining = approved - transferred;

                                    if ((decimal)product.Amount > remaining)
                                    {
                                        ModelState.AddModelError("",
                                            $"Mahsulot {product.ProductPNo} uchun sifat nazorati tasdiqlangan miqdor yetarli emas. Qoldiq: {remaining}, Talab: {product.Amount}");
                                        transaction.Rollback();
                                        goto RepopulateViewBag;
                                    }
                                }
                            }

                            // Yaratish DocNo agar not provided
                            if (string.IsNullOrEmpty(model.DocNo))
                            {
                                model.DocNo = await GenerateDocNo(db);
                            }

                            // Yaratish expense header
                            var expense = new EXPENS
                            {
                                DocNo = model.DocNo,
                                IssueDateTime = model.IssueDateTime,
                                SenderWHID = model.SenderWHID,
                                ReceiverWhID = model.ReceiverWhID,
                                CurrencyID = model.CurrencyID,
                                Amount = 0,
                                TotalPrice = 0,
                                Description = model.Description,
                                SendStatus = false,
                                IsDeleted = false
                            };

                            db.EXPENSES.Add(expense);
                            await db.SaveChangesAsync();

                            // Qo'shish expense products va decrease stock
                            double totalAmount = 0;
                            double totalPrice = 0;

                            if (model.ExpenseProducts != null && model.ExpenseProducts.Any())
                            {
                                foreach (var product in model.ExpenseProducts)
                                {
                                    var comment = product.IncomeID.HasValue
                                        ? LotTrackingService.FormatIncomeIdComment(product.IncomeID.Value)
                                        : (product.Comment ?? "");
                                    var expenseProduct = new EXPENSE_PRODUCTS
                                    {
                                        ExpenseID = expense.ID,
                                        ProductID = product.ProductID,
                                        Amount = product.Amount,
                                        PiecePrice = product.PiecePrice,
                                        TotalPrice = product.Amount * product.PiecePrice,
                                        UnitID = product.UnitID,
                                        Comment = comment
                                    };

                                    db.EXPENSE_PRODUCTS.Add(expenseProduct);
                                    totalAmount += product.Amount;
                                    totalPrice += expenseProduct.TotalPrice;

                                    // Decrease PRODUCT_STOCKS
                                    var stock = await db.PRODUCT_STOCKS
                                        .FirstOrDefaultAsync(s => s.WHID == expense.SenderWHID && s.ProductID == product.ProductID);

                                    if (stock != null)
                                    {
                                        stock.Amount -= product.Amount;
                                        if (stock.Amount < 0) stock.Amount = 0;
                                    }

                                    // agar receiver warehouse specified va immediate receive is allowed, increase stock there
                                    if (expense.ReceiverWhID.HasValue && ShouldUpdateReceiverStockImmediately(db, expense.ReceiverWhID))
                                    {
                                        var receiverStock = await db.PRODUCT_STOCKS
                                            .FirstOrDefaultAsync(s => s.WHID == expense.ReceiverWhID.Value && s.ProductID == product.ProductID);

                                        if (receiverStock != null)
                                        {
                                            receiverStock.Amount += product.Amount;
                                        }
                                        else
                                        {
                                            var productEntity = await db.PRODUCTS.FindAsync(product.ProductID);
                                            if (productEntity != null)
                                            {
                                                var ensuredReceiverStock = WarehouseFlowService.GetOrCreateStockRow(db, expense.ReceiverWhID.Value, product.ProductID, product.UnitID);
                                                ensuredReceiverStock.Amount += product.Amount;
                                            }
                                        }

                                        // Income will be created after barcha products are processed (see below)
                                    }
                                }

                            expense.Amount = totalAmount;
                            expense.TotalPrice = totalPrice;
                            await db.SaveChangesAsync();

                            // Ichki o'tkazma bo'lsa linked income hujjatini majburiy upsert qilamiz.
                            if (expense.ReceiverWhID.HasValue && expense.ReceiverWhID.Value != expense.SenderWHID)
                            {
                                await CreateLinkedIncomeDocumentForExpenseAsync(expense, model.ExpenseProducts, db);
                            }
                            }

                            // Upload expense hujjat agar provided
                            if (expenseFile != null && expenseFile.ContentLength > 0)
                            {
                                byte[] fileData = null;
                                using (var binaryReader = new BinaryReader(expenseFile.InputStream))
                                {
                                    fileData = binaryReader.ReadBytes(expenseFile.ContentLength);
                                }

                                var expenseDoc = new WRHS_EXPENSE_DOCS
                                {
                                    ExpenseID = expense.ID,
                                    Doc = fileData
                                };

                                db.WRHS_EXPENSE_DOCS.Add(expenseDoc);
                                await db.SaveChangesAsync();
                            }

                            transaction.Commit();

                            // Log the action
                            LogHelper.LogToDatabase(User.Identity.Name, "Expenses", $"Create - {expense.DocNo} chiqim hujjati yaratildi");

                            return RedirectToAction("Index");
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
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
            }

            RepopulateViewBag:
            // Qayta ga'ldirish ViewBag on xatolik
            using (var db = new DBTHSNEntities())
            {
                var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                ViewBag.SenderWarehouses = new SelectList(
                    await db.WAREHOUSES
                        .Where(w => assignedWarehouseId.HasValue ? w.ID == assignedWarehouseId.Value : w.IsDeleted != true)
                        .OrderBy(w => w.WHName)
                        .ToListAsync(),
                    "ID", "WHName", model.SenderWHID
                );

                ViewBag.ReceiverWarehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToListAsync(),
                    "ID", "WHName", model.ReceiverWhID
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname", model.CurrencyID
                );
            }

            return View(model);
        }

        // Yaratish DocNo
        private async Task<string> GenerateDocNo(DBTHSNEntities db)
        {
            var month = DateTime.Now.Month;
            var prefix = $"{month}_";

            var lastExpense = await db.EXPENSES
                .Where(e => e.DocNo.StartsWith(prefix))
                .OrderByDescending(e => e.DocNo)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastExpense != null)
            {
                var lastNumberStr = lastExpense.DocNo.Substring(prefix.Length);
                if (int.TryParse(lastNumberStr, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber}";
        }

        private bool ShouldUpdateReceiverStockImmediately(DBTHSNEntities db, int? receiverWhId)
        {
            _ = db;
            if (!receiverWhId.HasValue)
            {
                return false;
            }
            // Ichki o‘tkazmalar pending income orqali qabul qilinadi.
            // Shu sabab receiver qoldig'ini chiqim paytida darhol oshirmaymiz
            // (aks holda Receive vaqtida ikki marta qo'shilib qoladi).
            return false;
        }

        private async Task<int?> GetAssignedWarehouseIdAsync(DBTHSNEntities db)
        {
            var ids = await GetAssignedWarehouseIdsAsync(db);
            return ids.FirstOrDefault();
        }

        private async Task<List<int>> GetAssignedWarehouseIdsAsync(DBTHSNEntities db)
        {
            var currentUserId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
            return await db.WAREHOUSES
                .Where(w => w.IsDeleted != true && w.MRP == currentUserId)
                .Select(w => w.ID)
                .ToListAsync();
        }

        // Olish so'rovi: Expenses/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var expense = await db.EXPENSES
                    .Include(e => e.EXPENSE_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(e => e.EXPENSE_PRODUCTS.Select(p => p.UNIT))
                    .FirstOrDefaultAsync(e => e.ID == id && e.IsDeleted != true);

                if (expense == null)
                {
                    return HttpNotFound();
                }

                var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                if (assignedWarehouseId.HasValue && expense.SenderWHID != assignedWarehouseId.Value)
                {
                    return RedirectToAction("Index", "ViewNotAuthorized");
                }

                // Tekshirish agar already sent (SendStatus == true)
                if (expense.SendStatus == true)
                {
                    TempData["Error"] = "Bu chiqim hujjati allaqachon yuborilgan va tahrirlash mumkin emas.";
                    return RedirectToAction("Index");
                }

                var productIds = expense.EXPENSE_PRODUCTS.Select(ep => ep.ProductID).ToList();
                var stockMap = await db.PRODUCT_STOCKS
                    .Where(s => s.WHID == expense.SenderWHID && productIds.Contains(s.ProductID))
                    .ToListAsync();

                var model = new ViewModels.ExpenseEditViewModel
                {
                    ID = expense.ID,
                    DocNo = expense.DocNo,
                    IssueDateTime = expense.IssueDateTime,
                    SenderWHID = expense.SenderWHID,
                    ReceiverWhID = expense.ReceiverWhID,
                    CurrencyID = expense.CurrencyID,
                    Amount = expense.Amount,
                    TotalPrice = expense.TotalPrice,
                    Description = expense.Description,
                    ExpenseProducts = expense.EXPENSE_PRODUCTS.Select(ep => new ViewModels.ExpenseProductViewModel
                    {
                        ID = ep.ID,
                        ProductID = ep.ProductID,
                        ProductPNo = ep.PRODUCT.PNo,
                        ProductName = ep.PRODUCT.Name,
                        Amount = ep.Amount,
                        UnitID = ep.UnitID,
                        UnitName = ep.UNIT != null ? ep.UNIT.ShortName : null,
                        PiecePrice = ep.PiecePrice,
                        TotalPrice = ep.TotalPrice,
                        Comment = LotTrackingService.ParseIncomeIdFromComment(ep.Comment).HasValue ? "" : (ep.Comment ?? ""),
                        IncomeID = LotTrackingService.ParseIncomeIdFromComment(ep.Comment),
                        AvailableStock = stockMap.FirstOrDefault(s => s.ProductID == ep.ProductID)?.Amount ?? 0
                    }).ToList()
                };

                ViewBag.SenderWarehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.ID == expense.SenderWHID).OrderBy(w => w.WHName).ToListAsync(),
                    "ID", "WHName", model.SenderWHID
                );

                ViewBag.ReceiverWarehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToListAsync(),
                    "ID", "WHName", model.ReceiverWhID
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname", model.CurrencyID
                );

                ViewBag.Units = new SelectList(
                    await db.UNITS.ToListAsync(),
                    "ID", "ShortName"
                );

                return View(model);
            }
        }

        // POST so'rovi: Expenses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(ViewModels.ExpenseEditViewModel model, HttpPostedFileBase expenseFile)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    using (var transaction = db.Database.BeginTransaction())
                    {
                        try
                        {
                            var expense = await db.EXPENSES.FindAsync(model.ID);
                            if (expense == null || expense.IsDeleted == true)
                            {
                                return HttpNotFound();
                            }

                            var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                            if (assignedWarehouseId.HasValue && expense.SenderWHID != assignedWarehouseId.Value)
                            {
                                return RedirectToAction("Index", "ViewNotAuthorized");
                            }

                            if (expense.SendStatus == true)
                            {
                                TempData["Error"] = "Bu chiqim hujjati allaqachon yuborilgan va tahrirlash mumkin emas.";
                                return RedirectToAction("Index");
                            }

                            // Revert old stock changes
                            var oldProducts = await db.EXPENSE_PRODUCTS
                                .Where(ep => ep.ExpenseID == expense.ID)
                                .ToListAsync();

                            foreach (var oldProduct in oldProducts)
                            {
                                // Revert sender warehouse stock
                                var senderStock = await db.PRODUCT_STOCKS
                                    .FirstOrDefaultAsync(s => s.WHID == expense.SenderWHID && s.ProductID == oldProduct.ProductID);

                                if (senderStock != null)
                                {
                                    senderStock.Amount += oldProduct.Amount;
                                }

                                // Revert receiver warehouse stock agar mavjud
                                if (expense.ReceiverWhID.HasValue && ShouldUpdateReceiverStockImmediately(db, expense.ReceiverWhID))
                                {
                                    var receiverStock = await db.PRODUCT_STOCKS
                                        .FirstOrDefaultAsync(s => s.WHID == expense.ReceiverWhID.Value && s.ProductID == oldProduct.ProductID);

                                    if (receiverStock != null)
                                    {
                                        receiverStock.Amount -= oldProduct.Amount;
                                        if (receiverStock.Amount < 0) receiverStock.Amount = 0;
                                    }
                                }
                            }

                            // Tekshirish new stock availability
                            if (model.ExpenseProducts == null)
                            {
                                model.ExpenseProducts = new List<ViewModels.ExpenseProductViewModel>();
                            }

                            foreach (var product in model.ExpenseProducts)
                            {
                                var stock = await db.PRODUCT_STOCKS
                                    .FirstOrDefaultAsync(s => s.WHID == model.SenderWHID && s.ProductID == product.ProductID);

                                if (stock == null || stock.Amount < product.Amount)
                                {
                                    ModelState.AddModelError("", $"Mahsulot {product.ProductPNo} uchun yetarli qoldiq yo'q. Mavjud: {(stock?.Amount ?? 0)}, Talab: {product.Amount}");
                                    transaction.Rollback();
                                    goto RepopulateViewBag;
                                }
                            }

                            // Yangilash expense header
                            expense.DocNo = model.DocNo;
                            expense.IssueDateTime = model.IssueDateTime;
                            expense.SenderWHID = model.SenderWHID;
                            expense.ReceiverWhID = model.ReceiverWhID;
                            expense.CurrencyID = model.CurrencyID;
                            expense.Description = model.Description;

                            // O'chirish existing expense products
                            foreach (var existing in oldProducts)
                            {
                                db.EXPENSE_PRODUCTS.Remove(existing);
                            }

                            // Qo'shish updated expense products va yangilash stock
                            double totalAmount = 0;
                            double totalPrice = 0;

                            if (model.ExpenseProducts != null && model.ExpenseProducts.Any())
                            {
                                foreach (var product in model.ExpenseProducts)
                                {
                                    var comment = product.IncomeID.HasValue
                                        ? LotTrackingService.FormatIncomeIdComment(product.IncomeID.Value)
                                        : (product.Comment ?? "");
                                    var expenseProduct = new EXPENSE_PRODUCTS
                                    {
                                        ExpenseID = expense.ID,
                                        ProductID = product.ProductID,
                                        Amount = product.Amount,
                                        PiecePrice = product.PiecePrice,
                                        TotalPrice = product.Amount * product.PiecePrice,
                                        UnitID = product.UnitID,
                                        Comment = comment
                                    };

                                    db.EXPENSE_PRODUCTS.Add(expenseProduct);
                                    totalAmount += product.Amount;
                                    totalPrice += expenseProduct.TotalPrice;

                                    // Decrease sender warehouse stock
                                    var senderStock = await db.PRODUCT_STOCKS
                                        .FirstOrDefaultAsync(s => s.WHID == expense.SenderWHID && s.ProductID == product.ProductID);

                                    if (senderStock != null)
                                    {
                                        senderStock.Amount -= product.Amount;
                                        if (senderStock.Amount < 0) senderStock.Amount = 0;
                                    }

                                    // Increase receiver warehouse stock agar specified
                                    if (expense.ReceiverWhID.HasValue && ShouldUpdateReceiverStockImmediately(db, expense.ReceiverWhID))
                                    {
                                        var receiverStock = await db.PRODUCT_STOCKS
                                            .FirstOrDefaultAsync(s => s.WHID == expense.ReceiverWhID.Value && s.ProductID == product.ProductID);

                                        if (receiverStock != null)
                                        {
                                            receiverStock.Amount += product.Amount;
                                        }
                                        else
                                        {
                                            var productEntity = await db.PRODUCTS.FindAsync(product.ProductID);
                                            if (productEntity != null)
                                            {
                                                var ensuredReceiverStock = WarehouseFlowService.GetOrCreateStockRow(db, expense.ReceiverWhID.Value, product.ProductID, product.UnitID);
                                                ensuredReceiverStock.Amount += product.Amount;
                                            }
                                        }
                                    }
                                }
                            }

                            expense.Amount = totalAmount;
                            expense.TotalPrice = totalPrice;

                            // Handle fayl upload
                            if (expenseFile != null && expenseFile.ContentLength > 0)
                            {
                                // O'chirish existing docs
                                var existingDocs = db.WRHS_EXPENSE_DOCS.Where(ed => ed.ExpenseID == expense.ID).ToList();
                                foreach (var doc in existingDocs)
                                {
                                    db.WRHS_EXPENSE_DOCS.Remove(doc);
                                }

                                // Qo'shish new doc
                                byte[] fileData = null;
                                using (var binaryReader = new BinaryReader(expenseFile.InputStream))
                                {
                                    fileData = binaryReader.ReadBytes(expenseFile.ContentLength);
                                }

                                var expenseDoc = new WRHS_EXPENSE_DOCS
                                {
                                    ExpenseID = expense.ID,
                                    Doc = fileData
                                };

                                db.WRHS_EXPENSE_DOCS.Add(expenseDoc);
                            }

                            await db.SaveChangesAsync();

                            // Editda ham linked income hujjatini yangilab/yaratib turamiz.
                            if (expense.ReceiverWhID.HasValue && expense.ReceiverWhID.Value != expense.SenderWHID)
                            {
                                await CreateLinkedIncomeDocumentForExpenseAsync(expense, model.ExpenseProducts, db);
                            }
                            transaction.Commit();

                            // Log the action
                            LogHelper.LogToDatabase(User.Identity.Name, "Expenses", $"Edit - {expense.DocNo} chiqim hujjati tahrirlandi");

                            return RedirectToAction("Index");
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
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
            }

            RepopulateViewBag:
            // Qayta ga'ldirish ViewBag on xatolik
            using (var db = new DBTHSNEntities())
            {
                ViewBag.SenderWarehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToListAsync(),
                    "ID", "WHName", model.SenderWHID
                );

                ViewBag.ReceiverWarehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToListAsync(),
                    "ID", "WHName", model.ReceiverWhID
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname", model.CurrencyID
                );
            }

            return View(model);
        }

        // Olish so'rovi: Expenses/o'chirish/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var expense = await db.EXPENSES
                    .Include(e => e.Warehouse)
                    .Include(e => e.Warehouse1)
                    .Include(e => e.CURRENCy)
                    .Include(e => e.EXPENSE_PRODUCTS.Select(p => p.PRODUCT))
                    .FirstOrDefaultAsync(e => e.ID == id && e.IsDeleted != true);

                if (expense == null)
                {
                    return HttpNotFound();
                }

                var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                if (assignedWarehouseId.HasValue && expense.SenderWHID != assignedWarehouseId.Value)
                {
                    return RedirectToAction("Index", "ViewNotAuthorized");
                }

                return View(expense);
            }
        }

        // POST so'rovi: Expenses/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var expense = await db.EXPENSES.FindAsync(id);
                        if (expense != null)
                        {
                            var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                            if (assignedWarehouseId.HasValue && expense.SenderWHID != assignedWarehouseId.Value)
                            {
                                return RedirectToAction("Index", "ViewNotAuthorized");
                            }

                            // Revert stock changes
                            var expenseProducts = await db.EXPENSE_PRODUCTS
                                .Where(ep => ep.ExpenseID == expense.ID)
                                .ToListAsync();

                            foreach (var product in expenseProducts)
                            {
                                // Revert sender warehouse stock
                                var senderStock = await db.PRODUCT_STOCKS
                                    .FirstOrDefaultAsync(s => s.WHID == expense.SenderWHID && s.ProductID == product.ProductID);

                                if (senderStock != null)
                                {
                                    senderStock.Amount += product.Amount;
                                }

                                // Revert receiver warehouse stock agar mavjud
                                if (expense.ReceiverWhID.HasValue && ShouldUpdateReceiverStockImmediately(db, expense.ReceiverWhID))
                                {
                                    var receiverStock = await db.PRODUCT_STOCKS
                                        .FirstOrDefaultAsync(s => s.WHID == expense.ReceiverWhID.Value && s.ProductID == product.ProductID);

                                    if (receiverStock != null)
                                    {
                                        receiverStock.Amount -= product.Amount;
                                        if (receiverStock.Amount < 0) receiverStock.Amount = 0;
                                    }
                                }
                            }

                            expense.IsDeleted = true;
                            await db.SaveChangesAsync();
                            transaction.Commit();

                            // Log the action
                            LogHelper.LogToDatabase(User.Identity.Name, "Expenses", $"Delete - {expense.DocNo} chiqim hujjati o'chirildi");
                        }

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        TempData["Error"] = $"Xatolik: {ex.Message}";
                        return RedirectToAction("Index");
                    }
                }
            }
        }

        // Olish so'rovi: Expenses/DownloadDoc/5
        public async Task<ActionResult> DownloadDoc(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.WRHS_EXPENSE_DOCS.FindAsync(id);
                if (doc == null || doc.Doc == null)
                {
                    return HttpNotFound();
                }

                return File(doc.Doc, "application/pdf", $"Expense_{doc.ExpenseID}.pdf");
            }
        }

        /// <summary>
        /// Kutilmoqdagi chiqimlar uchun yetishmayotgan kirim hujjatlarini yaratadi (mavjud ma'lumotlarni tuzatish)
        /// </summary>
        public async Task<ActionResult> RepairMissingLinkedIncomes()
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses"))
                return RedirectToAction("Index", "ViewNotAuthorized");

            int created = 0;
            int updated = 0;
            int skipped = 0;
            var errors = new List<string>();
            using (var db = new DBTHSNEntities())
            {
                var pendingExpenses = await db.EXPENSES
                    .Include(e => e.EXPENSE_PRODUCTS)
                    .Where(e => e.IsDeleted != true
                        && e.ReceiverWhID.HasValue
                        && e.ReceiverWhID.Value != e.SenderWHID)
                    .ToListAsync();

                foreach (var exp in pendingExpenses)
                {
                    try
                    {
                        var products = exp.EXPENSE_PRODUCTS.Select(ep => new ViewModels.ExpenseProductViewModel
                        {
                            ProductID = ep.ProductID,
                            Amount = ep.Amount,
                            PiecePrice = ep.PiecePrice,
                            TotalPrice = ep.TotalPrice,
                            UnitID = ep.UnitID
                        }).ToList();

                        if (!products.Any())
                        {
                            skipped++;
                            continue;
                        }

                        var marker = LinkedExpenseIdMarker + exp.ID;
                        var existedBefore = await db.INCOMES.AnyAsync(i =>
                            i.IsDeleted != true
                            && i.Description != null
                            && i.Description.Contains(marker));

                        await CreateLinkedIncomeDocumentForExpenseAsync(exp, products, db);
                        if (existedBefore) updated++;
                        else created++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{exp.DocNo}: {ex.Message}");
                    }
                }
            }

            if (errors.Any())
            {
                TempData["Error"] =
                    $"Tuzatish qisman bajarildi. Yaratildi: {created}, yangilandi: {updated}, o'tkazib yuborildi: {skipped}, xatolar: {errors.Count}. " +
                    string.Join(" | ", errors.Take(3));
            }
            else
            {
                TempData["Success"] =
                    $"Tuzatish tugadi. Yaratildi: {created}, yangilandi: {updated}, o'tkazib yuborildi: {skipped}.";
            }
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Ichki o'tkazma uchun avtomatik kirim hujjati yaratadi (Xomashyo → Ishlab chiqarish/Sotuv)
        /// Har bir chiqim uchun barcha mahsulotlar bilan BIRTA kirim hujjati yaratadi
        /// </summary>
        private async Task CreateLinkedIncomeDocumentForExpenseAsync(EXPENS expense, List<ViewModels.ExpenseProductViewModel> products, DBTHSNEntities db)
        {
            if (!expense.ReceiverWhID.HasValue || products == null || !products.Any())
                return;

            // Har qanday ichki o'tkazma (sender != receiver) uchun pending kirim yaratiladi.
            // Bu ombor nomlari/lookup o'zgarib ketganda ham barqaror ishlaydi.
            if (expense.ReceiverWhID.Value == expense.SenderWHID)
                return;

            var marker = LinkedExpenseIdMarker + expense.ID;
            var linkedIncome = await db.INCOMES
                .Include(i => i.INCOME_PRODUCTS)
                .FirstOrDefaultAsync(i =>
                    i.IsDeleted != true
                    && i.Description != null
                    && i.Description.Contains(marker));

            if (linkedIncome == null)
            {
                var incomeDocNo = await GenerateIncomeDocNo(db);
                linkedIncome = new INCOME
                {
                    DocNo = incomeDocNo,
                    IssueDateTime = expense.IssueDateTime,
                    ReceivingWHID = expense.ReceiverWhID.Value,
                    SenderWHID = expense.SenderWHID,
                    CurrencyID = expense.CurrencyID,
                    Description = $"Chiqimdan avtomatik bog'langan: {expense.DocNo} - {expense.Description} | {marker}",
                    RecieveStatus = false, // MRP qabul qilishi uchun pending
                    IsDeleted = false
                };
                db.INCOMES.Add(linkedIncome);
                await db.SaveChangesAsync();
            }
            else
            {
                linkedIncome.IssueDateTime = expense.IssueDateTime;
                linkedIncome.ReceivingWHID = expense.ReceiverWhID.Value;
                linkedIncome.SenderWHID = expense.SenderWHID;
                linkedIncome.CurrencyID = expense.CurrencyID;
                linkedIncome.Description = $"Chiqimdan avtomatik bog'langan: {expense.DocNo} - {expense.Description} | {marker}";
                if (linkedIncome.RecieveStatus != true)
                {
                    linkedIncome.RecieveStatus = false;
                }

                if (linkedIncome.INCOME_PRODUCTS != null && linkedIncome.INCOME_PRODUCTS.Any())
                {
                    db.INCOME_PRODUCTS.RemoveRange(linkedIncome.INCOME_PRODUCTS.ToList());
                }
            }

            double totalAmount = 0;
            double totalPrice = 0;
            foreach (var product in products)
            {
                var lineTotal = product.TotalPrice != 0
                    ? product.TotalPrice
                    : (product.Amount * product.PiecePrice);
                var incomeProduct = new INCOME_PRODUCTS
                {
                    IncomeID = linkedIncome.ID,
                    ProductID = product.ProductID,
                    Amount = product.Amount,
                    PiecePrice = product.PiecePrice,
                    TotalPrice = lineTotal,
                    UnitID = product.UnitID,
                    Comment = $"Chiqimdan bog'langan: {expense.DocNo}"
                };

                db.INCOME_PRODUCTS.Add(incomeProduct);
                totalAmount += product.Amount;
                totalPrice += lineTotal;
            }

            linkedIncome.Amount = totalAmount;
            linkedIncome.TotalPrice = totalPrice;
            await db.SaveChangesAsync();

            LogHelper.LogToDatabase(User.Identity.Name, "Expenses",
                $"Chiqimdan avtomatik kirim upsert qilindi: {linkedIncome.DocNo}. Chiqim: {expense.DocNo}, Mahsulotlar: {products.Count} ta");
        }

        /// <summary>
        /// Generates Income DocNo
        /// </summary>
        private async Task<string> GenerateIncomeDocNo(DBTHSNEntities db)
        {
            var month = DateTime.Now.Month;
            var prefix = $"INC-{month}_";

            var numbers = await db.INCOMES
                .Where(i => i.DocNo != null && i.DocNo.StartsWith(prefix))
                .Select(i => i.DocNo.Substring(prefix.Length))
                .ToListAsync();

            int maxNumber = 0;
            foreach (var numberStr in numbers)
            {
                if (int.TryParse(numberStr, out int number) && number > maxNumber)
                {
                    maxNumber = number;
                }
            }

            return $"{prefix}{maxNumber + 1}";
        }
    }

}
