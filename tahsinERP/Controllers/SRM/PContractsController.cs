using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    public class PContractsController : Controller
    {
        // GET: PContracts
        public async Task<ActionResult> Index()
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "PContracts", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var contracts = await db.P_CONTRACTS
                    .Include(c => c.SUPPLIER)
                    .Include(c => c.COMPANy)
                    .Include(c => c.CURRENCy)
                    .Where(c => c.IsDeleted != true)
                    .OrderByDescending(c => c.IssuedDate)
                    .ToListAsync();

                return View(contracts);
            }
        }

        // GET: PContracts/Details/5
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

                return View(contract);
            }
        }

        // GET: PContracts/Create
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

                ViewBag.Companies = new SelectList(
                    await db.COMPANIES.Where(c => c.IsActive != false).ToListAsync(),
                    "ID", "Name"
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname"
                );

                var purchasableProducts = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => p.IsDeleted != true && 
                                (p.PRODUCT_CATEGORIES == null || 
                                 (p.PRODUCT_CATEGORIES.IsFinishedGood != true && 
                                  (p.PRODUCT_CATEGORIES.AllowPurchase == true || p.IsRawMaterial == true))))
                    .OrderBy(p => p.PNo)
                    .ToListAsync();

                ViewBag.PurchasableProducts = new SelectList(purchasableProducts, "ID", "PNo");

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

        // POST: PContracts/Create
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
                        // Generate ContractNo if not provided
                        if (string.IsNullOrEmpty(model.ContractNo))
                        {
                            model.ContractNo = await GenerateContractNo(db);
                        }

                        // Create contract header
                        var contract = new P_CONTRACTS
                        {
                            ContractNo = model.ContractNo,
                            IssuedDate = model.IssuedDate,
                            CompanyID = model.CompanyID,
                            SupplierID = model.SupplierID,
                            CurrencyID = model.CurrencyID,
                            Amount = 0,
                            Incoterms = model.Incoterms,
                            PaymentTerms = model.PaymentTerms,
                            DueDate = model.DueDate,
                            IDN = model.IDN,
                            IsDeleted = false
                        };

                        db.P_CONTRACTS.Add(contract);
                        await db.SaveChangesAsync();

                        // Add contract products
                        double totalAmount = 0;
                        if (model.ContractProducts != null && model.ContractProducts.Any())
                        {
                            foreach (var product in model.ContractProducts)
                            {
                                var contractProduct = new P_CONTRACT_PRODUCTS
                                {
                                    ContractID = contract.ID,
                                    PartID = product.ProductID,
                                    Price = product.Price,
                                    Quantity = product.Quantity,
                                    Amount = product.Price * product.Quantity,
                                    UnitID = product.UnitID,
                                    MOQ = product.MOQ,
                                    HSCodeID = product.HSCodeID,
                                    ActivePart = true
                                };

                                db.P_CONTRACT_PRODUCTS.Add(contractProduct);
                                totalAmount += contractProduct.Amount;
                            }

                            contract.Amount = totalAmount;
                            await db.SaveChangesAsync();
                        }

                        // Upload contract document if provided
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
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }

            // Repopulate ViewBag on error
            using (var db = new DBTHSNEntities())
            {
                ViewBag.Suppliers = new SelectList(
                    await db.SUPPLIERS.Where(s => s.IsDeleted != true).OrderBy(s => s.Name).ToListAsync(),
                    "ID", "Name", model.SupplierID
                );

                ViewBag.Companies = new SelectList(
                    await db.COMPANIES.Where(c => c.IsActive != false).ToListAsync(),
                    "ID", "Name", model.CompanyID
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname", model.CurrencyID
                );

                var purchasableProducts = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => p.IsDeleted != true && 
                                (p.PRODUCT_CATEGORIES == null || 
                                 (p.PRODUCT_CATEGORIES.IsFinishedGood != true && 
                                  (p.PRODUCT_CATEGORIES.AllowPurchase == true || p.IsRawMaterial == true))))
                    .OrderBy(p => p.PNo)
                    .ToListAsync();

                ViewBag.PurchasableProducts = new SelectList(purchasableProducts, "ID", "PNo");

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

        // Generate ContractNo
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

        // GET: PContracts/Edit/5
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
                        ProductID = cp.PartID,
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

                ViewBag.Companies = new SelectList(
                    await db.COMPANIES.Where(c => c.IsActive != false).ToListAsync(),
                    "ID", "Name", model.CompanyID
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname", model.CurrencyID
                );

                var purchasableProducts = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => p.IsDeleted != true && 
                                (p.PRODUCT_CATEGORIES == null || 
                                 (p.PRODUCT_CATEGORIES.IsFinishedGood != true && 
                                  (p.PRODUCT_CATEGORIES.AllowPurchase == true || p.IsRawMaterial == true))))
                    .OrderBy(p => p.PNo)
                    .ToListAsync();

                ViewBag.PurchasableProducts = new SelectList(purchasableProducts, "ID", "PNo");

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

        // POST: PContracts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(PContractEditViewModel model, HttpPostedFileBase contractFile)
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
                        var contract = await db.P_CONTRACTS.FindAsync(model.ID);
                        if (contract == null || contract.IsDeleted == true)
                        {
                            return HttpNotFound();
                        }

                        // Update contract header
                        contract.ContractNo = model.ContractNo;
                        contract.IssuedDate = model.IssuedDate;
                        contract.CompanyID = model.CompanyID;
                        contract.SupplierID = model.SupplierID;
                        contract.CurrencyID = model.CurrencyID;
                        contract.Incoterms = model.Incoterms;
                        contract.PaymentTerms = model.PaymentTerms;
                        contract.DueDate = model.DueDate;
                        contract.IDN = model.IDN;

                        // Remove existing contract products
                        var existingProducts = db.P_CONTRACT_PRODUCTS.Where(cp => cp.ContractID == contract.ID).ToList();
                        foreach (var existing in existingProducts)
                        {
                            db.P_CONTRACT_PRODUCTS.Remove(existing);
                        }

                        // Add updated contract products
                        double totalAmount = 0;
                        if (model.ContractProducts != null && model.ContractProducts.Any())
                        {
                            foreach (var product in model.ContractProducts)
                            {
                                var contractProduct = new P_CONTRACT_PRODUCTS
                                {
                                    ContractID = contract.ID,
                                    PartID = product.ProductID,
                                    Price = product.Price,
                                    Quantity = product.Quantity,
                                    Amount = product.Price * product.Quantity,
                                    UnitID = product.UnitID,
                                    MOQ = product.MOQ,
                                    HSCodeID = product.HSCodeID,
                                    ActivePart = true
                                };

                                db.P_CONTRACT_PRODUCTS.Add(contractProduct);
                                totalAmount += contractProduct.Amount;
                            }
                        }

                        contract.Amount = totalAmount;

                        // Handle file upload (replace existing if new file uploaded)
                        if (contractFile != null && contractFile.ContentLength > 0)
                        {
                            // Remove existing docs
                            var existingDocs = db.P_CONTRACT_DOCS.Where(cd => cd.ContractID == contract.ID).ToList();
                            foreach (var doc in existingDocs)
                            {
                                db.P_CONTRACT_DOCS.Remove(doc);
                            }

                            // Add new doc
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
                        ModelState.AddModelError("", $"Xatolik: {ex.Message}");
                    }
                }
            }

            // Repopulate ViewBag on error
            using (var db = new DBTHSNEntities())
            {
                ViewBag.Suppliers = new SelectList(
                    await db.SUPPLIERS.Where(s => s.IsDeleted != true).OrderBy(s => s.Name).ToListAsync(),
                    "ID", "Name", model.SupplierID
                );

                ViewBag.Companies = new SelectList(
                    await db.COMPANIES.Where(c => c.IsActive != false).ToListAsync(),
                    "ID", "Name", model.CompanyID
                );

                ViewBag.Currencies = new SelectList(
                    await db.CURRENCIES.ToListAsync(),
                    "ID", "Shortname", model.CurrencyID
                );

                var purchasableProducts = await db.PRODUCTS
                    .Include(p => p.PRODUCT_CATEGORIES)
                    .Where(p => p.IsDeleted != true && 
                                (p.PRODUCT_CATEGORIES == null || 
                                 (p.PRODUCT_CATEGORIES.IsFinishedGood != true && 
                                  (p.PRODUCT_CATEGORIES.AllowPurchase == true || p.IsRawMaterial == true))))
                    .OrderBy(p => p.PNo)
                    .ToListAsync();

                ViewBag.PurchasableProducts = new SelectList(purchasableProducts, "ID", "PNo");

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

        // GET: PContracts/Delete/5
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

        // POST: PContracts/Delete/5
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

        // GET: PContracts/DownloadDoc/5
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
        public int CompanyID { get; set; }
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
        public int CompanyID { get; set; }
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
    }
}
