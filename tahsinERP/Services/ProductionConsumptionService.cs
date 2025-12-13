using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Service for calculating production consumption including waste from NORMLINES
    /// </summary>
    public class ProductionConsumptionService
    {
        private readonly DBTHSNEntities _db;
        private readonly UnitConversionService _conversionService;

        public ProductionConsumptionService(DBTHSNEntities db)
        {
            _db = db;
            _conversionService = new UnitConversionService(db);
        }

        /// <summary>
        /// Calculates required raw materials for a production plan including waste
        /// </summary>
        public async Task<List<ProductionRawMaterialRequirement>> CalculateRequiredRawMaterialsAsync(int productId, double producedQty)
        {
            var requirements = new List<ProductionRawMaterialRequirement>();

            // Get active BOM for the product
            var bomHeader = await _db.BOM_HEADERS
                .Where(bh => bh.ProductID == productId && bh.IsActive == true)
                .OrderByDescending(bh => bh.CreatedAt)
                .FirstOrDefaultAsync();

            if (bomHeader == null)
            {
                return requirements; // No BOM found
            }

            // Get BOM lines (materials)
            var bomLines = await _db.BOM_LINES
                .Include(bl => bl.PRODUCT)
                .Include(bl => bl.PRODUCT.PRODUCT_CATEGORIES)
                .Include(bl => bl.UNIT)
                .Where(bl => bl.BOMID == bomHeader.ID)
                .ToListAsync();

            // Get linked NORMVERSION via BOMS_AND_NORMS table
            // Note: BOMS_AND_NORMS table exists but may not have a DbSet in DBTHSNEntities
            // Using direct SQL query to access it
            int? normId = null;
            try
            {
                // Try to access via direct SQL query using Entity Framework parameter syntax
                var result = await _db.Database.SqlQuery<int?>(
                    "SELECT NormID FROM BOMS_AND_NORMS WHERE BOMID = {0} AND IsActive = 1",
                    bomHeader.ID).FirstOrDefaultAsync();
                normId = result;
            }
            catch
            {
                // If table doesn't exist or query fails, continue without NORMLINES
                normId = null;
            }

            if (normId.HasValue)
            {
                // Use NORMLINES for waste calculation
                var normLines = await _db.NORMLINES
                    .Include(nl => nl.PRODUCT)
                    .Include(nl => nl.PRODUCT.PRODUCT_CATEGORIES)
                    .Where(nl => nl.NormID == normId.Value)
                    .ToListAsync();

                foreach (var normLine in normLines)
                {
                    if (!normLine.MaterialID.HasValue || !normLine.QtyPerUnit.HasValue)
                        continue;

                    var material = normLine.PRODUCT;
                    if (material == null)
                        continue;

                    // Calculate required quantity
                    var qtyPerUnit = (double)normLine.QtyPerUnit.Value;
                    var requiredQty = qtyPerUnit * producedQty;

                    var requiredQtyPieces = requiredQty;
                    var requiredQtyMeters = 0.0;
                    var wasteQty = 0.0;
                    var totalConsumption = requiredQty;

                    // Check if material requires cutting
                    bool requiresCutting = material.CategoryID.HasValue &&
                                           material.PRODUCT_CATEGORIES != null &&
                                           material.PRODUCT_CATEGORIES.RequiresCutting;

                    if (requiresCutting)
                    {
                        // Convert meters to pieces
                        requiredQtyMeters = requiredQty;
                        requiredQtyPieces = (double)await _conversionService.ConvertMeterToPieceAsync(
                            material.ID, (decimal)requiredQty);

                        // Calculate waste
                        if (normLine.WastePercent.HasValue)
                        {
                            var wastePercent = (double)normLine.WastePercent.Value;
                            wasteQty = requiredQtyPieces * (wastePercent / 100.0);
                            totalConsumption = requiredQtyPieces + wasteQty;
                        }
                    }
                    else
                    {
                        // For non-cutting materials, waste is applied directly
                        if (normLine.WastePercent.HasValue)
                        {
                            var wastePercent = (double)normLine.WastePercent.Value;
                            wasteQty = requiredQty * (wastePercent / 100.0);
                            totalConsumption = requiredQty + wasteQty;
                        }
                    }

                    requirements.Add(new ProductionRawMaterialRequirement
                    {
                        MaterialID = material.ID,
                        MaterialPNo = material.PNo,
                        MaterialName = material.Name,
                        RequiredQtyPieces = requiredQtyPieces,
                        RequiredQtyMeters = requiredQtyMeters,
                        WasteQty = wasteQty,
                        TotalConsumption = totalConsumption,
                        UnitID = material.UnitID,
                        RequiresCutting = requiresCutting
                    });
                }
            }
            else
            {
                // No NORMLINES linked, use BOM_LINES directly (no waste calculation)
                foreach (var bomLine in bomLines)
                {
                    if (bomLine.ComponentID.HasValue && bomLine.Qty.HasValue)
                    {
                        var material = await _db.PRODUCTS
                            .Include(p => p.PRODUCT_CATEGORIES)
                            .FirstOrDefaultAsync(p => p.ID == bomLine.ComponentID.Value);

                        if (material != null)
                        {
                            var requiredQty = (double)(bomLine.Qty.Value * (decimal)producedQty);
                            var requiredQtyPieces = requiredQty;
                            var requiredQtyMeters = 0.0;
                            var wasteQty = 0.0;
                            var totalConsumption = requiredQty;

                            // Check if material requires cutting
                            bool requiresCutting = material.CategoryID.HasValue &&
                                                   material.PRODUCT_CATEGORIES != null &&
                                                   material.PRODUCT_CATEGORIES.RequiresCutting;

                            if (requiresCutting)
                            {
                                // Convert to pieces if needed
                                requiredQtyMeters = requiredQty;
                                requiredQtyPieces = (double)await _conversionService.ConvertMeterToPieceAsync(
                                    material.ID, (decimal)requiredQty);
                                totalConsumption = requiredQtyPieces;
                            }

                            requirements.Add(new ProductionRawMaterialRequirement
                            {
                                MaterialID = material.ID,
                                MaterialPNo = material.PNo,
                                MaterialName = material.Name,
                                RequiredQtyPieces = requiredQtyPieces,
                                RequiredQtyMeters = requiredQtyMeters,
                                WasteQty = wasteQty,
                                TotalConsumption = totalConsumption,
                                UnitID = bomLine.UnitID ?? material.UnitID,
                                RequiresCutting = requiresCutting
                            });
                        }
                    }
                }
            }

            return requirements;
        }

        /// <summary>
        /// Consumes materials from Production warehouse when production is confirmed
        /// </summary>
        public async Task ConsumeMaterialsForProductionAsync(int productId, double producedQty, int productionWarehouseId, int? productionProcessId = null)
        {
            var requirements = await CalculateRequiredRawMaterialsAsync(productId, producedQty);
            var wasteService = new WasteHandlingService(_db);

            foreach (var req in requirements)
            {
                // Get stock in Production warehouse
                var stock = await _db.PRODUCT_STOCKS
                    .FirstOrDefaultAsync(s => s.WHID == productionWarehouseId && s.ProductID == req.MaterialID);

                if (stock == null || stock.Amount < req.TotalConsumption)
                {
                    throw new InvalidOperationException(
                        $"Insufficient stock for material {req.MaterialPNo}. Required: {req.TotalConsumption}, Available: {stock?.Amount ?? 0}");
                }

                // Subtract total consumption (including waste)
                stock.Amount -= req.TotalConsumption;
                if (stock.Amount < 0) stock.Amount = 0;

                _db.Entry(stock).State = EntityState.Modified;

                // Record waste if applicable
                if (req.WasteQty > 0 && productionProcessId.HasValue)
                {
                    await wasteService.RecordWasteAsync(
                        productId, 
                        req.MaterialID, 
                        req.WasteQty, 
                        productionProcessId.Value, 
                        productionWarehouseId, 
                        req.UnitID,
                        $"Waste from production of Product ID {productId}, Qty: {producedQty}");
                }
            }

            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Raw material requirement calculation result for production consumption
    /// </summary>
    public class ProductionRawMaterialRequirement
    {
        public int MaterialID { get; set; }
        public string MaterialPNo { get; set; }
        public string MaterialName { get; set; }
        public double RequiredQtyPieces { get; set; }
        public double RequiredQtyMeters { get; set; }
        public double WasteQty { get; set; }
        public double TotalConsumption { get; set; }
        public int UnitID { get; set; }
        public bool RequiresCutting { get; set; }
    }
}
