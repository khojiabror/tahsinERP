using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;
using tahsinERP.Services.Norm;
using tahsinERP.Helpers;

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
            var prices = await CalculateMaterialPricesAsync(new List<int> { productID }, month);
            decimal price;
            return prices.TryGetValue(productID, out price) ? price : 0m;
        }

        /// <summary>
        /// <see cref="CalculateMaterialPriceAsync"/> ning ko'p mahsulotli varianti: aktiv versiyalar, norma qatorlari
        /// va material kategoriyalari bir martada yuklanadi (avval har mahsulotga 3–4 so'rov, MES-P06).
        /// </summary>
        private async Task<Dictionary<int, decimal>> CalculateMaterialPricesAsync(List<int> productIds, string month)
        {
            var result = new Dictionary<int, decimal>();
            if (productIds == null || productIds.Count == 0)
                return result;

            // Tannarxdagi A (material narxi) Norm/ViewDetails "Umumiy summa" mantig'idan olinadi.
            // Avvalo requested oyga aktiv NORMVERSION topamiz va uning periodidagi input lines ni olamiz.
            var periodByProduct = new Dictionary<int, string>();
            DateTime requestedDate;
            if (TryParseMonthString(month, out requestedDate))
            {
                var versions = await _db.NORMVERSIONS.AsNoTracking()
                    .Where(nv => nv.ProductID.HasValue
                        && productIds.Contains(nv.ProductID.Value)
                        && nv.IsDeleted != true
                        && nv.EffectiveFrom.HasValue
                        && nv.EffectiveFrom.Value <= requestedDate
                        && (nv.EffectiveTo == null || nv.EffectiveTo.Value >= requestedDate))
                    .Select(nv => new { ProductID = nv.ProductID.Value, nv.ID, EffectiveFrom = nv.EffectiveFrom.Value })
                    .ToListAsync();

                foreach (var g in versions.GroupBy(v => v.ProductID))
                {
                    var latest = g.OrderByDescending(v => v.EffectiveFrom).ThenByDescending(v => v.ID).First();
                    periodByProduct[g.Key] = latest.EffectiveFrom.ToString("yyyy-MM");
                }
            }

            var months = periodByProduct.Values.Concat(new[] { month }).Where(m => m != null).Distinct().ToList();
            var allLines = await _db.NORM_INPUT_LINES.AsNoTracking()
                .Where(n => productIds.Contains(n.ProductID) && months.Contains(n.Month))
                .ToListAsync();
            if (!allLines.Any())
                return result;

            var materialIds = allLines.Select(n => n.MaterialID).Distinct().ToList();
            var requiresCuttingByMaterial = await _db.PRODUCTS.AsNoTracking()
                .Where(p => materialIds.Contains(p.ID))
                .Select(p => new { p.ID, RequiresCutting = p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.RequiresCutting })
                .ToDictionaryAsync(p => p.ID, p => p.RequiresCutting);

            var linesByProduct = allLines.GroupBy(n => n.ProductID).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var productID in productIds.Distinct())
            {
                List<NORM_INPUT_LINES> productLines;
                if (!linesByProduct.TryGetValue(productID, out productLines))
                    continue;

                List<NORM_INPUT_LINES> normLines = null;
                string period;
                if (periodByProduct.TryGetValue(productID, out period))
                {
                    normLines = productLines.Where(n => n.Month == period).ToList();
                }

                // Fallback: to'g'ridan-to'g'ri selected month
                if (normLines == null || !normLines.Any())
                {
                    normLines = productLines.Where(n => n.Month == month).ToList();
                }

                if (!normLines.Any())
                    continue;

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

                result[productID] = SumMaterialPriceFromNormLines(normLines, requiresCuttingByMaterial);
            }

            return result;
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
        /// Sum material price from a list of NORM_INPUT_LINES (sarf + chiqindi, metrda — NormLineMath).
        /// </summary>
        private static decimal SumMaterialPriceFromNormLines(
            List<NORM_INPUT_LINES> normLines,
            IDictionary<int, bool> requiresCuttingByMaterial)
        {
            decimal total = 0;
            foreach (var line in normLines)
            {
                if (line.Price <= 0)
                    continue;

                bool requiresCutting;
                requiresCuttingByMaterial.TryGetValue(line.MaterialID, out requiresCutting);

                var consumptionMeters = NormLineMath.LineConsumption(requiresCutting, line.CutLengthMm, line.PiecesPerProduct, line.Consumption);
                var wasteMeters = NormLineMath.WasteToDisplay(line.WasteAmount);

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
            var costs = await CalculateLaborCostsAsync(new List<int> { productID });
            decimal cost;
            return costs.TryGetValue(productID, out cost) ? cost : 0m;
        }

        /// <summary>Ko'p mahsulot uchun ish haqi (bitta GROUP BY so'rov).</summary>
        private async Task<Dictionary<int, decimal>> CalculateLaborCostsAsync(List<int> productIds)
        {
            var now = DateTime.Now;
            return await _db.HR_PIECE_RATE_TARIFFS.AsNoTracking()
                .Where(t => productIds.Contains(t.ProductID)
                    && t.IsActive == true
                    && t.EffectiveFrom <= now
                    && (t.EffectiveTo == null || t.EffectiveTo >= now))
                .GroupBy(t => t.ProductID)
                .Select(g => new { ProductID = g.Key, Total = g.Sum(t => t.RatePerUnit) })
                .ToDictionaryAsync(x => x.ProductID, x => x.Total);
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
            var ids = await FilterProductsWithNormsAsync(new List<int> { productID }, year, month);
            return ids.Contains(productID);
        }

        /// <summary>
        /// Get finished goods product IDs that have norms for a specific month.
        /// Avval har tayyor mahsulot uchun HasNormsForMonthAsync (1–2 so'rov) chaqirilardi — 500 FG ≈ 1000 so'rov (MES-P05).
        /// </summary>
        public async Task<List<int>> GetFinishedGoodsWithNormsProductIdsAsync(int year, int month)
        {
            var finishedGoods = await FinishedGoodsQuery().Select(p => p.ID).ToListAsync();
            var withNorms = await FilterProductsWithNormsAsync(finishedGoods, year, month);
            return finishedGoods.Where(withNorms.Contains).ToList();
        }

        /// <summary>
        /// Tayyor mahsulot: PRODUCT.IsFinishedGood yoki PRODUCT_CATEGORIES.IsFinishedGood.
        /// </summary>
        private IQueryable<PRODUCT> FinishedGoodsQuery()
        {
            return MesLookups.FinishedGoods(_db);
        }

        /// <summary>
        /// Berilgan mahsulotlardan tanlangan oyda aktiv normasi borlarini qaytaradi (set-based, 2 ta so'rov):
        /// aktiv versiyada NORMLINES bo'lsa — bor; aks holda versiya EffectiveFrom oyi bo'yicha NORM_INPUT_LINES tekshiriladi.
        /// </summary>
        private async Task<HashSet<int>> FilterProductsWithNormsAsync(List<int> productIds, int year, int month)
        {
            var result = new HashSet<int>();
            if (productIds == null || productIds.Count == 0)
                return result;

            var requestedDate = new DateTime(year, month, 1);
            var activeVersions = await _db.NORMVERSIONS.AsNoTracking()
                .Where(nv => nv.ProductID.HasValue
                    && productIds.Contains(nv.ProductID.Value)
                    && nv.IsDeleted != true
                    && nv.EffectiveFrom.HasValue
                    && nv.EffectiveFrom.Value <= requestedDate
                    && (!nv.EffectiveTo.HasValue || nv.EffectiveTo.Value >= requestedDate))
                .Select(nv => new
                {
                    ProductID = nv.ProductID.Value,
                    EffectiveFrom = nv.EffectiveFrom.Value,
                    HasLines = nv.NORMLINES.Any()
                })
                .ToListAsync();

            foreach (var v in activeVersions.Where(v => v.HasLines))
                result.Add(v.ProductID);

            // Norm module stores NORM_INPUT_LINES by EffectiveFrom month.
            var pending = activeVersions
                .Where(v => !result.Contains(v.ProductID))
                .Select(v => new { v.ProductID, Period = v.EffectiveFrom.ToString("yyyy-MM") })
                .Distinct()
                .ToList();
            if (!pending.Any())
                return result;

            var pendingProductIds = pending.Select(p => p.ProductID).Distinct().ToList();
            var periods = pending.Select(p => p.Period).Distinct().ToList();
            var existingPairs = await _db.NORM_INPUT_LINES.AsNoTracking()
                .Where(n => pendingProductIds.Contains(n.ProductID) && n.Month != null && periods.Contains(n.Month))
                .Select(n => new { n.ProductID, n.Month })
                .Distinct()
                .ToListAsync();
            var pairSet = new HashSet<string>(existingPairs.Select(p => p.ProductID + "|" + p.Month));

            foreach (var p in pending)
            {
                if (pairSet.Contains(p.ProductID + "|" + p.Period))
                    result.Add(p.ProductID);
            }
            return result;
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
            var finishedGoodIds = await FinishedGoodsQuery().Select(p => p.ID).ToListAsync();

            // Hammasi oldindan: mavjud tannarxlar, normasi borlar, material narxlari va ish haqi
            // (avval har mahsulotga ~8 so'rov ketardi, MES-P06).
            var existingProductIds = new HashSet<int>(await _db.TANNARXes.AsNoTracking()
                .Where(t => t.Year == year && t.Month == month && !t.IsDeleted)
                .Select(t => t.ProductID)
                .ToListAsync());
            var candidateIds = finishedGoodIds.Where(id => !existingProductIds.Contains(id)).ToList();
            var withNorms = await FilterProductsWithNormsAsync(candidateIds, year, month);
            var targetIds = candidateIds.Where(withNorms.Contains).ToList();

            var materialPrices = await CalculateMaterialPricesAsync(targetIds, monthString);
            var laborCosts = await CalculateLaborCostsAsync(targetIds);

            int generatedCount = 0;

            foreach (var productID in targetIds)
            {
                // Calculate material price from NORM tables
                decimal materialPrice;
                materialPrices.TryGetValue(productID, out materialPrice);

                // Calculate material transport cost (2% of material price)
                var materialTransportCost = CalculateMaterialTransportCost(materialPrice);

                // Calculate labor cost from HR_PIECE_RATE_TARIFFS
                decimal laborCost;
                laborCosts.TryGetValue(productID, out laborCost);

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
                    ProductID = productID,
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
            return UzMonthNames.Get(month);
        }
    }
}
