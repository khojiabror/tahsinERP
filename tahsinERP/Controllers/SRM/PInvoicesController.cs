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

namespace tahsinERP.Controllers
{
    public class PInvoicesController : Controller
    {
        // Olish so'rovi: PInvoices
        public async Task<ActionResult> Index(string purchaseTypeFilter = "", int? supplierID = null, int? contractID = null)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "PInvoices", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var query = db.P_INVOICES
                    .Include(i => i.SUPPLIER)
                    .Include(i => i.COMPANy)
                    .Include(i => i.CURRENCy)
                    .Include(i => i.P_ORDERS)
                    .Include(i => i.P_CONTRACTS)
                    .Where(i => i.IsDeleted != true)
                    .AsQueryable();

                // Filtrlash bo'yicha supplier
                if (supplierID.HasValue && supplierID.Value > 0)
                {
                    query = query.Where(i => i.SupplierID == supplierID.Value);
                }

                // Filtrlash bo'yicha contract
                if (contractID.HasValue && contractID.Value > 0)
                {
                    query = query.Where(i => i.ContractID == contractID.Value);
                }

                // Filtrlash bo'yicha purchase type
                if (!string.IsNullOrEmpty(purchaseTypeFilter))
                {
                    byte purchaseType = purchaseTypeFilter == "Indirect" ? (byte)1 : (byte)0;
                    query = query.Where(i => i.PurchaseType == purchaseType);
                }

                var invoices = await query
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync();

                ViewBag.PurchaseTypeFilter = purchaseTypeFilter;
                ViewBag.SupplierID = supplierID;
                ViewBag.ContractID = contractID;

