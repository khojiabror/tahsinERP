using System;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;
using tahsinERP.Services.DlocFifo;

namespace tahsinERP.Services
{
    /// <summary>
    /// Chiqimda lot/DLOC: kirim loti yo'q bo'lsa umumiy qoldiqdan chiqim.
    /// </summary>
    public static class ExpenseStockIssueHelper
    {
        public const int LegacyIssueIncomeId = 0;
        private const double QtyEpsilon = 1e-6;

        public class LegacyIssueInfo
        {
            public bool CanUseLegacy { get; set; }
            public double StockQty { get; set; }
            public double LedgerQty { get; set; }
            public double UnassignedQty { get; set; }
            public string Label { get; set; }
        }

        public static async Task<LegacyIssueInfo> GetLegacyIssueInfoAsync(DBTHSNEntities db, int warehouseId, int productId)
        {
            var info = new LegacyIssueInfo();
            var incomeLots = await LotTrackingService.GetAvailableIncomeLotsAsync(warehouseId, productId, db);
            if (incomeLots.Any())
                return info;

            info.StockQty = InventoryValuationService.GetQty(db, warehouseId, productId);
            if (info.StockQty <= QtyEpsilon)
                return info;

            if (DlocFifoSchema.IsEnabled(db) && DlocFifoDataAccess.WarehouseHasLocations(db, warehouseId))
            {
                info.LedgerQty = (double)DlocFifoDataAccess.GetLedgerQty(db, warehouseId, productId);
                info.UnassignedQty = Math.Max(0, info.StockQty - info.LedgerQty);
                if (info.UnassignedQty <= QtyEpsilon)
                    return info;
            }
            else
            {
                info.UnassignedQty = info.StockQty;
            }

            info.CanUseLegacy = true;
            info.Label = info.LedgerQty > QtyEpsilon
                ? $"--- Lot siz (joylashtirilmagan: {info.UnassignedQty:N2}) ---"
                : $"--- Lot siz (umumiy qoldiq: {info.UnassignedQty:N2}) ---";
            return info;
        }

        public static bool IsLegacyIssueSelection(int? incomeId)
        {
            return !incomeId.HasValue || incomeId.Value <= 0;
        }

        public static async Task<string> ValidateLegacyIssueAsync(DBTHSNEntities db, int warehouseId, int productId, double qty)
        {
            var info = await GetLegacyIssueInfoAsync(db, warehouseId, productId);
            if (!info.CanUseLegacy)
            {
                var incomeLots = await LotTrackingService.GetAvailableIncomeLotsAsync(warehouseId, productId, db);
                if (incomeLots.Any())
                    return "Kirim lotini tanlang.";
                if (info.StockQty <= QtyEpsilon)
                    return "Omborda qoldiq yo'q.";
                return "Barcha qoldiq DLOC/lot da. Avval «DLOC ga joylashtirish» yoki lot bo'yicha chiqim qiling.";
            }

            if (qty > info.UnassignedQty + QtyEpsilon)
            {
                return info.LedgerQty > QtyEpsilon
                    ? $"Lot siz chiqim uchun maksimum {info.UnassignedQty:N2} (joylashtirilmagan qoldiq). DLOC da: {info.LedgerQty:N2}"
                    : $"Lot siz chiqim miqdori qoldiqdan oshib ketdi. Maksimum: {info.UnassignedQty:N2}";
            }

            return null;
        }

        /// <summary>Chiqim qatorini ombordan chiqarish (lot tanlovi bo'yicha).</summary>
        public static async Task ApplyIssueForExpenseProductAsync(DBTHSNEntities db, int warehouseId,
            int productId, double amount, int unitId, int expenseProductId, int? incomeId)
        {
            if (IsLegacyIssueSelection(incomeId))
            {
                var err = await ValidateLegacyIssueAsync(db, warehouseId, productId, amount);
                if (err != null)
                    throw new InvalidOperationException(err);

                WarehouseFlowService.ApplyIncomeIssueFromStock(db, warehouseId, productId, amount, unitId);
                return;
            }

            if (DlocFifoSchema.IsEnabled(db) && DlocFifoDataAccess.WarehouseHasLocations(db, warehouseId))
            {
                try
                {
                    if (LocationLotStockLedger.TryIssueFifo(
                        db, warehouseId, productId, (decimal)amount, unitId, expenseProductId, out _))
                    {
                        return;
                    }
                }
                catch (WarehouseDlocException ex) when (ex.ErrorCode == "INSUFFICIENT_STOCK")
                {
                    var info = await GetLegacyIssueInfoAsync(db, warehouseId, productId);
                    if (info.CanUseLegacy && amount <= info.UnassignedQty + QtyEpsilon)
                    {
                        WarehouseFlowService.ApplyIncomeIssueFromStock(db, warehouseId, productId, amount, unitId);
                        return;
                    }

                    throw;
                }

                var legacyErr = await ValidateLegacyIssueAsync(db, warehouseId, productId, amount);
                if (legacyErr == null)
                {
                    WarehouseFlowService.ApplyIncomeIssueFromStock(db, warehouseId, productId, amount, unitId);
                    return;
                }

                throw new InvalidOperationException(legacyErr);
            }

            WarehouseFlowService.ApplyIncomeIssueFromStock(db, warehouseId, productId, amount, unitId);
        }

