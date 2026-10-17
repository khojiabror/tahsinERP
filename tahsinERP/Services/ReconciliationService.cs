using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlClient;
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

        /// <summary>
        /// Shartnoma opening'larining (FIN_OPENING_CONTRACT_BALANCES) shu turdagi eng so'nggi sanasi <= asOf.
        /// FIN-B09: avval FIN_BALANCE_SNAPSHOTS'dan olinardi - u yerdagi sana shartnoma qatorlari sanasiga
        /// to'g'ri kelmasa, opening umuman topilmasdi.
        /// </summary>
        private static DateTime? GetLatestOpeningDate(DBTHSNEntities db, DateTime asOfDate, byte balanceType)
        {
            var day = asOfDate.Date;
            return db.FIN_OPENING_CONTRACT_BALANCES
                .Where(x => x.BalanceType == balanceType && x.SnapshotDate <= day)
                .Select(x => (DateTime?)x.SnapshotDate)
                .Max();
        }

        private static OpeningContractBalanceLite GetOpeningContractBalance(DBTHSNEntities db, DateTime openingDate, byte balanceType, int counterpartyId, int contractId, string contractNo)
        {
            var contractNoNorm = (contractNo ?? "").Trim().ToUpperInvariant();
            var rows = db.Database.SqlQuery<OpeningContractBalanceLite>(
                "SELECT TOP 1 Amount FROM FIN_OPENING_CONTRACT_BALANCES " +
                "WHERE SnapshotDate = @p0 AND BalanceType = @p1 AND CounterpartyID = @p2 " +
                "AND ((ContractID IS NOT NULL AND ContractID = @p3) " +
                "OR (@p4 <> '' AND UPPER(LTRIM(RTRIM(ISNULL(ContractNo,'')))) = @p4) " +
                "OR (@p3 IS NULL AND ContractID IS NULL AND ISNULL(ContractNo,'') = ''))",
                new SqlParameter("@p0", openingDate.Date),
                new SqlParameter("@p1", balanceType),
                new SqlParameter("@p2", counterpartyId),
                new SqlParameter("@p3", contractId),
                new SqlParameter("@p4", contractNoNorm)
            ).ToList();

            return rows.FirstOrDefault();
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

            // FIN-B07: opening bo'lsa - opening qoldig'i o'z sanasida, keyin faqat shu sanadan keyingi harakatlar
            // (avval opening + butun tarix qo'shilib, opening'gacha bo'lgan tarix ikki marta hisoblanardi).
            var openingDate = GetLatestOpeningDate(db, asOfDate, 2);
            var invoiceQuery = db.P_INVOICES
                .Where(i => i.SupplierID == supplierId && i.ContractID == contractId && i.InvoiceDate <= asOfDate && (i.IsDeleted == null || i.IsDeleted == false));
            if (openingDate.HasValue)
            {
                var od = openingDate.Value;
                invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate > od);
            }
            var invoices = invoiceQuery
                .OrderBy(i => i.InvoiceDate)
                .Select(i => new { i.ID, i.InvoiceNo, i.InvoiceDate, i.Amount })
                .ToList();

            // Xarid summasi - yagona qoida (FIN-B08): kirimlar TotalPrice (odatda QQS bilan), bo'lmasa P_INVOICES.Amount.
            var incomeTotalsByInvoice = Finance.CounterpartyBalanceService.PurchaseInvoiceAmounts(db, invoiceQuery, asOfDate);

            var invIdSet = new HashSet<int>(invoices.Select(i => i.ID));
            var payQuery = db.FIN_TRANSACTIONS
                .Where(t => t.CounterpartyType == 2 && t.CounterpartyID == supplierId && t.TxnDate <= asOfDate
                    && (t.IsDeleted == null || t.IsDeleted == false) && t.Direction == 2);
            if (openingDate.HasValue)
            {
                var od = openingDate.Value;
                payQuery = payQuery.Where(t => t.TxnDate > od);
            }
            var payCandidates = payQuery.OrderBy(t => t.TxnDate).ToList();

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
            var opening = openingDate.HasValue
                ? GetOpeningContractBalance(db, openingDate.Value, 2, supplierId, contractId, contractNo)
                : null;

            if (opening != null && opening.Amount != 0)
            {
                allItems.Add((openingDate.Value.Date, "open",
                    "Opening balansdan boshlangan qarzdorlik",
                    0, opening.Amount, opening.Amount, 0));
            }

            foreach (var inv in invoices)
            {
                var amt = incomeTotalsByInvoice.ContainsKey(inv.ID)
                    ? incomeTotalsByInvoice[inv.ID]
                    : Convert.ToDecimal(inv.Amount ?? 0);
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

            var ordered = allItems.OrderBy(x => x.dt).ThenBy(x => x.type == "open" ? 0 : x.type == "inv" ? 1 : x.type == "pay" ? 2 : 3).ToList();

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

            var totalInv = invoices.Sum(i =>
                incomeTotalsByInvoice.ContainsKey(i.ID)
                    ? incomeTotalsByInvoice[i.ID]
                    : Convert.ToDecimal(i.Amount ?? 0));
            if (opening != null) totalInv += opening.Amount;
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

            var openingDate = GetLatestOpeningDate(db, asOfDate, 1);
            var invoiceQuery = db.SALES_INVOICES
                .Where(i => i.CustomerID == customerId && i.ContractID == contractId && i.InvoiceDate <= asOfDate);
            if (openingDate.HasValue)
            {
                var od = openingDate.Value;
                invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate > od);
            }
            var invoices = invoiceQuery
                .OrderBy(i => i.InvoiceDate)
                .Select(i => new
                {
                    i.ID,
                    i.InvoiceNo,
                    i.InvoiceDate,
                    Amount = i.TotalAmount,
                    TotalWithVAT = i.TotalWithVAT
                })
                .ToList();

            var invIdSet = new HashSet<long>(invoices.Select(i => i.ID));
            var contractNoNorm = (contractNo ?? "").Trim();

            var customerPayQuery = db.FIN_TRANSACTIONS
                .Where(t => t.CounterpartyType == 1 && t.CounterpartyID == customerId && t.TxnDate <= asOfDate
                    && (t.IsDeleted == null || t.IsDeleted == false) && t.Direction == 1);
            if (openingDate.HasValue)
            {
                var od = openingDate.Value;
                customerPayQuery = customerPayQuery.Where(t => t.TxnDate > od);
            }
            var payCandidates = customerPayQuery.OrderBy(t => t.TxnDate).ToList();

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
            var opening = openingDate.HasValue
                ? GetOpeningContractBalance(db, openingDate.Value, 1, customerId, contractId, contractNo)
                : null;

            if (opening != null && opening.Amount != 0)
            {
                allItems.Add((openingDate.Value.Date, "open",
                    "Opening balansdan boshlangan haqqimiz",
                    opening.Amount, 0, 0, opening.Amount));
            }

            foreach (var inv in invoices)
            {
                var invoiceAmountWithVat = inv.TotalWithVAT ?? inv.Amount;
                allItems.Add((inv.InvoiceDate, "inv",
                    $"Sotuv invoysi №{inv.InvoiceNo} sanasi {inv.InvoiceDate:dd.MM.yyyy}",
                    invoiceAmountWithVat, 0, 0, invoiceAmountWithVat));
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

            foreach (var item in allItems.OrderBy(x => x.dt).ThenBy(x => x.type == "open" ? 0 : x.type == "inv" ? 1 : x.type == "pay" ? 2 : 3))
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

            var totalInv = invoices.Sum(i => i.TotalWithVAT ?? i.Amount);
            if (opening != null) totalInv += opening.Amount;
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

        private class OpeningContractBalanceLite
        {
            public decimal Amount { get; set; }
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

            // Foydalanuvchi qarori (2026-10-18): ta'minotchi/mijoz kabi — ekspeditor opening'i (BalanceType 4) bo'lsa,
            // shu sanadagi qoldiq boshlang'ich qator, keyin faqat opening sanasidan keyingi yo'l varaqalari va to'lovlar.
            var openingDate = GetLatestOpeningDate(db, asOfDate, 4);
            var contractWaybills = db.F_WAYBILLS
                .Where(w => w.ForwarderID == forwarderId && w.ContractID == contractId && w.IssueDate <= asOfDate && (w.IsDeleted == null || w.IsDeleted == false) && w.Amount.HasValue)
                .OrderBy(w => w.IssueDate)
                .Select(w => new { w.ID, w.WaybillNo, w.IssueDate, w.Amount })
                .ToList();
            var waybills = openingDate.HasValue
                ? contractWaybills.Where(w => w.IssueDate > openingDate.Value).ToList()
                : contractWaybills;

            // To'lovlar shartnomaning barcha yo'l varaqalari bo'yicha (opening'gacha chiqarilgan varaqaga keyin to'langani ham)
            var wbIds = new HashSet<int>(contractWaybills.Select(w => w.ID));

            var paymentQuery = db.F_WAYBILL_PAYMENTS
                .Where(p => p.ForwarderID == forwarderId && wbIds.Contains(p.WaybillID) && (p.PaymentDate ?? DateTime.MinValue) <= asOfDate && !p.IsDeleted);
            if (openingDate.HasValue)
            {
                // Sanasiz to'lov opening'ga kirgan (AutoFill ham shunday hisoblaydi)
                var od = openingDate.Value;
                paymentQuery = paymentQuery.Where(p => p.PaymentDate.HasValue && p.PaymentDate.Value > od);
            }
            var payments = paymentQuery
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
                    && (!openingDate.HasValue || t.TxnDate > openingDate.Value)
                    && (t.SourceDocType == null || t.SourceDocType != "WaybillPayment")
                    && (string.IsNullOrEmpty(contractNoNorm)
                        || (!string.IsNullOrEmpty(t.ContractNo)
                            && t.ContractNo.Trim().Equals(contractNoNorm, StringComparison.OrdinalIgnoreCase))))
                .OrderBy(t => t.TxnDate)
                .Select(t => new { t.ID, t.TxnDate, t.Amount, t.Purpose })
                .ToList();

            var allItems = new List<(DateTime dt, string type, string content, decimal p1Dr, decimal p1Cr, decimal p2Dr, decimal p2Cr)>();
            var opening = openingDate.HasValue
                ? GetOpeningContractBalance(db, openingDate.Value, 4, forwarderId, contractId, contractNo)
                : null;
            if (opening != null && opening.Amount != 0)
            {
                allItems.Add((openingDate.Value.Date, "open",
                    "Opening balansdan boshlangan qarzdorlik",
                    0, opening.Amount, opening.Amount, 0));
            }

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

            foreach (var item in allItems.OrderBy(x => x.dt).ThenBy(x => x.type == "open" ? -1 : x.type == "wb" ? 0 : (x.type == "pay" || x.type == "fin") ? 1 : 2))
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
            if (opening != null) totalWb += opening.Amount;
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
