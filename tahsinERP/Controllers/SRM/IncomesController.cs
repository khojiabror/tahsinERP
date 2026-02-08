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
        public async Task<ActionResult> Index(int? status)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Incomes", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                var incomesQuery = db.INCOMES
                    .Include(i => i.Warehouse)
                    .Include(i => i.Warehouse1)
                    .Include(i => i.CURRENCy1)
                    .Include(i => i.P_INVOICES)
                    .Where(i => i.IsDeleted != true);

                if (assignedWarehouseId.HasValue)
                {
                    incomesQuery = incomesQuery.Where(i => i.ReceivingWHID == assignedWarehouseId.Value);
                }

                if (status.HasValue)
                {
                    if (status.Value == 1)
                    {
                        incomesQuery = incomesQuery.Where(i => i.RecieveStatus == true);
                    }
                    else if (status.Value == 0)
                    {
                        incomesQuery = incomesQuery.Where(i => i.RecieveStatus != true);
                    }
                }

                var incomes = await incomesQuery
                    .OrderByDescending(i => i.IssueDateTime)
                    .ToListAsync();

                ViewBag.Status = status;
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

                var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                if (assignedWarehouseId.HasValue && income.ReceivingWHID != assignedWarehouseId.Value)
                {
                    return RedirectToAction("Index", "ViewNotAuthorized");
                }

                var currentUserId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
                bool isMrpForWarehouse = await db.WAREHOUSES
                    .AnyAsync(w => w.ID == income.ReceivingWHID && w.MRP == currentUserId);
                ViewBag.CanMrpReceive = isMrpForWarehouse && income.RecieveStatus != true;

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
                var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                if (!assignedWarehouseId.HasValue)
                {
                    ViewBag.NoWarehouseError = "Sizga biriktirilgan ombor mavjud emas";
                    return View();
                }

                // Get invoices that are not fully received
                var invoices = await db.P_INVOICES
                    .Include(i => i.SUPPLIER)
                    .Where(i => i.IsDeleted != true)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync();

                var invoiceSelectItems = new List<SelectListItem>
                {
                    new SelectListItem
                    {
                        Value = "",
                        Text = "---naqdga olingan invoys yo'q---"
                    }
                };
                invoiceSelectItems.AddRange(invoices.Select(i => new SelectListItem
                {
                    Value = i.ID.ToString(),
                    Text = $"{i.InvoiceNo} - {i.SUPPLIER.Name} ({i.InvoiceDate:dd.MM.yyyy})"
                }));

                ViewBag.Invoices = new SelectList(invoiceSelectItems, "Value", "Text");

                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.ID == assignedWarehouseId.Value).OrderBy(w => w.WHName).ToListAsync(),
                    "ID", "WHName"
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname"
                );

                var products = await db.PRODUCTS
                    .Where(p => p.IsDeleted != true)
                    .OrderBy(p => p.PNo)
                    .ToListAsync();

                var productSelectList = products.Select(p => new SelectListItem
                {
                    Value = p.ID.ToString(),
                    Text = $"{p.PNo} - {p.Name ?? ""}"
                }).ToList();

                ViewBag.Products = new SelectList(productSelectList, "Value", "Text");
                ViewBag.ProductsData = products.Select(p => new ViewModels.ProductLookupItem
                {
                    Value = p.ID.ToString(),
                    PNo = p.PNo,
                    Name = p.Name ?? ""
                }).ToList();

                ViewBag.Units = new SelectList(
                    await db.UNITS.ToListAsync(),
                    "ID", "ShortName"
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
                            var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                            if (!assignedWarehouseId.HasValue)
                            {
                                TempData["Error"] = "Sizga biriktirilgan ombor mavjud emas";
                                return RedirectToAction("Index");
                            }

                            if (model.ReceivingWHID != assignedWarehouseId.Value)
                            {
                                TempData["Error"] = "Siz faqat biriktirilgan omborga kirim qilishingiz mumkin.";
                                return RedirectToAction("Index");
                            }

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
                                RecieveStatus = model.InvoiceID.HasValue,
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
                var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                var invoices = await db.P_INVOICES
                    .Include(i => i.SUPPLIER)
                    .Where(i => i.IsDeleted != true)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync();

                var invoiceSelectItems = new List<SelectListItem>
                {
                    new SelectListItem
                    {
                        Value = "",
                        Text = "---naqdga olingan invoys yo'q---"
                    }
                };
                invoiceSelectItems.AddRange(invoices.Select(i => new SelectListItem
                {
                    Value = i.ID.ToString(),
                    Text = $"{i.InvoiceNo} - {i.SUPPLIER.Name} ({i.InvoiceDate:dd.MM.yyyy})"
                }));

                ViewBag.Invoices = new SelectList(invoiceSelectItems, "Value", "Text", model.InvoiceID?.ToString());

                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES
                        .Where(w => assignedWarehouseId.HasValue ? w.ID == assignedWarehouseId.Value : w.IsDeleted != true)
                        .OrderBy(w => w.WHName)
                        .ToListAsync(),
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

                var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                if (!assignedWarehouseId.HasValue)
                {
                    TempData["Error"] = "Sizga biriktirilgan ombor mavjud emas";
                    return RedirectToAction("Index");
                }
                if (assignedWarehouseId.HasValue && income.ReceivingWHID != assignedWarehouseId.Value)
                {
                    return RedirectToAction("Index", "ViewNotAuthorized");
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

                var products = await db.PRODUCTS
                    .Where(p => p.IsDeleted != true)
                    .OrderBy(p => p.PNo)
                    .ToListAsync();

                var productSelectList = products.Select(p => new SelectListItem
                {
                    Value = p.ID.ToString(),
                    Text = $"{p.PNo} - {p.Name ?? ""}"
                }).ToList();

                ViewBag.Products = new SelectList(productSelectList, "Value", "Text");
                ViewBag.ProductsData = products.Select(p => new ViewModels.ProductLookupItem
                {
                    Value = p.ID.ToString(),
                    PNo = p.PNo,
                    Name = p.Name ?? ""
                }).ToList();

                ViewBag.Units = new SelectList(
                    await db.UNITS.ToListAsync(),
                    "ID", "ShortName"
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

            ModelState.Remove("InvoiceID");
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

                            var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                            if (!assignedWarehouseId.HasValue)
                            {
                                TempData["Error"] = "Sizga biriktirilgan ombor mavjud emas";
                                return RedirectToAction("Index");
                            }
                            if (assignedWarehouseId.HasValue && income.ReceivingWHID != assignedWarehouseId.Value)
                            {
                                return RedirectToAction("Index", "ViewNotAuthorized");
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

        // POST: Incomes/Receive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Receive(int id)
        {
            using (var db = new DBTHSNEntities())
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var income = await db.INCOMES
                            .Include(i => i.INCOME_PRODUCTS)
                            .FirstOrDefaultAsync(i => i.ID == id && i.IsDeleted != true);
                        if (income == null)
                        {
                            return HttpNotFound();
                        }

                        var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                        if (assignedWarehouseId.HasValue && income.ReceivingWHID != assignedWarehouseId.Value)
                        {
                            return RedirectToAction("Index", "ViewNotAuthorized");
                        }

                        var currentUserId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
                        bool isMrpForWarehouse = await db.WAREHOUSES
                            .AnyAsync(w => w.ID == income.ReceivingWHID && w.MRP == currentUserId);
                        if (!isMrpForWarehouse)
                        {
                            return RedirectToAction("Index", "ViewNotAuthorized");
                        }

                        if (income.RecieveStatus == true)
                        {
                            TempData["Error"] = "Bu kirim hujjati allaqachon qabul qilingan.";
                            return RedirectToAction("Details", new { id });
                        }

                        var isAutoLinkedTransfer = income.Description != null &&
                            income.Description.Contains("Chiqimdan avtomatik bog'langan:");

                        if (isAutoLinkedTransfer && income.INCOME_PRODUCTS != null)
                        {
                            foreach (var product in income.INCOME_PRODUCTS)
                            {
                                var stock = await db.PRODUCT_STOCKS
                                    .FirstOrDefaultAsync(s => s.WHID == income.ReceivingWHID && s.ProductID == product.ProductID);

                                if (stock != null)
                                {
                                    stock.Amount += product.Amount;
                                }
                                else
                                {
                                    db.PRODUCT_STOCKS.Add(new PRODUCT_STOCKS
                                    {
                                        WHID = income.ReceivingWHID,
                                        ProductID = product.ProductID,
                                        Amount = product.Amount,
                                        UnitID = product.UnitID
                                    });
                                }
                            }
                        }

                        income.RecieveStatus = true;
                        await db.SaveChangesAsync();
                        transaction.Commit();

                        LogHelper.LogToDatabase(User.Identity.Name, "Incomes",
                            $"Receive - {income.DocNo} kirim hujjati qabul qilindi");

                        return RedirectToAction("Details", new { id });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        TempData["Error"] = $"Xatolik: {ex.Message}";
                        return RedirectToAction("Details", new { id });
                    }
                }
            }
        }

        // POST: Incomes/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Reject(int id)
        {
            using (var db = new DBTHSNEntities())
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var income = await db.INCOMES
                            .Include(i => i.INCOME_PRODUCTS)
                            .Include(i => i.WRHS_INCOME_DOCS)
                            .FirstOrDefaultAsync(i => i.ID == id && i.IsDeleted != true);
                        if (income == null)
                        {
                            return HttpNotFound();
                        }

                        var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                        if (assignedWarehouseId.HasValue && income.ReceivingWHID != assignedWarehouseId.Value)
                        {
                            return RedirectToAction("Index", "ViewNotAuthorized");
                        }

                        var currentUserId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
                        bool isMrpForWarehouse = await db.WAREHOUSES
                            .AnyAsync(w => w.ID == income.ReceivingWHID && w.MRP == currentUserId);
                        if (!isMrpForWarehouse)
                        {
                            return RedirectToAction("Index", "ViewNotAuthorized");
                        }

                        if (income.RecieveStatus == true)
                        {
                            TempData["Error"] = "Bu kirim hujjati allaqachon qabul qilingan va rad etib bo'lmaydi.";
                            return RedirectToAction("Details", new { id });
                        }

                        var expenseDocNo = ExtractLinkedExpenseDocNo(income.Description);
                        if (string.IsNullOrWhiteSpace(expenseDocNo))
                        {
                            TempData["Error"] = "Bog'langan chiqim topilmadi. Rad etish mumkin emas.";
                            return RedirectToAction("Details", new { id });
                        }

                        var expense = await db.EXPENSES
                            .Include(e => e.EXPENSE_PRODUCTS)
                            .Include(e => e.WRHS_EXPENSE_DOCS)
                            .FirstOrDefaultAsync(e =>
                                e.DocNo == expenseDocNo &&
                                e.ReceiverWhID == income.ReceivingWHID &&
                                e.SenderWHID == income.SenderWHID &&
                                e.IsDeleted != true);

                        if (expense == null)
                        {
                            TempData["Error"] = "Bog'langan chiqim topilmadi. Rad etish mumkin emas.";
                            return RedirectToAction("Details", new { id });
                        }

                        // Revert stock changes made by expense transfer
                        foreach (var product in expense.EXPENSE_PRODUCTS)
                        {
                            var senderStock = await db.PRODUCT_STOCKS
                                .FirstOrDefaultAsync(s => s.WHID == expense.SenderWHID && s.ProductID == product.ProductID);
                            if (senderStock != null)
                            {
                                senderStock.Amount += product.Amount;
                            }
                            else
                            {
                                db.PRODUCT_STOCKS.Add(new PRODUCT_STOCKS
                                {
                                    WHID = expense.SenderWHID,
                                    ProductID = product.ProductID,
                                    Amount = product.Amount,
                                    UnitID = product.UnitID
                                });
                            }

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

                        if (income.WRHS_INCOME_DOCS != null && income.WRHS_INCOME_DOCS.Any())
                        {
                            db.WRHS_INCOME_DOCS.RemoveRange(income.WRHS_INCOME_DOCS.ToList());
                        }

                        if (income.INCOME_PRODUCTS != null && income.INCOME_PRODUCTS.Any())
                        {
                            db.INCOME_PRODUCTS.RemoveRange(income.INCOME_PRODUCTS.ToList());
                        }

                        if (expense.WRHS_EXPENSE_DOCS != null && expense.WRHS_EXPENSE_DOCS.Any())
                        {
                            db.WRHS_EXPENSE_DOCS.RemoveRange(expense.WRHS_EXPENSE_DOCS.ToList());
                        }

                        if (expense.EXPENSE_PRODUCTS != null && expense.EXPENSE_PRODUCTS.Any())
                        {
                            db.EXPENSE_PRODUCTS.RemoveRange(expense.EXPENSE_PRODUCTS.ToList());
                        }

                        db.INCOMES.Remove(income);
                        db.EXPENSES.Remove(expense);

                        await db.SaveChangesAsync();
                        transaction.Commit();

                        LogHelper.LogToDatabase(User.Identity.Name, "Incomes",
                            $"Reject - {income.DocNo} kirim hujjati rad etildi va bog'langan chiqim o'chirildi");

                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        TempData["Error"] = $"Xatolik: {ex.Message}";
                        return RedirectToAction("Details", new { id });
                    }
                }
            }
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

                var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                if (assignedWarehouseId.HasValue && income.ReceivingWHID != assignedWarehouseId.Value)
                {
                    return RedirectToAction("Index", "ViewNotAuthorized");
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
                            var assignedWarehouseId = await GetAssignedWarehouseIdAsync(db);
                            if (assignedWarehouseId.HasValue && income.ReceivingWHID != assignedWarehouseId.Value)
                            {
                                return RedirectToAction("Index", "ViewNotAuthorized");
                            }

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

        private string ExtractLinkedExpenseDocNo(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return null;
            }

            const string marker = "Chiqimdan avtomatik bog'langan: ";
            var index = description.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            var start = index + marker.Length;
            var rest = description.Substring(start);
            var end = rest.IndexOf(" - ", StringComparison.Ordinal);
            if (end < 0)
            {
                end = rest.Length;
            }

            var docNo = rest.Substring(0, end).Trim();
            return string.IsNullOrWhiteSpace(docNo) ? null : docNo;
        }

        private async Task<int?> GetAssignedWarehouseIdAsync(DBTHSNEntities db)
        {
            var currentUserId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
            var warehouse = await db.WAREHOUSES
                .FirstOrDefaultAsync(w => w.IsDeleted != true && w.MRP == currentUserId);
            return warehouse?.ID;
        }
    }

}
