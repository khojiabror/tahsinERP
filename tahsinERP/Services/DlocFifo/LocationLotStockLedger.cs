using System;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services.DlocFifo
{
    public static class LocationLotStockLedger
    {
        /// <summary>
        /// Receive stock into DLOC; creates lot and updates ledger + PRODUCT_STOCKS rollup.
        /// If warehouse has no DLOC configured, returns false (caller uses legacy path only).
        /// </summary>
        public static bool TryReceive(DBTHSNEntities db, int warehouseId, int incomeId, int incomeProductId,
            int productId, decimal qty, int locationId, DateTime receivedAt, int unitId, string lotNoPrefix = null)
        {
            if (!DlocFifoSchema.IsEnabled(db))
                return false;

            if (!DlocFifoDataAccess.WarehouseHasLocations(db, warehouseId))
                return false;

            if (locationId <= 0)
                throw new WarehouseDlocException("DLOC_REQUIRED", "Joylashuv (DLOC) tanlanmagan.");

            DlocCapacityGuard.ValidatePutaway(db, locationId, productId, qty);

            var lotNo = string.IsNullOrWhiteSpace(lotNoPrefix)
                ? $"LOT-{incomeId}-{incomeProductId}"
                : $"{lotNoPrefix}-{incomeId}";

            const string insertLot = @"
INSERT INTO dbo.INCOME_LOTS (IncomeID, IncomeProductID, ProductID, WarehouseID, LotNo, ReceivedDateTime, QtyReceived, QtyConsumed, IsDeleted, CreatedAt)
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, 0, 0, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var lotId = db.Database.SqlQuery<int>(insertLot, incomeId, incomeProductId, productId, warehouseId,
                lotNo, receivedAt, qty).First();

            UpsertLotLocation(db, lotId, locationId, productId, qty);
            WarehouseFlowService.AdjustProductStock(db, warehouseId, productId, (double)qty, unitId);
            return true;
        }

        /// <summary>
        /// Mavjud PRODUCT_STOCKS qoldig'ini DLOC ga joylashtirish (qoldiqni qayta hisoblamaydi).
        /// </summary>
        public static void AssignExistingStockToLocation(DBTHSNEntities db, int warehouseId, int incomeId,
            int incomeProductId, int productId, decimal qty, int locationId, DateTime receivedAt, string lotNoPrefix = "OPENING")
        {
            if (!DlocFifoSchema.IsEnabled(db))
                throw new WarehouseDlocException("DLOC_DISABLED", "DLOC rejimi yoqilmagan.");

            if (!DlocFifoDataAccess.WarehouseHasLocations(db, warehouseId))
                throw new WarehouseDlocException("DLOC_NOT_CONFIGURED", "Omborda DLOC joylashuvlari sozlanmagan.");

            if (locationId <= 0)
                throw new WarehouseDlocException("DLOC_REQUIRED", "Joylashuv (DLOC) tanlanmagan.");

            DlocCapacityGuard.ValidatePutaway(db, locationId, productId, qty);

            var lotNo = string.IsNullOrWhiteSpace(lotNoPrefix)
                ? $"OPENING-{warehouseId}-{productId}-{incomeProductId}"
                : $"{lotNoPrefix}-{warehouseId}-{productId}";

            const string insertLot = @"
INSERT INTO dbo.INCOME_LOTS (IncomeID, IncomeProductID, ProductID, WarehouseID, LotNo, ReceivedDateTime, QtyReceived, QtyConsumed, IsDeleted, CreatedAt)
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, 0, 0, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var lotId = db.Database.SqlQuery<int>(insertLot, incomeId, incomeProductId, productId, warehouseId,
                lotNo, receivedAt, qty).First();

            UpsertLotLocation(db, lotId, locationId, productId, qty);
        }

        /// <summary>
        /// Issue stock using FIFO allocations; updates ledger + rollup.
        /// </summary>
        public static bool TryIssueFifo(DBTHSNEntities db, int warehouseId, int productId, decimal qty, int unitId,
            int expenseProductId, out System.Collections.Generic.List<FifoAllocationLine> allocations)
        {
            allocations = new System.Collections.Generic.List<FifoAllocationLine>();

            if (!DlocFifoSchema.IsEnabled(db))
                return false;

            if (!DlocFifoDataAccess.WarehouseHasLocations(db, warehouseId))
                return false;

            allocations = FifoAllocatorService.Allocate(db, warehouseId, productId, qty);

            foreach (var line in allocations)
            {
                DecrementLotLocation(db, line.LotID, line.LocationID, line.Qty);
                ConsumeLot(db, line.LotID, line.Qty);

                const string insAlloc = @"
INSERT INTO dbo.EXPENSE_PRODUCT_ALLOCATIONS (ExpenseProductID, LotID, FromLocationID, Qty, CreatedAt)
VALUES (@p0, @p1, @p2, @p3, GETDATE())";
                db.Database.ExecuteSqlCommand(insAlloc, expenseProductId, line.LotID, line.LocationID, line.Qty);
            }

            if (allocations.Any())
            {
                var first = allocations.First();
                const string updEp = @"
UPDATE dbo.EXPENSE_PRODUCTS SET LotID=@p0, FromLocationID=@p1, AllocationPolicy='FIFO' WHERE ID=@p2";
                db.Database.ExecuteSqlCommand(updEp, first.LotID, first.LocationID, expenseProductId);
            }

            WarehouseFlowService.AdjustProductStock(db, warehouseId, productId, -(double)qty, unitId);
            return true;
        }

        private static void UpsertLotLocation(DBTHSNEntities db, int lotId, int locationId, int productId, decimal qty)
        {
            const string merge = @"
IF EXISTS (SELECT 1 FROM dbo.LOT_LOCATIONS WHERE LotID=@p0 AND LocationID=@p1)
    UPDATE dbo.LOT_LOCATIONS SET QtyAvailable = QtyAvailable + @p2, LastMovementAt = GETDATE(), ProductID = @p3
    WHERE LotID=@p0 AND LocationID=@p1
ELSE
    INSERT INTO dbo.LOT_LOCATIONS (LotID, LocationID, ProductID, QtyAvailable, LastMovementAt)
    VALUES (@p0, @p1, @p3, @p2, GETDATE())";
            db.Database.ExecuteSqlCommand(merge, lotId, locationId, qty, productId);
        }

        private static void DecrementLotLocation(DBTHSNEntities db, int lotId, int locationId, decimal qty)
        {
            const string sql = @"
UPDATE dbo.LOT_LOCATIONS SET QtyAvailable = QtyAvailable - @p2, LastMovementAt = GETDATE()
WHERE LotID=@p0 AND LocationID=@p1 AND QtyAvailable >= @p2";
            var n = db.Database.ExecuteSqlCommand(sql, lotId, locationId, qty);
            if (n == 0)
                throw new WarehouseDlocException("INSUFFICIENT_STOCK", "DLOC qoldig‘i yetarli emas.");
        }

        private static void ConsumeLot(DBTHSNEntities db, int lotId, decimal qty)
        {
            const string sql = @"
UPDATE dbo.INCOME_LOTS SET QtyConsumed = QtyConsumed + @p1 WHERE ID = @p0";
            db.Database.ExecuteSqlCommand(sql, lotId, qty);
        }
    }
}
