using System.Collections.Generic;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.I18n;

namespace tahsinERP.Services.DlocFifo
{
    public static class FifoAllocatorService
    {
        public static List<FifoAllocationLine> Allocate(DBTHSNEntities db, int warehouseId, int productId, decimal quantity)
        {
            var lines = DlocFifoDataAccess.GetAvailableFifoLines(db, warehouseId, productId);
            var result = new List<FifoAllocationLine>();
            var remaining = quantity;

            foreach (var line in lines)
            {
                if (remaining <= 0)
                    break;

                var take = line.Qty >= remaining ? remaining : line.Qty;
                result.Add(new FifoAllocationLine
                {
                    LotID = line.LotID,
                    LocationID = line.LocationID,
                    LotNo = line.LotNo,
                    LocationCode = line.LocationCode,
                    ReceivedDateTime = line.ReceivedDateTime,
                    Qty = take
                });
                remaining -= take;
            }

            if (remaining > 0.0001m)
                throw new WarehouseDlocException("INSUFFICIENT_STOCK",
                    L.T("Wh_Err_FifoInsufficient", quantity.ToString("N2"), (quantity - remaining).ToString("N2")));

            return result;
        }

    }
}
