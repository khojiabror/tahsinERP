using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;

namespace tahsinERP.Areas.Distribution.Services.Reports
{
    /// <summary>Savdo hisoboti kesimlari.</summary>
    public enum SalesDimension
    {
        Agent,
        Customer,
        Product,
        Territory,
        Category
    }

    /// <summary>
    /// Bitta savdo fakti: jo'natish qatori (sotuv) yoki qaytarish qatori (manfiy tomon). Summalar va FIFO tannarx
    /// hujjatlarning o'zidan (D_SHIPMENT_LINES.Amount/CostAmount, D_RETURN_LINES.Amount/UnitCost) — qayta hisoblanmaydi.
    /// </summary>
    public sealed class SalesFact
    {
        public DateTime Date { get; set; }
        public int CustomerID { get; set; }
        public int? AgentUserID { get; set; }
        public int? TerritoryID { get; set; }
        public int ProductID { get; set; }
        public int? CategoryID { get; set; }
        public int WarehouseID { get; set; }
        public int? OrderID { get; set; }
        public decimal Qty { get; set; }
        public decimal Amount { get; set; }
        public decimal Cost { get; set; }
        public decimal ReturnQty { get; set; }
        public decimal ReturnAmount { get; set; }
        public decimal ReturnCost { get; set; }
    }

    /// <summary>Kesim bo'yicha yig'indi (SQL'da GROUP BY).</summary>
    public sealed class SalesRow
    {
        public int? Key { get; set; }
        public string Name { get; set; }
        public decimal Qty { get; set; }
        public decimal Amount { get; set; }
        public decimal ReturnQty { get; set; }
        public decimal ReturnAmount { get; set; }
        public decimal Cost { get; set; }
        public decimal ReturnCost { get; set; }
        public int Orders { get; set; }
        public int Customers { get; set; }

        public decimal Net => Amount - ReturnAmount;
        public decimal NetCost => Cost - ReturnCost;
        public decimal Profit => Net - NetCost;
        public decimal? MarginPct => Net == 0 ? (decimal?)null : Math.Round(Profit / Net * 100m, 1);
    }

    public sealed class SalesTotals
    {
        public decimal Amount { get; set; }
        public decimal ReturnAmount { get; set; }
        public decimal Cost { get; set; }
        public decimal ReturnCost { get; set; }
        public int Orders { get; set; }
        public int Customers { get; set; }

        public decimal Net => Amount - ReturnAmount;
        public decimal Profit => Net - (Cost - ReturnCost);
        public decimal? MarginPct => Net == 0 ? (decimal?)null : Math.Round(Profit / Net * 100m, 1);
    }

    /// <summary>
    /// Savdo hisobotlari: tushum = jo'natilgan (o'tkazilgan jo'natishlar) − qaytarilgan, yalpi foyda = tushum − FIFO tannarx.
    /// Hamma yig'indi SQL'da; sahifalash chaqiruvchida (IQueryable).
    /// Agent — buyurtmadagi agent (buyurtmasiz qaytarishda — mijoz kartasidagi), hudud — mijoz kartasidagi joriy hudud.
    /// </summary>
    public static class SalesReportService
    {
        public static IQueryable<SalesFact> Facts(DistributionDbContext db, ReportFilter filter, ReportScope scope)
        {
            var fromDate = filter.From.Date;
            var toDate = filter.To.Date;

            var shipped = from l in db.ShipmentLines
                          join s in db.Shipments on l.ShipmentID equals s.ID
                          join o in db.Orders on s.OrderID equals o.ID
                          join p in db.Products on l.ProductID equals p.ID
                          where s.Status == DocumentStatus.Posted && s.DocDate >= fromDate && s.DocDate <= toDate
                          select new SalesFact
                          {
                              Date = s.DocDate, CustomerID = o.CustomerID, AgentUserID = o.AgentUserID,
                              TerritoryID = db.CustomerProfiles.Where(cp => cp.CustomerID == o.CustomerID).Select(cp => cp.TerritoryID).FirstOrDefault(),
                              ProductID = l.ProductID, CategoryID = p.CategoryID, WarehouseID = s.WarehouseID, OrderID = (int?)s.OrderID,
                              Qty = l.QtyBase, Amount = l.Amount, Cost = l.CostAmount ?? 0m,
                              ReturnQty = 0m, ReturnAmount = 0m, ReturnCost = 0m
                          };

            var returned = from l in db.ReturnLines
                           join r in db.Returns on l.ReturnID equals r.ID
                           join p in db.Products on l.ProductID equals p.ID
                           where r.Status == DocumentStatus.Posted && r.DocDate >= fromDate && r.DocDate <= toDate
                           select new SalesFact
                           {
                               Date = r.DocDate, CustomerID = r.CustomerID,
                               AgentUserID = db.Orders.Where(o => o.ID == r.OrderID).Select(o => o.AgentUserID).FirstOrDefault()
                                             ?? db.CustomerProfiles.Where(cp => cp.CustomerID == r.CustomerID).Select(cp => cp.AgentUserID).FirstOrDefault(),
                               TerritoryID = db.CustomerProfiles.Where(cp => cp.CustomerID == r.CustomerID).Select(cp => cp.TerritoryID).FirstOrDefault(),
                               ProductID = l.ProductID, CategoryID = p.CategoryID, WarehouseID = r.WarehouseID, OrderID = r.OrderID,
                               Qty = 0m, Amount = 0m, Cost = 0m,
                               ReturnQty = l.QtyBase, ReturnAmount = l.Amount, ReturnCost = l.UnitCost * l.QtyBase
                           };

            var q = shipped.Concat(returned);

            if (filter.AgentId.HasValue) q = q.Where(f => f.AgentUserID == filter.AgentId);
            if (filter.CustomerId.HasValue) q = q.Where(f => f.CustomerID == filter.CustomerId);
            if (filter.ProductId.HasValue) q = q.Where(f => f.ProductID == filter.ProductId);
            if (filter.CategoryId.HasValue) q = q.Where(f => f.CategoryID == filter.CategoryId);
            if (filter.WarehouseId.HasValue) q = q.Where(f => f.WarehouseID == filter.WarehouseId);
            if (filter.TerritoryId.HasValue)
            {
                var territories = TerritoryFilter.WithDescendants(db, filter.TerritoryId.Value).Select(id => (int?)id).ToList();
                q = q.Where(f => territories.Contains(f.TerritoryID));
            }

            scope = scope ?? ReportScope.Unrestricted;
            q = scope.Customers.Apply(q, f => f.CustomerID);
            q = scope.Territories.Apply(q, f => f.TerritoryID);
            q = scope.Agents.Apply(q, f => f.AgentUserID);
            q = scope.Warehouses.Apply(q, f => f.WarehouseID);
            return q;
        }

