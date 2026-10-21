using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Models;
using tahsinERP.Services.Procurement;
using tahsinERP.ViewModels.POrder;

namespace tahsinERP.Areas.Distribution.Services.Warehouse
{
    /// <summary>
    /// Minimal qoldiq → yetkazib beruvchiga buyurtma (TZ 5-bo'lim). Mavjud (rezervdan tashqari) qoldiq minimaldan past
    /// tovarlar: taklif = maqsad (MaxQty, bo'lmasa MinQty × 2) − mavjud. Yetkazib beruvchi va narx — tovarning oxirgi xarid
    /// hisob-fakturasidan (P_INVOICE_PRODUCTS). Tanlanganlar yetkazib beruvchi bo'yicha guruhlanib, SRM'ning
    /// <see cref="POrderService"/> orqali xarid buyurtmasi (P_ORDERS, "Created") yaratiladi — keyingi qadamlar (shartnoma,
    /// hisob-faktura, kirim) odatdagidek SRM'da. SRM qoidasi bo'yicha to'g'ridan-to'g'ri xaridda yetkazib beruvchi
    /// buyurtmaga yozilmaydi — taklif qilingan yetkazib beruvchi izohda.
    /// </summary>
    public static class ReorderService
    {
        public const string Marker = "DST_REORDER";

        public sealed class Row
        {
            public int WarehouseID { get; set; }
            public string Warehouse { get; set; }
            public int ProductID { get; set; }
            public string PNo { get; set; }
            public string Name { get; set; }
            public int UnitID { get; set; }
            public string Unit { get; set; }
            public decimal OnHand { get; set; }
            public decimal Reserved { get; set; }
            public decimal Available => OnHand - Reserved;
            public decimal MinQty { get; set; }
            public decimal? MaxQty { get; set; }
            public decimal Target => MaxQty ?? MinQty * 2;
            public decimal Suggested => Math.Max(0, Math.Ceiling(Target - Available));
            public int? SupplierID { get; set; }
            public string Supplier { get; set; }
            public decimal? LastPrice { get; set; }
            public DateTime? LastPurchase { get; set; }
            public string RecentOrderNo { get; set; }
        }

        public sealed class Selection
        {
            public int ProductID { get; set; }
            public int WarehouseID { get; set; }
            public decimal Qty { get; set; }
            /// <summary>Bo'sh — oxirgi xarid narxi serverda olinadi (tannarx huquqisiz foydalanuvchi narxni ko'rmaydi, K1-5).</summary>
            public decimal? Price { get; set; }
            public int? SupplierID { get; set; }
        }

        /// <summary>Minimaldan past tovarlar (<paramref name="warehouseIds"/> — scope; null — hammasi).</summary>
        public static List<Row> BelowMin(DistributionDbContext db, DBTHSNEntities erp, int? warehouseId, IReadOnlyCollection<int> warehouseIds, DateTime now)
        {
            var limits = db.ProductStockLimits.AsNoTracking().AsQueryable();
            if (warehouseId.HasValue) limits = limits.Where(l => l.WarehouseID == warehouseId);
            if (warehouseIds != null)
            {
                var allowed = warehouseIds.ToList();
                limits = limits.Where(l => allowed.Contains(l.WarehouseID));
            }
            var rows = limits.ToList();
            if (rows.Count == 0) return new List<Row>();
            var productIds = rows.Select(r => r.ProductID).Distinct().ToList();
            var whIds = rows.Select(r => r.WarehouseID).Distinct().ToList();
            var levels = StockService.Levels(db, productIds, whIds).ToDictionary(l => (l.WarehouseID, l.ProductID));
            var products = (from p in db.Products
                            where productIds.Contains(p.ID) && !p.IsDeleted
                            join u in db.Units on p.UnitID equals u.ID into uj
                            from u in uj.DefaultIfEmpty()
                            select new { p.ID, p.PNo, p.Name, p.UnitID, Unit = u.ShortName }).ToList().ToDictionary(p => p.ID);
            var warehouses = db.Warehouses.Where(w => whIds.Contains(w.ID)).ToDictionary(w => w.ID, w => w.WHName);

            var result = new List<Row>();
            foreach (var l in rows)
            {
                if (!products.TryGetValue(l.ProductID, out var p)) continue;
                levels.TryGetValue((l.WarehouseID, l.ProductID), out var lvl);
                var row = new Row
                {
                    WarehouseID = l.WarehouseID, Warehouse = warehouses.TryGetValue(l.WarehouseID, out var w) ? w : "#" + l.WarehouseID,
                    ProductID = p.ID, PNo = p.PNo, Name = p.Name, UnitID = p.UnitID, Unit = p.Unit,
                    OnHand = lvl?.OnHand ?? 0, Reserved = lvl?.Reserved ?? 0, MinQty = l.MinQty, MaxQty = l.MaxQty
                };
                if (row.Available < row.MinQty) result.Add(row);
            }
            FillSuppliers(erp, result, now);
            return result.OrderBy(r => r.Warehouse).ThenBy(r => r.PNo).ToList();
        }

        private sealed class LastPurchaseInfo
        {
            public int ProductID { get; set; }
            public int? SupplierID { get; set; }
            public string Supplier { get; set; }
            public decimal? Price { get; set; }
            public DateTime? InvoiceDate { get; set; }
        }

