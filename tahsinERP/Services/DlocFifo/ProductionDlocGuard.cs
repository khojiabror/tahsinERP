using System;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.I18n;

namespace tahsinERP.Services.DlocFifo
{
    /// <summary>
    /// Ishlab chiqarish avtomatik kirim/chiqimi faqat DLOC bilan; DLOC yo'q bo'lsa xato.
    /// </summary>
    public static class ProductionDlocGuard
    {
        public static void RequireForProductionWarehouse(DBTHSNEntities db, int warehouseId, string warehouseName = null)
        {
            var whLabel = string.IsNullOrWhiteSpace(warehouseName)
                ? L.T("Wh_WarehouseIdLabel", warehouseId)
                : warehouseName;

            if (!DlocFifoSchema.IsEnabled(db))
            {
                throw new InvalidOperationException(
                    L.T("Wh_Err_ProdDlocDisabled"));
            }

            if (!DlocFifoDataAccess.WarehouseHasLocations(db, warehouseId))
            {
                throw new InvalidOperationException(
                    L.T("Wh_Err_ProdDlocNotConfigured", whLabel));
            }

            var activeLocations = DlocFifoDataAccess.GetLocations(db, warehouseId, activeOnly: true);
            if (activeLocations == null || !activeLocations.Any())
            {
                throw new InvalidOperationException(
                    L.T("Wh_Err_ProdNoActiveDloc", whLabel));
            }
        }
    }
}
