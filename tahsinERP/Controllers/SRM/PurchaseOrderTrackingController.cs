using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
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

        // Olish so'rovi: PurchaseOrderTracking
        public async Task<ActionResult> Index(string purchaseTypeFilter = "", string statusFilter = "")
        {
            if (!RoleHelper.IsViewPermitted(CurrentUserName, Module, "Index"))
            {
                return NotAuthorized();
            }

            using (var db = new DBTHSNEntities())
            {
                var query = db.P_ORDERS.AsNoTracking().Where(o => o.IsDeleted != true);

                if (!string.IsNullOrEmpty(purchaseTypeFilter))
                {
                    byte purchaseType = purchaseTypeFilter == "Indirect" ? (byte)1 : (byte)0;
                    query = query.Where(o => o.PurchaseType == purchaseType);
                }

                // Bosqichlar bitta so'rovda: shartnoma, birinchi faol invoys va omborga kirim sanasi.
                // Avval buyurtma qatorlari/kompaniya ham Include qilinib, ombor holati doim "qizil" edi (SRM-B07).
                var rows = await query
                    .OrderByDescending(o => o.IssuedDate)
                    .Select(o => new
                    {
                        Order = o,
                        Supplier = o.SUPPLIER,
                        Facts = new TrackingFacts
                        {
                            ContractNo = o.P_CONTRACTS.ContractNo,
                            ContractDate = (DateTime?)o.P_CONTRACTS.IssuedDate,
                            InvoiceNo = o.P_INVOICES.Where(i => i.IsDeleted != true).OrderBy(i => i.ID).Select(i => i.InvoiceNo).FirstOrDefault(),
                            InvoiceDate = o.P_INVOICES.Where(i => i.IsDeleted != true).OrderBy(i => i.ID).Select(i => (DateTime?)i.InvoiceDate).FirstOrDefault(),
                            ReceiptDate = o.P_INVOICES.Where(i => i.IsDeleted != true)
                                .SelectMany(i => i.INCOMES)
                                .Where(inc => inc.IsDeleted != true)
                                .Min(inc => (DateTime?)inc.IssueDateTime)
                        }
                    })
                    .ToListAsync();

                var trackingData = new List<PurchaseOrderTrackingIndexItemViewModel>();
                foreach (var row in rows)
                {
                    row.Order.SUPPLIER = row.Supplier;
                    var status = BuildTrackingStatus(row.Order, row.Facts);

                    // Filtr ko'rsatilayotgan (hisoblangan) holat bo'yicha — DB ustuni bo'yicha emas
                    if (!string.IsNullOrEmpty(statusFilter) && row.Order.Status != statusFilter)
                    {
                        continue;
                    }

                    trackingData.Add(new PurchaseOrderTrackingIndexItemViewModel
                    {
                        POrder = row.Order,
                        TrackingStatus = status
                    });
                }

                ViewBag.PurchaseTypeFilter = purchaseTypeFilter;
                ViewBag.PurchaseTypeList = new SelectList(new[]
                {
                    new { Value = "", Text = "All" },
                    new { Value = "Direct", Text = "Direct Purchase" },
                    new { Value = "Indirect", Text = "Indirect Purchase" }
                }, "Value", "Text", purchaseTypeFilter);

                ViewBag.StatusFilter = statusFilter;
                ViewBag.StatusList = new SelectList(new[]
                {
                    new { Value = "", Text = "All" },
                    new { Value = "Created", Text = "Created" },
                    new { Value = "ContractCreated", Text = "Contract Created" },
                    new { Value = "Invoiced", Text = "Invoiced" },
                    new { Value = "Received", Text = "Received" }
                }, "Value", "Text", statusFilter);

                return View(trackingData);
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
