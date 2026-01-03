using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Service for generating Purchase Orders from Sales Orders using BOM explosion
    /// </summary>
    public class POrderGenerationService
    {
        private readonly DBTHSNEntities _db;
        private readonly UnitConversionService _conversionService;

        public POrderGenerationService(DBTHSNEntities db)
        {
            _db = db;
            _conversionService = new UnitConversionService(db);
        }

        /// <summary>
        /// Generates raw material requirements from Sales Orders
        /// </summary>
        /// <param name="salesOrderIds">Array of Sales Order IDs</param>
        /// <returns>List of required raw materials with converted quantities</returns>
        public async Task<List<RawMaterialRequirement>> GenerateRequirementsFromSalesOrdersAsync(int[] salesOrderIds)
        {
            if (salesOrderIds == null || salesOrderIds.Length == 0)
            {
                throw new ArgumentException("Sales order IDs cannot be empty.");
            }

            // Load sales orders with products
            var salesOrders = await _db.S_ORDERS
                .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.PRODUCT))
                .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.PRODUCT.PRODUCT_CATEGORIES))
                .Where(o => salesOrderIds.Contains(o.ID) && o.IsDeleted != true)
                .ToListAsync();

            // Dictionary to aggregate required quantities by component (raw material)
            var componentRequirements = new Dictionary<int, RawMaterialRequirement>();

            // Process each sales order
            foreach (var sOrder in salesOrders)
            {
                foreach (var sOrderProduct in sOrder.S_ORDER_PRODUCTS)
                {
                    var product = sOrderProduct.PRODUCT;
                    if (product == null) continue;

                    // Get product quantity from sales order
                    double productQty = sOrderProduct.Amount;

                    // Find latest active BOM for this product (try for all products, not just finished goods)
                    var bomHeader = await GetLatestActiveBOMAsync(product.ID);

                    if (bomHeader != null && bomHeader.BOM_LINES != null && bomHeader.BOM_LINES.Any())
                    {
                        // Process each BOM line
                        foreach (var bomLine in bomHeader.BOM_LINES)
                        {
                            // Check if BOM line has required data
                            if (!bomLine.ComponentID.HasValue || !bomLine.Qty.HasValue || bomLine.Qty.Value <= 0)
                            {
                                continue; // Skip invalid BOM lines
                            }

                            int componentId = bomLine.ComponentID.Value;
                            decimal bomQtyPerUnit = bomLine.Qty.Value;
                            
                            // Calculate required quantity: BOM quantity × Product quantity
                            decimal requiredQty = bomQtyPerUnit * (decimal)productQty;

                            // Load component product with category
                            var component = await _db.PRODUCTS
                                .Include(p => p.PRODUCT_CATEGORIES)
                                .FirstOrDefaultAsync(p => p.ID == componentId && p.IsDeleted != true);

                            if (component != null)
                            {
                                // Check if component is purchasable (not a finished good)
                                // If category is null, assume it's purchasable
                                bool isPurchasable = component.PRODUCT_CATEGORIES == null ||
                                                    component.PRODUCT_CATEGORIES.IsFinishedGood == false;

                                if (isPurchasable)
                                {
                                    // Get BOM unit ID (from BOM_LINES)
                                    int bomUnitId = bomLine.UnitID ?? component.UnitID;

                                    // Check if product should use weight-based calculations
                                    bool calculateByWeight = await _conversionService.ShouldCalculateByWeightAsync(componentId);
                                    
                                    // Check if conversion is needed
                                    bool requiresConversion = await _conversionService.RequiresCuttingAsync(componentId);
                                    decimal finalQuantity = requiredQty;
                                    int finalUnitId = bomUnitId;
                                    string calculationExplanation = "";

                                    // If product uses weight-based calculations, convert from pieces to kg
                                    if (calculateByWeight)
                                    {
                                        try
                                        {
                                            // First, ensure we have quantity in pieces
                                            decimal pieceQuantity = requiredQty;
                                            
                                            // If BOM unit is METER (5) and product requires cutting, convert to pieces first
                                            if (requiresConversion && bomUnitId == 5)
                                            {
                                                pieceQuantity = await _conversionService.ConvertMeterToPieceAsync(
                                                    componentId, requiredQty);
                                                calculationExplanation = 
                                                    $"{requiredQty:F2} m (BOM) × {productQty} (Product qty) = {requiredQty:F2} m → " +
                                                    $"Converted to {pieceQuantity:F0} pcs";
                                            }

                                            // Convert pieces to kg
                                            finalQuantity = await _conversionService.ConvertPieceToKgAsync(componentId, pieceQuantity);
                                            // Use component's UnitID (should be kg/gr for weight-based products)
                                            finalUnitId = component.UnitID > 0 ? component.UnitID : 2; // Default to 2 if not set
                                            calculationExplanation += $" → {finalQuantity:F2} kg (Weight: {component.Weight} kg/pc)";
                                        }
                                        catch (Exception ex)
                                        {
                                            calculationExplanation = 
                                                $"Error converting to kg: {ex.Message}. Using original quantity: {requiredQty:F2}";
                                        }
                                    }
                                    // If BOM unit is METER (5) and product requires cutting, convert to pieces
                                    else if (requiresConversion && bomUnitId == 5)
                                    {
                                        try
                                        {
                                            finalQuantity = await _conversionService.ConvertMeterToPieceAsync(
                                                componentId, requiredQty);
                                            finalUnitId = 1; // Piece
                                            calculationExplanation = 
                                                $"{requiredQty:F2} m (BOM) × {productQty} (Product qty) = {requiredQty:F2} m → " +
                                                $"Converted to {finalQuantity:F0} pcs using Length: {component.Length}mm";
                                        }
                                        catch (Exception ex)
                                        {
                                            calculationExplanation = 
                                                $"Error converting: {ex.Message}. Using original quantity: {requiredQty:F2}";
                                        }
                                    }
                                    else
                                    {
                                        calculationExplanation = 
                                            $"{bomQtyPerUnit:F2} (BOM qty) × {productQty} (Product qty) = {requiredQty:F2}";
                                    }

                                    // Aggregate quantities
                                    if (componentRequirements.ContainsKey(componentId))
                                    {
                                        var existing = componentRequirements[componentId];
                                        existing.RequiredQty += finalQuantity;
                                        existing.CalculationExplanation += $"; {calculationExplanation}";
                                    }
                                    else
                                    {
                                        // Get estimated price from NORM_INPUT_LINES
                                        decimal? estimatedPrice = await GetEstimatedPriceAsync(product.ID, componentId);

                                        componentRequirements[componentId] = new RawMaterialRequirement
                                        {
                                            ComponentID = componentId,
                                            ComponentPNo = component.PNo,
                                            ComponentName = component.Name,
                                            UnitID = finalUnitId,
                                            RequiredQty = finalQuantity,
                                            EstimatedPrice = estimatedPrice,
                                            CalculationExplanation = calculationExplanation
                                        };
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return componentRequirements.Values.ToList();
        }

        /// <summary>
        /// Gets the latest active BOM header for a product
        /// </summary>
        private async Task<BOM_HEADERS> GetLatestActiveBOMAsync(int productId)
        {
            // Get BOM headers - IsActive can be null, true, or false
            // Consider active if IsActive == true OR IsActive == null (treat null as active)
            var bomHeaders = await _db.BOM_HEADERS
                .Include(b => b.BOM_LINES)
                .Include(b => b.BOM_LINES.Select(bl => bl.PRODUCT))
                .Include(b => b.BOM_LINES.Select(bl => bl.UNIT))
                .Where(b => b.ProductID == productId && (b.IsActive == true || b.IsActive == null))
                .ToListAsync();

            if (!bomHeaders.Any())
            {
                return null;
            }

            // Sort by VersionNo as date (YYYY-MM format) descending, then by CreatedAt
            var sortedBomHeaders = bomHeaders
                .Where(bh => !string.IsNullOrEmpty(bh.VersionNo))
                .OrderByDescending(bh =>
                {
                    string versionNo = bh.VersionNo.Trim();
                    if (DateTime.TryParseExact(versionNo, "yyyy-MM", null, 
                        System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                    {
                        return parsedDate;
                    }
                    var parts = versionNo.Split('-');
                    if (parts.Length >= 2 && 
                        int.TryParse(parts[0], out int year) && 
                        int.TryParse(parts[1], out int month))
                    {
                        if (month >= 1 && month <= 12 && year > 1900 && year < 2100)
                        {
                            return new DateTime(year, month, 1);
                        }
                    }
                    return DateTime.MinValue;
                })
                .ThenByDescending(bh => bh.CreatedAt)
                .FirstOrDefault();

            return sortedBomHeaders ?? bomHeaders.OrderByDescending(bh => bh.CreatedAt).FirstOrDefault();
        }

        /// <summary>
        /// Gets estimated price from NORM_INPUT_LINES
        /// </summary>
        private async Task<decimal?> GetEstimatedPriceAsync(int finishedGoodId, int materialId)
        {
            var normLine = await _db.NORM_INPUT_LINES
                .Where(n => n.ProductID == finishedGoodId && n.MaterialID == materialId)
                .OrderByDescending(n => n.CreatedDT)
                .FirstOrDefaultAsync();

            return normLine?.Price;
        }
    }

    /// <summary>
    /// DTO for raw material requirements
    /// </summary>
    public class RawMaterialRequirement
    {
        public int ComponentID { get; set; }
        public string ComponentPNo { get; set; }
        public string ComponentName { get; set; }
        public int UnitID { get; set; }
        public decimal RequiredQty { get; set; }
        public decimal? EstimatedPrice { get; set; }
        public string CalculationExplanation { get; set; }

        public decimal? EstimatedAmount => EstimatedPrice.HasValue 
            ? RequiredQty * EstimatedPrice.Value 
            : (decimal?)null;
    }
}
