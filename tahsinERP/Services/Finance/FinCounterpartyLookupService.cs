using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services.Finance
{
    /// <summary>
    /// Kontragent va shartnoma ma'lumotnomalari (FIN-D17: FinTransactions va OpeningBalance'dagi takrorlangan
    /// JSON lookup'lar) hamda shartnoma bo'yicha "umumiy oldi-berdi / to'langan" hisobi (OpeningBalance
    /// tahriri va Excel importi uchun).
    /// Kontragent turi: 1 = mijoz, 2 = ta'minotchi, 3 = aksiyador, 4 = ekspeditor, 6 = xodim.
    /// </summary>
    public sealed class FinCounterpartyLookupService
    {
        public const byte Customer = 1;
        public const byte Supplier = 2;
        public const byte Shareholder = 3;
        public const byte Forwarder = 4;
        public const byte Employee = 6;

        private readonly DBTHSNEntities _db;

        public FinCounterpartyLookupService(DBTHSNEntities db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public sealed class Option
        {
            public int ID { get; set; }
            public string Name { get; set; }
        }

        public sealed class ContractOption
        {
            public int ID { get; set; }
            public string ContractNo { get; set; }
        }

        public sealed class ContractTotals
        {
            public decimal Total { get; set; }
            public decimal Paid { get; set; }
        }

        private static string Normalize(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToUpper();
        }

        /// <summary>Kontragentlar ro'yxati (o'chirilmaganlar, nom bo'yicha). Noma'lum tur — bo'sh.</summary>
        public List<Option> Counterparties(byte type)
        {
            switch (type)
            {
                case Customer:
                    return _db.CUSTOMERS.AsNoTracking().Where(c => c.IsDeleted != true).OrderBy(c => c.Name)
                        .Select(c => new Option { ID = c.ID, Name = c.Name }).ToList();
                case Supplier:
                    return _db.SUPPLIERS.AsNoTracking().Where(s => s.IsDeleted != true).OrderBy(s => s.Name)
                        .Select(s => new Option { ID = s.ID, Name = s.Name }).ToList();
                case Shareholder:
                    return _db.SHAREHOLDERS.AsNoTracking().Where(s => s.IsDeleted != true).OrderBy(s => s.FullName)
                        .Select(s => new Option { ID = s.ID, Name = s.FullName }).ToList();
                case Forwarder:
                    return _db.FORWARDERS.AsNoTracking().Where(f => f.IsDeleted != true).OrderBy(f => f.ForwarderName)
                        .Select(f => new Option { ID = f.ID, Name = f.ForwarderName }).ToList();
                case Employee:
                    return _db.HR_EMPLOYEES.AsNoTracking().Where(e => e.IsDeleted != true && e.IsActive).OrderBy(e => e.FullName)
                        .Select(e => new Option { ID = e.ID, Name = e.FullName }).ToList();
                default:
                    return new List<Option>();
            }
        }

        /// <summary>Kontragent shartnomalari (yangi sanadagisi birinchi). Faqat mijoz/ta'minotchi/ekspeditor.</summary>
        public List<ContractOption> Contracts(byte type, int counterpartyId)
        {
            switch (type)
            {
                case Customer:
                    return _db.S_CONTRACTS.AsNoTracking()
                        .Where(c => c.CustomerID == counterpartyId && c.IsDeleted != true)
                        .OrderByDescending(c => c.IssuedDate).ThenByDescending(c => c.ID)
                        .Select(c => new ContractOption { ID = c.ID, ContractNo = c.ContractNo }).ToList();
                case Supplier:
                    return _db.P_CONTRACTS.AsNoTracking()
                        .Where(c => c.SupplierID == counterpartyId && c.IsDeleted != true)
                        .OrderByDescending(c => c.IssuedDate).ThenByDescending(c => c.ID)
                        .Select(c => new ContractOption { ID = c.ID, ContractNo = c.ContractNo }).ToList();
                case Forwarder:
                    return _db.F_CONTRACTS.AsNoTracking()
                        .Where(c => c.ForwarderID == counterpartyId && c.IsDeleted != true)
                        .OrderByDescending(c => c.IssueDate).ThenByDescending(c => c.ID)
                        .Select(c => new ContractOption { ID = c.ID, ContractNo = c.ContractNo }).ToList();
                default:
                    return new List<ContractOption>();
            }
        }

        /// <summary>
        /// Shartnoma bo'yicha sana holatiga: umumiy oldi-berdi (invoyslar / yo'l varaqalari) va to'langan
        /// (to'langan summa umumiydan oshmaydi).
        /// </summary>
        public ContractTotals Totals(byte type, int counterpartyId, int contractId, DateTime date)
        {
            ContractTotals result;
            switch (type)
            {
                case Customer: result = CustomerTotals(counterpartyId, contractId, date); break;
                case Supplier: result = SupplierTotals(counterpartyId, contractId, date); break;
                case Forwarder: result = ForwarderTotals(contractId, date); break;
                default: return new ContractTotals();
            }
            if (result.Paid > result.Total) result.Paid = result.Total;
            return result;
        }

        private ContractTotals CustomerTotals(int customerId, int contractId, DateTime date)
        {
            var invoices = _db.SALES_INVOICES.AsNoTracking()
                .Where(i => i.CustomerID == customerId && i.ContractID == contractId && i.InvoiceDate <= date)
                .Select(i => new { i.ID, Amount = i.TotalWithVAT ?? i.TotalAmount })
                .ToList();

            var contractNoNorm = Normalize(_db.S_CONTRACTS
                .Where(c => c.ID == contractId && c.CustomerID == customerId && c.IsDeleted != true)
                .Select(c => c.ContractNo)
                .FirstOrDefault());

            var invIdSet = new HashSet<long>(invoices.Select(x => x.ID));
            var paid = FinQueries.Active(_db)
                .Where(t => t.TxnDate <= date &&
                            t.SourceDocType != "SalesInvoice" &&
                            ((t.Direction == 1 && t.CounterpartyType == 1 && t.CounterpartyID == customerId) ||
                             (t.SourceDocType == "SalesInvoicePayment" && t.SourceDocID.HasValue)))
                .Select(t => new { t.Amount, t.SourceDocType, t.SourceDocID, t.ContractNo })
                .ToList()
                .Where(p =>
                    (p.SourceDocType == "SalesInvoicePayment" && p.SourceDocID.HasValue && invIdSet.Contains((long)p.SourceDocID.Value))
                    || (contractNoNorm != "" && Normalize(p.ContractNo) == contractNoNorm))
                .Sum(p => Math.Abs(p.Amount));

            return new ContractTotals { Total = invoices.Sum(x => x.Amount), Paid = paid };
        }

        private ContractTotals SupplierTotals(int supplierId, int contractId, DateTime date)
        {
            // Xarid summasi — yagona qoida (FIN-B08): kirim (INCOMES.TotalPrice) yoki invoys summasi.
            var invoiceQuery = _db.P_INVOICES
                .Where(i => i.SupplierID == supplierId && i.ContractID == contractId && i.InvoiceDate <= date && (i.IsDeleted == null || i.IsDeleted == false));
            var amounts = CounterpartyBalanceService.PurchaseInvoiceAmounts(_db, invoiceQuery, date);

            var contractNoNorm = Normalize(_db.P_CONTRACTS
                .Where(c => c.ID == contractId && c.SupplierID == supplierId && c.IsDeleted != true)
                .Select(c => c.ContractNo)
                .FirstOrDefault());

            var invIdSet = new HashSet<int>(amounts.Keys);
            var paid = FinQueries.Active(_db)
                .Where(t => t.TxnDate <= date &&
                            (t.SourceDocType == null || (t.SourceDocType != "PurchaseInvoice" && t.SourceDocType != "SalesInvoice")) &&
                            ((t.Direction == 2 && t.CounterpartyType == 2 && t.CounterpartyID == supplierId) ||
                             (t.SourceDocType == "PurchaseInvoicePayment" && t.SourceDocID.HasValue)))
                .Select(t => new { t.Amount, t.SourceDocType, t.SourceDocID, t.ContractNo })
                .ToList()
                .Where(p =>
                    (p.SourceDocType == "PurchaseInvoicePayment" && p.SourceDocID.HasValue && invIdSet.Contains(p.SourceDocID.Value))
                    || (contractNoNorm != "" && Normalize(p.ContractNo) == contractNoNorm))
                .Sum(p => Math.Abs(p.Amount));

            return new ContractTotals { Total = amounts.Values.Sum(), Paid = paid };
        }

        private ContractTotals ForwarderTotals(int contractId, DateTime date)
        {
            // double ustunlar xotirada decimal'ga yig'iladi (EF ichida cast SQL'ga tarjima qilinmasdi)
            var total = _db.F_WAYBILLS.AsNoTracking()
                .Where(w => w.ContractID == contractId && w.IsDeleted != true &&
                            w.Amount.HasValue && w.IssueDate.HasValue && w.IssueDate.Value <= date)
                .Select(w => w.Amount)
                .ToList()
                .Sum(a => (decimal)(a ?? 0d));

            var paid = _db.F_WAYBILL_PAYMENTS.AsNoTracking()
                .Where(p => !p.IsDeleted &&
                            p.F_WAYBILLS.ContractID == contractId &&
                            (!p.PaymentDate.HasValue || p.PaymentDate.Value <= date))
                .Select(p => p.Payment_Amount)
                .ToList()
                .Sum(a => (decimal)a);

            return new ContractTotals { Total = total, Paid = paid };
        }
    }
}
