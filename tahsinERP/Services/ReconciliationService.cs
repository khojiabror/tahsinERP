using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Services
{
    /// <summary>
    /// Akt-sverki — faqat bitta shartnoma (xarid/sotuv/yuk tashish) bo'yicha.
    /// To'lovlar: FIN_TRANSACTIONS (naqd/chiqim), invoys-oyna qatorlari (PurchaseInvoice/SalesInvoice) bundan chiqariladi.
    /// </summary>
    public static class ReconciliationService
    {
        /// <summary>FIN da invoys bo'yicha faqat balans (qarz) aks ettiruvchi yozuvlar — to'lov emas.</summary>
        private static bool IsFinInvoiceLedgerMirror(string sourceDocType)
        {
            if (string.IsNullOrEmpty(sourceDocType)) return false;
            return sourceDocType.Equals("PurchaseInvoice", StringComparison.OrdinalIgnoreCase)
                || sourceDocType.Equals("SalesInvoice", StringComparison.OrdinalIgnoreCase);
        }

        public static ReconciliationViewModel BuildSupplierReconciliation(DBTHSNEntities db, int supplierId, int contractId, DateTime asOfDate, string companyName, string supplierName, string contractNo)
        {
            var vm = new ReconciliationViewModel
            {
                Party1Name = companyName ?? "Bizning korxona",
                Party2Name = supplierName,
                AsOfDate = asOfDate,
                ReportDate = DateTime.Now.ToString("dd.MM.yyyy"),
                ReconciliationType = "Supplier",
                CounterpartyId = supplierId,
                ContractId = contractId,
                ContractNo = contractNo,
                Rows = new List<ReconciliationRowViewModel>(),
                AgreementInfo = $"Shartnoma №{contractNo} — xarid-xo'jalik faoliyati bo'yicha o'zaro hisob-kitoblar"
            };

            var invoices = db.P_INVOICES
                .Where(i => i.SupplierID == supplierId && i.ContractID == contractId && i.InvoiceDate <= asOfDate && (i.IsDeleted == null || i.IsDeleted == false))
                .OrderBy(i => i.InvoiceDate)
                .Select(i => new { i.ID, i.InvoiceNo, i.InvoiceDate, i.Amount })
                .ToList();

            var invIdSet = new HashSet<int>(invoices.Select(i => i.ID));
            var payCandidates = db.FIN_TRANSACTIONS
                .Where(t => t.CounterpartyType == 2 && t.CounterpartyID == supplierId && t.TxnDate <= asOfDate
                    && (t.IsDeleted == null || t.IsDeleted == false) && t.Direction == 2)
                .OrderBy(t => t.TxnDate)
                .ToList();

            var contractNoNorm = (contractNo ?? "").Trim();
            // Naqd to'lovlar FIN dan: invoys ID si shu shartnoma invoyslariga bog'langan (tur qanday bo'lishidan qat'iy nazarda tutmasdan),
            // yoki shartnoma raqami mos; PurchaseInvoice yozuvlari — invoys bilan dublikat, chiqarilmaydi.
            var payments = payCandidates.Where(t =>
            {
                if (IsFinInvoiceLedgerMirror(t.SourceDocType))
                    return false;
                if (t.SourceDocID.HasValue && invIdSet.Contains(t.SourceDocID.Value))
                    return true;
                if (!string.IsNullOrEmpty(contractNoNorm) && !string.IsNullOrEmpty(t.ContractNo)
                    && string.Equals(t.ContractNo.Trim(), contractNoNorm, StringComparison.OrdinalIgnoreCase))
                    return true;
                return false;
            }).OrderBy(t => t.TxnDate).ToList();

            var allItems = new List<(DateTime dt, string type, string content, decimal p1Dr, decimal p1Cr, decimal p2Dr, decimal p2Cr)>();

            foreach (var inv in invoices)
            {
                var amt = Convert.ToDecimal(inv.Amount);
                allItems.Add((inv.InvoiceDate, "inv",
                    $"Hisob-faktura №{inv.InvoiceNo} sanasi {inv.InvoiceDate:dd.MM.yyyy}",
                    0, amt, amt, 0));
            }
            foreach (var pay in payments)
            {
                var ptxt = string.IsNullOrWhiteSpace(pay.Purpose) ? "" : " — " + pay.Purpose;
                allItems.Add((pay.TxnDate, "pay",
                    $"FIN to'lovi №{pay.ID}{ptxt} sanasi {pay.TxnDate:dd.MM.yyyy}",
                    pay.Amount, 0, 0, pay.Amount));
            }

            var adjustments = ReconciliationAdjustmentService.LoadForReport(db, ReconciliationAdjustmentService.CounterpartySupplier, supplierId, contractId, asOfDate);
            foreach (var a in adjustments)
            {
                var note = string.IsNullOrWhiteSpace(a.Note) ? "" : " — " + a.Note;
                allItems.Add((a.CutoffDate, "adj",
                    $"Akt-sverki tuzatishi ({a.EntryYear} yil), {a.CutoffDate:dd.MM.yyyy}{note}",
                    a.Party1Debit, a.Party1Credit, a.Party1Credit, a.Party1Debit));
            }

            var ordered = allItems.OrderBy(x => x.dt).ThenBy(x => x.type == "inv" ? 0 : x.type == "pay" ? 1 : 2).ToList();

            foreach (var item in ordered)
            {
                vm.Rows.Add(new ReconciliationRowViewModel
                {
                    Content = item.content,
                    Date = item.dt,
                    Party1Debit = item.p1Dr,
                    Party1Credit = item.p1Cr,
                    Party2Debit = item.p2Dr,
                    Party2Credit = item.p2Cr,
                    IsAdjustmentRow = item.type == "adj"
                });
            }

            var totalInv = invoices.Sum(i => Convert.ToDecimal(i.Amount));
            var totalPay = payments.Sum(p => p.Amount);
            var adjDr = adjustments.Sum(a => a.Party1Debit);
            var adjCr = adjustments.Sum(a => a.Party1Credit);

            vm.Party1DebitTotal = totalPay + adjDr;
            vm.Party1CreditTotal = totalInv + adjCr;
            vm.Party2DebitTotal = totalInv + adjCr;
            vm.Party2CreditTotal = totalPay + adjDr;
            vm.FinalBalance = vm.Party1CreditTotal - vm.Party1DebitTotal;

            if (vm.Rows != null && vm.Rows.Count > 0)
            {
                vm.Rows.Add(new ReconciliationRowViewModel { IsBalanceRow = true });
                vm.Rows.Add(new ReconciliationRowViewModel
                {
                    Content = $"Yakuniy qoldiq {asOfDate:dd.MM.yyyy}",
                    IsBalanceRow = true,
                    Party1Debit = vm.FinalBalance > 0 ? 0 : Math.Abs(vm.FinalBalance),
                    Party1Credit = vm.FinalBalance > 0 ? vm.FinalBalance : 0,
                    Party2Debit = vm.FinalBalance > 0 ? vm.FinalBalance : 0,
                    Party2Credit = vm.FinalBalance > 0 ? 0 : Math.Abs(vm.FinalBalance)
                });
            }

            return vm;
        }

        public static ReconciliationViewModel BuildCustomerReconciliation(DBTHSNEntities db, int customerId, int contractId, DateTime asOfDate, string companyName, string customerName, string contractNo)
        {
            var vm = new ReconciliationViewModel
            {
                Party1Name = companyName ?? "Bizning korxona",
                Party2Name = customerName,
                AsOfDate = asOfDate,
                ReportDate = DateTime.Now.ToString("dd.MM.yyyy"),
                ReconciliationType = "Customer",
                CounterpartyId = customerId,
                ContractId = contractId,
                ContractNo = contractNo,
                Rows = new List<ReconciliationRowViewModel>(),
                AgreementInfo = $"Shartnoma №{contractNo} — sotuv faoliyati bo'yicha o'zaro hisob-kitoblar"
            };

            var invoices = db.SALES_INVOICES
                .Where(i => i.CustomerID == customerId && i.ContractID == contractId && i.InvoiceDate <= asOfDate)
                .OrderBy(i => i.InvoiceDate)
                .Select(i => new { i.ID, i.InvoiceNo, i.InvoiceDate, Amount = i.TotalAmount })
                .ToList();

            var invIdSet = new HashSet<long>(invoices.Select(i => i.ID));
            var contractNoNorm = (contractNo ?? "").Trim();

            var payCandidates = db.FIN_TRANSACTIONS
                .Where(t => t.CounterpartyType == 1 && t.CounterpartyID == customerId && t.TxnDate <= asOfDate
                    && (t.IsDeleted == null || t.IsDeleted == false) && t.Direction == 1)
                .OrderBy(t => t.TxnDate)
                .ToList();

            var payments = payCandidates.Where(t =>
            {
                if (IsFinInvoiceLedgerMirror(t.SourceDocType))
                    return false;
                if (t.SourceDocID.HasValue && invIdSet.Contains((long)t.SourceDocID.Value))
                    return true;
                if (!string.IsNullOrEmpty(contractNoNorm) && !string.IsNullOrEmpty(t.ContractNo)
                    && string.Equals(t.ContractNo.Trim(), contractNoNorm, StringComparison.OrdinalIgnoreCase))
                    return true;
                return false;
            }).OrderBy(t => t.TxnDate).ToList();

            var allItems = new List<(DateTime dt, string type, string content, decimal p1Dr, decimal p1Cr, decimal p2Dr, decimal p2Cr)>();

            foreach (var inv in invoices)
            {
                allItems.Add((inv.InvoiceDate, "inv",
                    $"Sotuv invoysi №{inv.InvoiceNo} sanasi {inv.InvoiceDate:dd.MM.yyyy}",
                    inv.Amount, 0, 0, inv.Amount));
            }
            foreach (var pay in payments)
            {
                var ptxt = string.IsNullOrWhiteSpace(pay.Purpose) ? "" : " — " + pay.Purpose;
                allItems.Add((pay.TxnDate, "pay",
                    $"FIN to'lovi №{pay.ID}{ptxt} sanasi {pay.TxnDate:dd.MM.yyyy}",
                    0, pay.Amount, pay.Amount, 0));
            }

            var adjList = ReconciliationAdjustmentService.LoadForReport(db, ReconciliationAdjustmentService.CounterpartyCustomer, customerId, contractId, asOfDate);
            foreach (var a in adjList)
            {
                var note = string.IsNullOrWhiteSpace(a.Note) ? "" : " — " + a.Note;
                allItems.Add((a.CutoffDate, "adj",
                    $"Akt-sverki tuzatishi ({a.EntryYear} yil), {a.CutoffDate:dd.MM.yyyy}{note}",
                    a.Party1Debit, a.Party1Credit, a.Party1Credit, a.Party1Debit));
            }

            foreach (var item in allItems.OrderBy(x => x.dt).ThenBy(x => x.type == "inv" ? 0 : x.type == "pay" ? 1 : 2))
            {
                vm.Rows.Add(new ReconciliationRowViewModel
                {
                    Content = item.content,
                    Date = item.dt,
                    Party1Debit = item.p1Dr,
                    Party1Credit = item.p1Cr,
                    Party2Debit = item.p2Dr,
                    Party2Credit = item.p2Cr,
                    IsAdjustmentRow = item.type == "adj"
                });
            }

            var totalInv = invoices.Sum(i => i.Amount);
            var totalPay = payments.Sum(p => p.Amount);
            var adjDr = adjList.Sum(a => a.Party1Debit);
            var adjCr = adjList.Sum(a => a.Party1Credit);

            vm.Party1DebitTotal = totalInv + adjDr;
            vm.Party1CreditTotal = totalPay + adjCr;
            vm.Party2DebitTotal = totalPay + adjCr;
            vm.Party2CreditTotal = totalInv + adjDr;
            vm.FinalBalance = vm.Party1DebitTotal - vm.Party1CreditTotal;

            if (vm.Rows.Count > 0)
            {
                vm.Rows.Add(new ReconciliationRowViewModel { IsBalanceRow = true });
                vm.Rows.Add(new ReconciliationRowViewModel
                {
                    Content = $"Yakuniy qoldiq {asOfDate:dd.MM.yyyy}",
                    IsBalanceRow = true,
                    Party1Debit = vm.FinalBalance > 0 ? vm.FinalBalance : 0,
                    Party1Credit = vm.FinalBalance > 0 ? 0 : Math.Abs(vm.FinalBalance),
                    Party2Debit = vm.FinalBalance > 0 ? 0 : Math.Abs(vm.FinalBalance),
                    Party2Credit = vm.FinalBalance > 0 ? vm.FinalBalance : 0
                });
            }

            return vm;
        }

        public static ReconciliationViewModel BuildForwarderReconciliation(DBTHSNEntities db, int forwarderId, int contractId, DateTime asOfDate, string companyName, string forwarderName, string contractNo)
        {
            var vm = new ReconciliationViewModel
            {
                Party1Name = companyName ?? "Bizning korxona",
                Party2Name = forwarderName,
                AsOfDate = asOfDate,
                ReportDate = DateTime.Now.ToString("dd.MM.yyyy"),
                ReconciliationType = "Forwarder",
                CounterpartyId = forwarderId,
                ContractId = contractId,
                ContractNo = contractNo,
                Rows = new List<ReconciliationRowViewModel>(),
                AgreementInfo = $"Shartnoma №{contractNo} — yuk tashish xizmatlari bo'yicha o'zaro hisob-kitoblar"
            };

            var waybills = db.F_WAYBILLS
                .Where(w => w.ForwarderID == forwarderId && w.ContractID == contractId && w.IssueDate <= asOfDate && (w.IsDeleted == null || w.IsDeleted == false) && w.Amount.HasValue)
                .OrderBy(w => w.IssueDate)
                .Select(w => new { w.ID, w.WaybillNo, w.IssueDate, w.Amount })
                .ToList();

            var wbIds = new HashSet<int>(waybills.Select(w => w.ID));

            var payments = db.F_WAYBILL_PAYMENTS
                .Where(p => p.ForwarderID == forwarderId && wbIds.Contains(p.WaybillID) && (p.PaymentDate ?? DateTime.MinValue) <= asOfDate && !p.IsDeleted)
                .OrderBy(p => p.PaymentDate)
                .Select(p => new { p.ID, p.PaymentDate, p.Payment_Amount, p.WaybillID })
                .ToList();

            var contractNoNorm = (contractNo ?? "").Trim();
            // Qo'lda kiritilgan FIN to'lovlari: WaybillPayment dublikatlarini chiqaramiz.
            // Agar shartnoma tanlangan bo'lsa, faqat ContractNo mos to'lovlar kiritiladi.
            var finPayments = db.FIN_TRANSACTIONS
                .Where(t => t.CounterpartyType == 4
                    && t.CounterpartyID == forwarderId
                    && t.Direction == 2
                    && (t.IsDeleted == null || t.IsDeleted == false)
                    && t.TxnDate <= asOfDate
                    && (t.SourceDocType == null || t.SourceDocType != "WaybillPayment")
                    && (string.IsNullOrEmpty(contractNoNorm)
                        || (!string.IsNullOrEmpty(t.ContractNo)
                            && t.ContractNo.Trim().Equals(contractNoNorm, StringComparison.OrdinalIgnoreCase))))
                .OrderBy(t => t.TxnDate)
                .Select(t => new { t.ID, t.TxnDate, t.Amount, t.Purpose })
                .ToList();

            var allItems = new List<(DateTime dt, string type, string content, decimal p1Dr, decimal p1Cr, decimal p2Dr, decimal p2Cr)>();

            foreach (var wb in waybills)
            {
                var amt = Convert.ToDecimal(wb.Amount ?? 0);
                allItems.Add((wb.IssueDate ?? DateTime.MinValue, "wb",
                    $"Yo'l varaqasi №{wb.WaybillNo} sanasi {(wb.IssueDate?.ToString("dd.MM.yyyy") ?? "")}",
                    0, amt, amt, 0));
            }
            foreach (var pay in payments)
            {
                var amt = Convert.ToDecimal(pay.Payment_Amount);
                allItems.Add((pay.PaymentDate ?? DateTime.MinValue, "pay",
                    $"Yuk tashish to'lovi №{pay.ID} sanasi {(pay.PaymentDate?.ToString("dd.MM.yyyy") ?? "")}",
                    amt, 0, 0, amt));
            }
            foreach (var pay in finPayments)
            {
                var ptxt = string.IsNullOrWhiteSpace(pay.Purpose) ? "" : $" — {pay.Purpose}";
                allItems.Add((pay.TxnDate, "fin",
                    $"FIN to'lovi №{pay.ID}{ptxt} sanasi {pay.TxnDate:dd.MM.yyyy}",
                    pay.Amount, 0, 0, pay.Amount));
            }

            var adjList = ReconciliationAdjustmentService.LoadForReport(db, ReconciliationAdjustmentService.CounterpartyForwarder, forwarderId, contractId, asOfDate);
            foreach (var a in adjList)
            {
                var note = string.IsNullOrWhiteSpace(a.Note) ? "" : " — " + a.Note;
                allItems.Add((a.CutoffDate, "adj",
                    $"Akt-sverki tuzatishi ({a.EntryYear} yil), {a.CutoffDate:dd.MM.yyyy}{note}",
                    a.Party1Debit, a.Party1Credit, a.Party1Credit, a.Party1Debit));
            }

            foreach (var item in allItems.OrderBy(x => x.dt).ThenBy(x => x.type == "wb" ? 0 : (x.type == "pay" || x.type == "fin") ? 1 : 2))
            {
                vm.Rows.Add(new ReconciliationRowViewModel
                {
                    Content = item.content,
                    Date = item.dt,
                    Party1Debit = item.p1Dr,
                    Party1Credit = item.p1Cr,
                    Party2Debit = item.p2Dr,
                    Party2Credit = item.p2Cr,
                    IsAdjustmentRow = item.type == "adj"
                });
            }

            var totalWb = waybills.Sum(w => Convert.ToDecimal(w.Amount ?? 0));
            var totalPay = payments.Sum(p => Convert.ToDecimal(p.Payment_Amount))
                + finPayments.Sum(p => p.Amount);
            var adjDr = adjList.Sum(a => a.Party1Debit);
            var adjCr = adjList.Sum(a => a.Party1Credit);

            vm.Party1DebitTotal = totalPay + adjDr;
            vm.Party1CreditTotal = totalWb + adjCr;
            vm.Party2DebitTotal = totalWb + adjCr;
            vm.Party2CreditTotal = totalPay + adjDr;
            vm.FinalBalance = vm.Party1CreditTotal - vm.Party1DebitTotal;

            if (vm.Rows.Count > 0)
            {
                vm.Rows.Add(new ReconciliationRowViewModel { IsBalanceRow = true });
                vm.Rows.Add(new ReconciliationRowViewModel
                {
                    Content = $"Yakuniy qoldiq {asOfDate:dd.MM.yyyy}",
                    IsBalanceRow = true,
                    Party1Debit = vm.FinalBalance > 0 ? 0 : Math.Abs(vm.FinalBalance),
                    Party1Credit = vm.FinalBalance > 0 ? vm.FinalBalance : 0,
                    Party2Debit = vm.FinalBalance > 0 ? vm.FinalBalance : 0,
                    Party2Credit = vm.FinalBalance > 0 ? 0 : Math.Abs(vm.FinalBalance)
                });
            }

            return vm;
        }
    }
}
