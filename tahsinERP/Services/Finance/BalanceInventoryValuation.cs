using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.Services.DlocFifo;
using tahsinERP.ViewModels.Finance;
using tahsinERP.Services.Common;
using tahsinERP.Services.Stock;

namespace tahsinERP.Services.Finance
{
    /// <summary>
    /// Balans uchun ombor bahosi:
    ///  - "Tayyor maxsulot": ishlab chiqarish omboridagi tayyor mahsulot × sanagacha oxirgi Tannarx (SalesPriceCIPWithVAT);
    ///  - "Xomashyo": xomashyo ombori qoldig'i (Jihoz kategoriyasisiz) × shu omborga oxirgi kirim birlik narxi.
    /// Avval har qoldiq qatoriga alohida PRODUCTS so'rovi ketardi (FIN-P03) — endi bitta so'rov.
    /// </summary>
    public static class BalanceInventoryValuation
    {
        public static decimal FinishedGoodsTotal(DBTHSNEntities db, DateTime asOfDate)
        {
            return FinishedGoodsLines(db, asOfDate).Sum(r => r.LineTotal);
        }

        public static decimal RawMaterialTotal(DBTHSNEntities db, DateTime asOfDate)
        {
            return RawMaterialLines(db, asOfDate).Sum(r => r.LineTotal);
        }

        public static List<BalanceSheetInventoryBreakdownRow> FinishedGoodsLines(DBTHSNEntities db, DateTime asOfDate)
        {
            var rows = new List<BalanceSheetInventoryBreakdownRow>();
            var wh = ResolveProductionWarehouse(db);
            if (wh == null)
            {
                rows.Add(new BalanceSheetInventoryBreakdownRow
                {
                    Note = "Ishlab chiqarish ombori topilmadi (Sozlamalar → Omborlar: ombor turi «Ishlab chiqarish» bo'lishi kerak)."
                });
                return rows;
            }

            var fgStocks = GetStockLines(db, wh.ID, IsFinishedGood);
            if (fgStocks.Count == 0)
            {
                rows.Add(new BalanceSheetInventoryBreakdownRow
                {
                    Note = "Bu omborda tayyor maxsulot (IsFinishedGood) qoldig'i yo'q."
                });
                return rows;
            }

            var fgProductIds = fgStocks.Select(s => s.ProductId).Distinct().ToList();
            var tannarxRows = db.TANNARXes
                .AsNoTracking()
                .Where(t => fgProductIds.Contains(t.ProductID) && !t.IsDeleted)
                .ToList()
                .Where(t => new DateTime(t.Year, t.Month, 1) <= asOfDate.Date)
                .GroupBy(t => t.ProductID)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).First());

            foreach (var s in fgStocks)
            {
                var p = s.Product;
                if (!tannarxRows.TryGetValue(s.ProductId, out var tx))
                {
                    rows.Add(new BalanceSheetInventoryBreakdownRow
                    {
                        ProductCode = p.PNo,
                        ProductName = p.Name,
                        Qty = s.Qty,
                        Unit = s.UnitShortName ?? "",
                        UnitPrice = null,
                        LineTotal = 0m,
                        Note = "Sanagacha mos Tannarx topilmadi"
                    });
                    continue;
                }

                rows.Add(new BalanceSheetInventoryBreakdownRow
                {
                    ProductCode = p.PNo,
                    ProductName = p.Name,
                    Qty = s.Qty,
                    Unit = s.UnitShortName ?? "",
                    UnitPrice = tx.SalesPriceCIPWithVAT,
                    LineTotal = s.Qty * tx.SalesPriceCIPWithVAT,
                    Note = $"Tannarx: {tx.Month:00}/{tx.Year}, SalesPriceCIPWithVAT"
                });
            }

