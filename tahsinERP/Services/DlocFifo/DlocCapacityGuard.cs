using System.Linq;
using tahsinERP.Models;

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
                    "Joylashuv (DLOC) topilmadi yoki faol emas.");

            var current = DlocFifoDataAccess.GetLocationCurrentQty(db, locationId);
            if (current + incomingQty > loc.MaxCapacity)
                throw new WarehouseDlocException("DLOC_CAPACITY_EXCEEDED",
                    $"Tanlangan DLOC sig‘imi oshib ketdi. Maksimum: {loc.MaxCapacity:N2}, joriy: {current:N2}, kirim: {incomingQty:N2}.");

            if (!loc.AllowMixedProducts)
            {
                const string sql = @"
SELECT TOP 1 ll.ProductID FROM dbo.LOT_LOCATIONS ll
WHERE ll.LocationID = @p0 AND ll.QtyAvailable > 0 AND ll.ProductID <> @p1";
                var other = db.Database.SqlQuery<int>(sql, locationId, productId).FirstOrDefault();
                if (other > 0)
                    throw new WarehouseDlocException("LOT_MIXING_NOT_ALLOWED",
                        "Bu DLOC’da boshqa mahsulot mavjud. Aralashtirishga ruxsat yo‘q.");
            }
        }

        public static bool IsBelowMin(DBTHSNEntities db, int locationId)
        {
            if (!DlocFifoSchema.IsEnabled(db))
                return false;

            var loc = DlocFifoDataAccess.GetLocation(db, locationId);
            if (loc == null || loc.MinCapacity <= 0)
                return false;

            var current = DlocFifoDataAccess.GetLocationCurrentQty(db, locationId);
            return current < loc.MinCapacity;
        }
    }
}
