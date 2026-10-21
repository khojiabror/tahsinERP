using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Common;

namespace tahsinERP.Areas.Distribution.Services.Money
{
    /// <summary>
    /// Moliya moduli (dbo.FIN_TRANSACTIONS) bilan bog'lanish. Faqat HAQIQIY pul yoziladi: mijoz to'lovi kassaga/hisobga
    /// tushganda — Direction=1, kontragent = mijoz, SALES kategoriyasi, SourceDocType='DstPayment' (mavjud moliya
    /// hisobotlari uni mijoz tushumi sifatida o'qiydi). Jo'natish moliyaga "mirror" qilinmaydi — u faqat dst ledgerda.
    /// </summary>
    public static class DstFinancePosting
    {
        public const string PaymentSource = tahsinERP.Services.Finance.FinQueries.DistributionPayment;
        public const string HandoverSource = "DstHandover";
        public const string CashShortageCode = "DST_CASH_DIFF";

        public static FIN_ACCOUNTS RequireAccount(DBTHSNEntities erp, int accountId)
        {
            var account = erp.FIN_ACCOUNTS.Find(accountId);
            if (account == null || account.IsDeleted == true || !account.IsActive)
                throw new DstValidationException(DstLocalization.T("Payment_AccountRequired"));
            var baseCurrency = CurrencyRateService.BaseCurrencyId();
            if (baseCurrency.HasValue && account.CurrencyID != baseCurrency.Value)
                throw new DstValidationException(DstLocalization.T("Payment_AccountNotSum"));
            return account;
        }

        public static int PostPayment(DBTHSNEntities erp, DstPayment payment, FIN_ACCOUNTS account, string customerName, int userId, DateTime at)
        {
            var category = FinancePostingService.EnsureSystemCategory(erp, SystemCodes.FinCategory.Sales, "Sotuv daromadi", 1);
            if (category.ID == 0) erp.SaveChanges();
            var tx = new FIN_TRANSACTIONS
            {
                TxnDate = at, AccountID = account.ID, CategoryID = category.ID, Direction = 1, Amount = payment.Amount,
                CurrencyID = account.CurrencyID, CounterpartyType = 1, CounterpartyID = payment.CustomerID, CounterpartyName = customerName,
                Purpose = "Distributsiya to'lovi " + payment.DocNo, SourceDocType = PaymentSource, SourceDocID = payment.ID,
                Comment = payment.Method, CreatedByUserID = userId, CreatedDT = at, IsDeleted = false
            };
            erp.FIN_TRANSACTIONS.Add(tx);
            erp.SaveChanges();
            return tx.ID;
        }

        /// <summary>FIN_TRANSACTIONS.CounterpartyType: xodim (HR_EMPLOYEES.ID) — Moliya uni ish haqi hisobida o'qiydi.</summary>
        public const byte CounterpartyEmployee = 6;

        /// <summary>
        /// Kassadagi farq (kamomad — chiqim, ortiqcha — kirim) — kassa qoldig'i haqiqiy pulga teng bo'lishi uchun.
        /// Kamomad agent HR xodimiga bog'langan bo'lsa (KPI sxemasidagi xodim) — kontragent = xodim (CounterpartyType 6):
        /// Moliya uni xodimga berilgan pul deb o'qiydi, ish haqi qarzidan ayriladi — agentning qarzi (05_FINDINGS K2-15).
        /// </summary>
        public static void PostHandoverDifference(DBTHSNEntities erp, CashHandover handover, FIN_ACCOUNTS account, string agentName, int? employeeId,
            int userId, DateTime at)
        {
            var diff = handover.Difference ?? 0;
            if (diff == 0) return;
            var category = FinancePostingService.EnsureSystemCategory(erp, CashShortageCode, "Agent kassa farqi", 2);
            if (category.ID == 0) erp.SaveChanges();
            bool toEmployee = diff < 0 && employeeId.HasValue;
            erp.FIN_TRANSACTIONS.Add(new FIN_TRANSACTIONS
            {
                TxnDate = at, AccountID = account.ID, CategoryID = category.ID, Direction = (byte)(diff > 0 ? 1 : 2), Amount = Math.Abs(diff),
                CurrencyID = account.CurrencyID, CounterpartyName = agentName,
                CounterpartyType = toEmployee ? CounterpartyEmployee : (byte?)null, CounterpartyID = toEmployee ? employeeId : null,
                Purpose = (diff > 0 ? "Ortiqcha: " : toEmployee ? "Kamomad (agent qarzi): " : "Kamomad: ") + handover.DocNo,
                SourceDocType = HandoverSource, SourceDocID = handover.ID,
                CreatedByUserID = userId, CreatedDT = at, IsDeleted = false
            });
            erp.SaveChanges();
        }