            return rows;
        }

        public static List<BalanceSheetInventoryBreakdownRow> RawMaterialLines(DBTHSNEntities db, DateTime asOfDate)
        {
            var rows = new List<BalanceSheetInventoryBreakdownRow>();
            var wh = ResolveRawMaterialWarehouse(db);
            if (wh == null)
            {
                rows.Add(new BalanceSheetInventoryBreakdownRow
                {
                    Note = "Xomashyo ombori topilmadi (Sozlamalar → Omborlar: ombor turi «Xomashyo (ta'minot)» bo'lishi kerak)."
                });
                return rows;
            }

            var eligible = GetStockLines(db, wh.ID, p =>
                !p.IsDeleted
                && p.IsRawMaterial == true
                && p.IsFinishedGood != true
                && !IsJihozProductCategory(p));

            if (eligible.Count == 0)
            {
                rows.Add(new BalanceSheetInventoryBreakdownRow
                {
                    Note = "Bu omborda xomashyo qoldig'i yo'q."
                });
                return rows;
            }

            var productIds = eligible.Select(s => s.ProductId).Distinct().ToList();
            var latestIncomeByProduct = db.INCOME_PRODUCTS
                .AsNoTracking()
                .Include(ip => ip.INCOME)
                .Where(ip => ip.INCOME.ReceivingWHID == wh.ID
                    && (ip.INCOME.IsDeleted == null || ip.INCOME.IsDeleted == false)
                    && ip.INCOME.IssueDateTime <= asOfDate
                    && productIds.Contains(ip.ProductID)
                    && ip.Amount > 0)
                .ToList()
                .GroupBy(ip => ip.ProductID)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.INCOME.IssueDateTime).First());

            foreach (var s in eligible)
            {
                var p = s.Product;
                if (!latestIncomeByProduct.TryGetValue(s.ProductId, out var ip))
                {
                    rows.Add(new BalanceSheetInventoryBreakdownRow
                    {
                        ProductCode = p.PNo,
                        ProductName = p.Name,
                        Qty = s.Qty,
                        Unit = s.UnitShortName ?? "",
                        UnitPrice = null,
                        LineTotal = 0m,
                        Note = "Oxirgi kirim (shu ombor, sanagacha) topilmadi"
                    });
                    continue;
                }

                var unitPrice = GetIncomeProductUnitPrice(ip);
                if (unitPrice <= 0)
                {
                    rows.Add(new BalanceSheetInventoryBreakdownRow
                    {
                        ProductCode = p.PNo,
                        ProductName = p.Name,
                        Qty = s.Qty,
                        Unit = s.UnitShortName ?? "",
                        UnitPrice = null,
                        LineTotal = 0m,
                        Note = "Kirim qatorida narx (PiecePrice / TotalPrice) yo'q"
                    });
                    continue;
                }

                rows.Add(new BalanceSheetInventoryBreakdownRow
                {
                    ProductCode = p.PNo,
                    ProductName = p.Name,
                    Qty = s.Qty,
                    Unit = s.UnitShortName ?? "",
                    UnitPrice = unitPrice,
                    LineTotal = s.Qty * unitPrice,
                    Note = $"Qoldiq × oxirgi kirim narxi: {ip.INCOME.IssueDateTime:dd.MM.yyyy} {ip.INCOME.DocNo}"
                });
            }

            return rows;
        }

        /// <summary>Ishlab chiqarish ombori — WAREHOUSES.WarehouseType = PRODUCTION (avval nomida "Ishlab chiqarish" qidirilardi).</summary>
        public static Warehouse ResolveProductionWarehouse(DBTHSNEntities db)
        {
            return ActiveWarehouseOfType(db, WarehouseRoles.Production);
        }

        /// <summary>Xomashyo ombori — WAREHOUSES.WarehouseType = SUPPLY (avval nomida "Xomashyo" qidirilardi).</summary>
        public static Warehouse ResolveRawMaterialWarehouse(DBTHSNEntities db)
        {
            return ActiveWarehouseOfType(db, WarehouseRoles.Supply);
        }

        private static Warehouse ActiveWarehouseOfType(DBTHSNEntities db, string warehouseType)
        {
            // WarehouseRoles keshi jarayon darajasida (tenant kalitisiz) — balans uchun to'g'ridan-to'g'ri so'rov
            return db.WAREHOUSES
                .AsNoTracking()
                .Where(w => (w.IsDeleted == null || w.IsDeleted == false) && w.WarehouseType == warehouseType)
                .OrderBy(w => w.ID)
                .FirstOrDefault();
        }

        private sealed class StockLine
        {
            public int ProductId { get; set; }
            public PRODUCT Product { get; set; }
            public decimal Qty { get; set; }
            public string UnitShortName { get; set; }
        }

        private static List<StockLine> GetStockLines(DBTHSNEntities db, int warehouseId, Func<PRODUCT, bool> productFilter)
        {
            var qtyRows = InventoryValuationService.GetPositiveStocks(db, warehouseId);
            if (qtyRows.Count == 0)
                return new List<StockLine>();

            var ids = qtyRows.Select(q => q.ProductID).Distinct().ToList();
            var products = db.PRODUCTS.AsNoTracking()
                .Include(x => x.PRODUCT_CATEGORIES)
                .Include(x => x.UNIT)
                .Where(x => ids.Contains(x.ID))
                .ToList()
                .ToDictionary(x => x.ID);

            var lines = new List<StockLine>();
            foreach (var q in qtyRows)
            {
                if (!products.TryGetValue(q.ProductID, out var p) || p.IsDeleted || !productFilter(p))
                    continue;
                lines.Add(new StockLine
                {
                    ProductId = p.ID,
                    Product = p,
                    Qty = q.Qty,
                    UnitShortName = p.UNIT != null ? p.UNIT.ShortName : ""
                });
            }
            return lines;
        }

        private static bool IsFinishedGood(PRODUCT p)
        {
            if (p.IsFinishedGood == true)
                return true;
            return p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.IsFinishedGood;
        }

        private static bool IsJihozProductCategory(PRODUCT p)
        {
            return p.PRODUCT_CATEGORIES?.SystemCode == SystemCodes.ProductCategory.Equipment;
        }

        private static decimal GetIncomeProductUnitPrice(INCOME_PRODUCTS ip)
        {
            if (ip.TotalPrice.HasValue && ip.TotalPrice.Value > 0 && ip.Amount > 0)
                return (decimal)(ip.TotalPrice.Value / ip.Amount);

            if (ip.PiecePrice.HasValue && ip.PiecePrice.Value > 0)
                return (decimal)ip.PiecePrice.Value;

            return 0m;
        }
    }
}
