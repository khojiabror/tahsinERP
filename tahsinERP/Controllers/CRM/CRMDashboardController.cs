using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.Services.Crm;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// CRM Dashboard - shartnomalar, buyurtmalar, invoyslar bajarilishi va kechikishlari.
    /// ERP standartlariga mos (SAP, Oracle, MS Dynamics) KPI va hisobotlar.
    /// </summary>
    public class CRMDashboardController : Controller
    {
        /// <summary>
        /// Muddati o'tgan invoyslar va ularning qoldig'i (so'mda). Invoys summasi QQS bilan
        /// (TotalWithVAT ?? TotalAmount), to'lovlar — FIN_TRANSACTIONS'dagi SalesInvoicePayment
        /// yozuvlari; ikkalasi ham o'z sanasidagi kurs bilan so'mga o'giriladi.
        /// </summary>
        private static List<CRMInvoiceOverdueItem> OverdueInvoicesWithBalance(DBTHSNEntities db, IQueryable<SALES_INVOICES> activeInvoices, DateTime today, UzsConverter uzs)
        {
            var candidates = activeInvoices
                .Where(i => i.DueDate.HasValue && i.DueDate.Value < today && i.StatusCode != SalesInvoiceStatus.Paid)
                .Select(i => new
                {
                    i.ID,
                    i.InvoiceNo,
                    CustomerName = i.CUSTOMER.Name,
                    i.DueDate,
                    i.InvoiceDate,
                    i.CurrencyCode,
                    Amount = i.TotalWithVAT ?? i.TotalAmount
                })
                .ToList();
            if (candidates.Count == 0)
            {
                return new List<CRMInvoiceOverdueItem>();
            }

            var ids = candidates.Select(i => (int?)i.ID).ToList();
            var paidById = db.FIN_TRANSACTIONS.AsNoTracking()
                .Where(t => t.SourceDocType == "SalesInvoicePayment" && ids.Contains(t.SourceDocID)
                            && (t.IsDeleted == null || t.IsDeleted == false))
                .GroupBy(t => new { t.SourceDocID, t.CurrencyID, Day = DbFunctions.TruncateTime(t.TxnDate) })
                .Select(g => new { g.Key.SourceDocID, g.Key.CurrencyID, g.Key.Day, Amount = g.Sum(t => t.Amount) })
                .ToList()
                .GroupBy(x => (long)x.SourceDocID.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => uzs.ToUzsOrZero(Math.Abs(x.Amount), x.CurrencyID, x.Day ?? today)));

            return candidates
                .Select(i =>
                {
                    decimal paid;
                    paidById.TryGetValue(i.ID, out paid);
                    var total = uzs.ToUzsOrZero(i.Amount, i.CurrencyCode, i.InvoiceDate);
                    return new CRMInvoiceOverdueItem
                    {
                        InvoiceId = i.ID,
                        InvoiceNo = i.InvoiceNo ?? "",
                        CustomerName = i.CustomerName ?? "",
                        DueDate = i.DueDate,
                        DaysOverdue = (today - i.DueDate.Value.Date).Days,
                        TotalAmount = total,
                        PaidAmount = paid,
                        RemainingAmount = Math.Max(0, total - paid)
                    };
                })
                .Where(i => i.RemainingAmount > 0.01m)
                .OrderBy(i => i.DueDate)
                .ToList();
        }

        private class OpeningContractReceivableRow
        {
            public int CounterpartyID { get; set; }
            public string ContractNo { get; set; }
            public decimal Amount { get; set; }
        }

        public ActionResult Index(DateTime? fromDate, DateTime? toDate)
        {
            if (!CrmReport.AnyViewPermitted(User.Identity.Name, "CRMDashboard", "SOrder", "SContract", "SalesInvoices"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var period = CrmReport.Period(fromDate, toDate, DateTime.Now.AddMonths(-11).Date);
            var from = period.From;
            var to = period.To;

            using (var db = new DBTHSNEntities())
            {
                var model = new CRMDashboardViewModel();
                // Barcha summalar so'mga, hujjat sanasidagi kurs bilan (CRM-B18)
                var uzs = new UzsConverter(db);

                var today = DateTime.Now.Date;

                // KPI va ro'yxatlar SQL'da hisoblanadi (CRM-P01/P02): avval butun S_CONTRACTS/S_ORDERS/
                // SORDER_DELIVERY_SCHEDULE/SALES_INVOICES jadvallari kuzatuv bilan yuklanib, xotirada sanalardi,
                // "bajarilgan shartnoma" esa har shartnomaga alohida so'rov edi.
                // Bekor qilingan invoyslar hisobga olinmaydi, davr yuqori chegarasi yarim ochiq (CRM-B07).
                var toExclusive = to.AddDays(1);
                var activeInvoices = db.SALES_INVOICES.AsNoTracking().Active();

                // ——— Shartnomalar ———
                var contractsQ = db.S_CONTRACTS.AsNoTracking().Where(c => c.IsDeleted != true);
                model.TotalContracts = contractsQ.Count();
                model.ContractsOverdue = contractsQ.Count(c => c.DueDate < today);
                model.ContractsCompleted = contractsQ.Count(c => activeInvoices.Any(inv => inv.ContractID == c.ID));
                model.ContractsTotalAmount = contractsQ
                    .GroupBy(c => new { c.CurrencyID, Day = DbFunctions.TruncateTime(c.IssuedDate) })
                    .Select(g => new { g.Key.CurrencyID, g.Key.Day, Amount = g.Sum(c => c.Amount) ?? 0 })
                    .ToList()
                    .Sum(g => uzs.ToUzsOrZero((decimal)g.Amount, g.CurrencyID, g.Day ?? today));

                // ——— Buyurtmalar ———
                var ordersQ = db.S_ORDERS.AsNoTracking().Where(o => o.IsDeleted != true);
                model.TotalOrders = ordersQ.Count();
                model.OrdersWithInvoice = ordersQ.Count(o => activeInvoices.Any(inv => inv.OrderID == o.ID));
                model.OrdersWithoutInvoice = model.TotalOrders - model.OrdersWithInvoice;
                model.OrdersTotalAmount = ordersQ
                    .GroupBy(o => new { o.CurrencyID, Day = DbFunctions.TruncateTime(o.IssuedDate) })
                    .Select(g => new { g.Key.CurrencyID, g.Key.Day, Amount = g.Sum(o => o.Amount) ?? 0 })
                    .ToList()
                    .Sum(g => uzs.ToUzsOrZero((decimal)g.Amount, g.CurrencyID, g.Day ?? today));

                // Yetkazib berish rejasi bo'yicha kechikkan (DeliveryDate o'tgan, QtyShipped < QtyPlanned)
                var deliveryOverdueQ = db.SORDER_DELIVERY_SCHEDULE.AsNoTracking()
                    .Where(s => s.S_ORDERS != null && (s.S_ORDERS.IsDeleted == false || s.S_ORDERS.IsDeleted == null)
                        && s.DeliveryDate < today && s.QtyShipped < s.QtyPlanned);
                model.OrdersDeliveryOverdue = deliveryOverdueQ.Count();
                model.DeliveryOverdueList = deliveryOverdueQ
                    .OrderBy(s => s.DeliveryDate)
                    .Take(50)
                    .Select(s => new
                    {
                        s.ID,
                        s.SOrderID,
                        OrderNo = s.S_ORDERS.OrderNo,
                        CustomerName = s.S_ORDERS.CUSTOMER.Name,
                        PNo = s.PRODUCT.PNo,
                        ProductName = s.PRODUCT.Name,
                        s.DeliveryDate,
                        s.QtyPlanned,
                        s.QtyShipped
                    })
                    .ToList()
                    .Select(s => new CRMDeliveryOverdueItem
                    {
                        ScheduleId = s.ID,
                        OrderId = s.SOrderID ?? 0,
                        OrderNo = s.OrderNo ?? "",
                        CustomerName = s.CustomerName ?? "",
                        ProductName = s.PNo ?? s.ProductName ?? "",
                        DeliveryDate = s.DeliveryDate,
                        QtyPlanned = s.QtyPlanned,
                        QtyShipped = s.QtyShipped,
                        DaysOverdue = (today - s.DeliveryDate.Date).Days
                    })
                    .ToList();

                // ——— Invoyslar (faqat faol) ———
                // Muddati o'tgan: PAID chiqariladi, qolganlarida invoysga bog'langan to'lovlar
                // (SalesInvoicePayment) ayiriladi — to'liq yopilgani ro'yxatga tushmaydi (CRM-B18)
                var overdueInvoices = OverdueInvoicesWithBalance(db, activeInvoices, today, uzs);
                model.TotalInvoices = activeInvoices.Count();
                model.InvoicesOverdue = overdueInvoices.Count;
                model.InvoicesTotalAmount = activeInvoices
                    .GroupBy(i => new { i.CurrencyCode, Day = DbFunctions.TruncateTime(i.InvoiceDate) })
                    .Select(g => new { g.Key.CurrencyCode, g.Key.Day, Amount = g.Sum(i => i.TotalAmount) })
                    .ToList()
                    .Sum(g => uzs.ToUzsOrZero(g.Amount, g.CurrencyCode, g.Day ?? today));

                // ——— Mijozlar ———
                model.TotalCustomers = db.CUSTOMERS.Count(c => c.IsDeleted != true);
                var yearStart = new DateTime(today.Year, 1, 1);
                model.CustomersWithOrdersThisYear = ordersQ.Where(o => o.IssuedDate >= yearStart).Select(o => o.CustomerID).Distinct().Count();

                // ——— Grafik: shartnomalar / buyurtmalar oy bo'yicha ———
                model.ContractsByMonth = contractsQ
                    .Where(c => c.IssuedDate >= from && c.IssuedDate < toExclusive)
                    .GroupBy(c => new { c.IssuedDate.Year, c.IssuedDate.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                    .ToList()
                    .OrderBy(g => g.Year).ThenBy(g => g.Month)
                    .Select(g => new CRMChartPoint { Label = $"{g.Year}-{g.Month:D2}", Value = g.Count })
                    .ToList();

                model.OrdersByMonth = ordersQ
                    .Where(o => o.IssuedDate >= from && o.IssuedDate < toExclusive)
                    .GroupBy(o => new { o.IssuedDate.Year, o.IssuedDate.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                    .ToList()
                    .OrderBy(g => g.Year).ThenBy(g => g.Month)
                    .Select(g => new CRMChartPoint { Label = $"{g.Year}-{g.Month:D2}", Value = g.Count })
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
                model.OrdersWithoutInvoiceList = ordersQ
                    .Where(o => !activeInvoices.Any(inv => inv.OrderID == o.ID))
                    .OrderByDescending(o => o.IssuedDate)
                    .Take(30)
                    .Select(o => new { o.ID, o.OrderNo, CustomerName = o.CUSTOMER.Name, o.IssuedDate, o.Amount, Cur = o.CURRENCy1.Shortname, o.Currency })
                    .ToList()
                    .Select(o => new CRMOrderWithoutInvoiceItem
                    {
                        OrderId = o.ID,
                        OrderNo = o.OrderNo ?? "",
                        CustomerName = o.CustomerName ?? "",
                        IssuedDate = o.IssuedDate,
                        Amount = (decimal?)o.Amount,
                        CurrencyCode = o.Cur ?? o.Currency ?? ""
                    })
                    .ToList();

                // ——— Muddati o'tgan shartnomalar ———
                model.ContractsOverdueList = contractsQ
                    .Where(c => c.DueDate < today)
                    .OrderBy(c => c.DueDate)
                    .Take(30)
                    .Select(c => new { c.ID, c.ContractNo, CustomerName = c.CUSTOMER.Name, c.DueDate, c.Amount })
                    .ToList()
                    .Select(c => new CRMContractOverdueItem
                    {
                        ContractId = c.ID,
                        ContractNo = c.ContractNo ?? "",
                        CustomerName = c.CustomerName ?? "",
                        DueDate = c.DueDate,
                        DaysOverdue = (today - c.DueDate.Date).Days,
                        Amount = (decimal?)c.Amount
                    })
                    .ToList();

                // ——— Muddati o'tgan invoyslar ———
                model.InvoicesOverdueList = overdueInvoices.Take(30).ToList();

                // ——— Mijoz bo'yicha buyurtma summasi (top 10) ———
                // Mijoz × valyuta × kun bo'yicha SQL'da yig'ilib, so'mga o'giriladi (CRM-B18)
                model.TopCustomersByOrderAmount = ordersQ
                    .GroupBy(o => new { o.CustomerID, o.CurrencyID, Day = DbFunctions.TruncateTime(o.IssuedDate) })
                    .Select(g => new { g.Key.CustomerID, g.Key.CurrencyID, g.Key.Day, OrderCount = g.Count(), Total = g.Sum(o => o.Amount) ?? 0 })
                    .ToList()
                    .GroupBy(x => x.CustomerID)
                    .Select(g => new CRMCustomerOrderSum
                    {
                        CustomerId = g.Key,
                        CustomerName = ReferenceDataCache.NameOf(ReferenceDataCache.Customers, g.Key) ?? "",
                        OrderCount = g.Sum(x => x.OrderCount),
                        TotalAmount = g.Sum(x => uzs.ToUzsOrZero((decimal)x.Total, x.CurrencyID, x.Day ?? today))
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .Take(10)
                    .ToList();

                // ——— Mijozlar bo'yicha haqqimiz (shartnoma kesimida, expand/collapse uchun) ———
                Func<string, string> normalize = s => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToUpper();
                var asOfDate = to;
                var asOfExclusive = to.AddDays(1);
                DateTime? latestOpeningDate = null;
                try
                {
                    latestOpeningDate = db.Database.SqlQuery<DateTime?>(
                        "SELECT MAX(SnapshotDate) FROM FIN_OPENING_CONTRACT_BALANCES WHERE SnapshotDate <= @p0 AND BalanceType = 1",
                        new System.Data.SqlClient.SqlParameter("@p0", asOfDate.Date)).FirstOrDefault();
                }
                catch { }
                if (!latestOpeningDate.HasValue)
                {
                    latestOpeningDate = db.FIN_BALANCE_SNAPSHOTS
                        .Where(s => s.SourceType == "OpeningBalance" && s.SnapshotDate <= asOfDate)
                        .OrderByDescending(s => s.SnapshotDate)
                        .Select(s => (DateTime?)s.SnapshotDate)
                        .FirstOrDefault();
                }

                var receivableInvoices = db.SALES_INVOICES
                    .Where(i => i.InvoiceDate < asOfExclusive
                        && (!latestOpeningDate.HasValue || i.InvoiceDate > latestOpeningDate.Value)
                        && (i.StatusCode == null || (i.StatusCode != "CANCELED" && i.StatusCode != "CANCELLED")))
                    .Select(i => new
                    {
                        ID = (int)i.ID,
                        i.CustomerID,
                        i.ContractID,
                        i.InvoiceDate,
                        i.CurrencyCode,
                        Amount = (i.TotalWithVAT ?? i.TotalAmount)
                    })
                    .ToList()
                    .Select(i => new
                    {
                        i.ID,
                        i.CustomerID,
                        i.ContractID,
                        i.InvoiceDate,
                        Amount = uzs.ToUzsOrZero(i.Amount, i.CurrencyCode, i.InvoiceDate)
                    })
                    .ToList();
                var openingContractByInvoiceId = new Dictionary<int, string>();
                if (latestOpeningDate.HasValue)
                {
                    try
                    {
                        var openingRows = db.Database.SqlQuery<OpeningContractReceivableRow>(
                            "SELECT CounterpartyID, ContractNo, Amount FROM FIN_OPENING_CONTRACT_BALANCES WHERE SnapshotDate = @p0 AND BalanceType = 1",
                            new System.Data.SqlClient.SqlParameter("@p0", latestOpeningDate.Value.Date))
                            .ToList();
                        var nextOpeningId = -1;
                        // Ochilish qoldiqlari so'mda kiritiladi (FIN_OPENING_CONTRACT_BALANCES'da valyuta ustuni yo'q)
                        foreach (var row in openingRows.Where(x => x.CounterpartyID > 0 && x.Amount > 0))
                        {
                            receivableInvoices.Add(new
                            {
                                ID = nextOpeningId,
                                CustomerID = row.CounterpartyID,
                                ContractID = (int?)null,
                                InvoiceDate = latestOpeningDate.Value,
                                Amount = row.Amount
                            });
                            openingContractByInvoiceId[nextOpeningId] = normalize(row.ContractNo);
                            nextOpeningId--;
                        }
                    }
                    catch
                    {
                    }
                }

                var contractIds = receivableInvoices
                    .Where(i => i.ContractID.HasValue)
                    .Select(i => i.ContractID.Value)
                    .Distinct()
                    .ToList();

                var contractMetaById = contractIds.Count == 0
                    ? new Dictionary<int, Tuple<string, int?>>()
                    : db.S_CONTRACTS
                        .Where(c => contractIds.Contains(c.ID))
                        .Select(c => new { c.ID, c.ContractNo })
                        .ToList()
                        .ToDictionary(x => x.ID, x => Tuple.Create(normalize(x.ContractNo), (int?)x.ID));

                var receiptCandidates = db.FIN_TRANSACTIONS
                    .Where(t => t.TxnDate < asOfExclusive
                        && (!latestOpeningDate.HasValue || t.TxnDate > latestOpeningDate.Value)
                        && (t.IsDeleted == null || t.IsDeleted == false)
                        && t.SourceDocType != "SalesInvoice"
                        && (
                            // Standard customer receipts
                            (t.Direction == 1 && t.CounterpartyType == 1 && t.CounterpartyID.HasValue)
                            // Linked payment documents should also be considered even if counterparty fields are partially empty
                            || (t.SourceDocType == "SalesInvoicePayment" && t.SourceDocID.HasValue)
                        ))
                    .Select(t => new { t.CounterpartyID, t.TxnDate, t.Amount, t.CurrencyID, t.Direction, t.SourceDocType, t.SourceDocID, t.ContractNo })
                    .ToList();

                var linkedInvoiceIds = receiptCandidates
                    .Where(r => r.SourceDocType == "SalesInvoicePayment" && r.SourceDocID.HasValue)
                    .Select(r => (long)r.SourceDocID.Value)
                    .Distinct()
                    .ToList();

                var invoiceMetaById = linkedInvoiceIds.Count == 0
                    ? new Dictionary<long, Tuple<int, string, int?>>()
                    : db.SALES_INVOICES
                        .Where(i => linkedInvoiceIds.Contains(i.ID))
                        .Select(i => new { i.ID, i.CustomerID, i.ContractID })
                        .ToList()
                        .ToDictionary(
                            x => x.ID,
                            x =>
                            {
                                var contractNo = x.ContractID.HasValue && contractMetaById.ContainsKey(x.ContractID.Value)
                                    ? contractMetaById[x.ContractID.Value].Item1
                                    : "";
                                return Tuple.Create(x.CustomerID, contractNo, x.ContractID);
                            });

                var receipts = receiptCandidates
                    .Select(r =>
                    {
                        var customerId = r.CounterpartyID ?? 0;
                        var contractNo = normalize(r.ContractNo);
                        int? contractId = null;
                        if (r.SourceDocType == "SalesInvoicePayment" && r.SourceDocID.HasValue && invoiceMetaById.ContainsKey((long)r.SourceDocID.Value))
                        {
                            var meta = invoiceMetaById[(long)r.SourceDocID.Value];
                            customerId = meta.Item1;
                            if (string.IsNullOrEmpty(contractNo))
                            {
                                contractNo = meta.Item2;
                                contractId = meta.Item3;
                            }
                        }
                        return new
                        {
                            CustomerID = customerId,
                            ContractNo = contractNo,
                            ContractID = contractId,
                            r.TxnDate,
                            Amount = uzs.ToUzsOrZero(Math.Abs(r.Amount), r.CurrencyID, r.TxnDate),
                            InvoiceID = (r.SourceDocType == "SalesInvoicePayment") ? r.SourceDocID : null
                        };
                    })
                    .Where(r => r.CustomerID > 0)
                    .ToList();

                var invoiceRemaining = receivableInvoices.ToDictionary(i => i.ID, i => i.Amount);
                var invoiceByKey = receivableInvoices
                    .GroupBy(i =>
                    {
                        var contractNo = "";
                        if (openingContractByInvoiceId.ContainsKey(i.ID))
                        {
                            contractNo = openingContractByInvoiceId[i.ID];
                        }
                        else if (i.ContractID.HasValue && contractMetaById.ContainsKey(i.ContractID.Value))
                        {
                            contractNo = contractMetaById[i.ContractID.Value].Item1;
                        }
                        return Tuple.Create(i.CustomerID, contractNo);
                    })
                    .ToDictionary(g => g.Key, g => g.OrderBy(i => i.InvoiceDate).Select(i => i.ID).ToList());

                var receiptRemainders = new List<(int CustomerID, string ContractNo, DateTime TxnDate, decimal Amount)>();
                var unappliedByKey = new Dictionary<Tuple<int, string>, decimal>();
                foreach (var receipt in receipts)
                {
                    var remaining = receipt.Amount;
                    if (receipt.InvoiceID.HasValue && invoiceRemaining.ContainsKey(receipt.InvoiceID.Value))
                    {
                        var invoiceBalance = invoiceRemaining[receipt.InvoiceID.Value];
                        var applied = Math.Min(invoiceBalance, remaining);
                        invoiceRemaining[receipt.InvoiceID.Value] = invoiceBalance - applied;
                        remaining -= applied;
                    }
                    if (remaining > 0)
                    {
                        receiptRemainders.Add((receipt.CustomerID, receipt.ContractNo, receipt.TxnDate, remaining));
                    }
                }

                foreach (var group in receiptRemainders.GroupBy(p => Tuple.Create(p.CustomerID, p.ContractNo)))
                {
                    if (!invoiceByKey.ContainsKey(group.Key))
                    {
                        unappliedByKey[group.Key] = (unappliedByKey.ContainsKey(group.Key) ? unappliedByKey[group.Key] : 0m) + group.Sum(x => x.Amount);
                        continue;
                    }

                    var queue = new Queue<int>(invoiceByKey[group.Key]);
                    foreach (var r in group.OrderBy(x => x.TxnDate))
                    {
                        var remaining = r.Amount;
                        while (remaining > 0 && queue.Count > 0)
                        {
                            var invId = queue.Peek();
                            var invBal = invoiceRemaining[invId];
                            if (invBal <= 0) { queue.Dequeue(); continue; }
                            var applied = Math.Min(invBal, remaining);
                            invoiceRemaining[invId] = invBal - applied;
                            remaining -= applied;
                            if (invoiceRemaining[invId] <= 0) queue.Dequeue();
                        }
                        if (remaining > 0)
                        {
                            unappliedByKey[group.Key] = (unappliedByKey.ContainsKey(group.Key) ? unappliedByKey[group.Key] : 0m) + remaining;
                        }
                    }
                }

                var customerNames = ReferenceDataCache.Get(ReferenceDataCache.Customers)
                    .ToDictionary(c => c.ID, c => c.Name ?? "Noma'lum");
                var contractNosById = contractMetaById.ToDictionary(x => x.Key, x => x.Value.Item1);

                var invoiceAggByCustomerContract = receivableInvoices
                    .GroupBy(i =>
                    {
                        var contractNo = "";
                        if (openingContractByInvoiceId.ContainsKey(i.ID))
                        {
                            contractNo = openingContractByInvoiceId[i.ID];
                        }
                        else if (i.ContractID.HasValue && contractNosById.ContainsKey(i.ContractID.Value))
                        {
                            contractNo = contractNosById[i.ContractID.Value];
                        }
                        return Tuple.Create(i.CustomerID, contractNo);
                    })
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

                var receiptAggByCustomerContract = receipts
                    .GroupBy(r => Tuple.Create(r.CustomerID, r.ContractNo))
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

                var contractIdByKey = receivableInvoices
                    .Where(i => i.ContractID.HasValue && i.ContractID.Value > 0)
                    .GroupBy(i =>
                    {
                        var contractNo = contractNosById.ContainsKey(i.ContractID.Value) ? contractNosById[i.ContractID.Value] : "";
                        return Tuple.Create(i.CustomerID, contractNo);
                    })
                    .ToDictionary(g => g.Key, g => (int?)g.Select(x => x.ContractID).FirstOrDefault());

                var customerIdsForTable = invoiceAggByCustomerContract.Keys.Select(k => k.Item1)
                    .Union(receiptAggByCustomerContract.Keys.Select(k => k.Item1))
                    .Distinct()
                    .ToList();

                model.CustomerReceivables = customerIdsForTable
                    .Select(customerId =>
                    {
                        var contractKeys = invoiceAggByCustomerContract.Keys.Where(k => k.Item1 == customerId)
                            .Union(receiptAggByCustomerContract.Keys.Where(k => k.Item1 == customerId))
                            .Distinct()
                            .ToList();

                        var contractsForCustomer = contractKeys
                            .Select(key =>
                            {
                                var invoiced = invoiceAggByCustomerContract.ContainsKey(key) ? invoiceAggByCustomerContract[key] : 0m;
                                var paid = receiptAggByCustomerContract.ContainsKey(key) ? receiptAggByCustomerContract[key] : 0m;
                                var unapplied = unappliedByKey.ContainsKey(key) ? unappliedByKey[key] : 0m;
                                var receivable = invoiced - (paid - unapplied);
                                return new CRMCustomerReceivableContractItem
                                {
                                    ContractId = contractIdByKey.ContainsKey(key) ? contractIdByKey[key] : null,
                                    ContractNo = string.IsNullOrWhiteSpace(key.Item2) ? "(Shartnoma ko'rsatilmagan)" : key.Item2,
                                    InvoicedAmount = invoiced,
                                    PaidAmount = paid - unapplied,
                                    ReceivableAmount = receivable
                                };
                            })
                            .Where(c => c.ReceivableAmount > 0)
                            .OrderByDescending(c => c.ReceivableAmount)
                            .ToList();

                        return new CRMCustomerReceivableItem
                        {
                            CustomerId = customerId,
                            CustomerName = customerNames.ContainsKey(customerId) ? customerNames[customerId] : $"Customer #{customerId}",
                            InvoicedAmount = contractsForCustomer.Sum(c => c.InvoicedAmount),
                            PaidAmount = contractsForCustomer.Sum(c => c.PaidAmount),
                            ReceivableAmount = contractsForCustomer.Sum(c => c.ReceivableAmount),
                            Contracts = contractsForCustomer
                        };
                    })
                    .Where(x => x.ReceivableAmount > 0 && x.Contracts.Any())
                    .OrderByDescending(x => x.ReceivableAmount)
                    .Take(20)
                    .ToList();

                ViewBag.CurrencyWarning = uzs.Warning;
                ViewBag.FromDate = from;
                ViewBag.ToDate = to;
                return View(model);
            }
        }
    }
}
