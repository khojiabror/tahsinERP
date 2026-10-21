using System;
using System.Collections.Generic;
using System.Linq;

namespace tahsinERP.Areas.Distribution.Services.Money
{
    /// <summary>Qarz muddatlari bo'yicha taqsimot (TZ 9.2: 7 kungacha, 8–30, 31–60, 60+).</summary>
    public sealed class AgingBuckets
    {
        public decimal NotDue { get; set; }       // muddati kelmagan
        public decimal Days1To7 { get; set; }
        public decimal Days8To30 { get; set; }
        public decimal Days31To60 { get; set; }
        public decimal Over60 { get; set; }
        public decimal Advance { get; set; }      // mijoz oldindan to'lagan (ortiqcha kredit)

        public decimal Overdue => Days1To7 + Days8To30 + Days31To60 + Over60;
        public decimal Total => NotDue + Overdue;
        public int MaxOverdueDays { get; set; }
    }

    public sealed class DebitItem
    {
        public DateTime Date { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal Amount { get; set; }
        public int? OrderID { get; set; }
        /// <summary>Debet yozilgan paytdagi agent (buyurtma agenti) — KPI jarimasi shu agentga.</summary>
        public int? AgentUserID { get; set; }
    }

    /// <summary>
    /// Qarz hisoblari (sof funksiyalar): kreditlar (to'lov, qaytarish, teskari yozuv) eng ESKI debetlarni yopadi (FIFO).
    /// </summary>
    public static class DebtMath
    {
        /// <summary>Kreditlar FIFO bo'yicha taqsimlangandan keyin har debetning yopilmagan qismi.</summary>
        public static List<(DebitItem Item, decimal Open)> OpenDebits(IEnumerable<DebitItem> debits, decimal totalCredits)
        {
            decimal left = Math.Max(0, totalCredits);
            var result = new List<(DebitItem, decimal)>();
            foreach (var d in debits.OrderBy(d => d.DueDate ?? d.Date).ThenBy(d => d.Date))
            {
                decimal covered = Math.Min(left, d.Amount);
                left -= covered;
                result.Add((d, d.Amount - covered));
            }
            return result;
        }

        public static AgingBuckets Aging(IEnumerable<DebitItem> debits, decimal totalCredits, DateTime today)
        {
            var list = debits.ToList();
            var buckets = new AgingBuckets { Advance = Math.Max(0, totalCredits - list.Sum(d => d.Amount)) };
            foreach (var (item, open) in OpenDebits(list, totalCredits))
            {
                if (open <= 0) continue;
                int days = item.DueDate.HasValue ? (int)(today.Date - item.DueDate.Value.Date).TotalDays : 0;
                if (days <= 0) buckets.NotDue += open;
                else if (days <= 7) buckets.Days1To7 += open;
                else if (days <= 30) buckets.Days8To30 += open;
                else if (days <= 60) buckets.Days31To60 += open;
                else buckets.Over60 += open;
                if (days > buckets.MaxOverdueDays) buckets.MaxOverdueDays = days;
            }
            return buckets;
        }

        /// <summary>
        /// Agentning muddati <paramref name="minDays"/> kundan ortiq o'tgan ochiq qarzi: FIFO taqsimotdan keyin qolgan debetlar, faqat
        /// debet yozilgan paytdagi agentniki (mijoz keyin boshqa agentga o'tsa ham jarima eski agentda qoladi) — 05_FINDINGS K3-36.
        /// </summary>
        public static decimal OverdueFor(IEnumerable<DebitItem> debits, decimal totalCredits, DateTime today, int minDays, int agentUserId)
        {
            return OpenDebits(debits, totalCredits)
                .Where(x => x.Open > 0 && x.Item.AgentUserID == agentUserId && x.Item.DueDate.HasValue
                            && (today.Date - x.Item.DueDate.Value.Date).TotalDays > minDays)
                .Sum(x => x.Open);
        }

        /// <summary>To'liq yopilgan buyurtmalar (barcha debetlari FIFO bo'yicha kredit bilan qoplangan).</summary>
        public static HashSet<int> SettledOrders(IEnumerable<DebitItem> debits, decimal totalCredits)
        {
            var open = OpenDebits(debits, totalCredits).Where(x => x.Item.OrderID.HasValue).ToList();
            return new HashSet<int>(open.GroupBy(x => x.Item.OrderID.Value)
                .Where(g => g.All(x => x.Open <= 0))
                .Select(g => g.Key));
        }
    }
}
