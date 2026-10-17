using System;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.Services.Finance;
using tahsinERP.Controllers;

namespace tahsinERP.Services
{
    /// <summary>
    /// Service for automatically posting financial transactions from operational documents
    /// </summary>
    public static class FinancePostingService
    {
        /// <summary>
        /// Post transaction from Sales Invoice
        /// </summary>
        public static void PostSalesInvoice(int invoiceID, DBTHSNEntities db, int userId)
        {
            var invoice = db.SALES_INVOICES
                .Include(i => i.CUSTOMER)
                .Include(i => i.CURRENCy)
                .FirstOrDefault(i => i.ID == invoiceID);

            if (invoice == null) return;

            // Check if already posted
            var existing = db.FIN_TRANSACTIONS
                .FirstOrDefault(t => t.SourceDocType == "SalesInvoice" && t.SourceDocID == invoiceID && t.IsDeleted != true);

            // Already posted bo'lsa, mavjud yozuvni yangilaymiz (edit oqimi uchun)

            // Find or create account for receivables
            var currencyId = invoice.CurrencyID ?? 1;
            var receivableCode = "RECEIVABLES_" + currencyId;

            // 1) Preferred: deterministic code-based lookup
            var account = db.FIN_ACCOUNTS.FirstOrDefault(a => a.Code == receivableCode);

            // 2) Fallback: legacy name-based lookup for old data
            if (account == null)
            {
                account = db.FIN_ACCOUNTS
                    .FirstOrDefault(a =>
                        a.IsActive &&
                        a.CurrencyID == currencyId &&
                        !a.IsCash &&
                        !a.IsBank &&
                        (a.Name.Contains("Debitorlar") || a.Name.Contains("Receivable")));
            }

            if (account == null)
            {
                account = new FIN_ACCOUNTS
                {
                    Code = receivableCode,
                    Name = "Debitorlar qarzi",
                    CurrencyID = currencyId,
                    IsCash = false,
                    IsBank = false,
                    IsActive = true,
                    CreatedByUserID = userId,
                    CreatedDT = DateTime.Now
                };
                db.FIN_ACCOUNTS.Add(account);
                db.SaveChanges();
            }
            else if (!account.IsActive)
            {
                account.IsActive = true;
                if (account.CurrencyID <= 0)
                {
                    account.CurrencyID = currencyId;
                }
                db.SaveChanges();
            }

            // Find sales revenue category
            var category = db.FIN_CATEGORIES
                .FirstOrDefault(c => c.Name.Contains("Sotuv") && c.Type == 1 && c.IsActive);

            if (category == null)
            {
                category = new FIN_CATEGORIES
                {
                    Name = "Sotuv daromadi",
                    Type = 1, // Income
                    IsActive = true,
                    SortOrder = 1
                };
                db.FIN_CATEGORIES.Add(category);
                db.SaveChanges();
            }

            var transaction = existing ?? new FIN_TRANSACTIONS
            {
                SourceDocType = "SalesInvoice",
                SourceDocID = (int)invoice.ID, // SALES_INVOICES.ID is long
                CreatedByUserID = userId,
                CreatedDT = DateTime.Now
            };

            transaction.TxnDate = invoice.InvoiceDate;
            transaction.AccountID = account.ID;
            transaction.CategoryID = category.ID;
            transaction.Direction = 1; // In
            transaction.Amount = CounterpartyBalanceService.SalesInvoiceAmount(invoice); // QQS bilan (AR bilan bir xil, FIN-B08)
            transaction.CurrencyID = currencyId;
            transaction.CounterpartyType = 1; // Customer
            transaction.CounterpartyID = invoice.CustomerID;
            transaction.CounterpartyName = invoice.CUSTOMER?.Name;
            transaction.Purpose = $"Sotuv invoysi #{invoice.InvoiceNo}";
            transaction.IsDeleted = false;

            if (existing == null)
            {
                db.FIN_TRANSACTIONS.Add(transaction);
            }
            db.SaveChanges();

            LogHelper.LogToDatabase("System", "FinancePosting", "PostSalesInvoice", $"Sotuv invoysi tranzaksiyaga kiritildi: {invoiceID}");
        }

