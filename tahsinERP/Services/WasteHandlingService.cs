using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Service for handling waste from production (Option A: separate stock, Option B: consumption loss)
    /// </summary>
    public class WasteHandlingService
    {
        private readonly DBTHSNEntities _db;
        private const bool USE_SEPARATE_WASTE_STOCK = true; // Configurable: true = Option A, false = Option B

        public WasteHandlingService(DBTHSNEntities db)
        {
            _db = db;
        }

        /// <summary>
        /// Records waste from production
        /// Option A: Creates separate waste stock entries
        /// Option B: Only logs for reporting (no stock tracking)
        /// </summary>
        public async Task RecordWasteAsync(int productId, int materialId, double wasteQty, int productionProcessId, int warehouseId, int unitId, string comment = null)
        {
            if (USE_SEPARATE_WASTE_STOCK)
            {
                // Option A: Create separate waste stock
                await RecordWasteAsStockAsync(productId, materialId, wasteQty, productionProcessId, warehouseId, unitId, comment);
            }
            else
            {
                // Option B: Only log for reporting (consumption loss)
                await RecordWasteAsLossAsync(productId, materialId, wasteQty, productionProcessId, comment);
            }
        }

        /// <summary>
        /// Option A: Records waste as separate stock in WASTE_STOCKS
        /// </summary>
        private async Task RecordWasteAsStockAsync(int productId, int materialId, double wasteQty, int productionProcessId, int warehouseId, int unitId, string comment)
        {
            // Find or create WASTE record
            var waste = await _db.WASTES
                .FirstOrDefaultAsync(w => w.ProcessID == productionProcessId && 
                                         w.WasteName.Contains(materialId.ToString()) && 
                                         w.IsDeleted == false);

            if (waste == null)
            {
                var material = await _db.PRODUCTS.FindAsync(materialId);
                waste = new WASTE
                {
                    WasteName = $"{material?.PNo ?? materialId.ToString()} - Waste",
                    ProcessID = productionProcessId,
                    Description = comment ?? $"Waste from production of Product ID {productId}",
                    IsDeleted = false
                };
                _db.WASTES.Add(waste);
                await _db.SaveChangesAsync();
            }

            // Add to WASTE_STOCKS
            var wasteStock = await _db.WASTE_STOCKS
                .FirstOrDefaultAsync(ws => ws.WHID == warehouseId && ws.WasteID == waste.ID);

            if (wasteStock != null)
            {
                wasteStock.Amount += wasteQty;
                _db.Entry(wasteStock).State = EntityState.Modified;
            }
            else
            {
                var newWasteStock = new WASTE_STOCKS
                {
                    WHID = warehouseId,
                    WasteID = waste.ID,
                    Amount = wasteQty,
                    UnitID = unitId
                };
                _db.WASTE_STOCKS.Add(newWasteStock);
            }

            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Option B: Records waste as consumption loss (only for reporting)
        /// </summary>
        private async Task RecordWasteAsLossAsync(int productId, int materialId, double wasteQty, int productionProcessId, string comment)
        {
            // In Option B, waste is only reflected in reports
            // You could create a WASTE_RECORDS table or log to a separate table for reporting
            // For now, we'll just log it (can be extended with a dedicated table)
            
            // TODO: Create WASTE_RECORDS table if needed for reporting
            // This would track: ProductID, MaterialID, WasteQty, ProductionProcessID, Date, Comment
        }

        /// <summary>
        /// Gets waste stock for a warehouse
        /// </summary>
        public async Task<List<WasteStockInfo>> GetWasteStockAsync(int warehouseId)
        {
            var wasteStocks = await _db.WASTE_STOCKS
                .Include(ws => ws.WASTE)
                .Include(ws => ws.WASTE.PRODUCTIONPROCESS)
                .Include(ws => ws.UNIT)
                .Where(ws => ws.WHID == warehouseId && ws.Amount > 0)
                .ToListAsync();

            return wasteStocks.Select(ws => new WasteStockInfo
            {
                WasteID = ws.WasteID,
                WasteName = ws.WASTE?.WasteName ?? "Unknown",
                ProcessName = ws.WASTE?.PRODUCTIONPROCESS?.ProcessName ?? "Unknown",
                Amount = ws.Amount,
                UnitID = ws.UnitID,
                UnitName = ws.UNIT?.ShortName ?? "N/A"
            }).ToList();
        }
    }

    public class WasteStockInfo
    {
        public int WasteID { get; set; }
        public string WasteName { get; set; }
        public string ProcessName { get; set; }
        public double Amount { get; set; }
        public int UnitID { get; set; }
        public string UnitName { get; set; }
    }
}
