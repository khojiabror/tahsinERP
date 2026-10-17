using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services.Finance
{
    /// <summary>
    /// Kontragent qarzlari (debitorlik/kreditorlik) — dashboard, balans, ReportsHub va Liabilities uchun yagona hisob.
    /// Avval FinDashboardController'dagi CalculateSupplierOutstanding/CalculateCustomerOutstanding har chaqiriqda
    /// barcha invoys/to'lovlarni qayta yuklardi: LiabilitiesDashboard 12 oy uchun ~28 marta (FIN-P01).
    /// Endi bir so'rov (request) doirasida:
    ///  - ma'lumot eng katta sana uchun BIR MARTA yuklanadi, har sana bo'yicha xotirada kesiladi (<see cref="SupplierSeries"/>);
    ///  - (sana, opening sanasi) bo'yicha natija keshlanadi — balans, details va Liabilities bir xil hisobni qayta qilmaydi.
    /// Formula o'zgarmagan (FIN-B08 bo'yicha VAT/to'lov qoidasini birlashtirish — alohida biznes qarori).
    /// </summary>
    public sealed class CounterpartyBalanceService
    {
        public const byte BalanceTypeReceivable = 1;
        public const byte BalanceTypePayable = 2;
        public const byte BalanceTypePayroll = 3;
        public const byte BalanceTypeLogistics = 4;

        private readonly DBTHSNEntities _db;
        private readonly Dictionary<int, List<DateTime>> _openingDatesByType = new Dictionary<int, List<DateTime>>();
        private List<DateTime> _snapshotOpeningDates;
        private readonly Dictionary<(DateTime, int), List<OpeningContractBalance>> _openingRows = new Dictionary<(DateTime, int), List<OpeningContractBalance>>();
        private readonly Dictionary<(DateTime, DateTime?), List<SupplierOutstanding>> _supplierCache = new Dictionary<(DateTime, DateTime?), List<SupplierOutstanding>>();
        private readonly Dictionary<(DateTime, DateTime?), List<CustomerOutstanding>> _customerCache = new Dictionary<(DateTime, DateTime?), List<CustomerOutstanding>>();

        public CounterpartyBalanceService(DBTHSNEntities db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public static string NormalizeContractNo(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToUpper();
        }

        #region Yagona summa qoidasi (FIN-B08)

        /// <summary>Sotuv invoysi summasi — QQS bilan (TotalWithVAT), bo'lmasa TotalAmount.</summary>
        public static decimal SalesInvoiceAmount(SALES_INVOICES invoice)
        {
            return invoice.TotalWithVAT ?? invoice.TotalAmount;
        }

        /// <summary>
        /// Xarid invoyslari summasi: invoysga bog'langan kirim hujjatlari (INCOMES.TotalPrice, odatda QQS bilan)
        /// bo'lsa — ularning yig'indisi (asOf gacha), aks holda P_INVOICES.Amount.
        /// Dashboard, balans, Akt-sverki, opening AutoFill va FIN "mirror" yozuvi shu qoidadan foydalanadi.
        /// </summary>
        public static Dictionary<int, decimal> PurchaseInvoiceAmounts(DBTHSNEntities db, IQueryable<P_INVOICES> invoices, DateTime? asOf = null)
        {
            var baseAmounts = invoices
                .Select(i => new { i.ID, i.Amount })
                .ToList();
            if (baseAmounts.Count == 0)
                return new Dictionary<int, decimal>();

            var incomeQuery = db.INCOMES.AsNoTracking()
                .Where(inc => inc.InvoiceID.HasValue &&
                              inc.IsDeleted != true &&
                              inc.TotalPrice.HasValue &&
                              invoices.Any(i => i.ID == inc.InvoiceID.Value));
            if (asOf.HasValue)
            {
                var to = asOf.Value;
                incomeQuery = incomeQuery.Where(inc => inc.IssueDateTime <= to);
            }
            var incomeTotals = incomeQuery
                .GroupBy(inc => inc.InvoiceID.Value)
                .Select(g => new { InvoiceID = g.Key, Total = g.Sum(x => x.TotalPrice.Value) })
                .ToList()
                .ToDictionary(x => x.InvoiceID, x => Convert.ToDecimal(x.Total));

            return baseAmounts.ToDictionary(
                i => i.ID,
                i => incomeTotals.TryGetValue(i.ID, out var total) ? total : (i.Amount.HasValue ? Convert.ToDecimal(i.Amount.Value) : 0m));
        }

        #endregion

        #region Opening

        /// <summary>
        /// Berilgan tur bo'yicha eng so'nggi opening sanasi (FIN_OPENING_CONTRACT_BALANCES, SnapshotDate ≤ asOf).
        /// Bo'lmasa — FIN_BALANCE_SNAPSHOTS dagi oxirgi "OpeningBalance" sanasi (eski xatti-harakat saqlangan).
        /// </summary>
        public DateTime? LatestOpeningDate(DateTime asOf, int balanceType)
        {
            var day = asOf.Date;
            var byContract = OpeningDates(balanceType).Where(d => d <= day).Select(d => (DateTime?)d).Max();
            if (byContract.HasValue)
                return byContract.Value.Date;

            if (_snapshotOpeningDates == null)
            {
                _snapshotOpeningDates = _db.FIN_BALANCE_SNAPSHOTS.AsNoTracking()
                    .Where(s => s.SourceType == "OpeningBalance")
                    .Select(s => s.SnapshotDate)
                    .Distinct()
                    .ToList();
            }
            return _snapshotOpeningDates.Where(d => d <= asOf).Select(d => (DateTime?)d).Max();
        }

        private List<DateTime> OpeningDates(int balanceType)
        {
            if (!_openingDatesByType.TryGetValue(balanceType, out var dates))
            {
                var bt = (byte)balanceType;
                dates = _db.FIN_OPENING_CONTRACT_BALANCES.AsNoTracking()
                    .Where(x => x.BalanceType == bt)
                    .Select(x => x.SnapshotDate)
                    .Distinct()
                    .ToList();
                _openingDatesByType[balanceType] = dates;
            }
            return dates;
        }

        /// <summary>Opening qatorlari (kontragent/shartnoma kesimida) berilgan sana va tur uchun.</summary>
        public List<OpeningContractBalance> OpeningContractBalances(DateTime snapshotDate, int balanceType)
        {
            var key = (snapshotDate.Date, balanceType);
            if (!_openingRows.TryGetValue(key, out var rows))
            {
                var d = snapshotDate.Date;
                var bt = (byte)balanceType;
                rows = _db.FIN_OPENING_CONTRACT_BALANCES.AsNoTracking()
                    .Where(x => x.SnapshotDate == d && x.BalanceType == bt)
                    .OrderBy(x => x.ID)
                    .Select(x => new OpeningContractBalance
                    {
                        CounterpartyID = x.CounterpartyID,
                        ContractNo = x.ContractNo,
                        TotalAmount = x.TotalAmount,
                        PaidAmount = x.PaidAmount,
                        Amount = x.Amount
                    })
                    .ToList();
                _openingRows[key] = rows;
            }
            return rows;
        }

        #endregion

        #region Suppliers (kreditorlik)

        /// <summary>
        /// Ta'minotchilar qarzi: opening + SUM(invoys; INCOMES.TotalPrice bo'lsa shu, aks holda P_INVOICES.Amount)
        /// − SUM(ta'minotchiga chiqim: PurchaseInvoicePayment yoki CP=2 qo'lda to'lov).
        /// <paramref name="fromExclusive"/> berilmasa — asOf uchun oxirgi AP opening sanasi.
        /// </summary>
        public List<SupplierOutstanding> Suppliers(DateTime asOf, DateTime? fromExclusive = null)
        {
            return SupplierSeries(new[] { asOf }, fromExclusive)[0];
        }

        /// <summary>Bir nechta sana uchun (masalan oy oxirlari) — ma'lumot bir marta yuklanadi.</summary>
        public List<List<SupplierOutstanding>> SupplierSeries(IList<DateTime> asOfDates, DateTime? fromExclusive = null)
        {
            var keys = asOfDates
                .Select(a => (a, fromExclusive ?? LatestOpeningDate(a, BalanceTypePayable)))
                .ToList();
            var missing = keys.Where(k => !_supplierCache.ContainsKey(k)).Distinct().ToList();
            if (missing.Count > 0)
            {
                var maxAsOf = missing.Max(k => k.Item1);
                var minFrom = missing.Any(k => !k.Item2.HasValue) ? (DateTime?)null : missing.Min(k => k.Item2.Value);
                var data = LoadSupplierData(maxAsOf, minFrom);
                foreach (var k in missing)
                {
                    _supplierCache[k] = ComputeSuppliers(data, k.Item1, k.Item2);
                }
            }
            return keys.Select(k => _supplierCache[k]).ToList();
        }

        private sealed class SupplierData
        {
            public List<SupplierInvoiceRow> Invoices;
            public Dictionary<int, List<IncomeRow>> IncomesByInvoice;
            public Dictionary<int, string> ContractNoById;
            public List<PaymentRow> Payments;
            public Dictionary<int, SupplierInvoiceRow> LinkedInvoices;
        }

        private sealed class SupplierInvoiceRow
        {
            public int ID { get; set; }
            public int SupplierID { get; set; }
            public int ContractID { get; set; }
            public DateTime InvoiceDate { get; set; }
            public double? Amount { get; set; }
        }

        private sealed class IncomeRow
        {
            public int InvoiceID { get; set; }
            public DateTime IssueDateTime { get; set; }
            public double? TotalPrice { get; set; }
            public bool? IsDeleted { get; set; }
        }

        private sealed class PaymentRow
        {
            public DateTime TxnDate { get; set; }
            public byte? CounterpartyType { get; set; }
            public int? CounterpartyID { get; set; }
            public decimal Amount { get; set; }
            public string SourceDocType { get; set; }
            public int? SourceDocID { get; set; }
            public string ContractNo { get; set; }
        }

        private sealed class SupplierInvoiceWork
        {
            public int SupplierID;
            public decimal Amount;
            public string ContractNo;
        }

        private SupplierData LoadSupplierData(DateTime maxAsOf, DateTime? minFromExclusive)
        {
            var db = _db;
            var invoiceQuery = db.P_INVOICES.AsNoTracking()
                .Where(i => i.InvoiceDate <= maxAsOf && (i.IsDeleted == null || i.IsDeleted == false));
            if (minFromExclusive.HasValue)
            {
                var f = minFromExclusive.Value;
                invoiceQuery = invoiceQuery.Where(i =>
                    i.InvoiceDate > f ||
                    db.INCOMES.Any(inc => inc.InvoiceID == i.ID && inc.IssueDateTime > f && inc.IssueDateTime <= maxAsOf && (inc.IsDeleted == null || inc.IsDeleted == false)));
            }

            var data = new SupplierData();
            data.Invoices = invoiceQuery
                .OrderBy(i => i.ID)
                .Select(i => new SupplierInvoiceRow { ID = i.ID, SupplierID = i.SupplierID, ContractID = i.ContractID, InvoiceDate = i.InvoiceDate, Amount = i.Amount })
                .ToList();

            data.IncomesByInvoice = db.INCOMES.AsNoTracking()
                .Where(inc => inc.InvoiceID.HasValue && inc.IssueDateTime <= maxAsOf && invoiceQuery.Any(i => i.ID == inc.InvoiceID.Value))
                .Select(inc => new IncomeRow { InvoiceID = inc.InvoiceID.Value, IssueDateTime = inc.IssueDateTime, TotalPrice = inc.TotalPrice, IsDeleted = inc.IsDeleted })
                .ToList()
                .GroupBy(x => x.InvoiceID)
                .ToDictionary(g => g.Key, g => g.ToList());

            var contractIds = data.Invoices.Select(i => i.ContractID).Distinct().ToList();
            data.ContractNoById = contractIds.Count == 0
                ? new Dictionary<int, string>()
                : db.P_CONTRACTS.AsNoTracking().Where(c => contractIds.Contains(c.ID))
                    .Select(c => new { c.ID, c.ContractNo })
                    .ToList()
                    .ToDictionary(x => x.ID, x => NormalizeContractNo(x.ContractNo));

            var paymentQuery = FinQueries.Active(db)
                .Where(t => t.TxnDate <= maxAsOf &&
                            t.Direction == 2 &&
                            t.CounterpartyType == 2 &&
                            t.CounterpartyID != null &&
                            (t.SourceDocType == null || (t.SourceDocType != FinQueries.PurchaseInvoiceMirror && t.SourceDocType != FinQueries.SalesInvoiceMirror)));
            if (minFromExclusive.HasValue)
            {
                var f = minFromExclusive.Value;
                paymentQuery = paymentQuery.Where(t => t.TxnDate > f);
            }
            data.Payments = paymentQuery
                .OrderBy(t => t.ID)
                .Select(t => new PaymentRow { TxnDate = t.TxnDate, CounterpartyType = t.CounterpartyType, CounterpartyID = t.CounterpartyID, Amount = t.Amount, SourceDocType = t.SourceDocType, SourceDocID = t.SourceDocID, ContractNo = t.ContractNo })
                .ToList();

            var linkedIds = data.Payments
                .Where(p => p.SourceDocType == "PurchaseInvoicePayment" && p.SourceDocID.HasValue)
                .Select(p => p.SourceDocID.Value)
                .Distinct()
                .ToList();
            data.LinkedInvoices = linkedIds.Count == 0
                ? new Dictionary<int, SupplierInvoiceRow>()
                : db.P_INVOICES.AsNoTracking().Where(i => linkedIds.Contains(i.ID))
                    .Select(i => new SupplierInvoiceRow { ID = i.ID, SupplierID = i.SupplierID, ContractID = i.ContractID })
                    .ToList()
                    .ToDictionary(x => x.ID);

            return data;
        }

        private List<SupplierOutstanding> ComputeSuppliers(SupplierData data, DateTime asOf, DateTime? effectiveFrom)
        {
            bool HasIncomeAfterOpening(int invoiceId)
            {
                return data.IncomesByInvoice.TryGetValue(invoiceId, out var incs) &&
                       incs.Any(inc => inc.IssueDateTime > effectiveFrom.Value && inc.IssueDateTime <= asOf && inc.IsDeleted != true);
            }

            var invoices = data.Invoices
                .Where(i => i.InvoiceDate <= asOf &&
                            (!effectiveFrom.HasValue || i.InvoiceDate > effectiveFrom.Value || HasIncomeAfterOpening(i.ID)))
                .ToList();

            // Kirim hujjati (INCOMES) summasi bo'lsa — invoys summasi o'rniga shu olinadi.
            decimal InvoiceAmount(SupplierInvoiceRow i)
            {
                if (data.IncomesByInvoice.TryGetValue(i.ID, out var incs))
                {
                    var valid = incs.Where(inc => inc.IsDeleted != true && inc.TotalPrice.HasValue && inc.IssueDateTime <= asOf).ToList();
                    if (valid.Count > 0)
                        return Convert.ToDecimal(valid.Sum(inc => inc.TotalPrice.Value));
                }
                return i.Amount.HasValue ? Convert.ToDecimal(i.Amount.Value) : 0m;
            }

            // Shartnoma raqami faqat joriy invoyslar shartnomalari bo'yicha olinadi (avvalgi xatti-harakat).
            var currentContractIds = new HashSet<int>(invoices.Select(i => i.ContractID));
            string ContractNoOf(int contractId)
            {
                return currentContractIds.Contains(contractId) && data.ContractNoById.TryGetValue(contractId, out var no) ? no : "";
            }

            var invoiceWork = invoices.Select(i => new SupplierInvoiceWork
            {
                SupplierID = i.SupplierID,
                Amount = InvoiceAmount(i),
                ContractNo = ContractNoOf(i.ContractID)
            }).ToList();

            if (effectiveFrom.HasValue)
            {
                foreach (var row in OpeningContractBalances(effectiveFrom.Value, BalanceTypePayable).Where(x => x.CounterpartyID > 0 && x.Amount > 0))
                {
                    invoiceWork.Add(new SupplierInvoiceWork
                    {
                        SupplierID = row.CounterpartyID,
                        Amount = row.Amount,
                        ContractNo = NormalizeContractNo(row.ContractNo)
                    });
                }
            }

            // Chiqim: PurchaseInvoicePayment orqali invoysga bog'langan to'lov yoki ta'minotchiga (CP=2) qo'lda chiqim.
            var payments = data.Payments
                .Where(p => p.TxnDate <= asOf && (!effectiveFrom.HasValue || p.TxnDate > effectiveFrom.Value))
                .ToList();

            var invoiceTotalsByKey = invoiceWork
                .GroupBy(i => Tuple.Create(i.SupplierID, i.ContractNo))
                .ToDictionary(g => g.Key, g => new { Total = g.Sum(x => x.Amount), Count = g.Count() });

            var paymentTotalsByKey = payments
                .Select(p =>
                {
                    var supplierId = p.CounterpartyID.Value;
                    var contractNo = NormalizeContractNo(p.ContractNo);
                    if (p.SourceDocType == "PurchaseInvoicePayment" && p.SourceDocID.HasValue &&
                        data.LinkedInvoices.TryGetValue(p.SourceDocID.Value, out var meta))
                    {
                        supplierId = meta.SupplierID;
                        contractNo = ContractNoOf(meta.ContractID);
                    }
                    return new { SupplierID = supplierId, ContractNo = contractNo, p.Amount };
                })
                .GroupBy(x => Tuple.Create(x.SupplierID, x.ContractNo))
                .ToDictionary(g => g.Key, g => new { Total = g.Sum(x => x.Amount), Count = g.Count() });

            var allKeys = invoiceTotalsByKey.Keys.Union(paymentTotalsByKey.Keys).ToList();
            var summary = new List<SupplierOutstanding>();
            foreach (var supplierId in allKeys.Select(k => k.Item1).Distinct())
            {
                var invoiceTotal = allKeys.Where(k => k.Item1 == supplierId && invoiceTotalsByKey.ContainsKey(k)).Sum(k => invoiceTotalsByKey[k].Total);
                var paymentTotal = allKeys.Where(k => k.Item1 == supplierId && paymentTotalsByKey.ContainsKey(k)).Sum(k => paymentTotalsByKey[k].Total);
                var invoiceCount = allKeys.Where(k => k.Item1 == supplierId && invoiceTotalsByKey.ContainsKey(k)).Sum(k => invoiceTotalsByKey[k].Count);
                var paymentCount = allKeys.Where(k => k.Item1 == supplierId && paymentTotalsByKey.ContainsKey(k)).Sum(k => paymentTotalsByKey[k].Count);

                summary.Add(new SupplierOutstanding
                {
                    SupplierID = supplierId,
                    Outstanding = invoiceTotal - paymentTotal,
                    InvoiceCount = invoiceCount,
                    PaymentCount = paymentCount,
                    InvoiceTotal = invoiceTotal,
                    PaymentTotal = paymentTotal
                });
            }

            return summary;
        }

        #endregion

        #region Customers (debitorlik)

        /// <summary>
        /// Mijozlar qarzi: opening + sotuv invoyslari (TotalWithVAT ?? TotalAmount) − mijozdan tushumlar (FIFO taqsimot).
        /// <paramref name="fromExclusive"/> berilmasa — asOf uchun oxirgi AR opening sanasi.
        /// </summary>
        public List<CustomerOutstanding> Customers(DateTime asOf, DateTime? fromExclusive = null)
        {
            return CustomerSeries(new[] { asOf }, fromExclusive)[0];
        }

        public List<List<CustomerOutstanding>> CustomerSeries(IList<DateTime> asOfDates, DateTime? fromExclusive = null)
        {
            var keys = asOfDates
                .Select(a => (a, fromExclusive ?? LatestOpeningDate(a, BalanceTypeReceivable)))
                .ToList();
            var missing = keys.Where(k => !_customerCache.ContainsKey(k)).Distinct().ToList();
            if (missing.Count > 0)
            {
                var maxAsOf = missing.Max(k => k.Item1);
                var minFrom = missing.Any(k => !k.Item2.HasValue) ? (DateTime?)null : missing.Min(k => k.Item2.Value);
                var data = LoadCustomerData(maxAsOf, minFrom);
                foreach (var k in missing)
                {
                    _customerCache[k] = ComputeCustomers(data, k.Item1, k.Item2);
                }
            }
            return keys.Select(k => _customerCache[k]).ToList();
        }

        private sealed class CustomerData
        {
            public List<SalesInvoiceRow> Invoices;
            public Dictionary<int, string> ContractNoById;
            public List<PaymentRow> Receipts;
            public Dictionary<long, SalesInvoiceRow> LinkedInvoices;
        }

        private sealed class SalesInvoiceRow
        {
            public long ID { get; set; }
            public int CustomerID { get; set; }
            public int? ContractID { get; set; }
            public DateTime InvoiceDate { get; set; }
            public decimal Amount { get; set; }
        }

        private sealed class CustomerInvoiceWork
        {
            public int ID;
            public int CustomerID;
            public int? ContractID;
            public DateTime InvoiceDate;
            public decimal Amount;
        }

        private CustomerData LoadCustomerData(DateTime maxAsOf, DateTime? minFromExclusive)
        {
            var db = _db;
            var invoiceQuery = db.SALES_INVOICES.AsNoTracking().Where(i => i.InvoiceDate <= maxAsOf);
            if (minFromExclusive.HasValue)
            {
                var f = minFromExclusive.Value;
                invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate > f);
            }

            var data = new CustomerData();
            data.Invoices = invoiceQuery
                .OrderBy(i => i.ID)
                .Select(i => new SalesInvoiceRow { ID = i.ID, CustomerID = i.CustomerID, ContractID = i.ContractID, InvoiceDate = i.InvoiceDate, Amount = i.TotalWithVAT ?? i.TotalAmount })
                .ToList();

            var contractIds = data.Invoices.Where(i => i.ContractID.HasValue).Select(i => i.ContractID.Value).Distinct().ToList();
            data.ContractNoById = contractIds.Count == 0
                ? new Dictionary<int, string>()
                : db.S_CONTRACTS.AsNoTracking().Where(c => contractIds.Contains(c.ID))
                    .Select(c => new { c.ID, c.ContractNo })
                    .ToList()
                    .ToDictionary(x => x.ID, x => NormalizeContractNo(x.ContractNo));

            // Faqat mijozga (CP=1) tegishli tushumlar SQL'da filtrlanadi (avval barcha kirimlar xotiraga olinardi — FIN-P13).
            var receiptQuery = FinQueries.Active(db)
                .Where(t => t.TxnDate <= maxAsOf &&
                            t.Direction == 1 &&
                            t.SourceDocType != FinQueries.SalesInvoiceMirror &&
                            t.CounterpartyType == 1 &&
                            t.CounterpartyID != null);
            if (minFromExclusive.HasValue)
            {
                var f = minFromExclusive.Value;
                receiptQuery = receiptQuery.Where(t => t.TxnDate > f);
            }
            data.Receipts = receiptQuery
                .OrderBy(t => t.ID)
                .Select(t => new PaymentRow { TxnDate = t.TxnDate, CounterpartyType = t.CounterpartyType, CounterpartyID = t.CounterpartyID, Amount = t.Amount, SourceDocType = t.SourceDocType, SourceDocID = t.SourceDocID, ContractNo = t.ContractNo })
                .ToList();

            var linkedIds = data.Receipts
                .Where(r => r.SourceDocType == "SalesInvoicePayment" && r.SourceDocID.HasValue)
                .Select(r => (long)r.SourceDocID.Value)
                .Distinct()
                .ToList();
            data.LinkedInvoices = linkedIds.Count == 0
                ? new Dictionary<long, SalesInvoiceRow>()
                : db.SALES_INVOICES.AsNoTracking().Where(i => linkedIds.Contains(i.ID))
                    .Select(i => new SalesInvoiceRow { ID = i.ID, CustomerID = i.CustomerID, ContractID = i.ContractID })
                    .ToList()
                    .ToDictionary(x => x.ID);

            return data;
        }

        private List<CustomerOutstanding> ComputeCustomers(CustomerData data, DateTime asOf, DateTime? effectiveFrom)
        {
            var invoices = data.Invoices
                .Where(i => i.InvoiceDate <= asOf && (!effectiveFrom.HasValue || i.InvoiceDate > effectiveFrom.Value))
                .Select(i => new CustomerInvoiceWork { ID = (int)i.ID, CustomerID = i.CustomerID, ContractID = i.ContractID, InvoiceDate = i.InvoiceDate, Amount = i.Amount })
                .ToList();

            var openingContractByInvoiceId = new Dictionary<int, string>();
            if (effectiveFrom.HasValue)
            {
                var nextOpeningId = -1;
                foreach (var row in OpeningContractBalances(effectiveFrom.Value, BalanceTypeReceivable).Where(x => x.CounterpartyID > 0 && x.Amount > 0))
                {
                    invoices.Add(new CustomerInvoiceWork
                    {
                        ID = nextOpeningId,
                        CustomerID = row.CounterpartyID,
                        ContractID = null,
                        InvoiceDate = effectiveFrom.Value,
                        Amount = row.Amount
                    });
                    openingContractByInvoiceId[nextOpeningId] = NormalizeContractNo(row.ContractNo);
                    nextOpeningId--;
                }
            }

            var currentContractIds = new HashSet<int>(invoices.Where(i => i.ContractID.HasValue).Select(i => i.ContractID.Value));
            string ContractNoOf(int? contractId)
            {
                return contractId.HasValue && currentContractIds.Contains(contractId.Value) &&
                       data.ContractNoById.TryGetValue(contractId.Value, out var no) ? no : "";
            }

            var receipts = data.Receipts
                .Where(r => r.TxnDate <= asOf && (!effectiveFrom.HasValue || r.TxnDate > effectiveFrom.Value))
                .Select(r =>
                {
                    var customerId = r.CounterpartyID.Value;
                    var contractNo = NormalizeContractNo(r.ContractNo);
                    if (r.SourceDocType == "SalesInvoicePayment" && r.SourceDocID.HasValue &&
                        data.LinkedInvoices.TryGetValue((long)r.SourceDocID.Value, out var meta))
                    {
                        if (string.IsNullOrEmpty(contractNo))
                        {
                            contractNo = ContractNoOf(meta.ContractID);
                        }
                    }
                    return new
                    {
                        CustomerID = customerId,
                        ContractNo = contractNo,
                        r.TxnDate,
                        r.Amount,
                        InvoiceID = (r.SourceDocType == "SalesInvoicePayment") ? r.SourceDocID : null
                    };
                }).ToList();

            var invoiceRemaining = invoices.ToDictionary(i => i.ID, i => i.Amount);
            var invoiceByKey = invoices
                .GroupBy(i =>
                {
                    var contractNo = openingContractByInvoiceId.TryGetValue(i.ID, out var openingNo) ? openingNo : ContractNoOf(i.ContractID);
                    return Tuple.Create(i.CustomerID, contractNo);
                })
                .ToDictionary(g => g.Key, g => g.OrderBy(i => i.InvoiceDate).Select(i => i.ID).ToList());

            // 1) Invoysga bog'langan tushum avval o'sha invoysni yopadi; 2) qolgani shartnoma kesimida FIFO.
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
                var invoiceQueue = new Queue<int>(invoiceByKey[group.Key]);
                foreach (var receipt in group.OrderBy(p => p.TxnDate))
                {
                    var remaining = receipt.Amount;
                    while (remaining > 0 && invoiceQueue.Count > 0)
                    {
                        var invoiceId = invoiceQueue.Peek();
                        var invoiceBalance = invoiceRemaining[invoiceId];
                        if (invoiceBalance <= 0) { invoiceQueue.Dequeue(); continue; }
                        var applied = Math.Min(invoiceBalance, remaining);
                        invoiceRemaining[invoiceId] = invoiceBalance - applied;
                        remaining -= applied;
                        if (invoiceRemaining[invoiceId] <= 0) invoiceQueue.Dequeue();
                    }
                    if (remaining > 0)
                    {
                        unappliedByKey[group.Key] = (unappliedByKey.ContainsKey(group.Key) ? unappliedByKey[group.Key] : 0m) + remaining;
                    }
                }
            }

            var invoiceTotalsByCustomer = invoices.GroupBy(i => i.CustomerID).ToDictionary(g => g.Key, g => new { Total = g.Sum(x => x.Amount), Count = g.Count() });
            var receiptTotalsByCustomer = receipts.GroupBy(p => p.CustomerID).ToDictionary(g => g.Key, g => new { Total = g.Sum(x => x.Amount), Count = g.Count() });

            var summary = new List<CustomerOutstanding>();
            foreach (var customerId in invoiceTotalsByCustomer.Keys.Union(receiptTotalsByCustomer.Keys).ToList())
            {
                var outstanding = invoices.Where(i => i.CustomerID == customerId).Sum(i => invoiceRemaining[i.ID]);
                outstanding -= unappliedByKey.Where(k => k.Key.Item1 == customerId).Sum(k => k.Value);

                var invoiceInfo = invoiceTotalsByCustomer.ContainsKey(customerId) ? invoiceTotalsByCustomer[customerId] : new { Total = 0m, Count = 0 };
                var receiptInfo = receiptTotalsByCustomer.ContainsKey(customerId) ? receiptTotalsByCustomer[customerId] : new { Total = 0m, Count = 0 };

                summary.Add(new CustomerOutstanding
                {
                    CustomerID = customerId,
                    Outstanding = outstanding,
                    InvoiceCount = invoiceInfo.Count,
                    ReceiptCount = receiptInfo.Count,
                    InvoiceTotal = invoiceInfo.Total,
                    ReceiptTotal = receiptInfo.Total
                });
            }

            return summary;
        }

        #endregion
    }

    public sealed class SupplierOutstanding
    {
        public int SupplierID { get; set; }
        public decimal Outstanding { get; set; }
        public int InvoiceCount { get; set; }
        public int PaymentCount { get; set; }
        public decimal InvoiceTotal { get; set; }
        public decimal PaymentTotal { get; set; }
    }

    public sealed class CustomerOutstanding
    {
        public int CustomerID { get; set; }
        public decimal Outstanding { get; set; }
        public int InvoiceCount { get; set; }
        public int ReceiptCount { get; set; }
        public decimal InvoiceTotal { get; set; }
        public decimal ReceiptTotal { get; set; }
    }

    public sealed class OpeningContractBalance
    {
        public int CounterpartyID { get; set; }
        public string ContractNo { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Amount { get; set; }
    }
}
