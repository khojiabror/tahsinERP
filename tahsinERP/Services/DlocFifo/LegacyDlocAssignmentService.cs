using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Controllers;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.Services.Stock;

namespace tahsinERP.Services.DlocFifo
{
    public class AssignDlocLineRequest
    {
        public int ProductID { get; set; }
        public int LocationID { get; set; }
        public double AssignQty { get; set; }
    }

    /// <summary>
    /// Mavjud umumiy qoldiqni DLOC lot/joyga joylashtirish (inventarizatsiya/kirimdan tashqari).
    /// Hujjat "DLOC-ASN-..." raqamli kirim sifatida yoziladi, lekin PRODUCT_STOCKS oshmaydi —
    /// qoldiq allaqachon bor, faqat ledger'ga (lot/joy) bog'lanadi.
    /// </summary>
    public static class LegacyDlocAssignmentService
    {
        /// <summary>Joylashtirish kirimlarining DocNo prefiksi (qoldiqqa ta'sir qilmaydigan kirim belgisi).</summary>
        public const string AssignmentDocNoPrefix = "DLOC-ASN-";

        private const double QtyEpsilon = 1e-6;

        /// <summary>DocNo bo'yicha: bu kirim faqat ledger'ga joylashtirishmi (qoldiqni oshirmagan).</summary>
        public static bool IsAssignmentIncome(string docNo)
        {
            return docNo != null && docNo.StartsWith(AssignmentDocNoPrefix, StringComparison.OrdinalIgnoreCase);
        }

        // Bitta so'rov: (ombor, mahsulot) bo'yicha PRODUCT_STOCKS jami − faol joylardagi ledger jami.
        // Avval har ombor uchun WarehouseHasLocations va har mahsulot uchun GetLedgerQty alohida chaqirilardi (SRM-P10).
        private const string UnassignedSql = @"
;WITH Stk AS (
    SELECT s.WHID, s.ProductID, SUM(s.Amount) AS StockQty, MAX(CASE WHEN s.UnitID > 0 THEN s.UnitID END) AS UnitID
    FROM dbo.PRODUCT_STOCKS s
    WHERE s.Amount > 0
    GROUP BY s.WHID, s.ProductID
), Led AS (
    SELECT loc.WarehouseID AS WHID, ll.ProductID, SUM(ll.QtyAvailable) AS LedgerQty
    FROM dbo.LOT_LOCATIONS ll
    JOIN dbo.WAREHOUSE_LOCATIONS loc ON loc.ID = ll.LocationID AND loc.IsDeleted = 0
    GROUP BY loc.WarehouseID, ll.ProductID
)
SELECT w.ID AS WarehouseID, w.WHName AS WarehouseName, p.ID AS ProductID,
       ISNULL(p.PNo, CAST(p.ID AS NVARCHAR(20))) AS ProductPNo, ISNULL(p.Name, N'') AS ProductName,
       CAST(stk.StockQty AS FLOAT) AS StockQty,
       CAST(ISNULL(led.LedgerQty, 0) AS FLOAT) AS LedgerQty,
       CAST(stk.StockQty - ISNULL(led.LedgerQty, 0) AS FLOAT) AS UnassignedQty,
       ISNULL(stk.UnitID, 1) AS UnitID,
       ISNULL(ISNULL(u.ShortName, u.UnitName), N'') AS UnitName
FROM Stk stk
JOIN dbo.WAREHOUSES w ON w.ID = stk.WHID AND (w.IsDeleted IS NULL OR w.IsDeleted = 0)
JOIN dbo.PRODUCTS p ON p.ID = stk.ProductID AND (p.IsDeleted IS NULL OR p.IsDeleted = 0)
LEFT JOIN Led led ON led.WHID = stk.WHID AND led.ProductID = stk.ProductID
LEFT JOIN dbo.UNITS u ON u.ID = stk.UnitID
WHERE (@p0 = 0 OR stk.WHID = @p0)
  AND EXISTS (SELECT 1 FROM dbo.WAREHOUSE_LOCATIONS l WHERE l.WarehouseID = stk.WHID AND l.IsDeleted = 0 AND l.IsActive = 1)
  AND stk.StockQty - ISNULL(led.LedgerQty, 0) > @p1
ORDER BY w.WHName, p.PNo";

        public static List<UnassignedDlocStockRow> GetUnassignedRows(DBTHSNEntities db, int? warehouseId,
            IEnumerable<int> allowedWarehouseIds = null)
        {
            if (!DlocFifoSchema.IsEnabled(db))
                return new List<UnassignedDlocStockRow>();

            var rows = db.Database.SqlQuery<UnassignedDlocStockRow>(UnassignedSql, warehouseId ?? 0, QtyEpsilon).ToList();

            if (allowedWarehouseIds != null)
            {
                var allowed = new HashSet<int>(allowedWarehouseIds);
                rows = rows.Where(r => allowed.Contains(r.WarehouseID)).ToList();
            }
            return rows;
        }

