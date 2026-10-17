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
using tahsinERP.Services.Common;
using tahsinERP.Services.Procurement;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    public class PContractsController : BaseController
    {
        private const string Module = "PContracts";

        /// <summary>UNITS.ID: m3 — kesiladigan mahsulotlar shartnomada CBM'da yuritiladi.</summary>
        private const int CubicMeterUnitId = 11;

        // Olish so'rovi: PContracts
        public async Task<ActionResult> Index(
            string purchaseTypeFilter = "",
            int? supplierID = null,
            int? contractID = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, Module, "Index"))
            {
                return NotAuthorized();
            }

            using (var db = new DBTHSNEntities())
            {
                // Ro'yxat faqat SUPPLIER navigatsiyasini ishlatadi; kuzatuvsiz (SRM-P03)
                var query = db.P_CONTRACTS
                    .AsNoTracking()
                    .Include(c => c.SUPPLIER)
                    .Where(c => c.IsDeleted != true);

                if (supplierID.HasValue && supplierID.Value > 0)
                {
                    query = query.Where(c => c.SupplierID == supplierID.Value);
                }

                if (contractID.HasValue && contractID.Value > 0)
                {
                    query = query.Where(c => c.ID == contractID.Value);
                }

                if (!string.IsNullOrEmpty(purchaseTypeFilter))
                {
                    byte purchaseType = purchaseTypeFilter == "Indirect" ? ProcurementLookups.IndirectPurchase : ProcurementLookups.DirectPurchase;
                    query = query.Where(c => c.PurchaseType == purchaseType);
                }

                if (fromDate.HasValue)
                {
                    var from = fromDate.Value.Date;
                    query = query.Where(c => c.IssuedDate >= from);
                }
                if (toDate.HasValue)
                {
                    var toExclusive = toDate.Value.Date.AddDays(1);
                    query = query.Where(c => c.IssuedDate < toExclusive);
                }

                ViewBag.Suppliers = ProcurementLookups.Suppliers(supplierID);
                ViewBag.Contracts = await ProcurementLookups.AllContractsAsync(db, contractID);

                var contracts = await query
                    .OrderByDescending(c => c.IssuedDate)
                    .ToListAsync();

                ViewBag.PurchaseTypeFilter = purchaseTypeFilter;
                ViewBag.SupplierID = supplierID;
                ViewBag.ContractID = contractID;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.ChangePermit = CanChange(Module);

                return View(contracts);
            }
        }

        // Olish so'rovi: PContracts/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, Module, "Details"))
            {
                return NotAuthorized();
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                // P_CONTRACT_DOCS (PDF blob) yuklanmaydi — faqat ID'lar (SRM-P04)
                var contract = await db.P_CONTRACTS
                    .AsNoTracking()
                    .Include(c => c.SUPPLIER)
                    .Include(c => c.COMPANy)
                    .Include(c => c.CURRENCy)
                    .Include(c => c.P_CONTRACT_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(c => c.P_CONTRACT_PRODUCTS.Select(p => p.UNIT))
                    .FirstOrDefaultAsync(c => c.ID == id && c.IsDeleted != true);

                if (contract == null)
                {
                    return HttpNotFound();
                }

                ViewBag.DocIds = await db.P_CONTRACT_DOCS
                    .Where(d => d.ContractID == contract.ID)
                    .Select(d => d.ID)
                    .ToListAsync();

                var linkedInvoices = await db.P_INVOICES
                    .AsNoTracking()
                    .Include(i => i.CURRENCy)
                    .Where(i => i.ContractID == id && i.IsDeleted != true)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync();
                ViewBag.LinkedInvoices = linkedInvoices;
                ViewBag.InvoicesCount = linkedInvoices.Count;

                var ordersQuery = db.P_ORDERS.Where(o => o.ContractID == id && o.IsDeleted != true);
                ViewBag.OrdersCount = await ordersQuery.CountAsync();

                ViewBag.ContractedTotal = contract.Amount ?? 0;
                ViewBag.OrderedTotal = await ordersQuery.SumAsync(o => (double?)o.Amount) ?? 0;
                ViewBag.InvoicedTotal = linkedInvoices.Sum(i => (double?)i.Amount) ?? 0;

                return View(contract);
            }
        }

        // Olish so'rovi: PContracts/yaratish
        public async Task<ActionResult> Create()
        {
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            using (var db = new DBTHSNEntities())
            {
                await PopulateFormLookupsAsync(db, null, null, ResolveRequestedPurchaseType(), null);
                return View();
            }
        }

        // POST so'rovi: PContracts/yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(PContractCreateViewModel model, HttpPostedFileBase contractFile)
        {
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            // Faqat haqiqiy PDF qabul qilinadi (SRM-B23)
            var fileError = PdfUpload.Validate(contractFile);
            if (fileError != null)
            {
                ModelState.AddModelError("", fileError);
            }

            if (ModelState.IsValid)
            {
                using (var db = new DBTHSNEntities())
                {
                    try
                    {
                        P_CONTRACTS contract = null;

                        // Sarlavha, qatorlar, hujjat — bitta tranzaksiyada (SRM-B12)
                        using (var tx = db.Database.BeginTransaction())
                        {
                            var number = await ResolveContractNoAsync(db, model.ContractNo, model.SupplierID, excludeId: null);
                            if (number != null)
                            {
                                contract = new P_CONTRACTS
                                {
                                    ContractNo = number,
                                    IssuedDate = model.IssuedDate,
                                    CompanyID = model.CompanyID ?? await ProcurementLookups.GetDefaultCompanyIdAsync(db),
                                    SupplierID = model.SupplierID,
                                    CurrencyID = model.CurrencyID,
                                    Incoterms = model.Incoterms,
                                    PaymentTerms = model.PaymentTerms,
                                    DueDate = model.DueDate,
                                    IDN = model.IDN,
                                    IsDeleted = false,
                                    PurchaseType = ResolveRequestedPurchaseType()
                                };
                                db.P_CONTRACTS.Add(contract);

                                // Jami (QQS bilan) — bog'langan model qatorlaridan (avval Request.Form qayta o'qilardi, SRM-D10)
                                contract.Amount = await AddLinesAsync(db, contract, model.ContractProducts);
                                AddDocument(db, contract, contractFile);

                                await db.SaveChangesAsync();
                                tx.Commit();
                            }
                        }

                        if (contract != null)
                        {
                            LogHelper.LogToDatabase(CurrentUserName, Module, $"Create - {contract.ContractNo} shartnomasi yaratildi");
                            return RedirectToAction("Index");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", FormatError(ex));
                    }
                }
            }

            using (var db = new DBTHSNEntities())
            {
                await PopulateFormLookupsAsync(db, model.SupplierID, model.CurrencyID, ResolveRequestedPurchaseType(),
                    model.ContractProducts?.Select(cp => cp.ProductID));
            }

            return View(model);
        }

        // Olish so'rovi: PContracts/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var contract = await db.P_CONTRACTS
                    .AsNoTracking()
                    .Include(c => c.P_CONTRACT_PRODUCTS.Select(cp => cp.PRODUCT))
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
                        ProductID = cp.ProductID,
                        ProductPNo = cp.PRODUCT.PNo,
                        ProductName = cp.PRODUCT.Name,
                        Price = cp.Price,
                        Quantity = cp.Quantity,
                        Amount = cp.Amount,
                        UnitID = cp.UnitID,
                        MOQ = cp.MOQ,
                        HSCodeID = cp.HSCodeID,
                        VatPercent = VatMath.PercentFromLine(cp.Amount, cp.VATAmount)
                    }).ToList()
                };

                await PopulateFormLookupsAsync(db, model.SupplierID, model.CurrencyID, contract.PurchaseType,
                    contract.P_CONTRACT_PRODUCTS.Select(cp => cp.ProductID));

                return View(model);
            }
        }

        // POST so'rovi: PContracts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(PContractEditViewModel model, HttpPostedFileBase contractFile)
        {
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            // Faqat haqiqiy PDF qabul qilinadi (SRM-B23)
            var fileError = PdfUpload.Validate(contractFile);
            if (fileError != null)
            {
                ModelState.AddModelError("", fileError);
            }

            // ContractProducts bo'sh bo'lsa, qator validatsiya xatolari o'rniga bitta umumiy xabar
            if (model.ContractProducts == null || !model.ContractProducts.Any())
            {
                var keysToRemove = ModelState.Keys.Where(k => k.StartsWith("ContractProducts")).ToList();
                foreach (var key in keysToRemove)
                {
                    ModelState.Remove(key);
                }
                ModelState.AddModelError("", "Kamida bitta mahsulot qo'shish kerak");
            }

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

                        var number = await ResolveContractNoAsync(db, model.ContractNo, model.SupplierID, excludeId: contract.ID);
                        if (number != null)
                        {
                            using (var tx = db.Database.BeginTransaction())
                            {
                                contract.ContractNo = number;
                                contract.IssuedDate = model.IssuedDate;
                                contract.CompanyID = model.CompanyID ?? await ProcurementLookups.GetDefaultCompanyIdAsync(db);
                                contract.SupplierID = model.SupplierID;
                                contract.CurrencyID = model.CurrencyID;
                                contract.Incoterms = model.Incoterms;
                                contract.PaymentTerms = model.PaymentTerms;
                                contract.DueDate = model.DueDate;
                                contract.IDN = model.IDN;

                                var existingProducts = await db.P_CONTRACT_PRODUCTS.Where(cp => cp.ContractID == contract.ID).ToListAsync();
                                db.P_CONTRACT_PRODUCTS.RemoveRange(existingProducts);

                                contract.Amount = await AddLinesAsync(db, contract, model.ContractProducts);

                                if (PdfUpload.HasFile(contractFile))
                                {
                                    // Yangi hujjat eskisining o'rniga (eski blob'lar xotiraga yuklanmaydi)
                                    await db.Database.ExecuteSqlCommandAsync("DELETE FROM P_CONTRACT_DOCS WHERE ContractID = @p0", contract.ID);
                                    AddDocument(db, contract, contractFile);
                                }

                                await db.SaveChangesAsync();
                                tx.Commit();
                            }

                            LogHelper.LogToDatabase(CurrentUserName, Module, $"Edit - {contract.ContractNo} shartnomasi tahrirlandi");

                            return RedirectToAction("Index");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", FormatError(ex));
                    }
                }
            }

            using (var db = new DBTHSNEntities())
            {
                // Xarid turi saqlangan shartnomadan, topilmasa — formadagi belgidan
                var storedType = await db.P_CONTRACTS
                    .Where(c => c.ID == model.ID)
                    .Select(c => (byte?)c.PurchaseType)
                    .FirstOrDefaultAsync();

                await PopulateFormLookupsAsync(db, model.SupplierID, model.CurrencyID, storedType ?? ResolveRequestedPurchaseType(),
                    model.ContractProducts?.Select(cp => cp.ProductID));
            }

            return View(model);
        }

        // Olish so'rovi: PContracts/o'chirish/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var contract = await db.P_CONTRACTS
                    .AsNoTracking()
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
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            using (var db = new DBTHSNEntities())
            {
                var contract = await db.P_CONTRACTS.FindAsync(id);
                if (contract != null && contract.IsDeleted != true)
                {
                    contract.IsDeleted = true;
                    await SoftDeleteReferenceService.AfterPContractSoftDeletedAsync(db, id);
                    await db.SaveChangesAsync();

                    LogHelper.LogToDatabase(CurrentUserName, Module, $"Delete - {contract.ContractNo} shartnomasi o'chirildi");
                }

                return RedirectToAction("Index");
            }
        }

        // Olish so'rovi: PContracts/DownloadDoc/5
        [Permission(Module)]
        public async Task<ActionResult> DownloadDoc(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                // O'chirilgan shartnomaning hujjati berilmaydi (SRM-B11)
                var doc = await db.P_CONTRACT_DOCS
                    .Where(d => d.ID == id && d.P_CONTRACTS.IsDeleted != true)
                    .Select(d => new { d.ContractID, d.Doc })
                    .FirstOrDefaultAsync();
                if (doc == null || doc.Doc == null)
                {
                    return HttpNotFound();
                }

                return File(doc.Doc, "application/pdf", $"Contract_{doc.ContractID}.pdf");
            }
        }

        // ---------- yordamchilar ----------

        /// <summary>Create/Edit formasi spravochniklari (GET va xato yo'llari uchun bitta joy, SRM-D02).</summary>
        private async Task PopulateFormLookupsAsync(DBTHSNEntities db, int? supplierId, int? currencyId, byte purchaseType, IEnumerable<int> ensureProductIds)
        {
            ViewBag.Suppliers = ProcurementLookups.Suppliers(supplierId > 0 ? supplierId : null);
            ViewBag.Currencies = ProcurementLookups.Currencies(currencyId > 0 ? currencyId : null);
            ViewBag.DefaultCompanyID = await ProcurementLookups.GetDefaultCompanyIdAsync(db);
            ViewBag.PurchaseType = purchaseType;

            var products = await ProcurementLookups.GetPurchasableProductsAsync(db, purchaseType, ensureProductIds);
            ViewBag.PurchasableProducts = ProcurementLookups.ProductSelectList(products);
            ViewBag.Units = ProcurementLookups.Units();
            ViewBag.HSCodes = ProcurementLookups.HSCodes();
        }

        /// <summary>Formadagi/URL'dagi xarid turi belgisi (Indirect bo'lsa 1).</summary>
        private byte ResolveRequestedPurchaseType()
        {
            return Request.QueryString["purchaseType"] == "Indirect" || Request.Form["PurchaseType"] == "Indirect"
                ? ProcurementLookups.IndirectPurchase
                : ProcurementLookups.DirectPurchase;
        }

        /// <summary>
        /// Shartnoma raqami: bo'sh bo'lsa PC-{yil}-NNNN (qulf bilan, o'chirilganlar ham hisobga olinadi — SRM-B14);
        /// qo'lda kiritilsa — shu ta'minotchining faol shartnomalarida takrorlanmasligi tekshiriladi.
        /// Avtomatik raqam tranzaksiya ichida chaqirilishi kerak. Xato bo'lsa null (ModelState'ga yoziladi).
        /// </summary>
        private async Task<string> ResolveContractNoAsync(DBTHSNEntities db, string requested, int supplierId, int? excludeId)
        {
            var number = string.IsNullOrWhiteSpace(requested) ? null : requested.Trim();
            if (number == null)
            {
                if (excludeId.HasValue)
                {
                    // Tahrirlashda bo'sh qoldirilsa — mavjud raqam saqlanadi
                    return await db.P_CONTRACTS.Where(c => c.ID == excludeId.Value).Select(c => c.ContractNo).FirstOrDefaultAsync();
                }

                return await DocumentNumberService.NextAsync(db, "P_CONTRACTS", "ContractNo", $"PC-{DateTime.Now.Year}-", 4);
            }

            bool duplicate = await db.P_CONTRACTS.AnyAsync(c =>
                c.IsDeleted != true &&
                c.SupplierID == supplierId &&
                c.ContractNo == number &&
                (!excludeId.HasValue || c.ID != excludeId.Value));
            if (duplicate)
            {
                ModelState.AddModelError("ContractNo", "Bu ta'minotchida shu raqamli shartnoma allaqachon mavjud.");
                return null;
            }

            return number;
        }

        /// <summary>
        /// Shartnoma qatorlarini qo'shadi va QQS bilan jami summani qaytaradi.
        /// Mahsulot birligi va "kesiladimi" belgisi bitta so'rovda olinadi (avval har qatorga 2 so'rov, SRM-P06).
        /// </summary>
        private static async Task<double> AddLinesAsync(DBTHSNEntities db, P_CONTRACTS contract, IEnumerable<PContractProductViewModel> lines)
        {
            var list = (lines ?? Enumerable.Empty<PContractProductViewModel>()).ToList();
            if (list.Count == 0)
            {
                return 0;
            }

            var productIds = list.Select(l => l.ProductID).Distinct().ToList();
            var products = await db.PRODUCTS.AsNoTracking()
                .Where(p => productIds.Contains(p.ID))
                .Select(p => new
                {
                    p.ID,
                    p.UnitID,
                    RequiresCutting = p.CategoryID.HasValue && p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.RequiresCutting
                })
                .ToDictionaryAsync(p => p.ID);

            double total = 0;
            foreach (var line in list)
            {
                var vatPercent = VatMath.ClampPercent(line.VatPercent);
                var lineAmount = line.Price * line.Quantity;
                var vatAmount = VatMath.AmountFromPercent(lineAmount, vatPercent);

                // Kesiladigan mahsulotlar CBM'da (m3), qolganlari — tanlangan birlikda yoki mahsulot birligida
                var product = products.ContainsKey(line.ProductID) ? products[line.ProductID] : null;
                int unitId = product != null && product.RequiresCutting
                    ? CubicMeterUnitId
                    : line.UnitID ?? (product?.UnitID ?? 1);

                db.P_CONTRACT_PRODUCTS.Add(new P_CONTRACT_PRODUCTS
                {
                    P_CONTRACTS = contract,
                    ProductID = line.ProductID,
                    Price = line.Price,
                    Quantity = line.Quantity, // kesiladiganlarda allaqachon CBM'da
                    UnitID = unitId,
                    MOQ = line.MOQ,
                    HSCodeID = line.HSCodeID,
                    ActivePart = true,
                    VATAmount = vatAmount
                });

                // Avvalgi qoida saqlandi: jamiga faqat mahsuloti tanlangan, narx va miqdori musbat qatorlar kiradi
                if (line.ProductID > 0 && line.Price > 0 && line.Quantity > 0)
                {
                    total += lineAmount + vatAmount;
                }
            }

            return total;
        }

        private static void AddDocument(DBTHSNEntities db, P_CONTRACTS contract, HttpPostedFileBase file)
        {
            if (!PdfUpload.HasFile(file))
            {
                return;
            }

            db.P_CONTRACT_DOCS.Add(new P_CONTRACT_DOCS { P_CONTRACTS = contract, Doc = PdfUpload.Read(file) });
        }

        private static string FormatError(Exception ex)
        {
            var message = $"Xatolik: {ex.Message}";
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
            {
                message += $" | Ichki xatolik: {inner.Message}";
            }
            return message;
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
        public double VatPercent { get; set; }
        
        // uchun display: indicates agar product requires cutting (uses CBM)
        public bool RequiresCutting { get; set; }
    }
}