                return View(invoices);
            }
        }

        // Olish so'rovi: PInvoices/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "PInvoices", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var invoice = await db.P_INVOICES
                    .Include(i => i.SUPPLIER)
                    .Include(i => i.COMPANy)
                    .Include(i => i.CURRENCy)
                    .Include(i => i.P_ORDERS)
                    .Include(i => i.P_ORDERS.P_CONTRACTS)
                    .Include(i => i.P_INVOICE_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(i => i.P_INVOICE_PRODUCTS.Select(p => p.PRODUCT.PRODUCT_CATEGORIES))
                    .Include(i => i.P_INVOICE_PRODUCTS.Select(p => p.UNIT))
                    .Include(i => i.P_INVOICE_DOCS)
                    .FirstOrDefaultAsync(i => i.ID == id && i.IsDeleted != true);

                if (invoice == null)
                {
                    return HttpNotFound();
                }

                // Calculate contract balances uchun har bir product agar contract mavjud
                var contractBalanceService = new ContractBalanceService(db);
                var conversionService = new UnitConversionService(db);
                var contractBalances = new Dictionary<int, ContractBalanceInfo>();
                
                if (invoice.P_ORDERS?.ContractID.HasValue == true)
                {
                    foreach (var invoiceProduct in invoice.P_INVOICE_PRODUCTS)
                    {
                        var balance = await contractBalanceService.GetContractBalanceAsync(
                            invoice.P_ORDERS.ContractID.Value, 
                            invoiceProduct.ProductID);
                        contractBalances[invoiceProduct.ProductID] = balance;
                    }
                }

                // Calculate CBM uchun har bir invoice product that requires cutting
                var productCBMInfo = new Dictionary<int, decimal>();
                foreach (var invoiceProduct in invoice.P_INVOICE_PRODUCTS)
                {
                    bool requiresCutting = await conversionService.RequiresCuttingAsync(invoiceProduct.ProductID);
                    if (requiresCutting)
                    {
                        try
                        {
                            decimal cbm = await conversionService.ConvertPieceToCBMAsync(
                                invoiceProduct.ProductID, 
                                (decimal)invoiceProduct.Quantity);
                            productCBMInfo[invoiceProduct.ProductID] = cbm;
                        }
                        catch
                        {
                            productCBMInfo[invoiceProduct.ProductID] = 0;
                        }
                    }
                }

                ViewBag.ContractBalances = contractBalances;
                ViewBag.ProductCBMInfo = productCBMInfo;

                return View(invoice);
            }
        }

        // Olish so'rovi: PInvoices/yaratish
        public async Task<ActionResult> Create()
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "PInvoices"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                ViewBag.Suppliers = new SelectList(
                    await db.SUPPLIERS.Where(s => s.IsDeleted != true).OrderBy(s => s.Name).ToListAsync(),
                    "ID", "Name"
                );

                ViewBag.POrders = new SelectList(
                    await db.P_ORDERS.Where(o => o.IsDeleted != true).OrderByDescending(o => o.IssuedDate).ToListAsync(),
                    "ID", "OrderNo"
                );

                // Olish standart company (birinchi faol company)
                var defaultCompany = await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync();
                ViewBag.DefaultCompanyID = defaultCompany?.ID ?? 0;

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname"
                );

                // Aniqlash purchase type dan order agar available (dan query string)
                byte purchaseType = 0;
                if (Request.QueryString["orderId"] != null && int.TryParse(Request.QueryString["orderId"], out int orderId))
                {
                    var order = await db.P_ORDERS.FindAsync(orderId);
                    if (order != null)
                    {
                        purchaseType = order.PurchaseType;
                    }
                }

                ViewBag.PurchaseType = purchaseType;

                // Olish purchasable products based on purchase type
                var purchasableProducts = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => p.IsDeleted != true)
                    .Where(p => purchaseType == 1 
                        ? (p.PRODUCT_CATEGORIES == null || 
                           (p.PRODUCT_CATEGORIES.IsFinishedGood == false && 
                            (p.PRODUCT_CATEGORIES.IsConsumable == true || 
                             p.PRODUCT_CATEGORIES.AllowPurchase == true)))
                        : (p.PRODUCT_CATEGORIES == null || p.PRODUCT_CATEGORIES.IsFinishedGood == false))
                    .OrderBy(p => p.PNo)
                    .ToListAsync();

                // Yaratish SelectList bilan "PNo - Name" format
                var productSelectList = purchasableProducts.Select(p => new SelectListItem
                {
                    Value = p.ID.ToString(),
                    Text = $"{p.PNo} - {p.Name ?? ""}"
                }).ToList();

                ViewBag.PurchasableProducts = new SelectList(productSelectList, "Value", "Text");

                ViewBag.Units = new SelectList(
                    await db.UNITS.ToListAsync(),
                    "ID", "ShortName"
                );

                return View();
            }
        }

        // POST so'rovi: PInvoices/LoadOrderProducts - yuklash products dan P_ORDER
        [HttpPost]
        public async Task<JsonResult> LoadOrderProducts(int orderId)
        {
            using (var db = new DBTHSNEntities())
            {
                var orderProducts = await db.P_ORDER_PRODUCTS
                    .Include(op => op.PRODUCT)
                    .Include(op => op.UNIT)
                    .Where(op => op.OrderID == orderId)
                    .Select(op => new
                    {
                        ProductID = op.ProductID,
                        ProductPNo = op.PRODUCT.PNo,
                        ProductName = op.PRODUCT.Name,
                        Quantity = op.Amount,
                        Price = op.Price,
                        UnitID = op.UnitID,
                        TotalPrice = op.TotalPrice
                    })
                    .ToListAsync();

                return Json(new { success = true, data = orderProducts });
            }
        }

        // POST so'rovi: PInvoices/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(PInvoiceCreateViewModel model, HttpPostedFileBase invoiceFile)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "PInvoices"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    try
                    {
                        // Olish standart company (birinchi faol company)
                        var defaultCompany = await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync();

                        // Yaratish InvoiceNo agar not provided
                        if (string.IsNullOrEmpty(model.InvoiceNo))
                        {
                            model.InvoiceNo = await GenerateInvoiceNo(db);
                        }

                        // Aniqlash purchase type dan order agar available
                        byte purchaseType = 0; // standart: Direct
                        if (model.OrderID.HasValue)
                        {
                            var order = await db.P_ORDERS.FindAsync(model.OrderID.Value);
                            if (order != null)
                            {
                                purchaseType = order.PurchaseType;
                            }
                        }
                        else if (Request.Form["PurchaseType"] != null && Request.Form["PurchaseType"] == "Indirect")
                        {
                            purchaseType = 1;
                        }

                        // Yaratish invoice header
                        var invoice = new P_INVOICES
                        {
                            InvoiceNo = model.InvoiceNo,
                            InvoiceDate = model.InvoiceDate,
                            OrderID = model.OrderID,
                            SupplierID = model.SupplierID,
                            ContractID = model.ContractID ?? 0,
                            CompanyID = model.CompanyID ?? (defaultCompany?.ID ?? 0),
                            CurrencyID = model.CurrencyID,
                            Amount = 0,
                            IsDeleted = false,
                            PurchaseType = purchaseType
                        };

                        db.P_INVOICES.Add(invoice);
                        await db.SaveChangesAsync();

                        // Qo'shish invoice products bilan conversion logic
                        var conversionService = new UnitConversionService(db);
                        var contractBalanceService = new ContractBalanceService(db);
                        double totalAmount = 0;
                        
                        if (model.InvoiceProducts != null && model.InvoiceProducts.Any())
                        {
                            foreach (var product in model.InvoiceProducts)
                            {
                                // Tekshirish agar product requires cutting
                                bool requiresCutting = await conversionService.RequiresCuttingAsync(product.ProductID);
                                
                                // uchun requires_cutting products, quantity is in PIECES, UnitID = 1
                                // uchun other products, use user-selected unit
                                int unitId;
                                if (requiresCutting)
                                {
                                    unitId = 1; // Piece
                                }
                                else
                                {
                                    // Use user-selected unit yoki product's standart unit
                                    var prod = await db.PRODUCTS.FindAsync(product.ProductID);
                                    unitId = product.UnitID > 0 ? product.UnitID : (prod?.UnitID ?? 1);
                                }

                                var invoiceProduct = new P_INVOICE_PRODUCTS
                                {
                                    InvoiceID = invoice.ID,
                                    ProductID = product.ProductID,
                                    Quantity = product.Quantity, // In PIECES uchun cutting products, yoki user-selected unit uchun others
                                    Price = product.Price,
                                    TotalPrice = product.Price * product.Quantity,
                                    UnitID = unitId
                                };

                                db.P_INVOICE_PRODUCTS.Add(invoiceProduct);
                                totalAmount += invoiceProduct.TotalPrice;
                            }

                            // Yangilash invoice total amount
                            invoice.Amount = totalAmount;
                            await db.SaveChangesAsync();

                            // Post ga finance when purchase invoice is created
                            try
                            {
                                var userId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
                                FinancePostingService.PostPurchaseInvoice(invoice.ID, db, userId);
                            }
                            catch (Exception ex)
                            {
                                // Finance posting xatolik - don't log as it's not a database change
                            }
                        }

                        // Upload invoice hujjat agar provided
                        if (invoiceFile != null && invoiceFile.ContentLength > 0)
                        {
                            byte[] fileData = null;
                            using (var binaryReader = new BinaryReader(invoiceFile.InputStream))
                            {
                                fileData = binaryReader.ReadBytes(invoiceFile.ContentLength);
                            }

                            var invoiceDoc = new P_INVOICE_DOCS
                            {
                                InvoiceID = invoice.ID,
                                Doc = fileData
                            };

                            db.P_INVOICE_DOCS.Add(invoiceDoc);
                            await db.SaveChangesAsync();
                        }

                        // Log the action
                        LogHelper.LogToDatabase(User.Identity.Name, "PInvoices", $"Create - {invoice.InvoiceNo} invoysi yaratildi");

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

            // Qayta ga'ldirish ViewBag on xatolik
            using (var db = new DBTHSNEntities())
            {
                ViewBag.Suppliers = new SelectList(
                    await db.SUPPLIERS.Where(s => s.IsDeleted != true).OrderBy(s => s.Name).ToListAsync(),
                    "ID", "Name", model.SupplierID
                );

                ViewBag.POrders = new SelectList(
                    await db.P_ORDERS.Where(o => o.IsDeleted != true).OrderByDescending(o => o.IssuedDate).ToListAsync(),
                    "ID", "OrderNo", model.OrderID
                );

                ViewBag.Companies = new SelectList(
                    await db.COMPANIES.Where(c => c.IsActive != false).ToListAsync(),
                    "ID", "Name", model.CompanyID
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname", model.CurrencyID
                );

                // Olish standart company
                var defaultCompany = await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync();
                ViewBag.DefaultCompanyID = defaultCompany?.ID ?? 0;

                // Yuklash contracts uchun current supplier
                var contracts = await db.P_CONTRACTS
                    .Where(c => c.SupplierID == model.SupplierID && c.IsDeleted != true)
                    .OrderByDescending(c => c.IssuedDate)
                    .ToListAsync();
                ViewBag.Contracts = new SelectList(contracts, "ID", "ContractNo", model.ContractID);

                // Aniqlash purchase type dan contract agar available, otherwise dan order
                byte purchaseType = 0;
                if (model.ContractID.HasValue && model.ContractID.Value > 0)
                {
                    var contract = await db.P_CONTRACTS.FindAsync(model.ContractID.Value);
                    if (contract != null)
                    {
                        purchaseType = contract.PurchaseType;
                    }
                }
                else if (model.OrderID.HasValue && model.OrderID.Value > 0)
                {
                    var order = await db.P_ORDERS.FindAsync(model.OrderID.Value);
                    if (order != null)
                    {
                        purchaseType = order.PurchaseType;
                    }
                }

                ViewBag.PurchaseType = purchaseType;

                // Olish purchasable products based on purchase type
                var purchasableProducts = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => p.IsDeleted != true)
                    .Where(p => purchaseType == 1 
                        ? (p.PRODUCT_CATEGORIES == null || 
                           (p.PRODUCT_CATEGORIES.IsFinishedGood == false && 
                            (p.PRODUCT_CATEGORIES.IsConsumable == true || 
                             p.PRODUCT_CATEGORIES.AllowPurchase == true)))
                        : (p.PRODUCT_CATEGORIES == null || p.PRODUCT_CATEGORIES.IsFinishedGood == false))
                    .OrderBy(p => p.PNo)
                    .ToListAsync();

                // Yaratish SelectList bilan "PNo - Name" format
                var productSelectList = purchasableProducts.Select(p => new SelectListItem
                {
                    Value = p.ID.ToString(),
                    Text = $"{p.PNo} - {p.Name ?? ""}"
                }).ToList();

                ViewBag.PurchasableProducts = new SelectList(productSelectList, "Value", "Text");

                ViewBag.Units = new SelectList(
                    await db.UNITS.ToListAsync(),
                    "ID", "ShortName"
                );
            }

            return View(model);
        }

        // Yaratish InvoiceNo
        private async Task<string> GenerateInvoiceNo(DBTHSNEntities db)
        {
            var year = DateTime.Now.Year;
            var prefix = $"PI-{year}-";

            var lastInvoice = await db.P_INVOICES
                .Where(i => i.InvoiceNo.StartsWith(prefix))
                .OrderByDescending(i => i.InvoiceNo)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastInvoice != null)
            {
                var lastNumberStr = lastInvoice.InvoiceNo.Substring(prefix.Length);
                if (int.TryParse(lastNumberStr, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D4}";
        }

        // Olish so'rovi: PInvoices/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "PInvoices"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var invoice = await db.P_INVOICES
                    .Include(i => i.P_INVOICE_PRODUCTS)
                    .FirstOrDefaultAsync(i => i.ID == id && i.IsDeleted != true);

                if (invoice == null)
                {
                    return HttpNotFound();
                }

                var model = new PInvoiceEditViewModel
                {
                    ID = invoice.ID,
                    InvoiceNo = invoice.InvoiceNo,
                    InvoiceDate = invoice.InvoiceDate,
                    OrderID = invoice.OrderID,
                    SupplierID = invoice.SupplierID,
                    CompanyID = invoice.CompanyID,
                    ContractID = invoice.ContractID,
                    CurrencyID = invoice.CurrencyID ?? 0,
                    Amount = invoice.Amount,
                    InvoiceProducts = invoice.P_INVOICE_PRODUCTS.Select(ip => new PInvoiceProductViewModel
                    {
                        ID = ip.ID,
                        ProductID = ip.ProductID,
                        ProductPNo = ip.PRODUCT.PNo,
                        ProductName = ip.PRODUCT.Name,
                        Quantity = ip.Quantity,
                        Price = ip.Price,
                        TotalPrice = ip.TotalPrice,
                        UnitID = ip.UnitID
                    }).ToList()
                };

                ViewBag.Suppliers = new SelectList(
                    await db.SUPPLIERS.Where(s => s.IsDeleted != true).OrderBy(s => s.Name).ToListAsync(),
                    "ID", "Name", model.SupplierID
                );

                ViewBag.POrders = new SelectList(
                    await db.P_ORDERS.Where(o => o.IsDeleted != true).OrderByDescending(o => o.IssuedDate).ToListAsync(),
                    "ID", "OrderNo", model.OrderID
                );

                ViewBag.Companies = new SelectList(
                    await db.COMPANIES.Where(c => c.IsActive != false).ToListAsync(),
                    "ID", "Name", model.CompanyID
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname", model.CurrencyID
                );

                // Olish standart company (birinchi faol company)
                var defaultCompany = await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync();
                ViewBag.DefaultCompanyID = defaultCompany?.ID ?? 0;

                // Yuklash contracts uchun current supplier
                var contracts = await db.P_CONTRACTS
                    .Where(c => c.SupplierID == model.SupplierID && c.IsDeleted != true)
                    .OrderByDescending(c => c.IssuedDate)
                    .ToListAsync();
                ViewBag.Contracts = new SelectList(contracts, "ID", "ContractNo", model.ContractID);

                // Aniqlash purchase type dan contract agar available, otherwise dan order
                byte purchaseType = 0;
                if (model.ContractID.HasValue && model.ContractID.Value > 0)
                {
                    var contract = await db.P_CONTRACTS.FindAsync(model.ContractID.Value);
                    if (contract != null)
                    {
                        purchaseType = contract.PurchaseType;
                    }
                }
                else if (model.OrderID.HasValue && model.OrderID.Value > 0)
                {
                    var order = await db.P_ORDERS.FindAsync(model.OrderID.Value);
                    if (order != null)
                    {
                        purchaseType = order.PurchaseType;
                    }
                }

                ViewBag.PurchaseType = purchaseType;

                // Olish purchasable products based on purchase type
                var purchasableProducts = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => p.IsDeleted != true)
                    .Where(p => purchaseType == 1 
                        ? (p.PRODUCT_CATEGORIES == null || 
                           (p.PRODUCT_CATEGORIES.IsFinishedGood == false && 
                            (p.PRODUCT_CATEGORIES.IsConsumable == true || 
                             p.PRODUCT_CATEGORIES.AllowPurchase == true)))
                        : (p.PRODUCT_CATEGORIES == null || p.PRODUCT_CATEGORIES.IsFinishedGood == false))
                    .OrderBy(p => p.PNo)
                    .ToListAsync();

                // Yaratish SelectList bilan "PNo - Name" format
                var productSelectList = purchasableProducts.Select(p => new SelectListItem
                {
                    Value = p.ID.ToString(),
                    Text = $"{p.PNo} - {p.Name ?? ""}"
                }).ToList();

                ViewBag.PurchasableProducts = new SelectList(productSelectList, "Value", "Text");

                ViewBag.Units = new SelectList(
                    await db.UNITS.ToListAsync(),
                    "ID", "ShortName"
                );

                return View(model);
            }
        }

        // POST so'rovi: PInvoices/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(PInvoiceEditViewModel model, HttpPostedFileBase invoiceFile)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "PInvoices"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    try
                    {
                        var invoice = await db.P_INVOICES.FindAsync(model.ID);
                        if (invoice == null || invoice.IsDeleted == true)
                        {
                            return HttpNotFound();
                        }

                        // Aniqlash purchase type dan contract agar available, otherwise dan order
                        byte purchaseType = 0;
                        if (model.ContractID.HasValue && model.ContractID.Value > 0)
                        {
                            var contract = await db.P_CONTRACTS.FindAsync(model.ContractID.Value);
                            if (contract != null)
                            {
                                purchaseType = contract.PurchaseType;
                            }
                        }
                        else if (model.OrderID.HasValue && model.OrderID.Value > 0)
                        {
                            var order = await db.P_ORDERS.FindAsync(model.OrderID.Value);
                            if (order != null)
                            {
                                purchaseType = order.PurchaseType;
                            }
                        }

                        // Yangilash invoice header
                        invoice.InvoiceNo = model.InvoiceNo;
                        invoice.InvoiceDate = model.InvoiceDate;
                        invoice.OrderID = model.OrderID; // Optional
                        invoice.SupplierID = model.SupplierID;
                        invoice.ContractID = model.ContractID ?? 0; // majburiy uchun contract-based calculations
                        invoice.CompanyID = model.CompanyID ?? (await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync())?.ID ?? 0;
                        invoice.CurrencyID = model.CurrencyID;
                        invoice.PurchaseType = purchaseType;

                        // O'chirish existing invoice products
                        var existingProducts = db.P_INVOICE_PRODUCTS.Where(ip => ip.InvoiceID == invoice.ID).ToList();
                        foreach (var existing in existingProducts)
                        {
                            db.P_INVOICE_PRODUCTS.Remove(existing);
                        }

                        // Qo'shish updated invoice products bilan conversion logic
                        var conversionService = new UnitConversionService(db);
                        
                        if (model.InvoiceProducts != null && model.InvoiceProducts.Any())
                        {
                            foreach (var product in model.InvoiceProducts)
                            {
                                // Tekshirish agar product requires cutting
                                bool requiresCutting = await conversionService.RequiresCuttingAsync(product.ProductID);
                                
                                // uchun requires_cutting products, quantity is in PIECES, UnitID = 1
                                // uchun other products, use user-selected unit
                                int unitId;
                                if (requiresCutting)
                                {
                                    unitId = 1; // Piece
                                }
                                else
                                {
                                    // Use user-selected unit yoki product's standart unit
                                    var prod = await db.PRODUCTS.FindAsync(product.ProductID);
                                    unitId = product.UnitID > 0 ? product.UnitID : (prod?.UnitID ?? 1);
                                }

                                var invoiceProduct = new P_INVOICE_PRODUCTS
                                {
                                    InvoiceID = invoice.ID,
                                    ProductID = product.ProductID,
                                    Quantity = product.Quantity, // In PIECES uchun cutting products, yoki user-selected unit uchun others
                                    Price = product.Price,
                                    TotalPrice = product.Price * product.Quantity,
                                    UnitID = unitId
                                };

                                db.P_INVOICE_PRODUCTS.Add(invoiceProduct);
                            }
                        }

                        // Handle fayl upload
                        if (invoiceFile != null && invoiceFile.ContentLength > 0)
                        {
                            // O'chirish existing docs
                            var existingDocs = db.P_INVOICE_DOCS.Where(id => id.InvoiceID == invoice.ID).ToList();
                            foreach (var doc in existingDocs)
                            {
                                db.P_INVOICE_DOCS.Remove(doc);
                            }

                            // Qo'shish new doc
                            byte[] fileData = null;
                            using (var binaryReader = new BinaryReader(invoiceFile.InputStream))
                            {
                                fileData = binaryReader.ReadBytes(invoiceFile.ContentLength);
                            }

                            var invoiceDoc = new P_INVOICE_DOCS
                            {
                                InvoiceID = invoice.ID,
                                Doc = fileData
                            };

                            db.P_INVOICE_DOCS.Add(invoiceDoc);
                        }

                        await db.SaveChangesAsync();

                        // Log the action
                        LogHelper.LogToDatabase(User.Identity.Name, "PInvoices", $"Edit - {invoice.InvoiceNo} invoysi tahrirlandi");

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

            // Qayta ga'ldirish ViewBag on xatolik
            using (var db = new DBTHSNEntities())
            {
                ViewBag.Suppliers = new SelectList(
                    await db.SUPPLIERS.Where(s => s.IsDeleted != true).OrderBy(s => s.Name).ToListAsync(),
                    "ID", "Name", model.SupplierID
                );

                ViewBag.POrders = new SelectList(
                    await db.P_ORDERS.Where(o => o.IsDeleted != true).OrderByDescending(o => o.IssuedDate).ToListAsync(),
                    "ID", "OrderNo", model.OrderID
                );

                ViewBag.Companies = new SelectList(
                    await db.COMPANIES.Where(c => c.IsActive != false).ToListAsync(),
                    "ID", "Name", model.CompanyID
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname", model.CurrencyID
                );

                // Olish standart company
                var defaultCompany = await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync();
                ViewBag.DefaultCompanyID = defaultCompany?.ID ?? 0;

                // Yuklash contracts uchun current supplier
                var contracts = await db.P_CONTRACTS
                    .Where(c => c.SupplierID == model.SupplierID && c.IsDeleted != true)
                    .OrderByDescending(c => c.IssuedDate)
                    .ToListAsync();
                ViewBag.Contracts = new SelectList(contracts, "ID", "ContractNo", model.ContractID);

                // Aniqlash purchase type dan contract agar available, otherwise dan order
                byte purchaseType = 0;
                if (model.ContractID.HasValue && model.ContractID.Value > 0)
                {
                    var contract = await db.P_CONTRACTS.FindAsync(model.ContractID.Value);
                    if (contract != null)
                    {
                        purchaseType = contract.PurchaseType;
                    }
                }
                else if (model.OrderID.HasValue && model.OrderID.Value > 0)
                {
                    var order = await db.P_ORDERS.FindAsync(model.OrderID.Value);
                    if (order != null)
                    {
                        purchaseType = order.PurchaseType;
                    }
                }

                ViewBag.PurchaseType = purchaseType;

                // Olish purchasable products based on purchase type
                var purchasableProducts = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => p.IsDeleted != true)
                    .Where(p => purchaseType == 1 
                        ? (p.PRODUCT_CATEGORIES == null || 
                           (p.PRODUCT_CATEGORIES.IsFinishedGood == false && 
                            (p.PRODUCT_CATEGORIES.IsConsumable == true || 
                             p.PRODUCT_CATEGORIES.AllowPurchase == true)))
                        : (p.PRODUCT_CATEGORIES == null || p.PRODUCT_CATEGORIES.IsFinishedGood == false))
                    .OrderBy(p => p.PNo)
                    .ToListAsync();

                // Yaratish SelectList bilan "PNo - Name" format
                var productSelectList = purchasableProducts.Select(p => new SelectListItem
                {
                    Value = p.ID.ToString(),
                    Text = $"{p.PNo} - {p.Name ?? ""}"
                }).ToList();

                ViewBag.PurchasableProducts = new SelectList(productSelectList, "Value", "Text");

                ViewBag.Units = new SelectList(
                    await db.UNITS.ToListAsync(),
                    "ID", "ShortName"
                );
            }

            return View(model);
        }

        // Olish so'rovi: PInvoices/o'chirish/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "PInvoices"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var invoice = await db.P_INVOICES
                    .Include(i => i.SUPPLIER)
                    .Include(i => i.COMPANy)
                    .Include(i => i.CURRENCy)
                    .Include(i => i.P_ORDERS)
                    .Include(i => i.P_INVOICE_PRODUCTS.Select(p => p.PRODUCT))
                    .FirstOrDefaultAsync(i => i.ID == id && i.IsDeleted != true);

                if (invoice == null)
                {
                    return HttpNotFound();
                }

                return View(invoice);
            }
        }

        // POST so'rovi: PInvoices/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "PInvoices"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var invoice = await db.P_INVOICES.FindAsync(id);
                if (invoice != null)
                {
                    invoice.IsDeleted = true;
                    await db.SaveChangesAsync();

                    // Log the action
                    LogHelper.LogToDatabase(User.Identity.Name, "PInvoices", $"Delete - {invoice.InvoiceNo} invoysi o'chirildi");
                }

                return RedirectToAction("Index");
            }
        }

        // Olish so'rovi: PInvoices/DownloadDoc/5
        public async Task<ActionResult> DownloadDoc(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.P_INVOICE_DOCS.FindAsync(id);
                if (doc == null || doc.Doc == null)
                {
                    return HttpNotFound();
                }

                return File(doc.Doc, "application/pdf", $"Invoice_{doc.InvoiceID}.pdf");
            }
        }

        // AJAX so'rovi: Get contracts by supplier
        [HttpPost]
        public async Task<JsonResult> GetContractsBySupplier(int supplierId)
        {
            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var contracts = await db.P_CONTRACTS
                        .Where(c => c.SupplierID == supplierId && c.IsDeleted != true)
                        .OrderByDescending(c => c.IssuedDate)
                        .Select(c => new
                        {
                            ID = c.ID,
                            ContractNo = c.ContractNo
                        })
                        .ToListAsync();

                    return Json(new { success = true, data = contracts }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
    }

    // ViewModels
    public class PInvoiceCreateViewModel
    {
        public string InvoiceNo { get; set; }
        public DateTime InvoiceDate { get; set; } = DateTime.Now;
        public int? OrderID { get; set; }
        public int SupplierID { get; set; }
        public int? CompanyID { get; set; }
        public int? ContractID { get; set; }
        public int CurrencyID { get; set; }
        public List<PInvoiceProductViewModel> InvoiceProducts { get; set; }

        public PInvoiceCreateViewModel()
        {
            InvoiceProducts = new List<PInvoiceProductViewModel>();
        }
    }

    public class PInvoiceEditViewModel
    {
        public int ID { get; set; }
        public string InvoiceNo { get; set; }
        public DateTime InvoiceDate { get; set; }
        public int? OrderID { get; set; }
        public int SupplierID { get; set; }
        public int? CompanyID { get; set; }
        public int? ContractID { get; set; }
        public int CurrencyID { get; set; }
        public double Amount { get; set; }
        public List<PInvoiceProductViewModel> InvoiceProducts { get; set; }

        public PInvoiceEditViewModel()
        {
            InvoiceProducts = new List<PInvoiceProductViewModel>();
        }
    }

    public class PInvoiceProductViewModel
    {
        public int? ID { get; set; }
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public double Quantity { get; set; }
        public double Price { get; set; }
        public double TotalPrice { get; set; }
        public int UnitID { get; set; }
    }
}
