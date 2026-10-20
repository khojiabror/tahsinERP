using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Services.DlocFifo;

namespace tahsinERP.Areas.Distribution.Services.Reports
{
    /// <summary>
    /// Qoldiqning pul qiymati FIFO bo'yicha: omborda qolgan miqdor — eng YANGI kirim qatlamlari (FIFO'da eskilari birinchi chiqadi).
    /// Qatlamlar va narx qoidasi <see cref="tahsinERP.Services.Stock.StockCostService"/> bilan bir xil (qabul qilingan kirimlar,
    /// DLOC-ASN emas, PiecePrice yoki TotalPrice / Amount, QQSsiz); tarix qoldiqni qoplamasa — eng eski ma'lum narx.
    /// Hammasi bitta SQL so'rovida (oyna funksiyalari). Qatlam birligi qoldiq birligidan farq qilsa, u qatlam hisobga olinmaydi
    /// (distributsiya ombori asosiy birlikda yuritiladi).
    /// </summary>
    public static class StockValuationService
    {
        public sealed class Line
        {
            public int WarehouseID { get; set; }
            public int ProductID { get; set; }
            public decimal Qty { get; set; }
            public decimal Value { get; set; }
            /// <summary>Kirim tarixi umuman yo'q — qiymati 0 deb olingan.</summary>
            public bool NoPrice { get; set; }
        }

        private const string Sql = @"
WITH stock AS (
    SELECT s.WHID, s.ProductID, s.UnitID, SUM(s.Amount) AS Qty
    FROM dbo.PRODUCT_STOCKS s
    WHERE s.WHID IN ({0}) {1}
    GROUP BY s.WHID, s.ProductID, s.UnitID
    HAVING SUM(s.Amount) > 0
), layers AS (
    SELECT st.WHID, st.ProductID, st.UnitID, st.Qty, ip.Amount,
           CASE WHEN ISNULL(ip.PiecePrice, 0) > 0 THEN CAST(ip.PiecePrice AS decimal(38, 12))
                ELSE CAST(ISNULL(ip.TotalPrice, 0) AS decimal(38, 12)) / ip.Amount END AS Price,
           SUM(ip.Amount) OVER (PARTITION BY st.WHID, st.ProductID, st.UnitID
                                ORDER BY i.IssueDateTime DESC, i.ID DESC, ip.ID DESC ROWS UNBOUNDED PRECEDING) AS Cum,
           ROW_NUMBER() OVER (PARTITION BY st.WHID, st.ProductID, st.UnitID
                              ORDER BY i.IssueDateTime ASC, i.ID ASC, ip.ID ASC) AS OldestRank
    FROM stock st
    JOIN dbo.INCOMES i ON i.ReceivingWHID = st.WHID AND ISNULL(i.IsDeleted, 0) = 0 AND i.RecieveStatus = 1
                      AND (i.DocNo IS NULL OR i.DocNo NOT LIKE @p0)
    JOIN dbo.INCOME_PRODUCTS ip ON ip.IncomeID = i.ID AND ip.ProductID = st.ProductID AND ip.UnitID = st.UnitID AND ip.Amount > 0
), taken AS (
    SELECT WHID, ProductID, UnitID, Price, OldestRank,
           CASE WHEN Cum - Amount >= Qty THEN 0 WHEN Cum <= Qty THEN Amount ELSE Qty - (Cum - Amount) END AS Take
    FROM layers
), valued AS (
    SELECT WHID, ProductID, UnitID, SUM(Take * Price) AS Covered, SUM(Take) AS CoveredQty,
           MAX(CASE WHEN OldestRank = 1 THEN Price END) AS OldestPrice
    FROM taken
    GROUP BY WHID, ProductID, UnitID
)
SELECT st.WHID AS WarehouseID, st.ProductID, CAST(SUM(st.Qty) AS decimal(19, 4)) AS Qty,
       CAST(SUM(ISNULL(v.Covered, 0) + CASE WHEN st.Qty > ISNULL(v.CoveredQty, 0) THEN (st.Qty - ISNULL(v.CoveredQty, 0)) * ISNULL(v.OldestPrice, 0) ELSE 0 END)
            AS decimal(19, 2)) AS Value,
       CAST(MAX(CASE WHEN v.WHID IS NULL THEN 1 ELSE 0 END) AS bit) AS NoPrice
FROM stock st
LEFT JOIN valued v ON v.WHID = st.WHID AND v.ProductID = st.ProductID AND v.UnitID = st.UnitID
GROUP BY st.WHID, st.ProductID";

        /// <summary>Omborlar (va ixtiyoriy tovarlar) bo'yicha qoldiq qiymati, qator — ombor × tovar.</summary>
        public static List<Line> Lines(DistributionDbContext db, IReadOnlyCollection<int> warehouseIds, IReadOnlyCollection<int> productIds = null)
        {
            if (warehouseIds == null || warehouseIds.Count == 0) return new List<Line>();
            if (productIds != null && productIds.Count == 0) return new List<Line>();
            // ID'lar butun son — matnga xavfsiz qo'yiladi (parametrlar soni cheklovidan qochish uchun)
            string wh = string.Join(",", warehouseIds.Distinct());
            string products = productIds == null ? "" : "AND s.ProductID IN (" + string.Join(",", productIds.Distinct()) + ")";
            return db.Database.SqlQuery<Line>(string.Format(Sql, wh, products),
                LegacyDlocAssignmentService.AssignmentDocNoPrefix + "%").ToList();
        }

        public sealed class WarehouseValue
        {
            public int WarehouseID { get; set; }
            public string Warehouse { get; set; }
            public int Products { get; set; }
            public decimal Value { get; set; }
            public int NoPrice { get; set; }
        }

        public static List<WarehouseValue> ByWarehouse(DistributionDbContext db, IReadOnlyCollection<int> warehouseIds)
        {
            var lines = Lines(db, warehouseIds);
            var ids = lines.Select(l => l.WarehouseID).Distinct().ToList();
            var names = db.Warehouses.Where(w => ids.Contains(w.ID)).ToDictionary(w => w.ID, w => w.WHName);
            return lines.GroupBy(l => l.WarehouseID)
                .Select(g => new WarehouseValue
                {
                    WarehouseID = g.Key, Warehouse = names.TryGetValue(g.Key, out var n) ? n : "#" + g.Key,
                    Products = g.Count(), Value = g.Sum(l => l.Value), NoPrice = g.Count(l => l.NoPrice)
                })
                .OrderByDescending(w => w.Value)
                .ToList();
        }
    }
}
