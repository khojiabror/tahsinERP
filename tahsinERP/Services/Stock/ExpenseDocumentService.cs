using System;
using System.Collections.Generic;
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
    }
}
