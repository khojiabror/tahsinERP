using System.Collections.Generic;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.Services.Stock;

namespace tahsinERP.Services.DlocFifo
{
    /// <summary>
    /// Ombor qoldig'i miqdori uchun yagona kirish nuqtasi. Hech qachon istisno tashlamaydi.
    ///
    /// Manba — PRODUCT_STOCKS (DLOC ledger + joylashtirilmagan qoldiqning umumiy yig'indisi).
    /// Avval "ledger &gt; 0 bo'lsa ledger, aks holda PRODUCT_STOCKS" qaytarilardi: ledger tugaganda manba
    /// almashib, joylashtirilmagan qoldiq bor omborda mavjudlik noto'g'ri chiqardi (SRM-B27).
    /// Lot/joy tafsiloti kerak bo'lsa — <see cref="DlocFifoDataAccess.GetLedgerQty"/>.
    /// </summary>
    public static class InventoryValuationService
    {
        public static double GetQty(DBTHSNEntities db, int warehouseId, int productId)
        {
            return StockLedger.GetOnHand(db, warehouseId, productId);
        }

        public static List<StockQtyRow> GetPositiveStocks(DBTHSNEntities db, int warehouseId)
        {
            return db.PRODUCT_STOCKS
                .Where(s => s.WHID == warehouseId)
                .GroupBy(s => s.ProductID)
                .Select(g => new StockQtyRow { ProductID = g.Key, Qty = g.Sum(x => x.Amount) })
                .Where(r => r.Qty > 0)
                .ToList();
        }

        public class StockQtyRow
        {
            public int ProductID { get; set; }
            public double Qty { get; set; }
        }
    }
}
