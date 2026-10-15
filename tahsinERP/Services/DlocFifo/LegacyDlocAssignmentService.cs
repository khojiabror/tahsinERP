using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Controllers;
using tahsinERP.Models;

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
    /// </summary>
    public static class LegacyDlocAssignmentService
    {
        private const double QtyEpsilon = 1e-6;

        public static List<UnassignedDlocStockRow> GetUnassignedRows(DBTHSNEntities db, int? warehouseId,
            IEnumerable<int> allowedWarehouseIds = null)
        {
            if (!DlocFifoSchema.IsEnabled(db))
                return new List<UnassignedDlocStockRow>();

            var allowed = allowedWarehouseIds != null
                ? new HashSet<int>(allowedWarehouseIds)
                : null;

            var warehouseQuery = db.WAREHOUSES.Where(w => w.IsDeleted != true);
            if (warehouseId.HasValue)
                warehouseQuery = warehouseQuery.Where(w => w.ID == warehouseId.Value);

            var warehouses = warehouseQuery.ToList()
                .Where(w => allowed == null || allowed.Contains(w.ID))
                .Where(w => DlocFifoDataAccess.WarehouseHasLocations(db, w.ID))
                .ToList();

            if (!warehouses.Any())
                return new List<UnassignedDlocStockRow>();

            var whIds = warehouses.Select(w => w.ID).ToList();
            var whNames = warehouses.ToDictionary(w => w.ID, w => w.WHName);

            var rawStocks = db.PRODUCT_STOCKS
                .Include(s => s.PRODUCT)
                .Include(s => s.UNIT)
                .Where(s => whIds.Contains(s.WHID) && s.Amount > 0 && s.PRODUCT.IsDeleted != true)
                .ToList();

            var grouped = rawStocks
                .GroupBy(s => new { s.WHID, s.ProductID })
                .Select(g =>
                {
                    var first = g.First();
                    return new
                    {
                        g.Key.WHID,
                        g.Key.ProductID,
                        StockQty = g.Sum(x => x.Amount),
                        first.PRODUCT,
                        UnitID = g.Select(x => x.UnitID).FirstOrDefault(u => u > 0),
                        Unit = first.UNIT
                    };
                })
                .ToList();

            var result = new List<UnassignedDlocStockRow>();
            foreach (var row in grouped)
            {
                var ledger = (double)DlocFifoDataAccess.GetLedgerQty(db, row.WHID, row.ProductID);
                var unassigned = row.StockQty - ledger;
                if (unassigned <= QtyEpsilon)
                    continue;

                result.Add(new UnassignedDlocStockRow
                {
                    WarehouseID = row.WHID,
                    WarehouseName = whNames.ContainsKey(row.WHID) ? whNames[row.WHID] : row.WHID.ToString(),
                    ProductID = row.ProductID,
                    ProductPNo = row.PRODUCT?.PNo ?? row.ProductID.ToString(),
                    ProductName = row.PRODUCT?.Name ?? "",
                    StockQty = row.StockQty,
                    LedgerQty = ledger,
                    UnassignedQty = unassigned,
                    UnitID = row.UnitID > 0 ? row.UnitID : 1,
                    UnitName = row.Unit?.ShortName ?? row.Unit?.UnitName ?? ""
                });
            }

            return result
                .OrderBy(r => r.WarehouseName)
                .ThenBy(r => r.ProductPNo)
                .ToList();
        }

        public static int AssignToLocations(DBTHSNEntities db, int warehouseId,
            IList<AssignDlocLineRequest> lines, string userEmail)
        {
            if (lines == null || !lines.Any())
                throw new InvalidOperationException("Joylashtirish uchun kamida bitta qator tanlang.");

            if (!DlocFifoSchema.IsEnabled(db))
                throw new InvalidOperationException("DLOC rejimi yoqilmagan.");

            if (!DlocFifoDataAccess.WarehouseHasLocations(db, warehouseId))
                throw new InvalidOperationException("Tanlangan omborda DLOC joylashuvlari sozlanmagan.");

            var activeLocations = DlocFifoDataAccess.GetLocations(db, warehouseId, activeOnly: true);
            if (activeLocations == null || !activeLocations.Any())
                throw new InvalidOperationException("Faol DLOC joylashuvi yo'q.");

            var validLines = lines
                .Where(l => l.LocationID > 0 && l.AssignQty > QtyEpsilon)
                .ToList();

            if (!validLines.Any())
                throw new InvalidOperationException("DLOC va miqdor tanlangan qatorlar yo'q.");

            foreach (var line in validLines)
            {
                var unassigned = GetUnassignedRows(db, warehouseId, null)
                    .FirstOrDefault(r => r.ProductID == line.ProductID);

                if (unassigned == null || line.AssignQty > unassigned.UnassignedQty + QtyEpsilon)
                {
                    throw new InvalidOperationException(
                        $"Mahsulot ID {line.ProductID}: joylashtirish miqdori ({line.AssignQty:N3}) " +
                        $"joylashtirilmagan qoldiqdan ({unassigned?.UnassignedQty ?? 0:N3}) oshib ketdi.");
                }

                if (!activeLocations.Any(loc => loc.ID == line.LocationID))
                    throw new InvalidOperationException($"DLOC ID {line.LocationID} ushbu omborda faol emas.");
            }

            var warehouse = db.WAREHOUSES.Find(warehouseId);
            var currencyId = GetDefaultCurrencyId(db);
            var now = DateTime.Now;
            var docNo = $"DLOC-ASN-{now:yyyyMMddHHmmss}";

            var income = new INCOME
            {
                DocNo = docNo,
                ReceivingWHID = warehouseId,
                IssueDateTime = now,
                CurrencyID = currencyId,
                IsDeleted = false,
                RecieveStatus = true,
                Description = $"Mavjud qoldiqni DLOC ga joylashtirish: {warehouse?.WHName ?? warehouseId.ToString()}"
            };
            db.INCOMES.Add(income);
            db.SaveChanges();

            var assignedCount = 0;
            foreach (var line in validLines)
            {
                var product = db.PRODUCTS.Find(line.ProductID);
                var unitId = product?.UnitID > 0 ? product.UnitID : 1;

                var incomeProduct = new INCOME_PRODUCTS
                {
                    IncomeID = income.ID,
                    ProductID = line.ProductID,
                    Amount = line.AssignQty,
                    PiecePrice = 0,
                    UnitID = unitId
                };
                db.INCOME_PRODUCTS.Add(incomeProduct);
                db.SaveChanges();

                LocationLotStockLedger.AssignExistingStockToLocation(
                    db, warehouseId, income.ID, incomeProduct.ID, line.ProductID,
                    (decimal)line.AssignQty, line.LocationID, now, "OPENING");

                assignedCount++;
            }

            LogHelper.LogToDatabase(userEmail, "LegacyDlocAssignment", "AssignToLocations",
                $"DLOC joylashtirish: kirim {income.ID}, {assignedCount} qator, ombor {warehouseId}");

            return assignedCount;
        }

        private static int GetDefaultCurrencyId(DBTHSNEntities db)
        {
            var c = db.CURRENCIES.FirstOrDefault(x => x.Shortname != null && x.Shortname.Trim().ToUpper() == "UZS")
                ?? db.CURRENCIES.OrderBy(x => x.ID).FirstOrDefault();
            return c != null ? c.ID : 1;
        }
    }
}
