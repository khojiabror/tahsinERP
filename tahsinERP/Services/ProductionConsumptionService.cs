using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Controllers;
using tahsinERP.Models;
using tahsinERP.Services.DlocFifo;
using tahsinERP.Services.Stock;

namespace tahsinERP.Services
{
    /// <summary>
    /// Ishlab chiqarish vaqtida xomashyo sarfini hisoblash va EXPENSE ga yozish.
    /// Miqdor manbai: BOM Details jadvalidagi 1 FG sarf (BomMaterialSummaryService) × ishlab chiqarilgan soni.
    /// </summary>
    public class ProductionConsumptionService
    {
        private readonly DBTHSNEntities _db;
        private readonly UnitConversionService _conversionService;
        // MaterialID → birinchi kirim birligi (bitta servis nusxasi doirasida keshlanadi; avval har chaqiruvda so'rov, MES-P10)
        private readonly Dictionary<int, int?> _displayUnitCache = new Dictionary<int, int?>();

        public ProductionConsumptionService(DBTHSNEntities db)
        {
            _db = db;
            _conversionService = new UnitConversionService(db);
        }

        /// <summary>
        /// BOM Details jadvalidagi 1 FG sarfini BomMaterialSummaryService orqali oladi va producedQty ga ko'paytiradi.
        /// </summary>
        public async Task<List<ProductionRawMaterialRequirement>> CalculateRequiredRawMaterialsAsync(int productId, double producedQty, DateTime? asOfDate = null)
        {
            var requirements = new List<ProductionRawMaterialRequirement>();
            if (producedQty <= 0)
            {
                return requirements;
            }

            var summaryService = new BomMaterialSummaryService(_db);
            var summaries = await summaryService.GetMaterialSummariesPerUnitAsync(productId, null, asOfDate);
            if (!summaries.Any())
            {
                return requirements;
            }

            var materialIds = summaries.Select(s => s.MaterialId).Distinct().ToList();
            var materials = await _db.PRODUCTS
                .Include(p => p.PRODUCT_CATEGORIES)
                .Where(p => materialIds.Contains(p.ID))
                .ToDictionaryAsync(p => p.ID);

            foreach (var summary in summaries)
            {
                if (summary.TotalQuantity <= 0)
                {
                    continue;
                }

                if (!materials.TryGetValue(summary.MaterialId, out var material))
                {
                    continue;
                }

                var totalConsumption = (double)(summary.TotalQuantity * (decimal)producedQty);
                var unitId = summary.UnitId > 0
                    ? summary.UnitId
                    : (material.UnitID > 0 ? material.UnitID : 1);

                requirements.Add(new ProductionRawMaterialRequirement
                {
                    MaterialID = summary.MaterialId,
                    MaterialPNo = summary.MaterialPno ?? material.PNo,
                    MaterialName = summary.MaterialName ?? material.Name,
                    RequiredQtyPieces = totalConsumption,
                    TotalConsumption = totalConsumption,
                    UnitID = unitId,
                    RequiresCutting = material.PRODUCT_CATEGORIES?.RequiresCutting ?? false,
                    TotalConsumptionIsInKg = false
                });
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
            int? cached;
            if (_displayUnitCache.TryGetValue(materialId, out cached))
                return cached;

            var unitId = _db.INCOME_PRODUCTS.AsNoTracking()
                .Where(ip => ip.ProductID == materialId
                    && (ip.INCOME.IsDeleted == null || ip.INCOME.IsDeleted == false))
                .OrderBy(ip => ip.INCOME.IssueDateTime)
                .Select(ip => (int?)ip.UnitID)
                .FirstOrDefault();

            _displayUnitCache[materialId] = unitId;
            return unitId;
        }

        /// <summary>
        /// Xomashyo yetarliligini tekshiradi (ishlab chiqarish yaratish/tahrirlash va chiqimdan oldin - yagona joy, MES-D23).
        /// <paramref name="credit"/>: tahrirlashda eski yozuv sarfi omborga qaytgandek qo'shiladigan miqdor (MaterialID -> qty).
        /// Yetmasa foydalanuvchiga ko'rsatiladigan xabar, yetsa null qaytaradi.
        /// </summary>
        public async Task<string> FindStockShortageAsync(IList<ProductionRawMaterialRequirement> requirements, int warehouseId,
            IDictionary<int, double> credit = null)
        {
            if (requirements == null || requirements.Count == 0)
                return null;

            foreach (var req in requirements)
            {
                var availableQty = InventoryValuationService.GetQty(_db, warehouseId, req.MaterialID);
                double extra;
                if (credit != null && credit.TryGetValue(req.MaterialID, out extra))
                    availableQty += extra;

                if (availableQty + 1e-9 >= req.TotalConsumption)
                    continue;

                var unitName = await _db.PRODUCT_STOCKS.AsNoTracking()
                    .Where(s => s.WHID == warehouseId && s.ProductID == req.MaterialID && s.UNIT != null)
                    .Select(s => s.UNIT.ShortName ?? s.UNIT.UnitName)
                    .FirstOrDefaultAsync();
                unitName = (unitName ?? "").Trim();
                var suffix = unitName.Length > 0 ? " " + unitName : "";

                return $"Ishlab chiqarish natijasini saqlab bo'lmadi: xomashyo yetarli emas. Mahsulot: {req.MaterialPNo ?? req.MaterialID.ToString()}, " +
                       $"omborda {availableQty:N2}{suffix}, kerak {req.TotalConsumption:N2}{suffix}.";
            }

            return null;
        }

        /// <summary>
        /// Ishlab chiqarish uchun xomashyo yetarliligi. oldProductId/oldProducedQty berilsa (tahrirlash),
        /// eski yozuv sarfi omborga qaytgandek hisoblanadi.
        /// </summary>
        public async Task<string> ValidateStockForProductionAsync(int productId, double producedQty, int warehouseId,
            int? oldProductId = null, double? oldProducedQty = null)
        {
            var requirements = await CalculateRequiredRawMaterialsAsync(productId, producedQty);
            if (requirements.Count == 0)
                return null;

            var credit = new Dictionary<int, double>();
            if (oldProductId.HasValue && oldProducedQty.HasValue && oldProducedQty.Value > 0)
            {
                foreach (var req in await CalculateRequiredRawMaterialsAsync(oldProductId.Value, oldProducedQty.Value))
                {
                    double q;
                    credit.TryGetValue(req.MaterialID, out q);
                    credit[req.MaterialID] = q + req.TotalConsumption;
                }
            }

            return await FindStockShortageAsync(requirements, warehouseId, credit);
        }

        private class IncomeLayer
        {
            public int ProductID { get; set; }
            public double Amount { get; set; }
            public double? PiecePrice { get; set; }
            public double? TotalPrice { get; set; }
        }

        /// <summary>
        /// FIFO narx (MES-B11): ombordagi joriy qoldiq eng so'nggi kirimlardan iborat deb olinadi, shuning uchun
        /// (jami kirim - qoldiq) miqdordagi eng eski qatlamlar allaqachon sarflangan hisoblanib o'tkazib yuboriladi va
        /// talab qilingan miqdor keyingi qatlamlar narxida baholanadi. Avval doim eng eski kirimlar narxi olinardi
        /// (sarflangan miqdor hisobga olinmasdan). Barcha materiallar bitta so'rovda (avval material boshiga, MES-P11).
        /// Qoldiq kamayishidan (chiqimdan) OLDIN chaqirilishi kerak.
        /// </summary>
        private async Task<Dictionary<int, double>> GetFifoPiecePricesAsync(
            int warehouseId, IList<ProductionRawMaterialRequirement> requirements)
        {
            var result = new Dictionary<int, double>();
            var materialIds = requirements.Select(r => r.MaterialID).Distinct().ToList();
            if (materialIds.Count == 0)
                return result;

            var layers = (await _db.INCOME_PRODUCTS.AsNoTracking()
                    .Where(ip => materialIds.Contains(ip.ProductID)
                        && ip.INCOME.ReceivingWHID == warehouseId
                        && (ip.INCOME.IsDeleted == null || ip.INCOME.IsDeleted == false)
                        && ip.Amount > 0)
                    .OrderBy(ip => ip.INCOME.IssueDateTime).ThenBy(ip => ip.ID)
                    .Select(ip => new IncomeLayer { ProductID = ip.ProductID, Amount = ip.Amount, PiecePrice = ip.PiecePrice, TotalPrice = ip.TotalPrice })
                    .ToListAsync())
                .ToLookup(l => l.ProductID);
            var onHand = StockLedger.GetOnHand(_db, warehouseId, materialIds);

            foreach (var group in requirements.GroupBy(r => r.MaterialID))
            {
                var requiredQty = group.Sum(r => r.TotalConsumption);
                var materialLayers = layers[group.Key].ToList();
                if (materialLayers.Count == 0 || requiredQty <= 0)
                {
                    result[group.Key] = 0;
                    continue;
                }

                double stock;
                onHand.TryGetValue(group.Key, out stock);
                var alreadyConsumed = Math.Max(0, materialLayers.Sum(l => l.Amount) - Math.Max(0, stock));

                double remaining = requiredQty, priceSum = 0, qtyTaken = 0, lastPrice = 0;
                foreach (var layer in materialLayers)
                {
                    var price = layer.PiecePrice ?? 0;
                    if (price <= 0 && layer.TotalPrice.HasValue)
                        price = layer.TotalPrice.Value / layer.Amount;
                    lastPrice = price;

                    var available = layer.Amount;
                    if (alreadyConsumed > 0)
                    {
                        var skip = Math.Min(available, alreadyConsumed);
                        alreadyConsumed -= skip;
                        available -= skip;
                    }
                    if (available <= 0 || remaining <= 0)
                        continue;

                    var take = Math.Min(available, remaining);
                    priceSum += take * price;
                    qtyTaken += take;
                    remaining -= take;
                }

                // Qatlamlar yetmasa (kirimsiz qoldiq) - qolgan miqdor eng so'nggi kirim narxida
                if (remaining > 0)
                {
                    priceSum += remaining * lastPrice;
                    qtyTaken += remaining;
                }

                result[group.Key] = qtyTaken > 0 ? priceSum / qtyTaken : 0;
            }

            return result;
        }

        /// <summary>
        /// Consumes materials from the given warehouse when production is confirmed.
        /// Og'irlik bo'yicha (kg) va kesish (m → dona) uchun UnitConversionService ishlatiladi.
        /// Creates Expense document without linked Income.
        /// </summary>
        public async Task ConsumeMaterialsForProductionAsync(int productId, double producedQty, int warehouseId, int? productionProcessId = null, DateTime? productionDate = null)
        {
            var requirements = await CalculateRequiredRawMaterialsAsync(productId, producedQty);
            if (requirements.Count == 0)
            {
                return;
            }

            // Chiqimdan oldin qoldiq tekshiruvi (DLOC + umumiy qoldiq)
            var shortage = await FindStockShortageAsync(requirements, warehouseId);
            if (shortage != null)
            {
                throw new InvalidOperationException(shortage);
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
                IsDeleted = false,
                // Haqiqiy bog'lanish ustuni (Description'dagi marker eski o'quvchilar uchun qoladi, MES-P12)
                ProductionID = productionProcessId.HasValue && productionProcessId.Value > 0 ? productionProcessId : null
            };

            // Narxlar qoldiq kamayishidan oldin, bitta so'rovda (MES-B11/P11)
            var piecePrices = await GetFifoPiecePricesAsync(warehouseId, requirements);
            var lines = new List<EXPENSE_PRODUCTS>();
            double totalAmount = 0;
            double totalExpensePrice = 0;

            foreach (var req in requirements)
            {
                var piecePrice = piecePrices[req.MaterialID];
                var totalPrice = piecePrice * req.TotalConsumption;

                var displayUnitId = GetDisplayUnitFromIncome(req.MaterialID);
                if (!displayUnitId.HasValue || displayUnitId.Value <= 0)
                {
                    var materialUnitId = await _db.PRODUCTS.Where(p => p.ID == req.MaterialID).Select(p => (int?)p.UnitID).FirstOrDefaultAsync();
                    displayUnitId = materialUnitId > 0 ? materialUnitId : (req.UnitID > 0 ? req.UnitID : 1);
                }

                var line = new EXPENSE_PRODUCTS
                {
                    ProductID = req.MaterialID,
                    Amount = req.TotalConsumption,
                    PiecePrice = piecePrice,
                    TotalPrice = totalPrice,
                    UnitID = displayUnitId.Value,
                    Comment = "BOM/NORM sarfi"
                };
                expense.EXPENSE_PRODUCTS.Add(line);
                lines.Add(line);

                totalAmount += req.TotalConsumption;
                totalExpensePrice += totalPrice;
            }

            expense.Amount = totalAmount;
            expense.TotalPrice = totalExpensePrice;

            // Hujjat + barcha qatorlar bitta SaveChanges'da (avval har qatorga alohida, MES-P11);
            // chiqim (lot ajratmasi) qator ID'sini talab qiladi - shuning uchun undan keyin
            _db.EXPENSES.Add(expense);
            await _db.SaveChangesAsync();

            foreach (var line in lines)
            {
                await ExpenseStockIssueHelper.ApplyAutoIssueForExpenseProductAsync(
                    _db, warehouseId, line.ProductID, line.Amount, line.UnitID, line.ID);
            }

            LogHelper.LogToDatabase("system", "ProductionConsumptionService", "ConsumeMaterialsForProductionAsync",
                $"Xomashyo chiqimi yaratildi. Expense ID: {expense.ID}, Mahsulot ID: {productId}, Miqdor: {producedQty}");
        }

        /// <summary>
        /// Ushbu ishlab chiqarish uchun avtomatik yaratilgan xomashyo chiqimini to'liq bekor qiladi:
        /// har bir chiqim qatori bo'yicha DLOC lot + umumiy qoldiqni qaytaradi (ExpenseStockIssueHelper)
        /// va EXPENSE/EXPENSE_PRODUCTS/WRHS_EXPENSE_DOCS yozuvlarini fizik o'chiradi.
        /// Faqat ichki sarf chiqimlari (ReceiverWhID == null) tegiladi; ishlab chiqarish→sotuv
        /// o'tkazmalari (ReceiverWhID mavjud) tegilmaydi.
        /// </summary>
        public async Task ReverseProductionConsumptionAsync(int productionId)
        {
            if (productionId <= 0)
                return;

            // EXPENSES.ProductionID ustuni bo'yicha (avval Description LIKE '%ProductionID:x%' — to'liq skan, MES-P12).
            // Eski markerli yozuvlar migratsiyada (MES_2026_10_PlanDate_ProductionID.sql) ustunga ko'chiriladi.
            var expenses = await _db.EXPENSES
                .Include(e => e.EXPENSE_PRODUCTS)
                .Where(e => e.IsDeleted != true
                    && e.ReceiverWhID == null
                    && e.ProductionID == productionId)
                .ToListAsync();

            if (!expenses.Any())
                return;

            foreach (var expense in expenses)
            {
                var lines = expense.EXPENSE_PRODUCTS != null
                    ? expense.EXPENSE_PRODUCTS.ToList()
                    : new List<EXPENSE_PRODUCTS>();

                foreach (var line in lines)
                {
                    var unitId = line.UnitID > 0 ? line.UnitID : 1;
                    ExpenseStockIssueHelper.ReverseExpenseProductIssue(
                        _db, expense.SenderWHID, line.ProductID, line.Amount, unitId, line.ID);
                }

                if (lines.Any())
                {
                    _db.EXPENSE_PRODUCTS.RemoveRange(lines);
                }

                var docs = await _db.WRHS_EXPENSE_DOCS
                    .Where(d => d.ExpenseID == expense.ID)
                    .ToListAsync();
                if (docs.Any())
                {
                    _db.WRHS_EXPENSE_DOCS.RemoveRange(docs);
                }

                _db.EXPENSES.Remove(expense);
            }

            await _db.SaveChangesAsync();

            LogHelper.LogToDatabase("system", "ProductionConsumptionService", "ReverseProductionConsumptionAsync",
                $"ProductionID {productionId} uchun {expenses.Count} ta xomashyo chiqimi bekor qilindi.");
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
        public double TotalConsumption { get; set; }
        public int UnitID { get; set; }
        public bool RequiresCutting { get; set; }
        /// <summary>True bo'lsa, TotalConsumption kg (piece→kg konversiyasidan keyin).</summary>
        public bool TotalConsumptionIsInKg { get; set; }
    }
}
