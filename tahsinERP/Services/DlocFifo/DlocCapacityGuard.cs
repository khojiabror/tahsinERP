using System.Linq;
using tahsinERP.Models;
using tahsinERP.I18n;

namespace tahsinERP.Services.DlocFifo
{
    public static class DlocCapacityGuard
    {
        public static void ValidatePutaway(DBTHSNEntities db, int locationId, int productId, decimal incomingQty)
        {
            if (!DlocFifoSchema.IsEnabled(db))
                return;

            var loc = DlocFifoDataAccess.GetLocation(db, locationId);
            if (loc == null || !loc.IsActive)
                throw new WarehouseDlocException("DLOC_REQUIRED_OR_INACTIVE",
                    L.T("Wh_Err_DlocNotFoundOrInactive"));

            var current = DlocFifoDataAccess.GetLocationCurrentQty(db, locationId);
            if (current + incomingQty > loc.MaxCapacity)
                throw new WarehouseDlocException("DLOC_CAPACITY_EXCEEDED",
                    L.T("Wh_Err_DlocCapacityGuard", loc.MaxCapacity.ToString("N2"), current.ToString("N2"), incomingQty.ToString("N2")));

            if (!loc.AllowMixedProducts)
            {
                const string sql = @"
SELECT TOP 1 ll.ProductID FROM dbo.LOT_LOCATIONS ll
WHERE ll.LocationID = @p0 AND ll.QtyAvailable > 0 AND ll.ProductID <> @p1";
                var other = db.Database.SqlQuery<int>(sql, locationId, productId).FirstOrDefault();
                if (other > 0)
                    throw new WarehouseDlocException("LOT_MIXING_NOT_ALLOWED",
                        L.T("Wh_Err_DlocMixingNotAllowed"));
            }
        }

    }
}