        private sealed class Agg
        {
            public int? Key { get; set; }
            public decimal Qty { get; set; }
            public decimal Amount { get; set; }
            public decimal ReturnQty { get; set; }
            public decimal ReturnAmount { get; set; }
            public decimal Cost { get; set; }
            public decimal ReturnCost { get; set; }
            public int Orders { get; set; }
            public int Customers { get; set; }
        }

        private sealed class KeyName
        {
            public int ID { get; set; }
            public string Name { get; set; }
        }

        /// <summary>Kesim bo'yicha yig'indilar (nomi bilan), saralash/sahifalash — chaqiruvchida.</summary>
        public static IQueryable<SalesRow> ByDimension(DistributionDbContext db, IQueryable<SalesFact> facts, SalesDimension dimension, bool russian = false)
        {
            IQueryable<IGrouping<int?, SalesFact>> groups;
            IQueryable<KeyName> names;
            switch (dimension)
            {
                case SalesDimension.Customer:
                    groups = facts.GroupBy(f => (int?)f.CustomerID);
                    names = db.Customers.Select(c => new KeyName { ID = c.ID, Name = c.Name });
                    break;
                case SalesDimension.Product:
                    groups = facts.GroupBy(f => (int?)f.ProductID);
                    names = db.Products.Select(p => new KeyName { ID = p.ID, Name = p.PNo + " · " + p.Name });
                    break;
                case SalesDimension.Territory:
                    groups = facts.GroupBy(f => f.TerritoryID);
                    names = russian
                        ? db.Territories.Select(t => new KeyName { ID = t.ID, Name = t.NameRu ?? t.Name })
                        : db.Territories.Select(t => new KeyName { ID = t.ID, Name = t.Name });
                    break;
                case SalesDimension.Category:
                    groups = facts.GroupBy(f => f.CategoryID);
                    names = db.Categories.Select(c => new KeyName { ID = c.ID, Name = c.Name });
                    break;
                default:
                    groups = facts.GroupBy(f => f.AgentUserID);
                    names = db.Users.Select(u => new KeyName { ID = u.ID, Name = u.FullName ?? u.Uname });
                    break;
            }

            var aggregated = groups.Select(g => new Agg
            {
                Key = g.Key,
                Qty = g.Sum(x => x.Qty),
                Amount = g.Sum(x => x.Amount),
                ReturnQty = g.Sum(x => x.ReturnQty),
                ReturnAmount = g.Sum(x => x.ReturnAmount),
                Cost = g.Sum(x => x.Cost),
                ReturnCost = g.Sum(x => x.ReturnCost),
                Orders = g.Where(x => x.OrderID != null && x.Amount != 0).Select(x => x.OrderID).Distinct().Count(),
                Customers = g.Select(x => x.CustomerID).Distinct().Count()
            });

            return from a in aggregated
                   join n in names on a.Key equals (int?)n.ID into gj
                   from n in gj.DefaultIfEmpty()
                   select new SalesRow
                   {
                       Key = a.Key, Name = n.Name, Qty = a.Qty, Amount = a.Amount, ReturnQty = a.ReturnQty, ReturnAmount = a.ReturnAmount,
                       Cost = a.Cost, ReturnCost = a.ReturnCost, Orders = a.Orders, Customers = a.Customers
                   };
        }

