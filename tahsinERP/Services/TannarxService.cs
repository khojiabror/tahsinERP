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
        /// Calculate material price from NORM_INPUT_LINES for a product and month.
        /// If no lines for the exact month, finds NORMVERSION where the requested month
        /// falls within EffectiveFrom–EffectiveTo and uses that version's period (EffectiveFrom yyyy-MM).
        /// </summary>
        public async Task<decimal> CalculateMaterialPriceAsync(int productID, string month)
        {
            // Tannarxdagi A (material narxi) Norm/ViewDetails "Umumiy summa" mantig'idan olinadi.
            // Avvalo requested oyga aktiv NORMVERSION topamiz va uning periodidagi input lines ni olamiz.
            List<NORM_INPUT_LINES> normLines = null;
            DateTime requestedDate;
            if (TryParseMonthString(month, out requestedDate))
            {
                var version = await _db.NORMVERSIONS
                    .Where(nv => nv.ProductID == productID
                        && nv.IsDeleted != true
                        && nv.EffectiveFrom.HasValue
                        && nv.EffectiveFrom.Value <= requestedDate
                        && (nv.EffectiveTo == null || nv.EffectiveTo.Value >= requestedDate))
                    .OrderByDescending(nv => nv.EffectiveFrom)
                    .ThenByDescending(nv => nv.ID)
                    .FirstOrDefaultAsync();

                if (version != null && version.EffectiveFrom.HasValue)
                {
                    var period = version.EffectiveFrom.Value.ToString("yyyy-MM");
                    normLines = await _db.NORM_INPUT_LINES
                        .Where(n => n.ProductID == productID && n.Month == period)
                        .OrderByDescending(n => n.CreatedDT)
                        .ToListAsync();
                }
            }

            // Fallback: to'g'ridan-to'g'ri selected month
            if (normLines == null || !normLines.Any())
            {
                normLines = await _db.NORM_INPUT_LINES
                    .Where(n => n.ProductID == productID && n.Month == month)
                    .OrderByDescending(n => n.CreatedDT)
                    .ToListAsync();
            }

            if (!normLines.Any())
                return 0;

            // Bir oyda qayta saqlangan eski satrlar qolib ketgan bo'lsa, narx oshib ketmasin:
            // bir xil variant (material + uzunlik + son + birlik) bo'yicha oxirgi satrni olamiz.
            normLines = normLines
                .GroupBy(n => new
                {
                    n.MaterialID,
                    n.CutLengthMm,
                    n.PiecesPerProduct,
                    UnitID = n.UnitID ?? 0
                })
                .Select(g => g.OrderByDescending(x => x.CreatedDT).ThenByDescending(x => x.ID).First())
                .ToList();

            var materialIds = normLines
                .Select(n => n.MaterialID)
                .Distinct()
                .ToList();

            var requiresCuttingByMaterial = await _db.PRODUCTS
                .Include(p => p.PRODUCT_CATEGORIES)
                .Where(p => materialIds.Contains(p.ID))
                .ToDictionaryAsync(
                    p => p.ID,
                    p => p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.RequiresCutting);

            return SumMaterialPriceFromNormLines(normLines, requiresCuttingByMaterial);
        }

        /// <summary>
        /// Parse month string (e.g. "2025-10" or "2025-09") to first day of month.
        /// </summary>
        private bool TryParseMonthString(string month, out DateTime date)
        {
            date = default(DateTime);
            if (string.IsNullOrWhiteSpace(month))
                return false;
            var parts = month.Trim().Split('-');
            if (parts.Length < 2)
                return false;
            int y, m;
            if (!int.TryParse(parts[0], out y) || !int.TryParse(parts[1], out m) || m < 1 || m > 12)
                return false;
            try
            {
                date = new DateTime(y, m, 1);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sum material price from a list of NORM_INPUT_LINES.
        /// </summary>
        private decimal SumMaterialPriceFromNormLines(
            List<NORM_INPUT_LINES> normLines,
            IDictionary<int, bool> requiresCuttingByMaterial)
        {
            decimal total = 0;
            foreach (var line in normLines)
            {
                if (line.Price <= 0)
                    continue;

                var requiresCutting = requiresCuttingByMaterial != null
                    && requiresCuttingByMaterial.ContainsKey(line.MaterialID)
                    && requiresCuttingByMaterial[line.MaterialID];

                decimal consumptionMeters = 0;
                if (line.Consumption.HasValue && line.Consumption.Value > 0)
                {
                    consumptionMeters = NormInputLineConsumptionToDisplay(
                        requiresCutting,
                        line.CutLengthMm,
                        line.Consumption);
                }
                else if (requiresCutting && line.CutLengthMm > 0)
                {
                    consumptionMeters = (decimal)(line.CutLengthMm / 1000.0) * line.PiecesPerProduct;
                }
                else
                {
                    consumptionMeters = line.PiecesPerProduct;
                }

                decimal wasteMeters = NormInputLineWasteToDisplay(line.WasteAmount);

                total += (consumptionMeters + wasteMeters) * line.Price;
            }
            return total;
        }

        /// <summary>
        /// NORM_INPUT_LINES.Consumption: kesish qatori (CutLengthMm > 0) bo'lsa jami mm saqlanadi -> m ga /1000.
        /// Qolganlarda Consumption qiymati as-is olinadi.
        /// </summary>
        private static decimal NormInputLineConsumptionToDisplay(bool requiresCutting, int cutLengthMm, double? consumptionDb)
        {
            if (!consumptionDb.HasValue || consumptionDb.Value <= 0)
                return 0;
            if (requiresCutting && cutLengthMm > 0)
                return (decimal)(consumptionDb.Value / 1000.0);
            return (decimal)consumptionDb.Value;
        }

        /// <summary>
        /// Norm/ViewDetails "Summa hisoblar" bilan bir xil bo'lishi uchun WasteAmount har doim mm -> m.
        /// </summary>
        private static decimal NormInputLineWasteToDisplay(double? wasteAmountDb)
        {
            if (!wasteAmountDb.HasValue || wasteAmountDb.Value <= 0)
                return 0;
            return (decimal)(wasteAmountDb.Value / 1000.0);
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
        /// Check if product has norms (NORM_INPUT_LINES or NORMLINES) for the given month.
        /// Rule: norm is valid only when selected month is within EffectiveFrom-EffectiveTo.
        /// </summary>
        public async Task<bool> HasNormsForMonthAsync(int productID, int year, int month)
        {
            var requestedDate = new DateTime(year, month, 1);
            var activeVersions = await _db.NORMVERSIONS
                .Include(nv => nv.NORMLINES)
                .Where(nv => nv.ProductID.HasValue
                    && nv.ProductID.Value == productID
                    && nv.IsDeleted != true
                    && nv.EffectiveFrom.HasValue
                    && nv.EffectiveFrom.Value <= requestedDate
                    && (!nv.EffectiveTo.HasValue || nv.EffectiveTo.Value >= requestedDate))
                .OrderByDescending(nv => nv.EffectiveFrom)
                .ToListAsync();

            if (!activeVersions.Any())
                return false;

            // If any active version has NORMLINES - norm exists.
            if (activeVersions.Any(nv => nv.NORMLINES != null && nv.NORMLINES.Any()))
                return true;

            // Norm module stores NORM_INPUT_LINES by EffectiveFrom month.
            // For active version(s), check NORM_INPUT_LINES on their EffectiveFrom period.
            var periods = activeVersions
                .Where(v => v.EffectiveFrom.HasValue)
                .Select(v => v.EffectiveFrom.Value.ToString("yyyy-MM"))
                .Distinct()
                .ToList();

            if (!periods.Any())
                return false;

            return await _db.NORM_INPUT_LINES
                .AnyAsync(n => n.ProductID == productID
                    && n.Month != null
                    && periods.Contains(n.Month));
        }

        /// <summary>
        /// Get finished goods product IDs that have norms for a specific month.
        /// </summary>
        public async Task<List<int>> GetFinishedGoodsWithNormsProductIdsAsync(int year, int month)
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

            var productIdsWithNorms = new List<int>();
            
            // Check each product for norms
            foreach (var productId in finishedGoods)
            {
                if (await HasNormsForMonthAsync(productId, year, month))
                {
                    productIdsWithNorms.Add(productId);
                }
            }

            return productIdsWithNorms;
        }

        /// <summary>
        /// Get count of finished goods products that have norms for a specific month
        /// </summary>
        public async Task<int> GetFinishedGoodsWithNormsCountAsync(int year, int month)
        {
            var ids = await GetFinishedGoodsWithNormsProductIdsAsync(year, month);
            return ids.Count;
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

            // Belgilangan EXW narxi UI da QQS bilan saqlanadi.
            // Foizli xarajatlar (A/B/C/D/E) QQS siz EXW bazasidan olinishi kerak.
            var setPriceEXWWithVat = tannarx.SetPriceEXWWithVAT.Value;
            var setPriceEXWWithoutVat = setPriceEXWWithVat / 1.12m;

            // Recalculate B, C, D, E based on belgilangan EXW (QQS siz) foizlari
            tannarx.TaxAndBank = CalculateExpenseByPercent(setPriceEXWWithoutVat, tannarx.TaxAndBankPercent ?? 5m);
            tannarx.AdministrativeExpenses = CalculateExpenseByPercent(setPriceEXWWithoutVat, tannarx.AdministrativeExpensesPercent ?? 5m);
            tannarx.OtherExpenses = CalculateExpenseByPercent(setPriceEXWWithoutVat, tannarx.OtherExpensesPercent ?? 5m);
            tannarx.Profit = CalculateExpenseByPercent(setPriceEXWWithoutVat, tannarx.ProfitPercent ?? 10m);

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
            
            // Recalculate EXW with VAT (12%)
            tannarx.SalesPriceEXWWithVAT = CalculateEXWWithVAT(exwWithoutVAT);

            // Recalculate CIP prices: CIP(qqssiz)=EXW(qqssiz)+tashuv, CIP(qqsli)=*1.12
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
