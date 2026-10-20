using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Models;
using tahsinERP.Services.Finance;

namespace tahsinERP.Areas.Distribution.Services.Reports
{
    /// <summary>
    /// Pul: agentlar topshirgan naqd (dst.CASH_HANDOVERS), agentlar qo'lidagi naqd (dst.D_PAYMENTS, kassaga tushmagan)
    /// va kassa/bank hisoblari qoldig'i (<see cref="FinQueries.AccountBalances"/> — moliya moduli bilan bir xil hisob).
    /// </summary>
    public static class CashReportService
    {
        public sealed class HandoverRow
        {
            public int ID { get; set; }
            public string DocNo { get; set; }
            public DateTime DocDate { get; set; }
            public DocumentStatus Status { get; set; }
            public int AgentUserID { get; set; }
            public string Agent { get; set; }
            public string Account { get; set; }
            public decimal Declared { get; set; }
            public decimal? Received { get; set; }
            public decimal? Difference => Received.HasValue ? Received - Declared : null;
            public DateTime? PostedAt { get; set; }
            public string Note { get; set; }
        }

        public sealed class AgentSummary
        {
            public int AgentUserID { get; set; }
            public string Agent { get; set; }
            public int Handovers { get; set; }
            public decimal Declared { get; set; }
            public decimal Received { get; set; }
            public decimal Difference { get; set; }
            public decimal Pending { get; set; }
            public decimal OnHand { get; set; }
            public int OnHandCount { get; set; }
            public DateTime? OldestOnHand { get; set; }
        }

        /// <summary>Davrdagi topshirishlar (sahifalash — chaqiruvchida). status: null — hammasi.</summary>
        public static IQueryable<HandoverRow> Handovers(DistributionDbContext db, ReportFilter filter, ReportScope scope, DocumentStatus? status)
        {
            scope = scope ?? ReportScope.Unrestricted;
            var from = filter.From.Date;
            var to = filter.To.Date;
            var q = db.CashHandovers.Where(h => h.DocDate >= from && h.DocDate <= to);
            if (status.HasValue) q = q.Where(h => h.Status == status.Value);
            if (filter.AgentId.HasValue) q = q.Where(h => h.AgentUserID == filter.AgentId);
            q = scope.Agents.Apply(q, h => h.AgentUserID);
            return q.Select(h => new HandoverRow
            {
                ID = h.ID, DocNo = h.DocNo, DocDate = h.DocDate, Status = h.Status, AgentUserID = h.AgentUserID,
                Agent = db.Users.Where(u => u.ID == h.AgentUserID).Select(u => u.FullName ?? u.Uname).FirstOrDefault(),
                Account = db.FinAccounts.Where(a => a.ID == h.FinAccountID).Select(a => a.Name).FirstOrDefault(),
                Declared = h.AmountDeclared, Received = h.AmountReceived, PostedAt = h.PostedAt, Note = h.Note
            });
        }

        /// <summary>Agentlar bo'yicha: davrda topshirilgan/qabul qilingan, farq, kutilayotgan va hozir qo'lidagi naqd.</summary>
        public static List<AgentSummary> ByAgent(DistributionDbContext db, ReportFilter filter, ReportScope scope)
        {
            scope = scope ?? ReportScope.Unrestricted;
            var from = filter.From.Date;
            var to = filter.To.Date;

            var handovers = db.CashHandovers.Where(h => h.DocDate >= from && h.DocDate <= to && h.Status != DocumentStatus.Cancelled);
            handovers = scope.Agents.Apply(handovers, h => h.AgentUserID);
            if (filter.AgentId.HasValue) handovers = handovers.Where(h => h.AgentUserID == filter.AgentId);
            var periods = handovers.GroupBy(h => h.AgentUserID).Select(g => new
            {
                Agent = g.Key,
                Count = g.Count(),
                Declared = g.Sum(h => h.AmountDeclared),
                Received = g.Sum(h => h.Status == DocumentStatus.Posted ? h.AmountReceived ?? 0m : 0m),
                Difference = g.Sum(h => h.Status == DocumentStatus.Posted ? (h.AmountReceived ?? 0m) - h.AmountDeclared : 0m),
                Pending = g.Sum(h => h.Status == DocumentStatus.Draft ? h.AmountDeclared : 0m)
            }).ToList();

            var onHand = OnHandQuery(db, scope);
            if (filter.AgentId.HasValue) onHand = onHand.Where(p => p.ReceivedByUserID == filter.AgentId);
            var hands = onHand.GroupBy(p => p.ReceivedByUserID)
                .Select(g => new { Agent = g.Key, Amount = g.Sum(p => p.Amount), Count = g.Count(), Oldest = g.Min(p => p.DocDate) })
                .ToList();

            var ids = periods.Select(p => p.Agent).Union(hands.Select(h => h.Agent)).ToList();
            var names = db.Users.Where(u => ids.Contains(u.ID)).Select(u => new { u.ID, Name = u.FullName ?? u.Uname }).ToList()
                .ToDictionary(u => u.ID, u => u.Name);
            return ids.Select(id =>
            {
                var p = periods.FirstOrDefault(x => x.Agent == id);
                var h = hands.FirstOrDefault(x => x.Agent == id);
                return new AgentSummary
                {
                    AgentUserID = id, Agent = names.TryGetValue(id, out var n) ? n : "#" + id,
                    Handovers = p?.Count ?? 0, Declared = p?.Declared ?? 0, Received = p?.Received ?? 0, Difference = p?.Difference ?? 0,
                    Pending = p?.Pending ?? 0, OnHand = h?.Amount ?? 0, OnHandCount = h?.Count ?? 0, OldestOnHand = h?.Oldest
                };
            }).OrderByDescending(r => r.OnHand).ThenByDescending(r => r.Declared).ToList();
        }

        /// <summary>Agent qo'lidagi to'lovlar (o'tkazilgan, hisobga hali tushmagan; kutilayotgan topshirish ichidagilari ham).</summary>
        public static IQueryable<DstPayment> OnHandQuery(DistributionDbContext db, ReportScope scope)
        {
            var q = db.Payments.Where(p => p.Status == DocumentStatus.Posted && p.FinAccountID == null);
            return (scope ?? ReportScope.Unrestricted).Agents.Apply(q, p => p.ReceivedByUserID);
        }

        public sealed class AccountBalance
        {
            public int AccountID { get; set; }
            public string Name { get; set; }
            public string Currency { get; set; }
            public bool IsCash { get; set; }
            public decimal Balance { get; set; }
        }

        /// <summary>Faol kassa va bank hisoblari qoldig'i (sana oxirigacha).</summary>
        public static List<AccountBalance> Accounts(DBTHSNEntities erp, DateTime asOf)
        {
            var accounts = erp.FIN_ACCOUNTS.AsNoTracking()
                .Where(a => a.IsActive && a.IsDeleted != true && (a.IsCash || a.IsBank))
                .Select(a => new { a.ID, a.Name, a.IsCash, Currency = a.CURRENCy.Shortname })
                .ToList();
            var balances = FinQueries.AccountBalances(erp, accounts.Select(a => a.ID).ToList(), asOf.Date.AddDays(1).AddTicks(-1));
            return accounts.Select(a => new AccountBalance
            {
                AccountID = a.ID, Name = a.Name, Currency = a.Currency, IsCash = a.IsCash,
                Balance = balances.TryGetValue(a.ID, out var b) ? b : 0m
            }).OrderByDescending(a => a.IsCash).ThenBy(a => a.Currency).ThenBy(a => a.Name).ToList();
        }
    }
}
