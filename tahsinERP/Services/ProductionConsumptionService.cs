using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Controllers;
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
                // Asosiy yo'l: NORM_INPUT_LINES dagi sarf/chiqindini 1:1 ishlatish.
                // Bunda normadagi birlik saqlanadi, keyin faqat ombor kirim birligiga konvert qilinadi.
                var linkedNorm = await _db.NORMVERSIONS.FirstOrDefaultAsync(n => n.ID == normId.Value);
                var normMonth = linkedNorm?.EffectiveFrom.HasValue == true
                    ? linkedNorm.EffectiveFrom.Value.ToString("yyyy-MM")
                    : null;

                var normInputQuery = _db.NORM_INPUT_LINES
                    .Include(n => n.PRODUCT1)
                    .Include(n => n.PRODUCT1.PRODUCT_CATEGORIES)
                    .Where(n => n.ProductID == productId);
                if (!string.IsNullOrWhiteSpace(normMonth))
                {
                    normInputQuery = normInputQuery.Where(n => n.Month == normMonth);
                }

                var normInputLines = await normInputQuery
                    .OrderByDescending(n => n.CreatedDT)
                    .ThenByDescending(n => n.ID)
                    .ToListAsync();

                // Bir xil variantlar bo'yicha eng oxirgi satrni olish (eski dublikatlarni hisobga qo'shmaslik uchun)
                var dedupedNormInputLines = normInputLines
                    .GroupBy(n => new { n.MaterialID, n.CutLengthMm, n.PiecesPerProduct, UnitID = n.UnitID ?? 0 })
                    .Select(g => g.OrderByDescending(x => x.CreatedDT).ThenByDescending(x => x.ID).First())
                    .ToList();

                foreach (var line in dedupedNormInputLines)
                {
                    var material = line.PRODUCT1;
                    if (material == null)
                        continue;

                    bool requiresCutting = material.PRODUCT_CATEGORIES?.RequiresCutting ?? false;

                    var consumptionPerProduct = (double)NormInputLineConsumptionToDisplay(
                        requiresCutting,
                        line.CutLengthMm,
                        line.Consumption);
                    var wastePerProduct = (double)NormInputLineWasteToDisplay(line.WasteAmount);
                    var totalPerProduct = consumptionPerProduct + wastePerProduct;
                    if (totalPerProduct <= 0)
                        continue;

                    var totalConsumption = totalPerProduct * producedQty;
                    var wasteQty = wastePerProduct * producedQty;
                    var baseUnitId = (line.UnitID.HasValue && line.UnitID.Value > 0)
                        ? line.UnitID.Value
                        : (material.UnitID > 0 ? material.UnitID : 1);

                    requirements.Add(new ProductionRawMaterialRequirement
                    {
                        MaterialID = material.ID,
                        MaterialPNo = material.PNo,
                        MaterialName = material.Name,
                        RequiredQtyPieces = consumptionPerProduct * producedQty,
                        RequiredQtyMeters = requiresCutting ? consumptionPerProduct * producedQty : 0,
                        WasteQty = wasteQty,
                        TotalConsumption = totalConsumption,
                        UnitID = baseUnitId,
                        RequiresCutting = requiresCutting,
                        TotalConsumptionIsInKg = false
                    });
                }

                // Agar NORM_INPUT_LINES topilmasa, eski NORMLINES fallback ishlasin.
                if (!requirements.Any())
                {
                    var normLines = await _db.NORMLINES
                        .Where(nl => nl.NormID == normId.Value)
                        .ToListAsync();

                    foreach (var normLine in normLines)
                    {
                        if (!normLine.MaterialID.HasValue || !normLine.QtyPerUnit.HasValue)
                            continue;

                        var material = await _db.PRODUCTS
                            .Include(p => p.PRODUCT_CATEGORIES)
                            .FirstOrDefaultAsync(p => p.ID == normLine.MaterialID.Value);
                        if (material == null)
                            continue;

                        var qtyPerUnit = (double)normLine.QtyPerUnit.Value;
                        var requiredQty = qtyPerUnit * producedQty;
                        var wasteQty = normLine.WastePercent.HasValue
                            ? requiredQty * ((double)normLine.WastePercent.Value / 100.0)
                            : 0.0;
                        var totalConsumption = requiredQty + wasteQty;
                        int unitId = normLine.UnitID ?? material.UnitID;

                        requirements.Add(new ProductionRawMaterialRequirement
                        {
                            MaterialID = material.ID,
                            MaterialPNo = material.PNo,
                            MaterialName = material.Name,
                            RequiredQtyPieces = requiredQty,
                            RequiredQtyMeters = 0,
                            WasteQty = wasteQty,
                            TotalConsumption = totalConsumption,
                            UnitID = unitId > 0 ? unitId : material.UnitID,
                            RequiresCutting = material.PRODUCT_CATEGORIES?.RequiresCutting ?? false,
                            TotalConsumptionIsInKg = false
                        });
                    }
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

                            bool requiresCutting = material.CategoryID.HasValue &&
                                                   material.PRODUCT_CATEGORIES != null &&
                                                   material.PRODUCT_CATEGORIES.RequiresCutting;

                            bool calculateByWeight = await _conversionService.ShouldCalculateByWeightAsync(material.ID);

                            if (calculateByWeight)
                            {
                                decimal pieceQuantity = (decimal)requiredQty;

                                if (requiresCutting)
                                {
                                    requiredQtyMeters = requiredQty;
                                    pieceQuantity = await _conversionService.ConvertMeterToPieceAsync(
                                        material.ID, (decimal)requiredQty);
                                    requiredQtyPieces = (double)pieceQuantity;
                                }
                                else
                                {
                                    requiredQtyPieces = requiredQty;
                                }

                                try
                                {
                                    decimal totalConsumptionKg = await _conversionService.ConvertPieceToKgAsync(
                                        material.ID, pieceQuantity);
                                    totalConsumption = (double)totalConsumptionKg;
                                }
                                catch
                                {
                                    totalConsumption = requiredQtyPieces;
                                }
                            }
                            else if (requiresCutting)
                            {
                                requiredQtyMeters = requiredQty;
                                requiredQtyPieces = (double)await _conversionService.ConvertMeterToPieceAsync(
                                    material.ID, (decimal)requiredQty);
                                totalConsumption = requiredQtyPieces;
                            }

                            int unitId = bomLine.UnitID ?? material.UnitID;

                            requirements.Add(new ProductionRawMaterialRequirement
                            {
                                MaterialID = material.ID,
                                MaterialPNo = material.PNo,
                                MaterialName = material.Name,
                                RequiredQtyPieces = requiredQtyPieces,
                                RequiredQtyMeters = requiredQtyMeters,
                                WasteQty = wasteQty,
                                TotalConsumption = totalConsumption,
                                UnitID = unitId > 0 ? unitId : material.UnitID,
                                RequiresCutting = requiresCutting,
                                TotalConsumptionIsInKg = calculateByWeight
                            });
                        }
                    }
                }
            }

            await NormalizeRequirementsToInventoryUnitsAsync(requirements);

            return requirements;
        }

        /// <summary>
        /// BOM/NORM hisobi o'z birligida; chiqim va ombor qoldig'i birinchi kirim birligida bo'lishi kerak.
        /// </summary>
        private async Task NormalizeRequirementsToInventoryUnitsAsync(List<ProductionRawMaterialRequirement> requirements)
        {
            foreach (var req in requirements)
            {
                var invUnitId = GetDisplayUnitFromIncome(req.MaterialID);
                if (!invUnitId.HasValue)
                    continue;

                var sourceUnitId = await ResolveSourceUnitIdAsync(req);
                if (!sourceUnitId.HasValue)
                    continue;

                if (sourceUnitId.Value == invUnitId.Value)
                {
                    req.UnitID = invUnitId.Value;
                    continue;
                }

                var qty = await _conversionService.ConvertToUnitAsync(
                    req.MaterialID, (decimal)req.TotalConsumption, sourceUnitId.Value, invUnitId.Value);
                req.TotalConsumption = (double)qty;

                if (req.WasteQty > 0)
                {
                    var w = await _conversionService.ConvertToUnitAsync(
                        req.MaterialID, (decimal)req.WasteQty, sourceUnitId.Value, invUnitId.Value);
                    req.WasteQty = (double)w;
                }

                req.UnitID = invUnitId.Value;
                req.TotalConsumptionIsInKg = false;
            }
        }

        /// <summary>
        /// Hisoblangan miqdorning fizik birligi (kirimga o'tkazishdan oldin).
        /// </summary>
        private async Task<int?> ResolveSourceUnitIdAsync(ProductionRawMaterialRequirement req)
        {
            if (req.TotalConsumptionIsInKg)
            {
                var kgId = await _conversionService.FindKgUnitIdAsync();
                if (!kgId.HasValue)
                {
                    throw new InvalidOperationException(
                        $"UNITS jadvalida kg birligi topilmadi (mahsulot: {req.MaterialPNo ?? req.MaterialID.ToString()}).");
                }
                return kgId;
            }

            if (req.UnitID > 0)
            {
                return req.UnitID;
            }

            return (await _conversionService.FindDonaUnitIdAsync()) ?? 1;
        }

        /// <summary>
        /// Faqat income'dan birlik: birinchi kirimda qanday unit ishlatilgan bo'lsa shuni qaytaradi.
        /// BOM/material UnitID ga tushmaydi. Expense va ProductProduction/Details da bir xil ko'rsatish uchun.
        /// </summary>
        public int? GetDisplayUnitFromIncome(int materialId)
        {
            var firstIp = _db.INCOME_PRODUCTS
                .Include(ip => ip.INCOME)
                .Where(ip => ip.ProductID == materialId
                    && (ip.INCOME.IsDeleted == null || ip.INCOME.IsDeleted == false))
                .OrderBy(ip => ip.INCOME.IssueDateTime)
                .FirstOrDefault();

            return firstIp != null ? (int?)firstIp.UnitID : null;
        }

        private static decimal NormInputLineConsumptionToDisplay(bool requiresCutting, int cutLengthMm, double? consumptionDb)
        {
            if (!consumptionDb.HasValue || consumptionDb.Value <= 0)
                return 0;
            if (requiresCutting && cutLengthMm > 0)
                return (decimal)(consumptionDb.Value / 1000.0);
            return (decimal)consumptionDb.Value;
        }

        private static decimal NormInputLineWasteToDisplay(double? wasteAmountDb)
        {
            if (!wasteAmountDb.HasValue || wasteAmountDb.Value <= 0)
                return 0;
            return (decimal)(wasteAmountDb.Value / 1000.0);
        }

        /// <summary>
        /// FIFO: Gets piece price and total price for consuming given qty from oldest income batches first.
        /// </summary>
        private async Task<(double piecePrice, double totalPrice)> GetFifoPricesAsync(int materialId, int warehouseId, double requiredQty)
        {
            var incomeProducts = await _db.INCOME_PRODUCTS
                .Include(ip => ip.INCOME)
                .Where(ip => ip.ProductID == materialId
                    && ip.INCOME.ReceivingWHID == warehouseId
                    && (ip.INCOME.IsDeleted == null || ip.INCOME.IsDeleted == false)
                    && ip.Amount > 0)
                .OrderBy(ip => ip.INCOME.IssueDateTime)
                .ToListAsync();

            if (incomeProducts == null || !incomeProducts.Any())
            {
                return (0, 0);
            }

            double remainingToConsume = requiredQty;
            double totalPriceSum = 0;
            double totalQtyTaken = 0;

            foreach (var ip in incomeProducts)
            {
                if (remainingToConsume <= 0) break;

                var piecePrice = (ip.PiecePrice ?? 0);
                if (piecePrice <= 0 && ip.TotalPrice.HasValue && ip.Amount > 0)
                {
                    piecePrice = (double)(ip.TotalPrice.Value / ip.Amount);
                }

                var takeQty = Math.Min(ip.Amount, remainingToConsume);
                totalPriceSum += takeQty * piecePrice;
                totalQtyTaken += takeQty;
                remainingToConsume -= takeQty;
            }

            if (totalQtyTaken <= 0)
            {
                return (0, 0);
            }

            var avgPiecePrice = totalPriceSum / totalQtyTaken;
            var totalPrice = avgPiecePrice * requiredQty;
            return (avgPiecePrice, totalPrice);
        }

        /// <summary>
        /// Consumes materials from the given warehouse when production is confirmed.
        /// Og'irlik bo'yicha (kg) va kesish (m → dona) uchun UnitConversionService ishlatiladi.
        /// Creates Expense document without linked Income.
        /// </summary>
        public async Task ConsumeMaterialsForProductionAsync(int productId, double producedQty, int warehouseId, int? productionProcessId = null, DateTime? productionDate = null)
        {
            var requirements = await CalculateRequiredRawMaterialsAsync(productId, producedQty);
            var wasteService = new WasteHandlingService(_db);

            if (requirements == null || !requirements.Any())
            {
                return;
            }

            // Validate stock before creating expense
            foreach (var req in requirements)
            {
                var stock = await _db.PRODUCT_STOCKS
                    .FirstOrDefaultAsync(s => s.WHID == warehouseId && s.ProductID == req.MaterialID);

                if (stock == null || stock.Amount < req.TotalConsumption)
                {
                    throw new InvalidOperationException(
                        $"Omborda yetarli qoldiq yo'q. Mahsulot: {req.MaterialPNo}, Talab: {req.TotalConsumption}, Mavjud: {stock?.Amount ?? 0}");
                }
            }

            var currency = await _db.CURRENCIES
                .FirstOrDefaultAsync(c => c.Shortname != null && c.Shortname.Trim().ToUpper() == "UZS")
                ?? await _db.CURRENCIES.OrderBy(c => c.ID).FirstOrDefaultAsync();
            int currencyId = currency != null ? currency.ID : 1;

            var product = await _db.PRODUCTS.FindAsync(productId);
            var productPNo = product?.PNo ?? productId.ToString();
            var dateStr = DateTime.Now.ToString("yyyyMMdd");
            var docNo = $"{dateStr}_{productPNo}_{producedQty}";
            var description = productionDate.HasValue
                ? $"{productionDate.Value:dd.MM.yyyy}y. kunlik reja asosida Ishlab chiqarish uchun xomashyo chiqimi: {productPNo}, Miqdor {producedQty}"
                : $"Ishlab chiqarish uchun xomashyo chiqimi: {productPNo}, Miqdor {producedQty}";
            if (productionProcessId.HasValue && productionProcessId.Value > 0)
            {
                description += $" | ProductionID:{productionProcessId.Value}";
            }

            var expense = new EXPENS
            {
                DocNo = docNo,
                IssueDateTime = DateTime.Now,
                SenderWHID = warehouseId,
                ReceiverWhID = null,
                CurrencyID = currencyId,
                Amount = 0,
                TotalPrice = 0,
                Description = description,
                SendStatus = true,
                IsDeleted = false
            };

            _db.EXPENSES.Add(expense);
            await _db.SaveChangesAsync();

            double totalAmount = 0;
            double totalExpensePrice = 0;

            foreach (var req in requirements)
            {
                var (piecePrice, totalPrice) = await GetFifoPricesAsync(req.MaterialID, warehouseId, req.TotalConsumption);
                var displayUnitId = GetDisplayUnitFromIncome(req.MaterialID);
                if (!displayUnitId.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Mahsulot {req.MaterialPNo ?? req.MaterialID.ToString()} uchun kirim topilmadi. Birlik aniqlanishi uchun avval xomashyoni kirim qilishingiz kerak.");
                }

                var expenseProduct = new EXPENSE_PRODUCTS
                {
                    ExpenseID = expense.ID,
                    ProductID = req.MaterialID,
                    Amount = req.TotalConsumption,
                    PiecePrice = piecePrice,
                    TotalPrice = totalPrice,
                    UnitID = displayUnitId.Value,
                    Comment = "BOM/NORM sarfi"
                };

                _db.EXPENSE_PRODUCTS.Add(expenseProduct);
                totalAmount += req.TotalConsumption;
                totalExpensePrice += totalPrice;

                // Get stock in warehouse
                var stock = await _db.PRODUCT_STOCKS
                    .FirstOrDefaultAsync(s => s.WHID == warehouseId && s.ProductID == req.MaterialID);

                if (stock == null)
                {
                    throw new Exception($"Omborda mahsulot qoldig'i topilmadi: {req.MaterialPNo ?? req.MaterialID.ToString()}");
                }

                if (stock.Amount < req.TotalConsumption)
                {
                    throw new Exception(
                        $"Omborda yetarli qoldiq yo'q. " +
                        $"Mahsulot: {req.MaterialPNo ?? req.MaterialID.ToString()}, " +
                        $"Mavjud: {stock.Amount}, Talab: {req.TotalConsumption}");
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
                        warehouseId, 
                        req.UnitID,
                        $"Ishlab chiqarish chiqindisi: Mahsulot ID {productId}, Miqdor {producedQty}");
                }
            }

            expense.Amount = totalAmount;
            expense.TotalPrice = totalExpensePrice;
            await _db.SaveChangesAsync();

            LogHelper.LogToDatabase("system", "ProductionConsumptionService", "ConsumeMaterialsForProductionAsync",
                $"Xomashyo chiqimi yaratildi. Expense ID: {expense.ID}, Mahsulot ID: {productId}, Miqdor: {producedQty}");
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
        /// <summary>True bo'lsa, TotalConsumption kg (piece→kg konversiyasidan keyin).</summary>
        public bool TotalConsumptionIsInKg { get; set; }
    }
}
