using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    public class TannarxService
    {
        private readonly DBTHSNEntities _db;

        public TannarxService(DBTHSNEntities db)
        {
            _db = db;
        }

        /// <summary>
        /// Calculate material price from NORM_INPUT_LINES for a product and month
        /// </summary>
        public async Task<decimal> CalculateMaterialPriceAsync(int productID, string month)
        {
            var normLines = await _db.NORM_INPUT_LINES
                .Where(n => n.ProductID == productID && n.Month == month)
                .ToListAsync();

            if (!normLines.Any())
                return 0;

            decimal total = 0;
            foreach (var line in normLines)
            {
                if (line.Price <= 0)
                {
                    continue;
                }

                decimal consumptionMeters = 0;
                if (line.Consumption.HasValue && line.Consumption.Value > 0)
                {
                    consumptionMeters = (decimal)(line.Consumption.Value / 1000.0);
                }
                else if (line.CutLengthMm > 0)
                {
                    consumptionMeters = (decimal)(line.CutLengthMm / 1000.0) * line.PiecesPerProduct;
                }
                else
                {
                    consumptionMeters = line.PiecesPerProduct;
                }

                decimal wasteMeters = 0;
                if (line.WasteAmount.HasValue && line.WasteAmount.Value > 0)
                {
                    wasteMeters = (decimal)(line.WasteAmount.Value / 1000.0);
                }

                total += (consumptionMeters + wasteMeters) * line.Price;
            }

            return total;
        }

        /// <summary>
        /// Calculate labor cost (A - Ish haqi) from HR_PIECE_RATE_TARIFFS
        /// Sum all RatePerUnit for all OperationIDs related to the ProductID
        /// </summary>
        public async Task<decimal> CalculateLaborCostAsync(int productID)
        {
            var now = DateTime.Now;
            
            var tariffs = await _db.HR_PIECE_RATE_TARIFFS
                .Where(t => t.ProductID == productID 
                    && t.IsActive == true
                    && t.EffectiveFrom <= now
                    && (t.EffectiveTo == null || t.EffectiveTo >= now))
                .ToListAsync();

            if (!tariffs.Any())
                return 0;

            // Sum all RatePerUnit for all operations
            return tariffs.Sum(t => t.RatePerUnit);
        }

        /// <summary>
        /// Calculate material transport cost (MaterialPrice * 0.02 = 2%)
        /// </summary>
        public decimal CalculateMaterialTransportCost(decimal materialPrice)
        {
            return materialPrice * 0.02m;
        }

        /// <summary>
        /// Calculate expenses based on percentage of SetPriceEXWWithVAT
        /// </summary>
        public decimal CalculateExpenseByPercent(decimal basePrice, decimal? percent)
        {
            if (!percent.HasValue || percent.Value <= 0)
                return 0;

            return basePrice * (percent.Value / 100m);
        }

        /// <summary>
        /// Calculate EXW price without VAT: SUM(a to e + material tashuvi narx + material narxi)
        /// </summary>
        public decimal CalculateEXWWithoutVAT(
            decimal materialPrice,
            decimal materialTransportCost,
            decimal laborCost,
            decimal taxAndBank,
            decimal administrativeExpenses,
            decimal otherExpenses,
            decimal profit)
        {
            return materialPrice 
                + materialTransportCost 
                + laborCost 
                + taxAndBank 
                + administrativeExpenses 
                + otherExpenses 
                + profit;
        }

        /// <summary>
        /// Calculate EXW price with VAT: EXWWithoutVAT * 1.12
        /// </summary>
        public decimal CalculateEXWWithVAT(decimal exwWithoutVAT)
        {
            return exwWithoutVAT * 1.12m;
        }

        /// <summary>
        /// Calculate CIP price without VAT: EXWWithoutVAT + FinishedProductTransportCost
        /// </summary>
        public decimal CalculateCIPWithoutVAT(decimal exwWithoutVAT, decimal? finishedProductTransportCost)
        {
            return exwWithoutVAT + (finishedProductTransportCost ?? 0);
        }

        /// <summary>
        /// Calculate CIP price with VAT: CIPWithoutVAT * 1.12
        /// </summary>
        public decimal CalculateCIPWithVAT(decimal cipWithoutVAT)
        {
            return cipWithoutVAT * 1.12m;
        }

        /// <summary>
        /// Check if product has norms (NORM_INPUT_LINES or NORMLINES) for the given month
        /// Supports multiple Month formats: "2025-09", "2025-9", "Sentabr-25", etc.
        /// If NORM_INPUT_LINES exists for the month, return true.
        /// If NORMVERSION exists (regardless of date range) and has NORMLINES, return true.
        /// </summary>
        public async Task<bool> HasNormsForMonthAsync(int productID, int year, int month)
        {
            // Try different Month format variations (based on how NormController saves it)
            var monthFormats = new List<string>
            {
                $"{year}-{month:D2}",      // "2025-09" (standard format from NormController)
                $"{year}-{month}",         // "2025-9" (without leading zero)
                $"{GetMonthName(month)}-{year % 100:D2}",  // "Sentabr-25"
                $"{GetMonthName(month)}-{year % 100}",    // "Sentabr-25" (without leading zero)
            };

            // Check NORM_INPUT_LINES for the specific month (try all formats)
            // Use ToListAsync first, then check in memory to support multiple formats
            var normInputLines = await _db.NORM_INPUT_LINES
                .Where(n => n.ProductID == productID && n.Month != null)
                .Select(n => n.Month)
                .ToListAsync();

            var hasNormInputLines = normInputLines.Any(m => monthFormats.Contains(m));

            if (hasNormInputLines)
                return true;

            // Check NORMLINES through NORMVERSION
            // IMPORTANT: Check ALL NORMVERSIONS for the product (not just active ones)
            // because NormController shows all versions regardless of date range
            var normVersions = await _db.NORMVERSIONS
                .Include(nv => nv.NORMLINES)
                .Where(nv => nv.ProductID.HasValue && nv.ProductID.Value == productID)
                .ToListAsync();

            // Check if any NORMVERSION has NORMLINES
            var hasNormLines = normVersions.Any(nv => nv.NORMLINES != null && nv.NORMLINES.Any());

            return hasNormLines;
        }

        /// <summary>
        /// Get count of finished goods products that have norms for a specific month
        /// </summary>
        public async Task<int> GetFinishedGoodsWithNormsCountAsync(int year, int month)
        {
            // Get all finished goods products
            // Check both PRODUCT.IsFinishedGood and PRODUCT_CATEGORIES.IsFinishedGood
            var finishedGoods = await _db.PRODUCTS
                .Include(p => p.PRODUCT_CATEGORIES)
                .Where(p => p.IsDeleted == false 
                    && (p.IsFinishedGood == true 
                        || (p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.IsFinishedGood == true)))
                .Select(p => p.ID)
                .ToListAsync();

            int count = 0;
            
            // Check each product for norms
            foreach (var productId in finishedGoods)
            {
                if (await HasNormsForMonthAsync(productId, year, month))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Generate tannarx for all finished goods products for a specific month
        /// Only generates for products that have norms (NORM_INPUT_LINES or NORMLINES)
        /// </summary>
        public async Task<int> GenerateTannarxForMonthAsync(int year, int month, int? createdByUserID = null)
        {
            var monthString = $"{year}-{month:D2}";
            var monthName = GetMonthName(month);
            var monthStringDisplay = $"{monthName}-{year % 100:D2}";

            // Get all products where product_category.IsFinishedGood == true or PRODUCT.IsFinishedGood == true
            var finishedGoods = await _db.PRODUCTS
                .Include(p => p.PRODUCT_CATEGORIES)
                .Where(p => p.IsDeleted == false 
                    && (p.IsFinishedGood == true 
                        || (p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.IsFinishedGood == true)))
                .ToListAsync();

            int generatedCount = 0;

            foreach (var product in finishedGoods)
            {
                // Check if tannarx already exists for this product and month
                var existing = await _db.TANNARXes
                    .FirstOrDefaultAsync(t => t.ProductID == product.ID 
                        && t.Year == year 
                        && t.Month == month 
                        && !t.IsDeleted);

                if (existing != null)
                    continue; // Skip if already exists

                // Check if product has norms (NORM_INPUT_LINES or NORMLINES) for this month
                var hasNorms = await HasNormsForMonthAsync(product.ID, year, month);
                if (!hasNorms)
                    continue; // Skip if no norms exist

                // Calculate material price from NORM tables
                var materialPrice = await CalculateMaterialPriceAsync(product.ID, monthString);

                // Calculate material transport cost (2% of material price)
                var materialTransportCost = CalculateMaterialTransportCost(materialPrice);

                // Calculate labor cost from HR_PIECE_RATE_TARIFFS
                var laborCost = await CalculateLaborCostAsync(product.ID);

                // Default percentages
                var taxAndBankPercent = 5m;
                var administrativeExpensesPercent = 5m;
                var otherExpensesPercent = 5m;
                var profitPercent = 10m;

                // For initial calculation, use material price as base
                // Later, when SetPriceEXWWithVAT is set, recalculate based on that
                var basePrice = materialPrice + materialTransportCost + laborCost;
                
                // Calculate expenses (will be recalculated when SetPriceEXWWithVAT is set)
                var taxAndBank = CalculateExpenseByPercent(basePrice, taxAndBankPercent);
                var administrativeExpenses = CalculateExpenseByPercent(basePrice, administrativeExpensesPercent);
                var otherExpenses = CalculateExpenseByPercent(basePrice, otherExpensesPercent);
                var profit = CalculateExpenseByPercent(basePrice, profitPercent);

                // Calculate EXW prices
                var exwWithoutVAT = CalculateEXWWithoutVAT(
                    materialPrice, materialTransportCost, laborCost,
                    taxAndBank, administrativeExpenses, otherExpenses, profit);
                var exwWithVAT = CalculateEXWWithVAT(exwWithoutVAT);

                // Set suggested price (can be edited by user)
                var setPriceEXWWithVAT = exwWithVAT;

                // CIP prices (will be calculated when FinishedProductTransportCost is set)
                var cipWithoutVAT = exwWithoutVAT;
                var cipWithVAT = CalculateCIPWithVAT(cipWithoutVAT);
                var setPriceCIPWithVAT = cipWithVAT;

                // Create new TANNARX record
                var tannarx = new TANNARX
                {
                    ProductID = product.ID,
                    Year = year,
                    Month = month,
                    MonthString = monthStringDisplay,
                    MaterialPrice = materialPrice,
                    MaterialTransportCost = materialTransportCost,
                    LaborCost = laborCost,
                    TaxAndBank = taxAndBank,
                    AdministrativeExpenses = administrativeExpenses,
                    OtherExpenses = otherExpenses,
                    Profit = profit,
                    TaxAndBankPercent = taxAndBankPercent,
                    AdministrativeExpensesPercent = administrativeExpensesPercent,
                    OtherExpensesPercent = otherExpensesPercent,
                    ProfitPercent = profitPercent,
                    SalesPriceEXWWithoutVAT = exwWithoutVAT,
                    SalesPriceEXWWithVAT = exwWithVAT,
                    SetPriceEXWWithVAT = setPriceEXWWithVAT,
                    SalesPriceCIPWithoutVAT = cipWithoutVAT,
                    SalesPriceCIPWithVAT = cipWithVAT,
                    SetPriceCIPWithVAT = setPriceCIPWithVAT,
                    CreatedByUserID = createdByUserID,
                    CreatedDT = DateTime.Now,
                    IsDeleted = false
                };

                _db.TANNARXes.Add(tannarx);
                generatedCount++;
            }

            if (generatedCount > 0)
            {
                await _db.SaveChangesAsync();
            }

            return generatedCount;
        }

        /// <summary>
        /// Recalculate all prices when SetPriceEXWWithVAT is changed
        /// B, C, D, E are calculated as percentages of SetPriceEXWWithVAT
        /// Then EXWWithoutVAT = MaterialPrice + MaterialTransportCost + LaborCost + B + C + D + E
        /// </summary>
        public void RecalculatePricesFromSetPriceEXW(TANNARX tannarx)
        {
            if (!tannarx.SetPriceEXWWithVAT.HasValue)
                return;

            var setPriceEXW = tannarx.SetPriceEXWWithVAT.Value;

            // Recalculate B, C, D, E based on SetPriceEXWWithVAT (as percentages)
            tannarx.TaxAndBank = CalculateExpenseByPercent(setPriceEXW, tannarx.TaxAndBankPercent ?? 5m);
            tannarx.AdministrativeExpenses = CalculateExpenseByPercent(setPriceEXW, tannarx.AdministrativeExpensesPercent ?? 5m);
            tannarx.OtherExpenses = CalculateExpenseByPercent(setPriceEXW, tannarx.OtherExpensesPercent ?? 5m);
            tannarx.Profit = CalculateExpenseByPercent(setPriceEXW, tannarx.ProfitPercent ?? 10m);

            // Recalculate EXW without VAT: SUM(a to e + material tashuvi narx + material narxi)
            var exwWithoutVAT = CalculateEXWWithoutVAT(
                tannarx.MaterialPrice,
                tannarx.MaterialTransportCost,
                tannarx.LaborCost,
                tannarx.TaxAndBank,
                tannarx.AdministrativeExpenses,
                tannarx.OtherExpenses,
                tannarx.Profit);
            
            tannarx.SalesPriceEXWWithoutVAT = exwWithoutVAT;
            
            // Recalculate EXW with VAT
            tannarx.SalesPriceEXWWithVAT = CalculateEXWWithVAT(exwWithoutVAT);

            // Recalculate CIP prices
            var cipWithoutVAT = CalculateCIPWithoutVAT(exwWithoutVAT, tannarx.FinishedProductTransportCost);
            tannarx.SalesPriceCIPWithoutVAT = cipWithoutVAT;
            tannarx.SalesPriceCIPWithVAT = CalculateCIPWithVAT(cipWithoutVAT);

            // Update SetPriceCIPWithVAT if not manually set
            if (!tannarx.SetPriceCIPWithVAT.HasValue)
            {
                tannarx.SetPriceCIPWithVAT = tannarx.SalesPriceCIPWithVAT;
            }
        }

        /// <summary>
        /// Recalculate CIP prices when FinishedProductTransportCost is changed
        /// </summary>
        public void RecalculateCIPPrices(TANNARX tannarx)
        {
            var cipWithoutVAT = CalculateCIPWithoutVAT(tannarx.SalesPriceEXWWithoutVAT, tannarx.FinishedProductTransportCost);
            tannarx.SalesPriceCIPWithoutVAT = cipWithoutVAT;
            tannarx.SalesPriceCIPWithVAT = CalculateCIPWithVAT(cipWithoutVAT);

            // Update SetPriceCIPWithVAT if not manually set
            if (!tannarx.SetPriceCIPWithVAT.HasValue)
            {
                tannarx.SetPriceCIPWithVAT = tannarx.SalesPriceCIPWithVAT;
            }
        }

        /// <summary>
        /// Get month name in Uzbek
        /// </summary>
        public string GetMonthName(int month)
        {
            var monthNames = new Dictionary<int, string>
            {
                { 1, "Yanvar" }, { 2, "Fevral" }, { 3, "Mart" },
                { 4, "Aprel" }, { 5, "May" }, { 6, "Iyun" },
                { 7, "Iyul" }, { 8, "Avgust" }, { 9, "Sentabr" },
                { 10, "Oktabr" }, { 11, "Noyabr" }, { 12, "Dekabr" }
            };

            return monthNames.ContainsKey(month) ? monthNames[month] : month.ToString();
        }
    }
}
