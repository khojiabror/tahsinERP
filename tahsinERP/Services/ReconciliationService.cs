using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.I18n;
using tahsinERP.Models;
using tahsinERP.Services.Crm;
using tahsinERP.Services.Finance;
using tahsinERP.ViewModels;

namespace tahsinERP.Services
{
    /// <summary>
    /// Akt-sverki — bitta shartnoma (xarid/sotuv/yuk tashish) yoki kontragentning barcha shartnomalari
    /// (<c>contractId = null</c>) bo'yicha.
    /// To'lovlar: FIN_TRANSACTIONS (naqd/chiqim), invoys-oyna qatorlari (PurchaseInvoice/SalesInvoice) bundan chiqariladi.
    /// Opening bo'lsa (FIN_OPENING_CONTRACT_BALANCES) — opening qoldig'i o'z sanasida, keyin faqat opening sanasidan
    /// keyingi hujjat va to'lovlar.
    /// "Barcha shartnomalar" rejimida kontragentning hamma to'lovlari (shartnomaga bog'lanmaganlari ham) kiradi —
    /// yakuniy qoldiq kontragent qarzi bilan bir xil bo'ladi. Shartnoma rejimida hech qaysi shartnomaga
    /// bog'lanmagan to'lovlar bo'lsa, ogohlantirish chiqadi (<see cref="ReconciliationViewModel.UnattributedNote"/>).
    /// FIN-B01: barcha summalar so'mda — har hujjat/to'lov o'z valyutasida o'z sanasidagi CBU kursi bilan.
    /// </summary>
    public static class ReconciliationService
    {
        private const byte TypeReceivable = 1;
        private const byte TypePayable = 2;
        private const byte TypeForwarder = 4;

        private sealed class Item
        {
            public DateTime Date;
            public int Order; // bir kunda: opening, hujjat, to'lov, tuzatish
            public string Content;
            public decimal P1Dr, P1Cr, P2Dr, P2Cr;
            public bool IsAdjustment;
        }

        /// <summary>FIN da invoys bo'yicha faqat balans (qarz) aks ettiruvchi yozuvlar — to'lov emas.</summary>
        private static bool IsFinInvoiceLedgerMirror(string sourceDocType)
        {
            if (string.IsNullOrEmpty(sourceDocType)) return false;
            return sourceDocType.Equals("PurchaseInvoice", StringComparison.OrdinalIgnoreCase)
                || sourceDocType.Equals("SalesInvoice", StringComparison.OrdinalIgnoreCase);
        }

