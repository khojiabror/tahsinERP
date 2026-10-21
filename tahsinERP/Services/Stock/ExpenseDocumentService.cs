using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;
using tahsinERP.Services.DlocFifo;
using tahsinERP.ViewModels;

namespace tahsinERP.Services.Stock
{
    /// <summary>
    /// Chiqim hujjati qatorlarini yozish (ExpensesController Create/Edit'dan ajratilgan — SRM-M01).
    /// Har qator: saqlanadi → ombordan sof FIFO bilan chiqariladi (SRM-B13) → narxi FIFO tannarxidan, QQSsiz
    /// olinadi (SRM-B19; formadagi narx e'tiborga olinmaydi). Chaqiruvchi tranzaksiyasida ishlaydi.
    /// </summary>
    public static class ExpenseDocumentService
    {
        /// <summary>
        /// Qatorlarni yozadi va chiqim qiladi; sarlavha Amount/TotalPrice ni to'ldiradi. Formadagi qatorlarning
        /// PiecePrice/TotalPrice ham hisoblangan tannarx bilan yangilanadi (bog'langan kirim shu narxda yaratiladi).
        /// </summary>
        public static async Task PostLinesAsync(DBTHSNEntities db, EXPENS expense, IEnumerable<ExpenseProductViewModel> lines)
        {
            decimal totalAmount = 0;
            decimal totalPrice = 0;

            foreach (var line in lines ?? new List<ExpenseProductViewModel>())
            {
                var expenseProduct = new EXPENSE_PRODUCTS
                {
                    ExpenseID = expense.ID,
                    ProductID = line.ProductID,
                    Amount = line.Amount,
                    PiecePrice = 0,
                    UnitID = line.UnitID,
                    Comment = line.Comment ?? ""
                };
                db.EXPENSE_PRODUCTS.Add(expenseProduct);
                await db.SaveChangesAsync();

                try
                {
                    await StockLedger.IssueLineAsync(db, expense.SenderWHID, line.ProductID, line.Amount,
                        line.UnitID, expenseProduct.ID);
                }
                catch (WarehouseDlocException wex)
                {
                    throw new InvalidOperationException(wex.Message, wex);
                }
                EnsureNotBelowReserved(db, expense.SenderWHID, line.ProductID);

                var unitCost = StockCostService.IssuedLineUnitCost(db, expense.SenderWHID,
                    line.ProductID, line.Amount, line.UnitID, expenseProduct.ID);
                expenseProduct.PiecePrice = StockCostService.StoredUnitCost(unitCost);
                line.PiecePrice = expenseProduct.PiecePrice;
                line.TotalPrice = line.Amount * line.PiecePrice;

                totalAmount += line.Amount;
                totalPrice += line.TotalPrice;
            }

            expense.Amount = totalAmount;
            expense.TotalPrice = totalPrice;
        }

        /// <summary>
        /// Distributsiya rezervi (dst.STOCK_RESERVATIONS — tasdiqlangan buyurtmalar uchun ushlab turilgan tovar): ombor modulidagi
        /// chiqim/ko'chirma qoldiqni rezervdan pastga tushirsa — rad (05_FINDINGS 48). Distributsiya sxemasi yo'q bazada — tekshiruvsiz.
        /// Avval distributsiyada buyurtma tasdig'ini qaytarish yoki rezervni boshqa omborga ko'chirish kerak.
        /// </summary>
        internal static void EnsureNotBelowReserved(DBTHSNEntities db, int warehouseId, int productId)
        {
            var row = db.Database.SqlQuery<ReserveCheck>(
                "IF OBJECT_ID('dst.STOCK_RESERVATIONS', 'U') IS NULL SELECT CAST(0 AS decimal(19,6)) AS Reserved, CAST(0 AS decimal(19,6)) AS OnHand ELSE " +
                "SELECT ISNULL((SELECT SUM(QtyBase) FROM dst.STOCK_RESERVATIONS WHERE WarehouseID = @p0 AND ProductID = @p1), 0) AS Reserved, " +
                "ISNULL((SELECT SUM(Amount) FROM dbo.PRODUCT_STOCKS WHERE WHID = @p0 AND ProductID = @p1), 0) AS OnHand",
                warehouseId, productId).FirstOrDefault();
            if (row != null && row.Reserved > 0 && row.OnHand + StockLedger.Epsilon < row.Reserved)
            {
                var pno = db.PRODUCTS.Where(p => p.ID == productId).Select(p => p.PNo).FirstOrDefault();
                throw new InvalidOperationException(
                    $"{pno}: distributsiya buyurtmalari uchun {row.Reserved:0.####} rezervda, chiqimdan keyin qoldiq {row.OnHand:0.####} bo'lib qoladi. " +
                    "Avval Distributsiya → Buyurtmalar'da rezervni yeching (tasdiqni qaytarish) yoki chiqim miqdorini kamaytiring.");
            }
        }

        private sealed class ReserveCheck
        {
            public decimal Reserved { get; set; }
            public decimal OnHand { get; set; }
        }
    }
}