        /// <summary>
        /// Tanlangan qatorlarni DLOC joylariga joylashtiradi. Hammasi bitta tranzaksiyada (avval yarmida xato
        /// bo'lsa yarim yozilgan OPENING kirim qolardi); bir mahsulot bir necha qatorda bo'lsa jami tekshiriladi.
        /// </summary>
        public static int AssignToLocations(DBTHSNEntities db, int warehouseId,
            IList<AssignDlocLineRequest> lines, string userEmail)
        {
            if (lines == null || !lines.Any())
                throw new InvalidOperationException("Joylashtirish uchun kamida bitta qator tanlang.");

            if (!DlocFifoSchema.IsEnabled(db))
                throw new InvalidOperationException("DLOC rejimi yoqilmagan.");

            var activeLocations = DlocFifoDataAccess.GetLocations(db, warehouseId, activeOnly: true);
            if (activeLocations == null || !activeLocations.Any())
                throw new InvalidOperationException("Tanlangan omborda faol DLOC joylashuvi yo'q.");

            var validLines = lines
                .Where(l => l.LocationID > 0 && l.AssignQty > QtyEpsilon)
                .ToList();

            if (!validLines.Any())
                throw new InvalidOperationException("DLOC va miqdor tanlangan qatorlar yo'q.");

            var activeLocationIds = new HashSet<int>(activeLocations.Select(l => l.ID));
            var assignedCount = 0;

            StockLedger.InTransaction(db, () =>
            {
                // Joylashtirilmagan qoldiq bir marta (avval har qator uchun butun ro'yxat qayta hisoblanardi)
                var unassignedByProduct = GetUnassignedRows(db, warehouseId)
                    .ToDictionary(r => r.ProductID, r => r.UnassignedQty);

                foreach (var g in validLines.GroupBy(l => l.ProductID))
                {
                    double unassigned;
                    unassignedByProduct.TryGetValue(g.Key, out unassigned);
                    var requested = g.Sum(x => x.AssignQty);
                    if (requested > unassigned + QtyEpsilon)
                    {
                        throw new InvalidOperationException(
                            $"Mahsulot ID {g.Key}: joylashtirish miqdori ({requested:N3}) " +
                            $"joylashtirilmagan qoldiqdan ({unassigned:N3}) oshib ketdi.");
                    }
                }

                var badLocation = validLines.FirstOrDefault(l => !activeLocationIds.Contains(l.LocationID));
                if (badLocation != null)
                    throw new InvalidOperationException($"DLOC ID {badLocation.LocationID} ushbu omborda faol emas.");

                var warehouseName = ReferenceDataCache.NameOf(ReferenceDataCache.Warehouses, warehouseId);
                var now = DateTime.Now;
                var productIds = validLines.Select(l => l.ProductID).Distinct().ToList();
                var unitByProduct = db.PRODUCTS
                    .Where(p => productIds.Contains(p.ID))
                    .Select(p => new { p.ID, p.UnitID })
                    .ToDictionary(p => p.ID, p => p.UnitID > 0 ? p.UnitID : 1);

                var income = new INCOME
                {
                    // Bir soniyada ikki joylashtirish bir xil raqam olardi — endi qulf bilan ketma-ket raqam
                    DocNo = DocumentNumberService.Next(db, "INCOMES", "DocNo", $"{AssignmentDocNoPrefix}{now:yyyyMMdd}-"),
                    ReceivingWHID = warehouseId,
                    IssueDateTime = now,
                    CurrencyID = GetDefaultCurrencyId(),
                    IsDeleted = false,
                    RecieveStatus = true,
                    Description = $"Mavjud qoldiqni DLOC ga joylashtirish: {warehouseName ?? warehouseId.ToString()}"
                };
                db.INCOMES.Add(income);

                var incomeLines = validLines.Select(line => new INCOME_PRODUCTS
                {
                    INCOME = income,
                    ProductID = line.ProductID,
                    Amount = (decimal)line.AssignQty,
                    PiecePrice = 0,
                    UnitID = unitByProduct.ContainsKey(line.ProductID) ? unitByProduct[line.ProductID] : 1
                }).ToList();
                db.INCOME_PRODUCTS.AddRange(incomeLines);
                income.Amount = incomeLines.Sum(l => l.Amount);
                db.SaveChanges();

                for (int i = 0; i < validLines.Count; i++)
                {
                    LocationLotStockLedger.AssignExistingStockToLocation(
                        db, warehouseId, income.ID, incomeLines[i].ID, validLines[i].ProductID,
                        (decimal)validLines[i].AssignQty, validLines[i].LocationID, now, "OPENING");
                    assignedCount++;
                }

                LogHelper.LogToDatabase(userEmail, "LegacyDlocAssignment", "AssignToLocations",
                    $"DLOC joylashtirish: kirim {income.ID} ({income.DocNo}), {assignedCount} qator, ombor {warehouseId}");
            });

            return assignedCount;
        }

        private static int GetDefaultCurrencyId()
        {
            var currencies = ReferenceDataCache.Get(ReferenceDataCache.Currencies);
            var uzs = currencies.FirstOrDefault(c => c.Name != null && c.Name.Trim().ToUpperInvariant() == "UZS");
            return uzs?.ID ?? currencies.OrderBy(c => c.ID).Select(c => (int?)c.ID).FirstOrDefault() ?? 1;
        }
    }
}
