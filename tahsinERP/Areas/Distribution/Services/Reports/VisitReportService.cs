using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;

namespace tahsinERP.Areas.Distribution.Services.Reports
{
    /// <summary>
    /// Tashriflar: reja (mijoz kartasidagi tashrif kunlari × davrdagi hafta kunlari) va fakt (dst.VISITS), samaradorlik.
    /// Reja — mijozlarning JORIY agenti va tashrif kunlari bo'yicha (tarix saqlanmaydi).
    /// </summary>
    public static class VisitReportService
    {
        public sealed class AgentRow
        {
            public int AgentUserID { get; set; }
            public string Agent { get; set; }
            public int WorkDays { get; set; }
            public int Planned { get; set; }
            public int Visits { get; set; }
            public int PlannedVisited { get; set; }
            public int Unplanned => Visits - PlannedVisited;
            public int WithOrder { get; set; }
            public int WithPayment { get; set; }
            public int NoOrder { get; set; }
            public decimal OrderTotal { get; set; }

            /// <summary>Rejaning bajarilishi, %.</summary>
            public decimal? CoveragePct => Planned == 0 ? (decimal?)null : Math.Round(PlannedVisited * 100m / Planned, 1);

            /// <summary>Buyurtma bilan yakunlangan tashriflar ulushi, %.</summary>
            public decimal? StrikePct => Visits == 0 ? (decimal?)null : Math.Round(WithOrder * 100m / Visits, 1);

            /// <summary>O'rtacha buyurtma (tashrifdagi).</summary>
            public decimal? AvgOrder => WithOrder == 0 ? (decimal?)null : Math.Round(OrderTotal / WithOrder, 0);
        }

        /// <summary>Davrda har hafta kuni necha marta uchraydi (indeks — <see cref="VisitDays.For"/> biti tartibida: 0 — dushanba).</summary>
        public static int[] WeekdayOccurrences(DateTime from, DateTime to)
        {
            var result = new int[7];
            for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
                result[((int)d.DayOfWeek + 6) % 7]++;
            return result;
        }

        /// <summary>Reja: har hafta kuni uchun rejadagi mijozlar soni × shu kunning davrda uchrashi.</summary>
        public static int Planned(int[] customersPerWeekday, int[] occurrences)
        {
            int total = 0;
            for (int i = 0; i < 7; i++) total += customersPerWeekday[i] * occurrences[i];
            return total;
        }

        public static List<AgentRow> ByAgent(DistributionDbContext db, ReportFilter filter, ReportScope scope)
        {
            scope = scope ?? ReportScope.Unrestricted;
            var from = filter.From.Date;
            var to = filter.To.Date;

            var agents = from a in db.SalesAgents
                         join u in db.Users on a.UserID equals u.ID
                         where !a.IsExpeditor
                         select new { a.UserID, a.IsActive, Name = u.FullName ?? u.Uname };
            agents = scope.Agents.Apply(agents, a => a.UserID);
            if (filter.AgentId.HasValue) agents = agents.Where(a => a.UserID == filter.AgentId);
            var agentList = agents.ToList();
            var ids = agentList.Select(a => a.UserID).ToList();

            // reja: tasdiqlangan, o'chirilmagan mijozlar, hafta kunlari bo'yicha (bitta GROUP BY)
            var customers = from p in db.CustomerProfiles
                            join c in db.Customers on p.CustomerID equals c.ID
                            where c.IsDeleted != true && p.IsApproved && p.AgentUserID != null && ids.Contains(p.AgentUserID.Value)
                            select p;
            customers = scope.Customers.Apply(customers, p => p.CustomerID);
            customers = scope.Territories.Apply(customers, p => p.TerritoryID);
            if (filter.TerritoryId.HasValue)
            {
                var territories = TerritoryFilter.WithDescendants(db, filter.TerritoryId.Value).Select(t => (int?)t).ToList();
                customers = customers.Where(p => territories.Contains(p.TerritoryID));
            }
            var plan = customers.GroupBy(p => p.AgentUserID.Value).Select(g => new
            {
                Agent = g.Key,
                D0 = g.Sum(p => (p.VisitDays & 1) != 0 ? 1 : 0),
                D1 = g.Sum(p => (p.VisitDays & 2) != 0 ? 1 : 0),
                D2 = g.Sum(p => (p.VisitDays & 4) != 0 ? 1 : 0),
                D3 = g.Sum(p => (p.VisitDays & 8) != 0 ? 1 : 0),
                D4 = g.Sum(p => (p.VisitDays & 16) != 0 ? 1 : 0),
                D5 = g.Sum(p => (p.VisitDays & 32) != 0 ? 1 : 0),
                D6 = g.Sum(p => (p.VisitDays & 64) != 0 ? 1 : 0)
            }).ToList().ToDictionary(x => x.Agent, x => new[] { x.D0, x.D1, x.D2, x.D3, x.D4, x.D5, x.D6 });
            var occurrences = WeekdayOccurrences(from, to);

            // fakt
            var visits = db.Visits.Where(v => v.VisitDate >= from && v.VisitDate <= to && ids.Contains(v.UserID));
            visits = scope.Customers.Apply(visits, v => v.CustomerID);
            if (filter.CustomerId.HasValue) visits = visits.Where(v => v.CustomerID == filter.CustomerId);
            if (filter.TerritoryId.HasValue)
            {
                var territories = TerritoryFilter.WithDescendants(db, filter.TerritoryId.Value).Select(t => (int?)t).ToList();
                visits = visits.Where(v => db.CustomerProfiles.Any(p => p.CustomerID == v.CustomerID && territories.Contains(p.TerritoryID)));
            }
            var fact = visits.GroupBy(v => v.UserID).Select(g => new
            {
                Agent = g.Key,
                Visits = g.Count(),
                PlannedVisited = g.Count(v => v.IsPlanned),
                WithOrder = g.Count(v => v.OrderID != null),
                WithPayment = g.Count(v => v.PaymentID != null),
                NoOrder = g.Count(v => v.Result == VisitResults.NoOrder),
                OrderTotal = g.Sum(v => (decimal?)db.Orders.Where(o => o.ID == v.OrderID && o.Status != DocumentStatus.Cancelled)
                    .Select(o => o.Total).FirstOrDefault()) ?? 0m
            }).ToList().ToDictionary(x => x.Agent);

            var workDays = db.AgentDays.Where(d => d.WorkDate >= from && d.WorkDate <= to && ids.Contains(d.UserID))
                .GroupBy(d => d.UserID).Select(g => new { g.Key, Count = g.Count() }).ToList().ToDictionary(x => x.Key, x => x.Count);

            return agentList.Select(a =>
            {
                fact.TryGetValue(a.UserID, out var f);
                return new AgentRow
                {
                    AgentUserID = a.UserID, Agent = a.Name,
                    WorkDays = workDays.TryGetValue(a.UserID, out var w) ? w : 0,
                    Planned = plan.TryGetValue(a.UserID, out var p) ? Planned(p, occurrences) : 0,
                    Visits = f?.Visits ?? 0, PlannedVisited = f?.PlannedVisited ?? 0, WithOrder = f?.WithOrder ?? 0,
                    WithPayment = f?.WithPayment ?? 0, NoOrder = f?.NoOrder ?? 0, OrderTotal = f?.OrderTotal ?? 0
                };
            })
            .Where(r => r.Planned > 0 || r.Visits > 0 || agentList.Any(a => a.UserID == r.AgentUserID && a.IsActive))
            .OrderByDescending(r => r.Visits).ThenBy(r => r.Agent)
            .ToList();
        }
    }
}
