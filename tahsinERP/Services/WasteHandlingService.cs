using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Ishlab chiqarish chiqindisini alohida chiqindi qoldig'i (WASTES / WASTE_STOCKS) sifatida yozadi.
    /// </summary>
    public class WasteHandlingService
    {
        private readonly DBTHSNEntities _db;

        public WasteHandlingService(DBTHSNEntities db)
        {
            _db = db;
        }

        /// <summary>
        /// Chiqindini qayd qiladi: (jarayon, material) uchun WASTE yozuvi (yo'q bo'lsa yaratiladi) va
        /// ombordagi WASTE_STOCKS qoldig'i atomik oshiriladi.
        /// </summary>
        public async Task RecordWasteAsync(int productId, int materialId, double wasteQty, int productionProcessId, int warehouseId, int unitId, string comment = null)
        {
            if (wasteQty <= 0)
                return;

            // SRM-B22: avval WasteName.Contains(materialId.ToString()) bilan izlanardi, lekin nom "{PNo} - Waste" —
            // deyarli hech qachon mos kelmas va har ishlab chiqarishda yangi WASTE yaratilardi (yoki raqami
            // o'xshash boshqa materialnikiga tushardi). Endi nom aniq solishtiriladi (sxema o'zgarishisiz;
            // to'g'ri yechim — WASTES.MaterialID ustuni).
            var material = await _db.PRODUCTS.FindAsync(materialId);
            var wasteName = $"{material?.PNo ?? materialId.ToString()} - Waste";

            var waste = await _db.WASTES
                .Where(w => w.ProcessID == productionProcessId && w.WasteName == wasteName && w.IsDeleted == false)
                .OrderBy(w => w.ID)
                .FirstOrDefaultAsync();

            if (waste == null)
            {
                waste = new WASTE
                {
                    WasteName = wasteName,
                    ProcessID = productionProcessId,
                    Description = comment ?? $"Waste from production of Product ID {productId}",
                    IsDeleted = false
                };
                _db.WASTES.Add(waste);
                await _db.SaveChangesAsync();
            }

            // Atomik "+=" (avval o'qib-yozish: parallel ishlab chiqarishlarda miqdor yo'qolardi)
            const string upsert = @"
UPDATE dbo.WASTE_STOCKS WITH (UPDLOCK, HOLDLOCK) SET Amount = Amount + @p2 WHERE WHID = @p0 AND WasteID = @p1;
IF @@ROWCOUNT = 0
    INSERT INTO dbo.WASTE_STOCKS (WHID, WasteID, Amount, UnitID) VALUES (@p0, @p1, @p2, @p3);";
            await _db.Database.ExecuteSqlCommandAsync(upsert, warehouseId, waste.ID, wasteQty, unitId > 0 ? unitId : 1);
        }
    }
}
