using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services.DlocFifo
{
    /// <summary>
    /// PRODUCT_STOCKS ni LOT_LOCATIONS yig'indisiga moslashtirish (faqat repair/inventarizatsiya).
    /// Kundalik operatsiyalar <see cref="WarehouseFlowService.AdjustProductStock"/> ishlatadi.
    /// </summary>
    public static class StockLedgerRollupService
    {
        /// <summary>DLOC lot yig'indisini PRODUCT_STOCKS ga yozadi (moslashtirish).</summary>
        public static void SyncProductStock(DBTHSNEntities db, int warehouseId, int productId, int unitId)
        {
            if (!DlocFifoSchema.IsEnabled(db))
                return;

            var ledgerQty = (double)DlocFifoDataAccess.GetLedgerQty(db, warehouseId, productId);
            var stock = WarehouseFlowService.GetOrCreateStockRow(db, warehouseId, productId, unitId);
            stock.Amount = ledgerQty;
        }

        public static void RepairWarehouseProduct(DBTHSNEntities db, int warehouseId, int productId, int unitId)
        {
            SyncProductStock(db, warehouseId, productId, unitId);
        }

        public static void RepairAll(DBTHSNEntities db)
        {
            if (!DlocFifoSchema.IsEnabled(db))
                return;

            const string sql = @"
SELECT DISTINCT loc.WarehouseID AS WHID, ll.ProductID, p.UnitID
FROM dbo.LOT_LOCATIONS ll
INNER JOIN dbo.WAREHOUSE_LOCATIONS loc ON loc.ID = ll.LocationID
INNER JOIN dbo.PRODUCTS p ON p.ID = ll.ProductID
WHERE ll.QtyAvailable > 0";

            var keys = db.Database.SqlQuery<RollupKey>(sql).ToList();
            foreach (var k in keys)
                SyncProductStock(db, k.WHID, k.ProductID, k.UnitID > 0 ? k.UnitID : 1);
        }

        private class RollupKey
        {
            public int WHID { get; set; }
            public int ProductID { get; set; }
            public int UnitID { get; set; }
        }
    }
}