        /// <summary>Topshirish farqi yozuvini bekor qilish (topshirish bekor qilinganda).</summary>
        public static void RemoveHandoverDifference(DBTHSNEntities erp, int handoverId)
        {
            foreach (var tx in erp.FIN_TRANSACTIONS.Where(t => t.SourceDocType == HandoverSource && t.SourceDocID == handoverId && t.IsDeleted != true).ToList())
                tx.IsDeleted = true;
            erp.SaveChanges();
        }

        public static void RemovePosting(DBTHSNEntities erp, int? finTransactionId)
        {
            if (!finTransactionId.HasValue) return;
            var tx = erp.FIN_TRANSACTIONS.Find(finTransactionId.Value);
            if (tx != null) tx.IsDeleted = true;
            erp.SaveChanges();
        }
    }

    public sealed class PaymentInput
    {
        public int CustomerID { get; set; }
        public int? OrderID { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = PaymentMethods.Cash;
        /// <summary>Pul tushgan hisob. Bo'sh — naqd pul qabul qiluvchining (agentning) qo'lida.</summary>
        public int? FinAccountID { get; set; }
        public int? ReceivedByUserID { get; set; }
        public string Note { get; set; }
        public Guid? ClientRequestId { get; set; }
    }

    /// <summary>To'lovlar va agent naqdini kassaga topshirish. Chaqiruvchi UoW ochadi va Commit qiladi.</summary>
    public static class PaymentService
    {
        public const string PaymentPrefix = "DP";
        public const string HandoverPrefix = "DK";

        public static int Receive(DistributionUnitOfWork uow, PaymentInput input, int userId, string source, DateTime now)
        {
            var db = uow.Dst;
            if (input.ClientRequestId.HasValue)
            {
                var existing = db.Payments.Where(p => p.ClientRequestId == input.ClientRequestId).Select(p => (int?)p.ID).FirstOrDefault();
                if (existing.HasValue) return existing.Value;
            }
            if (input.Amount <= 0) throw new DstValidationException(DstLocalization.T("Payment_AmountPositive"));
            if (!PaymentMethods.All.Contains(input.Method)) throw new DstValidationException(DstLocalization.T("Payment_MethodRequired"));
            if (input.FinAccountID == null && !PaymentMethods.CanStayWithAgent(input.Method))
                throw new DstValidationException(DstLocalization.T("Payment_AccountRequired"));
            var customer = db.Customers.FirstOrDefault(c => c.ID == input.CustomerID && c.IsDeleted != true)
                           ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (db.CustomerProfiles.Find(input.CustomerID) == null)
                throw new DstValidationException(DstLocalization.T("Order_CustomerNotDistribution"));
            if (input.OrderID.HasValue && !db.Orders.Any(o => o.ID == input.OrderID && o.CustomerID == input.CustomerID))
                throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            FIN_ACCOUNTS account = input.FinAccountID.HasValue ? DstFinancePosting.RequireAccount(uow.Erp, input.FinAccountID.Value) : null;

            var payment = new DstPayment
            {
                DocNo = DstNumbering.Next(db, "D_PAYMENTS", PaymentPrefix, now), DocDate = now.Date, Status = DocumentStatus.Draft,
                CustomerID = input.CustomerID, OrderID = input.OrderID, Amount = Math.Round(input.Amount, 2), Method = input.Method,
                ReceivedByUserID = input.ReceivedByUserID ?? userId, FinAccountID = input.FinAccountID,
                Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(), Source = source ?? AuditActor.Web,
                ClientRequestId = input.ClientRequestId, CreatedBy = userId, CreatedAt = now
            };
            DocumentLifecycle.Post(payment, userId, now);
            db.Payments.Add(payment);
            db.SaveChanges();

            if (account != null)
                payment.FinTransactionID = DstFinancePosting.PostPayment(uow.Erp, payment, account, customer.Name, userId, now);

            var agent = db.Orders.Where(o => o.ID == input.OrderID).Select(o => o.AgentUserID).FirstOrDefault()
                        ?? db.CustomerProfiles.Where(p => p.CustomerID == input.CustomerID).Select(p => p.AgentUserID).FirstOrDefault();
            DebtService.Credit(db, input.CustomerID, LedgerDocTypes.Payment, payment.ID, input.OrderID, payment.Amount, now, agent, userId, payment.DocNo);
            db.SaveChanges();
            NotificationService.ToCustomer(db, input.CustomerID, NotificationService.PaymentReceived,
                l => DstLocalization.TFor(l, "Notify_PaymentReceived", payment.Amount.ToString("N0"), payment.DocNo,
                    LedgerService.CreditState(db, input.CustomerID, now).Balance.ToString("N0")), now);
            DebtService.SettleOrders(db, input.CustomerID, userId, now);
            db.SaveChanges();
            return payment.ID;
        }

