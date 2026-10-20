using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;
using tahsinERP.Services.DlocFifo;

namespace tahsinERP.Services.Stock
{
    /// <summary>Inventarizatsiya sanog'i qatori (Excel'dan tekshirilgan).</summary>
    public sealed class InventoryCountLine
    {
        public int RowNumber { get; set; }
        public string ProductPNo { get; set; }
        public int ProductID { get; set; }
        public int UnitID { get; set; }
        public decimal Quantity { get; set; }
    }

    /// <summary>
    /// Inventarizatsiyani qoldiqqa yozish (InventoryController.Adjust'dan ajratilgan — SRM-M01).
    /// Bitta inventarizatsiya = oylik qulf (INVENTORY_ADJUSTMENTS, SRM-P07) + bitta INV kirim (ortiqcha) +
    /// bitta INV chiqim (kamomad, FIFO tannarxida — SRM-B19). Qoldiq, narxlar va DLOC joylari to'plab o'qiladi (SRM-P06).
    /// Chaqiruvchi tranzaksiyasida ishlaydi; xatoda <see cref="InvalidOperationException"/>.
    /// </summary>
    public static class InventoryAdjustmentService
    {
        public const string AlreadyAdjustedMessage =
            "Ushbu ombor uchun joriy oyda inventarizatsiya allaqachon kiritilgan. Oyiga faqat bir marta ruxsat etiladi.";

        /// <summary>Joriy oyda inventarizatsiya qilingan omborlar (bitta so'rov).</summary>
        public static Task<List<int>> GetLockedWarehouseIdsAsync(DBTHSNEntities db, List<int> warehouseIds)
        {
            if (warehouseIds == null || warehouseIds.Count == 0)
                return Task.FromResult(new List<int>());

            var period = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            return db.INVENTORY_ADJUSTMENTS
                .AsNoTracking()
                .Where(a => a.Period == period && warehouseIds.Contains(a.WarehouseID))
                .Select(a => a.WarehouseID)
                .ToListAsync();
        }

        /// <param name="unitCosts">Ixtiyoriy: ortiqcha uchun tovar tannarxi (masalan, boshlang'ich sanoqda kirim tarixi yo'q tovar); bo'lmasa — oxirgi kirim narxi.</param>
        /// <returns>INVENTORY_ADJUSTMENTS.ID</returns>
        public static async Task<int> ApplyAsync(DBTHSNEntities db, int warehouseId, int? userId, IList<InventoryCountLine> lines,
            IDictionary<int, decimal> unitCosts = null)
        {
            var requireDloc = StockLedger.IsDlocWarehouse(db, warehouseId);
            var currencyId = WarehouseAccess.DefaultCurrencyId(db);
            var now = DateTime.Now;

            // Qulf tranzaksiya ichida birinchi yoziladi: parallel ikkinchi urinish unique indeksga uriladi
            var adjustmentLock = new INVENTORY_ADJUSTMENTS
            {
                WarehouseID = warehouseId,
                Period = new DateTime(now.Year, now.Month, 1),
                UserID = userId > 0 ? userId : null,
                CreatedAt = now
            };
            db.INVENTORY_ADJUSTMENTS.Add(adjustmentLock);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException)
            {
                throw new InvalidOperationException(AlreadyAdjustedMessage);
            }

            var invToken = $"{now.Month:00}_{now.Day:00}_{{0}}_{warehouseId}_{now:HHmmssfff}";

            // Ombor qoldig'i bitta qulflangan so'rovda
            var lockedOnHand = StockLedger.GetOnHandForUpdate(db, warehouseId);
            var diffs = new List<Tuple<InventoryCountLine, decimal>>();
            foreach (var line in lines)
            {
                decimal oldQty;
                lockedOnHand.TryGetValue(line.ProductID, out oldQty);
                var diff = line.Quantity - oldQty;
                if (Math.Abs(diff) >= StockLedger.Epsilon)
                    diffs.Add(Tuple.Create(line, diff));
            }