        private static string Norm(string s)
        {
            return (s ?? "").Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Shartnoma opening'larining (FIN_OPENING_CONTRACT_BALANCES) shu turdagi eng so'nggi sanasi &lt;= asOf.
        /// FIN-B09: avval FIN_BALANCE_SNAPSHOTS'dan olinardi.
        /// </summary>
        private static DateTime? GetLatestOpeningDate(DBTHSNEntities db, DateTime asOfDate, byte balanceType)
        {
            var day = asOfDate.Date;
            return db.FIN_OPENING_CONTRACT_BALANCES
                .Where(x => x.BalanceType == balanceType && x.SnapshotDate <= day)
                .Select(x => (DateTime?)x.SnapshotDate)
                .Max();
        }

        /// <summary>
        /// Opening qoldig'i: shartnoma bo'yicha (ID yoki raqam mos birinchi qator) yoki <paramref name="contractId"/> null
        /// bo'lsa — kontragentning barcha qatorlari yig'indisi.
        /// </summary>
        private static decimal OpeningAmount(DBTHSNEntities db, DateTime openingDate, byte balanceType, int counterpartyId, int? contractId, string contractNo, bool canonicalForNumber)
        {
            var d = openingDate.Date;
            var rows = db.FIN_OPENING_CONTRACT_BALANCES.AsNoTracking()
                .Where(x => x.SnapshotDate == d && x.BalanceType == balanceType && x.CounterpartyID == counterpartyId)
                .Select(x => new { x.ID, x.ContractID, x.ContractNo, x.Amount, x.CurrencyID })
                .ToList()
                .Select(x => new { x.ID, x.ContractID, x.ContractNo, Amount = FinMoney.For(db).Uzs(x.Amount, x.CurrencyID, d) })
                .ToList();
            if (!contractId.HasValue)
                return rows.Sum(x => x.Amount);

            // Avval ContractID bo'yicha; ContractID'siz (eski) qator raqam bo'yicha faqat shu raqamning asosiy
            // shartnomasiga — bir xil raqamli ikki shartnomada opening ikki marta sanalmasin
            var byId = rows.Where(x => x.ContractID.HasValue && x.ContractID.Value == contractId.Value).ToList();
            if (byId.Count > 0)
                return byId.Sum(x => x.Amount);
            var no = Norm(contractNo);
            if (no == "" || !canonicalForNumber)
                return 0m;
            return rows.Where(x => !x.ContractID.HasValue && Norm(x.ContractNo) == no).Sum(x => x.Amount);
        }

        /// <summary>
        /// Raqam bo'yicha bog'lanadigan to'lov/opening qaysi shartnomaga tushadi: bir xil raqamli shartnomalar
        /// bo'lsa — eng kichik ID'liga (aks holda ikkala shartnoma aktida ham sanalardi).
        /// </summary>
        private static bool IsCanonicalForNumber(IDictionary<int, string> contractNoById, int? contractId)
        {
            if (!contractId.HasValue || !contractNoById.TryGetValue(contractId.Value, out var no)) return true;
            var n = Norm(no);
            return n == "" || contractNoById.Where(kv => Norm(kv.Value) == n).Min(kv => kv.Key) == contractId.Value;
        }

        /// <summary>
        /// Tuzatishlar: opening bo'lsa faqat undan keyingilari (opening holati oldingi tuzatishlarni o'z ichiga oladi —
        /// aks holda ular ikki marta sanalardi).
        /// </summary>
        private static List<ReconciliationAdjustmentRow> Adjustments(DBTHSNEntities db, byte type, int counterpartyId, int? contractId, DateTime asOfDate, DateTime? openingDate)
        {
            var rows = contractId.HasValue
                ? ReconciliationAdjustmentService.LoadForReport(db, type, counterpartyId, contractId.Value, asOfDate)
                : ReconciliationAdjustmentService.LoadForCounterparty(db, type, counterpartyId, asOfDate);
            return openingDate.HasValue
                ? rows.Where(a => a.CutoffDate.Date > openingDate.Value.Date).ToList()
                : rows;
        }

        private static ReconciliationViewModel NewVm(string type, string companyName, string party2Name, int counterpartyId, int? contractId, string contractNo, DateTime asOfDate, string activity)
        {
            return new ReconciliationViewModel
            {
                Party1Name = companyName ?? L.T("Crm_RecOurCompany"),
                Party2Name = party2Name,
                AsOfDate = asOfDate,
                ReportDate = DateTime.Now.ToString("dd.MM.yyyy"),
                ReconciliationType = type,
                CounterpartyId = counterpartyId,
                ContractId = contractId ?? 0,
                AllContracts = !contractId.HasValue,
                ContractNo = contractId.HasValue ? contractNo : L.T("Crm_RecAllContracts"),
                Rows = new List<ReconciliationRowViewModel>(),
                AgreementInfo = contractId.HasValue
                    ? L.T("Crm_RecAgreementContract", contractNo, activity)
                    : L.T("Crm_RecAgreementAll", activity)
            };
        }

        private static void AddAdjustmentItems(List<Item> items, IEnumerable<ReconciliationAdjustmentRow> adjustments)
        {
            foreach (var a in adjustments)
            {
                var note = string.IsNullOrWhiteSpace(a.Note) ? "" : " — " + a.Note;
                items.Add(new Item
                {
                    Date = a.CutoffDate,
                    Order = 3,
                    Content = L.T("Crm_RecRowAdjustment", a.EntryYear, a.CutoffDate.ToString("dd.MM.yyyy")) + note,
                    P1Dr = a.Party1Debit,
                    P1Cr = a.Party1Credit,
                    P2Dr = a.Party1Credit,
                    P2Cr = a.Party1Debit,
                    IsAdjustment = true
                });
            }
        }

        /// <summary>
        /// Qatorlar, jami va yakuniy qoldiq. Ikki tomon jadvali ko'zgu: Party2 debeti = Party1 krediti va aksincha.
        /// <paramref name="party1Receivable"/> = true (mijoz): qoldiq = Party1 debet − kredit; aks holda kredit − debet.
        /// </summary>
        private static void Finish(ReconciliationViewModel vm, List<Item> items, bool party1Receivable)
        {
            foreach (var item in items.OrderBy(x => x.Date).ThenBy(x => x.Order))
            {
                vm.Rows.Add(new ReconciliationRowViewModel
                {
                    Content = item.Content,
                    Date = item.Date,
                    Party1Debit = item.P1Dr,
                    Party1Credit = item.P1Cr,
                    Party2Debit = item.P2Dr,
                    Party2Credit = item.P2Cr,
                    IsAdjustmentRow = item.IsAdjustment
                });
            }

            vm.Party1DebitTotal = items.Sum(x => x.P1Dr);
            vm.Party1CreditTotal = items.Sum(x => x.P1Cr);
            vm.Party2DebitTotal = items.Sum(x => x.P2Dr);
            vm.Party2CreditTotal = items.Sum(x => x.P2Cr);
            vm.FinalBalance = party1Receivable
                ? vm.Party1DebitTotal - vm.Party1CreditTotal
                : vm.Party1CreditTotal - vm.Party1DebitTotal;

            if (vm.Rows.Count == 0) return;

            vm.Rows.Add(new ReconciliationRowViewModel { IsBalanceRow = true });
            var weOwe = party1Receivable ? vm.FinalBalance < 0 : vm.FinalBalance > 0; // Party1 kredit qoldig'i
            var amount = Math.Abs(vm.FinalBalance);
            vm.Rows.Add(new ReconciliationRowViewModel
            {
                Content = L.T("Crm_RecRowFinalBalance", vm.AsOfDate.ToString("dd.MM.yyyy")),
                IsBalanceRow = true,
                Party1Debit = weOwe ? 0 : amount,
                Party1Credit = weOwe ? amount : 0,
                Party2Debit = weOwe ? amount : 0,
                Party2Credit = weOwe ? 0 : amount
            });
        }

        private static string UnattributedNote(int count, decimal sum)
        {
            return count == 0
                ? null
                : L.T("Crm_RecUnattributedNote", count, sum.ToString("N2"));
        }

        /// <summary>
        /// To'lovlar (invoys ko'zgu yozuvlarisiz), summa so'mda (to'lov sanasidagi kurs). Entity o'zgartirilmaydi —
        /// faqat o'qish uchun nusxa.
        /// </summary>
        private static List<FIN_TRANSACTIONS> UzsPayments(DBTHSNEntities db, IQueryable<FIN_TRANSACTIONS> payQuery)
        {
            var money = FinMoney.For(db);
            return payQuery.AsNoTracking().OrderBy(t => t.TxnDate).ThenBy(t => t.ID).ToList()
                .Where(t => !IsFinInvoiceLedgerMirror(t.SourceDocType))
                .Select(t => new FIN_TRANSACTIONS
                {
                    ID = t.ID,
                    TxnDate = t.TxnDate,
                    Amount = money.Uzs(t.Amount, t.CurrencyID, t.TxnDate),
                    CurrencyID = t.CurrencyID,
                    Purpose = t.Purpose,
                    ContractNo = t.ContractNo,
                    SourceDocType = t.SourceDocType,
                    SourceDocID = t.SourceDocID
                })
                .ToList();
        }

        #region Ta'minotchi

        public static ReconciliationViewModel BuildSupplierReconciliation(DBTHSNEntities db, int supplierId, int? contractId, DateTime asOfDate, string companyName, string supplierName, string contractNo)
        {
            var vm = NewVm("Supplier", companyName, supplierName, supplierId, contractId, contractNo, asOfDate, L.T("Crm_RecActivitySupplier"));
            var openingDate = GetLatestOpeningDate(db, asOfDate, TypePayable);

            // Kontragentning barcha invoyslari (to'lovni shartnomaga bog'lash uchun opening'gacha bo'lganlari ham)
            var allInvoices = db.P_INVOICES
                .Where(i => i.SupplierID == supplierId && i.InvoiceDate <= asOfDate && (i.IsDeleted == null || i.IsDeleted == false))
                .Select(i => new { i.ID, i.ContractID, i.InvoiceNo, i.InvoiceDate, i.Amount })
                .ToList();
            var contractNoById = db.P_CONTRACTS.AsNoTracking()
                .Where(c => c.SupplierID == supplierId && c.IsDeleted != true)
                .Select(c => new { c.ID, c.ContractNo })
                .ToList()
                .ToDictionary(c => c.ID, c => c.ContractNo ?? "");
            var knownContractNos = new HashSet<string>(contractNoById.Values.Select(Norm).Where(n => n != ""));

            var scopeInvoiceIds = new HashSet<int>(allInvoices.Where(i => !contractId.HasValue || i.ContractID == contractId.Value).Select(i => i.ID));
            var anyContractInvoiceIds = new HashSet<int>(allInvoices.Where(i => i.ContractID > 0).Select(i => i.ID));

            // Hujjatlar: opening sanasidan keyingilari
            var invoices = allInvoices
                .Where(i => scopeInvoiceIds.Contains(i.ID) && (!openingDate.HasValue || i.InvoiceDate > openingDate.Value))
                .OrderBy(i => i.InvoiceDate).ThenBy(i => i.ID)
                .ToList();
            var invoiceIdList = invoices.Select(i => i.ID).ToList();
            // Xarid summasi — yagona qoida (FIN-B08): kirimlar TotalPrice, bo'lmasa P_INVOICES.Amount
            var amountById = invoiceIdList.Count == 0
                ? new Dictionary<int, decimal>()
                : Finance.CounterpartyBalanceService.PurchaseInvoiceAmountsUzs(db, db.P_INVOICES.Where(i => invoiceIdList.Contains(i.ID)), asOfDate);

            var payQuery = db.FIN_TRANSACTIONS
                .Where(t => t.CounterpartyType == 2 && t.CounterpartyID == supplierId && t.TxnDate <= asOfDate
                    && (t.IsDeleted == null || t.IsDeleted == false) && t.Direction == 2);
            if (openingDate.HasValue)
            {
                var od = openingDate.Value;
                payQuery = payQuery.Where(t => t.TxnDate > od);
            }
            var payCandidates = UzsPayments(db, payQuery);

            // Shartnoma rejimi: invoysi shu shartnomada (opening'gacha bo'lsa ham) yoki shartnoma raqami mos
            var canonical = IsCanonicalForNumber(contractNoById, contractId);
            var contractNoNorm = canonical ? Norm(contractNo) : "";
            var payments = payCandidates.Where(t =>
                    !contractId.HasValue
                    || (t.SourceDocID.HasValue && scopeInvoiceIds.Contains(t.SourceDocID.Value))
                    || (contractNoNorm != "" && Norm(t.ContractNo) == contractNoNorm))
                .ToList();

            if (contractId.HasValue)
            {
                var unattributed = payCandidates
                    .Where(t => !(t.SourceDocID.HasValue && anyContractInvoiceIds.Contains(t.SourceDocID.Value))
                                && !knownContractNos.Contains(Norm(t.ContractNo)))
                    .ToList();
                vm.UnattributedNote = UnattributedNote(unattributed.Count, unattributed.Sum(t => t.Amount));
            }

            var items = new List<Item>();
            if (openingDate.HasValue)
            {
                var opening = OpeningAmount(db, openingDate.Value, TypePayable, supplierId, contractId, contractNo, canonical);
                if (opening != 0)
                {
                    items.Add(new Item { Date = openingDate.Value.Date, Order = 0, Content = L.T("Crm_RecRowOpeningPayable"), P1Cr = opening, P2Dr = opening });
                }
            }

            // Opening'gacha chiqarilgan invoys, kirim hujjati opening'dan keyin kelgan: opening'da u o'sha sanadagi
            // summa bilan bor — faqat keyingi o'zgarish qatori (qarz hisobi bilan bir xil qoida)
            if (openingDate.HasValue)
            {
                var od = openingDate.Value;
                var preOpeningIds = allInvoices.Where(i => scopeInvoiceIds.Contains(i.ID) && i.InvoiceDate <= od).Select(i => i.ID).ToList();
                var changed = preOpeningIds.Count == 0
                    ? new List<int>()
                    : db.INCOMES.AsNoTracking()
                        .Where(inc => inc.InvoiceID.HasValue && preOpeningIds.Contains(inc.InvoiceID.Value) && inc.IsDeleted != true &&
                                      inc.TotalPrice.HasValue && inc.IssueDateTime > od && inc.IssueDateTime <= asOfDate)
                        .Select(inc => inc.InvoiceID.Value)
                        .Distinct()
                        .ToList();
                if (changed.Count > 0)
                {
                    var changedQuery = db.P_INVOICES.Where(i => changed.Contains(i.ID));
                    var now = Finance.CounterpartyBalanceService.PurchaseInvoiceAmountsUzs(db, changedQuery, asOfDate);
                    var then = Finance.CounterpartyBalanceService.PurchaseInvoiceAmountsUzs(db, changedQuery, od);
                    var lastIncome = db.INCOMES.AsNoTracking()
                        .Where(inc => inc.InvoiceID.HasValue && changed.Contains(inc.InvoiceID.Value) && inc.IsDeleted != true && inc.IssueDateTime <= asOfDate)
                        .GroupBy(inc => inc.InvoiceID.Value)
                        .Select(g => new { ID = g.Key, Date = g.Max(x => x.IssueDateTime) })
                        .ToList()
                        .ToDictionary(x => x.ID, x => x.Date);
                    foreach (var inv in allInvoices.Where(i => changed.Contains(i.ID)))
                    {
                        var delta = (now.TryGetValue(inv.ID, out var n) ? n : 0m) - (then.TryGetValue(inv.ID, out var o) ? o : 0m);
                        if (delta == 0) continue;
                        var date = lastIncome.TryGetValue(inv.ID, out var d) ? d.Date : asOfDate.Date;
                        items.Add(new Item
                        {
                            Date = date,
                            Order = 1,
                            Content = L.T("Crm_RecRowInvoiceChanged", inv.InvoiceNo, inv.InvoiceDate.ToString("dd.MM.yyyy")),
                            P1Cr = delta > 0 ? delta : 0,
                            P2Dr = delta > 0 ? delta : 0,
                            P1Dr = delta < 0 ? -delta : 0,
                            P2Cr = delta < 0 ? -delta : 0
                        });
                    }
                }
            }

            foreach (var inv in invoices)
            {
                var amt = amountById.TryGetValue(inv.ID, out var a) ? a : Convert.ToDecimal(inv.Amount ?? 0);
                var prefix = !contractId.HasValue && inv.ContractID > 0 && contractNoById.TryGetValue(inv.ContractID, out var no) && no != "" ? $"[{no}] " : "";
                items.Add(new Item { Date = inv.InvoiceDate, Order = 1, Content = prefix + L.T("Crm_RecRowPurchaseInvoice", inv.InvoiceNo, inv.InvoiceDate.ToString("dd.MM.yyyy")), P1Cr = amt, P2Dr = amt });
            }
            foreach (var pay in payments)
            {
                var ptxt = string.IsNullOrWhiteSpace(pay.Purpose) ? "" : " — " + pay.Purpose;
                var ctxt = !contractId.HasValue && !string.IsNullOrWhiteSpace(pay.ContractNo) ? $"[{pay.ContractNo.Trim()}] " : "";
                items.Add(new Item { Date = pay.TxnDate, Order = 2, Content = ctxt + L.T("Crm_RecRowFinPayment", pay.ID, ptxt, pay.TxnDate.ToString("dd.MM.yyyy")), P1Dr = pay.Amount, P2Cr = pay.Amount });
            }
            AddAdjustmentItems(items, Adjustments(db, ReconciliationAdjustmentService.CounterpartySupplier, supplierId, contractId, asOfDate, openingDate));

            Finish(vm, items, party1Receivable: false);
            return vm;
        }

        #endregion

        #region Mijoz

        public static ReconciliationViewModel BuildCustomerReconciliation(DBTHSNEntities db, int customerId, int? contractId, DateTime asOfDate, string companyName, string customerName, string contractNo)
        {
            var vm = NewVm("Customer", companyName, customerName, customerId, contractId, contractNo, asOfDate, L.T("Crm_RecActivityCustomer"));
            var openingDate = GetLatestOpeningDate(db, asOfDate, TypeReceivable);

            // Bekor qilingan invoyslar akt-sverkiga kirmaydi
            var allInvoices = db.SALES_INVOICES.Active()
                .Where(i => i.CustomerID == customerId && i.InvoiceDate <= asOfDate)
                .Select(i => new { i.ID, i.ContractID, i.InvoiceNo, i.InvoiceDate, Amount = i.TotalWithVAT ?? i.TotalAmount, i.CurrencyID })
                .ToList()
                .Select(i => new { i.ID, i.ContractID, i.InvoiceNo, i.InvoiceDate, Amount = FinMoney.For(db).Uzs(i.Amount, i.CurrencyID, i.InvoiceDate) })
                .ToList();
            var contractNoById = db.S_CONTRACTS.AsNoTracking()
                .Where(c => c.CustomerID == customerId && c.IsDeleted != true)
                .Select(c => new { c.ID, c.ContractNo })
                .ToList()
                .ToDictionary(c => c.ID, c => c.ContractNo ?? "");
            var knownContractNos = new HashSet<string>(contractNoById.Values.Select(Norm).Where(n => n != ""));

            var scopeInvoiceIds = new HashSet<long>(allInvoices.Where(i => !contractId.HasValue || i.ContractID == contractId.Value).Select(i => i.ID));
            var anyContractInvoiceIds = new HashSet<long>(allInvoices.Where(i => i.ContractID.HasValue).Select(i => i.ID));

            var invoices = allInvoices
                .Where(i => scopeInvoiceIds.Contains(i.ID) && (!openingDate.HasValue || i.InvoiceDate > openingDate.Value))
                .OrderBy(i => i.InvoiceDate).ThenBy(i => i.ID)
                .ToList();

            var payQuery = db.FIN_TRANSACTIONS
                .Where(t => t.CounterpartyType == 1 && t.CounterpartyID == customerId && t.TxnDate <= asOfDate
                    && (t.IsDeleted == null || t.IsDeleted == false) && t.Direction == 1);
            if (openingDate.HasValue)
            {
                var od = openingDate.Value;
                payQuery = payQuery.Where(t => t.TxnDate > od);
            }
            var payCandidates = UzsPayments(db, payQuery);

            var canonical = IsCanonicalForNumber(contractNoById, contractId);
            var contractNoNorm = canonical ? Norm(contractNo) : "";
            var payments = payCandidates.Where(t =>
                    !contractId.HasValue
                    || (t.SourceDocID.HasValue && scopeInvoiceIds.Contains(t.SourceDocID.Value))
                    || (contractNoNorm != "" && Norm(t.ContractNo) == contractNoNorm))
                .ToList();

            if (contractId.HasValue)
            {
                var unattributed = payCandidates
                    .Where(t => !(t.SourceDocID.HasValue && anyContractInvoiceIds.Contains(t.SourceDocID.Value))
                                && !knownContractNos.Contains(Norm(t.ContractNo)))
                    .ToList();
                vm.UnattributedNote = UnattributedNote(unattributed.Count, unattributed.Sum(t => t.Amount));
            }

            var items = new List<Item>();
            if (openingDate.HasValue)
            {
                var opening = OpeningAmount(db, openingDate.Value, TypeReceivable, customerId, contractId, contractNo, canonical);
                if (opening != 0)
                {
                    items.Add(new Item { Date = openingDate.Value.Date, Order = 0, Content = L.T("Crm_RecRowOpeningReceivable"), P1Dr = opening, P2Cr = opening });
                }
            }

            foreach (var inv in invoices)
            {
                var prefix = !contractId.HasValue && inv.ContractID.HasValue && contractNoById.TryGetValue(inv.ContractID.Value, out var no) && no != "" ? $"[{no}] " : "";
                items.Add(new Item { Date = inv.InvoiceDate, Order = 1, Content = prefix + L.T("Crm_RecRowSalesInvoice", inv.InvoiceNo, inv.InvoiceDate.ToString("dd.MM.yyyy")), P1Dr = inv.Amount, P2Cr = inv.Amount });
            }
            foreach (var pay in payments)
            {
                var ptxt = string.IsNullOrWhiteSpace(pay.Purpose) ? "" : " — " + pay.Purpose;
                var ctxt = !contractId.HasValue && !string.IsNullOrWhiteSpace(pay.ContractNo) ? $"[{pay.ContractNo.Trim()}] " : "";
                items.Add(new Item { Date = pay.TxnDate, Order = 2, Content = ctxt + L.T("Crm_RecRowFinPayment", pay.ID, ptxt, pay.TxnDate.ToString("dd.MM.yyyy")), P1Cr = pay.Amount, P2Dr = pay.Amount });
            }
            AddAdjustmentItems(items, Adjustments(db, ReconciliationAdjustmentService.CounterpartyCustomer, customerId, contractId, asOfDate, openingDate));

            Finish(vm, items, party1Receivable: true);
            return vm;
        }

        #endregion

        #region Ekspeditor

        public static ReconciliationViewModel BuildForwarderReconciliation(DBTHSNEntities db, int forwarderId, int? contractId, DateTime asOfDate, string companyName, string forwarderName, string contractNo)
        {
            var vm = NewVm("Forwarder", companyName, forwarderName, forwarderId, contractId, contractNo, asOfDate, L.T("Crm_RecActivityForwarder"));
            // Foydalanuvchi qarori (2026-10-18): ta'minotchi/mijoz kabi — opening (BalanceType 4) bo'lsa, shu sanadan keyingilar
            var openingDate = GetLatestOpeningDate(db, asOfDate, TypeForwarder);

            var contractNoById = db.F_CONTRACTS.AsNoTracking()
                .Where(c => c.ForwarderID == forwarderId && c.IsDeleted == false)
                .Select(c => new { c.ID, c.ContractNo })
                .ToList()
                .ToDictionary(c => c.ID, c => c.ContractNo ?? "");
            var knownContractNos = new HashSet<string>(contractNoById.Values.Select(Norm).Where(n => n != ""));

            var scopeWaybills = db.F_WAYBILLS
                .Where(w => w.ForwarderID == forwarderId && w.IssueDate <= asOfDate && (w.IsDeleted == null || w.IsDeleted == false) && w.Amount.HasValue)
                .Select(w => new { w.ID, w.ContractID, w.WaybillNo, w.IssueDate, w.Amount, w.CurrencyID })
                .ToList()
                .Where(w => !contractId.HasValue || w.ContractID == contractId.Value)
                .OrderBy(w => w.IssueDate).ThenBy(w => w.ID)
                .ToList();
            var waybills = openingDate.HasValue
                ? scopeWaybills.Where(w => w.IssueDate > openingDate.Value).ToList()
                : scopeWaybills;

            // To'lovlar — doiradagi barcha yo'l varaqalari bo'yicha (opening'gacha chiqarilganiga keyin to'langani ham)
            var wbIds = scopeWaybills.Select(w => w.ID).ToList();
            var paymentQuery = db.F_WAYBILL_PAYMENTS
                .Where(p => p.ForwarderID == forwarderId && wbIds.Contains(p.WaybillID) && (p.PaymentDate ?? DateTime.MinValue) <= asOfDate && !p.IsDeleted);
            if (openingDate.HasValue)
            {
                // Sanasiz to'lov opening'ga kirgan (AutoFill ham shunday hisoblaydi)
                var od = openingDate.Value;
                paymentQuery = paymentQuery.Where(p => p.PaymentDate.HasValue && p.PaymentDate.Value > od);
            }
            var payments = paymentQuery
                .OrderBy(p => p.PaymentDate).ThenBy(p => p.ID)
                .Select(p => new { p.ID, p.PaymentDate, p.Payment_Amount, p.WaybillID, p.CurrencyID })
                .ToList();

            // Qo'lda kiritilgan FIN to'lovlari (WaybillPayment dublikatlari chiqariladi)
            var finQuery = db.FIN_TRANSACTIONS
                .Where(t => t.CounterpartyType == 4
                    && t.CounterpartyID == forwarderId
                    && t.Direction == 2
                    && (t.IsDeleted == null || t.IsDeleted == false)
                    && t.TxnDate <= asOfDate
                    && (t.SourceDocType == null || t.SourceDocType != "WaybillPayment"));
            if (openingDate.HasValue)
            {
                var od = openingDate.Value;
                finQuery = finQuery.Where(t => t.TxnDate > od);
            }
            var finCandidates = finQuery
                .OrderBy(t => t.TxnDate).ThenBy(t => t.ID)
                .Select(t => new { t.ID, t.TxnDate, t.Amount, t.Purpose, t.ContractNo, t.CurrencyID })
                .ToList()
                .Select(t => new { t.ID, t.TxnDate, Amount = FinMoney.For(db).Uzs(t.Amount, t.CurrencyID, t.TxnDate), t.Purpose, t.ContractNo })
                .ToList();
            var canonical = IsCanonicalForNumber(contractNoById, contractId);
            var contractNoNorm = canonical ? Norm(contractNo) : "";
            var finPayments = finCandidates
                .Where(t => !contractId.HasValue || (contractNoNorm != "" && Norm(t.ContractNo) == contractNoNorm))
                .ToList();
            if (contractId.HasValue)
            {
                var unattributed = finCandidates.Where(t => !knownContractNos.Contains(Norm(t.ContractNo))).ToList();
                vm.UnattributedNote = UnattributedNote(unattributed.Count, unattributed.Sum(t => t.Amount));
            }

            var items = new List<Item>();
            if (openingDate.HasValue)
            {
                var opening = OpeningAmount(db, openingDate.Value, TypeForwarder, forwarderId, contractId, contractNo, canonical);
                if (opening != 0)
                {
                    items.Add(new Item { Date = openingDate.Value.Date, Order = 0, Content = L.T("Crm_RecRowOpeningPayable"), P1Cr = opening, P2Dr = opening });
                }
            }
            foreach (var wb in waybills)
            {
                var amt = FinMoney.For(db).Uzs(wb.Amount ?? 0m, wb.CurrencyID, wb.IssueDate ?? asOfDate);
                var prefix = !contractId.HasValue && wb.ContractID.HasValue && contractNoById.TryGetValue(wb.ContractID.Value, out var no) && no != "" ? $"[{no}] " : "";
                items.Add(new Item { Date = wb.IssueDate ?? DateTime.MinValue, Order = 1, Content = prefix + L.T("Crm_RecRowWaybill", wb.WaybillNo, wb.IssueDate?.ToString("dd.MM.yyyy") ?? ""), P1Cr = amt, P2Dr = amt });
            }
            foreach (var pay in payments)
            {
                var amt = FinMoney.For(db).Uzs(pay.Payment_Amount, pay.CurrencyID, pay.PaymentDate ?? asOfDate);
                items.Add(new Item { Date = pay.PaymentDate ?? DateTime.MinValue, Order = 2, Content = L.T("Crm_RecRowFreightPayment", pay.ID, pay.PaymentDate?.ToString("dd.MM.yyyy") ?? ""), P1Dr = amt, P2Cr = amt });
            }
            foreach (var pay in finPayments)
            {
                var ptxt = string.IsNullOrWhiteSpace(pay.Purpose) ? "" : $" — {pay.Purpose}";
                var ctxt = !contractId.HasValue && !string.IsNullOrWhiteSpace(pay.ContractNo) ? $"[{pay.ContractNo.Trim()}] " : "";
                items.Add(new Item { Date = pay.TxnDate, Order = 2, Content = ctxt + L.T("Crm_RecRowFinPayment", pay.ID, ptxt, pay.TxnDate.ToString("dd.MM.yyyy")), P1Dr = pay.Amount, P2Cr = pay.Amount });
            }
            AddAdjustmentItems(items, Adjustments(db, ReconciliationAdjustmentService.CounterpartyForwarder, forwarderId, contractId, asOfDate, openingDate));

            Finish(vm, items, party1Receivable: false);
            return vm;
        }

        #endregion
    }
}
