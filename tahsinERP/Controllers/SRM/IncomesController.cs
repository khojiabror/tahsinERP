using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        private static readonly Regex IncomeLineVatInComment = new Regex(
            @"\s*\|\s*QQS\s*%:\s*[\d.,]+\s*%|\s*QQS\s*%:\s*[\d.,]+\s*%",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Olish so'rovi: Incomes
        public async Task<ActionResult> Index(int? status, int? warehouseId)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "Incomes", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                var incomesQuery = db.INCOMES
                    .Include(i => i.Warehouse)
                    .Include(i => i.Warehouse1)
                    .Include(i => i.CURRENCy1)
                    .Include(i => i.P_INVOICES)
                    .Where(i => i.IsDeleted != true);

                // View barcha uchun ochiq - faqat filtr bo'yicha (ombor, status tanlansa)
                if (warehouseId.HasValue)
                {
                    incomesQuery = incomesQuery.Where(i => i.ReceivingWHID == warehouseId.Value);
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

                ViewBag.Warehouses = new SelectList(
                    await db.WAREHOUSES.Where(w => w.IsDeleted != true).OrderBy(w => w.WHName).ToListAsync(),
                    "ID", "WHName", warehouseId
                );
                ViewBag.Status = status;
                ViewBag.WarehouseId = warehouseId;
                ViewBag.AssignedWarehouseIds = assignedWarehouseIds;
                var userRoles = tahsinERP.Models.RoleHelper.GetUserRoles(User.Identity.Name);
                bool isMES = userRoles != null && userRoles.Any(r => r.Contains("MES") || r.Contains("Production"));
                bool isSales = userRoles != null && userRoles.Any(r => r.Contains("Sales") || r.Contains("CRM"));
                ViewBag.ChangePermit = !isMES && !isSales && tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Incomes");
                return View(incomes);
            }
        }

        // Olish so'rovi: Incomes/Details/5
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

                // View barcha uchun ochiq; Edit/Delete/Receive faqat MRP uchun
                // Bir foydalanuvchi bir nechta omborga MRP bo'lishi mumkin,
                // shuning uchun bitta emas, barcha biriktirilgan omborlarni tekshiramiz.
                var assignedWarehouseIds = await GetAssignedWarehouseIdsAsync(db);
                var userRoles = tahsinERP.Models.RoleHelper.GetUserRoles(User.Identity.Name);
                bool isMES = userRoles.Any(r => r.Contains("MES") || r.Contains("Production"));
                bool isSales = userRoles.Any(r => r.Contains("Sales") || r.Contains("CRM"));
                bool isMrpForWarehouse = assignedWarehouseIds.Contains(income.ReceivingWHID);
                var isTransferIncome = ((income.DocNo ?? "").StartsWith("TRNSF_", StringComparison.OrdinalIgnoreCase) && income.SenderWHID.HasValue)
                    || (!string.IsNullOrEmpty(income.Description) && income.Description.Contains("Chiqimdan avtomatik bog'langan:"));

                ViewBag.CanMrpReceive = isMrpForWarehouse && income.RecieveStatus != true;
                ViewBag.CanRepairTransferStock = isMrpForWarehouse && income.RecieveStatus == true && isTransferIncome
                    && (income.Description == null || !income.Description.Contains("| STOCK_REPAIRED"));
                ViewBag.CanEdit = !isMES && !isSales
                    && tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "Incomes")
                    && isMrpForWarehouse
                    && income.RecieveStatus != true;

                // Chiqim hujjatiga link (avtomatik yaratilgan kirimlar uchun)
                if (!string.IsNullOrEmpty(income.Description) && income.Description.Contains("Chiqimdan avtomatik bog'langan:"))
                {
                    const string marker = "Chiqimdan avtomatik bog'langan: ";
                    var afterMarker = income.Description.Substring(income.Description.IndexOf(marker) + marker.Length);
                    var docNoEnd = afterMarker.IndexOf(" - ");
                    var expenseDocNo = docNoEnd >= 0 ? afterMarker.Substring(0, docNoEnd).Trim() : afterMarker.Trim();
                    var linkedExpense = await db.EXPENSES.FirstOrDefaultAsync(e => e.DocNo == expenseDocNo && e.IsDeleted != true);
                    if (linkedExpense != null)
                    {
                        ViewBag.LinkedExpenseId = linkedExpense.ID;
                        ViewBag.LinkedExpenseDocNo = linkedExpense.DocNo;
                    }
                }

                return View(income);
            }
        }

        // Olish so'rovi: Incomes/yaratish
        public async Task<ActionResult> Create()
        {
            // MES va Sales xodimlari faqat ko'radi, yaratish qila olmaydi
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

                // Olish invoices that are not fully received
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

        // POST so'rovi: Incomes/LoadInvoiceProducts
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
                        TotalPrice = ip.TotalPrice,
                        VATRate = 0d
                    })
                    .ToListAsync();

                return Json(new { success = true, data = invoiceProducts });
            }
        }

        // POST so'rovi: Incomes/yaratish
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

                            // Yaratish DocNo agar not provided
                            if (string.IsNullOrEmpty(model.DocNo))
                            {
                                model.DocNo = await GenerateDocNo(db);
                            }

                            // Yaratish income header
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

                            // Qo'shish income products va yangilash stock
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
                                    
                                    // Tekshirish agar product requires cutting
                                    bool requiresCutting = await conversionService.RequiresCuttingAsync(product.ProductID);
                                    
                                    // uchun requires_cutting products, use PIECES (UnitID = 1)
                                    // uchun other products, use user-selected unit yoki product's standart unit
                                    int unitId;
                                    if (requiresCutting)
                                    {
                                        unitId = 1; // Piece
                                    }
                                    else
                                    {
                                        unitId = product.UnitID ?? (productEntity?.UnitID ?? 1);
                                    }

                                    var vat = Math.Max(0, Math.Min(100, product.VATRate));
                                    var baseLine = product.Amount * (product.PiecePrice ?? 0);
                                    var lineTotal = baseLine * (1.0 + vat / 100.0);
                                    var mergedComment = MergeIncomeCommentWithVat(product.Comment, vat);

                                    var incomeProduct = new INCOME_PRODUCTS
                                    {
                                        IncomeID = income.ID,
                                        ProductID = product.ProductID,
                                        Amount = product.Amount, // In PIECES uchun cutting products, yoki user-selected unit uchun others
                                        PiecePrice = product.PiecePrice,
                                        TotalPrice = lineTotal,
                                        UnitID = unitId,
                                        Comment = mergedComment
                                    };

                                    db.INCOME_PRODUCTS.Add(incomeProduct);
                                    totalAmount += product.Amount;
                                    totalPrice += incomeProduct.TotalPrice.Value;

                                    // Yangilash PRODUCT_STOCKS in the same unit
                                    var stock = await db.PRODUCT_STOCKS
                                        .FirstOrDefaultAsync(s => s.WHID == income.ReceivingWHID && s.ProductID == product.ProductID);

                                    if (stock != null)
                                    {
                                        // agar stock mavjud in different unit, we might need conversion
                                        // uchun now, assume same unit va add
                                        if (stock.UnitID == unitId)
                                        {
                                            stock.Amount += product.Amount;
                                        }
                                        else
                                        {
                                            // Units don't match - this is a ma'lumot integrity issue
                                            // uchun now, we'll still add but log a warning
                                            // In production, you might want ga handle this differently
                                            stock.Amount += product.Amount;
                                        }
                                    }
                                    else if (productEntity != null)
                                    {
                                        var ensuredStock = WarehouseFlowService.GetOrCreateStockRow(db, income.ReceivingWHID, product.ProductID, unitId);
                                        ensuredStock.Amount += product.Amount; // In PIECES uchun cutting products
                                    }
                                }

                                income.Amount = totalAmount;
                                income.TotalPrice = totalPrice;
                                await db.SaveChangesAsync();
                            }

                            // Upload income hujjat agar provided
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

            // Qayta ga'ldirish ViewBag on xatolik
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

        // Yaratish DocNo
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

        // Olish so'rovi: Incomes/Edit/5
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

                // Tekshirish agar already received (RecieveStatus == true)
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
                        Comment = ip.Comment,
                        VATRate = GetVatRateForIncomeLine(ip.TotalPrice, ip.Amount, ip.PiecePrice, ip.Comment)
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

        // POST so'rovi: Incomes/Edit/5
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

                            // Yangilash income header
                            income.DocNo = model.DocNo;
                            income.IssueDateTime = model.IssueDateTime;
                            income.ReceivingWHID = model.ReceivingWHID;
                            income.SenderWHID = model.SenderWHID;
                            income.CurrencyID = model.CurrencyID;
                            income.Description = model.Description;

                            // O'chirish existing income products
                            foreach (var existing in oldProducts)
                            {
                                db.INCOME_PRODUCTS.Remove(existing);
                            }

                            // Qo'shish updated income products va yangilash stock
                            double totalAmount = 0;
                            double totalPrice = 0;

                            if (model.IncomeProducts != null && model.IncomeProducts.Any())
                            {
                                foreach (var product in model.IncomeProducts)
                                {
                                    var productEntity = await db.PRODUCTS.FindAsync(product.ProductID);
                                    var unitId = product.UnitID ?? (productEntity != null ? productEntity.UnitID : 1);
                                    var vat = Math.Max(0, Math.Min(100, product.VATRate));
                                    var baseLine = product.Amount * (product.PiecePrice ?? 0);
                                    var lineTotal = baseLine * (1.0 + vat / 100.0);
                                    var mergedComment = MergeIncomeCommentWithVat(product.Comment, vat);

                                    var incomeProduct = new INCOME_PRODUCTS
                                    {
                                        IncomeID = income.ID,
                                        ProductID = product.ProductID,
                                        Amount = product.Amount,
                                        PiecePrice = product.PiecePrice,
                                        TotalPrice = lineTotal,
                                        UnitID = unitId,
                                        Comment = mergedComment
                                    };

                                    db.INCOME_PRODUCTS.Add(incomeProduct);
                                    totalAmount += product.Amount;
                                    totalPrice += incomeProduct.TotalPrice.Value;

                                    // Yangilash PRODUCT_STOCKS
                                    var stock = await db.PRODUCT_STOCKS
                                        .FirstOrDefaultAsync(s => s.WHID == income.ReceivingWHID && s.ProductID == product.ProductID);

                                    if (stock != null)
                                    {
                                        stock.Amount += product.Amount;
                                    }
                                    else if (productEntity != null)
                                    {
                                        var ensuredStock = WarehouseFlowService.GetOrCreateStockRow(db, income.ReceivingWHID, product.ProductID, unitId);
                                        ensuredStock.Amount += product.Amount;
                                    }
                                }
                            }

                            income.Amount = totalAmount;
                            income.TotalPrice = totalPrice;

                            // Handle fayl upload
                            if (incomeFile != null && incomeFile.ContentLength > 0)
                            {
                                // O'chirish existing docs
                                var existingDocs = db.WRHS_INCOME_DOCS.Where(id => id.IncomeID == income.ID).ToList();
                                foreach (var doc in existingDocs)
                                {
                                    db.WRHS_INCOME_DOCS.Remove(doc);
                                }

                                // Qo'shish new doc
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

            // Qayta ga'ldirish ViewBag on xatolik
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

        // POST so'rovi: Incomes/Receive/5
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

                        var isAutoLinkedTransfer =
                            // Current canonical marker
                            (income.Description != null && income.Description.Contains("Chiqimdan avtomatik bog'langan:"))
                            // Backward compatibility for old transfer descriptions
                            || ((income.DocNo ?? "").StartsWith("TRNSF_", StringComparison.OrdinalIgnoreCase) && income.SenderWHID.HasValue);

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
                                    var ensuredStock = WarehouseFlowService.GetOrCreateStockRow(db, income.ReceivingWHID, product.ProductID, product.UnitID);
                                    ensuredStock.Amount += product.Amount;
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

        // POST so'rovi: Incomes/RepairTransferStock/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RepairTransferStock(int id)
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

                        var currentUserId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
                        bool isMrpForWarehouse = await db.WAREHOUSES
                            .AnyAsync(w => w.ID == income.ReceivingWHID && w.MRP == currentUserId);
                        if (!isMrpForWarehouse)
                        {
                            return RedirectToAction("Index", "ViewNotAuthorized");
                        }

                        var isTransferIncome = ((income.DocNo ?? "").StartsWith("TRNSF_", StringComparison.OrdinalIgnoreCase) && income.SenderWHID.HasValue)
                            || (!string.IsNullOrEmpty(income.Description) && income.Description.Contains("Chiqimdan avtomatik bog'langan:"));
                        if (!isTransferIncome || income.RecieveStatus != true)
                        {
                            TempData["Error"] = "Bu hujjat uchun repair qo'llab bo'lmaydi.";
                            return RedirectToAction("Details", new { id });
                        }

                        if (!string.IsNullOrEmpty(income.Description) && income.Description.Contains("| STOCK_REPAIRED"))
                        {
                            TempData["Error"] = "Repair allaqachon bajarilgan.";
                            return RedirectToAction("Details", new { id });
                        }

                        if (income.INCOME_PRODUCTS != null)
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
                                    var ensuredStock = WarehouseFlowService.GetOrCreateStockRow(db, income.ReceivingWHID, product.ProductID, product.UnitID);
                                    ensuredStock.Amount += product.Amount;
                                }
                            }
                        }

                        income.Description = (income.Description ?? string.Empty) + " | STOCK_REPAIRED";
                        await db.SaveChangesAsync();
                        transaction.Commit();

                        TempData["Success"] = "Transfer kirimi uchun qoldiq qayta tiklandi.";
                        LogHelper.LogToDatabase(User.Identity.Name, "Incomes",
                            $"RepairTransferStock - {income.DocNo} uchun qoldiq qayta tiklandi");
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

        // POST so'rovi: Incomes/Reject/5
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
                                var ensuredSenderStock = WarehouseFlowService.GetOrCreateStockRow(db, expense.SenderWHID, product.ProductID, product.UnitID);
                                ensuredSenderStock.Amount += product.Amount;
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

        // Olish so'rovi: Incomes/o'chirish/5
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

        // POST so'rovi: Incomes/o'chirish/5
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

        // Olish so'rovi: Incomes/DownloadDoc/5
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

        private static string MergeIncomeCommentWithVat(string commentFromForm, double vatRate)
        {
            var userPart = string.IsNullOrWhiteSpace(commentFromForm)
                ? ""
                : IncomeLineVatInComment.Replace(commentFromForm, "").Trim().Trim('|').Trim();
            var pct = vatRate.ToString("0.##", CultureInfo.InvariantCulture);
            var qqs = "QQS %: " + pct + "%";
            if (string.IsNullOrEmpty(userPart))
            {
                return qqs;
            }

            return userPart + " | " + qqs;
        }

        private static double? TryParseVatFromComment(string comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                return null;
            }

            var m = Regex.Match(comment, @"QQS\s*%:\s*([\d.,]+)\s*%", RegexOptions.IgnoreCase);
            if (!m.Success)
            {
                return null;
            }

            var s = m.Groups[1].Value.Replace(",", ".");
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            {
                return v;
            }

            return null;
        }

        /// <summary>
        /// Tahrirlash: izohdan QQS o‘qiladi; yo‘q bo‘lsa, jami va asosiy summadan taxmin qilinadi (eski yozuvlar).
        /// </summary>
        private static double GetVatRateForIncomeLine(double? totalPrice, double amount, double? piecePrice, string comment)
        {
            var parsed = TryParseVatFromComment(comment);
            if (parsed.HasValue)
            {
                return Math.Max(0, Math.Min(100, parsed.Value));
            }

            var b = amount * (piecePrice ?? 0);
            if (b <= 0.0000001)
            {
                return 12;
            }

            if (!totalPrice.HasValue || Math.Abs(totalPrice.Value - b) < 0.02)
            {
                return 0;
            }

            var implied = (totalPrice.Value / b - 1.0) * 100.0;
            if (implied >= -0.5 && implied <= 100.5)
            {
                return Math.Max(0, Math.Min(100, Math.Round(implied, 4)));
            }

            return 0;
        }

        private string ExtractLinkedExpenseDocNo(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return null;
            }

            const string marker = "Chiqimdan avtomatik bog'langan: ";
            var index = description.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var start = index + marker.Length;
                var rest = description.Substring(start);
                var end = rest.IndexOf(" - ", StringComparison.Ordinal);
                if (end < 0) end = rest.Length;
                var docNo = rest.Substring(0, end).Trim();
                if (!string.IsNullOrWhiteSpace(docNo))
                {
                    return docNo;
                }
            }

            // Backward compatibility: old format "... EXPENS ID: 488"
            var idMatch = Regex.Match(description, @"EXPENS\s*ID\s*:\s*(\d+)", RegexOptions.IgnoreCase);
            if (idMatch.Success && int.TryParse(idMatch.Groups[1].Value, out var expenseId))
            {
                using (var db = new DBTHSNEntities())
                {
                    var exp = db.EXPENSES.FirstOrDefault(e => e.ID == expenseId && e.IsDeleted != true);
                    if (exp != null && !string.IsNullOrWhiteSpace(exp.DocNo))
                    {
                        return exp.DocNo;
                    }
                }
            }

            return null;
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
    }

}
