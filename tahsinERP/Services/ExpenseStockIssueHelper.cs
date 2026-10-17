using System;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;
using tahsinERP.Services.DlocFifo;
using tahsinERP.Services.Stock;

namespace tahsinERP.Services
{
    /// <summary>
    /// Chiqim qatorini ombordan chiqarish va bekor qilish (StockLedger ichki amalga oshirishi).
    /// Chiqim har doim sof FIFO (SRM-B13): DLOC omborda eng eski lotdan, oddiy omborda umumiy qoldiqdan.
    /// Avvalgi "kirim lotini tanlash" (IncomeID izohda) va "lot siz" rejimlari olib tashlangan.
    /// </summary>
    public static class ExpenseStockIssueHelper
    {
        private const double QtyEpsilon = 1e-6;

        /// <summary>
        /// Chiqim (qo'lda, ishlab chiqarish BOM, o'tkazma, sotuv): DLOC omborda FIFO lotlardan; lotlarda yetmasa
        /// va butun miqdor joylashtirilmagan qoldiqqa sig'sa — o'shandan; oddiy omborda PRODUCT_STOCKS dan.
        /// Qoldiq yetmasa — xato (hech narsa yozilmaydi).
        /// </summary>
        public static void ApplyAutoIssueForExpenseProduct(DBTHSNEntities db, int warehouseId,
            int productId, double amount, int unitId, int expenseProductId)
        {
            if (amount <= QtyEpsilon)
                return;

            var stockQty = StockLedger.GetOnHand(db, warehouseId, productId);
            if (stockQty + QtyEpsilon < amount)
            {
                throw new InvalidOperationException(
                    $"Omborda yetarli qoldiq yo'q. Mavjud: {stockQty:N3}, Talab: {amount:N3}");
            }

            if (StockLedger.IsDlocWarehouse(db, warehouseId))
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
                        StockLedger.Decrease(db, warehouseId, productId, amount, unitId);
                        return;
                    }

                    throw new InvalidOperationException(
                        $"DLOC lot qoldig'i yetarli emas. DLOC da: {ledger:N3}, joylashtirilmagan: {unassigned:N3}, talab: {amount:N3}. " +
                        "Avval «DLOC ga joylashtirish» sahifasida joylashtirilmagan qoldiqni joylashtiring.");
                }
            }

            StockLedger.Decrease(db, warehouseId, productId, amount, unitId);
        }

        /// <summary>Async chaqiruvchilar uchun (ish sinxron bajariladi).</summary>
        public static Task ApplyAutoIssueForExpenseProductAsync(DBTHSNEntities db, int warehouseId,
            int productId, double amount, int unitId, int expenseProductId)
        {
            ApplyAutoIssueForExpenseProduct(db, warehouseId, productId, amount, unitId, expenseProductId);
            return Task.CompletedTask;
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

            StockLedger.Increase(db, warehouseId, productId, amount, unitId);
        }
    }
}
