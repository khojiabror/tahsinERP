using System;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Models;
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
                .FirstOrDefault(t => t.SourceDocType == "SalesInvoice" && t.SourceDocID == invoiceID);

            if (existing != null) return; // Already posted

            // Find or create account for receivables
            var account = db.FIN_ACCOUNTS
                .FirstOrDefault(a => a.Name.Contains("Debitorlar") && a.IsActive && 
                    (invoice.CurrencyID.HasValue ? a.CurrencyID == invoice.CurrencyID.Value : true));

            if (account == null && invoice.CurrencyID.HasValue)
            {
                // Create default receivables account
                account = new FIN_ACCOUNTS
                {
                    Code = "RECEIVABLES_" + invoice.CurrencyID.Value,
                    Name = "Debitorlar qarzi",
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

            // Create transaction
            var transaction = new FIN_TRANSACTIONS
            {
                TxnDate = invoice.InvoiceDate,
                AccountID = account.ID,
                CategoryID = category.ID,
                Direction = 1, // In
                Amount = invoice.TotalAmount,
                CurrencyID = invoice.CurrencyID ?? 1, // Default currency if null
                CounterpartyType = 1, // Customer
                CounterpartyID = invoice.CustomerID,
                CounterpartyName = invoice.CUSTOMER?.Name,
                Purpose = $"Sotuv invoysi #{invoice.InvoiceNo}",
                SourceDocType = "SalesInvoice",
                SourceDocID = (int)invoice.ID, // SALES_INVOICES.ID is long
                CreatedByUserID = userId,
                CreatedDT = DateTime.Now
            };

            db.FIN_TRANSACTIONS.Add(transaction);
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
                .FirstOrDefault(t => t.SourceDocType == "PurchaseInvoice" && t.SourceDocID == invoiceID);

            if (existing != null) return;

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

            // Create transaction
            var transaction = new FIN_TRANSACTIONS
            {
                TxnDate = invoice.InvoiceDate,
                AccountID = account.ID,
                CategoryID = category.ID,
                Direction = 2, // Out
                Amount = (decimal)invoice.Amount, // P_INVOICES.Amount is double
                CurrencyID = invoice.CurrencyID ?? 1, // Default currency if null
                CounterpartyType = 2, // Supplier
                CounterpartyID = invoice.SupplierID,
                CounterpartyName = invoice.SUPPLIER?.Name,
                Purpose = $"Xarid invoysi #{invoice.InvoiceNo}",
                SourceDocType = "PurchaseInvoice",
                SourceDocID = invoiceID,
                CreatedByUserID = userId,
                CreatedDT = DateTime.Now
            };

            db.FIN_TRANSACTIONS.Add(transaction);
            db.SaveChanges();

            LogHelper.LogToDatabase("System", "FinancePosting", "PostPurchaseInvoice", $"Xarid invoysi tranzaksiyaga kiritildi: {invoiceID}");
        }

        /// <summary>
        /// Post transaction from Waybill Payment
        /// </summary>
        public static void PostWaybillPayment(int paymentID, int? accountID, DBTHSNEntities db, int userId)
        {
            var payment = db.F_WAYBILL_PAYMENTS
                .Include(p => p.F_WAYBILLS)
                .Include(p => p.FORWARDER)
                .Include(p => p.CURRENCy)
                .FirstOrDefault(p => p.ID == paymentID);

            if (payment == null) return;

            // Check if already posted
            var existing = db.FIN_TRANSACTIONS
                .FirstOrDefault(t => t.SourceDocType == "WaybillPayment" && t.SourceDocID == paymentID);

            if (existing != null) return;

            // Find cash/bank account - use provided accountID or find default
            FIN_ACCOUNTS account = null;
            if (accountID.HasValue)
            {
                account = db.FIN_ACCOUNTS.FirstOrDefault(a => a.ID == accountID.Value && a.IsActive);
            }
            
            if (account == null)
            {
                // Find default cash/bank account matching currency
                account = db.FIN_ACCOUNTS
                    .FirstOrDefault(a => (a.IsCash || a.IsBank) && a.IsActive && 
                        (payment.CurrencyID.HasValue ? a.CurrencyID == payment.CurrencyID.Value : true));
            }

            if (account == null) 
            {
                LogHelper.LogToDatabase("System", "FinancePosting", "PostWaybillPayment", 
                    $"No suitable account found for waybill payment {paymentID}");
                return; // No account found
            }

            // Find transport expense category
            var category = db.FIN_CATEGORIES
                .FirstOrDefault(c => (c.Name.Contains("Transport") || c.Name.Contains("Logistics")) && c.Type == 2 && c.IsActive);

            if (category == null)
            {
                category = new FIN_CATEGORIES
                {
                    Name = "Transport xarajatlari",
                    Type = 2, // Expense
                    IsActive = true,
                    SortOrder = 10
                };
                db.FIN_CATEGORIES.Add(category);
                db.SaveChanges();
            }

            // Create transaction
            var transaction = new FIN_TRANSACTIONS
            {
                TxnDate = payment.PaymentDate ?? DateTime.Now,
                AccountID = account.ID,
                CategoryID = category.ID,
                Direction = 2, // Out
                Amount = (decimal)payment.Payment_Amount,
                CurrencyID = payment.CurrencyID ?? 1,
                CounterpartyType = 4, // Forwarder (assuming 4 is forwarder type)
                CounterpartyID = payment.ForwarderID,
                CounterpartyName = payment.FORWARDER?.ForwarderName,
                Purpose = $"Logistics / freight payment for WaybillNo {payment.F_WAYBILLS?.WaybillNo}",
                SourceDocType = "WaybillPayment",
                SourceDocID = paymentID,
                CreatedByUserID = userId,
                CreatedDT = DateTime.Now
            };

            db.FIN_TRANSACTIONS.Add(transaction);
            db.SaveChanges();

            LogHelper.LogToDatabase("System", "FinancePosting", "PostWaybillPayment", $"Yo'l varaqasi to'lovi tranzaksiyaga kiritildi: {paymentID}");
        }
    }
}
