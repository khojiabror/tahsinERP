using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// SRM Dashboard - Jahon standartidagi ERP larda bo'ladigan ta'minotchi munosabatlari hisobotlari
    /// Spend, Aging, Delivery performance, Supplier performance
    /// </summary>
    public class SRMDashboardController : Controller
    {
        public async Task<ActionResult> Index(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                if (User?.Identity?.Name == null || !RoleHelper.IsViewPermitted(User.Identity.Name, "Supplier"))
                {
                    return RedirectToAction("Index", "ViewNotAuthorized");
                }

                fromDate = fromDate ?? DateTime.Now.AddMonths(-1);
                toDate = toDate ?? DateTime.Now;

                using (var db = new DBTHSNEntities())
                {
                    // 1. Spend by Supplier - Xarajat ta'minotchi bo'yicha (guruhlash xotirada - decimal cast xatoligini oldini olish)
                    var invoiceData = await db.P_INVOICES
                        .Where(i => i.InvoiceDate >= fromDate.Value && i.InvoiceDate <= toDate.Value &&
                            (i.IsDeleted == null || i.IsDeleted == false))
                        .Select(i => new { i.SupplierID, i.Amount })
                        .ToListAsync();

                    var spendRaw = invoiceData
                        .GroupBy(i => i.SupplierID)
                        .Select(g => new { SupplierID = g.Key, InvoiceCount = g.Count(), TotalSpend = g.Sum(x => x.Amount) })
                        .OrderByDescending(x => x.TotalSpend)
                        .Take(15)
                        .ToList();

                var spendSupplierIds = spendRaw.Select(x => x.SupplierID).Distinct().ToList();
                Dictionary<int, string> spendSupplierNames;
                if (spendSupplierIds.Count == 0)
                    spendSupplierNames = new Dictionary<int, string>();
                else
                    spendSupplierNames = await db.SUPPLIERS
                        .Where(s => spendSupplierIds.Contains(s.ID))
                        .ToDictionaryAsync(s => s.ID, s => s.Name ?? "Noma'lum");

                var spendBySupplier = spendRaw.Select(x => new SRMSpendBySupplierItem
                {
                    SupplierID = x.SupplierID,
                    SupplierName = spendSupplierNames.ContainsKey(x.SupplierID) ? spendSupplierNames[x.SupplierID] : "Noma'lum",
                    InvoiceCount = x.InvoiceCount,
                    TotalSpend = Convert.ToDecimal(x.TotalSpend)
                }).ToList();

                // 2. Payables aging
                var supplierOutstanding = SRMDashboardHelper.GetSupplierOutstanding(db, toDate.Value);
                var totalPayables = supplierOutstanding.Sum(s => s.Outstanding);

                // 3. On-time delivery (F_WAYBILLS - rejalashtirilgan vs faktik yetkazib berish)
                var waybillsWithDelivery = await db.F_WAYBILLS
                    .Where(w => w.PlannedDeliveryDate.HasValue && w.ActualDeliveryDate.HasValue &&
                        w.PlannedDeliveryDate >= fromDate.Value && w.PlannedDeliveryDate <= toDate.Value &&
                        (w.IsDeleted == null || w.IsDeleted == false))
                    .Select(w => new { w.PlannedDeliveryDate, w.ActualDeliveryDate })
                    .ToListAsync();

                var onTimeCount = waybillsWithDelivery.Count(w => w.ActualDeliveryDate <= w.PlannedDeliveryDate);
                var totalDeliveries = waybillsWithDelivery.Count;
                var onTimeRate = totalDeliveries > 0 ? (double)onTimeCount / totalDeliveries * 100 : 0;

                // 4. Summary KPIs
                var totalSuppliers = await db.SUPPLIERS.Where(s => s.IsDeleted != true).CountAsync();
                var totalSpend = spendBySupplier.Sum(x => x.TotalSpend);
                var totalInvoices = await db.P_INVOICES
                    .Where(i => i.InvoiceDate >= fromDate.Value && i.InvoiceDate <= toDate.Value &&
                        (i.IsDeleted == null || i.IsDeleted == false))
                    .CountAsync();
                var totalContracts = await db.P_CONTRACTS.Where(c => c.IsDeleted != true).CountAsync();
                var totalOrders = await db.P_ORDERS
                    .Where(o => o.IssuedDate >= fromDate.Value && o.IssuedDate <= toDate.Value &&
                        (o.IsDeleted == null || o.IsDeleted == false))
                    .CountAsync();

                ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");
                ViewBag.TotalSuppliers = totalSuppliers;
                ViewBag.TotalSpend = totalSpend;
                ViewBag.TotalInvoices = totalInvoices;
                ViewBag.TotalContracts = totalContracts;
                ViewBag.TotalOrders = totalOrders;
                ViewBag.TotalPayables = totalPayables;
                ViewBag.OnTimeRate = Math.Round(onTimeRate, 1);
                ViewBag.OnTimeCount = onTimeCount;
                ViewBag.TotalDeliveries = totalDeliveries;
                ViewBag.SpendBySupplier = spendBySupplier;

                var outstandingIds = supplierOutstanding.Select(x => x.SupplierID).Distinct().ToList();
                Dictionary<int, string> supplierNames;
                if (outstandingIds.Count == 0)
                    supplierNames = new Dictionary<int, string>();
                else
                    supplierNames = await db.SUPPLIERS
                        .Where(s => outstandingIds.Contains(s.ID))
                        .ToDictionaryAsync(s => s.ID, s => s.Name ?? "Noma'lum");

                ViewBag.PayablesBySupplier = supplierOutstanding
                    .OrderByDescending(s => s.Outstanding)
                    .Take(15)
                    .Select(s => new SRMPayablesBySupplierItem
                    {
                        SupplierID = s.SupplierID,
                        SupplierName = supplierNames.ContainsKey(s.SupplierID) ? supplierNames[s.SupplierID] : "Noma'lum",
                        Outstanding = s.Outstanding
                    })
                    .ToList();
                }

                return View();
            }
            catch (Exception ex)
            {
                // 500 xatosini oldini olish - xatolikni log qilish
                LogHelper.LogToDatabase(User?.Identity?.Name ?? "System", "SRMDashboard", "Index", $"Xatolik: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }
    }

    public static class SRMDashboardHelper
    {
        public static List<SupplierOutstandingItem> GetSupplierOutstanding(DBTHSNEntities db, DateTime asOfDate)
        {
            Func<string, string> normalize = s => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToUpper();
            var invoices = db.P_INVOICES
                .Where(i => i.InvoiceDate <= asOfDate && (i.IsDeleted == null || i.IsDeleted == false))
                .Select(i => new { i.ID, i.SupplierID, i.ContractID, i.InvoiceDate, i.Amount })
                .ToList()
                .Select(i => new { i.ID, i.SupplierID, i.ContractID, i.InvoiceDate, Amount = Convert.ToDecimal(i.Amount) })
                .ToList();

            var contractIds = invoices.Select(i => i.ContractID).Distinct().ToList();
            var contractNoById = contractIds.Count == 0
                ? new Dictionary<int, string>()
                : db.P_CONTRACTS
                    .Where(c => contractIds.Contains(c.ID))
                    .Select(c => new { c.ID, c.ContractNo })
                    .ToList()
                    .ToDictionary(x => x.ID, x => normalize(x.ContractNo));

            var payments = db.FIN_TRANSACTIONS
                .Where(t => t.TxnDate <= asOfDate && t.Direction == 2 && t.CounterpartyType == 2 &&
                    t.CounterpartyID.HasValue && (t.IsDeleted == null || t.IsDeleted == false))
                .Select(t => new { t.ID, SupplierID = t.CounterpartyID.Value, t.TxnDate, t.Amount, t.SourceDocType, t.SourceDocID, t.ContractNo })
                .ToList();

            var invoiceRemaining = invoices.ToDictionary(i => i.ID, i => i.Amount);
            var invoiceBySupplier = invoices
                .GroupBy(i => new { i.SupplierID, ContractNo = contractNoById.ContainsKey(i.ContractID) ? contractNoById[i.ContractID] : "" })
                .ToDictionary(g => Tuple.Create(g.Key.SupplierID, g.Key.ContractNo), g => g.OrderBy(i => i.InvoiceDate).Select(i => i.ID).ToList());

            var paymentRemainders = new List<(int SupplierID, string ContractNo, DateTime TxnDate, decimal Amount)>();
            foreach (var payment in payments)
            {
                var remaining = payment.Amount;
                if (payment.SourceDocType == "PurchaseInvoicePayment" && payment.SourceDocID.HasValue &&
                    invoiceRemaining.ContainsKey(payment.SourceDocID.Value))
                {
                    var invBal = invoiceRemaining[payment.SourceDocID.Value];
                    var applied = Math.Min(invBal, remaining);
                    invoiceRemaining[payment.SourceDocID.Value] = invBal - applied;
                    remaining -= applied;
                }
                if (remaining > 0)
                    paymentRemainders.Add((payment.SupplierID, normalize(payment.ContractNo), payment.TxnDate, remaining));
            }

            foreach (var g in paymentRemainders.GroupBy(p => Tuple.Create(p.SupplierID, p.ContractNo)))
            {
                if (!invoiceBySupplier.ContainsKey(g.Key)) continue;
                var queue = new Queue<int>(invoiceBySupplier[g.Key]);
                foreach (var pay in g.OrderBy(p => p.TxnDate))
                {
                    var remaining = pay.Amount;
                    while (remaining > 0 && queue.Count > 0)
                    {
                        var invId = queue.Peek();
                        var invBal = invoiceRemaining[invId];
                        if (invBal <= 0) { queue.Dequeue(); continue; }
                        var applied = Math.Min(invBal, remaining);
                        invoiceRemaining[invId] -= applied;
                        remaining -= applied;
                        if (invoiceRemaining[invId] <= 0) queue.Dequeue();
                    }
                }
            }

            return invoices
                .GroupBy(i => i.SupplierID)
                .Select(g => new SupplierOutstandingItem
                {
                    SupplierID = g.Key,
                    Outstanding = g.Sum(i => invoiceRemaining[i.ID])
                })
                .Where(x => x.Outstanding > 0)
                .ToList();
        }
    }

    public class SupplierOutstandingItem
    {
        public int SupplierID { get; set; }
        public decimal Outstanding { get; set; }
    }
}
