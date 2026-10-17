using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.Services.Procurement;
using tahsinERP.ViewModels;
using tahsinERP.ViewModels.PInvoice;
using tahsinERP.Filters;

namespace tahsinERP.Controllers
{
    public class PInvoicesController : BaseController
    {
        private const string Module = "PInvoices";

        // Olish so'rovi: PInvoices — sahifa faqat filtr va jadval qobig'i; qatorlar IndexData orqali (SRM-P03)
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

        // Olish so'rovi: PInvoices/IndexData (DataTables server-side)
        [HttpGet]
        public async Task<ActionResult> IndexData(string purchaseTypeFilter = "", int? supplierID = null, int? contractID = null,
            DateTime? fromDate = null, DateTime? toDate = null)
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, Module, "Index"))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 6);
            using (var db = new DBTHSNEntities())
            {
                var result = await ServerTablePage.LoadAsync(
                    PInvoiceService.IndexQuery(db, purchaseTypeFilter, supplierID, contractID, fromDate, toDate), page,
                    PInvoiceService.SearchIndex, PInvoiceService.OrderIndex);

                // QQS stavkalari faqat joriy sahifadagi shartnomalar uchun
                var contractIds = result.Rows
                    .Where(r => r.VatContractID.HasValue && r.VatContractID.Value > 0)
                    .Select(r => r.VatContractID.Value)
                    .Distinct()
                    .ToList();
                var vatRates = await PContractVatHelper.GetContractVatRatesAsync(db, contractIds);

                return Json(new
                {
                    draw = page.Draw,
                    recordsTotal = result.Total,
                    recordsFiltered = result.Filtered,
                    data = result.Rows.Select(r =>
                    {
                        double rate;
                        if (!r.VatContractID.HasValue || !vatRates.TryGetValue(r.VatContractID.Value, out rate))
                        {
                            rate = 0d;
                        }

                        var gross = r.Amount ?? 0d;
                        return new
                        {
                            id = r.ID,
                            invoiceNo = r.InvoiceNo,
                            supplierId = r.SupplierID,
                            supplier = r.SupplierName,
                            indirect = r.PurchaseType == ProcurementLookups.IndirectPurchase,
                            orderId = r.OrderID,
                            orderNo = r.OrderNo,
                            contractId = r.ContractID,
                            contractNo = r.ContractNo,
                            amount = gross.ToString("N2"),
                            vatNote = rate > 0 ? $"{(r.Net ?? gross):N2} + QQS {rate:0.##}%" : null,
                            date = r.InvoiceDate.ToString("dd.MM.yyyy")
                        };
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // Olish so'rovi: PInvoices/ExportExcel — filtr va qidiruv bo'yicha barcha yozuvlar
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
                var query = PInvoiceService.IndexQuery(db, purchaseTypeFilter, supplierID, contractID, fromDate, toDate);
                if (!string.IsNullOrWhiteSpace(q))
                {
                    query = PInvoiceService.SearchIndex(query, q.Trim());
                }

                var rows = await PInvoiceService.OrderIndex(query, 6, true).ToListAsync();
                var bytes = ExcelExport.Create("Xarid invoyslari", rows,
                    ExcelExport.Col<PInvoiceService.IndexRow>("Invoys raqami", r => r.InvoiceNo),
                    ExcelExport.Col<PInvoiceService.IndexRow>("Ta'minotchi", r => r.SupplierName),
                    ExcelExport.Col<PInvoiceService.IndexRow>("Turi", r => r.PurchaseType == ProcurementLookups.IndirectPurchase ? "Indirect" : "Direct"),
                    ExcelExport.Col<PInvoiceService.IndexRow>("Buyurtma raqami", r => r.OrderNo),
                    ExcelExport.Col<PInvoiceService.IndexRow>("Shartnoma raqami", r => r.ContractNo),
                    ExcelExport.Col<PInvoiceService.IndexRow>("Invoys summasi (QQS bilan)", r => r.Amount ?? 0, "#,##0.00"),
                    ExcelExport.Col<PInvoiceService.IndexRow>("Sana", r => r.InvoiceDate, "dd.MM.yyyy"));

                return File(bytes, ExcelExport.ContentType, $"Xarid_invoyslari_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
        }

        // Olish so'rovi: PInvoices/Details/5
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
                // P_INVOICE_DOCS (PDF blob) yuklanmaydi — faqat ID'lar alohida olinadi (SRM-P04)
                var invoice = await db.P_INVOICES
                    .AsNoTracking()
                    .Include(i => i.SUPPLIER)
                    .Include(i => i.COMPANy)
                    .Include(i => i.CURRENCy)
                    .Include(i => i.P_ORDERS)
                    .Include(i => i.P_CONTRACTS)
                    .Include(i => i.P_ORDERS.P_CONTRACTS)
                    .Include(i => i.P_INVOICE_PRODUCTS.Select(p => p.PRODUCT.PRODUCT_CATEGORIES))
                    .Include(i => i.P_INVOICE_PRODUCTS.Select(p => p.UNIT))
                    .FirstOrDefaultAsync(i => i.ID == id && i.IsDeleted != true);

                if (invoice == null)
                {
                    return HttpNotFound();
                }

                ViewBag.DocIds = await db.P_INVOICE_DOCS
                    .Where(d => d.InvoiceID == invoice.ID)
                    .Select(d => d.ID)
                    .ToListAsync();

                // Shartnoma: to'g'ridan-to'g'ri ContractID, bo'lmasa buyurtma orqali (SRM-B18)
                int? contractId = invoice.ContractID > 0 ? (int?)invoice.ContractID : invoice.P_ORDERS?.ContractID;
                string contractNo = invoice.ContractID > 0
                    ? invoice.P_CONTRACTS?.ContractNo
                    : invoice.P_ORDERS?.P_CONTRACTS?.ContractNo;
                ViewBag.ContractID = contractId;
                ViewBag.ContractNo = contractNo;

                var productIds = invoice.P_INVOICE_PRODUCTS.Select(p => p.ProductID).Distinct().ToList();

                // Shartnoma qoldig'i — barcha mahsulotlar uchun bitta to'plam so'rov (SRM-P05)
                var contractBalances = new Dictionary<int, ContractBalanceInfo>();
                if (contractId.HasValue && contractId.Value > 0)
                {
                    contractBalances = await new ContractBalanceService(db).GetContractBalancesAsync(contractId.Value, productIds);
                }

                // CBM faqat kesiladigan mahsulot donada (UnitID=1) saqlangan bo'lsa hisoblanadi.
                // Agar foydalanuvchi to'g'ridan-to'g'ri m3 kiritgan bo'lsa, qayta aylantirish shart emas.
                var conversionService = new UnitConversionService(db);
                var productCBMInfo = new Dictionary<int, decimal>();
                foreach (var invoiceProduct in invoice.P_INVOICE_PRODUCTS)
                {
                    bool requiresCutting = invoiceProduct.PRODUCT?.PRODUCT_CATEGORIES?.RequiresCutting == true
                                           && invoiceProduct.PRODUCT.CategoryID.HasValue;
                    if (requiresCutting && invoiceProduct.UnitID == 1)
                    {
                        try
                        {
                            productCBMInfo[invoiceProduct.ProductID] = await conversionService.ConvertPieceToCBMAsync(
                                invoiceProduct.ProductID,
                                (decimal)invoiceProduct.Quantity);
                        }
                        catch
                        {
                            productCBMInfo[invoiceProduct.ProductID] = 0;
                        }
                    }
                }

                ViewBag.ContractBalances = contractBalances;
                ViewBag.ProductCBMInfo = productCBMInfo;
                ViewBag.ContractVatRate = await PContractVatHelper.CalculateContractVatRateAsync(db, contractId);
                ViewBag.NetAmount = invoice.P_INVOICE_PRODUCTS.Sum(p => p.TotalPrice);

                return View(invoice);
            }
        }

        // Olish so'rovi: PInvoices/yaratish
        public async Task<ActionResult> Create()
        {
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            using (var db = new DBTHSNEntities())
            {
                // Buyurtmadan yaratilsa (query string) — xarid turi buyurtmadan
                byte purchaseType = ProcurementLookups.DirectPurchase;
                int orderId;
                if (int.TryParse(Request.QueryString["orderId"], out orderId))
                {
                    purchaseType = await db.P_ORDERS
                        .Where(o => o.ID == orderId)
                        .Select(o => (byte?)o.PurchaseType)
                        .FirstOrDefaultAsync() ?? ProcurementLookups.DirectPurchase;
                }

                await PopulateFormLookupsAsync(db, new PInvoiceEditViewModel(), purchaseType, includeContracts: false);
                return View();
            }
        }

        // POST so'rovi: PInvoices/LoadOrderProducts - yuklash products dan P_ORDER
        [HttpPost]
        [ValidateAntiForgeryHeader]
        [Permission(Module)]
        public async Task<JsonResult> LoadOrderProducts(int orderId)
        {
            using (var db = new DBTHSNEntities())
            {
                var orderProducts = await db.P_ORDER_PRODUCTS
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
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            // Faqat haqiqiy PDF qabul qilinadi (SRM-B23)
            var fileError = PdfUpload.Validate(invoiceFile);
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
                        var invoice = await new PInvoiceService(db, ModelState)
                            .CreateAsync(model, Request.Form["PurchaseType"] == "Indirect", invoiceFile);
                        if (invoice != null)
                        {
                            // Finance'ga kreditor qarzi sifatida yozish. Avval xato jim yutilardi (SRM-B13).
                            PostToFinance(db, invoice.ID, "Create");

                            LogHelper.LogToDatabase(CurrentUserName, Module, $"Create - {invoice.InvoiceNo} invoysi yaratildi");

                            return RedirectToAction("Index");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", ProcurementErrors.Format(ex));
                    }
                }
            }

            using (var db = new DBTHSNEntities())
            {
                byte purchaseType = await new PInvoiceService(db, ModelState).ResolvePurchaseTypeAsync(model.ContractID, model.OrderID);
                var lookupModel = new PInvoiceEditViewModel
                {
                    SupplierID = model.SupplierID,
                    OrderID = model.OrderID,
                    ContractID = model.ContractID,
                    CurrencyID = model.CurrencyID,
                    InvoiceProducts = model.InvoiceProducts
                };
                await PopulateFormLookupsAsync(db, lookupModel, purchaseType, includeContracts: true);
            }

            return View(model);
        }

        // Olish so'rovi: PInvoices/Edit/5
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
                var invoice = await db.P_INVOICES
                    .AsNoTracking()
                    .Include(i => i.P_INVOICE_PRODUCTS.Select(p => p.PRODUCT))
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
                    // Formada "Jami" QQSsiz ko'rsatiladi (Amount bazada QQS bilan)
                    Amount = invoice.P_INVOICE_PRODUCTS.Sum(ip => ip.TotalPrice),
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

                byte purchaseType = await new PInvoiceService(db, ModelState).ResolvePurchaseTypeAsync(model.ContractID, model.OrderID);
                await PopulateFormLookupsAsync(db, model, purchaseType, includeContracts: true);

                return View(model);
            }
        }

        // POST so'rovi: PInvoices/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(PInvoiceEditViewModel model, HttpPostedFileBase invoiceFile)
        {
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            // Faqat haqiqiy PDF qabul qilinadi (SRM-B23)
            var fileError = PdfUpload.Validate(invoiceFile);
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
                        var invoice = await db.P_INVOICES.FindAsync(model.ID);
                        if (invoice == null || invoice.IsDeleted == true)
                        {
                            return HttpNotFound();
                        }

                        if (await new PInvoiceService(db, ModelState).UpdateAsync(invoice, model, invoiceFile))
                        {
                            // Finance yozuvini yangilash (upsert) — endi to'g'ri summa bilan
                            PostToFinance(db, invoice.ID, "Edit");

                            LogHelper.LogToDatabase(CurrentUserName, Module, $"Edit - {invoice.InvoiceNo} invoysi tahrirlandi");

                            return RedirectToAction("Index");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", ProcurementErrors.Format(ex));
                    }
                }
            }

            using (var db = new DBTHSNEntities())
            {
                byte purchaseType = await new PInvoiceService(db, ModelState).ResolvePurchaseTypeAsync(model.ContractID, model.OrderID);
                await PopulateFormLookupsAsync(db, model, purchaseType, includeContracts: true);
            }

            return View(model);
        }

        // Olish so'rovi: PInvoices/o'chirish/5
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
                var invoice = await db.P_INVOICES
                    .AsNoTracking()
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
            if (!CanChange(Module))
            {
                return NotAuthorized();
            }

            using (var db = new DBTHSNEntities())
            {
                // Invoys va uning finance yozuvi birga bekor qilinadi
                var invoice = await new PInvoiceService(db, ModelState).SoftDeleteAsync(id);
                if (invoice != null)
                {
                    LogHelper.LogToDatabase(CurrentUserName, Module, $"Delete - {invoice.InvoiceNo} invoysi o'chirildi");
                }

                return RedirectToAction("Index");
            }
        }

        // Olish so'rovi: PInvoices/DownloadDoc/5
        [Permission(Module)]
        public async Task<ActionResult> DownloadDoc(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                // O'chirilgan invoysning hujjati berilmaydi (SRM-B11)
                var doc = await db.P_INVOICE_DOCS
                    .Where(d => d.ID == id && d.P_INVOICES.IsDeleted != true)
                    .Select(d => new { d.InvoiceID, d.Doc })
                    .FirstOrDefaultAsync();
                if (doc == null || doc.Doc == null)
                {
                    return HttpNotFound();
                }

                return File(doc.Doc, "application/pdf", $"Invoice_{doc.InvoiceID}.pdf");
            }
        }

        // AJAX so'rovi: Get contracts by supplier
        [HttpPost]
        [ValidateAntiForgeryHeader]
        [Permission(Module)]
        public async Task<JsonResult> GetContractsBySupplier(int supplierId)
        {
            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var data = await ProcurementLookups.GetSupplierContractsWithVatAsync(db, supplierId);
                    return Json(new { success = true, data }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return JsonError(ex);
            }
        }

        // ---------- yordamchilar ----------

        /// <summary>Create/Edit formasi spravochniklari (GET va xato yo'llari uchun bitta joy).</summary>
        private async Task PopulateFormLookupsAsync(DBTHSNEntities db, PInvoiceEditViewModel model, byte purchaseType, bool includeContracts)
        {
            ViewBag.Suppliers = ProcurementLookups.Suppliers(model.SupplierID > 0 ? (object)model.SupplierID : null);
            ViewBag.Currencies = ProcurementLookups.Currencies(model.CurrencyID > 0 ? (object)model.CurrencyID : null);
            ViewBag.DefaultCompanyID = await ProcurementLookups.GetDefaultCompanyIdAsync(db);

            var orders = await db.P_ORDERS.AsNoTracking()
                .Where(o => o.IsDeleted != true)
                .OrderByDescending(o => o.IssuedDate)
                .Select(o => new { o.ID, o.OrderNo })
                .ToListAsync();
            ViewBag.POrders = new SelectList(orders, "ID", "OrderNo", model.OrderID);

            if (includeContracts)
            {
                ViewBag.Contracts = await ProcurementLookups.ContractsBySupplierAsync(db, model.SupplierID, model.ContractID);
                ViewBag.ContractVatRate = await PContractVatHelper.CalculateContractVatRateAsync(db, model.ContractID > 0 ? model.ContractID : null);
            }

            ViewBag.PurchaseType = purchaseType;

            var ensureIds = model.InvoiceProducts?.Select(p => p.ProductID);
            var products = await ProcurementLookups.GetPurchasableProductsAsync(db, purchaseType, ensureIds);
            ViewBag.PurchasableProducts = ProcurementLookups.ProductSelectList(products);
            ViewBag.Units = ProcurementLookups.Units();
        }

        /// <summary>Kreditor qarzini Finance'ga yozish (upsert). Xato invoysni bekor qilmaydi, lekin logga yoziladi.</summary>
        private void PostToFinance(DBTHSNEntities db, int invoiceId, string action)
        {
            try
            {
                FinancePostingService.PostPurchaseInvoice(invoiceId, db, CurrentUserId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("PInvoices {0}: finance posting xatosi (invoice {1}): {2}", action, invoiceId, ex);
                LogHelper.LogToDatabase(CurrentUserName, Module, action,
                    $"Finance posting error for invoice {invoiceId}: {InnermostMessage(ex)}");
            }
        }

    }
}
