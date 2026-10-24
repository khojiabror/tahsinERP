using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Filters;
using tahsinERP.I18n;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Common;
using tahsinERP.Services.Crm;
using tahsinERP.ViewModels.Sales;

namespace tahsinERP.Controllers
{
    [Permission]
    public class SalesInvoicesController : BaseController
    {

        // Olish so'rovi: SalesInvoices
        public async Task<ActionResult> Index(string searchTerm, string invoiceNo, int? customerID, int? orderID, int? contractID, DateTime? fromDate, DateTime? toDate, string statusCode, string sortOrder, int? page, int? pageSize)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var query = BuildIndexQuery(db, searchTerm, invoiceNo, customerID, orderID, contractID, fromDate, toDate, statusCode, sortOrder);

                // Server tomonida sahifalash (CRM-P05, umumiy blok — D16)
                var invoices = await query.ToPageAsync(page, pageSize, ViewData);

                // Filtr ro'yxatlari: mijozlar keshdan, buyurtmalar — faqat ID/raqam (CRM-P04)
                ViewBag.Customers = SalesLookups.Customers(customerID);
                ViewBag.CustomerNames = ReferenceDataCache.Get(ReferenceDataCache.Customers).ToDictionary(c => c.ID, c => c.Name);
                ViewBag.Orders = new SelectList(await db.S_ORDERS.AsNoTracking()
                    .Where(o => o.IsDeleted == false || o.IsDeleted == null)
                    .OrderByDescending(o => o.IssuedDate)
                    .Select(o => new { o.ID, o.OrderNo })
                    .ToListAsync(), "ID", "OrderNo", orderID);

                // Buyurtma va shartnoma raqamlarini ko'rsatish uchun lug'atlar
                var orderIds = invoices.Where(i => i.OrderID.HasValue).Select(i => i.OrderID.Value).Distinct().ToList();
                ViewBag.OrderNumbers = await db.S_ORDERS
                    .Where(o => orderIds.Contains(o.ID))
                    .ToDictionaryAsync(o => o.ID, o => o.OrderNo);

                var contractIds = invoices.Where(i => i.ContractID.HasValue).Select(i => i.ContractID.Value).Distinct().ToList();
                ViewBag.ContractNumbers = await db.S_CONTRACTS
                    .Where(c => contractIds.Contains(c.ID))
                    .ToDictionaryAsync(c => c.ID, c => c.ContractNo);
                ViewBag.SearchTerm = searchTerm;
                ViewBag.InvoiceNo = invoiceNo;
                ViewBag.CustomerID = customerID;
                ViewBag.OrderID = orderID;
                ViewBag.ContractID = contractID;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.StatusCode = statusCode;
                ViewBag.SortOrder = sortOrder;

