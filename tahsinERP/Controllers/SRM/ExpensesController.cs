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

namespace tahsinERP.Controllers.SRM
{
    public class ExpensesController : Controller
    {
        // GET: Expenses
        public async Task<ActionResult> Index()
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Expenses", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var expenses = await db.EXPENSES
                    .Include(e => e.Warehouse)
                    .Include(e => e.Warehouse1)
                    .Include(e => e.CURRENCy)
                    .Where(e => e.IsDeleted != true)
                    .OrderByDescending(e => e.IssueDateTime)
                    .ToListAsync();

                return View(expenses);
            }
        }

        // GET: Expenses/Details/5
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

                return View(expense);
            }
        }

        // GET: Expenses/Create
        public async Task<ActionResult> Create()
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Expenses"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                ViewBag.SenderWarehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToListAsync(),
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

                return View();
            }
        }

        // POST: Expenses/GetAvailableProducts
        [HttpPost]
        public async Task<JsonResult> GetAvailableProducts(int warehouseId)
        {
            using (var db = new DBTHSNEntities())
            {
                var products = await db.PRODUCT_STOCKS
                    .Include(s => s.PRODUCT)
                    .Include(s => s.UNIT)
                    .Where(s => s.WHID == warehouseId && s.Amount > 0)
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

                return Json(new { success = true, data = products });
            }
        }

        // POST: Expenses/Create
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
                            // Validate stock availability
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

                            // Generate DocNo if not provided
                            if (string.IsNullOrEmpty(model.DocNo))
                            {
                                model.DocNo = await GenerateDocNo(db);
                            }

                            // Create expense header
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

                            // Add expense products and decrease stock
                            double totalAmount = 0;
                            double totalPrice = 0;

                            if (model.ExpenseProducts != null && model.ExpenseProducts.Any())
                            {
                                foreach (var product in model.ExpenseProducts)
                                {
                                    var expenseProduct = new EXPENSE_PRODUCTS
                                    {
                                        ExpenseID = expense.ID,
                                        ProductID = product.ProductID,
                                        Amount = product.Amount,
                                        PiecePrice = product.PiecePrice,
                                        TotalPrice = product.Amount * product.PiecePrice,
                                        UnitID = product.UnitID,
                                        Comment = product.Comment
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

                                    // If receiver warehouse specified, increase stock there
                                    if (expense.ReceiverWhID.HasValue)
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
                                                var newStock = new PRODUCT_STOCKS
                                                {
                                                    WHID = expense.ReceiverWhID.Value,
                                                    ProductID = product.ProductID,
                                                    Amount = product.Amount,
                                                    UnitID = product.UnitID
                                                };
                                                db.PRODUCT_STOCKS.Add(newStock);
                                            }
                                        }

                                        // Income will be created after all products are processed (see below)
                                    }
                                }

                                expense.Amount = totalAmount;
                                expense.TotalPrice = totalPrice;
                                await db.SaveChangesAsync();

                                // Auto-create linked Income document for internal transfer (Supply → Production/Sales WH)
                                // Create ONE Income document for the entire Expense (not per product)
                                if (expense.ReceiverWhID.HasValue)
                                {
                                    await CreateLinkedIncomeDocumentForExpenseAsync(expense, model.ExpenseProducts, db);
                                }
                            }

                            // Upload expense document if provided
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
            // Repopulate ViewBag on error
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

        // Generate DocNo
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

        // GET: Expenses/Edit/5
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
                    .Include(e => e.EXPENSE_PRODUCTS)
                    .FirstOrDefaultAsync(e => e.ID == id && e.IsDeleted != true);

                if (expense == null)
                {
                    return HttpNotFound();
                }

                // Check if already sent (SendStatus == true)
                if (expense.SendStatus == true)
                {
                    TempData["Error"] = "Bu chiqim hujjati allaqachon yuborilgan va tahrirlash mumkin emas.";
                    return RedirectToAction("Index");
                }

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
                        PiecePrice = ep.PiecePrice,
                        TotalPrice = ep.TotalPrice,
                        Comment = ep.Comment
                    }).ToList()
                };

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

                return View(model);
            }
        }

        // POST: Expenses/Edit/5
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

                                // Revert receiver warehouse stock if exists
                                if (expense.ReceiverWhID.HasValue)
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

                            // Validate new stock availability
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

                            // Update expense header
                            expense.DocNo = model.DocNo;
                            expense.IssueDateTime = model.IssueDateTime;
                            expense.SenderWHID = model.SenderWHID;
                            expense.ReceiverWhID = model.ReceiverWhID;
                            expense.CurrencyID = model.CurrencyID;
                            expense.Description = model.Description;

                            // Remove existing expense products
                            foreach (var existing in oldProducts)
                            {
                                db.EXPENSE_PRODUCTS.Remove(existing);
                            }

                            // Add updated expense products and update stock
                            double totalAmount = 0;
                            double totalPrice = 0;

                            if (model.ExpenseProducts != null && model.ExpenseProducts.Any())
                            {
                                foreach (var product in model.ExpenseProducts)
                                {
                                    var expenseProduct = new EXPENSE_PRODUCTS
                                    {
                                        ExpenseID = expense.ID,
                                        ProductID = product.ProductID,
                                        Amount = product.Amount,
                                        PiecePrice = product.PiecePrice,
                                        TotalPrice = product.Amount * product.PiecePrice,
                                        UnitID = product.UnitID,
                                        Comment = product.Comment
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

                                    // Increase receiver warehouse stock if specified
                                    if (expense.ReceiverWhID.HasValue)
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
                                                var newStock = new PRODUCT_STOCKS
                                                {
                                                    WHID = expense.ReceiverWhID.Value,
                                                    ProductID = product.ProductID,
                                                    Amount = product.Amount,
                                                    UnitID = product.UnitID
                                                };
                                                db.PRODUCT_STOCKS.Add(newStock);
                                            }
                                        }
                                    }
                                }
                            }

                            expense.Amount = totalAmount;
                            expense.TotalPrice = totalPrice;

                            // Handle file upload
                            if (expenseFile != null && expenseFile.ContentLength > 0)
                            {
                                // Remove existing docs
                                var existingDocs = db.WRHS_EXPENSE_DOCS.Where(ed => ed.ExpenseID == expense.ID).ToList();
                                foreach (var doc in existingDocs)
                                {
                                    db.WRHS_EXPENSE_DOCS.Remove(doc);
                                }

                                // Add new doc
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
            // Repopulate ViewBag on error
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

        // GET: Expenses/Delete/5
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

                return View(expense);
            }
        }

        // POST: Expenses/Delete/5
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

                                // Revert receiver warehouse stock if exists
                                if (expense.ReceiverWhID.HasValue)
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

        // GET: Expenses/DownloadDoc/5
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
        /// Auto-creates linked Income document for internal transfer (Supply → Production/Sales WH)
        /// Creates ONE Income document per Expense with all products
        /// </summary>
        private async Task CreateLinkedIncomeDocumentForExpenseAsync(EXPENS expense, List<ViewModels.ExpenseProductViewModel> products, DBTHSNEntities db)
        {
            // Check if receiver is Production warehouse or Sales warehouse
            var receiverWarehouse = await db.WAREHOUSES.FindAsync(expense.ReceiverWhID.Value);
            if (receiverWarehouse == null)
                return;

            bool isProductionWarehouse = receiverWarehouse.WHName.Contains("Production") || 
                                        receiverWarehouse.WHName.Contains("Ishlab chiqarish");
            bool isSalesWarehouse = receiverWarehouse.WHName.Contains("Sales") || 
                                   receiverWarehouse.WHName.Contains("Sotuv");

            if (!isProductionWarehouse && !isSalesWarehouse)
                return; // Only auto-link for Production or Sales warehouse transfers

            // Generate Income DocNo
            var incomeDocNo = await GenerateIncomeDocNo(db);

            // Calculate totals
            double totalAmount = products.Sum(p => p.Amount);
            double totalPrice = products.Sum(p => p.TotalPrice);

            // Create Income document
            var income = new INCOME
            {
                DocNo = incomeDocNo,
                IssueDateTime = expense.IssueDateTime,
                ReceivingWHID = expense.ReceiverWhID.Value,
                SenderWHID = expense.SenderWHID,
                CurrencyID = expense.CurrencyID,
                Amount = totalAmount,
                TotalPrice = totalPrice,
                Description = $"Auto-linked from Expense {expense.DocNo} - {expense.Description}",
                RecieveStatus = true, // Auto-received for internal transfers
                IsDeleted = false
            };

            db.INCOMES.Add(income);
            await db.SaveChangesAsync();

            // Create Income Products for all products in the Expense
            foreach (var product in products)
            {
                var incomeProduct = new INCOME_PRODUCTS
                {
                    IncomeID = income.ID,
                    ProductID = product.ProductID,
                    Amount = product.Amount,
                    PiecePrice = product.PiecePrice,
                    TotalPrice = product.TotalPrice,
                    UnitID = product.UnitID,
                    Comment = $"Linked from Expense {expense.DocNo}"
                };

                db.INCOME_PRODUCTS.Add(incomeProduct);
            }

            await db.SaveChangesAsync();

            LogHelper.LogToDatabase(User.Identity.Name, "Expenses", 
                $"Auto-created linked Income {income.DocNo} for Expense {expense.DocNo} with {products.Count} products");
        }

        /// <summary>
        /// Generates Income DocNo
        /// </summary>
        private async Task<string> GenerateIncomeDocNo(DBTHSNEntities db)
        {
            var month = DateTime.Now.Month;
            var prefix = $"INC-{month}_";

            var lastIncome = await db.INCOMES
                .Where(i => i.DocNo.StartsWith(prefix))
                .OrderByDescending(i => i.DocNo)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastIncome != null)
            {
                var lastNumberStr = lastIncome.DocNo.Substring(prefix.Length);
                if (int.TryParse(lastNumberStr, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber}";
        }
    }

}
