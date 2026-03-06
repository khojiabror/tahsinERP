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
    public class PContractsController : Controller
    {
        // Olish so'rovi: PContracts
        public async Task<ActionResult> Index(string purchaseTypeFilter = "", int? supplierID = null)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "PContracts", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var query = db.P_CONTRACTS
                    .Include(c => c.SUPPLIER)
                    .Include(c => c.COMPANy)
                    .Include(c => c.CURRENCy)
                    .Where(c => c.IsDeleted != true)
                    .AsQueryable();

                // Filtrlash bo'yicha supplier
                if (supplierID.HasValue && supplierID.Value > 0)
                {
                    query = query.Where(c => c.SupplierID == supplierID.Value);
                }

                // Filtrlash bo'yicha purchase type
                if (!string.IsNullOrEmpty(purchaseTypeFilter))
                {
                    byte purchaseType = purchaseTypeFilter == "Indirect" ? (byte)1 : (byte)0;
                    query = query.Where(c => c.PurchaseType == purchaseType);
                }

                var contracts = await query
                    .OrderByDescending(c => c.IssuedDate)
                    .ToListAsync();

                ViewBag.PurchaseTypeFilter = purchaseTypeFilter;
                ViewBag.SupplierID = supplierID;
                ViewBag.ChangePermit = tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "PContracts");

                return View(contracts);
            }
        }

        // Olish so'rovi: PContracts/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "PContracts", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var contract = await db.P_CONTRACTS
                    .Include(c => c.SUPPLIER)
                    .Include(c => c.COMPANy)
                    .Include(c => c.CURRENCy)
                    .Include(c => c.P_CONTRACT_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(c => c.P_CONTRACT_PRODUCTS.Select(p => p.UNIT))
                    .Include(c => c.P_CONTRACT_DOCS)
                    .FirstOrDefaultAsync(c => c.ID == id && c.IsDeleted != true);

                if (contract == null)
                {
                    return HttpNotFound();
                }

                // Olish linked invoices
                var linkedInvoices = await db.P_INVOICES
                    .Include(i => i.CURRENCy)
                    .Where(i => i.ContractID == id && i.IsDeleted != true)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync();
                ViewBag.LinkedInvoices = linkedInvoices;
                ViewBag.InvoicesCount = linkedInvoices.Count;

                // Olish linked orders count
                var ordersCount = await db.P_ORDERS
                    .Where(o => o.ContractID == id && o.IsDeleted != true)
                    .CountAsync();
                ViewBag.OrdersCount = ordersCount;

                // Calculate totals
                var contractedTotal = contract.Amount ?? 0;
                var orderedTotal = await db.P_ORDERS
                    .Where(o => o.ContractID == id && o.IsDeleted != true)
                    .SumAsync(o => (double?)o.Amount) ?? 0;
                var invoicedTotal = linkedInvoices.Sum(i => (double?)i.Amount) ?? 0;

                ViewBag.ContractedTotal = contractedTotal;
                ViewBag.OrderedTotal = orderedTotal;
                ViewBag.InvoicedTotal = invoicedTotal;

                return View(contract);
            }
        }

        // Olish so'rovi: PContracts/yaratish
        public async Task<ActionResult> Create()
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "PContracts"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                ViewBag.Suppliers = new SelectList(
                    await db.SUPPLIERS.Where(s => s.IsDeleted != true).OrderBy(s => s.Name).ToListAsync(),
                    "ID", "Name"
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname"
                );

                // Aniqlash purchase type dan query string yoki form
                byte purchaseType = 0;
                if (Request.QueryString["purchaseType"] == "Indirect" || Request.Form["PurchaseType"] == "Indirect")
                {
                    purchaseType = 1;
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

                ViewBag.HSCodes = new SelectList(
                    await db.HSCODES.ToListAsync(),
                    "ID", "HSCODE1"
                );

                return View();
            }
        }

        // POST so'rovi: PContracts/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(PContractCreateViewModel model, HttpPostedFileBase contractFile)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "PContracts"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    try
                    {
                        // Olish standart company agar not provided
                        var defaultCompany = await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync();
                        var companyId = model.CompanyID ?? (defaultCompany?.ID ?? 0);

                        // Yaratish ContractNo agar not provided
                        if (string.IsNullOrEmpty(model.ContractNo))
                        {
                            model.ContractNo = await GenerateContractNo(db);
                        }

                        // Aniqlash purchase type (0 = Direct, 1 = Indirect)
                        byte purchaseType = 0; // standart: Direct
                        if (Request.Form["PurchaseType"] != null && Request.Form["PurchaseType"] == "Indirect")
                        {
                            purchaseType = 1;
                        }

                        // Calculate total amount (including VAT agar provided)
                        double totalAmount = 0;
                        int totalIndex = 0;
                        while (Request.Form[$"ContractProducts[{totalIndex}].ProductID"] != null)
                        {
                            var productIDStr = Request.Form[$"ContractProducts[{totalIndex}].ProductID"];
                            if (!string.IsNullOrEmpty(productIDStr) && productIDStr != "0")
                            {
                                var price = double.Parse(Request.Form[$"ContractProducts[{totalIndex}].Price"] ?? "0");
                                var quantity = double.Parse(Request.Form[$"ContractProducts[{totalIndex}].Quantity"] ?? "0");
                                var vatPercent = double.Parse(Request.Form[$"ContractProducts[{totalIndex}].VatPercent"] ?? "12");
                                if (price > 0 && quantity > 0)
                                {
                                    var baseTotal = price * quantity;
                                    var totalWithVat = baseTotal + (baseTotal * vatPercent / 100);
                                    totalAmount += totalWithVat;
                                }
                            }
                            totalIndex++;
                        }

                        // Yaratish contract header
                        var contract = new P_CONTRACTS
                        {
                            ContractNo = model.ContractNo,
                            IssuedDate = model.IssuedDate,
                            CompanyID = companyId,
                            SupplierID = model.SupplierID,
                            CurrencyID = model.CurrencyID,
                            Amount = totalAmount,
                            Incoterms = model.Incoterms,
                            PaymentTerms = model.PaymentTerms,
                            DueDate = model.DueDate,
                            IDN = model.IDN,
                            IsDeleted = false,
                            PurchaseType = purchaseType
                        };

                        db.P_CONTRACTS.Add(contract);
                        await db.SaveChangesAsync();

                        // Qo'shish contract products
                        if (model.ContractProducts != null && model.ContractProducts.Any())
                        {
                            var conversionService = new UnitConversionService(db);
                            
                            foreach (var product in model.ContractProducts)
                            {
                                // Tekshirish agar product requires cutting
                                bool requiresCutting = await conversionService.RequiresCuttingAsync(product.ProductID);
                                
                                // uchun requires_cutting products, use CBM (UnitID = 11 uchun m3)
                                // uchun other products, use the user-selected unit
                                int unitId;
                                if (requiresCutting)
                                {
                                    unitId = 11; // m3 (cubic meter) uchun CBM
                                }
                                else
                                {
                                    // Use user-selected unit yoki product's standart unit
                                    var prod = await db.PRODUCTS.FindAsync(product.ProductID);
                                    unitId = product.UnitID ?? (prod?.UnitID ?? 1);
                                }

                                var contractProduct = new P_CONTRACT_PRODUCTS
                                {
                                    ContractID = contract.ID,
                                    ProductID = product.ProductID,
                                    Price = product.Price,
                                    Quantity = product.Quantity, // Already in CBM uchun cutting products, yoki user-selected unit uchun others
                                    UnitID = unitId,
                                    MOQ = product.MOQ,
                                    HSCodeID = product.HSCodeID,
                                    ActivePart = true
                                };

                                db.P_CONTRACT_PRODUCTS.Add(contractProduct);
                            }

                            await db.SaveChangesAsync();
                        }

                        // Upload contract hujjat agar provided
                        if (contractFile != null && contractFile.ContentLength > 0)
                        {
                            byte[] fileData = null;
                            using (var binaryReader = new BinaryReader(contractFile.InputStream))
                            {
                                fileData = binaryReader.ReadBytes(contractFile.ContentLength);
                            }

                            var contractDoc = new P_CONTRACT_DOCS
                            {
                                ContractID = contract.ID,
                                Doc = fileData
                            };

                            db.P_CONTRACT_DOCS.Add(contractDoc);
                            await db.SaveChangesAsync();
                        }

                        // Log the action
                        LogHelper.LogToDatabase(User.Identity.Name, "PContracts", $"Create - {contract.ContractNo} shartnomasi yaratildi");

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

                // Olish standart company (birinchi faol company)
                var defaultCompany = await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync();
                ViewBag.DefaultCompanyID = defaultCompany?.ID ?? 0;

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname", model.CurrencyID
                );

                // Aniqlash purchase type dan query string yoki form
                byte purchaseType = 0;
                if (Request.QueryString["purchaseType"] == "Indirect" || Request.Form["PurchaseType"] == "Indirect")
                {
                    purchaseType = 1;
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

                ViewBag.HSCodes = new SelectList(
                    await db.HSCODES.ToListAsync(),
                    "ID", "HSCODE1"
                );
            }

            return View(model);
        }

        // Yaratish ContractNo
        private async Task<string> GenerateContractNo(DBTHSNEntities db)
        {
            var year = DateTime.Now.Year;
            var prefix = $"PC-{year}-";

            var lastContract = await db.P_CONTRACTS
                .Where(c => c.ContractNo.StartsWith(prefix))
                .OrderByDescending(c => c.ContractNo)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastContract != null)
            {
                var lastNumberStr = lastContract.ContractNo.Substring(prefix.Length);
                if (int.TryParse(lastNumberStr, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D4}";
        }

        // Olish so'rovi: PContracts/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "PContracts"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var contract = await db.P_CONTRACTS
                    .Include(c => c.P_CONTRACT_PRODUCTS)
                    .FirstOrDefaultAsync(c => c.ID == id && c.IsDeleted != true);

                if (contract == null)
                {
                    return HttpNotFound();
                }

                var model = new PContractEditViewModel
                {
                    ID = contract.ID,
                    ContractNo = contract.ContractNo,
                    IssuedDate = contract.IssuedDate,
                    CompanyID = contract.CompanyID,
                    SupplierID = contract.SupplierID,
                    CurrencyID = contract.CurrencyID ?? 0,
                    Amount = contract.Amount,
                    Incoterms = contract.Incoterms,
                    PaymentTerms = contract.PaymentTerms,
                    DueDate = contract.DueDate,
                    IDN = contract.IDN,
                    ContractProducts = contract.P_CONTRACT_PRODUCTS.Select(cp => new PContractProductViewModel
                    {
                        ID = cp.ID,
                        ProductID = cp.ProductID, // Changed dan PartID ga ProductID
                        ProductPNo = cp.PRODUCT.PNo,
                        ProductName = cp.PRODUCT.Name,
                        Price = cp.Price,
                        Quantity = cp.Quantity,
                        Amount = cp.Amount,
                        UnitID = cp.UnitID,
                        MOQ = cp.MOQ,
                        HSCodeID = cp.HSCodeID
                    }).ToList()
                };

                ViewBag.Suppliers = new SelectList(
                    await db.SUPPLIERS.Where(s => s.IsDeleted != true).OrderBy(s => s.Name).ToListAsync(),
                    "ID", "Name", model.SupplierID
                );

                // Olish standart company (birinchi faol company)
                var defaultCompany = await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync();
                ViewBag.DefaultCompanyID = defaultCompany?.ID ?? 0;

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname", model.CurrencyID
                );

                // Aniqlash purchase type dan query string yoki form
                byte purchaseType = 0;
                if (Request.QueryString["purchaseType"] == "Indirect" || Request.Form["PurchaseType"] == "Indirect")
                {
                    purchaseType = 1;
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

                ViewBag.HSCodes = new SelectList(
                    await db.HSCODES.ToListAsync(),
                    "ID", "HSCODE1"
                );

                return View(model);
            }
        }

        // POST so'rovi: PContracts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(PContractEditViewModel model, HttpPostedFileBase contractFile)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "PContracts"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // O'chirish ModelState errors uchun ContractProducts agar collection is null yoki empty
            if (model.ContractProducts == null || !model.ContractProducts.Any())
            {
                var keysToRemove = ModelState.Keys.Where(k => k.StartsWith("ContractProducts")).ToList();
                foreach (var key in keysToRemove)
                {
                    ModelState.Remove(key);
                }
                ModelState.AddModelError("", "Kamida bitta mahsulot qo'shish kerak");
            }

            // Tekshirish ContractProducts collection
            if (model.ContractProducts != null)
            {
                for (int i = 0; i < model.ContractProducts.Count; i++)
                {
                    var product = model.ContractProducts[i];
                    if (product.ProductID <= 0)
                    {
                        ModelState.AddModelError($"ContractProducts[{i}].ProductID", "Mahsulot tanlang");
                    }
                    if (product.Quantity <= 0)
                    {
                        ModelState.AddModelError($"ContractProducts[{i}].Quantity", "Miqdor 0 dan katta bo'lishi kerak");
                    }
                    if (product.Price <= 0)
                    {
                        ModelState.AddModelError($"ContractProducts[{i}].Price", "Narx 0 dan katta bo'lishi kerak");
                    }
                }
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    try
                    {
                        var contract = await db.P_CONTRACTS.FindAsync(model.ID);
                        if (contract == null || contract.IsDeleted == true)
                        {
                            return HttpNotFound();
                        }

                        // Olish standart company agar not provided
                        var defaultCompany = await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync();
                        var companyId = model.CompanyID ?? (defaultCompany?.ID ?? 0);

                        // Yangilash contract header
                        contract.ContractNo = model.ContractNo;
                        contract.IssuedDate = model.IssuedDate;
                        contract.CompanyID = companyId;
                        contract.SupplierID = model.SupplierID;
                        contract.CurrencyID = model.CurrencyID;
                        contract.Incoterms = model.Incoterms;
                        contract.PaymentTerms = model.PaymentTerms;
                        contract.DueDate = model.DueDate;
                        contract.IDN = model.IDN;

                        // O'chirish existing contract products
                        var existingProducts = db.P_CONTRACT_PRODUCTS.Where(cp => cp.ContractID == contract.ID).ToList();
                        foreach (var existing in existingProducts)
                        {
                            db.P_CONTRACT_PRODUCTS.Remove(existing);
                        }

                        // Qo'shish updated contract products
                        if (model.ContractProducts != null && model.ContractProducts.Any())
                        {
                            var conversionService = new UnitConversionService(db);
                            
                            foreach (var product in model.ContractProducts)
                            {
                                // Tekshirish agar product requires cutting
                                bool requiresCutting = await conversionService.RequiresCuttingAsync(product.ProductID);
                                
                                // uchun requires_cutting products, use CBM (UnitID = 11 uchun m3)
                                // uchun other products, use the user-selected unit
                                int unitId;
                                if (requiresCutting)
                                {
                                    unitId = 11; // m3 (cubic meter) uchun CBM
                                }
                                else
                                {
                                    // Use user-selected unit yoki product's standart unit
                                    var prod = await db.PRODUCTS.FindAsync(product.ProductID);
                                    unitId = product.UnitID ?? (prod?.UnitID ?? 1);
                                }

                                var contractProduct = new P_CONTRACT_PRODUCTS
                                {
                                    ContractID = contract.ID,
                                    ProductID = product.ProductID,
                                    Price = product.Price,
                                    Quantity = product.Quantity, // Already in CBM uchun cutting products, yoki user-selected unit uchun others
                                    UnitID = unitId,
                                    MOQ = product.MOQ,
                                    HSCodeID = product.HSCodeID,
                                    ActivePart = true
                                };

                                db.P_CONTRACT_PRODUCTS.Add(contractProduct);
                            }
                        }

                        // Calculate total amount (including VAT agar provided)
                        double totalAmount = 0;
                        int totalIndex = 0;
                        while (Request.Form[$"ContractProducts[{totalIndex}].ProductID"] != null)
                        {
                            var productIDStr = Request.Form[$"ContractProducts[{totalIndex}].ProductID"];
                            if (!string.IsNullOrEmpty(productIDStr) && productIDStr != "0")
                            {
                                var price = double.Parse(Request.Form[$"ContractProducts[{totalIndex}].Price"] ?? "0");
                                var quantity = double.Parse(Request.Form[$"ContractProducts[{totalIndex}].Quantity"] ?? "0");
                                var vatPercent = double.Parse(Request.Form[$"ContractProducts[{totalIndex}].VatPercent"] ?? "12");
                                if (price > 0 && quantity > 0)
                                {
                                    var baseTotal = price * quantity;
                                    var totalWithVat = baseTotal + (baseTotal * vatPercent / 100);
                                    totalAmount += totalWithVat;
                                }
                            }
                            totalIndex++;
                        }
                        contract.Amount = totalAmount;

                        await db.SaveChangesAsync();

                        // Handle fayl upload (replace existing agar new fayl uploaded)
                        if (contractFile != null && contractFile.ContentLength > 0)
                        {
                            // O'chirish existing docs
                            var existingDocs = db.P_CONTRACT_DOCS.Where(cd => cd.ContractID == contract.ID).ToList();
                            foreach (var doc in existingDocs)
                            {
                                db.P_CONTRACT_DOCS.Remove(doc);
                            }

                            // Qo'shish new doc
                            byte[] fileData = null;
                            using (var binaryReader = new BinaryReader(contractFile.InputStream))
                            {
                                fileData = binaryReader.ReadBytes(contractFile.ContentLength);
                            }

                            var contractDoc = new P_CONTRACT_DOCS
                            {
                                ContractID = contract.ID,
                                Doc = fileData
                            };

                            db.P_CONTRACT_DOCS.Add(contractDoc);
                        }

                        await db.SaveChangesAsync();

                        // Log the action
                        LogHelper.LogToDatabase(User.Identity.Name, "PContracts", $"Edit - {contract.ContractNo} shartnomasi tahrirlandi");

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

                // Olish standart company (birinchi faol company)
                var defaultCompany = await db.COMPANIES.Where(c => c.IsActive != false).FirstOrDefaultAsync();
                ViewBag.DefaultCompanyID = defaultCompany?.ID ?? 0;

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname", model.CurrencyID
                );

                // Aniqlash purchase type dan query string yoki form
                byte purchaseType = 0;
                if (Request.QueryString["purchaseType"] == "Indirect" || Request.Form["PurchaseType"] == "Indirect")
                {
                    purchaseType = 1;
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

                ViewBag.HSCodes = new SelectList(
                    await db.HSCODES.ToListAsync(),
                    "ID", "HSCODE1"
                );
            }

            return View(model);
        }

        // Olish so'rovi: PContracts/o'chirish/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "PContracts"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var contract = await db.P_CONTRACTS
                    .Include(c => c.SUPPLIER)
                    .Include(c => c.COMPANy)
                    .Include(c => c.CURRENCy)
                    .Include(c => c.P_CONTRACT_PRODUCTS.Select(p => p.PRODUCT))
                    .FirstOrDefaultAsync(c => c.ID == id && c.IsDeleted != true);

                if (contract == null)
                {
                    return HttpNotFound();
                }

                return View(contract);
            }
        }

        // POST so'rovi: PContracts/o'chirish/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            if (!tahsinERP.Models.RoleHelper.IsChangePermitted(User.Identity.Name, "PContracts"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var contract = await db.P_CONTRACTS.FindAsync(id);
                if (contract != null)
                {
                    contract.IsDeleted = true;
                    await db.SaveChangesAsync();

                    // Log the action
                    LogHelper.LogToDatabase(User.Identity.Name, "PContracts", $"Delete - {contract.ContractNo} shartnomasi o'chirildi");
                }

                return RedirectToAction("Index");
            }
        }

        // Olish so'rovi: PContracts/DownloadDoc/5
        public async Task<ActionResult> DownloadDoc(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var doc = await db.P_CONTRACT_DOCS.FindAsync(id);
                if (doc == null || doc.Doc == null)
                {
                    return HttpNotFound();
                }

                return File(doc.Doc, "application/pdf", $"Contract_{doc.ContractID}.pdf");
            }
        }
    }

    // ViewModels
    public class PContractCreateViewModel
    {
        public string ContractNo { get; set; }
        public DateTime IssuedDate { get; set; } = DateTime.Now;
        public int? CompanyID { get; set; }
        public int SupplierID { get; set; }
        public int CurrencyID { get; set; }
        public double? Amount { get; set; }
        public string Incoterms { get; set; }
        public string PaymentTerms { get; set; }
        public DateTime DueDate { get; set; }
        public string IDN { get; set; }
        public List<PContractProductViewModel> ContractProducts { get; set; }

        public PContractCreateViewModel()
        {
            ContractProducts = new List<PContractProductViewModel>();
        }
    }

    public class PContractEditViewModel
    {
        public int ID { get; set; }
        public string ContractNo { get; set; }
        public DateTime IssuedDate { get; set; }
        public int? CompanyID { get; set; }
        public int SupplierID { get; set; }
        public int CurrencyID { get; set; }
        public double? Amount { get; set; }
        public string Incoterms { get; set; }
        public string PaymentTerms { get; set; }
        public DateTime DueDate { get; set; }
        public string IDN { get; set; }
        public List<PContractProductViewModel> ContractProducts { get; set; }

        public PContractEditViewModel()
        {
            ContractProducts = new List<PContractProductViewModel>();
        }
    }

    public class PContractProductViewModel
    {
        public int? ID { get; set; }
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public double Price { get; set; }
        public double Quantity { get; set; }
        public double Amount { get; set; }
        public int? UnitID { get; set; }
        public double? MOQ { get; set; }
        public int? HSCodeID { get; set; }
        
        // uchun display: indicates agar product requires cutting (uses CBM)
        public bool RequiresCutting { get; set; }
    }
}
