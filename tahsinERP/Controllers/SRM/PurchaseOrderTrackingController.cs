using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers.SRM
{
    public class PurchaseOrderTrackingController : Controller
    {
        // Olish so'rovi: PurchaseOrderTracking
        public async Task<ActionResult> Index(string purchaseTypeFilter = "", string statusFilter = "")
        {
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "PurchaseOrderTracking", "Index"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            using (var db = new DBTHSNEntities())
            {
                var query = db.P_ORDERS
                    .Include(o => o.SUPPLIER)
                    .Include(o => o.COMPANy)
                    .Include(o => o.P_CONTRACTS)
                    .Include(o => o.P_INVOICES)
                    .Include(o => o.P_ORDER_PRODUCTS)
                    .Where(o => o.IsDeleted != true)
                    .AsQueryable();

                // Filtrlash bo'yicha purchase type
                if (!string.IsNullOrEmpty(purchaseTypeFilter))
                {
                    byte purchaseType = purchaseTypeFilter == "Indirect" ? (byte)1 : (byte)0;
                    query = query.Where(o => o.PurchaseType == purchaseType);
                }

                // Filtrlash bo'yicha holat
                if (!string.IsNullOrEmpty(statusFilter))
                {
                    query = query.Where(o => o.Status == statusFilter);
                }

                var orders = await query
                    .OrderByDescending(o => o.IssuedDate)
                    .ToListAsync();

                // Calculate tracking holat uchun har bir order
                var trackingData = orders.Select(o => new
                {
                    Order = o,
                    TrackingStatus = CalculateTrackingStatus(o)
                }).ToList();

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
            if (!tahsinERP.Models.RoleHelper.IsViewPermitted(User.Identity.Name, "PurchaseOrderTracking", "Details"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (var db = new DBTHSNEntities())
            {
                var order = await db.P_ORDERS
                    .Include(o => o.SUPPLIER)
                    .Include(o => o.COMPANy)
                    .Include(o => o.CURRENCy)
                    .Include(o => o.P_CONTRACTS)
                    .Include(o => o.P_INVOICES)
                    .Include(o => o.P_ORDER_PRODUCTS.Select(p => p.PRODUCT))
                    .Include(o => o.P_ORDER_PRODUCTS.Select(p => p.UNIT))
                    .Include(o => o.P_REQUISITIONS)
                    .FirstOrDefaultAsync(o => o.ID == id && o.IsDeleted != true);

                if (order == null)
                {
                    return HttpNotFound();
                }

                // Olish related contracts
                var contracts = await db.P_CONTRACTS
                    .Where(c => c.IsDeleted != true && 
                                (order.ContractID.HasValue ? c.ID == order.ContractID.Value : true))
                    .ToListAsync();

                // Olish related invoices
                var invoices = await db.P_INVOICES
                    .Where(i => i.IsDeleted != true && i.OrderID == order.ID)
                    .ToListAsync();

                // Olish related income hujjatlar (warehouse receipts)
                var invoiceIds = invoices.Select(inv => inv.ID).ToList();
                var incomes = await db.INCOMES
                    .Where(inc => inc.InvoiceID.HasValue && invoiceIds.Contains(inc.InvoiceID.Value) && inc.IsDeleted != true)
                    .ToListAsync();

                // Calculate tracking holat
                var trackingStatus = CalculateTrackingStatus(order);

                // Yangilash tracking holat bilan warehouse information
                if (incomes.Any())
                {
                    trackingStatus.WarehouseReceived = true;
                    trackingStatus.WarehouseStatus = "green";
                    trackingStatus.WarehouseReceiptDate = incomes.First().IssueDateTime;
                }
                else
                {
                    trackingStatus.WarehouseReceived = false;
                    trackingStatus.WarehouseStatus = "red";
                }
                ViewBag.Contracts = contracts;
                ViewBag.Invoices = invoices;
                ViewBag.Incomes = incomes;
                ViewBag.TrackingStatus = trackingStatus;

                return View(order);
            }
        }

        // yordamchi: Calculate tracking holat uchun an order
        private OrderTrackingStatus CalculateTrackingStatus(P_ORDERS order)
        {
            var status = new OrderTrackingStatus
            {
                OrderID = order.ID,
                OrderNo = order.OrderNo,
                OrderCreated = true,
                OrderCreatedDate = order.IssuedDate,
                OrderStatus = "green"
            };

            // Tekshirish contract holat
            if (order.ContractID.HasValue)
            {
                status.ContractCreated = true;
                status.ContractStatus = "green";
                if (order.P_CONTRACTS != null)
                {
                    status.ContractNo = order.P_CONTRACTS.ContractNo;
                    status.ContractDate = order.P_CONTRACTS.IssuedDate;
                }
            }
            else
            {
                status.ContractCreated = false;
                status.ContractStatus = "red";
            }

            // Tekshirish invoice holat
            if (order.P_INVOICES != null && order.P_INVOICES.Any(i => i.IsDeleted != true))
            {
                status.InvoiceCreated = true;
                status.InvoiceStatus = "green";
                var firstInvoice = order.P_INVOICES.First(i => i.IsDeleted != true);
                status.InvoiceNo = firstInvoice.InvoiceNo;
                status.InvoiceDate = firstInvoice.InvoiceDate;
            }
            else
            {
                status.InvoiceCreated = false;
                status.InvoiceStatus = "red";
            }

            // Tekshirish warehouse receipt holat (Income)
            // Note: This should be called within a foydalanib block in the calling method
            // uchun now, we'll tekshirish agar invoices exist va have related incomes
            if (order.P_INVOICES != null && order.P_INVOICES.Any(i => i.IsDeleted != true))
            {
                var invoiceIds = order.P_INVOICES.Where(i => i.IsDeleted != true).Select(i => i.ID).ToList();
                // Note: INCOMES relationship needs ga be loaded yoki checked separately
                // This is a simplified tekshirish - full implementation should query INCOMES jadval
                status.WarehouseReceived = false; // Will be updated when INCOMES are loaded
                status.WarehouseStatus = "red";
            }
            else
            {
                status.WarehouseReceived = false;
                status.WarehouseStatus = "red";
            }

            // Yangilash order holat fields
            order.ContractStatus = status.ContractCreated ? "Created" : "NotCreated";
            order.InvoiceStatus = status.InvoiceCreated ? "Created" : "NotCreated";
            order.WarehouseStatus = status.WarehouseReceived ? "Received" : "NotReceived";

            // Aniqlash overall holat
            if (status.WarehouseReceived)
            {
                order.Status = "Received";
            }
            else if (status.InvoiceCreated)
            {
                order.Status = "Invoiced";
            }
            else if (status.ContractCreated)
            {
                order.Status = "ContractCreated";
            }
            else
            {
                order.Status = "Created";
            }

            return status;
        }
    }

    // ViewModel uchun order tracking holat
    public class OrderTrackingStatus
    {
        public int OrderID { get; set; }
        public string OrderNo { get; set; }
        public bool OrderCreated { get; set; }
        public DateTime? OrderCreatedDate { get; set; }
        public string OrderStatus { get; set; } // green/red

        public bool ContractCreated { get; set; }
        public string ContractNo { get; set; }
        public DateTime? ContractDate { get; set; }
        public string ContractStatus { get; set; } // green/red

        public bool InvoiceCreated { get; set; }
        public string InvoiceNo { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string InvoiceStatus { get; set; } // green/red

        public bool WarehouseReceived { get; set; }
        public DateTime? WarehouseReceiptDate { get; set; }
        public string WarehouseStatus { get; set; } // green/red
    }
}