        /// <summary>Tovarning oxirgi xarid hisob-fakturasi (yetkazib beruvchi, narx).</summary>
        private static Dictionary<int, LastPurchaseInfo> LastPurchases(DBTHSNEntities erp, List<int> ids)
        {
            return (from ip in erp.P_INVOICE_PRODUCTS
                    join i in erp.P_INVOICES on ip.InvoiceID equals i.ID
                    where ids.Contains(ip.ProductID) && i.IsDeleted != true
                    orderby i.InvoiceDate descending, ip.ID descending
                    select new LastPurchaseInfo { ProductID = ip.ProductID, SupplierID = i.SupplierID, Supplier = i.SUPPLIER.Name, Price = ip.Price, InvoiceDate = i.InvoiceDate })
                .Take(5000).ToList()
                .GroupBy(x => x.ProductID).ToDictionary(g => g.Key, g => g.First());
        }

        private static void FillSuppliers(DBTHSNEntities erp, List<Row> rows, DateTime now)
        {
            if (rows.Count == 0) return;
            var ids = rows.Select(r => r.ProductID).Distinct().ToList();
            var last = LastPurchases(erp, ids);
            var since = now.AddDays(-30);
            string marker = Marker;
            var recent = (from op in erp.P_ORDER_PRODUCTS
                          join o in erp.P_ORDERS on op.OrderID equals o.ID
                          where ids.Contains(op.ProductID) && o.IsDeleted != true && o.IssuedDate >= since && o.Description.Contains(marker)
                          orderby o.IssuedDate descending
                          select new { op.ProductID, o.OrderNo }).ToList()
                         .GroupBy(x => x.ProductID).ToDictionary(g => g.Key, g => g.First().OrderNo);
            foreach (var r in rows)
            {
                if (last.TryGetValue(r.ProductID, out var x))
                {
                    r.SupplierID = x.SupplierID;
                    r.Supplier = x.Supplier;
                    r.LastPrice = x.Price;
                    r.LastPurchase = x.InvoiceDate;
                }
                r.RecentOrderNo = recent.TryGetValue(r.ProductID, out var no) ? no : null;
            }
        }

        /// <summary>Narxi kiritilmagan (tannarx huquqisiz) qatorlarga oxirgi xarid narxi; xaridi yo'q tovar — 0 (SRM'da to'ldiriladi).</summary>
        internal static void FillMissingPrices(IEnumerable<Selection> rows, IReadOnlyDictionary<int, decimal?> lastPrices)
        {
            foreach (var r in rows.Where(r => r.Price == null))
                r.Price = lastPrices.TryGetValue(r.ProductID, out var p) ? p ?? 0 : 0;
        }

        /// <summary>
        /// Xarid buyurtmalari: yetkazib beruvchi bo'yicha bittadan (yetkazib beruvchisiz tovarlar — alohida bitta).
        /// <paramref name="erp"/> — tranzaksiyasiz yangi kontekst (POrderService o'z tranzaksiyasini ochadi). Qaytaradi: buyurtma raqamlari.
        /// </summary>
        public static async Task<List<string>> CreateOrdersAsync(DBTHSNEntities erp, IList<Selection> selections, int userId, DateTime now)
        {
            var rows = (selections ?? new List<Selection>()).Where(s => s != null && s.ProductID > 0 && s.Qty > 0).ToList();
            if (rows.Count == 0) throw new DstValidationException(DstLocalization.T("Order_NoLines"));
            if (rows.Any(r => r.Price < 0)) throw new DstValidationException(DstLocalization.T("Error_NumberFormat"));
            var ids = rows.Select(r => r.ProductID).Distinct().ToList();
            if (rows.Any(r => r.Price == null))
                FillMissingPrices(rows, LastPurchases(erp, ids).ToDictionary(x => x.Key, x => x.Value.Price));
            var units = erp.PRODUCTS.Where(p => ids.Contains(p.ID) && p.IsDeleted != true).ToDictionary(p => p.ID, p => p.UnitID);
            if (units.Count != ids.Count) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var supplierIds = rows.Where(r => r.SupplierID.HasValue).Select(r => r.SupplierID.Value).Distinct().ToList();
            var suppliers = erp.SUPPLIERS.Where(s => supplierIds.Contains(s.ID)).ToDictionary(s => s.ID, s => s.Name);
            var warehouseIds = rows.Select(r => r.WarehouseID).Distinct().ToList();
            var warehouses = erp.WAREHOUSES.Where(w => warehouseIds.Contains(w.ID)).ToDictionary(w => w.ID, w => w.WHName);

            var created = new List<string>();
            foreach (var g in rows.GroupBy(r => r.SupplierID))
            {
                string supplier = g.Key.HasValue && suppliers.TryGetValue(g.Key.Value, out var sn) ? sn : null;
                var model = new POrderCreateViewModel
                {
                    IssuedDate = now,
                    VatRate = 0,
                    Description = Marker + ": minimal qoldiq bo'yicha (distributsiya)."
                                  + (supplier == null ? "" : " Taklif qilingan yetkazib beruvchi: " + supplier + ".")
                                  + " Omborlar: " + string.Join(", ", g.Select(r => r.WarehouseID).Distinct().Select(w => warehouses.TryGetValue(w, out var n) ? n : "#" + w)),
                    OrderProducts = g.GroupBy(r => r.ProductID).Select(p => new POrderProductViewModel
                    {
                        ProductID = p.Key, Amount = p.Sum(x => x.Qty), Price = p.First().Price ?? 0, UnitID = units[p.Key],
                        CalculationSummary = "Min qoldiq: " + string.Join(", ", p.Select(x => (warehouses.TryGetValue(x.WarehouseID, out var n) ? n : "#" + x.WarehouseID) + " " + x.Qty.ToString("0.##")))
                    }).ToList()
                };
                var errors = new ModelStateDictionary();
                var order = await new POrderService(erp, errors).CreateAsync(model, userId);
                if (order == null)
                    throw new DstValidationException(string.Join("; ", errors.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                created.Add(order.OrderNo);
            }
            return created;
        }
    }
}
