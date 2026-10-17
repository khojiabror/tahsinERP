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
                // Foydalanuvchi tanlagan oxirgi kun to'liq kiradi (sana 00:00 bo'lgani uchun <= oxirgi kunni tashlab yuborardi, SRM-B24)
                var toExclusive = toDate.Value.Date.AddDays(1);

                using (var db = new DBTHSNEntities())
                {
                    // 1. Spend by Supplier - Xarajat ta'minotchi bo'yicha (guruhlash xotirada - decimal cast xatoligini oldini olish)
                    var invoiceData = await db.P_INVOICES
                        .Where(i => i.InvoiceDate >= fromDate.Value && i.InvoiceDate < toExclusive &&
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
                        w.PlannedDeliveryDate >= fromDate.Value && w.PlannedDeliveryDate < toExclusive &&
                        (w.IsDeleted == null || w.IsDeleted == false))
                    .Select(w => new { w.PlannedDeliveryDate, w.ActualDeliveryDate })
                    .ToListAsync();

                var onTimeCount = waybillsWithDelivery.Count(w => w.ActualDeliveryDate <= w.PlannedDeliveryDate);
                var totalDeliveries = waybillsWithDelivery.Count;
                var onTimeRate = totalDeliveries > 0 ? (double)onTimeCount / totalDeliveries * 100 : 0;

                // 4. Summary KPIs
                var totalSuppliers = await db.SUPPLIERS.Where(s => s.IsDeleted != true).CountAsync();
                // Jami xarajat barcha ta'minotchilar bo'yicha (avval faqat top-15 yig'ilardi, SRM-B08)
                var totalSpend = Convert.ToDecimal(invoiceData.Sum(x => x.Amount ?? 0));
                var totalInvoices = invoiceData.Count;
                var totalContracts = await db.P_CONTRACTS.Where(c => c.IsDeleted != true).CountAsync();
                var totalOrders = await db.P_ORDERS
                    .Where(o => o.IssuedDate >= fromDate.Value && o.IssuedDate < toExclusive &&
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
        private class OpeningContractPayableRow
        {
            public int CounterpartyID { get; set; }
            public string ContractNo { get; set; }
            public decimal Amount { get; set; }
        }

        public static List<SupplierOutstandingItem> GetSupplierOutstanding(DBTHSNEntities db, DateTime asOfDate)
        {
            Func<string, string> normalize = s => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToUpper();
            DateTime? effectiveFrom = null;
            try
            {
                effectiveFrom = db.Database.SqlQuery<DateTime?>(
                    "SELECT MAX(SnapshotDate) FROM FIN_OPENING_CONTRACT_BALANCES WHERE SnapshotDate <= @p0 AND BalanceType = 2",
                    new System.Data.SqlClient.SqlParameter("@p0", asOfDate.Date)).FirstOrDefault();
            }
            catch { }
            if (!effectiveFrom.HasValue)
            {
                effectiveFrom = db.FIN_BALANCE_SNAPSHOTS
                    .Where(s => s.SourceType == "OpeningBalance" && s.SnapshotDate <= asOfDate)
                    .OrderByDescending(s => s.SnapshotDate)
                    .Select(s => (DateTime?)s.SnapshotDate)
                    .FirstOrDefault();
            }

            var invoicesQuery = db.P_INVOICES
                .Where(i => i.InvoiceDate <= asOfDate &&
                            (i.IsDeleted == null || i.IsDeleted == false));
            if (effectiveFrom.HasValue)
            {
                var fromDate = effectiveFrom.Value;
                invoicesQuery = invoicesQuery.Where(i =>
                    i.InvoiceDate > fromDate ||
                    db.INCOMES.Any(inc => inc.InvoiceID == i.ID &&
                                          inc.IssueDateTime > fromDate &&
                                          inc.IssueDateTime <= asOfDate &&
                                          (inc.IsDeleted == null || inc.IsDeleted == false)));
            }

            var invoices = invoicesQuery
                .Select(i => new { i.ID, i.SupplierID, i.ContractID, i.InvoiceDate, i.Amount })
                .ToList()
                .Select(i => new { i.ID, i.SupplierID, i.ContractID, i.InvoiceDate, Amount = Convert.ToDecimal(i.Amount) })
                .ToList();

            var invoiceIds = invoices.Select(i => i.ID).ToList();
            var incomeTotalsByInvoice = invoiceIds.Count == 0
                ? new Dictionary<int, decimal>()
                : db.INCOMES
                    .Where(inc => inc.InvoiceID.HasValue &&
                                  invoiceIds.Contains(inc.InvoiceID.Value) &&
                                  inc.IsDeleted != true &&
                                  inc.TotalPrice.HasValue &&
                                  inc.IssueDateTime <= asOfDate)
                    .GroupBy(inc => inc.InvoiceID.Value)
                    .Select(g => new { InvoiceID = g.Key, Total = g.Sum(x => x.TotalPrice.Value) })
                    .ToList()
                    .ToDictionary(x => x.InvoiceID, x => Convert.ToDecimal(x.Total));

            invoices = invoices
                .Select(i => new
                {
                    i.ID,
                    i.SupplierID,
                    i.ContractID,
                    i.InvoiceDate,
                    Amount = incomeTotalsByInvoice.ContainsKey(i.ID) ? incomeTotalsByInvoice[i.ID] : i.Amount
                })
                .ToList();

            var contractIds = invoices.Select(i => i.ContractID).Distinct().ToList();
            var contractNoById = contractIds.Count == 0
                ? new Dictionary<int, string>()
                : db.P_CONTRACTS
                    .Where(c => contractIds.Contains(c.ID))
                    .Select(c => new { c.ID, c.ContractNo })
                    .ToList()
                    .ToDictionary(x => x.ID, x => normalize(x.ContractNo));

            var invoiceWork = invoices.Select(i => new
            {
                i.ID,
                i.SupplierID,
                i.InvoiceDate,
                i.Amount,
                ContractNo = contractNoById.ContainsKey(i.ContractID) ? contractNoById[i.ContractID] : ""
            }).ToList();

            if (effectiveFrom.HasValue)
            {
                try
                {
                    var openingRows = db.Database.SqlQuery<OpeningContractPayableRow>(
                        "SELECT CounterpartyID, ContractNo, Amount FROM FIN_OPENING_CONTRACT_BALANCES WHERE SnapshotDate = @p0 AND BalanceType = 2",
                        new System.Data.SqlClient.SqlParameter("@p0", effectiveFrom.Value.Date))
                        .ToList();

                    var nextOpeningId = -1;
                    foreach (var row in openingRows.Where(x => x.CounterpartyID > 0 && x.Amount > 0))
                    {
                        invoiceWork.Add(new
                        {
                            ID = nextOpeningId--,
                            SupplierID = row.CounterpartyID,
                            InvoiceDate = effectiveFrom.Value,
                            Amount = row.Amount,
                            ContractNo = normalize(row.ContractNo)
                        });
                    }
                }
                catch { }
            }

            var payments = db.FIN_TRANSACTIONS
                .Where(t => t.TxnDate <= asOfDate &&
                    (!effectiveFrom.HasValue || t.TxnDate > effectiveFrom.Value) &&
                    (t.IsDeleted == null || t.IsDeleted == false) &&
                    (t.SourceDocType == null || (t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice")) &&
                    (
                        // Standard supplier payments
                        (t.Direction == 2 && t.CounterpartyType == 2 && t.CounterpartyID.HasValue)
                        // Linked invoice payments should be considered even if counterparty fields are partially empty
                        || (t.SourceDocType == "PurchaseInvoicePayment" && t.SourceDocID.HasValue)
                    ))
                .Select(t => new { t.ID, t.CounterpartyID, t.TxnDate, t.Amount, t.SourceDocType, t.SourceDocID, t.ContractNo })
                .ToList();

            var linkedInvoiceIds = payments
                .Where(p => p.SourceDocType == "PurchaseInvoicePayment" && p.SourceDocID.HasValue)
                .Select(p => p.SourceDocID.Value)
                .Distinct()
                .ToList();

            var invoiceMetaById = linkedInvoiceIds.Count == 0
                ? new Dictionary<int, Tuple<int, string>>()
                : db.P_INVOICES
                    .Where(i => linkedInvoiceIds.Contains(i.ID))
                    .Select(i => new { i.ID, i.SupplierID, i.ContractID })
                    .ToList()
                    .ToDictionary(
                        x => x.ID,
                        x => Tuple.Create(
                            x.SupplierID,
                            contractNoById.ContainsKey(x.ContractID) ? contractNoById[x.ContractID] : ""));

            var invoiceTotalsByKey = invoiceWork
                .GroupBy(i => Tuple.Create(i.SupplierID, i.ContractNo))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            var paymentTotalsByKey = payments
                .Select(p =>
                {
                    if (!p.CounterpartyID.HasValue) return null;

                    var supplierId = p.CounterpartyID.Value;
                    var contractNo = normalize(p.ContractNo);
                    var valid = false;

                    if (p.SourceDocType == "PurchaseInvoicePayment" && p.SourceDocID.HasValue && invoiceMetaById.ContainsKey(p.SourceDocID.Value))
                    {
                        var meta = invoiceMetaById[p.SourceDocID.Value];
                        supplierId = meta.Item1;
                        contractNo = meta.Item2;
                        valid = true;
                    }
                    else if (!string.IsNullOrEmpty(contractNo))
                    {
                        valid = true;
                    }

                    if (!valid) return null;
                    return new { SupplierID = supplierId, ContractNo = contractNo, Amount = Math.Abs(p.Amount) };
                })
                .Where(x => x != null)
                .GroupBy(x => Tuple.Create(x.SupplierID, x.ContractNo))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            var allKeys = invoiceTotalsByKey.Keys.Union(paymentTotalsByKey.Keys).ToList();
            var supplierIds = allKeys.Select(k => k.Item1).Distinct().ToList();
            var result = new List<SupplierOutstandingItem>();

            foreach (var supplierId in supplierIds)
            {
                var invoiceTotal = allKeys
                    .Where(k => k.Item1 == supplierId && invoiceTotalsByKey.ContainsKey(k))
                    .Sum(k => invoiceTotalsByKey[k]);
                var paymentTotal = allKeys
                    .Where(k => k.Item1 == supplierId && paymentTotalsByKey.ContainsKey(k))
                    .Sum(k => paymentTotalsByKey[k]);

                var outstanding = invoiceTotal - paymentTotal;
                if (outstanding <= 0) continue;
                result.Add(new SupplierOutstandingItem
                {
                    SupplierID = supplierId,
                    Outstanding = outstanding
                });
            }

            return result;
        }
    }

    public class SupplierOutstandingItem
    {
        public int SupplierID { get; set; }
        public decimal Outstanding { get; set; }
    }
}
