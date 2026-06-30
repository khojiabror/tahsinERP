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
        public async Task<ActionResult> Index(
            string purchaseTypeFilter = "",
            int? supplierID = null,
            int? contractID = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
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

                // Filtrlash bo'yicha sana oralig'i
                if (fromDate.HasValue)
                {
                    var from = fromDate.Value.Date;
                    query = query.Where(i => i.InvoiceDate >= from);
                }
                if (toDate.HasValue)
                {
                    var to = toDate.Value.Date;
                    query = query.Where(i => DbFunctions.TruncateTime(i.InvoiceDate) <= to);
                }

                var suppliers = await db.SUPPLIERS
                    .Where(s => s.IsDeleted != true)
                    .OrderBy(s => s.Name)
                    .ToListAsync();
                ViewBag.Suppliers = new SelectList(suppliers, "ID", "Name", supplierID);

                var contractsForFilter = await db.P_CONTRACTS
                    .Where(c => c.IsDeleted != true)
                    .OrderByDescending(c => c.IssuedDate)
                    .ThenByDescending(c => c.ID)
                    .Select(c => new { c.ID, c.ContractNo })
                    .ToListAsync();
                ViewBag.Contracts = new SelectList(contractsForFilter, "ID", "ContractNo", contractID);

                var invoices = await query
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync();

                // QQS (VAT) faqat P_CONTRACT da saqlanadi. Har bir invoysga bog'langan
                // shartnomaning QQS stavkasini hisoblab, viewda invoice summasiga qo'shib ko'rsatamiz.
                var contractIds = invoices
                    .Select(i => i.ContractID > 0 ? (int?)i.ContractID : i.P_ORDERS?.ContractID)
                    .Where(cid => cid.HasValue && cid.Value > 0)
                    .Select(cid => cid.Value)
                    .Distinct()
                    .ToList();

                var vatRatesByContract = new Dictionary<int, double>();
                if (contractIds.Any())
                {
                    var contractVatLines = await db.P_CONTRACT_PRODUCTS
                        .Where(p => contractIds.Contains(p.ContractID))
                        .Select(p => new PContractVatHelper.ContractProductVatLine
                        {
                            ContractID = p.ContractID,
                            Amount = p.Amount,
                            VATAmount = p.VATAmount
                        })
                        .ToListAsync();

                    vatRatesByContract = PContractVatHelper.BuildContractVatRates(contractVatLines);
                }
                ViewBag.VatRatesByContract = vatRatesByContract;

                ViewBag.PurchaseTypeFilter = purchaseTypeFilter;
                ViewBag.SupplierID = supplierID;
                ViewBag.ContractID = contractID;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.ChangePermit = tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "PInvoices");

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
                    // CBM ni faqat miqdor donada (UnitID=1) saqlangan bo'lsa hisoblaymiz.
                    // Agar foydalanuvchi to'g'ridan-to'g'ri m3 kiritgan bo'lsa, qayta aylantirish shart emas.
                    if (requiresCutting && invoiceProduct.UnitID == 1)
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
                ViewBag.ContractVatRate = await PContractVatHelper.CalculateContractVatRateAsync(db, invoice.ContractID > 0 ? (int?)invoice.ContractID : invoice.P_ORDERS?.ContractID);

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

                var units = await db.UNITS.ToListAsync();
                ViewBag.Units = new SelectList(
                    units,
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
                        P_ORDERS order = null;
                        if (model.OrderID.HasValue)
                        {
                            order = await db.P_ORDERS.FindAsync(model.OrderID.Value);
                            if (order != null)
                            {
                                purchaseType = order.PurchaseType;
                            }
                        }
                        else if (Request.Form["PurchaseType"] != null && Request.Form["PurchaseType"] == "Indirect")
                        {
                            purchaseType = 1;
                        }

                        if (order != null)
                        {
                            var orderSupplierId = order.SupplierID;
                            var orderContractId = order.ContractID;

                            if (order.PurchaseType == 0)
                            {
                                if (!orderSupplierId.HasValue && model.SupplierID <= 0)
                                {
                                    ModelState.AddModelError("SupplierID", "Direct buyurtmadan invoice yaratilganda ta'minotchi tanlanishi shart.");
                                }
                                if (!orderContractId.HasValue && (!model.ContractID.HasValue || model.ContractID.Value <= 0))
                                {
                                    ModelState.AddModelError("ContractID", "Direct buyurtmadan invoice yaratilganda shartnoma tanlanishi shart.");
                                }
                            }

                            if (orderSupplierId.HasValue && orderSupplierId.Value > 0 && model.SupplierID != orderSupplierId.Value)
                            {
                                ModelState.AddModelError("SupplierID", "Tanlangan ta'minotchi buyurtmadagi ta'minotchi bilan mos emas.");
                            }
                            if (orderContractId.HasValue && orderContractId.Value > 0 && model.ContractID != orderContractId.Value)
                            {
                                ModelState.AddModelError("ContractID", "Tanlangan shartnoma buyurtmadagi shartnoma bilan mos emas.");
                            }
                        }

                        if (model.ContractID.HasValue && model.ContractID.Value > 0)
                        {
                            var contract = await db.P_CONTRACTS.FindAsync(model.ContractID.Value);
                            if (contract == null || contract.IsDeleted == true)
                            {
                                ModelState.AddModelError("ContractID", "Tanlangan shartnoma topilmadi.");
                            }
                            else if (model.SupplierID > 0 && contract.SupplierID != model.SupplierID)
                            {
                                ModelState.AddModelError("ContractID", "Tanlangan shartnoma tanlangan ta'minotchiga tegishli emas.");
                            }
                        }

                        if (ModelState.IsValid)
                        {
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

                        // Qo'shish invoice products
                        double totalAmount = 0;
                        
                        if (model.InvoiceProducts != null && model.InvoiceProducts.Any())
                        {
                            foreach (var product in model.InvoiceProducts)
                            {
                                // Foydalanuvchi tanlagan birlikni saqlaymiz (masalan, m3/CBM).
                                // Avval kesiladigan mahsulotlar majburan donaga (UnitID=1) o'tkazilardi —
                                // shu sababli m3 kiritilgan miqdor saqlangach donaga aylanib qolardi.
                                var prod = await db.PRODUCTS.FindAsync(product.ProductID);
                                int unitId = product.UnitID > 0 ? product.UnitID : (prod?.UnitID ?? 1);

                                var invoiceProduct = new P_INVOICE_PRODUCTS
                                {
                                    InvoiceID = invoice.ID,
                                    ProductID = product.ProductID,
                                    Quantity = product.Quantity,
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
                            catch (Exception)
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

                var units = await db.UNITS.ToListAsync();
                ViewBag.Units = new SelectList(
                    units,
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
                .Where(i => i.IsDeleted != true && i.InvoiceNo != null && i.InvoiceNo.StartsWith(prefix))
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
                    Amount = invoice.Amount ?? 0,
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
                ViewBag.ContractVatRate = await PContractVatHelper.CalculateContractVatRateAsync(
                    db,
                    model.ContractID > 0 ? model.ContractID : null);

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

                var units = await db.UNITS.ToListAsync();
                ViewBag.Units = new SelectList(
                    units,
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
                        P_ORDERS order = null;
                        if (model.OrderID.HasValue && model.OrderID.Value > 0)
                        {
                            order = await db.P_ORDERS.FindAsync(model.OrderID.Value);
                        }
                        if (model.ContractID.HasValue && model.ContractID.Value > 0)
                        {
                            var contract = await db.P_CONTRACTS.FindAsync(model.ContractID.Value);
                            if (contract != null)
                            {
                                purchaseType = contract.PurchaseType;
                            }
                        }
                        else if (order != null)
                        {
                            purchaseType = order.PurchaseType;
                        }

                        if (order != null)
                        {
                            if (order.PurchaseType == 0)
                            {
                                if (!order.SupplierID.HasValue && model.SupplierID <= 0)
                                {
                                    ModelState.AddModelError("SupplierID", "Direct buyurtmadan invoice tahrirlashda ta'minotchi tanlanishi shart.");
                                }
                                if (!order.ContractID.HasValue && (!model.ContractID.HasValue || model.ContractID.Value <= 0))
                                {
                                    ModelState.AddModelError("ContractID", "Direct buyurtmadan invoice tahrirlashda shartnoma tanlanishi shart.");
                                }
                            }

                            if (order.SupplierID.HasValue && order.SupplierID.Value > 0 && model.SupplierID != order.SupplierID.Value)
                            {
                                ModelState.AddModelError("SupplierID", "Tanlangan ta'minotchi buyurtmadagi ta'minotchi bilan mos emas.");
                            }
                            if (order.ContractID.HasValue && order.ContractID.Value > 0 && model.ContractID != order.ContractID.Value)
                            {
                                ModelState.AddModelError("ContractID", "Tanlangan shartnoma buyurtmadagi shartnoma bilan mos emas.");
                            }
                        }

                        if (model.ContractID.HasValue && model.ContractID.Value > 0)
                        {
                            var contract = await db.P_CONTRACTS.FindAsync(model.ContractID.Value);
                            if (contract == null || contract.IsDeleted == true)
                            {
                                ModelState.AddModelError("ContractID", "Tanlangan shartnoma topilmadi.");
                            }
                            else if (model.SupplierID > 0 && contract.SupplierID != model.SupplierID)
                            {
                                ModelState.AddModelError("ContractID", "Tanlangan shartnoma tanlangan ta'minotchiga tegishli emas.");
                            }
                        }

                        if (ModelState.IsValid)
                        {
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

                        // Qo'shish updated invoice products
                        if (model.InvoiceProducts != null && model.InvoiceProducts.Any())
                        {
                            foreach (var product in model.InvoiceProducts)
                            {
                                // Foydalanuvchi tanlagan birlikni saqlaymiz (masalan, m3/CBM).
                                // Avval kesiladigan mahsulotlar majburan donaga (UnitID=1) o'tkazilardi —
                                // shu sababli m3 kiritilgan miqdor saqlangach donaga aylanib qolardi.
                                var prod = await db.PRODUCTS.FindAsync(product.ProductID);
                                int unitId = product.UnitID > 0 ? product.UnitID : (prod?.UnitID ?? 1);

                                var invoiceProduct = new P_INVOICE_PRODUCTS
                                {
                                    InvoiceID = invoice.ID,
                                    ProductID = product.ProductID,
                                    Quantity = product.Quantity,
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

                        // Editdan keyin finance postingni ham yangilash (upsert)
                        try
                        {
                            var userId = tahsinERP.Controllers.UserHelper.GetUserId(User.Identity.Name);
                            FinancePostingService.PostPurchaseInvoice(invoice.ID, db, userId);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.LogToDatabase(User.Identity.Name, "PInvoices", "Edit",
                                $"Finance posting update error for invoice {invoice.ID}: {ex.Message}");
                        }

                            // Log the action
                            LogHelper.LogToDatabase(User.Identity.Name, "PInvoices", $"Edit - {invoice.InvoiceNo} invoysi tahrirlandi");

                            return RedirectToAction("Index");
                        }
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

                var units = await db.UNITS.ToListAsync();
                ViewBag.Units = new SelectList(
                    units,
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
                    await SoftDeleteReferenceService.AfterPInvoiceSoftDeletedAsync(db, id);
                    // O'chirilgan invoys uchun auto-post qilingan finance yozuvini ham bekor qilamiz
                    FinancePostingService.RemovePurchaseInvoicePosting(id, db);
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
                    var contractIds = contracts.Select(c => c.ID).ToList();
                    var contractProducts = await db.P_CONTRACT_PRODUCTS
                        .Where(p => contractIds.Contains(p.ContractID))
                        .Select(p => new PContractVatHelper.ContractProductVatLine
                        {
                            ContractID = p.ContractID,
                            Amount = p.Amount,
                            VATAmount = p.VATAmount
                        })
                        .ToListAsync();

                    var vatByContract = PContractVatHelper.BuildContractVatRates(contractProducts);

                    var data = contracts.Select(c => new
                    {
                        c.ID,
                        c.ContractNo,
                        VatRate = vatByContract.ContainsKey(c.ID) ? vatByContract[c.ID] : 0d
                    }).ToList();

                    return Json(new { success = true, data }, JsonRequestBehavior.AllowGet);
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
