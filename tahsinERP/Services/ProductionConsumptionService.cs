using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Controllers;
using tahsinERP.Models;
using tahsinERP.Services.DlocFifo;

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

        public ProductionConsumptionService(DBTHSNEntities db)
        {
            _db = db;
            _conversionService = new UnitConversionService(db);
        }

        /// <summary>
        /// BOM Details jadvalidagi 1 FG sarfini BomMaterialSummaryService orqali oladi va producedQty ga ko'paytiradi.
        /// </summary>
        public async Task<List<ProductionRawMaterialRequirement>> CalculateRequiredRawMaterialsAsync(int productId, double producedQty)
        {
            var requirements = new List<ProductionRawMaterialRequirement>();
            if (producedQty <= 0)
            {
                return requirements;
            }

            var summaryService = new BomMaterialSummaryService(_db);
            var summaries = await summaryService.GetMaterialSummariesPerUnitAsync(productId);
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
                    RequiredQtyMeters = 0,
                    WasteQty = 0,
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

            // Validate stock before creating expense (DLOC + umumiy qoldiq)
            foreach (var req in requirements)
            {
                var availableQty = InventoryValuationService.GetQty(_db, warehouseId, req.MaterialID);
                if (availableQty + 1e-9 < req.TotalConsumption)
                {
                    throw new InvalidOperationException(
                        $"Omborda yetarli qoldiq yo'q. Mahsulot: {req.MaterialPNo}, Talab: {req.TotalConsumption}, Mavjud: {availableQty}");
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
                if (!displayUnitId.HasValue || displayUnitId.Value <= 0)
                {
                    var material = await _db.PRODUCTS.FindAsync(req.MaterialID);
                    displayUnitId = material?.UnitID > 0 ? material.UnitID : (req.UnitID > 0 ? req.UnitID : 1);
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
                await _db.SaveChangesAsync();

                totalAmount += req.TotalConsumption;
                totalExpensePrice += totalPrice;

                await ExpenseStockIssueHelper.ApplyAutoIssueForExpenseProductAsync(
                    _db, warehouseId, req.MaterialID, req.TotalConsumption,
                    displayUnitId.Value, expenseProduct.ID);

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

            var marker = WarehouseFlowService.ProductionIncomeMarker + productionId;
            var expenses = await _db.EXPENSES
                .Include(e => e.EXPENSE_PRODUCTS)
                .Where(e => e.IsDeleted != true
                    && e.ReceiverWhID == null
                    && e.Description != null
                    && e.Description.Contains(marker))
                .ToListAsync();

            expenses = expenses
                .Where(e => WarehouseFlowService.DescriptionMatchesProductionId(e.Description, productionId))
                .ToList();

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
        public double RequiredQtyMeters { get; set; }
        public double WasteQty { get; set; }
        public double TotalConsumption { get; set; }
        public int UnitID { get; set; }
        public bool RequiresCutting { get; set; }
        /// <summary>True bo'lsa, TotalConsumption kg (piece→kg konversiyasidan keyin).</summary>
        public bool TotalConsumptionIsInKg { get; set; }
    }
}
