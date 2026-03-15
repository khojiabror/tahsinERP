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

                            // Check if material should use weight-based calculations
                            bool calculateByWeight = await _conversionService.ShouldCalculateByWeightAsync(material.ID);

                            if (calculateByWeight)
                            {
                                // Convert to pieces first if needed, then to kg
                                decimal pieceQuantity = (decimal)requiredQty;
                                
                                if (requiresCutting)
                                {
                                    // Convert meters to pieces first
                                    requiredQtyMeters = requiredQty;
                                    pieceQuantity = await _conversionService.ConvertMeterToPieceAsync(
                                        material.ID, (decimal)requiredQty);
                                    requiredQtyPieces = (double)pieceQuantity;
                                }
                                else
                                {
                                    requiredQtyPieces = requiredQty;
                                }

                                // Calculate waste in pieces
                                if (normLine.WastePercent.HasValue)
                                {
                                    var wastePercent = (double)normLine.WastePercent.Value;
                                    var wastePieces = requiredQtyPieces * (wastePercent / 100.0);
                                    wasteQty = wastePieces;
                                    totalConsumption = requiredQtyPieces + wasteQty;
                                }
                                else
                                {
                                    totalConsumption = requiredQtyPieces;
                                }

                                // Convert total consumption (pieces) to kg
                                try
                                {
                                    decimal totalConsumptionKg = await _conversionService.ConvertPieceToKgAsync(
                                        material.ID, (decimal)totalConsumption);
                                    totalConsumption = (double)totalConsumptionKg;
                                    requiredQtyPieces = (double)pieceQuantity; // Keep original piece quantity for reference
                                }
                                catch
                                {
                                    // If conversion fails, keep in pieces
                                }
                            }
                            else if (requiresCutting)
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
                        RequiresCutting = requiresCutting,
                        TotalConsumptionIsInKg = calculateByWeight
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

                            // Check if material should use weight-based calculations
                            bool calculateByWeight = await _conversionService.ShouldCalculateByWeightAsync(material.ID);

                            if (calculateByWeight)
                            {
                                // Convert to pieces first if needed, then to kg
                                decimal pieceQuantity = (decimal)requiredQty;
                                
                                if (requiresCutting)
                                {
                                    // Convert meters to pieces first
                                    requiredQtyMeters = requiredQty;
                                    pieceQuantity = await _conversionService.ConvertMeterToPieceAsync(
                                        material.ID, (decimal)requiredQty);
                                    requiredQtyPieces = (double)pieceQuantity;
                                }
                                else
                                {
                                    requiredQtyPieces = requiredQty;
                                }

                                // Convert total consumption (pieces) to kg
                                try
                                {
                                    decimal totalConsumptionKg = await _conversionService.ConvertPieceToKgAsync(
                                        material.ID, pieceQuantity);
                                    totalConsumption = (double)totalConsumptionKg;
                                }
                                catch
                                {
                                    // If conversion fails, keep in pieces
                                    totalConsumption = requiredQtyPieces;
                                }
                            }
                            else if (requiresCutting)
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
                                RequiresCutting = requiresCutting,
                                TotalConsumptionIsInKg = calculateByWeight
                            });
                        }
                    }
                }
            }

            return requirements;
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
        /// For weight-based materials (e.g. shurup) BOM dona is converted to kg; expense and stock deduction use kg.
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
        /// <summary>True when TotalConsumption is in kg (e.g. after piece→kg conversion for weight-based materials).</summary>
        public bool TotalConsumptionIsInKg { get; set; }
    }
}
