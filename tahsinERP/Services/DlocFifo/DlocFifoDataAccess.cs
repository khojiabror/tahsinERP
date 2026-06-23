using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services.DlocFifo
{
    public static class DlocFifoDataAccess
    {
        public static bool TableExists(DBTHSNEntities db, string tableName)
        {
            var sql = "SELECT CASE WHEN OBJECT_ID(@p0,'U') IS NOT NULL THEN 1 ELSE 0 END";
            return db.Database.SqlQuery<int>(sql, "dbo." + tableName).FirstOrDefault() == 1;
        }

        public static List<WarehouseLocationDto> GetLocations(DBTHSNEntities db, int? warehouseId = null, bool activeOnly = true)
        {
            if (!DlocFifoSchema.IsEnabled(db))
                return new List<WarehouseLocationDto>();

            var sql = @"
SELECT l.ID, l.WarehouseID, l.LocationCode, l.LocationName, l.MinCapacity, l.MaxCapacity,
       l.AllowMixedLots, l.AllowMixedProducts, l.IsActive, w.WHName AS WarehouseName
FROM dbo.WAREHOUSE_LOCATIONS l
INNER JOIN dbo.WAREHOUSES w ON w.ID = l.WarehouseID
WHERE l.IsDeleted = 0";
            if (activeOnly)
                sql += " AND l.IsActive = 1";
            if (warehouseId.HasValue)
                sql += " AND l.WarehouseID = @p0";

            sql += " ORDER BY w.WHName, l.LocationCode";

            return warehouseId.HasValue
                ? db.Database.SqlQuery<WarehouseLocationDto>(sql, warehouseId.Value).ToList()
                : db.Database.SqlQuery<WarehouseLocationDto>(sql).ToList();
        }

        public static WarehouseLocationDto GetLocation(DBTHSNEntities db, int locationId)
        {
            if (!DlocFifoSchema.IsEnabled(db))
                return null;

            const string sql = @"
SELECT l.ID, l.WarehouseID, l.LocationCode, l.LocationName, l.MinCapacity, l.MaxCapacity,
       l.AllowMixedLots, l.AllowMixedProducts, l.IsActive, w.WHName AS WarehouseName
FROM dbo.WAREHOUSE_LOCATIONS l
INNER JOIN dbo.WAREHOUSES w ON w.ID = l.WarehouseID
WHERE l.ID = @p0 AND l.IsDeleted = 0";
            return db.Database.SqlQuery<WarehouseLocationDto>(sql, locationId).FirstOrDefault();
        }

        public static decimal GetLocationCurrentQty(DBTHSNEntities db, int locationId)
        {
            if (!DlocFifoSchema.IsEnabled(db))
                return 0;

            const string sql = "SELECT ISNULL(SUM(QtyAvailable),0) FROM dbo.LOT_LOCATIONS WHERE LocationID = @p0";
            return db.Database.SqlQuery<decimal>(sql, locationId).FirstOrDefault();
        }

        public static bool WarehouseHasLocations(DBTHSNEntities db, int warehouseId)
        {
            if (!DlocFifoSchema.IsEnabled(db))
                return false;

            const string sql = @"
SELECT COUNT(1) FROM dbo.WAREHOUSE_LOCATIONS
WHERE WarehouseID = @p0 AND IsDeleted = 0 AND IsActive = 1";
            return db.Database.SqlQuery<int>(sql, warehouseId).FirstOrDefault() > 0;
        }

        public static List<InventoryDlocStockRow> GetInventoryByDloc(DBTHSNEntities db, int? warehouseId, int? productId,
            IEnumerable<int> allowedWarehouseIds = null)
        {
            if (!DlocFifoSchema.IsEnabled(db))
                return new List<InventoryDlocStockRow>();

            // Har bir DLOC (joy) + mahsulot uchun qoldiqni lotlar bo'yicha yig'amiz —
            // bitta DLOC da bir nechta kirim loti bo'lsa ham bitta jami qator ko'rsatiladi.
            var sql = @"
SELECT loc.WarehouseID, w.WHName AS WarehouseName, ll.ProductID, p.PNo AS ProductPNo, p.Name AS ProductName,
       ll.LocationID, loc.LocationCode, SUM(ll.QtyAvailable) AS QtyAvailable, loc.MinCapacity, loc.MaxCapacity,
       ISNULL(u.ShortName, u.UnitName) AS UnitName, COUNT(DISTINCT ll.LotID) AS LotCount, MIN(il.ReceivedDateTime) AS ReceivedDateTime
FROM dbo.LOT_LOCATIONS ll
INNER JOIN dbo.WAREHOUSE_LOCATIONS loc ON loc.ID = ll.LocationID AND loc.IsDeleted = 0 AND loc.IsActive = 1
INNER JOIN dbo.WAREHOUSES w ON w.ID = loc.WarehouseID AND (w.IsDeleted IS NULL OR w.IsDeleted = 0)
INNER JOIN dbo.PRODUCTS p ON p.ID = ll.ProductID AND (p.IsDeleted IS NULL OR p.IsDeleted = 0)
LEFT JOIN dbo.UNITS u ON u.ID = p.UnitID
LEFT JOIN dbo.INCOME_LOTS il ON il.ID = ll.LotID AND il.IsDeleted = 0
WHERE ll.QtyAvailable > 0";

            var parms = new List<object>();
            if (warehouseId.HasValue)
            {
                sql += " AND loc.WarehouseID = @p0";
                parms.Add(warehouseId.Value);
            }

            sql += @"
GROUP BY loc.WarehouseID, w.WHName, ll.ProductID, p.PNo, p.Name, ll.LocationID, loc.LocationCode,
         loc.MinCapacity, loc.MaxCapacity, ISNULL(u.ShortName, u.UnitName)
HAVING SUM(ll.QtyAvailable) > 0
ORDER BY w.WHName, loc.LocationCode, p.PNo";

            var rows = parms.Any()
                ? db.Database.SqlQuery<InventoryDlocStockRow>(sql, parms.ToArray()).ToList()
                : db.Database.SqlQuery<InventoryDlocStockRow>(sql).ToList();

            if (allowedWarehouseIds != null && allowedWarehouseIds.Any())
            {
                var set = new HashSet<int>(allowedWarehouseIds);
                rows = rows.Where(r => set.Contains(r.WarehouseID)).ToList();
            }

            if (productId.HasValue)
                rows = rows.Where(r => r.ProductID == productId.Value).ToList();

            return rows;
        }

        public static decimal GetLedgerQty(DBTHSNEntities db, int warehouseId, int productId)
        {
            if (!DlocFifoSchema.IsEnabled(db))
                return 0;

            const string sql = @"
SELECT ISNULL(SUM(ll.QtyAvailable), 0)
FROM dbo.LOT_LOCATIONS ll
INNER JOIN dbo.WAREHOUSE_LOCATIONS loc ON loc.ID = ll.LocationID AND loc.IsDeleted = 0
WHERE loc.WarehouseID = @p0 AND ll.ProductID = @p1";
            return db.Database.SqlQuery<decimal>(sql, warehouseId, productId).FirstOrDefault();
        }

        public static int InsertLocation(DBTHSNEntities db, WarehouseLocationDto dto)
        {
            const string sql = @"
INSERT INTO dbo.WAREHOUSE_LOCATIONS (WarehouseID, LocationCode, LocationName, MinCapacity, MaxCapacity,
    AllowMixedLots, AllowMixedProducts, IsActive, IsDeleted, CreatedAt)
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, 1, 0, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS INT);";
            return db.Database.SqlQuery<int>(sql, dto.WarehouseID, dto.LocationCode, dto.LocationName ?? "",
                dto.MinCapacity, dto.MaxCapacity, dto.AllowMixedLots, dto.AllowMixedProducts).First();
        }

        public static void UpdateLocation(DBTHSNEntities db, WarehouseLocationDto dto)
        {
            const string sql = @"
UPDATE dbo.WAREHOUSE_LOCATIONS SET LocationCode=@p1, LocationName=@p2, MinCapacity=@p3, MaxCapacity=@p4,
    AllowMixedLots=@p5, AllowMixedProducts=@p6, IsActive=@p7
WHERE ID=@p0 AND IsDeleted=0";
            db.Database.ExecuteSqlCommand(sql, dto.ID, dto.LocationCode, dto.LocationName ?? "",
                dto.MinCapacity, dto.MaxCapacity, dto.AllowMixedLots, dto.AllowMixedProducts, dto.IsActive);
        }

        public static void SoftDeleteLocation(DBTHSNEntities db, int id)
        {
            db.Database.ExecuteSqlCommand(
                "UPDATE dbo.WAREHOUSE_LOCATIONS SET IsDeleted=1 WHERE ID=@p0", id);
        }

        public static List<FifoAllocationLine> GetAvailableFifoLines(DBTHSNEntities db, int warehouseId, int productId)
        {
            if (!DlocFifoSchema.IsEnabled(db))
                return new List<FifoAllocationLine>();

            const string sql = @"
SELECT il.ID AS LotID, ll.LocationID, il.LotNo, loc.LocationCode, il.ReceivedDateTime,
       ll.QtyAvailable AS Qty
FROM dbo.INCOME_LOTS il
INNER JOIN dbo.LOT_LOCATIONS ll ON ll.LotID = il.ID AND ll.ProductID = il.ProductID
INNER JOIN dbo.WAREHOUSE_LOCATIONS loc ON loc.ID = ll.LocationID AND loc.IsDeleted = 0
WHERE il.WarehouseID = @p0 AND il.ProductID = @p1 AND il.IsDeleted = 0
  AND il.QtyReceived > il.QtyConsumed AND ll.QtyAvailable > 0
ORDER BY il.ReceivedDateTime ASC, il.ID ASC";
            return db.Database.SqlQuery<FifoAllocationLine>(sql, warehouseId, productId).ToList();
        }
    }
}
