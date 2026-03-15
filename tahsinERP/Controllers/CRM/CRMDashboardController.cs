using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// CRM Dashboard - shartnomalar, buyurtmalar, invoyslar bajarilishi va kechikishlari.
    /// ERP standartlariga mos (SAP, Oracle, MS Dynamics) KPI va hisobotlar.
    /// </summary>
    public class CRMDashboardController : Controller
    {
        public ActionResult Index(DateTime? fromDate, DateTime? toDate)
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "CRMDashboard") &&
                !RoleHelper.IsViewPermitted(User.Identity.Name, "SOrder") &&
                !RoleHelper.IsViewPermitted(User.Identity.Name, "SContract") &&
                !RoleHelper.IsViewPermitted(User.Identity.Name, "SalesInvoices"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var from = fromDate ?? DateTime.Now.AddMonths(-11).Date;
            var to = toDate ?? DateTime.Now.Date;
            if (from > to) { var t = from; from = to; to = t; }

            using (var db = new DBTHSNEntities())
            {
                var model = new CRMDashboardViewModel();

                var today = DateTime.Now.Date;

                // ——— Shartnomalar ———
                var contracts = db.S_CONTRACTS
                    .Include(c => c.CUSTOMER)
                    .Where(c => c.IsDeleted != true)
                    .ToList();

                model.TotalContracts = contracts.Count;
                model.ContractsOverdue = contracts.Count(c => c.DueDate < today);
                model.ContractsCompleted = contracts.Count(c => db.SALES_INVOICES.Any(inv => inv.ContractID == c.ID));
                model.ContractsTotalAmount = (decimal)(contracts.Sum(c => c.Amount) ?? 0);

                // ——— Buyurtmalar ———
                var orders = db.S_ORDERS
                    .Include(o => o.CUSTOMER)
                    .Include(o => o.CURRENCy1)
                    .Where(o => o.IsDeleted != true)
                    .ToList();

                model.TotalOrders = orders.Count;
                var orderIdsWithInvoice = db.SALES_INVOICES.Where(inv => inv.OrderID != null).Select(inv => inv.OrderID.Value).Distinct().ToHashSet();
                model.OrdersWithInvoice = orders.Count(o => orderIdsWithInvoice.Contains(o.ID));
                model.OrdersWithoutInvoice = orders.Count(o => !orderIdsWithInvoice.Contains(o.ID));
                model.OrdersTotalAmount = (decimal)(orders.Sum(o => o.Amount) ?? 0);

                // Yetkazib berish rejasi bo'yicha kechikkan (DeliveryDate o'tgan, QtyShipped < QtyPlanned)
                var deliverySchedules = db.SORDER_DELIVERY_SCHEDULE
                    .Include(s => s.S_ORDERS)
                    .Include(s => s.S_ORDERS.CUSTOMER)
                    .Include(s => s.PRODUCT)
                    .Where(s => s.S_ORDERS != null && (s.S_ORDERS.IsDeleted == false || s.S_ORDERS.IsDeleted == null))
                    .ToList();
                var deliveryOverdue = deliverySchedules
                    .Where(s => s.DeliveryDate < today && s.QtyShipped < s.QtyPlanned)
                    .Select(s => new CRMDeliveryOverdueItem
                    {
                        ScheduleId = s.ID,
                        OrderId = s.SOrderID ?? 0,
                        OrderNo = s.S_ORDERS?.OrderNo ?? "",
                        CustomerName = s.S_ORDERS?.CUSTOMER?.Name ?? "",
                        ProductName = s.PRODUCT?.PNo ?? s.PRODUCT?.Name ?? "",
                        DeliveryDate = s.DeliveryDate,
                        QtyPlanned = s.QtyPlanned,
                        QtyShipped = s.QtyShipped,
                        DaysOverdue = (today - s.DeliveryDate.Date).Days
                    })
                    .OrderByDescending(x => x.DaysOverdue)
                    .Take(50)
                    .ToList();
                model.DeliveryOverdueList = deliveryOverdue;
                model.OrdersDeliveryOverdue = deliverySchedules.Count(s => s.DeliveryDate < today && s.QtyShipped < s.QtyPlanned);

                // ——— Invoyslar ———
                var invoices = db.SALES_INVOICES
                    .Include(i => i.CUSTOMER)
                    .ToList();
                model.TotalInvoices = invoices.Count;
                model.InvoicesOverdue = invoices.Count(i => i.DueDate.HasValue && i.DueDate.Value < today);
                model.InvoicesTotalAmount = invoices.Sum(i => i.TotalAmount);

                // ——— Mijozlar ———
                model.TotalCustomers = db.CUSTOMERS.Count(c => c.IsDeleted != true);
                var yearStart = new DateTime(today.Year, 1, 1);
                model.CustomersWithOrdersThisYear = orders.Where(o => o.IssuedDate >= yearStart).Select(o => o.CustomerID).Distinct().Count();

                // ——— Grafik: shartnomalar oy bo'yicha ———
                var contractsInRange = contracts.Where(c => c.IssuedDate >= from && c.IssuedDate <= to).ToList();
                model.ContractsByMonth = contractsInRange
                    .GroupBy(c => new { c.IssuedDate.Year, c.IssuedDate.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .Select(g => new CRMChartPoint
                    {
                        Label = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Value = g.Count()
                    })
                    .ToList();

                // ——— Grafik: buyurtmalar oy bo'yicha ———
                var ordersInRange = orders.Where(o => o.IssuedDate >= from && o.IssuedDate <= to).ToList();
                model.OrdersByMonth = ordersInRange
                    .GroupBy(o => new { o.IssuedDate.Year, o.IssuedDate.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .Select(g => new CRMChartPoint
                    {
                        Label = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Value = g.Count()
                    })
                    .ToList();

                // ——— Buyurtma bajarilishi: invoys bilan / invoysiz ———
                model.OrderFulfillmentStatus = new List<CRMChartPoint>
                {
                    new CRMChartPoint { Label = "Invoys yaratilgan", Value = model.OrdersWithInvoice },
                    new CRMChartPoint { Label = "Invoys yaratilmagan", Value = model.OrdersWithoutInvoice }
                };

                // ——— Shartnoma holati: muddati o'tgan / bajarilgan (invoys bor) / davom etayotgan ———
                int contractOverdueCount = model.ContractsOverdue;
                int contractWithInvoice = model.ContractsCompleted;
                int contractInProgress = model.TotalContracts - contractWithInvoice - contractOverdueCount;
                if (contractInProgress < 0) contractInProgress = 0;
                model.ContractCompletionStatus = new List<CRMChartPoint>
                {
                    new CRMChartPoint { Label = "Muddati o'tgan", Value = contractOverdueCount },
                    new CRMChartPoint { Label = "Bajarilgan (invoys bor)", Value = contractWithInvoice },
                    new CRMChartPoint { Label = "Jarayonda", Value = contractInProgress }
                };

                // ——— Invoys yaratilmagan buyurtmalar ro'yxati ———
                model.OrdersWithoutInvoiceList = orders
                    .Where(o => !orderIdsWithInvoice.Contains(o.ID))
                    .OrderByDescending(o => o.IssuedDate)
                    .Take(30)
                    .Select(o => new CRMOrderWithoutInvoiceItem
                    {
                        OrderId = o.ID,
                        OrderNo = o.OrderNo ?? "",
                        CustomerName = o.CUSTOMER?.Name ?? "",
                        IssuedDate = o.IssuedDate,
                        Amount = (decimal?)o.Amount,
                        CurrencyCode = o.CURRENCy1?.Shortname ?? o.Currency ?? ""
                    })
                    .ToList();

                // ——— Muddati o'tgan shartnomalar ———
                model.ContractsOverdueList = contracts
                    .Where(c => c.DueDate < today)
                    .OrderBy(c => c.DueDate)
                    .Take(30)
                    .Select(c => new CRMContractOverdueItem
                    {
                        ContractId = c.ID,
                        ContractNo = c.ContractNo ?? "",
                        CustomerName = c.CUSTOMER?.Name ?? "",
                        DueDate = c.DueDate,
                        DaysOverdue = (today - c.DueDate.Date).Days,
                        Amount = (decimal?)c.Amount
                    })
                    .ToList();

                // ——— Muddati o'tgan invoyslar ———
                model.InvoicesOverdueList = invoices
                    .Where(i => i.DueDate.HasValue && i.DueDate.Value < today)
                    .OrderBy(i => i.DueDate)
                    .Take(30)
                    .Select(i => new CRMInvoiceOverdueItem
                    {
                        InvoiceId = i.ID,
                        InvoiceNo = i.InvoiceNo ?? "",
                        CustomerName = i.CUSTOMER?.Name ?? "",
                        DueDate = i.DueDate,
                        DaysOverdue = i.DueDate.HasValue ? (today - i.DueDate.Value.Date).Days : 0,
                        TotalAmount = i.TotalAmount
                    })
                    .ToList();

                // ——— Mijoz bo'yicha buyurtma summasi (top 10) ———
                model.TopCustomersByOrderAmount = orders
                    .GroupBy(o => new { o.CustomerID, Name = o.CUSTOMER?.Name ?? "" })
                    .Select(g => new CRMCustomerOrderSum
                    {
                        CustomerId = g.Key.CustomerID,
                        CustomerName = g.Key.Name,
                        OrderCount = g.Count(),
                        TotalAmount = (decimal)(g.Sum(o => o.Amount) ?? 0)
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .Take(10)
                    .ToList();

                ViewBag.FromDate = from;
                ViewBag.ToDate = to;
                return View(model);
            }
        }
    }
}
