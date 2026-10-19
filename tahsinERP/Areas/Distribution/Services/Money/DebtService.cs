using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Orders;

namespace tahsinERP.Areas.Distribution.Services.Money
{
    /// <summary>Mijoz qarzi (CUSTOMER_LEDGER): FIFO taqsimot, muddatlar, buyurtmani yopish, akt sverki.</summary>
    public static class DebtService
    {
        public sealed class CustomerDebts
        {
            public List<DebitItem> Debits { get; set; }
            public decimal Credits { get; set; }
        }

        /// <summary>
        /// Teskari yozuvlar (X_REV) o'z asl yozuvi bilan birga chiqarib tashlanadi — shunda bekor qilingan jo'natish
        /// FIFO taqsimotni buzmaydi. Qolgan debetlar va kreditlar summasi.
        /// </summary>
        public static Dictionary<int, CustomerDebts> Load(DistributionDbContext db, IReadOnlyCollection<int> customerIds = null)
        {
            IQueryable<CustomerLedgerEntry> q = db.CustomerLedger.AsNoTracking();
            if (customerIds != null)
            {
                var ids = customerIds.ToList();
                q = q.Where(l => ids.Contains(l.CustomerID));
            }
            var rows = q.ToList();
            return rows.GroupBy(r => r.CustomerID).ToDictionary(g => g.Key, g => Net(g.ToList()));
        }

        public static CustomerDebts Net(List<CustomerLedgerEntry> rows)
        {
            var reversed = new HashSet<(string, int?)>(rows
                .Where(r => r.DocType.EndsWith(LedgerDocTypes.ReversalSuffix))
                .Select(r => (r.DocType.Substring(0, r.DocType.Length - LedgerDocTypes.ReversalSuffix.Length), r.DocID)));
            var live = rows.Where(r => !r.DocType.EndsWith(LedgerDocTypes.ReversalSuffix) && !reversed.Contains((r.DocType, r.DocID))).ToList();
            return new CustomerDebts
            {
                Debits = live.Where(r => r.Debit > 0)
                    .Select(r => new DebitItem { Date = r.EntryDate, DueDate = r.DueDate, Amount = r.Debit, OrderID = r.OrderID }).ToList(),
                Credits = live.Sum(r => r.Credit)
            };
        }

        public static AgingBuckets Aging(DistributionDbContext db, int customerId, DateTime today)
        {
            var debts = Load(db, new[] { customerId });
            return debts.TryGetValue(customerId, out var d) ? DebtMath.Aging(d.Debits, d.Credits, today) : new AgingBuckets();
        }

        /// <summary>
        /// Qarzi to'liq yopilgan buyurtmalarni "yopildi" holatiga o'tkazadi (jo'natilgan/yetkazilgan; qisman jo'natilgan — yo'q).
        /// To'lov, qaytarish va jo'natishdan keyin chaqiriladi. Qaytaradi: yopilganlar soni.
        /// </summary>
        public static int SettleOrders(DistributionDbContext db, int customerId, int userId, DateTime now)
        {
            var debts = Load(db, new[] { customerId });
            if (!debts.TryGetValue(customerId, out var d)) return 0;
            var settled = DebtMath.SettledOrders(d.Debits, d.Credits);
            if (settled.Count == 0) return 0;

            // byte[] EF6'da binar qiymat sifatida ko'riladi — Contains o'rniga aniq shart
            byte shipped = (byte)OrderStatus.Shipped, delivered = (byte)OrderStatus.Delivered;
            var ids = settled.ToList();
            var orders = db.Orders.Where(o => ids.Contains(o.ID) && (o.OrderStatus == shipped || o.OrderStatus == delivered)).ToList();
            foreach (var order in orders)
            {
                var from = (OrderStatus)order.OrderStatus;
                order.OrderStatus = (byte)OrderStatus.Closed;
                db.OrderStatusLog.Add(new DstOrderStatusLog
                {
                    OrderID = order.ID, FromStatus = (byte)from, ToStatus = (byte)OrderStatus.Closed, UserID = userId, At = now
                });
            }
            return orders.Count;
        }

        public sealed class StatementRow
        {
            public DateTime Date { get; set; }
            public string DocType { get; set; }
            public string Doc { get; set; }
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
            public decimal Balance { get; set; }
            public string Note { get; set; }
        }

        public sealed class Statement
        {
            public decimal Opening { get; set; }
            public List<StatementRow> Rows { get; set; }
            public decimal TotalDebit => Rows.Sum(r => r.Debit);
            public decimal TotalCredit => Rows.Sum(r => r.Credit);
            public decimal Closing => Opening + TotalDebit - TotalCredit;
        }

        /// <summary>Akt sverki: davr boshidagi qoldiq, davrdagi yozuvlar (teskari yozuvlar ham ko'rinadi), oxirgi qoldiq.</summary>
        public static Statement BuildStatement(DistributionDbContext db, int customerId, DateTime from, DateTime to)
        {
            var start = from.Date;
            var end = to.Date.AddDays(1);
            var opening = db.CustomerLedger.Where(l => l.CustomerID == customerId && l.EntryDate < start)
                .Select(l => (decimal?)(l.Debit - l.Credit)).Sum() ?? 0;
            var rows = db.CustomerLedger.AsNoTracking()
                .Where(l => l.CustomerID == customerId && l.EntryDate >= start && l.EntryDate < end)
                .OrderBy(l => l.EntryDate).ThenBy(l => l.ID).ToList();
            decimal balance = opening;
            return new Statement
            {
                Opening = opening,
                Rows = rows.Select(r =>
                {
                    balance += r.Debit - r.Credit;
                    return new StatementRow
                    {
                        Date = r.EntryDate, DocType = r.DocType, Doc = r.Note, Debit = r.Debit, Credit = r.Credit, Balance = balance
                    };
                }).ToList()
            };
        }

        public static CustomerLedgerEntry Credit(DistributionDbContext db, int customerId, string docType, int docId, int? orderId,
            decimal amount, DateTime at, int? agentUserId, int? userId, string note)
        {
            var entry = new CustomerLedgerEntry
            {
                CustomerID = customerId, EntryDate = at, DocType = docType, DocID = docId, OrderID = orderId, Credit = amount,
                AgentUserID = agentUserId, CreatedBy = userId, Note = note
            };
            db.CustomerLedger.Add(entry);
            return entry;
        }

        /// <summary>Hujjat bekor qilinganda teskari yozuv (asl yozuvga qarama-qarshi).</summary>
        public static void Reverse(DistributionDbContext db, string docType, int docId, DateTime at, int? userId, string note)
        {
            var originals = db.CustomerLedger.Where(l => l.DocType == docType && l.DocID == docId).ToList();
            foreach (var o in originals)
            {
                db.CustomerLedger.Add(new CustomerLedgerEntry
                {
                    CustomerID = o.CustomerID, EntryDate = at, DocType = LedgerDocTypes.ReversalOf(docType), DocID = docId, OrderID = o.OrderID,
                    Debit = o.Credit, Credit = o.Debit, AgentUserID = o.AgentUserID, CreatedBy = userId, Note = note
                });
            }
        }
    }
}
