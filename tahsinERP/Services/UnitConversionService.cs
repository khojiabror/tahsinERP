using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Service for converting between different units of measurement
    /// Handles conversions for products that require cutting (requires_cutting = true)
    /// </summary>
    public class UnitConversionService
    {
        private readonly DBTHSNEntities _db;

        public UnitConversionService(DBTHSNEntities db)
        {
            _db = db;
        }

        /// <summary>
        /// Converts meter quantity to piece quantity for products that require cutting
        /// Formula: pieceQty = requiredMeter / (Length / 1000)
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <param name="meterQuantity">Quantity in meters</param>
        /// <returns>Quantity in pieces</returns>
        public async Task<decimal> ConvertMeterToPieceAsync(int productId, decimal meterQuantity)
        {
            var product = await _db.PRODUCTS
                .Include(p => p.PRODUCT_CATEGORIES)
                .FirstOrDefaultAsync(p => p.ID == productId);

            if (product == null)
            {
                throw new ArgumentException($"Product with ID {productId} not found.");
            }

            // Check if product requires cutting
            bool requiresCutting = product.CategoryID.HasValue && 
                                   product.PRODUCT_CATEGORIES != null && 
                                   product.PRODUCT_CATEGORIES.RequiresCutting;

            if (!requiresCutting)
            {
                // No conversion needed, return as-is
                return meterQuantity;
            }

            // Get product length in mm
            if (!product.Length.HasValue || product.Length.Value <= 0)
            {
                throw new InvalidOperationException(
                    $"Product {product.PNo} does not have a valid Length value. Cannot convert meter to piece.");
            }

            // Convert length from mm to meters
            decimal lengthInMeters = (decimal)(product.Length.Value / 1000.0);

            if (lengthInMeters <= 0)
            {
                throw new InvalidOperationException(
                    $"Product {product.PNo} has invalid length. Cannot convert meter to piece.");
            }

            // Calculate pieces: pieceQty = requiredMeter / (Length / 1000)
            decimal pieceQuantity = meterQuantity / lengthInMeters;

            return Math.Ceiling(pieceQuantity); // Round up to ensure we have enough pieces
        }

        /// <summary>
        /// Converts piece quantity to CBM (cubic meters) for products that require cutting
        /// Formula: cbm = pieces × (L/1000 × W/1000 × H/1000)
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <param name="pieceQuantity">Quantity in pieces</param>
        /// <returns>Quantity in CBM (cubic meters)</returns>
        public async Task<decimal> ConvertPieceToCBMAsync(int productId, decimal pieceQuantity)
        {
            var product = await _db.PRODUCTS
                .Include(p => p.PRODUCT_CATEGORIES)
                .FirstOrDefaultAsync(p => p.ID == productId);

            if (product == null)
            {
                throw new ArgumentException($"Product with ID {productId} not found.");
            }

            // Check if product requires cutting
            bool requiresCutting = product.CategoryID.HasValue && 
                                   product.PRODUCT_CATEGORIES != null && 
                                   product.PRODUCT_CATEGORIES.RequiresCutting;

            if (!requiresCutting)
            {
                // No conversion needed, return as-is
                return pieceQuantity;
            }

            // Get product dimensions in mm
            if (!product.Length.HasValue || product.Length.Value <= 0)
            {
                throw new InvalidOperationException(
                    $"Product {product.PNo} does not have a valid Length value. Cannot convert piece to CBM.");
            }

            // Width and Height are optional, default to Length if not provided
            decimal lengthMm = (decimal)product.Length.Value;
            decimal widthMm = product.Width.HasValue && product.Width.Value > 0 
                ? (decimal)product.Width.Value 
                : lengthMm;
            decimal heightMm = product.Height.HasValue && product.Height.Value > 0 
                ? (decimal)product.Height.Value 
                : lengthMm;

            // Convert dimensions from mm to meters
            decimal lengthM = lengthMm / 1000m;
            decimal widthM = widthMm / 1000m;
            decimal heightM = heightMm / 1000m;

            // Calculate CBM: cbm = pieces × (L/1000 × W/1000 × H/1000)
            decimal cbm = pieceQuantity * (lengthM * widthM * heightM);

            return cbm;
        }

        /// <summary>
        /// Checks if a product requires cutting based on its category
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <returns>True if product requires cutting, false otherwise</returns>
        public async Task<bool> RequiresCuttingAsync(int productId)
        {
            var product = await _db.PRODUCTS
                .Include(p => p.PRODUCT_CATEGORIES)
                .FirstOrDefaultAsync(p => p.ID == productId);

            if (product == null)
            {
                return false;
            }

            return product.CategoryID.HasValue && 
                   product.PRODUCT_CATEGORIES != null && 
                   product.PRODUCT_CATEGORIES.RequiresCutting;
        }

        /// <summary>
        /// Gets the appropriate unit ID for a product based on whether it requires cutting
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <param name="forPieces">If true, returns piece unit (1), if false returns meter unit (5) for cutting products</param>
        /// <returns>Unit ID</returns>
        public async Task<int> GetUnitIdAsync(int productId, bool forPieces = false)
        {
            var product = await _db.PRODUCTS
                .Include(p => p.PRODUCT_CATEGORIES)
                .FirstOrDefaultAsync(p => p.ID == productId);

            if (product == null)
            {
                return 1; // Default to dona
            }

            bool requiresCutting = product.CategoryID.HasValue && 
                                   product.PRODUCT_CATEGORIES != null && 
                                   product.PRODUCT_CATEGORIES.RequiresCutting;

            if (requiresCutting)
            {
                // For cutting products: 5 = meter, 1 = piece
                return forPieces ? 1 : 5;
            }
            else
            {
                // For non-cutting products, use product's default unit or dona
                return product.UnitID > 0 ? product.UnitID : 1;
            }
        }

        /// <summary>
        /// Converts piece quantity to kg using product's Weight property
        /// Formula: kg = pieces × Weight (kg per piece)
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <param name="pieceQuantity">Quantity in pieces</param>
        /// <returns>Quantity in kg</returns>
        public async Task<decimal> ConvertPieceToKgAsync(int productId, decimal pieceQuantity)
        {
            var product = await _db.PRODUCTS
                .FirstOrDefaultAsync(p => p.ID == productId);

            if (product == null)
            {
                throw new ArgumentException($"Product with ID {productId} not found.");
            }

            if (!product.Weight.HasValue || product.Weight.Value <= 0)
            {
                throw new InvalidOperationException(
                    $"Product {product.PNo} does not have a valid Weight value. Cannot convert piece to kg.");
            }

            // Calculate kg: kg = pieces × Weight (kg per piece)
            decimal kgQuantity = pieceQuantity * (decimal)product.Weight.Value;

            return kgQuantity;
        }

        /// <summary>
        /// Checks if a product should use weight-based calculations (BOM/norm in dona, stock in kg).
        /// True when: product or category has CalculateByWeight, or product's unit is kg; and Weight (per piece) is set.
        /// </summary>
        public async Task<bool> ShouldCalculateByWeightAsync(int productId)
        {
            var product = await _db.PRODUCTS
                .Include(p => p.PRODUCT_CATEGORIES)
                .Include(p => p.UNIT)
                .FirstOrDefaultAsync(p => p.ID == productId);

            if (product == null)
            {
                return false;
            }

            bool hasValidWeight = product.Weight.HasValue && product.Weight.Value > 0;
            if (!hasValidWeight)
            {
                return false;
            }

            // Product-level flag (e.g. shurup)
            if (product.CalculateByWeight == true)
            {
                return true;
            }

            // Category-level flag
            if (product.CategoryID.HasValue && product.PRODUCT_CATEGORIES != null &&
                (product.PRODUCT_CATEGORIES.CalculateByWeight ?? false))
            {
                return true;
            }

            // Product stored in kg (unit name contains kg) -> norm in dona, stock in kg
            if (product.UNIT != null)
            {
                var name = (product.UNIT.UnitName ?? "") + " " + (product.UNIT.ShortName ?? "");
                if (name.IndexOf("kg", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Converts kg quantity to piece quantity using product's Weight (kg per piece).
        /// Formula: pieces = kg / Weight
        /// </summary>
        public async Task<decimal> ConvertKgToPieceAsync(int productId, decimal kgQuantity)
        {
            var product = await _db.PRODUCTS
                .FirstOrDefaultAsync(p => p.ID == productId);

            if (product == null)
            {
                throw new ArgumentException($"Product with ID {productId} not found.");
            }

            if (!product.Weight.HasValue || product.Weight.Value <= 0)
            {
                throw new InvalidOperationException(
                    $"Product {product.PNo} does not have a valid Weight value. Cannot convert kg to piece.");
            }

            decimal pieceQuantity = kgQuantity / (decimal)product.Weight.Value;
            return pieceQuantity;
        }
    }
}
