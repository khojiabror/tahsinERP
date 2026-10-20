using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Money;

namespace tahsinERP.Areas.Distribution.Services.Bot
{
    /// <summary>Rahbarga kunlik yakun (TZ 11.3): buyurtmalar, jo'natish, tushum, qarz, agentlar faolligi.</summary>
    public static class DailySummaryService
    {
        public sealed class Figures
        {
            public int Orders { get; set; }
            public decimal OrdersTotal { get; set; }
            public decimal Shipped { get; set; }
            public decimal Collected { get; set; }
            public decimal Debt { get; set; }
            public decimal Overdue { get; set; }
            public int AgentsActive { get; set; }
            public int AgentsTotal { get; set; }
            public string TopAgent { get; set; }
            public decimal TopAgentTotal { get; set; }
        }

        public static Figures Compute(DistributionDbContext db, DateTime date)
        {
            var from = date.Date;
            var to = from.AddDays(1);
            var orders = db.Orders.Where(o => o.CreatedAt >= from && o.CreatedAt < to && o.Status != DocumentStatus.Cancelled);
            var top = orders.Where(o => o.AgentUserID != null).GroupBy(o => o.AgentUserID)
                .Select(g => new { g.Key, Total = g.Sum(o => o.Total) }).OrderByDescending(x => x.Total).FirstOrDefault();
            var debts = DebtService.Load(db);
            decimal debt = 0, overdue = 0;
            foreach (var d in debts.Values)
            {
                var a = DebtMath.Aging(d.Debits, d.Credits, date);
                debt += a.Total;
                overdue += a.Overdue;
            }
            return new Figures
            {
                Orders = orders.Count(),
                OrdersTotal = orders.Sum(o => (decimal?)o.Total) ?? 0,
                Shipped = db.Shipments.Where(s => s.Status == DocumentStatus.Posted && s.PostedAt >= from && s.PostedAt < to).Sum(s => (decimal?)s.Total) ?? 0,
                Collected = db.Payments.Where(p => p.Status == DocumentStatus.Posted && p.CreatedAt >= from && p.CreatedAt < to).Sum(p => (decimal?)p.Amount) ?? 0,
                Debt = debt,
                Overdue = overdue,
                AgentsTotal = db.SalesAgents.Count(a => a.IsActive && !a.IsExpeditor),
                AgentsActive = db.AgentDays.Count(d => d.WorkDate == from),
                TopAgent = top == null ? null : db.Users.Where(u => u.ID == top.Key).Select(u => u.FullName ?? u.Uname).FirstOrDefault(),
                TopAgentTotal = top?.Total ?? 0
            };
        }

        public static List<NotificationService.PendingItem> ForManagers(DistributionDbContext db, DateTime now)
        {
            var managers = db.TelegramAccounts.Where(a => a.IsActive && a.Role == BotRoles.Manager && a.UserID != null).ToList();
            if (managers.Count == 0) return new List<NotificationService.PendingItem>();
            var f = Compute(db, now);
            return managers.Select(m =>
            {
                var lang = DstLocalization.LanguageOf(m.UserID.Value);
                var text = DstLocalization.TFor(lang, "Summary_Text", now.ToString("dd.MM.yyyy"), f.Orders, f.OrdersTotal.ToString("N0"),
                    f.Shipped.ToString("N0"), f.Collected.ToString("N0"), f.Debt.ToString("N0"), f.Overdue.ToString("N0"),
                    f.AgentsActive, f.AgentsTotal);
                if (f.TopAgent != null)
                    text += Environment.NewLine + DstLocalization.TFor(lang, "Summary_TopAgent", f.TopAgent, f.TopAgentTotal.ToString("N0"));
                return new NotificationService.PendingItem { ChatId = m.ChatId, Type = "DAILY_SUMMARY", Text = text };
            }).ToList();
        }
    }
}
