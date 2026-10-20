using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;

namespace tahsinERP.Areas.Distribution.Services.Reports
{
    /// <summary>Hisobot filtri: davr (sanalar kiritilgan holda) va ixtiyoriy kesim qiymatlari.</summary>
    public sealed class ReportFilter
    {
        public DateTime From { get; set; } = DateTime.Today;
        public DateTime To { get; set; } = DateTime.Today;
        public int? AgentId { get; set; }
        public int? CustomerId { get; set; }
        public int? TerritoryId { get; set; }
        public int? CategoryId { get; set; }
        public int? ProductId { get; set; }
        public int? WarehouseId { get; set; }

        /// <summary>Davr tartibga keltiriladi: vaqt qismi tashlanadi, teskari bo'lsa almashtiriladi, ko'pi bilan 3 yil.</summary>
        public ReportFilter Normalized()
        {
            var from = From.Date;
            var to = To.Date;
            if (to < from) (from, to) = (to, from);
            if ((to - from).TotalDays > 366 * 3) from = to.AddDays(-366 * 3);
            From = from;
            To = to;
            return this;
        }

        /// <summary>Joriy oy boshidan bugungacha (davr berilmaganda).</summary>
        public static ReportFilter MonthToDate(DateTime today) =>
            new ReportFilter { From = new DateTime(today.Year, today.Month, 1), To = today.Date };
    }

    /// <summary>
    /// Hisobotlardagi qator darajasidagi ko'rinish — ro'yxat sahifalari bilan bir xil qoidalar (<see cref="ScopeService"/>):
    /// mijoz, hudud va ombor scope'lari hamda agent ierarxiyasi (agent — o'zi, supervayzer — jamoasi).
    /// </summary>
    public sealed class ReportScope
    {
        public static readonly ReportScope Unrestricted = new ReportScope
        {
            Customers = ScopeFilter.Unrestricted, Territories = ScopeFilter.Unrestricted,
            Warehouses = ScopeFilter.Unrestricted, Agents = ScopeFilter.Unrestricted
        };

        public ScopeFilter Customers { get; set; }
        public ScopeFilter Territories { get; set; }
        public ScopeFilter Warehouses { get; set; }
        public ScopeFilter Agents { get; set; }

        public bool IsUnrestricted => Customers.IsUnrestricted && Territories.IsUnrestricted && Warehouses.IsUnrestricted && Agents.IsUnrestricted;

        public static ReportScope For(DistributionDbContext db, string userName)
        {
            var scopes = new ScopeService(db);
            return new ReportScope
            {
                Customers = scopes.For(userName, ScopeTypes.Customer),
                Territories = scopes.For(userName, ScopeTypes.Territory),
                Warehouses = scopes.For(userName, ScopeTypes.Warehouse),
                Agents = scopes.AgentsFor(userName)
            };
        }

        /// <summary>Ko'rinadigan distributsiya mijozlari (profil bor, o'chirilmagan) — qarz hisobi uchun.</summary>
        public IQueryable<int> VisibleCustomers(DistributionDbContext db)
        {
            var q = from p in db.CustomerProfiles
                    join c in db.Customers on p.CustomerID equals c.ID
                    where c.IsDeleted != true
                    select new { p.CustomerID, p.TerritoryID, p.AgentUserID };
            q = Customers.Apply(q, x => x.CustomerID);
            q = Territories.Apply(q, x => x.TerritoryID);
            q = Agents.Apply(q, x => x.AgentUserID);
            return q.Select(x => x.CustomerID);
        }
    }

    /// <summary>Hududlar daraxti bo'yicha filtr: tanlangan hudud va uning barcha quyi hududlari.</summary>
    public static class TerritoryFilter
    {
        public static List<int> WithDescendants(DistributionDbContext db, int territoryId)
        {
            var all = db.Territories.AsNoTracking().Select(t => new { t.ID, t.ParentID }).ToList();
            var byParent = all.ToLookup(t => t.ParentID);
            var result = new List<int> { territoryId };
            for (int i = 0; i < result.Count && result.Count <= all.Count; i++)
                result.AddRange(byParent[result[i]].Select(t => t.ID).Where(id => !result.Contains(id)));
            return result;
        }
    }
}