        /// <summary>Filtrlangan faktlar bo'yicha jami (bitta so'rov).</summary>
        public static SalesTotals Totals(IQueryable<SalesFact> facts)
        {
            var t = facts.GroupBy(f => 1).Select(g => new
            {
                Amount = g.Sum(x => x.Amount),
                ReturnAmount = g.Sum(x => x.ReturnAmount),
                Cost = g.Sum(x => x.Cost),
                ReturnCost = g.Sum(x => x.ReturnCost),
                Orders = g.Where(x => x.OrderID != null && x.Amount != 0).Select(x => x.OrderID).Distinct().Count(),
                Customers = g.Select(x => x.CustomerID).Distinct().Count()
            }).FirstOrDefault();
            if (t == null) return new SalesTotals();
            return new SalesTotals
            {
                Amount = t.Amount, ReturnAmount = t.ReturnAmount, Cost = t.Cost, ReturnCost = t.ReturnCost, Orders = t.Orders, Customers = t.Customers
            };
        }

        /// <summary>
        /// Qo'shimcha xarajatning (landed cost) allaqachon sotilgan tovarga tushgan qismi (LandedCost.ConsumedAmount): FIFO tannarx
        /// qayta baholanmaydi (04_PHASE3 qarori), shuning uchun davr xarajati sifatida — hujjat o'tkazilgan kun bo'yicha (05_FINDINGS K2-22).
        /// Agent/mijoz/tovarga taqsimlanmaydi — faqat umumiy jamida.
        /// </summary>
        public static decimal LandedCostOnSold(DistributionDbContext db, DateTime from, DateTime to)
        {
            var start = from.Date;
            var end = to.Date.AddDays(1);
            return db.LandedCosts.Where(l => l.Status == DocumentStatus.Posted && l.PostedAt >= start && l.PostedAt < end)
                .Sum(l => (decimal?)l.ConsumedAmount) ?? 0;
        }

        /// <summary>Kunlar bo'yicha tushum va foyda (rahbar paneli grafigi uchun).</summary>
        public static List<(DateTime Date, decimal Net, decimal Profit)> Daily(IQueryable<SalesFact> facts)
        {
            return facts.GroupBy(f => f.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Net = g.Sum(x => x.Amount) - g.Sum(x => x.ReturnAmount),
                    Cost = g.Sum(x => x.Cost) - g.Sum(x => x.ReturnCost)
                })
                .ToList()
                .Select(x => (x.Date, x.Net, x.Net - x.Cost))
                .OrderBy(x => x.Date)
                .ToList();
        }

        /// <summary>Saralash: ustun nomi bo'yicha (DataTables ustun raqami controllerda nomga o'giriladi).</summary>
        public static IOrderedQueryable<SalesRow> Order(IQueryable<SalesRow> rows, string column, bool desc)
        {
            switch (column)
            {
                case "name": return (desc ? rows.OrderByDescending(r => r.Name) : rows.OrderBy(r => r.Name)).ThenBy(r => r.Key);
                case "qty": return (desc ? rows.OrderByDescending(r => r.Qty) : rows.OrderBy(r => r.Qty)).ThenBy(r => r.Key);
                case "amount": return (desc ? rows.OrderByDescending(r => r.Amount) : rows.OrderBy(r => r.Amount)).ThenBy(r => r.Key);
                case "returns": return (desc ? rows.OrderByDescending(r => r.ReturnAmount) : rows.OrderBy(r => r.ReturnAmount)).ThenBy(r => r.Key);
                case "profit":
                    return (desc
                        ? rows.OrderByDescending(r => r.Amount - r.ReturnAmount - r.Cost + r.ReturnCost)
                        : rows.OrderBy(r => r.Amount - r.ReturnAmount - r.Cost + r.ReturnCost)).ThenBy(r => r.Key);
                case "orders": return (desc ? rows.OrderByDescending(r => r.Orders) : rows.OrderBy(r => r.Orders)).ThenBy(r => r.Key);
                case "customers": return (desc ? rows.OrderByDescending(r => r.Customers) : rows.OrderBy(r => r.Customers)).ThenBy(r => r.Key);
                default:
                    return (desc
                        ? rows.OrderByDescending(r => r.Amount - r.ReturnAmount)
                        : rows.OrderBy(r => r.Amount - r.ReturnAmount)).ThenBy(r => r.Key);
            }
        }
    }
}
