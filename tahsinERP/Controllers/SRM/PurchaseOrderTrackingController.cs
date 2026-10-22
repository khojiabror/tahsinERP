using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.I18n;
using tahsinERP.Models;
using tahsinERP.Services.Procurement;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers.SRM
{
    public class PurchaseOrderTrackingController : BaseController
    {
        private const string Module = "PurchaseOrderTracking";

        /// <summary>Buyurtma bo'yicha bosqichlar ma'lumoti (SQL proyeksiyasidan).</summary>
        public sealed class TrackingFacts
        {
            public string ContractNo { get; set; }
            public DateTime? ContractDate { get; set; }
            public string InvoiceNo { get; set; }
            public DateTime? InvoiceDate { get; set; }
            public DateTime? ReceiptDate { get; set; }
        }

        // Olish so'rovi: PurchaseOrderTracking — sahifa faqat filtr va jadval qobig'i; qatorlar IndexData orqali (SRM-P03)
        public ActionResult Index(string purchaseTypeFilter = "", string statusFilter = "")
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, Module, "Index"))
            {
                return NotAuthorized();
            }

            ViewBag.PurchaseTypeFilter = purchaseTypeFilter;
            ViewBag.PurchaseTypeList = new SelectList(new[]
            {
                new { Value = "", Text = L.T("All") },
                new { Value = "Direct", Text = L.T("Srm_DirectPurchase") },
                new { Value = "Indirect", Text = L.T("Srm_IndirectPurchase") }
            }, "Value", "Text", purchaseTypeFilter);

            ViewBag.StatusFilter = statusFilter;
            ViewBag.StatusList = new SelectList(new[]
            {
                new { Value = "", Text = L.T("All") },
                new { Value = "Created", Text = L.T("Srm_TrackStatus_Created") },
                new { Value = "ContractCreated", Text = L.T("Srm_TrackStatus_ContractCreated") },
                new { Value = "Invoiced", Text = L.T("Srm_TrackStatus_Invoiced") },
                new { Value = "Received", Text = L.T("Srm_TrackStatus_Received") }
            }, "Value", "Text", statusFilter);

            return View();
        }

        /// <summary>Kuzatuv ro'yxati qatori: bosqich faktlari va hisoblangan holat — hammasi SQL'da.</summary>
        public sealed class TrackingIndexRow
        {
            public int ID { get; set; }
            public string OrderNo { get; set; }
            public string SupplierName { get; set; }
            public DateTime IssuedDate { get; set; }
            public byte PurchaseType { get; set; }
            public bool HasContract { get; set; }
            public string ContractNo { get; set; }
            public DateTime? ContractDate { get; set; }
            public string InvoiceNo { get; set; }
            public DateTime? InvoiceDate { get; set; }
            public DateTime? ReceiptDate { get; set; }
            public string Status { get; set; }
        }

        private static IQueryable<TrackingIndexRow> BuildIndexQuery(DBTHSNEntities db, string purchaseTypeFilter, string statusFilter)
        {
            var query = db.P_ORDERS.AsNoTracking().Where(o => o.IsDeleted != true);

            if (!string.IsNullOrEmpty(purchaseTypeFilter))
            {
                byte purchaseType = purchaseTypeFilter == "Indirect" ? (byte)1 : (byte)0;
                query = query.Where(o => o.PurchaseType == purchaseType);
            }

            // Bosqichlar: shartnoma, birinchi faol invoys va omborga birinchi kirim (BuildTrackingStatus bilan bir xil qoida)
            var facts = query.Select(o => new
            {
                o.ID,
                o.OrderNo,
                SupplierName = o.SUPPLIER.Name,
                o.IssuedDate,
                o.PurchaseType,
                HasContract = o.ContractID.HasValue,
                ContractNo = o.P_CONTRACTS.ContractNo,
                ContractDate = (DateTime?)o.P_CONTRACTS.IssuedDate,
                InvoiceNo = o.P_INVOICES.Where(i => i.IsDeleted != true).OrderBy(i => i.ID).Select(i => i.InvoiceNo).FirstOrDefault(),
                InvoiceDate = o.P_INVOICES.Where(i => i.IsDeleted != true).OrderBy(i => i.ID).Select(i => (DateTime?)i.InvoiceDate).FirstOrDefault(),
                ReceiptDate = o.P_INVOICES.Where(i => i.IsDeleted != true)
                    .SelectMany(i => i.INCOMES)
                    .Where(inc => inc.IsDeleted != true)
                    .Min(inc => (DateTime?)inc.IssueDateTime)
            });

            var rows = facts.Select(f => new TrackingIndexRow
            {
                ID = f.ID,
                OrderNo = f.OrderNo,
                SupplierName = f.SupplierName,
                IssuedDate = f.IssuedDate,
                PurchaseType = f.PurchaseType,
                HasContract = f.HasContract,
                ContractNo = f.ContractNo,
                ContractDate = f.ContractDate,
                InvoiceNo = f.InvoiceNo,
                InvoiceDate = f.InvoiceDate,
                ReceiptDate = f.ReceiptDate,
                Status = f.ReceiptDate != null ? "Received"
                    : (f.InvoiceNo != null || f.InvoiceDate != null) ? "Invoiced"
                    : f.HasContract ? "ContractCreated"
                    : "Created"
            });

            // Filtr hisoblangan holat bo'yicha — DB ustuni bo'yicha emas
            if (!string.IsNullOrEmpty(statusFilter))
            {
                rows = rows.Where(r => r.Status == statusFilter);
            }

            return rows;
        }

        private static IQueryable<TrackingIndexRow> SearchIndex(IQueryable<TrackingIndexRow> query, string s)
        {
            return query.Where(r => r.OrderNo.Contains(s) || r.SupplierName.Contains(s) || r.Status.Contains(s));
        }

        private static IOrderedQueryable<TrackingIndexRow> OrderIndex(IQueryable<TrackingIndexRow> query, int column, bool desc)
        {
            switch (column)
            {
                case 0: return ServerTablePage.By(query, r => r.OrderNo, desc).ThenByDescending(r => r.ID);
                case 1: return ServerTablePage.By(query, r => r.SupplierName, desc).ThenByDescending(r => r.ID);
                case 3: return ServerTablePage.By(query, r => r.PurchaseType, desc).ThenByDescending(r => r.ID);
                case 8: return ServerTablePage.By(query, r => r.Status, desc).ThenByDescending(r => r.ID);
                default: return desc ? query.OrderByDescending(r => r.IssuedDate).ThenByDescending(r => r.ID)
                                     : query.OrderBy(r => r.IssuedDate).ThenBy(r => r.ID);
            }
        }

        // Olish so'rovi: PurchaseOrderTracking/IndexData (DataTables server-side)
        [HttpGet]
        public async Task<ActionResult> IndexData(string purchaseTypeFilter = "", string statusFilter = "")
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, Module, "Index"))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            var page = DataTablesPageRequest.Parse(Request, defaultOrderColumn: 2);
            using (var db = new DBTHSNEntities())
            {
                var result = await ServerTablePage.LoadAsync(BuildIndexQuery(db, purchaseTypeFilter, statusFilter), page, SearchIndex, OrderIndex);

                return Json(new
                {
                    draw = page.Draw,
                    recordsTotal = result.Total,
                    recordsFiltered = result.Filtered,
                    data = result.Rows.Select(r => new
                    {
                        id = r.ID,
                        orderNo = r.OrderNo,
                        supplier = r.SupplierName,
                        date = r.IssuedDate.ToString("dd.MM.yyyy"),
                        indirect = r.PurchaseType == 1,
                        contract = r.HasContract ? L.T("Srm_ContractWithDate", r.ContractNo, r.ContractDate?.ToString("dd.MM.yyyy")) : null,
                        invoice = r.InvoiceNo != null || r.InvoiceDate.HasValue
                            ? L.T("Srm_InvoiceWithDate", r.InvoiceNo, r.InvoiceDate?.ToString("dd.MM.yyyy")) : null,
                        receipt = r.ReceiptDate.HasValue ? L.T("Srm_ReceivedOn", r.ReceiptDate?.ToString("dd.MM.yyyy")) : null,
                        status = r.Status
                    })
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // Olish so'rovi: PurchaseOrderTracking/Details/5
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
                var order = await db.P_ORDERS
                    .AsNoTracking()
                    .Include(o => o.SUPPLIER)
                    .Include(o => o.COMPANy)
                    .Include(o => o.CURRENCy)
                    .Include(o => o.P_CONTRACTS)
                    .Include(o => o.P_ORDER_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(o => o.P_ORDER_PRODUCTS.Select(p => p.UNIT))
                    .Include(o => o.P_REQUISITIONS)
                    .FirstOrDefaultAsync(o => o.ID == id && o.IsDeleted != true);

                if (order == null)
                {
                    return HttpNotFound();
                }

                // Faqat buyurtmaga bog'langan shartnoma. Avval shartnomasiz buyurtmada BARCHA
                // shartnomalar "bog'liq" deb ko'rsatilardi (SRM-B06).
                var contracts = order.ContractID.HasValue
                    ? await db.P_CONTRACTS.AsNoTracking()
                        .Where(c => c.ID == order.ContractID.Value && c.IsDeleted != true)
                        .ToListAsync()
                    : new List<P_CONTRACTS>();

                var invoices = await db.P_INVOICES.AsNoTracking()
                    .Where(i => i.IsDeleted != true && i.OrderID == order.ID)
                    .OrderBy(i => i.ID)
                    .ToListAsync();

                // Omborga kirim hujjatlari (invoys bo'yicha)
                var invoiceIds = invoices.Select(inv => inv.ID).ToList();
                var incomes = await db.INCOMES.AsNoTracking()
                    .Where(inc => inc.InvoiceID.HasValue && invoiceIds.Contains(inc.InvoiceID.Value) && inc.IsDeleted != true)
                    .OrderBy(inc => inc.IssueDateTime)
                    .ToListAsync();

                var firstInvoice = invoices.FirstOrDefault();
                var facts = new TrackingFacts
                {
                    ContractNo = order.P_CONTRACTS?.ContractNo,
                    ContractDate = order.P_CONTRACTS?.IssuedDate,
                    InvoiceNo = firstInvoice?.InvoiceNo,
                    InvoiceDate = firstInvoice?.InvoiceDate,
                    ReceiptDate = incomes.Count > 0 ? (DateTime?)incomes[0].IssueDateTime : null
                };

                ViewBag.Contracts = contracts;
                ViewBag.Invoices = invoices;
                ViewBag.Incomes = incomes;
                ViewBag.TrackingStatus = BuildTrackingStatus(order, facts);

                return View(order);
            }
        }

        /// <summary>
        /// Bosqichlar holati (yashil/qizil). Kuzatuvsiz (AsNoTracking) buyurtmaning ko'rsatish
        /// maydonlari (Status, ContractStatus, ...) ham to'ldiriladi — bazaga yozilmaydi.
        /// </summary>
        private static OrderTrackingStatusViewModel BuildTrackingStatus(P_ORDERS order, TrackingFacts facts)
        {
            bool hasInvoice = facts.InvoiceNo != null || facts.InvoiceDate.HasValue;
            var status = new OrderTrackingStatusViewModel
            {
                OrderID = order.ID,
                OrderNo = order.OrderNo,
                OrderCreated = true,
                OrderCreatedDate = order.IssuedDate,
                OrderStatus = "green",

                ContractCreated = order.ContractID.HasValue,
                ContractNo = order.ContractID.HasValue ? facts.ContractNo : null,
                ContractDate = order.ContractID.HasValue ? facts.ContractDate : null,

                InvoiceCreated = hasInvoice,
                InvoiceNo = facts.InvoiceNo,
                InvoiceDate = facts.InvoiceDate,

                WarehouseReceived = facts.ReceiptDate.HasValue,
                WarehouseReceiptDate = facts.ReceiptDate
            };
            status.ContractStatus = status.ContractCreated ? "green" : "red";
            status.InvoiceStatus = status.InvoiceCreated ? "green" : "red";
            status.WarehouseStatus = status.WarehouseReceived ? "green" : "red";

            order.ContractStatus = status.ContractCreated ? "Created" : "NotCreated";
            order.InvoiceStatus = status.InvoiceCreated ? "Created" : "NotCreated";
            order.WarehouseStatus = status.WarehouseReceived ? "Received" : "NotReceived";
            order.Status = status.WarehouseReceived ? "Received"
                : status.InvoiceCreated ? "Invoiced"
                : status.ContractCreated ? "ContractCreated"
                : "Created";

            return status;
        }
    }
}
