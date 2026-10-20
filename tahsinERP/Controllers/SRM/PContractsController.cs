using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.Services.Procurement;
using tahsinERP.ViewModels;
using tahsinERP.ViewModels.PContract;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    public class PContractsController : BaseController
    {
        private const string Module = "PContracts";

        // Olish so'rovi: PContracts — sahifa faqat filtr va jadval qobig'i; qatorlar IndexData orqali (SRM-P03)
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
                ViewBag.Suppliers = ProcurementLookups.Suppliers(supplierID);
                ViewBag.Contracts = await ProcurementLookups.AllContractsAsync(db, contractID);
            }

            ViewBag.PurchaseTypeFilter = purchaseTypeFilter;
            ViewBag.SupplierID = supplierID;
            ViewBag.ContractID = contractID;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.ChangePermit = CanChange(Module);
            return View();
        }

        // Olish so'rovi: PContracts/IndexData (DataTables server-side)
        [HttpGet]
        public async Task<ActionResult> IndexData(string purchaseTypeFilter = "", int? supplierID = null, int? contractID = null,
            DateTime? fromDate = null, DateTime? toDate = null)
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, Module, "Index"))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 5);
            using (var db = new DBTHSNEntities())
            {
                var result = await ServerTablePage.LoadAsync(
                    PContractService.IndexQuery(db, purchaseTypeFilter, supplierID, contractID, fromDate, toDate), page,
                    PContractService.SearchIndex, PContractService.OrderIndex);

                return Json(new
                {
                    draw = page.Draw,
                    recordsTotal = result.Total,
                    recordsFiltered = result.Filtered,
                    data = result.Rows.Select(r => new
                    {
                        id = r.ID,
                        contractNo = r.ContractNo,
                        supplierId = r.SupplierID,
                        supplier = r.SupplierName,
                        indirect = r.PurchaseType == ProcurementLookups.IndirectPurchase,
                        amount = (r.Amount ?? 0).ToString("N2"),
                        paymentTerms = r.PaymentTerms,
                        date = r.IssuedDate.ToString("dd.MM.yyyy")
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // Olish so'rovi: PContracts/ExportExcel — filtr va qidiruv bo'yicha barcha yozuvlar
        [HttpGet]
        public async Task<ActionResult> ExportExcel(string purchaseTypeFilter = "", int? supplierID = null, int? contractID = null,
            DateTime? fromDate = null, DateTime? toDate = null, string q = null)
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, Module, "Index"))
            {
                return NotAuthorized();
            }

            using (var db = new DBTHSNEntities())
            {
                var query = PContractService.IndexQuery(db, purchaseTypeFilter, supplierID, contractID, fromDate, toDate);
                if (!string.IsNullOrWhiteSpace(q))
                {
                    query = PContractService.SearchIndex(query, q.Trim());
                }

                var rows = await PContractService.OrderIndex(query, 5, true).ToListAsync();
                var bytes = ExcelExport.Create("Xarid shartnomalari", rows,
                    ExcelExport.Col<PContractService.IndexRow>("Shartnoma raqami", r => r.ContractNo),
                    ExcelExport.Col<PContractService.IndexRow>("Ta'minotchi", r => r.SupplierName),
                    ExcelExport.Col<PContractService.IndexRow>("Turi", r => r.PurchaseType == ProcurementLookups.IndirectPurchase ? "Indirect" : "Direct"),
                    ExcelExport.Col<PContractService.IndexRow>("Shartnoma summasi", r => r.Amount ?? 0, "#,##0.00"),
                    ExcelExport.Col<PContractService.IndexRow>("To'lov shartlari", r => r.PaymentTerms),
                    ExcelExport.Col<PContractService.IndexRow>("Sana", r => r.IssuedDate, "dd.MM.yyyy"));

                return File(bytes, ExcelExport.ContentType, $"Xarid_shartnomalari_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
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

                ViewBag.ContractedTotal = (contract.Amount ?? 0);
                ViewBag.OrderedTotal = await ordersQuery.SumAsync(o => o.Amount) ?? 0;
                ViewBag.InvoicedTotal = linkedInvoices.Sum(i => i.Amount) ?? 0;

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
                        var contract = await new PContractService(db, ModelState)
                            .CreateAsync(model, ResolveRequestedPurchaseType(), contractFile);
                        if (contract != null)
                        {
                            LogHelper.LogToDatabase(CurrentUserName, Module, $"Create - {contract.ContractNo} shartnomasi yaratildi");
                            return RedirectToAction("Index");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddDbErrors(ex);
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

                        if (await new PContractService(db, ModelState).UpdateAsync(contract, model, contractFile))
                        {
                            LogHelper.LogToDatabase(CurrentUserName, Module, $"Edit - {contract.ContractNo} shartnomasi tahrirlandi");

                            return RedirectToAction("Index");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddDbErrors(ex);
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
                var contract = await new PContractService(db, ModelState).SoftDeleteAsync(id);
                if (contract != null)
                {
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
    }
}
