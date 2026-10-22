using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;
using tahsinERP.Services.DlocFifo;
using tahsinERP.ViewModels;
using tahsinERP.I18n;

namespace tahsinERP.Services.Stock
{
    /// <summary>
    /// Kirim hujjati qatorlari bo'yicha umumiy qoidalar (IncomesController Create/Edit'dan ajratilgan — SRM-M01).
    ///
    /// QQS: stavka INCOME_PRODUCTS.VATRate ustunida (SRM-M04; avval izohga "QQS %: n%" deb yozilib, regex bilan
    /// qayta o'qilardi). INCOME_PRODUCTS.TotalPrice bazada hisoblanadigan ustun (Amount × PiecePrice, QQSsiz);
    /// INCOMES.TotalPrice — QQS bilan jami (moliya solishtirmasi shundan o'qiydi).
    /// </summary>
    public static class IncomeDocumentService
    {
        /// <summary>Qatorning QQS bilan summasi.</summary>
        public static decimal GrossTotal(decimal amount, decimal? piecePrice, decimal vatRate)
        {
            return VatMath.ApplyVat(amount * (piecePrice ?? 0), VatMath.ClampPercent(vatRate));
        }

        /// <summary>
        /// Qator birligi: kesiladigan mahsulot — dona (1), aks holda formadagi birlik yoki mahsulot birligi.
        /// (Avval Edit'da kesish qoidasi qo'llanmasdi.) Qoldiqqa yozishda StockLedger qoldiq birligiga o'giradi (SRM-B14).
        /// </summary>
        public static async Task<int> ResolveUnitIdAsync(UnitConversionService conversion, int productId, int? lineUnitId, int? productUnitId)
        {
            if (await conversion.RequiresCuttingAsync(productId))
                return 1;
            if (lineUnitId.HasValue && lineUnitId.Value > 0)
                return lineUnitId.Value;
            return productUnitId.HasValue && productUnitId.Value > 0 ? productUnitId.Value : 1;
        }

        /// <summary>Formadagi qatordan INCOME_PRODUCTS (QQS stavkasi ustunda, izoh foydalanuvchiniki).</summary>
        public static INCOME_PRODUCTS NewLine(int incomeId, IncomeProductViewModel line, int unitId)
        {
            return new INCOME_PRODUCTS
            {
                IncomeID = incomeId,
                ProductID = line.ProductID,
                Amount = line.Amount,
                PiecePrice = line.PiecePrice,
                UnitID = unitId,
                VATRate = (decimal?)(VatMath.ClampPercent(line.VATRate)),
                Comment = string.IsNullOrWhiteSpace(line.Comment) ? null : line.Comment.Trim()
            };
        }

        /// <summary>
        /// DLOC omborga kirim: har qatorda joy tanlangani, joy sig'imi (bir joyga tushgan qatorlar yig'indisi bilan)
        /// tekshiriladi. Xatolar ro'yxati qaytariladi (bo'sh — hammasi joyida).
        /// </summary>
        public static List<string> ValidatePutaway(DBTHSNEntities db, IEnumerable<IncomeProductViewModel> lines)
        {
            var errors = new List<string>();
            var pendingQtyByLocation = new Dictionary<int, decimal>();
            foreach (var line in lines ?? Enumerable.Empty<IncomeProductViewModel>())
            {
                if (!line.LocationID.HasValue || line.LocationID.Value <= 0)
                {
                    errors.Add(L.T("Wh_Err_DlocRequiredEachLine"));
                    continue;
                }

                try
                {
                    DlocCapacityGuard.ValidatePutaway(db, line.LocationID.Value, line.ProductID, line.Amount);
                }
                catch (WarehouseDlocException wex)
                {
                    errors.Add(wex.Message);
                    continue;
                }

                var locId = line.LocationID.Value;
                decimal pending;
                pendingQtyByLocation.TryGetValue(locId, out pending);
                pending += line.Amount;
                pendingQtyByLocation[locId] = pending;

                var loc = DlocFifoDataAccess.GetLocation(db, locId);
                if (loc != null)
                {
                    var after = DlocFifoDataAccess.GetLocationCurrentQty(db, locId) + pending;
                    if (after > loc.MaxCapacity)
                        errors.Add(L.T("Wh_Err_DlocCapacityExceeded", loc.LocationCode, loc.MaxCapacity.ToString("N2"), after.ToString("N2")));
                }
            }
            return errors.Distinct().ToList();
        }
    }
}