        /// <summary>
        /// Post transaction from Purchase Invoice
        /// </summary>
        public static void PostPurchaseInvoice(int invoiceID, DBTHSNEntities db, int userId)
        {
            var invoice = db.P_INVOICES
                .Include(i => i.SUPPLIER)
                .Include(i => i.CURRENCy)
                .FirstOrDefault(i => i.ID == invoiceID);

            if (invoice == null) return;

            // Check if already posted
            var existing = db.FIN_TRANSACTIONS
                .FirstOrDefault(t => t.SourceDocType == "PurchaseInvoice" && t.SourceDocID == invoiceID && t.IsDeleted != true);

            // Already posted bo'lsa, mavjud yozuvni yangilaymiz (edit oqimi uchun)

            // Find or create account for payables
            var account = db.FIN_ACCOUNTS
                .FirstOrDefault(a => (a.Name.Contains("Kreditorlar") || a.Name.Contains("Creditor")) && a.IsActive && 
                    (invoice.CurrencyID.HasValue ? a.CurrencyID == invoice.CurrencyID.Value : true));

            if (account == null && invoice.CurrencyID.HasValue)
            {
                account = new FIN_ACCOUNTS
                {
                    Code = "PAYABLES_" + invoice.CurrencyID.Value,
                    Name = "Kreditorlar qarzi",
                    CurrencyID = invoice.CurrencyID.Value,
                    IsCash = false,
                    IsBank = false,
                    IsActive = true,
                    CreatedByUserID = userId,
                    CreatedDT = DateTime.Now
                };
                db.FIN_ACCOUNTS.Add(account);
                db.SaveChanges();
            }
            
            if (account == null) return; // Cannot proceed without account

            // Find purchase expense category
            var category = db.FIN_CATEGORIES
                .FirstOrDefault(c => c.Name.Contains("Xarid") && c.Type == 2 && c.IsActive);

            if (category == null)
            {
                category = new FIN_CATEGORIES
                {
                    Name = "Xarid xarajatlari",
                    Type = 2, // Expense
                    IsActive = true,
                    SortOrder = 1
                };
                db.FIN_CATEGORIES.Add(category);
                db.SaveChanges();
            }

            var transaction = existing ?? new FIN_TRANSACTIONS
            {
                SourceDocType = "PurchaseInvoice",
                SourceDocID = invoiceID,
                CreatedByUserID = userId,
                CreatedDT = DateTime.Now
            };

            transaction.TxnDate = invoice.InvoiceDate;
            transaction.AccountID = account.ID;
            transaction.CategoryID = category.ID;
            transaction.Direction = 2; // Out
            // Yagona qoida (FIN-B08): kirim (INCOMES.TotalPrice) bo'lsa shu, aks holda P_INVOICES.Amount
            transaction.Amount = CounterpartyBalanceService.PurchaseInvoiceAmounts(db, db.P_INVOICES.Where(i => i.ID == invoiceID))
                .TryGetValue(invoiceID, out var purchaseAmount) ? purchaseAmount : 0m;
            transaction.CurrencyID = invoice.CurrencyID ?? 1;
            transaction.CounterpartyType = 2; // Supplier
            transaction.CounterpartyID = invoice.SupplierID;
            transaction.CounterpartyName = invoice.SUPPLIER?.Name;
            transaction.Purpose = $"Xarid invoysi #{invoice.InvoiceNo}";
            transaction.IsDeleted = false;

            if (existing == null)
            {
                db.FIN_TRANSACTIONS.Add(transaction);
            }
            db.SaveChanges();

            LogHelper.LogToDatabase("System", "FinancePosting", "PostPurchaseInvoice", $"Xarid invoysi tranzaksiyaga kiritildi: {invoiceID}");
        }

