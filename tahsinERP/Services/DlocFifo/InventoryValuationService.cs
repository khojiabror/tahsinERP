using System.Collections.Generic;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services.DlocFifo
{
    /// <summary>
    /// Single entry for inventory quantity — ledger first, PRODUCT_STOCKS fallback. Never throws.
    /// </summary>
    public static class InventoryValuationService
    {
        public static double GetQty(DBTHSNEntities db, int warehouseId, int productId)
        {
            if (DlocFifoSchema.IsEnabled(db))
            {
                var ledger = (double)DlocFifoDataAccess.GetLedgerQty(db, warehouseId, productId);
                if (ledger > 0.0000001)
                    return ledger;
            }

            var agg = db.PRODUCT_STOCKS
                .Where(s => s.WHID == warehouseId && s.ProductID == productId)
                .Select(s => (double?)s.Amount)
                .Sum();

            return agg ?? 0;
        }

        public static List<StockQtyRow> GetPositiveStocks(DBTHSNEntities db, int warehouseId)
        {
            var result = new Dictionary<int, double>();

            if (DlocFifoSchema.IsEnabled(db))
            {
                const string sql = @"
SELECT ll.ProductID, SUM(CAST(ll.QtyAvailable AS FLOAT)) AS Qty
FROM dbo.LOT_LOCATIONS ll
INNER JOIN dbo.WAREHOUSE_LOCATIONS loc ON loc.ID = ll.LocationID AND loc.IsDeleted = 0
WHERE loc.WarehouseID = @p0 AND ll.QtyAvailable > 0
GROUP BY ll.ProductID";
                foreach (var row in db.Database.SqlQuery<StockQtyRow>(sql, warehouseId))
                    result[row.ProductID] = row.Qty;
            }

            var aggRows = db.PRODUCT_STOCKS
                .Where(s => s.WHID == warehouseId && s.Amount > 0)
                .GroupBy(s => s.ProductID)
                .Select(g => new { ProductID = g.Key, Qty = g.Sum(x => x.Amount) })
                .ToList();

            foreach (var row in aggRows)
            {
                if (!result.ContainsKey(row.ProductID) || result[row.ProductID] <= 0)
                    result[row.ProductID] = row.Qty;
            }

            return result
                .Where(kv => kv.Value > 0)
                .Select(kv => new StockQtyRow { ProductID = kv.Key, Qty = kv.Value })
                .ToList();
        }

        public class StockQtyRow
        {
            public int ProductID { get; set; }
            public double Qty { get; set; }
        }
    }
}
