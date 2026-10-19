using System;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.Services.DlocFifo;

namespace tahsinERP.Services.Stock
{
    /// <summary>
    /// Chiqim tannarxi — FIFO, QQSsiz (SRM-B19 qarori, 2026-10-18). Avval chiqim narxini foydalanuvchi qo'lda
    /// kiritardi (yoki o'tkazmada sotuv shartnomasi narxi olinardi), shu sabab oylik hisobotning "summa" rejimi
    /// ma'nosiz edi.
    ///
    /// Hisob (<see cref="StockLedger.IssueLine"/>dan KEYIN chaqiriladi):
    ///  1. DLOC ajratmalari bor qism — aniq lot tannarxi: kirim qatori (Amount × PiecePrice) / lot QtyReceived;
    ///  2. qolgan qism (oddiy ombor yoki joylashtirilmagan qoldiq) — FIFO qatlamlari: omborga qabul qilingan
    ///     kirimlar eng yangisidan boshlab, chiqimdan keyin omborda qolgan miqdor (ular eng yangi qatlamlar)
    ///     o'tkazib yuboriladi, keyingi qatlamlardan chiqim miqdori olinadi.
    /// Kirim qatori narxi (PiecePrice) QQSsiz; QQS INCOME_PRODUCTS.VATRate'da alohida.
    /// </summary>
    public static class StockCostService
    {
        private sealed class AllocationCost
        {
            public decimal Qty { get; set; }
            public double Cost { get; set; }
        }

        private sealed class ReceiptLayer
        {
            public int ProductID { get; set; }
            public double Amount { get; set; }
            public int UnitID { get; set; }
            public double Price { get; set; }
        }

        /// <summary>
        /// Saqlanadigan dona tannarxi: EXPENSE_PRODUCTS.PiecePrice — decimal(19,6). EF ortiqcha xonani kesadi, shuning uchun
        /// shu yerda yaxlitlanadi — hujjat jami (C#) va qator TotalPrice (bazada Amount × PiecePrice) bir xil narxdan hisoblanadi.
        /// </summary>
        public static decimal StoredUnitCost(double unitCost)
        {
            if (double.IsNaN(unitCost) || double.IsInfinity(unitCost)) return 0m;
            return Math.Round((decimal)unitCost, 6, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Chiqim qatorining FIFO tannarxi: qator birligidagi dona narxi (qator TotalPrice = Amount × PiecePrice
        /// bazada hisoblanadi). Narx topilmasa 0.
        /// </summary>
        public static double IssuedLineUnitCost(DBTHSNEntities db, int warehouseId, int productId,
            double lineQty, int lineUnitId, int expenseProductId)
        {
            if (lineQty <= StockLedger.Epsilon)
                return 0;

            var qtyStock = StockLedger.ToStockUnit(db, warehouseId, productId, lineQty, lineUnitId, out var stockUnitId,
                allowReunit: false);

            double total = 0;
            double covered = 0;
            if (DlocFifoSchema.IsEnabled(db))
            {
                const string allocSql = @"
SELECT CAST(ISNULL(SUM(a.Qty), 0) AS DECIMAL(18,6)) AS Qty,
       CAST(ISNULL(SUM(a.Qty * CASE WHEN il.QtyReceived > 0
                                    THEN ip.Amount * ISNULL(ip.PiecePrice, 0) / il.QtyReceived ELSE 0 END), 0) AS FLOAT) AS Cost
FROM dbo.EXPENSE_PRODUCT_ALLOCATIONS a
JOIN dbo.INCOME_LOTS il ON il.ID = a.LotID
LEFT JOIN dbo.INCOME_PRODUCTS ip ON ip.ID = il.IncomeProductID
WHERE a.ExpenseProductID = @p0";
                var alloc = db.Database.SqlQuery<AllocationCost>(allocSql, expenseProductId).FirstOrDefault();
                if (alloc != null && alloc.Qty > 0)
                {
                    covered = (double)alloc.Qty;
                    total = alloc.Cost;
                }
            }

            var remaining = qtyStock - covered;
            if (remaining > StockLedger.Epsilon)
            {
                var onHandAfter = StockLedger.GetOnHand(db, warehouseId, productId);
                total += FifoLayerCost(db, warehouseId, productId, stockUnitId, remaining, onHandAfter);
            }

            return total / lineQty;
        }

        /// <summary>
        /// <paramref name="qty"/> (qoldiq birligida) uchun FIFO qatlam tannarxi: eng yangi
        /// <paramref name="newerQty"/> birlik (omborda qolgan) o'tkazib yuboriladi.
        /// </summary>
        private static double FifoLayerCost(DBTHSNEntities db, int warehouseId, int productId, int stockUnitId,
            double qty, double newerQty)
        {
            // Omborga haqiqatda qabul qilingan kirimlar (DLOC-ASN — mavjud qoldiqni joyga bog'lash, kirim emas)
            const string sql = @"
SELECT ip.ProductID, CAST(ip.Amount AS FLOAT) AS Amount, ip.UnitID,
       CAST(CASE WHEN ISNULL(ip.PiecePrice, 0) > 0 THEN ip.PiecePrice
                 WHEN ip.Amount > 0 THEN ISNULL(ip.TotalPrice, 0) / ip.Amount ELSE 0 END AS FLOAT) AS Price
FROM dbo.INCOME_PRODUCTS ip
JOIN dbo.INCOMES i ON i.ID = ip.IncomeID
WHERE i.ReceivingWHID = @p0 AND ip.ProductID = @p1 AND ip.Amount > 0
  AND ISNULL(i.IsDeleted, 0) = 0 AND i.RecieveStatus = 1
  AND (i.DocNo IS NULL OR i.DocNo NOT LIKE @p2)
ORDER BY i.IssueDateTime DESC, i.ID DESC, ip.ID DESC";
            var layers = db.Database.SqlQuery<ReceiptLayer>(sql, warehouseId, productId,
                LegacyDlocAssignmentService.AssignmentDocNoPrefix + "%").ToList();
            if (layers.Count == 0)
                return 0;

            var conversion = new UnitConversionService(db);
            double skip = Math.Max(0, newerQty);
            double need = qty;
            double cost = 0;
            double lastPrice = 0;
            foreach (var layer in layers)
            {
                // Qatlam miqdori va narxi qoldiq birligida
                double layerQty = layer.Amount;
                double price = layer.Price;
                if (stockUnitId > 0 && layer.UnitID > 0 && layer.UnitID != stockUnitId)
                {
                    var converted = conversion.TryConvert(productId, (decimal)layer.Amount, layer.UnitID, stockUnitId);
                    if (!converted.HasValue || converted.Value <= 0)
                        continue;
                    layerQty = (double)converted.Value;
                    price = layer.Price * layer.Amount / layerQty;
                }
                lastPrice = price;

                if (skip >= layerQty)
                {
                    skip -= layerQty;
                    continue;
                }

                var availableInLayer = layerQty - skip;
                skip = 0;
                var take = Math.Min(availableInLayer, need);
                cost += take * price;
                need -= take;
                if (need <= StockLedger.Epsilon)
                    break;
            }

            // Tarix to'liq bo'lmasa (qoldiq hujjatsiz kiritilgan) — eng eski ma'lum narx bilan
            if (need > StockLedger.Epsilon)
                cost += need * lastPrice;
            return cost;
        }
    }
}