        /// <summary>
        /// To'lovni bekor qilish (xato kiritilgan). Topshirish hujjatiga kirgan agent puli to'g'ridan-to'g'ri bekor qilinmaydi —
        /// avval topshirish bekor qilinadi (<see cref="CancelHandover"/>), to'lov yana agent qo'lida bo'ladi.
        /// </summary>
        public static void Cancel(DistributionUnitOfWork uow, int paymentId, int userId, string reason, DateTime now)
        {
            var db = uow.Dst;
            var payment = db.Payments.Find(paymentId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (payment.HandoverID.HasValue)
                throw new DstValidationException(DstLocalization.T("Payment_AlreadyHandedOver"));
            DocumentLifecycle.Cancel(payment, userId, reason, now);
            DstFinancePosting.RemovePosting(uow.Erp, payment.FinTransactionID);
            DebtService.Reverse(db, LedgerDocTypes.Payment, payment.ID, now, userId, payment.DocNo + ": " + payment.CancelReason);
            db.SaveChanges();
        }

        /// <summary>Agent qo'lidagi (kassaga topshirilmagan) naqd to'lovlar.</summary>
        public static List<DstPayment> OnHand(DistributionDbContext db, int agentUserId)
        {
            return db.Payments.Where(p => p.ReceivedByUserID == agentUserId && p.Status == DocumentStatus.Posted
                                          && p.FinAccountID == null && p.HandoverID == null)
                .OrderBy(p => p.ID).ToList();
        }

        /// <summary>Agent topshiradi (bot yoki web): qo'lidagi barcha naqd to'lovlar bir hujjatga bog'lanadi, kassir tasdiqlaguncha kutadi.</summary>
        public static int SubmitHandover(DistributionUnitOfWork uow, int agentUserId, int finAccountId, int userId, string note, DateTime now)
        {
            var db = uow.Dst;
            DstFinancePosting.RequireAccount(uow.Erp, finAccountId);
            var payments = OnHand(db, agentUserId);
            if (payments.Count == 0) throw new DstValidationException(DstLocalization.T("Handover_NothingOnHand"));
            if (db.CashHandovers.Any(h => h.AgentUserID == agentUserId && h.Status == DocumentStatus.Draft))
                throw new DstValidationException(DstLocalization.T("Handover_PendingExists"));

            var handover = new CashHandover
            {
                DocNo = DstNumbering.Next(db, "CASH_HANDOVERS", HandoverPrefix, now), DocDate = now.Date, Status = DocumentStatus.Draft,
                AgentUserID = agentUserId, FinAccountID = finAccountId, AmountDeclared = payments.Sum(p => p.Amount),
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(), CreatedBy = userId, CreatedAt = now
            };
            db.CashHandovers.Add(handover);
            db.SaveChanges();
            foreach (var p in payments) p.HandoverID = handover.ID;
            db.SaveChanges();
            return handover.ID;
        }

        /// <summary>
        /// Kassir qabul qiladi: sanalgan summa kiritiladi; har to'lov kassaga (FIN) yoziladi, farq alohida yozuv bilan.
        /// </summary>
        /// <param name="finAccountId">Kassir tanlagan kassa (bo'sh — agent topshirishda ko'rsatgani).</param>
        public static void ConfirmHandover(DistributionUnitOfWork uow, int handoverId, decimal amountReceived, int cashierUserId, DateTime now,
            int? finAccountId = null)
        {
            var db = uow.Dst;
            var handover = db.CashHandovers.Find(handoverId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (handover.Status != DocumentStatus.Draft) throw new DstValidationException(DstLocalization.T("Order_NotEditable"));
            if (amountReceived < 0) throw new DstValidationException(DstLocalization.T("Payment_AmountPositive"));
            if (finAccountId.HasValue) handover.FinAccountID = finAccountId.Value;
            var account = DstFinancePosting.RequireAccount(uow.Erp, handover.FinAccountID);

            handover.AmountReceived = Math.Round(amountReceived, 2);
            DocumentLifecycle.Post(handover, cashierUserId, now);
            var payments = db.Payments.Where(p => p.HandoverID == handoverId).ToList();
            var customerIds = payments.Select(p => p.CustomerID).Distinct().ToList();
            var names = db.Customers.Where(c => customerIds.Contains(c.ID)).ToDictionary(c => c.ID, c => c.Name);
            foreach (var p in payments)
            {
                p.FinAccountID = handover.FinAccountID;
                p.FinTransactionID = DstFinancePosting.PostPayment(uow.Erp, p, account, names.TryGetValue(p.CustomerID, out var n) ? n : null, cashierUserId, now);
            }
            var agentName = db.Users.Where(u => u.ID == handover.AgentUserID).Select(u => u.FullName ?? u.Uname).FirstOrDefault();
            var employeeId = db.KpiSchemes.Where(k => k.AgentUserID == handover.AgentUserID).Select(k => k.EmployeeID).FirstOrDefault();
            DstFinancePosting.PostHandoverDifference(uow.Erp, handover, account, agentName, employeeId, cashierUserId, now);
            db.SaveChanges();
        }

        /// <summary>
        /// Qabul qilingan topshirishni bekor qilish (kassir xato qildi yoki ichidagi to'lov xato): to'lovlarning moliya yozuvlari va
        /// farq yozuvi bekor qilinadi, to'lovlar yana agent qo'lida. Keyin xato to'lovni oddiy tartibda bekor qilish mumkin (K1-4).
        /// Kutilayotgan (qoralama) topshirish — rad etiladi.
        /// </summary>
        public static void CancelHandover(DistributionUnitOfWork uow, int handoverId, int userId, string reason, DateTime now)
        {
            var db = uow.Dst;
            var handover = db.CashHandovers.Find(handoverId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (handover.Status == DocumentStatus.Draft)
            {
                RejectHandover(uow, handoverId, userId, reason, now);
                return;
            }
            DocumentLifecycle.Cancel(handover, userId, reason, now);
            foreach (var p in db.Payments.Where(p => p.HandoverID == handoverId).ToList())
            {
                DstFinancePosting.RemovePosting(uow.Erp, p.FinTransactionID);
                p.FinTransactionID = null;
                p.FinAccountID = null;
                p.HandoverID = null;
            }
            DstFinancePosting.RemoveHandoverDifference(uow.Erp, handoverId);
            db.SaveChanges();
        }

        /// <summary>Kassir rad etadi: to'lovlar yana agent qo'lida.</summary>
        public static void RejectHandover(DistributionUnitOfWork uow, int handoverId, int userId, string reason, DateTime now)
        {
            var db = uow.Dst;
            var handover = db.CashHandovers.Find(handoverId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            if (handover.Status != DocumentStatus.Draft) throw new DstValidationException(DstLocalization.T("Order_NotEditable"));
            DocumentLifecycle.Cancel(handover, userId, reason, now);
            foreach (var p in db.Payments.Where(p => p.HandoverID == handoverId).ToList()) p.HandoverID = null;
            db.SaveChanges();
        }

        /// <summary>Web: agent kassaga keldi — topshirish va qabul bitta amalda.</summary>
        public static int AcceptCash(DistributionUnitOfWork uow, int agentUserId, int finAccountId, decimal amountReceived, int cashierUserId, string note, DateTime now)
        {
            var id = SubmitHandover(uow, agentUserId, finAccountId, cashierUserId, note, now);
            ConfirmHandover(uow, id, amountReceived, cashierUserId, now);
            return id;
        }
    }
}