            var plus = diffs.Where(d => d.Item2 > 0).ToList();
            var minus = diffs.Where(d => d.Item2 < 0).ToList();

            if (plus.Any())
            {
                // Ortiqcha — oxirgi kirim narxida (QQSsiz), barcha mahsulotlar bitta so'rovda
                var prices = await GetLatestIncomePricesAsync(db, plus.Select(d => d.Item1.ProductID).ToList());
                Func<int, decimal> priceOf = id => { decimal p; if (unitCosts != null && unitCosts.TryGetValue(id, out p)) return p; prices.TryGetValue(id, out p); return p; };

                var income = new INCOME
                {
                    DocNo = "INV_" + string.Format(invToken, "IN"),
                    ReceivingWHID = warehouseId,
                    IssueDateTime = now,
                    CurrencyID = currencyId,
                    Amount = plus.Sum(d => d.Item2),
                    TotalPrice = plus.Sum(d => d.Item2 * priceOf(d.Item1.ProductID)),
                    Description = $"Inventarizatsiya qo'shimcha kirim: {plus.Count} ta mahsulot",
                    RecieveStatus = true,
                    IsDeleted = false
                };
                db.INCOMES.Add(income);
                var incomeLines = plus.Select(d => new INCOME_PRODUCTS
                {
                    INCOME = income,
                    ProductID = d.Item1.ProductID,
                    Amount = d.Item2,
                    PiecePrice = priceOf(d.Item1.ProductID),
                    UnitID = d.Item1.UnitID,
                    VATRate = 0,
                    Comment = "Inventarizatsiya"
                }).ToList();
                db.INCOME_PRODUCTS.AddRange(incomeLines);
                await db.SaveChangesAsync();
                adjustmentLock.IncomeID = income.ID;

                var putaway = requireDloc ? new AutoPutawayPlanner(db, warehouseId) : null;
                for (int i = 0; i < plus.Count; i++)
                {
                    var d = plus[i];
                    int? locationId = null;
                    if (requireDloc)
                    {
                        // Qo'shilgan miqdor DLOC ga avtomatik joylashtiriladi
                        locationId = putaway.Resolve(d.Item1.ProductID, d.Item2);
                        if (locationId <= 0)
                            throw new InvalidOperationException(
                                $"Mahsulot {d.Item1.ProductPNo}: qo'shimcha miqdor uchun bo'sh DLOC joy topilmadi (sig'im yoki aralashtirish cheklovi).");
                    }

                    StockLedger.ReceiveLine(db, warehouseId, income.ID, incomeLines[i].ID, d.Item1.ProductID,
                        d.Item2, d.Item1.UnitID, locationId, now, "INV");
                }
            }

            if (minus.Any())
            {
                var expense = new EXPENS
                {
                    DocNo = "INV_" + string.Format(invToken, "OUT"),
                    SenderWHID = warehouseId,
                    IssueDateTime = now,
                    CurrencyID = currencyId,
                    Amount = minus.Sum(d => -d.Item2),
                    Description = $"Inventarizatsiya kamaytirish chiqimi: {minus.Count} ta mahsulot",
                    SendStatus = true,
                    IsDeleted = false
                };
                db.EXPENSES.Add(expense);
                var expenseLines = minus.Select(d => new EXPENSE_PRODUCTS
                {
                    EXPENS = expense,
                    ProductID = d.Item1.ProductID,
                    Amount = -d.Item2,
                    PiecePrice = 0,
                    UnitID = d.Item1.UnitID,
                    Comment = "Inventarizatsiya"
                }).ToList();
                db.EXPENSE_PRODUCTS.AddRange(expenseLines);
                await db.SaveChangesAsync();
                adjustmentLock.ExpenseID = expense.ID;

                // Kamomad: DLOC omborda FIFO lotlardan, oddiy omborda umumiy qoldiqdan; narxi FIFO tannarxi
                foreach (var ep in expenseLines)
                {
                    await StockLedger.IssueLineAsync(db, warehouseId, ep.ProductID, ep.Amount, ep.UnitID, ep.ID);
                    ep.PiecePrice = StockCostService.StoredUnitCost(StockCostService.IssuedLineUnitCost(db, warehouseId, ep.ProductID, ep.Amount, ep.UnitID, ep.ID));
                }
                expense.TotalPrice = expenseLines.Sum(ep => ep.Amount * ep.PiecePrice);
            }

