using System;
using System.Linq;
using tahsinERP.Models;

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
                ? $"Ombor ID {warehouseId}"
                : warehouseName;

            if (!DlocFifoSchema.IsEnabled(db))
            {
                throw new InvalidOperationException(
                    "DLOC rejimi yoqilmagan. Ishlab chiqarish uchun avtomatik kirim va xomashyo chiqimi faqat DLOC bilan yaratiladi.");
            }

            if (!DlocFifoDataAccess.WarehouseHasLocations(db, warehouseId))
            {
                throw new InvalidOperationException(
                    $"«{whLabel}» omborida DLOC joylashuvlari sozlanmagan. Avtomatik kirim/chiqim yaratib bo'lmaydi. Ombor bo'limida DLOC joylarini sozlang.");
            }

            var activeLocations = DlocFifoDataAccess.GetLocations(db, warehouseId, activeOnly: true);
            if (activeLocations == null || !activeLocations.Any())
            {
                throw new InvalidOperationException(
                    $"«{whLabel}» omborida faol DLOC joylashuvi yo'q. Kamida bitta faol DLOC yarating.");
            }
        }
    }
}
