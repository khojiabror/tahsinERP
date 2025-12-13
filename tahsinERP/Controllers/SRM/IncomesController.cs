using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers.SRM
{
    public class IncomesController : Controller
    {
        // GET: Incomes
        public async Task<ActionResult> Index()
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Incomes", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var incomes = await db.INCOMES
                    .Include(i => i.Warehouse)
                    .Include(i => i.Warehouse1)
                    .Include(i => i.CURRENCy1)
                    .Include(i => i.P_INVOICES)
                    .Where(i => i.IsDeleted != true)
                    .OrderByDescending(i => i.IssueDateTime)
                    .ToListAsync();

                return View(incomes);
            }
        }

        // GET: Incomes/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Incomes", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var income = await db.INCOMES
                    .Include(i => i.Warehouse)
                    .Include(i => i.Warehouse1)
                    .Include(i => i.CURRENCy1)
                    .Include(i => i.P_INVOICES)
                    .Include(i => i.INCOME_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(i => i.INCOME_PRODUCTS.Select(p => p.UNIT))
                    .Include(i => i.WRHS_INCOME_DOCS)
                    .FirstOrDefaultAsync(i => i.ID == id && i.IsDeleted != true);

                if (income == null)
                {
                    return HttpNotFound();
                }

                return View(income);
            }
        }

        // GET: Incomes/Create
        public async Task<ActionResult> Create()
        {
            // MES va Sales xodimlari faqat ko'radi, Create qila olmaydi
            var userRoles = tahsinERP.Models.RoleHelper.GetUserRoles(User.Identity.Name);
            bool isMES = userRoles.Any(r => r.Contains("MES") || r.Contains("Production"));
            bool isSales = userRoles.Any(r => r.Contains("Sales") || r.Contains("CRM"));
            
            if (isMES || isSales)
            {
                TempData["Error"] = "Siz faqat kirimlarni ko'rishingiz mumkin. Yaratish va tahrirlash huquqi yo'q.";
                return RedirectToAction("Index");
            }
            
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Incomes"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                // Get invoices that are not fully received
                var invoices = await db.P_INVOICES
                    .Include(i => i.SUPPLIER)
                    .Where(i => i.IsDeleted != true)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync();

                ViewBag.Invoices = new SelectList(
                    invoices.Select(i => new { 
                        ID = i.ID, 
                        DisplayText = $"{i.InvoiceNo} - {i.SUPPLIER.Name} ({i.InvoiceDate:dd.MM.yyyy})" 
                    }),
                    "ID", "DisplayText"
                );

                ViewBag.Warehouses = new SelectList(
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

        // POST: Incomes/LoadInvoiceProducts
        [HttpPost]
        public async Task<JsonResult> LoadInvoiceProducts(int invoiceId)
        {
            using (var db = new DBTHSNEntities())
            {
                var invoiceProducts = await db.P_INVOICE_PRODUCTS
                    .Include(ip => ip.PRODUCT)
                    .Include(ip => ip.UNIT)
                    .Where(ip => ip.InvoiceID == invoiceId)
                    .Select(ip => new
                    {
                        ProductID = ip.ProductID,
                        ProductPNo = ip.PRODUCT.PNo,
                        ProductName = ip.PRODUCT.Name,
                        Amount = ip.Quantity,
                        UnitID = ip.UnitID,
                        PiecePrice = ip.Price,
                        TotalPrice = ip.TotalPrice
                    })
                    .ToListAsync();

                return Json(new { success = true, data = invoiceProducts });
            }
        }

        // POST: Incomes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(ViewModels.IncomeCreateViewModel model, HttpPostedFileBase incomeFile)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Incomes"))
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
                            // Generate DocNo if not provided
                            if (string.IsNullOrEmpty(model.DocNo))
                            {
                                model.DocNo = await GenerateDocNo(db);
                            }

                            // Create income header
                            var income = new INCOME
                            {
                                DocNo = model.DocNo,
                                IssueDateTime = model.IssueDateTime,
                                ReceivingWHID = model.ReceivingWHID,
                                SenderWHID = model.SenderWHID,
                                InvoiceID = model.InvoiceID,
                                CurrencyID = model.CurrencyID,
                                Amount = 0,
                                TotalPrice = 0,
                                Description = model.Description,
                                RecieveStatus = false,
                                IsDeleted = false
                            };

                            db.INCOMES.Add(income);
                            await db.SaveChangesAsync();

                            // Add income products and update stock
                            var conversionService = new UnitConversionService(db);
                            double totalAmount = 0;
                            double totalPrice = 0;

                            if (model.IncomeProducts != null && model.IncomeProducts.Any())
                            {
                                foreach (var product in model.IncomeProducts)
                                {
                                    var productEntity = await db.PRODUCTS
                                        .Include(p => p.PRODUCT_CATEGORIES)
                                        .FirstOrDefaultAsync(p => p.ID == product.ProductID);
                                    
                                    // Check if product requires cutting
                                    bool requiresCutting = await conversionService.RequiresCuttingAsync(product.ProductID);
                                    
                                    // For requires_cutting products, use PIECES (UnitID = 1)
                                    // For other products, use user-selected unit or product's default unit
                                    int unitId;
                                    if (requiresCutting)
                                    {
                                        unitId = 1; // Piece
                                    }
                                    else
                                    {
                                        unitId = product.UnitID ?? (productEntity?.UnitID ?? 1);
                                    }

                                    var incomeProduct = new INCOME_PRODUCTS
                                    {
                                        IncomeID = income.ID,
                                        ProductID = product.ProductID,
                                        Amount = product.Amount, // In PIECES for cutting products, or user-selected unit for others
                                        PiecePrice = product.PiecePrice,
                                        TotalPrice = product.Amount * product.PiecePrice,
                                        UnitID = unitId,
                                        Comment = product.Comment
                                    };

                                    db.INCOME_PRODUCTS.Add(incomeProduct);
                                    totalAmount += product.Amount;
                                    totalPrice += incomeProduct.TotalPrice.Value;

                                    // Update PRODUCT_STOCKS in the same unit
                                    var stock = await db.PRODUCT_STOCKS
                                        .FirstOrDefaultAsync(s => s.WHID == income.ReceivingWHID && s.ProductID == product.ProductID);

                                    if (stock != null)
                                    {
                                        // If stock exists in different unit, we might need conversion
                                        // For now, assume same unit and add
                                        if (stock.UnitID == unitId)
                                        {
                                            stock.Amount += product.Amount;
                                        }
                                        else
                                        {
                                            // Units don't match - this is a data integrity issue
                                            // For now, we'll still add but log a warning
                                            // In production, you might want to handle this differently
                                            stock.Amount += product.Amount;
                                        }
                                    }
                                    else
                                    {
                                        if (productEntity != null)
                                        {
                                            var newStock = new PRODUCT_STOCKS
                                            {
                                                WHID = income.ReceivingWHID,
                                                ProductID = product.ProductID,
                                                Amount = product.Amount, // In PIECES for cutting products
                                                UnitID = unitId
                                            };
                                            db.PRODUCT_STOCKS.Add(newStock);
                                        }
                                    }
                                }

                                income.Amount = totalAmount;
                                income.TotalPrice = totalPrice;
                                await db.SaveChangesAsync();
                            }

                            // Upload income document if provided
                            if (incomeFile != null && incomeFile.ContentLength > 0)
                            {
                                byte[] fileData = null;
                                using (var binaryReader = new BinaryReader(incomeFile.InputStream))
                                {
                                    fileData = binaryReader.ReadBytes(incomeFile.ContentLength);
                                }

                                var incomeDoc = new WRHS_INCOME_DOCS
                                {
                                    IncomeID = income.ID,
                                    Doc = fileData
                                };

                                db.WRHS_INCOME_DOCS.Add(incomeDoc);
                                await db.SaveChangesAsync();
                            }

                            transaction.Commit();

                            // Log the action
                            LogHelper.LogToDatabase(User.Identity.Name, "Incomes", $"Create - {income.DocNo} kirim hujjati yaratildi");

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

            // Repopulate ViewBag on error
            using (var db = new DBTHSNEntities())
            {
                var invoices = await db.P_INVOICES
                    .Include(i => i.SUPPLIER)
                    .Where(i => i.IsDeleted != true)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync();

                ViewBag.Invoices = new SelectList(
                    invoices.Select(i => new { 
                        ID = i.ID, 
                        DisplayText = $"{i.InvoiceNo} - {i.SUPPLIER.Name} ({i.InvoiceDate:dd.MM.yyyy})" 
                    }),
                    "ID", "DisplayText", model.InvoiceID
                );

                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToListAsync(),
                    "ID", "WHName", model.ReceivingWHID
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

        // GET: Incomes/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            // MES va Sales xodimlari faqat ko'radi, Edit qila olmaydi
            var userRoles = tahsinERP.Models.RoleHelper.GetUserRoles(User.Identity.Name);
            bool isMES = userRoles.Any(r => r.Contains("MES") || r.Contains("Production"));
            bool isSales = userRoles.Any(r => r.Contains("Sales") || r.Contains("CRM"));
            
            if (isMES || isSales)
            {
                TempData["Error"] = "Siz faqat kirimlarni ko'rishingiz mumkin. Tahrirlash huquqi yo'q.";
                return RedirectToAction("Index");
            }
            
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Incomes"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var income = await db.INCOMES
                    .Include(i => i.INCOME_PRODUCTS)
                    .FirstOrDefaultAsync(i => i.ID == id && i.IsDeleted != true);

                if (income == null)
                {
                    return HttpNotFound();
                }

                // Check if already received (RecieveStatus == true)
                if (income.RecieveStatus == true)
                {
                    TempData["Error"] = "Bu kirim hujjati allaqachon qabul qilingan va tahrirlash mumkin emas.";
                    return RedirectToAction("Index");
                }

                var model = new ViewModels.IncomeEditViewModel
                {
                    ID = income.ID,
                    DocNo = income.DocNo,
                    IssueDateTime = income.IssueDateTime,
                    ReceivingWHID = income.ReceivingWHID,
                    SenderWHID = income.SenderWHID,
                    InvoiceID = income.InvoiceID,
                    CurrencyID = income.CurrencyID,
                    Amount = income.Amount,
                    TotalPrice = income.TotalPrice,
                    Description = income.Description,
                    IncomeProducts = income.INCOME_PRODUCTS.Select(ip => new ViewModels.IncomeProductViewModel
                    {
                        ID = ip.ID,
                        ProductID = ip.ProductID,
                        ProductPNo = ip.PRODUCT.PNo,
                        ProductName = ip.PRODUCT.Name,
                        Amount = ip.Amount,
                        UnitID = ip.UnitID,
                        PiecePrice = ip.PiecePrice,
                        TotalPrice = ip.TotalPrice,
                        Comment = ip.Comment
                    }).ToList()
                };

                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToListAsync(),
                    "ID", "WHName", model.ReceivingWHID
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname", model.CurrencyID
                );

                return View(model);
            }
        }

        // POST: Incomes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(ViewModels.IncomeEditViewModel model, HttpPostedFileBase incomeFile)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Incomes"))
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
                            var income = await db.INCOMES.FindAsync(model.ID);
                            if (income == null || income.IsDeleted == true)
                            {
                                return HttpNotFound();
                            }

                            if (income.RecieveStatus == true)
                            {
                                TempData["Error"] = "Bu kirim hujjati allaqachon qabul qilingan va tahrirlash mumkin emas.";
                                return RedirectToAction("Index");
                            }

                            // Revert old stock changes
                            var oldProducts = await db.INCOME_PRODUCTS
                                .Where(ip => ip.IncomeID == income.ID)
                                .ToListAsync();

                            foreach (var oldProduct in oldProducts)
                            {
                                var stock = await db.PRODUCT_STOCKS
                                    .FirstOrDefaultAsync(s => s.WHID == income.ReceivingWHID && s.ProductID == oldProduct.ProductID);

                                if (stock != null)
                                {
                                    stock.Amount -= oldProduct.Amount;
                                    if (stock.Amount < 0) stock.Amount = 0;
                                }
                            }

                            // Update income header
                            income.DocNo = model.DocNo;
                            income.IssueDateTime = model.IssueDateTime;
                            income.ReceivingWHID = model.ReceivingWHID;
                            income.SenderWHID = model.SenderWHID;
                            income.CurrencyID = model.CurrencyID;
                            income.Description = model.Description;

                            // Remove existing income products
                            foreach (var existing in oldProducts)
                            {
                                db.INCOME_PRODUCTS.Remove(existing);
                            }

                            // Add updated income products and update stock
                            double totalAmount = 0;
                            double totalPrice = 0;

                            if (model.IncomeProducts != null && model.IncomeProducts.Any())
                            {
                                foreach (var product in model.IncomeProducts)
                                {
                                    var productEntity = await db.PRODUCTS.FindAsync(product.ProductID);
                                    var unitId = product.UnitID ?? (productEntity != null ? productEntity.UnitID : 1);
                                    var incomeProduct = new INCOME_PRODUCTS
                                    {
                                        IncomeID = income.ID,
                                        ProductID = product.ProductID,
                                        Amount = product.Amount,
                                        PiecePrice = product.PiecePrice,
                                        TotalPrice = product.Amount * product.PiecePrice,
                                        UnitID = unitId,
                                        Comment = product.Comment
                                    };

                                    db.INCOME_PRODUCTS.Add(incomeProduct);
                                    totalAmount += product.Amount;
                                    totalPrice += incomeProduct.TotalPrice.Value;

                                    // Update PRODUCT_STOCKS
                                    var stock = await db.PRODUCT_STOCKS
                                        .FirstOrDefaultAsync(s => s.WHID == income.ReceivingWHID && s.ProductID == product.ProductID);

                                    if (stock != null)
                                    {
                                        stock.Amount += product.Amount;
                                    }
                                    else
                                    {
                                        if (productEntity != null)
                                        {
                                            var newStock = new PRODUCT_STOCKS
                                            {
                                                WHID = income.ReceivingWHID,
                                                ProductID = product.ProductID,
                                                Amount = product.Amount,
                                                UnitID = unitId
                                            };
                                            db.PRODUCT_STOCKS.Add(newStock);
                                        }
                                    }
                                }
                            }

                            income.Amount = totalAmount;
                            income.TotalPrice = totalPrice;

                            // Handle file upload
                            if (incomeFile != null && incomeFile.ContentLength > 0)
                            {
                                // Remove existing docs
                                var existingDocs = db.WRHS_INCOME_DOCS.Where(id => id.IncomeID == income.ID).ToList();
                                foreach (var doc in existingDocs)
                                {
                                    db.WRHS_INCOME_DOCS.Remove(doc);
                                }

                                // Add new doc
                                byte[] fileData = null;
                                using (var binaryReader = new BinaryReader(incomeFile.InputStream))
                                {
                                    fileData = binaryReader.ReadBytes(incomeFile.ContentLength);
                                }

                                var incomeDoc = new WRHS_INCOME_DOCS
                                {
                                    IncomeID = income.ID,
                                    Doc = fileData
                                };

                                db.WRHS_INCOME_DOCS.Add(incomeDoc);
                            }

                            await db.SaveChangesAsync();
                            transaction.Commit();

                            // Log the action
                            LogHelper.LogToDatabase(User.Identity.Name, "Incomes", $"Edit - {income.DocNo} kirim hujjati tahrirlandi");

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

            // Repopulate ViewBag on error
            using (var db = new DBTHSNEntities())
            {
                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToListAsync(),
                    "ID", "WHName", model.ReceivingWHID
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname", model.CurrencyID
                );
            }

            return View(model);
        }

        // GET: Incomes/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Incomes"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var income = await db.INCOMES
                    .Include(i => i.Warehouse)
                    .Include(i => i.CURRENCy1)
                    .Include(i => i.P_INVOICES)
                    .Include(i => i.INCOME_PRODUCTS.Select(p => p.PRODUCT))
                    .FirstOrDefaultAsync(i => i.ID == id && i.IsDeleted != true);

                if (income == null)
                {
                    return HttpNotFound();
                }

                return View(income);
            }
        }

        // POST: Incomes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Incomes"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var income = await db.INCOMES.FindAsync(id);
                        if (income != null)
                        {
                            // Revert stock changes
                            var incomeProducts = await db.INCOME_PRODUCTS
                                .Where(ip => ip.IncomeID == income.ID)
                                .ToListAsync();

                            foreach (var product in incomeProducts)
                            {
                                var stock = await db.PRODUCT_STOCKS
                                    .FirstOrDefaultAsync(s => s.WHID == income.ReceivingWHID && s.ProductID == product.ProductID);

                                if (stock != null)
                                {
                                    stock.Amount -= product.Amount;
                                    if (stock.Amount < 0) stock.Amount = 0;
                                }
                            }

                            income.IsDeleted = true;
                            await db.SaveChangesAsync();
                            transaction.Commit();

                            // Log the action
                            LogHelper.LogToDatabase(User.Identity.Name, "Incomes", $"Delete - {income.DocNo} kirim hujjati o'chirildi");
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

        // GET: Incomes/DownloadDoc/5
        public async Task<ActionResult> DownloadDoc(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.WRHS_INCOME_DOCS.FindAsync(id);
                if (doc == null || doc.Doc == null)
                {
                    return HttpNotFound();
                }

                return File(doc.Doc, "application/pdf", $"Income_{doc.IncomeID}.pdf");
            }
        }
    }

}
