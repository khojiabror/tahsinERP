using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;

namespace tahsinERP.Areas.Distribution.Services.Orders
{
    public sealed class CatalogUnit
    {
        public int? ProductUnitId { get; set; }
        public string Name { get; set; }
        public decimal Factor { get; set; }
        public decimal? Price { get; set; }
        public bool IsDefault { get; set; }
    }

    public sealed class CatalogItem
    {
        public int Id { get; set; }
        public string PNo { get; set; }
        public string Name { get; set; }
        public string NameRu { get; set; }
        public string Text { get; set; }
        public int? CategoryId { get; set; }
        public string Category { get; set; }
        public decimal Available { get; set; }   // asosiy birlikda
        public List<CatalogUnit> Units { get; set; }
    }

    /// <summary>
    /// Buyurtma katalogi: nom/artikul yoki aniq shtrix-kod bo'yicha; mijoz narxi (birlik bo'yicha), savdo omborlaridagi
    /// mavjud qoldiq. Web forma, ERP API (bot, Mini App) bir xil natija oladi.
    /// </summary>
    public static class CatalogService
    {
        public static List<CatalogItem> Search(DistributionDbContext db, string q, int? customerId, int? warehouseId, int? categoryId, DateTime now, int take = 20)
        {
            var term = q?.Trim();
            if (term == "") term = null;
            var barcode = term == null ? null
                : db.ProductBarcodes.Where(b => b.Barcode == term).Select(b => new { b.ProductID, b.ProductUnitID }).FirstOrDefault();
            List<int> ids;
            if (barcode != null)
                ids = new List<int> { barcode.ProductID };
            else
            {
                var query = from p in db.Products
                            where !p.IsDeleted && !db.ProductProfiles.Any(pf => pf.ProductID == p.ID && !pf.IsSaleActive)
                            select new { p.ID, p.PNo, p.Name, p.CategoryID, NameRu = db.ProductProfiles.Where(pf => pf.ProductID == p.ID).Select(pf => pf.NameRu).FirstOrDefault() };
                if (term != null) query = query.Where(p => p.PNo.Contains(term) || p.Name.Contains(term) || p.NameRu.Contains(term));
                if (categoryId.HasValue) query = query.Where(p => p.CategoryID == categoryId);
                ids = query.OrderBy(p => p.Name).Select(p => p.ID).Take(Math.Max(1, Math.Min(take, 100))).ToList();
            }
            if (ids.Count == 0) return new List<CatalogItem>();

            var products = (from p in db.Products
                            where ids.Contains(p.ID)
                            select new
                            {
                                p.ID, p.PNo, p.Name, p.CategoryID,
                                NameRu = db.ProductProfiles.Where(pf => pf.ProductID == p.ID).Select(pf => pf.NameRu).FirstOrDefault(),
                                Category = db.Categories.Where(c => c.ID == p.CategoryID).Select(c => c.Name).FirstOrDefault()
                            }).ToList();
            var prices = new PricingService(db).Resolve(customerId, ids, now);
            var warehouses = warehouseId.HasValue ? new List<int> { warehouseId.Value } : StockService.SalesWarehouses(db);
            var stock = StockService.Levels(db, ids, warehouses).GroupBy(l => l.ProductID).ToDictionary(g => g.Key, g => g.Sum(x => x.Available));
            var units = new UnitService(db);

            return products.OrderBy(p => p.Name).Select(p =>
            {
                var price = prices[p.ID];
                stock.TryGetValue(p.ID, out var available);
                return new CatalogItem
                {
                    Id = p.ID, PNo = p.PNo, Name = p.Name, NameRu = p.NameRu, Text = p.PNo + " — " + p.Name,
                    CategoryId = p.CategoryID, Category = p.Category, Available = available,
                    Units = units.UnitsOf(p.ID).Select(u => new CatalogUnit
                    {
                        ProductUnitId = u.ProductUnitID, Name = u.UnitName, Factor = u.Factor,
                        Price = price.Found ? UnitMath.UnitPrice(price.Price.Value, u.Factor) : (decimal?)null,
                        IsDefault = barcode != null ? u.ProductUnitID == barcode.ProductUnitID : u.IsDefaultSale
                    }).ToList()
                };
            }).ToList();
        }
    }
}
