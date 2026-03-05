using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Services
{
    /// <summary>
    /// Akt-sverki (O'zaro hisob-kitoblar akti) uchun xizmat - barcha content o'zbekcha
    /// </summary>
    public static class ReconciliationService
    {
        public static ReconciliationViewModel BuildSupplierReconciliation(DBTHSNEntities db, int supplierId, DateTime asOfDate, string companyName, string supplierName)
        {
            var vm = new ReconciliationViewModel
            {
                Party1Name = companyName ?? "Bizning korxona",
                Party2Name = supplierName,
                AsOfDate = asOfDate,
                ReportDate = DateTime.Now.ToString("dd.MM.yyyy"),
                ReconciliationType = "Supplier",
                CounterpartyId = supplierId,
                Rows = new List<ReconciliationRowViewModel>(),
                AgreementInfo = $"Xarid-xo'jalik faoliyati bo'yicha o'zaro hisob-kitoblar"
            };

            var invoices = db.P_INVOICES
                .Where(i => i.SupplierID == supplierId && i.InvoiceDate <= asOfDate && (i.IsDeleted == null || i.IsDeleted == false))
                .OrderBy(i => i.InvoiceDate)
                .Select(i => new { i.ID, i.InvoiceNo, i.InvoiceDate, i.Amount })
                .ToList();

            var payments = db.FIN_TRANSACTIONS
                .Where(t => t.CounterpartyType == 2 && t.CounterpartyID == supplierId && t.TxnDate <= asOfDate
                    && (t.IsDeleted == null || t.IsDeleted == false) && t.Direction == 2)
                .OrderBy(t => t.TxnDate)
                .Select(t => new { t.ID, t.TxnDate, t.Amount, t.Purpose, t.SourceDocType })
                .ToList();

            decimal balance1Dr = 0, balance1Cr = 0, balance2Dr = 0, balance2Cr = 0;

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
                var purpose = !string.IsNullOrEmpty(pay.Purpose) ? pay.Purpose : $"To'lov #{pay.ID}";
                allItems.Add((pay.TxnDate, "pay",
                    $"Bank to'lovi / To'lov №{pay.ID} sanasi {pay.TxnDate:dd.MM.yyyy}",
                    pay.Amount, 0, 0, pay.Amount));
            }

            var ordered = allItems.OrderBy(x => x.dt).ToList();

            foreach (var item in ordered)
            {
                vm.Rows.Add(new ReconciliationRowViewModel
                {
                    Content = item.content,
                    Date = item.dt,
                    Party1Debit = item.p1Dr,
                    Party1Credit = item.p1Cr,
                    Party2Debit = item.p2Dr,
                    Party2Credit = item.p2Cr
                });
                balance1Dr += item.p1Dr - item.p1Cr;
                balance1Cr += item.p1Cr - item.p1Dr;
                balance2Dr += item.p2Dr - item.p2Cr;
                balance2Cr += item.p2Cr - item.p2Dr;
            }

            var totalInv = invoices.Sum(i => Convert.ToDecimal(i.Amount));
            var totalPay = payments.Sum(p => p.Amount);
            vm.Party1DebitTotal = payments.Sum(p => p.Amount);
            vm.Party1CreditTotal = totalInv;
            vm.Party2DebitTotal = totalInv;
            vm.Party2CreditTotal = totalPay;
            vm.FinalBalance = totalInv - totalPay;

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

        public static ReconciliationViewModel BuildCustomerReconciliation(DBTHSNEntities db, int customerId, DateTime asOfDate, string companyName, string customerName)
        {
            var vm = new ReconciliationViewModel
            {
                Party1Name = companyName ?? "Bizning korxona",
                Party2Name = customerName,
                AsOfDate = asOfDate,
                ReportDate = DateTime.Now.ToString("dd.MM.yyyy"),
                ReconciliationType = "Customer",
                CounterpartyId = customerId,
                Rows = new List<ReconciliationRowViewModel>(),
                AgreementInfo = $"Sotuv faoliyati bo'yicha o'zaro hisob-kitoblar"
            };

            var invoices = db.SALES_INVOICES
                .Where(i => i.CustomerID == customerId && i.InvoiceDate <= asOfDate)
                .OrderBy(i => i.InvoiceDate)
                .Select(i => new { i.ID, i.InvoiceNo, i.InvoiceDate, Amount = i.TotalAmount })
                .ToList();

            var payments = db.FIN_TRANSACTIONS
                .Where(t => t.CounterpartyType == 1 && t.CounterpartyID == customerId && t.TxnDate <= asOfDate
                    && (t.IsDeleted == null || t.IsDeleted == false) && t.Direction == 1)
                .OrderBy(t => t.TxnDate)
                .Select(t => new { t.ID, t.TxnDate, t.Amount, t.Purpose })
                .ToList();

            var allItems = new List<(DateTime dt, string content, decimal p1Dr, decimal p1Cr, decimal p2Dr, decimal p2Cr)>();

            foreach (var inv in invoices)
            {
                allItems.Add((inv.InvoiceDate,
                    $"Sotuv invoysi №{inv.InvoiceNo} sanasi {inv.InvoiceDate:dd.MM.yyyy}",
                    inv.Amount, 0, 0, inv.Amount));
            }
            foreach (var pay in payments)
            {
                var purpose = !string.IsNullOrEmpty(pay.Purpose) ? pay.Purpose : $"To'lov #{pay.ID}";
                allItems.Add((pay.TxnDate,
                    $"Mijoz to'lovi №{pay.ID} sanasi {pay.TxnDate:dd.MM.yyyy}",
                    0, pay.Amount, pay.Amount, 0));
            }

            foreach (var item in allItems.OrderBy(x => x.dt))
            {
                vm.Rows.Add(new ReconciliationRowViewModel
                {
                    Content = item.content,
                    Date = item.dt,
                    Party1Debit = item.p1Dr,
                    Party1Credit = item.p1Cr,
                    Party2Debit = item.p2Dr,
                    Party2Credit = item.p2Cr
                });
            }

            var totalInv = invoices.Sum(i => i.Amount);
            var totalPay = payments.Sum(p => p.Amount);
            vm.Party1DebitTotal = totalInv;
            vm.Party1CreditTotal = totalPay;
            vm.Party2DebitTotal = totalPay;
            vm.Party2CreditTotal = totalInv;
            vm.FinalBalance = totalInv - totalPay;

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

        public static ReconciliationViewModel BuildForwarderReconciliation(DBTHSNEntities db, int forwarderId, DateTime asOfDate, string companyName, string forwarderName)
        {
            var vm = new ReconciliationViewModel
            {
                Party1Name = companyName ?? "Bizning korxona",
                Party2Name = forwarderName,
                AsOfDate = asOfDate,
                ReportDate = DateTime.Now.ToString("dd.MM.yyyy"),
                ReconciliationType = "Forwarder",
                CounterpartyId = forwarderId,
                Rows = new List<ReconciliationRowViewModel>(),
                AgreementInfo = $"Yuk tashish xizmatlari bo'yicha o'zaro hisob-kitoblar"
            };

            var waybills = db.F_WAYBILLS
                .Where(w => w.ForwarderID == forwarderId && w.IssueDate <= asOfDate && (w.IsDeleted == null || w.IsDeleted == false) && w.Amount.HasValue)
                .OrderBy(w => w.IssueDate)
                .Select(w => new { w.ID, w.WaybillNo, w.IssueDate, w.Amount })
                .ToList();

            var payments = db.F_WAYBILL_PAYMENTS
                .Where(p => p.ForwarderID == forwarderId && (p.PaymentDate ?? DateTime.MinValue) <= asOfDate && !p.IsDeleted)
                .OrderBy(p => p.PaymentDate)
                .Select(p => new { p.ID, p.PaymentDate, p.Payment_Amount, p.WaybillID })
                .ToList();

            var allItems = new List<(DateTime dt, string content, decimal p1Dr, decimal p1Cr, decimal p2Dr, decimal p2Cr)>();

            foreach (var wb in waybills)
            {
                var amt = Convert.ToDecimal(wb.Amount ?? 0);
                allItems.Add((wb.IssueDate ?? DateTime.MinValue,
                    $"Yo'l varaqasi №{wb.WaybillNo} sanasi {(wb.IssueDate?.ToString("dd.MM.yyyy") ?? "")}",
                    0, amt, amt, 0));
            }
            foreach (var pay in payments)
            {
                var amt = Convert.ToDecimal(pay.Payment_Amount);
                allItems.Add((pay.PaymentDate ?? DateTime.MinValue,
                    $"Yuk tashish to'lovi №{pay.ID} sanasi {(pay.PaymentDate?.ToString("dd.MM.yyyy") ?? "")}",
                    amt, 0, 0, amt));
            }

            foreach (var item in allItems.OrderBy(x => x.dt))
            {
                vm.Rows.Add(new ReconciliationRowViewModel
                {
                    Content = item.content,
                    Date = item.dt,
                    Party1Debit = item.p1Dr,
                    Party1Credit = item.p1Cr,
                    Party2Debit = item.p2Dr,
                    Party2Credit = item.p2Cr
                });
            }

            var totalWb = waybills.Sum(w => Convert.ToDecimal(w.Amount ?? 0));
            var totalPay = payments.Sum(p => Convert.ToDecimal(p.Payment_Amount));
            vm.Party1DebitTotal = totalPay;
            vm.Party1CreditTotal = totalWb;
            vm.Party2DebitTotal = totalWb;
            vm.Party2CreditTotal = totalPay;
            vm.FinalBalance = totalWb - totalPay;

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