        /// <summary>
        /// Post transaction from Waybill Payment
        /// </summary>
        /// <summary>
        /// Yo'l varaqasi to'lovini FIN_TRANSACTIONS'ga chiqim sifatida yozadi.
        /// true — moliya yozuvi bor (yangi yozildi yoki avval yozilgan); false — to'lov topilmadi
        /// yoki mos bank hisobi yo'q (yozilmadi). SaveChanges chaqiruvchining tranzaksiyasi ichida bajariladi.
        /// </summary>
        public static bool PostWaybillPayment(int paymentID, int? accountID, DBTHSNEntities db, int userId)
        {
            var payment = db.F_WAYBILL_PAYMENTS
                .Include(p => p.F_WAYBILLS)
                .Include(p => p.FORWARDER)
                .Include(p => p.CURRENCy)
                .FirstOrDefault(p => p.ID == paymentID);

            if (payment == null) return false;

            // Check if already posted
            var existing = db.FIN_TRANSACTIONS
                .FirstOrDefault(t => t.SourceDocType == WaybillLedgerService.FinSourceDocType && t.SourceDocID == paymentID && t.IsDeleted != true);

            if (existing != null) return true;

            // Find bank account - use provided accountID or find default bank account
            FIN_ACCOUNTS account = null;
            if (accountID.HasValue)
            {
                account = db.FIN_ACCOUNTS.FirstOrDefault(a => a.ID == accountID.Value && a.IsActive && a.IsBank);
            }
            
            if (account == null)
            {
                // Default only bank account (hisob-raqam), not cash
                account = db.FIN_ACCOUNTS
                    .FirstOrDefault(a => a.IsBank && a.IsActive &&
                        (payment.CurrencyID.HasValue ? a.CurrencyID == payment.CurrencyID.Value : true));
            }

            if (account == null) 
            {
                LogHelper.LogToDatabase("System", "FinancePosting", "PostWaybillPayment",
                    $"Yo'l varaqasi to'lovi (ID: {paymentID}) uchun mos hisob topilmadi.");
                return false; // No account found
            }

            // Find transport expense category (material vs finished product)
            var category = ResolveLogisticsCategory(payment.F_WAYBILLS, db);

            var linkedWaybillNos = (from s in db.F_WAYBILL_PAYMENT_SPLITS
                                    join w in db.F_WAYBILLS on s.WaybillID equals w.ID
                                    where s.PayerType == WaybillLedgerService.SplitPayerType
                                          && s.PayerID == paymentID
                                    select w.WaybillNo).Distinct().ToList();
            if (!linkedWaybillNos.Any() && payment.F_WAYBILLS != null)
            {
                linkedWaybillNos.Add(payment.F_WAYBILLS.WaybillNo);
            }
            var waybillNoText = string.Join(", ", linkedWaybillNos.Where(x => !string.IsNullOrWhiteSpace(x)));

            // Create transaction
            var transaction = new FIN_TRANSACTIONS
            {
                TxnDate = payment.PaymentDate ?? DateTime.Now,
                AccountID = account.ID,
                CategoryID = category.ID,
                Direction = WaybillLedgerService.FinDirectionOut,
                Amount = (decimal)payment.Payment_Amount,
                CurrencyID = payment.CurrencyID ?? 1,
                CounterpartyType = WaybillLedgerService.FinCounterpartyForwarder,
                CounterpartyID = payment.ForwarderID,
                CounterpartyName = payment.FORWARDER?.ForwarderName,
                Purpose = $"Yo'l varaqasi bo'yicha yuk tashish to'lovi: {waybillNoText}",
                SourceDocType = WaybillLedgerService.FinSourceDocType,
                SourceDocID = paymentID,
                CreatedByUserID = userId,
                CreatedDT = DateTime.Now
            };

            db.FIN_TRANSACTIONS.Add(transaction);
            db.SaveChanges();

            LogHelper.LogToDatabase("System", "FinancePosting", "PostWaybillPayment", $"Yo'l varaqasi to'lovi tranzaksiyaga kiritildi: {paymentID}");
            return true;
        }

        private static FIN_CATEGORIES ResolveLogisticsCategory(F_WAYBILLS waybill, DBTHSNEntities db)
        {
            var isFinishedGoods = waybill?.SalesInvoiceID.HasValue == true ||
                                  waybill?.SalesOrderID.HasValue == true ||
                                  waybill?.TTNKID.HasValue == true;

            var preferredNames = isFinishedGoods
                ? new[] { "T.Tash" }
                : new[] { "M.Tash" };

            var category = db.FIN_CATEGORIES
                .FirstOrDefault(c => preferredNames.Contains(c.Name) && c.Type == 2 && c.IsActive);

            if (category == null)
            {
                category = new FIN_CATEGORIES
                {
                    Name = preferredNames[0],
                    Type = 2, // Expense
                    IsActive = true,
                    SortOrder = 10
                };
                db.FIN_CATEGORIES.Add(category);
                db.SaveChanges();
            }

            return category;
        }

        public static void RemovePurchaseInvoicePosting(int invoiceID, DBTHSNEntities db)
        {
            var txns = db.FIN_TRANSACTIONS
                .Where(t => t.SourceDocType == "PurchaseInvoice" && t.SourceDocID == invoiceID && (t.IsDeleted == null || t.IsDeleted == false))
                .ToList();

            if (!txns.Any())
            {
                return;
            }

            foreach (var t in txns)
            {
                t.IsDeleted = true;
            }
            db.SaveChanges();
        }

        public static void RemoveSalesInvoicePosting(int invoiceID, DBTHSNEntities db)
        {
            var txns = db.FIN_TRANSACTIONS
                .Where(t => t.SourceDocType == "SalesInvoice" && t.SourceDocID == invoiceID && (t.IsDeleted == null || t.IsDeleted == false))
                .ToList();

            if (!txns.Any())
            {
                return;
            }

            foreach (var t in txns)
            {
                t.IsDeleted = true;
            }
            db.SaveChanges();
        }
    }
}
