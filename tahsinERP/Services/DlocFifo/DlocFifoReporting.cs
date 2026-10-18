using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services.DlocFifo
{
    public class DlocCapacityAlertRow
    {
        public string WarehouseName { get; set; }
        public string LocationCode { get; set; }
        public decimal CurrentQty { get; set; }
        public decimal MinCapacity { get; set; }
        public decimal MaxCapacity { get; set; }
        public string AlertType { get; set; }
    }

    public static class DlocFifoReporting
    {
        public static List<DlocCapacityAlertRow> GetDlocCapacityAlerts(DBTHSNEntities db, IEnumerable<int> warehouseIds)
        {
            if (!DlocFifoSchema.IsEnabled(db))
                return new List<DlocCapacityAlertRow>();

            var ids = warehouseIds?.Distinct().ToList() ?? new List<int>();
            var sql = @"
SELECT w.WHName AS WarehouseName, loc.LocationCode,
       ISNULL(SUM(ll.QtyAvailable), 0) AS CurrentQty,
       loc.MinCapacity, loc.MaxCapacity,
       CASE
         WHEN ISNULL(SUM(ll.QtyAvailable), 0) < loc.MinCapacity AND loc.MinCapacity > 0 THEN 'BELOW_MIN'
         WHEN loc.MaxCapacity > 0 AND ISNULL(SUM(ll.QtyAvailable), 0) >= loc.MaxCapacity * 0.9 THEN 'NEAR_MAX'
         ELSE NULL
       END AS AlertType
FROM dbo.WAREHOUSE_LOCATIONS loc
INNER JOIN dbo.WAREHOUSES w ON w.ID = loc.WarehouseID
LEFT JOIN dbo.LOT_LOCATIONS ll ON ll.LocationID = loc.ID
WHERE loc.IsDeleted = 0 AND loc.IsActive = 1";

            var parms = new List<object>();
            if (ids.Any())
                sql += " AND loc.WarehouseID IN (" + AddInParameters(parms, ids) + ")";

            sql += @"
GROUP BY w.WHName, loc.LocationCode, loc.MinCapacity, loc.MaxCapacity, loc.ID
HAVING CASE
         WHEN ISNULL(SUM(ll.QtyAvailable), 0) < loc.MinCapacity AND loc.MinCapacity > 0 THEN 'BELOW_MIN'
         WHEN loc.MaxCapacity > 0 AND ISNULL(SUM(ll.QtyAvailable), 0) >= loc.MaxCapacity * 0.9 THEN 'NEAR_MAX'
         ELSE NULL
       END IS NOT NULL
ORDER BY w.WHName, loc.LocationCode";

            return db.Database.SqlQuery<DlocCapacityAlertRow>(sql, parms.ToArray()).ToList();
        }

        /// <summary>
        /// IN (...) ro'yxatini parametrlar (@pN) bilan quradi — qiymatlar SQL matniga qo'shilmaydi (SRM-S06).
        /// </summary>
        private static string AddInParameters(List<object> parms, IEnumerable<int> values)
        {
            var names = new List<string>();
            foreach (var v in values)
            {
                names.Add("@p" + parms.Count);
                parms.Add(v);
            }
            return string.Join(",", names);
        }

        public class IncomePutawayRow
        {
            public int IncomeProductID { get; set; }
            public string LocationCode { get; set; }
            public string LotNo { get; set; }
            public decimal Qty { get; set; }
        }

        public static Dictionary<int, IncomePutawayRow> GetIncomePutawayByProductLine(DBTHSNEntities db, int incomeId)
        {
            if (!DlocFifoSchema.IsEnabled(db))
                return new Dictionary<int, IncomePutawayRow>();

            const string sql = @"
SELECT il.IncomeProductID, loc.LocationCode, il.LotNo, il.QtyReceived AS Qty
FROM dbo.INCOME_LOTS il
LEFT JOIN dbo.LOT_LOCATIONS ll ON ll.LotID = il.ID
LEFT JOIN dbo.WAREHOUSE_LOCATIONS loc ON loc.ID = ll.LocationID AND loc.IsDeleted = 0
WHERE il.IncomeID = @p0 AND il.IsDeleted = 0";

            var rows = db.Database.SqlQuery<IncomePutawayRow>(sql, incomeId).ToList();
            return rows
                .Where(r => r.IncomeProductID > 0)
                .GroupBy(r => r.IncomeProductID)
                .ToDictionary(g => g.Key, g => g.First());
        }
    }
}