        /// <summary>
        /// Avtomatik chiqim (ishlab chiqarish BOM, o'tkazma): avval DLOC FIFO, keyin joylashtirilmagan qoldiq.
        /// </summary>
        public static async Task ApplyAutoIssueForExpenseProductAsync(DBTHSNEntities db, int warehouseId,
            int productId, double amount, int unitId, int expenseProductId)
        {
            if (amount <= QtyEpsilon)
                return;

            var stockQty = InventoryValuationService.GetQty(db, warehouseId, productId);
            if (stockQty + QtyEpsilon < amount)
            {
                throw new InvalidOperationException(
                    $"Omborda yetarli qoldiq yo'q. Mavjud: {stockQty:N3}, Talab: {amount:N3}");
            }

            if (DlocFifoSchema.IsEnabled(db) && DlocFifoDataAccess.WarehouseHasLocations(db, warehouseId))
            {
                try
                {
                    if (LocationLotStockLedger.TryIssueFifo(
                        db, warehouseId, productId, (decimal)amount, unitId, expenseProductId, out _))
                    {
                        return;
                    }
                }
                catch (WarehouseDlocException ex) when (ex.ErrorCode == "INSUFFICIENT_STOCK")
                {
                    var ledger = (double)DlocFifoDataAccess.GetLedgerQty(db, warehouseId, productId);
                    var unassigned = Math.Max(0, stockQty - ledger);
                    if (amount <= unassigned + QtyEpsilon)
                    {
                        WarehouseFlowService.ApplyIncomeIssueFromStock(db, warehouseId, productId, amount, unitId);
                        return;
                    }

                    throw new InvalidOperationException(
                        $"DLOC lot qoldig'i yetarli emas. DLOC da: {ledger:N3}, joylashtirilmagan: {unassigned:N3}, talab: {amount:N3}. " +
                        "«DLOC ga joylashtirish» yoki lot bo'yicha chiqim qiling.");
                }

                var legacyErr = await ValidateLegacyIssueAsync(db, warehouseId, productId, amount);
                if (legacyErr == null)
                {
                    WarehouseFlowService.ApplyIncomeIssueFromStock(db, warehouseId, productId, amount, unitId);
                    return;
                }

                throw new InvalidOperationException(legacyErr);
            }

            WarehouseFlowService.ApplyIncomeIssueFromStock(db, warehouseId, productId, amount, unitId);
        }

        /// <summary>Sync oqimlar uchun (TransferProductionToSales).</summary>
        public static void ApplyAutoIssueForExpenseProduct(DBTHSNEntities db, int warehouseId,
            int productId, double amount, int unitId, int expenseProductId)
        {
            ApplyAutoIssueForExpenseProductAsync(db, warehouseId, productId, amount, unitId, expenseProductId)
                .GetAwaiter()
                .GetResult();
        }

        private class AllocationReverseRow
        {
            public int LotID { get; set; }
            public int FromLocationID { get; set; }
            public decimal Qty { get; set; }
        }

        /// <summary>Avtomatik chiqimni bekor qilish (invoys o'chirish va h.k.).</summary>
        public static void ReverseExpenseProductIssue(DBTHSNEntities db, int warehouseId, int productId,
            double amount, int unitId, int expenseProductId)
        {
            if (amount <= QtyEpsilon)
                return;

            if (DlocFifoSchema.IsEnabled(db))
            {
                const string sel = @"
SELECT LotID, FromLocationID, Qty
FROM dbo.EXPENSE_PRODUCT_ALLOCATIONS
WHERE ExpenseProductID = @p0";
                var rows = db.Database.SqlQuery<AllocationReverseRow>(sel, expenseProductId).ToList();
                foreach (var r in rows)
                {
                    const string merge = @"
IF EXISTS (SELECT 1 FROM dbo.LOT_LOCATIONS WHERE LotID=@p0 AND LocationID=@p1)
    UPDATE dbo.LOT_LOCATIONS SET QtyAvailable = QtyAvailable + @p2, LastMovementAt = GETDATE(), ProductID = @p3
    WHERE LotID=@p0 AND LocationID=@p1
ELSE
    INSERT INTO dbo.LOT_LOCATIONS (LotID, LocationID, ProductID, QtyAvailable, LastMovementAt)
    VALUES (@p0, @p1, @p3, @p2, GETDATE())";
                    db.Database.ExecuteSqlCommand(merge, r.LotID, r.FromLocationID, r.Qty, productId);

                    const string unconsume = @"
UPDATE dbo.INCOME_LOTS SET QtyConsumed = QtyConsumed - @p1
WHERE ID = @p0 AND QtyConsumed >= @p1";
                    db.Database.ExecuteSqlCommand(unconsume, r.LotID, r.Qty);
                }

                if (rows.Any())
                {
                    db.Database.ExecuteSqlCommand(
                        "DELETE FROM dbo.EXPENSE_PRODUCT_ALLOCATIONS WHERE ExpenseProductID = @p0",
                        expenseProductId);
                }
            }

            WarehouseFlowService.ApplyIncomeReceiveToStock(db, warehouseId, productId, amount, unitId);
        }
    }
}