            await db.SaveChangesAsync();
            return adjustmentLock.ID;
        }

        /// <summary>Har mahsulotning eng so'nggi (o'chirilmagan) kirim narxi, QQSsiz — bitta so'rov.</summary>
        private static async Task<Dictionary<int, decimal>> GetLatestIncomePricesAsync(DBTHSNEntities db, List<int> productIds)
        {
            var result = new Dictionary<int, decimal>();
            if (productIds == null || productIds.Count == 0)
                return result;

            var latest = await db.INCOME_PRODUCTS
                .AsNoTracking()
                .Where(x => productIds.Contains(x.ProductID) && (x.INCOME.IsDeleted == null || x.INCOME.IsDeleted == false))
                .GroupBy(x => x.ProductID)
                .Select(g => g.OrderByDescending(x => x.INCOME.IssueDateTime)
                    .Select(x => new { x.ProductID, x.PiecePrice, x.TotalPrice, x.Amount })
                    .FirstOrDefault())
                .ToListAsync();

            foreach (var ip in latest.Where(x => x != null))
            {
                decimal price = 0;
                if (ip.PiecePrice.HasValue && ip.PiecePrice.Value > 0)
                    price = ip.PiecePrice.Value;
                else if (ip.TotalPrice.HasValue && ip.Amount > 0)
                    price = ip.TotalPrice.Value / ip.Amount; // qator TotalPrice = Amount × PiecePrice (QQSsiz)
                result[ip.ProductID] = price;
            }
            return result;
        }

        /// <summary>
        /// DLOC omborda qo'shimcha miqdor uchun joy: avval mahsulot turgan, sig'imi yetadigan joy; bo'lmasa bo'sh yoki
        /// aralash mahsulotga ruxsat etilgan joy. Joylar va qoldiqlar bir marta o'qiladi va xotirada yangilanadi.
        /// </summary>
        private sealed class AutoPutawayPlanner
        {
            private readonly List<WarehouseLocationDto> _locations;
            private readonly Dictionary<int, decimal> _qtyByLocation;
            private readonly Dictionary<int, HashSet<int>> _locationsByProduct;

            public AutoPutawayPlanner(DBTHSNEntities db, int warehouseId)
            {
                _locations = DlocFifoDataAccess.GetLocations(db, warehouseId, activeOnly: true);
                _qtyByLocation = DlocFifoDataAccess.GetLocationQtyMap(db, warehouseId);
                _locationsByProduct = DlocFifoDataAccess.GetProductLocationIds(db, warehouseId);
            }

            public int Resolve(int productId, decimal qty)
            {
                HashSet<int> productLocationIds;
                if (!_locationsByProduct.TryGetValue(productId, out productLocationIds))
                    productLocationIds = new HashSet<int>();

                int chosen = 0;
                int fallbackLocationId = 0;
                foreach (var loc in _locations)
                {
                    decimal currentQty;
                    _qtyByLocation.TryGetValue(loc.ID, out currentQty);
                    if (loc.MaxCapacity - currentQty + 0.000001m < qty)
                        continue;

                    if (productLocationIds.Contains(loc.ID))
                    {
                        chosen = loc.ID;
                        break;
                    }

                    if (fallbackLocationId == 0 && (loc.AllowMixedProducts || currentQty <= 0.000001m))
                        fallbackLocationId = loc.ID;
                }

                if (chosen == 0)
                    chosen = fallbackLocationId;
                if (chosen > 0)
                {
                    decimal current;
                    _qtyByLocation.TryGetValue(chosen, out current);
                    _qtyByLocation[chosen] = current + qty;
                    productLocationIds.Add(chosen);
                    _locationsByProduct[productId] = productLocationIds;
                }
                return chosen;
            }
        }
    }
}
