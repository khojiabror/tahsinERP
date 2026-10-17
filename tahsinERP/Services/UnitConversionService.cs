using System.Collections.Generic;
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

        // Bitta so'rov (instansiya) ichida mahsulot va birliklar bir marta o'qiladi: avval har metod
        // (RequiresCutting, ShouldCalculateByWeight, Convert...) o'sha mahsulotni qayta so'rardi (SRM-P18).
        private readonly Dictionary<int, PRODUCT> _products = new Dictionary<int, PRODUCT>();
        private List<UNIT> _units;

        private async Task<PRODUCT> GetProductAsync(int productId)
        {
            PRODUCT product;
            if (_products.TryGetValue(productId, out product))
                return product;

            product = await _db.PRODUCTS
                .Include(p => p.PRODUCT_CATEGORIES)
                .Include(p => p.UNIT)
                .FirstOrDefaultAsync(p => p.ID == productId);
            _products[productId] = product;
            return product;
        }

        public UnitConversionService(DBTHSNEntities db)
        {
            _db = db;
        }

        private PRODUCT GetProduct(int productId)
        {
            PRODUCT product;
            if (_products.TryGetValue(productId, out product))
                return product;

            product = _db.PRODUCTS
                .Include(p => p.PRODUCT_CATEGORIES)
                .Include(p => p.UNIT)
                .FirstOrDefault(p => p.ID == productId);
            _products[productId] = product;
            return product;
        }

        private UNIT GetUnit(int unitId)
        {
            if (_units == null)
            {
                _units = _db.UNITS
                    .Where(x => x.IsDeleted == null || x.IsDeleted == false)
                    .OrderBy(x => x.ID)
                    .ToList();
            }
            return _units.FirstOrDefault(u => u.ID == unitId) ?? _db.UNITS.Find(unitId);
        }

        private static bool RequiresCutting(PRODUCT product)
        {
            return product.CategoryID.HasValue && product.PRODUCT_CATEGORIES != null && product.PRODUCT_CATEGORIES.RequiresCutting;
        }

        /// <summary>
        /// Qat'iy (sinxron) konversiya — ombor qoldig'i uchun (SRM-B14). Birliklar bir xil bo'lsa — o'zi;
        /// dona↔kg (mahsulot Weight bo'yicha) va metr↔dona (kesiladigan mahsulot Length bo'yicha) qo'llab-quvvatlanadi.
        /// Qo'llab-quvvatlanmasa yoki mahsulotda kerakli o'lcham yo'q bo'lsa — null (miqdor o'zgarmay qaytarilmaydi).
        /// Sinxron: StockLedger tranzaksiya ichida sinxron ishlaydi (ASP.NET kontekstida .Result deadlock'idan qochish).
        /// </summary>
        public decimal? TryConvert(int productId, decimal quantity, int fromUnitId, int toUnitId)
        {
            if (fromUnitId == toUnitId)
                return quantity;

            var fromUnit = GetUnit(fromUnitId);
            var toUnit = GetUnit(toUnitId);
            var product = GetProduct(productId);
            if (fromUnit == null || toUnit == null || product == null)
                return null;

            var weight = product.Weight.HasValue && product.Weight.Value > 0 ? (decimal)product.Weight.Value : 0m;
            var lengthM = RequiresCutting(product) && product.Length.HasValue && product.Length.Value > 0
                ? (decimal)(product.Length.Value / 1000.0)
                : 0m;

            if (IsKgUnit(fromUnit) && IsDonaUnit(toUnit))
                return weight > 0 ? quantity / weight : (decimal?)null;
            if (IsDonaUnit(fromUnit) && IsKgUnit(toUnit))
                return weight > 0 ? quantity * weight : (decimal?)null;
            if (IsMeterUnit(fromUnit) && IsDonaUnit(toUnit))
                return lengthM > 0 ? Math.Ceiling(quantity / lengthM) : (decimal?)null;
            if (IsDonaUnit(fromUnit) && IsMeterUnit(toUnit))
                return lengthM > 0 ? quantity * lengthM : (decimal?)null;

            return null;
        }

        /// <summary>Birlik nomi (xato xabarlari uchun).</summary>
        public string UnitName(int unitId)
        {
            var u = GetUnit(unitId);
            return u == null ? "#" + unitId : (u.ShortName ?? u.UnitName ?? "#" + unitId);
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
            var product = await GetProductAsync(productId);

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
            var product = await GetProductAsync(productId);

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
            var product = await GetProductAsync(productId);

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
            var product = await GetProductAsync(productId);

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
            var product = await GetProductAsync(productId);

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
            var product = await GetProductAsync(productId);

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

            // Metiz (shurup, bolt va h.k.): kategoriyada CalculateByWeight qo'lda yoqilmagan bo'lsa ham,
            // dona → kg (Weight dona massasi) — xarid/ombor bilan mos.
            if (product.PRODUCT_CATEGORIES != null && IsMetizHardwareCategory(product.PRODUCT_CATEGORIES))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// BOM/xarid generatsiyasida metiz guruhini aniqlash (CalculateByWeight flag qo'shilmagan mahsulotlar uchun).
        /// </summary>
        private static bool IsMetizHardwareCategory(PRODUCT_CATEGORIES c)
        {
            if (c == null)
            {
                return false;
            }

            var parts = new[] { c.Name, c.RawMaterialGroup, c.RawMaterialSubGrp, c.Code };
            foreach (var p in parts)
            {
                if (string.IsNullOrWhiteSpace(p))
                {
                    continue;
                }

                var t = p.Trim().ToLowerInvariant();
                if (t.IndexOf("metiz", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.IndexOf("shurup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.IndexOf("murvat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.IndexOf("bolt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.IndexOf("gaika", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.IndexOf("крепеж", StringComparison.OrdinalIgnoreCase) >= 0)
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
            var product = await GetProductAsync(productId);

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

        /// <summary>
        /// Converts quantity from one unit to another for a product (e.g. dona -> kg, kg -> dona).
        /// Uses product Weight for piece &lt;-&gt; kg. If units are the same or conversion is not supported, returns quantity unchanged.
        /// </summary>
        /// <param name="productId">Product ID (for Weight when converting dona &lt;-&gt; kg)</param>
        /// <param name="quantity">Quantity in source unit</param>
        /// <param name="fromUnitId">Source unit ID</param>
        /// <param name="toUnitId">Target unit ID (e.g. ombor qoldig'i birligi)</param>
        /// <returns>Quantity in target unit</returns>
        public async Task<decimal> ConvertToUnitAsync(int productId, decimal quantity, int fromUnitId, int toUnitId)
        {
            if (fromUnitId == toUnitId)
                return quantity;

            var fromUnit = await _db.UNITS.FindAsync(fromUnitId);
            var toUnit = await _db.UNITS.FindAsync(toUnitId);
            if (fromUnit == null || toUnit == null)
                return quantity;

            var fromIsKg = IsKgUnit(fromUnit);
            var toIsKg = IsKgUnit(toUnit);
            var fromIsDona = IsDonaUnit(fromUnit);
            var toIsDona = IsDonaUnit(toUnit);
            var fromIsMeter = IsMeterUnit(fromUnit);
            var toIsMeter = IsMeterUnit(toUnit);

            if (fromIsKg && toIsDona)
                return await ConvertKgToPieceAsync(productId, quantity);
            if (fromIsDona && toIsKg)
                return await ConvertPieceToKgAsync(productId, quantity);

            // Yog'och: metr (zaxira) &lt;-&gt; dona (shtuk)
            if (fromIsMeter && toIsDona)
                return await ConvertMeterToPieceAsync(productId, quantity);
            if (fromIsDona && toIsMeter)
                return await ConvertPieceToMeterAsync(productId, quantity);

            return quantity;
        }

        /// <summary>
        /// Yog'och shtukasi uzunligi bo'yicha: dona → metr (bir dona = mahsulot Length mm / 1000).
        /// </summary>
        public async Task<decimal> ConvertPieceToMeterAsync(int productId, decimal pieceQuantity)
        {
            var product = await GetProductAsync(productId);

            if (product == null)
                throw new ArgumentException($"Product with ID {productId} not found.");

            bool requiresCutting = product.CategoryID.HasValue &&
                                   product.PRODUCT_CATEGORIES != null &&
                                   product.PRODUCT_CATEGORIES.RequiresCutting;

            if (!requiresCutting)
                return pieceQuantity;

            if (!product.Length.HasValue || product.Length.Value <= 0)
                throw new InvalidOperationException(
                    $"Product {product.PNo} does not have a valid Length value. Cannot convert piece to meter.");

            decimal lengthInMeters = (decimal)(product.Length.Value / 1000.0);
            return pieceQuantity * lengthInMeters;
        }

        /// <summary>
        /// Birinchi mos keladigan birlik ID (masalan, kg, dona, m) — ombor kirimi bilan solishtirish uchun.
        /// </summary>
        public async Task<int?> FindFirstUnitIdAsync(Func<UNIT, bool> predicate)
        {
            if (_units == null)
            {
                _units = await _db.UNITS
                    .Where(x => x.IsDeleted == null || x.IsDeleted == false)
                    .OrderBy(x => x.ID)
                    .ToListAsync();
            }
            var u = _units.FirstOrDefault(predicate);
            return u != null ? (int?)u.ID : null;
        }

        public Task<int?> FindKgUnitIdAsync() => FindFirstUnitIdAsync(IsKgUnit);

        public Task<int?> FindDonaUnitIdAsync() => FindFirstUnitIdAsync(IsDonaUnit);

        public Task<int?> FindMeterUnitIdAsync() => FindFirstUnitIdAsync(IsMeterUnit);

        private static bool IsKgUnit(UNIT u)
        {
            if (u == null) return false;
            var s = ((u.ShortName ?? "") + " " + (u.UnitName ?? "")).ToLowerInvariant();
            return s.IndexOf("kg", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsDonaUnit(UNIT u)
        {
            if (u == null) return false;
            var s = ((u.ShortName ?? "") + " " + (u.UnitName ?? "")).ToLowerInvariant();
            return s.IndexOf("dona", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("pc", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("pcs", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Metr (mm emas) — yog'och zaxira o'lchovi.</summary>
        private static bool IsMeterUnit(UNIT u)
        {
            if (u == null) return false;
            var sn = (u.ShortName ?? "").Trim().ToLowerInvariant();
            var un = (u.UnitName ?? "").ToLowerInvariant();
            if (sn == "mm" || un.Contains("milli") || un.Contains("мм")) return false;
            if (sn == "m" || sn == "m." || un.Contains("metr") || un.Contains("метр")) return true;
            return false;
        }
    }
}