                return View(invoices);
            }
        }

        /// <summary>Filtrlangan barcha invoyslarni Excel'ga (DataTables eksporti faqat joriy sahifani olardi).</summary>
        public ActionResult ExportExcel(string searchTerm, string invoiceNo, int? customerID, int? orderID, int? contractID, DateTime? fromDate, DateTime? toDate, string statusCode, string sortOrder)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var rows = BuildIndexQuery(db, searchTerm, invoiceNo, customerID, orderID, contractID, fromDate, toDate, statusCode, sortOrder)
                    .Select(i => new InvoiceExportRow
                    {
                        InvoiceNo = i.InvoiceNo,
                        InvoiceDate = i.InvoiceDate,
                        Customer = i.CUSTOMER.Name,
                        ContractNo = i.S_CONTRACTS.ContractNo,
                        OrderNo = i.S_ORDERS.OrderNo,
                        TotalAmount = i.TotalAmount,
                        TotalVAT = i.TotalVAT,
                        TotalWithVAT = i.TotalWithVAT,
                        CurrencyCode = i.CURRENCy.Shortname,
                        StatusCode = i.StatusCode
                    })
                    .ToList();

                var bytes = ExcelExport.Create(L.T("Menu_Invoices"), rows,
                    ExcelExport.Col<InvoiceExportRow>(L.T("Crm_InvInvoiceNo"), r => r.InvoiceNo),
                    ExcelExport.Col<InvoiceExportRow>(L.T("Date"), r => r.InvoiceDate, "dd.MM.yyyy"),
                    ExcelExport.Col<InvoiceExportRow>(L.T("Crm_Client"), r => r.Customer),
                    ExcelExport.Col<InvoiceExportRow>(L.T("Crm_Contract"), r => r.ContractNo),
                    ExcelExport.Col<InvoiceExportRow>(L.T("Crm_Order"), r => r.OrderNo),
                    ExcelExport.Col<InvoiceExportRow>(L.T("Amount"), r => r.TotalAmount, "#,##0.00"),
                    ExcelExport.Col<InvoiceExportRow>(L.T("Crm_Vat"), r => r.TotalVAT, "#,##0.00"),
                    ExcelExport.Col<InvoiceExportRow>(L.T("Crm_TotalWithVat"), r => r.TotalWithVAT, "#,##0.00"),
                    ExcelExport.Col<InvoiceExportRow>(L.T("Currency"), r => r.CurrencyCode),
                    ExcelExport.Col<InvoiceExportRow>(L.T("Status"), r => r.StatusCode));
                return File(bytes, ExcelExport.ContentType, $"{L.T("Menu_Invoices")}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
        }

        private sealed class InvoiceExportRow
        {
            public string InvoiceNo { get; set; }
            public DateTime InvoiceDate { get; set; }
            public string Customer { get; set; }
            public string ContractNo { get; set; }
            public string OrderNo { get; set; }
            public decimal TotalAmount { get; set; }
            public decimal TotalVAT { get; set; }
            public decimal? TotalWithVAT { get; set; }
            public string CurrencyCode { get; set; }
            public string StatusCode { get; set; }
        }

        private static IQueryable<SALES_INVOICES> BuildIndexQuery(DBTHSNEntities db, string searchTerm, string invoiceNo, int? customerID, int? orderID, int? contractID, DateTime? fromDate, DateTime? toDate, string statusCode, string sortOrder)
        {
            // Ro'yxatda bekor qilinganlar ham ko'rinadi (holat ustuni bilan) — aks holda ularni topib bo'lmasdi;
            // jamilar/hisobotlar Active() bilan faqat faol invoyslarni hisoblaydi.
            IQueryable<SALES_INVOICES> query = db.SALES_INVOICES.AsNoTracking();

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(i => i.InvoiceNo.Contains(searchTerm));
            if (!string.IsNullOrEmpty(invoiceNo))
                query = query.Where(i => i.InvoiceNo.Contains(invoiceNo));
            if (customerID.HasValue)
                query = query.Where(i => i.CustomerID == customerID.Value);
            if (orderID.HasValue)
                query = query.Where(i => i.OrderID == orderID.Value);
            if (contractID.HasValue)
                query = query.Where(i => i.ContractID == contractID.Value);
            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                query = query.Where(i => i.InvoiceDate >= from);
            }
            if (toDate.HasValue)
            {
                // Yarim ochiq oraliq: oxirgi kunning soat 00:00 dan keyingi invoyslari ham kiradi (CRM-B07)
                var toExclusive = toDate.Value.Date.AddDays(1);
                query = query.Where(i => i.InvoiceDate < toExclusive);
            }
            if (!string.IsNullOrEmpty(statusCode))
                query = query.Where(i => i.StatusCode == statusCode);

            switch (sortOrder)
            {
                case "invoiceNo_desc":
                    return query.OrderByDescending(i => i.InvoiceNo);
                case "invoiceNo":
                    return query.OrderBy(i => i.InvoiceNo);
                case "date":
                    return query.OrderBy(i => i.InvoiceDate).ThenBy(i => i.ID);
                default:
                    return query.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.ID);
            }
        }

        // Olish so'rovi: Details
        public ActionResult Details(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoice = LoadInvoiceForDisplay(db, id.Value);
                if (invoice == null)
                {
                    return HttpNotFound();
                }

                ViewBag.SFCount = db.SALES_SF_HEADERS.Count(sf => sf.InvoiceID == id);
                ViewBag.TTNKCount = db.SALES_TTNK_INVOICES.Count(t => t.InvoiceID == id);
                // Eski invoysda ombor yozilmagan — standart savdo ombori
                var whId = SalesInvoiceService.ResolveWarehouseId(db, invoice.WarehouseID, strict: false);
                ViewBag.WarehouseName = invoice.WarehouseID.HasValue || whId > 0
                    ? db.WAREHOUSES.Where(w => w.ID == (invoice.WarehouseID ?? whId)).Select(w => w.WHName).FirstOrDefault()
                    : null;

                return View(invoice);
            }
        }

        // Olish so'rovi: SalesInvoices/Print/5
        public ActionResult Print(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoice = LoadInvoiceForDisplay(db, id.Value);
                if (invoice == null)
                {
                    return HttpNotFound();
                }

                ViewBag.Company = db.COMPANIES.FirstOrDefault();
                return View(invoice);
            }
        }

        /// <summary>Details va Print uchun umumiy yuklash (D15).</summary>
        private SALES_INVOICES LoadInvoiceForDisplay(DBTHSNEntities db, long id)
        {
            var invoice = db.SALES_INVOICES
                .Include(i => i.SALES_INVOICE_LINES)
                .FirstOrDefault(i => i.ID == id);
            if (invoice == null)
                return null;

            ViewBag.Customer = db.CUSTOMERS.FirstOrDefault(c => c.ID == invoice.CustomerID);

            if (invoice.OrderID.HasValue)
            {
                ViewBag.Order = db.S_ORDERS
                    .Include(o => o.CUSTOMER)
                    .FirstOrDefault(o => o.ID == invoice.OrderID.Value);
            }

            if (invoice.ContractID.HasValue)
            {
                ViewBag.Contract = db.S_CONTRACTS
                    .Include(c => c.CUSTOMER)
                    .FirstOrDefault(c => c.ID == invoice.ContractID.Value);
            }

            var productIDs = invoice.SALES_INVOICE_LINES.Select(l => l.ProductID).Distinct().ToList();
            ViewBag.Products = db.PRODUCTS
                .Include(p => p.UNIT)
                .Where(p => productIDs.Contains(p.ID))
                .ToDictionary(p => p.ID, p => p);

            return invoice;
        }

        // Olish so'rovi: yaratish (standalone) - SOrder/Create uslubida: xaridor -> shartnoma -> buyurtma
        public ActionResult Create(int? customerID, int? orderID, int? contractID)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Raqam saqlashda (qulf ostida) beriladi; formada faqat taxminiy keyingi raqam ko'rsatiladi
                var invoice = new SALES_INVOICES
                {
                    InvoiceDate = DateTime.Now,
                    StatusCode = SalesInvoiceStatus.Approved,
                    CreatedDT = DateTime.Now
                };

                if (customerID.HasValue)
                {
                    invoice.CustomerID = customerID.Value;
                }

                if (orderID.HasValue)
                {
                    invoice.OrderID = orderID.Value;
                    var order = db.S_ORDERS
                        .Include(o => o.CURRENCy1)
                        .FirstOrDefault(o => o.ID == orderID.Value && (o.IsDeleted == false || o.IsDeleted == null));
                    if (order != null)
                    {
                        invoice.CustomerID = order.CustomerID;
                        invoice.ContractID = order.ContractID;
                        invoice.CurrencyID = order.CurrencyID;
                        customerID = order.CustomerID;
                        contractID = order.ContractID;
                    }
                }

                if (contractID.HasValue)
                {
                    invoice.ContractID = contractID.Value;
                    var contractCurrencyId = db.S_CONTRACTS.Where(c => c.ID == contractID.Value).Select(c => (int?)c.CurrencyID).FirstOrDefault();
                    if (contractCurrencyId.HasValue)
                    {
                        invoice.CurrencyID = contractCurrencyId;
                    }
                }

                invoice.CurrencyID = invoice.CurrencyID ?? CurrencyRateService.BaseCurrencyId();
                PopulateFormLookups(db, invoice, null);
                return View(invoice);
            }
        }

        // AJAX: Xaridorga qarab shartnomalar
        [HttpGet]
        public ActionResult GetContractsByCustomer(int customerId)
        {
            using (var db = new DBTHSNEntities())
            {
                return Json(SalesLookups.ContractsByCustomer(db, customerId), JsonRequestBehavior.AllowGet);
            }
        }

        // AJAX: Shartnomaga qarab buyurtmalar
        [HttpGet]
        public ActionResult GetOrdersByContract(int contractId)
        {
            using (var db = new DBTHSNEntities())
            {
                var orders = db.S_ORDERS
                    .Where(o => o.ContractID == contractId && (o.IsDeleted == false || o.IsDeleted == null))
                    .OrderByDescending(o => o.IssuedDate)
                    .Select(o => new { ID = o.ID, OrderNo = o.OrderNo })
                    .ToList();
                return Json(orders, JsonRequestBehavior.AllowGet);
            }
        }

        // AJAX: Buyurtma mahsulotlari — Create formda buyurtma bo'yicha qatorlarni to'ldirish uchun
        [HttpGet]
        public ActionResult GetOrderProductsForInvoice(int orderId)
        {
            using (var db = new DBTHSNEntities())
            {
                var contractId = db.S_ORDERS.Where(o => o.ID == orderId).Select(o => (int?)o.ContractID).FirstOrDefault();
                var vatByProduct = SContractVatHelper.GetProductVatRates(db, contractId);

                var lines = db.S_ORDER_PRODUCTS
                    .Where(op => op.OrderID == orderId)
                    .Select(op => new
                    {
                        op.ProductID,
                        ProductName = op.PRODUCT != null ? op.PRODUCT.Name : "",
                        PNo = op.PRODUCT != null ? op.PRODUCT.PNo : "",
                        Qty = op.Amount,
                        UnitPrice = op.Price
                    })
                    .ToList()
                    .Select(op => new
                    {
                        op.ProductID,
                        op.ProductName,
                        op.PNo,
                        op.Qty,
                        op.UnitPrice,
                        VATRate = vatByProduct.ContainsKey(op.ProductID) ? vatByProduct[op.ProductID] : 0m
                    })
                    .ToList();
                return Json(lines, JsonRequestBehavior.AllowGet);
            }
        }

        // AJAX: Shartnoma va maxsulot bo'yicha narx
        [HttpGet]
        public ActionResult GetContractPriceForProduct(int contractId, int productId)
        {
            using (var db = new DBTHSNEntities())
            {
                var cp = db.S_CONTRACT_PRODUCTS
                    .FirstOrDefault(c => c.ContractID == contractId && c.ProductID == productId);
                if (cp == null)
                    return Json(new { PiecePrice = (decimal?)null, VatRate = 0m }, JsonRequestBehavior.AllowGet);
                return Json(new
                {
                    PiecePrice = cp.PiecePrice,
                    VatRate = SContractVatHelper.GetVatPercentFromLine(
                        SContractVatHelper.GetLineBaseAmount(cp), cp.vatamount)
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // AJAX: Tanlangan savdo omboridagi mahsulotlar ("... Mavjud: N"). Tahrirlashda invoysning o'z miqdori
        // faqat o'sha omborda bo'lsa qo'shiladi (boshqa omborga o'tkazilsa — qaytarilgan chiqim u yerga tushmaydi)
        [HttpGet]
        public ActionResult GetWarehouseProducts(int warehouseId, long? invoiceId)
        {
            using (var db = new DBTHSNEntities())
            {
                IDictionary<int, decimal> ownQty = null;
                if (invoiceId.HasValue)
                {
                    var inv = db.SALES_INVOICES.AsNoTracking()
                        .Where(i => i.ID == invoiceId.Value)
                        .Select(i => new { i.WarehouseID })
                        .FirstOrDefault();
                    if (inv != null && SalesInvoiceService.ResolveWarehouseId(db, inv.WarehouseID, strict: false) == warehouseId)
                        ownQty = OwnQuantities(db.SALES_INVOICE_LINES.AsNoTracking().Where(l => l.InvoiceID == invoiceId.Value).ToList());
                }
                var items = SalesInvoiceService.SalesWarehouseProductItems(db, warehouseId, ownQty)
                    .Select(i => new { i.Value, i.Text });
                return Json(items, JsonRequestBehavior.AllowGet);
            }
        }

        // AJAX: Shartnoma valyutasi
        [HttpGet]
        public ActionResult GetContractCurrency(int contractId)
        {
            using (var db = new DBTHSNEntities())
            {
                var currencyId = db.S_CONTRACTS
                    .Where(c => c.ID == contractId)
                    .Select(c => (int?)c.CurrencyID)
                    .FirstOrDefault() ?? CurrencyRateService.BaseCurrencyId();
                return Json(new { CurrencyID = currencyId, CurrencyCode = SalesLookups.CurrencyCode(currencyId) }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST so'rovi: yaratish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(SALES_INVOICES invoice, List<SalesInvoiceLineViewModel> lines)
        {
            // Biznes qoidasi: invoys yaratilganda status doim APPROVED; raqam bo'sh bo'lsa server beradi.
            invoice.StatusCode = SalesInvoiceStatus.Approved;
            ModelState.Remove("StatusCode");
            ModelState.Remove("InvoiceNo");

            if (ModelState.IsValid)
            {
                try
                {
                    var id = SalesInvoiceService.Create(invoice, lines, CurrentUserName, CurrentUserId);
                    LogHelper.LogToDatabase(CurrentUserName, "SalesInvoicesController", $"{id} - Invoysni yaratdi");
                    return RedirectToAction("Details", new { id });
                }
                catch (SalesInvoiceException ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", L.T("Crm_InvSaveFailedNothingWritten", InnermostMessage(ex)));
                }
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                SalesInvoiceService.AttachLinesForRedisplay(invoice, lines);
                PopulateFormLookups(db, invoice, null);
            }

            return View(invoice);
        }

        // Olish so'rovi: Edit
        public ActionResult Edit(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoice = db.SALES_INVOICES
                    .Include(i => i.SALES_INVOICE_LINES)
                    .FirstOrDefault(i => i.ID == id);

                if (invoice == null)
                {
                    return HttpNotFound();
                }

                if (SalesInvoiceStatus.IsLocked(invoice.StatusCode))
                {
                    TempData["ErrorMessage"] = L.T("Crm_InvCannotEditLocked");
                    return RedirectToAction("Details", new { id = id });
                }

                PopulateFormLookups(db, invoice, OwnQuantities(invoice.SALES_INVOICE_LINES));
                return View(invoice);
            }
        }

        // POST so'rovi: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(SALES_INVOICES invoice, List<SalesInvoiceLineViewModel> lines)
        {
            ModelState.Remove("StatusCode");

            if (ModelState.IsValid)
            {
                try
                {
                    SalesInvoiceService.Update(invoice, lines, CurrentUserName, CurrentUserId);
                    LogHelper.LogToDatabase(CurrentUserName, "SalesInvoicesController", $"{invoice.InvoiceNo} - Invoysni tahrirladi");
                    return RedirectToAction("Details", new { id = invoice.ID });
                }
                catch (SalesInvoiceException ex) when (ex.NotFound)
                {
                    return HttpNotFound();
                }
                catch (SalesInvoiceException ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", L.T("Crm_InvSaveFailedNothingChanged", InnermostMessage(ex)));
                }
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Tahrirlanayotgan invoysning saqlangan miqdorlari mahsulot ro'yxatida "mavjud"ga qo'shiladi
                var savedLines = db.SALES_INVOICE_LINES.AsNoTracking().Where(l => l.InvoiceID == invoice.ID).ToList();
                SalesInvoiceService.AttachLinesForRedisplay(invoice, lines);
                PopulateFormLookups(db, invoice, OwnQuantities(savedLines));
            }

            return View(invoice);
        }

        // Olish so'rovi: SalesInvoices/Delete/5
        public ActionResult Delete(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var invoice = db.SALES_INVOICES
                    .Include(i => i.CUSTOMER)
                    .FirstOrDefault(i => i.ID == id);

                if (invoice == null)
                {
                    return HttpNotFound();
                }

                return View(invoice);
            }
        }

        // POST so'rovi: SalesInvoices/Delete/5 — bekor qilish (status + ombor + finance bitta tranzaksiyada)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(long id)
        {
            try
            {
                var invoice = SalesInvoiceService.Cancel(id, CurrentUserName);
                LogHelper.LogToDatabase(CurrentUserName, "SalesInvoices", $"Delete - {invoice.InvoiceNo} invoysi bekor qilindi");
                return RedirectToAction("Index");
            }
            catch (SalesInvoiceException ex) when (ex.NotFound)
            {
                return HttpNotFound();
            }
            catch (SalesInvoiceException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = L.T("Crm_InvCancelFailed", InnermostMessage(ex));
            }
            return RedirectToAction("Details", new { id });
        }

        /// <summary>Create/Edit formasi ro'yxatlari — barcha qaytish yo'llarida bitta joy (D11, D12, CRM-B15).</summary>
        private void PopulateFormLookups(DBTHSNEntities db, SALES_INVOICES invoice, IDictionary<int, decimal> ownQty)
        {
            ViewBag.Customers = SalesLookups.Customers(invoice.CustomerID > 0 ? (int?)invoice.CustomerID : null);
            ViewBag.Contracts = SalesLookups.ContractsFor(db, invoice.CustomerID, invoice.ContractID);
            ViewBag.Orders = SalesLookups.OrdersFor(db, invoice.ContractID, invoice.OrderID);
            ViewBag.Currencies = SalesLookups.Currencies(invoice.CurrencyID ?? CurrencyRateService.BaseCurrencyId());
            ViewBag.SalesWarehouses = SalesInvoiceService.SalesWarehouseItems(db, invoice.WarehouseID);
            ViewBag.ProductItems = SalesInvoiceService.SalesWarehouseProductItems(db, invoice.WarehouseID, ownQty);
            ViewBag.SuggestedInvoiceNo = SalesInvoiceService.SuggestNextInvoiceNo(db);
        }

        private static Dictionary<int, decimal> OwnQuantities(IEnumerable<SALES_INVOICE_LINES> lines)
        {
            return lines.GroupBy(l => l.ProductID).ToDictionary(g => g.Key, g => g.Sum(l => l.Qty));
        }
    }
}
