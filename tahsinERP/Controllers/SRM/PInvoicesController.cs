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
using tahsinERP.ViewModels;
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

        /// <summary>Invoyslar ro'yxati qatori (proyeksiya).</summary>
        public sealed class PInvoiceIndexRow
        {
            public int ID { get; set; }
            public string InvoiceNo { get; set; }
            public int SupplierID { get; set; }
            public string SupplierName { get; set; }
            public byte PurchaseType { get; set; }
            public int? OrderID { get; set; }
            public string OrderNo { get; set; }
            public int ContractID { get; set; }
            public string ContractNo { get; set; }
            public int? VatContractID { get; set; }
            public double? Amount { get; set; }
            public double? Net { get; set; }
            public DateTime InvoiceDate { get; set; }
        }

        private static IQueryable<PInvoiceIndexRow> BuildIndexQuery(DBTHSNEntities db, string purchaseTypeFilter,
            int? supplierID, int? contractID, DateTime? fromDate, DateTime? toDate)
        {
            var query = db.P_INVOICES.AsNoTracking().Where(i => i.IsDeleted != true);

            if (supplierID.HasValue && supplierID.Value > 0)
            {
                query = query.Where(i => i.SupplierID == supplierID.Value);
            }

            if (contractID.HasValue && contractID.Value > 0)
            {
                query = query.Where(i => i.ContractID == contractID.Value);
            }

            if (!string.IsNullOrEmpty(purchaseTypeFilter))
            {
                byte purchaseType = purchaseTypeFilter == "Indirect" ? ProcurementLookups.IndirectPurchase : ProcurementLookups.DirectPurchase;
                query = query.Where(i => i.PurchaseType == purchaseType);
            }

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                query = query.Where(i => i.InvoiceDate >= from);
            }
            if (toDate.HasValue)
            {
                var toExclusive = toDate.Value.Date.AddDays(1);
                query = query.Where(i => i.InvoiceDate < toExclusive);
            }

            return query.Select(i => new PInvoiceIndexRow
            {
                ID = i.ID,
                InvoiceNo = i.InvoiceNo,
                SupplierID = i.SupplierID,
                SupplierName = i.SUPPLIER.Name,
                PurchaseType = i.PurchaseType,
                OrderID = i.OrderID,
                OrderNo = i.P_ORDERS.OrderNo,
                ContractID = i.ContractID,
                ContractNo = i.P_CONTRACTS.ContractNo,
                // QQS stavkasi shartnomadan: invoysning o'zi yoki buyurtmasi orqali (SRM-B18)
                VatContractID = i.ContractID > 0 ? (int?)i.ContractID : i.P_ORDERS.ContractID,
                Amount = i.Amount,
                // Amount QQS bilan saqlanadi (SRM-B25); QQSsiz summa — qatorlar yig'indisi
                Net = i.P_INVOICE_PRODUCTS.Sum(x => (double?)x.TotalPrice),
                InvoiceDate = i.InvoiceDate
            });
        }

        private static IQueryable<PInvoiceIndexRow> SearchIndex(IQueryable<PInvoiceIndexRow> query, string s)
        {
            return query.Where(r => r.InvoiceNo.Contains(s) || r.SupplierName.Contains(s)
                || r.OrderNo.Contains(s) || r.ContractNo.Contains(s));
        }

        private static IOrderedQueryable<PInvoiceIndexRow> OrderIndex(IQueryable<PInvoiceIndexRow> query, int column, bool desc)
        {
            switch (column)
            {
                case 0: return ServerTablePage.By(query, r => r.InvoiceNo, desc).ThenByDescending(r => r.ID);
                case 1: return ServerTablePage.By(query, r => r.SupplierName, desc).ThenByDescending(r => r.ID);
                case 2: return ServerTablePage.By(query, r => r.PurchaseType, desc).ThenByDescending(r => r.ID);
                case 3: return ServerTablePage.By(query, r => r.OrderNo, desc).ThenByDescending(r => r.ID);
                case 4: return ServerTablePage.By(query, r => r.ContractNo, desc).ThenByDescending(r => r.ID);
                case 5: return ServerTablePage.By(query, r => r.Amount, desc).ThenByDescending(r => r.ID);
                default: return desc ? query.OrderByDescending(r => r.InvoiceDate).ThenByDescending(r => r.ID)
                                     : query.OrderBy(r => r.InvoiceDate).ThenBy(r => r.ID);
            }
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
                    BuildIndexQuery(db, purchaseTypeFilter, supplierID, contractID, fromDate, toDate), page, SearchIndex, OrderIndex);

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
                var query = BuildIndexQuery(db, purchaseTypeFilter, supplierID, contractID, fromDate, toDate);
                if (!string.IsNullOrWhiteSpace(q))
                {
                    query = SearchIndex(query, q.Trim());
                }

                var rows = await OrderIndex(query, 6, true).ToListAsync();
                var bytes = ExcelExport.Create("Xarid invoyslari", rows,
                    ExcelExport.Col<PInvoiceIndexRow>("Invoys raqami", r => r.InvoiceNo),
                    ExcelExport.Col<PInvoiceIndexRow>("Ta'minotchi", r => r.SupplierName),
                    ExcelExport.Col<PInvoiceIndexRow>("Turi", r => r.PurchaseType == ProcurementLookups.IndirectPurchase ? "Indirect" : "Direct"),
                    ExcelExport.Col<PInvoiceIndexRow>("Buyurtma raqami", r => r.OrderNo),
                    ExcelExport.Col<PInvoiceIndexRow>("Shartnoma raqami", r => r.ContractNo),
                    ExcelExport.Col<PInvoiceIndexRow>("Invoys summasi (QQS bilan)", r => r.Amount ?? 0, "#,##0.00"),
                    ExcelExport.Col<PInvoiceIndexRow>("Sana", r => r.InvoiceDate, "dd.MM.yyyy"));

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
                        // Xarid turi: buyurtmadan, bo'lmasa formadagi belgidan
                        byte purchaseType = ProcurementLookups.DirectPurchase;
                        P_ORDERS order = null;
                        if (model.OrderID.HasValue)
                        {
                            order = await db.P_ORDERS.FindAsync(model.OrderID.Value);
                            if (order != null)
                            {
                                purchaseType = order.PurchaseType;
                            }
                        }
                        else if (Request.Form["PurchaseType"] == "Indirect")
                        {
                            purchaseType = ProcurementLookups.IndirectPurchase;
                        }

                        await ValidateLinksAsync(db, order, model.SupplierID, model.ContractID, "yaratilganda");

                        if (ModelState.IsValid)
                        {
                            P_INVOICES invoice = null;

                            // Sarlavha, qatorlar, hujjat — bitta tranzaksiyada (SRM-B12)
                            using (var tx = db.Database.BeginTransaction())
                            {
                                var number = await ResolveInvoiceNoAsync(db, model.InvoiceNo, excludeId: null);
                                if (number != null)
                                {
                                    invoice = new P_INVOICES
                                    {
                                        InvoiceNo = number,
                                        InvoiceDate = model.InvoiceDate,
                                        OrderID = model.OrderID,
                                        SupplierID = model.SupplierID,
                                        ContractID = model.ContractID ?? 0,
                                        CompanyID = model.CompanyID ?? await ProcurementLookups.GetDefaultCompanyIdAsync(db),
                                        CurrencyID = model.CurrencyID,
                                        IsDeleted = false,
                                        PurchaseType = purchaseType
                                    };
                                    db.P_INVOICES.Add(invoice);

                                    // Qatorlar navigatsiya orqali — EF FK'ni o'zi qo'yadi, bitta SaveChanges.
                                    // Amount QQS bilan saqlanadi (SRM-B25 qarori): net + shartnoma QQS stavkasi.
                                    var net = await AddLinesAsync(db, invoice, model.InvoiceProducts);
                                    invoice.Amount = VatMath.ApplyVat(net, await ResolveVatRateAsync(db, model.ContractID, order));
                                    AddDocument(db, invoice, invoiceFile);

                                    await db.SaveChangesAsync();
                                    tx.Commit();
                                }
                            }

                            if (invoice != null)
                            {
                                // Finance'ga kreditor qarzi sifatida yozish. Avval xato jim yutilardi (SRM-B13).
                                PostToFinance(db, invoice.ID, "Create");

                                LogHelper.LogToDatabase(CurrentUserName, Module, $"Create - {invoice.InvoiceNo} invoysi yaratildi");

                                return RedirectToAction("Index");
                            }
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
                byte purchaseType = await ResolvePurchaseTypeAsync(db, model.ContractID, model.OrderID);
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

                byte purchaseType = await ResolvePurchaseTypeAsync(db, model.ContractID, model.OrderID);
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

                        P_ORDERS order = null;
                        if (model.OrderID.HasValue && model.OrderID.Value > 0)
                        {
                            order = await db.P_ORDERS.FindAsync(model.OrderID.Value);
                        }
                        byte purchaseType = await ResolvePurchaseTypeAsync(db, model.ContractID, model.OrderID);

                        await ValidateLinksAsync(db, order, model.SupplierID, model.ContractID, "tahrirlashda");

                        string invoiceNo = null;
                        if (ModelState.IsValid)
                        {
                            invoiceNo = await ResolveInvoiceNoAsync(db, model.InvoiceNo, excludeId: invoice.ID);
                        }

                        if (ModelState.IsValid)
                        {
                            using (var tx = db.Database.BeginTransaction())
                            {
                                invoice.InvoiceNo = invoiceNo;
                                invoice.InvoiceDate = model.InvoiceDate;
                                invoice.OrderID = model.OrderID; // ixtiyoriy
                                invoice.SupplierID = model.SupplierID;
                                invoice.ContractID = model.ContractID ?? 0; // shartnoma asosidagi hisoblar uchun
                                invoice.CompanyID = model.CompanyID ?? await ProcurementLookups.GetDefaultCompanyIdAsync(db);
                                invoice.CurrencyID = model.CurrencyID;
                                invoice.PurchaseType = purchaseType;

                                // Qatorlarni almashtirish va summani qayta hisoblash (SRM-B01:
                                // avval Amount eski qolib, Finance'ga eski summa qayta yozilardi)
                                var existingProducts = await db.P_INVOICE_PRODUCTS.Where(ip => ip.InvoiceID == invoice.ID).ToListAsync();
                                db.P_INVOICE_PRODUCTS.RemoveRange(existingProducts);
                                var net = await AddLinesAsync(db, invoice, model.InvoiceProducts);
                                invoice.Amount = VatMath.ApplyVat(net, await ResolveVatRateAsync(db, model.ContractID, order));

                                if (PdfUpload.HasFile(invoiceFile))
                                {
                                    // Yangi hujjat eskisining o'rniga (eski blob'larni xotiraga yuklamasdan o'chiramiz)
                                    await db.Database.ExecuteSqlCommandAsync("DELETE FROM P_INVOICE_DOCS WHERE InvoiceID = @p0", invoice.ID);
                                    AddDocument(db, invoice, invoiceFile);
                                }

                                await db.SaveChangesAsync();
                                tx.Commit();
                            }

                            // Finance yozuvini yangilash (upsert) — endi to'g'ri summa bilan
                            PostToFinance(db, invoice.ID, "Edit");

                            LogHelper.LogToDatabase(CurrentUserName, Module, $"Edit - {invoice.InvoiceNo} invoysi tahrirlandi");

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
                byte purchaseType = await ResolvePurchaseTypeAsync(db, model.ContractID, model.OrderID);
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
                var invoice = await db.P_INVOICES.FindAsync(id);
                if (invoice != null && invoice.IsDeleted != true)
                {
                    // Invoys va uning finance yozuvi birga bekor qilinadi
                    using (var tx = db.Database.BeginTransaction())
                    {
                        invoice.IsDeleted = true;
                        await SoftDeleteReferenceService.AfterPInvoiceSoftDeletedAsync(db, id);
                        FinancePostingService.RemovePurchaseInvoicePosting(id, db);
                        await db.SaveChangesAsync();
                        tx.Commit();
                    }

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

        /// <summary>
        /// Invoys QQS stavkasi: shartnoma (to'g'ridan-to'g'ri yoki buyurtma orqali) qatorlaridagi stavka;
        /// shartnoma bo'lmasa 0. QQS faqat P_CONTRACT_PRODUCTS'da saqlanadi.
        /// </summary>
        private static async Task<double> ResolveVatRateAsync(DBTHSNEntities db, int? contractId, P_ORDERS order)
        {
            int? cid = contractId > 0 ? contractId : order?.ContractID;
            return await PContractVatHelper.CalculateContractVatRateAsync(db, cid);
        }

        /// <summary>Xarid turi: shartnomadan, bo'lmasa buyurtmadan (standart — bevosita).</summary>
        private static async Task<byte> ResolvePurchaseTypeAsync(DBTHSNEntities db, int? contractId, int? orderId)
        {
            if (contractId.HasValue && contractId.Value > 0)
            {
                var fromContract = await db.P_CONTRACTS
                    .Where(c => c.ID == contractId.Value)
                    .Select(c => (byte?)c.PurchaseType)
                    .FirstOrDefaultAsync();
                if (fromContract.HasValue)
                {
                    return fromContract.Value;
                }
            }
            else if (orderId.HasValue && orderId.Value > 0)
            {
                var fromOrder = await db.P_ORDERS
                    .Where(o => o.ID == orderId.Value)
                    .Select(o => (byte?)o.PurchaseType)
                    .FirstOrDefaultAsync();
                if (fromOrder.HasValue)
                {
                    return fromOrder.Value;
                }
            }

            return ProcurementLookups.DirectPurchase;
        }

        /// <summary>Invoys — buyurtma — shartnoma — ta'minotchi mosligi (Create/Edit'da bir xil, SRM-D8).</summary>
        private async Task ValidateLinksAsync(DBTHSNEntities db, P_ORDERS order, int supplierId, int? contractId, string actionWord)
        {
            if (order != null)
            {
                if (order.PurchaseType == ProcurementLookups.DirectPurchase)
                {
                    if (!order.SupplierID.HasValue && supplierId <= 0)
                    {
                        ModelState.AddModelError("SupplierID", $"Direct buyurtmadan invoice {actionWord} ta'minotchi tanlanishi shart.");
                    }
                    if (!order.ContractID.HasValue && (!contractId.HasValue || contractId.Value <= 0))
                    {
                        ModelState.AddModelError("ContractID", $"Direct buyurtmadan invoice {actionWord} shartnoma tanlanishi shart.");
                    }
                }

                if (order.SupplierID.HasValue && order.SupplierID.Value > 0 && supplierId != order.SupplierID.Value)
                {
                    ModelState.AddModelError("SupplierID", "Tanlangan ta'minotchi buyurtmadagi ta'minotchi bilan mos emas.");
                }
                if (order.ContractID.HasValue && order.ContractID.Value > 0 && contractId != order.ContractID.Value)
                {
                    ModelState.AddModelError("ContractID", "Tanlangan shartnoma buyurtmadagi shartnoma bilan mos emas.");
                }
            }

            if (contractId.HasValue && contractId.Value > 0)
            {
                var contract = await db.P_CONTRACTS
                    .Where(c => c.ID == contractId.Value)
                    .Select(c => new { c.SupplierID, c.IsDeleted })
                    .FirstOrDefaultAsync();
                if (contract == null || contract.IsDeleted == true)
                {
                    ModelState.AddModelError("ContractID", "Tanlangan shartnoma topilmadi.");
                }
                else if (supplierId > 0 && contract.SupplierID != supplierId)
                {
                    ModelState.AddModelError("ContractID", "Tanlangan shartnoma tanlangan ta'minotchiga tegishli emas.");
                }
            }
        }

        /// <summary>
        /// Invoys raqami: bo'sh bo'lsa PI-{yil}-NNNN (qulf bilan, o'chirilganlar ham hisobga olinadi — SRM-B14);
        /// qo'lda kiritilsa — barcha invoyslarda (o'chirilganlar ham) takrorlanmasligi tekshiriladi.
        /// Avtomatik raqam tranzaksiya ichida chaqirilishi kerak. Xato bo'lsa null (ModelState'ga yoziladi).
        /// </summary>
        private async Task<string> ResolveInvoiceNoAsync(DBTHSNEntities db, string requested, int? excludeId)
        {
            var number = string.IsNullOrWhiteSpace(requested) ? null : requested.Trim();
            if (number == null)
            {
                if (excludeId.HasValue)
                {
                    // Tahrirlashda bo'sh qoldirilsa — mavjud raqam saqlanadi
                    return await db.P_INVOICES.Where(i => i.ID == excludeId.Value).Select(i => i.InvoiceNo).FirstOrDefaultAsync();
                }

                return await DocumentNumberService.NextAsync(db, "P_INVOICES", "InvoiceNo", $"PI-{DateTime.Now.Year}-", 4);
            }

            // Bazada InvoiceNo bo'yicha global UNIQUE indeks bor (o'chirilganlar ham) — tekshiruv ham global
            bool duplicate = await db.P_INVOICES.AnyAsync(i =>
                i.InvoiceNo == number &&
                (!excludeId.HasValue || i.ID != excludeId.Value));
            if (duplicate)
            {
                ModelState.AddModelError("InvoiceNo", "Shu raqamli invoys allaqachon mavjud (o'chirilganlar orasida ham bo'lishi mumkin).");
                return null;
            }

            return number;
        }

        /// <summary>
        /// Invoys qatorlarini navigatsiya orqali qo'shadi va QQSsiz jami summani qaytaradi.
        /// Mahsulot birliklari bitta so'rovda olinadi (avval har qatorga FindAsync, SRM-P06).
        /// </summary>
        private static async Task<double> AddLinesAsync(DBTHSNEntities db, P_INVOICES invoice, IEnumerable<PInvoiceProductViewModel> lines)
        {
            var list = (lines ?? Enumerable.Empty<PInvoiceProductViewModel>()).ToList();
            if (list.Count == 0)
            {
                return 0;
            }

            var productIds = list.Select(l => l.ProductID).Distinct().ToList();
            var productUnits = await db.PRODUCTS.AsNoTracking()
                .Where(p => productIds.Contains(p.ID))
                .Select(p => new { p.ID, p.UnitID })
                .ToDictionaryAsync(p => p.ID, p => p.UnitID);

            double total = 0;
            foreach (var line in list)
            {
                // Foydalanuvchi tanlagan birlik saqlanadi (masalan, m3/CBM). Avval kesiladigan mahsulotlar
                // majburan donaga (UnitID=1) o'tkazilardi — m3 da kiritilgan miqdor donaga aylanib qolardi.
                int productUnit;
                int unitId = line.UnitID > 0
                    ? line.UnitID
                    : (productUnits.TryGetValue(line.ProductID, out productUnit) ? productUnit : 1);

                var invoiceProduct = new P_INVOICE_PRODUCTS
                {
                    ProductID = line.ProductID,
                    Quantity = line.Quantity,
                    Price = line.Price,
                    TotalPrice = line.Price * line.Quantity,
                    UnitID = unitId
                };

                // FK navigatsiya orqali: yangi invoysda ID SaveChanges'da beriladi
                invoiceProduct.P_INVOICES = invoice;
                db.P_INVOICE_PRODUCTS.Add(invoiceProduct);
                total += invoiceProduct.TotalPrice;
            }

            return total;
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

        private static void AddDocument(DBTHSNEntities db, P_INVOICES invoice, HttpPostedFileBase file)
        {
            if (!PdfUpload.HasFile(file))
            {
                return;
            }

            db.P_INVOICE_DOCS.Add(new P_INVOICE_DOCS { P_INVOICES = invoice, Doc = PdfUpload.Read(file) });
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
