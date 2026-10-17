using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using tahsinERP.Models;
using tahsinERP.Controllers;
using tahsinERP.Services.Common;
using tahsinERP.Services.DlocFifo;
using tahsinERP.Services.Stock;

namespace tahsinERP.Services
{
    /// <summary>
    /// Service for managing warehouse flow: Production WH → Sales WH → Customer
    /// Handles finished goods flow with proper stock management and document traceability
    /// </summary>
    public static class WarehouseFlowService
    {
        /// <summary>
        /// CURRENCIES dan Shortname = "UZS" bo'lganini oladi, yo'q bo'lsa birinchi valyuta ID si.
        /// </summary>
        private static int GetDefaultCurrencyId(DBTHSNEntities db)
        {
            var c = db.CURRENCIES.FirstOrDefault(x => x.Shortname != null && x.Shortname.Trim().ToUpper() == "UZS")
                ?? db.CURRENCIES.OrderBy(x => x.ID).FirstOrDefault();
            return c != null ? c.ID : 1;
        }

        /// <summary>
        /// Ishlab chiqarish ombori — WarehouseType = PRODUCTION (bir nechta bo'lsa, sexi mos kelgani).
        /// Sozlanmagan bo'lsa null (SRM-B12: nom bo'yicha taxmin va "birinchi ombor" zaxirasi yo'q).
        /// </summary>
        public static Warehouse GetProductionWarehouse(DBTHSNEntities db, int? shopID = null)
        {
            var id = WarehouseRoles.GetWarehouseId(db, WarehouseRoles.Production, shopID);
            return id.HasValue ? db.WAREHOUSES.Find(id.Value) : null;
        }

        /// <summary>Savdo ombori — WarehouseType = SALES; sozlanmagan bo'lsa null.</summary>
        public static Warehouse GetSalesWarehouse(DBTHSNEntities db)
        {
            var id = WarehouseRoles.GetWarehouseId(db, WarehouseRoles.Sales);
            return id.HasValue ? db.WAREHOUSES.Find(id.Value) : null;
        }

        public const string AutoTransferIncomeMarker = "Chiqimdan avtomatik bog'langan:";
        public const string ProductionIncomeMarker = "ProductionID:";

        /// <summary>
        /// Izohda "ProductionID:{id}" markeri aniq mosligini tekshiradi (chegara bilan).
        /// "ProductionID:5" "ProductionID:50" ga mos kelmasligi uchun marker'dan keyin
        /// raqam kelmasligi (yoki satr tugashi) talab qilinadi.
        /// </summary>
        public static bool DescriptionMatchesProductionId(string description, int productionId)
        {
            if (string.IsNullOrEmpty(description) || productionId <= 0)
                return false;

            var marker = ProductionIncomeMarker + productionId;
            int idx = 0;
            while ((idx = description.IndexOf(marker, idx, StringComparison.Ordinal)) >= 0)
            {
                int after = idx + marker.Length;
                if (after >= description.Length || !char.IsDigit(description[after]))
                    return true;
                idx = after;
            }
            return false;
        }

        private static bool _incomesHaveProductionIdColumn;

        /// <summary>
        /// INCOMES.ProductionID ustuni bormi (MES-P12 migratsiyasi). EDMX'da property bo'lmasa ham ishlashi uchun
        /// ustunga xom SQL bilan murojaat qilinadi; faqat "bor" natijasi keshlanadi (migratsiya keyin qo'llansa ham topiladi).
        /// </summary>
        private static bool IncomesHaveProductionIdColumn(DBTHSNEntities db)
        {
            if (_incomesHaveProductionIdColumn)
                return true;
            var exists = db.Database.SqlQuery<int>(
                "SELECT CASE WHEN COL_LENGTH('dbo.INCOMES', 'ProductionID') IS NULL THEN 0 ELSE 1 END").FirstOrDefault() == 1;
            if (exists)
                _incomesHaveProductionIdColumn = true;
            return exists;
        }

        /// <summary>
        /// Ishlab chiqarishning (o'chirilmagan) avtomatik kirimlari: avval INCOMES.ProductionID bo'yicha (indeksli, aniq);
        /// topilmasa — zaxira sifatida izohdagi "ProductionID:x" markeri (aniq moslik bilan; backfill qilinmagan eski yozuvlar).
        /// </summary>
        public static List<int> FindProductionIncomeIds(DBTHSNEntities db, int productionId)
        {
            if (productionId <= 0)
                return new List<int>();

            if (IncomesHaveProductionIdColumn(db))
            {
                var byColumn = db.Database.SqlQuery<int>(
                    "SELECT ID FROM dbo.INCOMES WHERE ProductionID = @p0 AND ISNULL(IsDeleted, 0) = 0", productionId).ToList();
                if (byColumn.Count > 0)
                    return byColumn;
            }

            var marker = ProductionIncomeMarker + productionId;
            return db.INCOMES
                .Where(i => i.IsDeleted != true && i.Description != null && i.Description.Contains(marker))
                .Select(i => new { i.ID, i.Description })
                .AsEnumerable()
                .Where(i => DescriptionMatchesProductionId(i.Description, productionId))
                .Select(i => i.ID)
                .ToList();
        }

        /// <summary>Ushbu ishlab chiqarish uchun avtomatik kirim yaratilganmi.</summary>
        public static bool HasProductionIncome(DBTHSNEntities db, int productionId)
        {
            return FindProductionIncomeIds(db, productionId).Count > 0;
        }

        /// <summary>
        /// Ishlab chiqarish tahrirlanganda eski kirim/DLOC lotini to'liq bekor qiladi:
        /// LOT_LOCATIONS qatorlarini o'chiradi, lotni va kirim hujjatini "deleted" deb belgilaydi
        /// va PRODUCT_STOCKS dan faqat hali ishlatilmagan (QtyConsumed == 0) qismini ayiradi.
        /// Shu yo'l bilan DLOC ledger va PRODUCT_STOCKS doim mos keladi (aggregate'ni to'g'ridan-to'g'ri
        /// tuzatib, DLOC'ni eskirgan holatda qoldirish o'rniga).
        /// </summary>
        public static void ReverseProductionIncome(DBTHSNEntities db, int productionId, int productId,
            int warehouseId, string userEmail)
        {
            if (productionId <= 0)
                return;

            var incomeIds = FindProductionIncomeIds(db, productionId);

            if (!incomeIds.Any())
                return;

            var lots = db.INCOME_LOTS
                .Where(l => incomeIds.Contains(l.IncomeID) && l.ProductID == productId
                    && l.WarehouseID == warehouseId && !l.IsDeleted)
                .ToList();

            foreach (var lot in lots)
            {
                if (lot.QtyConsumed > 0)
                {
                    throw new InvalidOperationException(
                        $"Lot {lot.LotNo} qisman ishlatilgan ({lot.QtyConsumed} dona chiqarilgan), shu sabab " +
                        "bu ishlab chiqarish yozuvini tahrirlash mumkin emas.");
                }

                var locations = db.LOT_LOCATIONS.Where(ll => ll.LotID == lot.ID).ToList();
                var totalAvailable = locations.Sum(ll => ll.QtyAvailable);

                foreach (var loc in locations)
                {
                    db.LOT_LOCATIONS.Remove(loc);
                }

                lot.QtyConsumed = lot.QtyReceived;
                lot.IsDeleted = true;

                if (totalAvailable > 0)
                {
                    // Lot joylaridagi miqdor allaqachon qoldiq birligida (unitId = 0 — konversiyasiz)
                    StockLedger.Adjust(db, warehouseId, productId, -(double)totalAvailable);
                }
            }

            foreach (var incomeId in incomeIds)
            {
                var income = db.INCOMES.Find(incomeId);
                if (income != null)
                {
                    income.IsDeleted = true;
                }
            }

            db.SaveChanges();

            LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "ReverseProductionIncome",
                $"ProductionID {productionId} uchun eski kirim/DLOC lot(lar) bekor qilindi.");
        }

        /// <summary>
        /// Chiqim (EXPENSE) dan avtomatik yaratilgan ichki o'tkazma kirimi.
        /// </summary>
        public static bool IsAutoLinkedTransferIncome(INCOME income)
        {
            if (income == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(income.Description))
            {
                if (income.Description.Contains(AutoTransferIncomeMarker))
                {
                    return true;
                }

                if (income.Description.IndexOf("EXPENSE_ID:", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return (income.DocNo ?? "").StartsWith("TRNSF_", StringComparison.OrdinalIgnoreCase)
                && income.SenderWHID.HasValue;
        }

        /// <summary>
        /// Qo'lda yangi kirim yaratish mumkinligi (faqat xomashyo ombori — invoys bilan).
        /// </summary>
        public static bool CanCreateManualIncome(DBTHSNEntities db, int receivingWhId)
        {
            return WarehouseRoles.GetType(db, receivingWhId) == WarehouseRoles.Supply;
        }

        /// <summary>
        /// Qo'lda kirim yaratish validatsiyasi. null — ruxsat.
        /// </summary>
        public static string ValidateManualIncomeCreate(DBTHSNEntities db, int receivingWhId, int? invoiceId)
        {
            if (!WarehouseRoles.Exists(db, receivingWhId))
            {
                return "Ombor topilmadi.";
            }

            if (WarehouseRoles.IsProductionOrSales(WarehouseRoles.GetType(db, receivingWhId)))
            {
                return "Ishlab chiqarish va Savdo omborlariga qo'lda kirim yaratish mumkin emas. Faqat chiqim hujjatidan avtomatik yaratilgan kirimni qabul qiling.";
            }

            return null;
        }

        /// <summary>
        /// Kirimni qabul qilish validatsiyasi. null — ruxsat.
        /// </summary>
        public static string ValidateIncomeReceive(DBTHSNEntities db, INCOME income)
        {
            if (income == null)
            {
                return "Kirim hujjati topilmadi.";
            }

            if (!WarehouseRoles.Exists(db, income.ReceivingWHID))
            {
                return "Ombor topilmadi.";
            }

            if (WarehouseRoles.IsProductionOrSales(WarehouseRoles.GetType(db, income.ReceivingWHID)))
            {
                if (!IsAutoLinkedTransferIncome(income))
                {
                    return "Ushbu omborga faqat chiqimdan avtomatik yaratilgan kirimni qabul qilish mumkin.";
                }
            }

            return null;
        }

        /// <summary>
        /// Kirim hujjatini tahrirlash mumkinligi.
        /// </summary>
        public static string ValidateIncomeEdit(DBTHSNEntities db, INCOME income)
        {
            if (income == null)
            {
                return "Kirim hujjati topilmadi.";
            }

            if (!WarehouseRoles.Exists(db, income.ReceivingWHID))
            {
                return "Ombor topilmadi.";
            }

            if (WarehouseRoles.IsProductionOrSales(WarehouseRoles.GetType(db, income.ReceivingWHID)))
            {
                return "Ishlab chiqarish va Savdo omborlaridagi kirimlarni qo'lda tahrirlash mumkin emas.";
            }

            return null;
        }

        /// <summary>
        /// Ishlab chiqarish kiritilganda: avtomatik kirim + ombor qoldig'i.
        /// Sifat nazorati qoldiqni bloklamaydi (transfer alohida tekshiriladi).
        /// Faqat DLOC orqali qabul; DLOC yo'q bo'lsa kirim yaratilmaydi va xato qaytariladi.
        /// Chaqiruvchining tranzaksiyasida (yoki o'z tranzaksiyasida) bajariladi.
        /// </summary>
        public static void IncreaseProductionStock(int productID, double quantity, int shopID,
            DBTHSNEntities db, string userEmail, int? productionID = null)
        {
            if (quantity <= 0)
                return;

            try
            {
                StockLedger.InTransaction(db, () =>
                {
                    var product = db.PRODUCTS.Find(productID);
                    if (product == null)
                        throw new Exception($"Mahsulot topilmadi: {productID}");

                    var productionWH = GetProductionWarehouse(db, shopID);
                    if (productionWH == null)
                        throw new Exception("Ishlab chiqarish ombori topilmadi");

                    ProductionDlocGuard.RequireForProductionWarehouse(db, productionWH.ID, productionWH.WHName);

                    var unitId = product.UnitID > 0
                        ? product.UnitID
                        : (db.UNITS.OrderBy(u => u.ID).Select(u => u.ID).FirstOrDefault());
                    if (unitId <= 0)
                        unitId = 1;

                    var income = new INCOME
                    {
                        DocNo = $"PROD-{DateTime.Now:yyyyMMddHHmmss}-{productID}",
                        ReceivingWHID = productionWH.ID,
                        IssueDateTime = DateTime.Now,
                        CurrencyID = GetDefaultCurrencyId(db),
                        IsDeleted = false,
                        RecieveStatus = true,
                        Description = productionID.HasValue && productionID.Value > 0
                            ? $"Ishlab chiqarishdan kirim: {product.PNo} - {product.Name}. ProductionID:{productionID.Value}"
                            : $"Ishlab chiqarishdan kirim: {product.PNo} - {product.Name}"
                    };

                    var incomeProduct = new INCOME_PRODUCTS
                    {
                        INCOME = income,
                        ProductID = productID,
                        Amount = quantity,
                        PiecePrice = 0,
                        UnitID = unitId
                    };
                    db.INCOMES.Add(income);
                    db.INCOME_PRODUCTS.Add(incomeProduct);
                    db.SaveChanges();

                    // MES-P12: ishlab chiqarishga FK bog'lanish (izohdagi marker ham saqlanadi). EDMX'da property
                    // qo'shilgunga qadar xom SQL bilan; ustun yo'q bo'lsa (migratsiyadan oldin) — o'tkazib yuboriladi.
                    if (productionID.HasValue && productionID.Value > 0 && IncomesHaveProductionIdColumn(db))
                    {
                        db.Database.ExecuteSqlCommand(
                            "UPDATE dbo.INCOMES SET ProductionID = @p0 WHERE ID = @p1", productionID.Value, income.ID);
                    }

                    var locations = DlocFifoDataAccess.GetLocations(db, productionWH.ID, activeOnly: true);
                    StockLedger.ReceiveLine(db, productionWH.ID, income.ID, incomeProduct.ID, productID,
                        quantity, unitId, locations.First().ID, income.IssueDateTime, $"PROD-{income.ID}");

                    LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "IncreaseProductionStock",
                        $"Mahsulot {product.PNo} uchun {quantity} miqdor ishlab chiqarish omboriga qo'shildi. Kirim ID: {income.ID}, Ishlab chiqarish ID: {productionID}");
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "IncreaseProductionStock",
                    $"Xatolik: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Keyingi o'tkazma hujjat raqami: TRNSF_ddMMyy_N (masalan TRNSF_150326_1), shu kun ichida N oshadi.
        /// Tranzaksiya ichida chaqiriladi (DocumentNumberService qulfi tranzaksiya oxirigacha turadi).
        /// </summary>
        public static string GetNextTransferDocNo(DBTHSNEntities db, DateTime? transferDate = null)
        {
            var dt = transferDate ?? DateTime.Now;
            return DocumentNumberService.Next(db, "EXPENSES", "DocNo", "TRNSF_" + dt.ToString("ddMMyy") + "_");
        }

        /// <summary>Ishlab chiqarish → sotuv o'tkazmasining bitta qatori.</summary>
        public sealed class ProductionTransferLine
        {
            public int ProductID { get; set; }
            public double Quantity { get; set; }
            /// <summary>Qaysi ishlab chiqarish lotidan qancha olingani (ProdID:123:7|ProdID:124:3).</summary>
            public System.Collections.Generic.List<LotAllocation> LotAllocations { get; set; }
        }

        /// <summary>
        /// Tayyor mahsulotni ishlab chiqarish omboridan sotuv omboriga o'tkazadi (SRM-B10):
        /// BITTA chiqim (N qator) + BITTA kutilayotgan kirim (N qator), hammasi bitta tranzaksiyada.
        /// Biror qatorda xato bo'lsa — hech narsa yozilmaydi. Sotuv ombori qoldig'i MRP kirimni
        /// "Qabul qilish" (IncomesController.Receive) qilganda oshadi.
        /// </summary>
        /// <returns>Yaratilgan chiqim hujjati.</returns>
        public static EXPENS TransferProductionToSales(DBTHSNEntities db,
            System.Collections.Generic.IList<ProductionTransferLine> lines, string userEmail,
            string transferDocNo = null, byte[] transferDocument = null, string description = null,
            DateTime? transferDate = null)
        {
            if (lines == null || !lines.Any(l => l.Quantity > 0))
                throw new ArgumentException("O'tkazma qatorlari bo'sh.", nameof(lines));

            EXPENS expense = null;
            try
            {
                StockLedger.InTransaction(db, () =>
                {
                    var productionWH = GetProductionWarehouse(db);
                    var salesWH = GetSalesWarehouse(db);
                    if (productionWH == null)
                        throw new Exception("Ishlab chiqarish ombori topilmadi");
                    if (salesWH == null)
                        throw new Exception("Sotuv ombori topilmadi");

                    var activeLines = lines.Where(l => l.Quantity > 0).ToList();
                    var productIds = activeLines.Select(l => l.ProductID).Distinct().ToList();
                    var products = db.PRODUCTS.Where(p => productIds.Contains(p.ID)).ToDictionary(p => p.ID);

                    // Bir mahsulot bir necha qatorda bo'lsa ham jami miqdor tekshiriladi
                    foreach (var g in activeLines.GroupBy(l => l.ProductID))
                    {
                        if (!products.ContainsKey(g.Key))
                            throw new Exception($"Mahsulot topilmadi: {g.Key}");
                        var need = g.Sum(x => x.Quantity);
                        var available = StockLedger.GetOnHand(db, productionWH.ID, g.Key, lockForUpdate: true);
                        if (available + StockLedger.Epsilon < need)
                        {
                            throw new Exception(
                                $"Ishlab chiqarish omborida yetarli mahsulot yo'q ({products[g.Key].PNo}). " +
                                $"Mavjud: {available}, Talab qilinmoqda: {need}");
                        }
                    }

                    if (string.IsNullOrEmpty(transferDocNo))
                        transferDocNo = GetNextTransferDocNo(db, transferDate);

                    var issueDt = transferDate ?? DateTime.Now;
                    int currencyId = GetDefaultCurrencyId(db);
                    var pnoList = string.Join(", ", activeLines.Select(l => products[l.ProductID].PNo).Distinct());

                    // Narx: oxirgi sotuv shartnomasidan (mahsulotlar bo'yicha bitta so'rov)
                    var prices = db.S_CONTRACT_PRODUCTS
                        .Where(cp => productIds.Contains(cp.ProductID)
                            && (cp.S_CONTRACTS.IsDeleted == false || cp.S_CONTRACTS.IsDeleted == null))
                        .Select(cp => new { cp.ProductID, cp.PiecePrice, cp.S_CONTRACTS.IssuedDate })
                        .ToList()
                        .GroupBy(x => x.ProductID)
                        .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IssuedDate).First().PiecePrice);

                    expense = new EXPENS
                    {
                        DocNo = transferDocNo,
                        SenderWHID = productionWH.ID,
                        ReceiverWhID = salesWH.ID,
                        IssueDateTime = issueDt,
                        CurrencyID = currencyId,
                        IsDeleted = false,
                        SendStatus = true,
                        Description = description ?? $"Ishlab chiqarishdan sotuv omboriga o'tkazma: {pnoList}"
                    };
                    db.EXPENSES.Add(expense);

                    var expenseLines = new System.Collections.Generic.List<EXPENSE_PRODUCTS>();
                    foreach (var line in activeLines)
                    {
                        var product = products[line.ProductID];
                        double piecePrice;
                        prices.TryGetValue(line.ProductID, out piecePrice);
                        var ep = new EXPENSE_PRODUCTS
                        {
                            EXPENS = expense,
                            ProductID = line.ProductID,
                            Amount = line.Quantity,
                            PiecePrice = piecePrice,
                            TotalPrice = line.Quantity * piecePrice,
                            UnitID = product.UnitID > 0 ? product.UnitID : 1,
                            Comment = ProductionLotService.BuildLotAllocationsComment(line.LotAllocations)
                        };
                        db.EXPENSE_PRODUCTS.Add(ep);
                        expenseLines.Add(ep);
                    }
                    expense.Amount = expenseLines.Sum(x => x.Amount);
                    expense.TotalPrice = expenseLines.Sum(x => x.TotalPrice);

                    if (transferDocument != null)
                        db.WRHS_EXPENSE_DOCS.Add(new WRHS_EXPENSE_DOCS { EXPENS = expense, Doc = transferDocument });

                    db.SaveChanges();

                    foreach (var ep in expenseLines)
                        StockLedger.IssueLine(db, productionWH.ID, ep.ProductID, ep.Amount, ep.UnitID, ep.ID);

                    // Sotuv omboriga kutilayotgan kirim (MRP "Qabul qilish" bilan tasdiqlaydi)
                    var income = new INCOME
                    {
                        DocNo = transferDocNo + "-IN",
                        ReceivingWHID = salesWH.ID,
                        SenderWHID = productionWH.ID,
                        IssueDateTime = issueDt,
                        CurrencyID = currencyId,
                        IsDeleted = false,
                        RecieveStatus = false,
                        Amount = expense.Amount,
                        TotalPrice = expense.TotalPrice,
                        Description = $"{AutoTransferIncomeMarker} {expense.DocNo} - Ishlab chiqarishdan sotuv omboriga o'tkazma: {pnoList}. EXPENS ID: {expense.ID}"
                    };
                    db.INCOMES.Add(income);
                    foreach (var ep in expenseLines)
                    {
                        db.INCOME_PRODUCTS.Add(new INCOME_PRODUCTS
                        {
                            INCOME = income,
                            ProductID = ep.ProductID,
                            Amount = ep.Amount,
                            PiecePrice = ep.PiecePrice,
                            TotalPrice = ep.TotalPrice,
                            UnitID = ep.UnitID
                        });
                    }

                    if (transferDocument != null)
                        db.WRHS_INCOME_DOCS.Add(new WRHS_INCOME_DOCS { INCOME = income, Doc = transferDocument });

                    db.SaveChanges();

                    LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "TransferProductionToSales",
                        $"Ishlab chiqarishdan sotuv omboriga o'tkazma {expense.DocNo}: {activeLines.Count} qator ({pnoList}). " +
                        $"Chiqim ID: {expense.ID}, Kirim ID: {income.ID}");
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "TransferProductionToSales",
                    $"Xatolik: {ex.Message}");
                throw;
            }

            return expense;
        }

        /// <summary>
        /// Decrease stock in Sales Warehouse when sales invoice is confirmed
        /// Prevents negative stock
        /// </summary>
        public static void DecreaseSalesStock(int productID, decimal quantity,
            DBTHSNEntities db, string userEmail, long invoiceID, string invoiceNo, DateTime? invoiceDate = null, int? contractID = null)
        {
            try
            {
                StockLedger.InTransaction(db, () =>
                {
                    var product = db.PRODUCTS.Find(productID);
                    if (product == null)
                        throw new Exception($"Mahsulot topilmadi: {productID}");

                    var salesWH = GetSalesWarehouse(db);
                    if (salesWH == null)
                        throw new Exception("Sotuv ombori topilmadi");

                    double qtyToDeduct = (double)quantity;
                    var availableQty = InventoryValuationService.GetQty(db, salesWH.ID, productID);

                    if (availableQty + 1e-9 < qtyToDeduct)
                    {
                        throw new Exception(
                            $"Sotuv omborida yetarli mahsulot yo'q. " +
                            $"Mahsulot: {product.PNo}, Mavjud: {availableQty}, Talab qilinmoqda: {qtyToDeduct}, " +
                            $"Invoys: {invoiceNo}");
                    }

                    int currencyId = GetDefaultCurrencyId(db);
                    var issueDate = invoiceDate ?? DateTime.Now;
                    var dateStr = issueDate.ToString("yyyyMMdd");
                    var qtyStr = ((double)quantity).ToString("0.##");

                    // Piece price olish: avvalo tanlangan sotuv shartnomasidan, bo'lmasa oxirgi sotuv shartnomasidan
                    double piecePrice = 0;

                    S_CONTRACT_PRODUCTS contractProduct = null;
                    if (contractID.HasValue)
                    {
                        contractProduct = db.S_CONTRACT_PRODUCTS
                            .Include(cp => cp.S_CONTRACTS)
                            .FirstOrDefault(cp => cp.ContractID == contractID.Value && cp.ProductID == productID);
                    }
                    if (contractProduct == null)
                    {
                        contractProduct = db.S_CONTRACT_PRODUCTS
                            .Include(cp => cp.S_CONTRACTS)
                            .Where(cp => cp.ProductID == productID && (cp.S_CONTRACTS.IsDeleted == false || cp.S_CONTRACTS.IsDeleted == null))
                            .OrderByDescending(cp => cp.S_CONTRACTS.IssuedDate)
                            .FirstOrDefault();
                    }
                    if (contractProduct != null)
                    {
                        piecePrice = contractProduct.PiecePrice;
                    }
                    var totalPrice = piecePrice * qtyToDeduct;
                    var invoiceUrl = DocumentMarkers.SalesInvoiceUrl + invoiceID;

                    // Create EXPENS document (ProductProduction uslubiga yaqin: date_pno_qty + marker)
                    var expense = new EXPENS
                    {
                        DocNo = $"{dateStr}_{product.PNo}_{qtyStr}",
                        SenderWHID = salesWH.ID,
                        IssueDateTime = issueDate,
                        CurrencyID = currencyId,
                        IsDeleted = false,
                        SendStatus = true,
                        Amount = qtyToDeduct,
                        TotalPrice = totalPrice,
                        Description = $"{issueDate:dd.MM.yyyy}y. sotuv invoysi asosida tayyor mahsulot chiqimi: {product.PNo}, Miqdor {qtyStr}. Invoys raqami: {invoiceNo}. URL: {invoiceUrl}"
                    };

                    var expenseProduct = new EXPENSE_PRODUCTS
                    {
                        EXPENS = expense,
                        ProductID = productID,
                        Amount = qtyToDeduct,
                        PiecePrice = piecePrice,
                        TotalPrice = totalPrice,
                        UnitID = product.UnitID > 0 ? product.UnitID : 1,
                        Comment = $"Invoys raqami: {invoiceNo}. URL: {invoiceUrl}"
                    };

                    db.EXPENSES.Add(expense);
                    db.EXPENSE_PRODUCTS.Add(expenseProduct);
                    db.SaveChanges();

                    StockLedger.IssueLine(db, salesWH.ID, productID, qtyToDeduct, expenseProduct.UnitID, expenseProduct.ID);

                    LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "DecreaseSalesStock",
                        $"Sotuv invoysi {invoiceNo} uchun mahsulot {product.PNo} dan {qtyToDeduct} miqdor sotuv omboridan chiqarildi. " +
                        $"Chiqim ID: {expense.ID}, Hisob-faktura ID: {invoiceID}");
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "DecreaseSalesStock",
                    $"Xatolik: {ex.Message}, Hisob-faktura ID: {invoiceID}");
                throw;
            }
        }

        /// <summary>
        /// Sales invoice bekor qilinganda:
        /// 1) avtomatik yaratilgan EXPENS hujjatlarini topadi (invoice URL marker orqali, aniq moslik bilan:
        ///    "/SalesInvoices/Details/12" 123-invoysga mos kelmaydi — SRM-B06),
        /// 2) sotuv ombori qoldig'ini (va DLOC lot ajratmalarini) qayta tiklaydi,
        /// 3) EXPENSES/EXPENSE_PRODUCTS/WRHS_EXPENSE_DOCS yozuvlarini fizik o'chiradi.
        /// </summary>
        public static void RollbackSalesInvoiceStockAndDeleteAutoExpenses(long invoiceID, DBTHSNEntities db, string userEmail)
        {
            try
            {
                StockLedger.InTransaction(db, () =>
                {
                    var invoiceUrlMarker = DocumentMarkers.For(DocumentMarkers.SalesInvoiceUrl, invoiceID);

                    var expenseIdsFromHeader = db.EXPENSES
                        .Where(e => e.Description != null && e.Description.Contains(invoiceUrlMarker))
                        .Select(e => new { e.ID, e.Description })
                        .ToList()
                        .Where(e => DocumentMarkers.Matches(e.Description, DocumentMarkers.SalesInvoiceUrl, invoiceID))
                        .Select(e => e.ID);

                    var expenseIdsFromLines = db.EXPENSE_PRODUCTS
                        .Where(ep => ep.Comment != null && ep.Comment.Contains(invoiceUrlMarker))
                        .Select(ep => new { ep.ExpenseID, ep.Comment })
                        .ToList()
                        .Where(ep => DocumentMarkers.Matches(ep.Comment, DocumentMarkers.SalesInvoiceUrl, invoiceID))
                        .Select(ep => ep.ExpenseID);

                    var expenseIds = expenseIdsFromHeader
                        .Union(expenseIdsFromLines)
                        .Distinct()
                        .ToList();

                    if (!expenseIds.Any())
                    {
                        LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "RollbackSalesInvoiceStockAndDeleteAutoExpenses",
                            $"Invoice {invoiceID} uchun auto-expense topilmadi.");
                        return;
                    }

                    var expenses = db.EXPENSES
                        .Where(e => expenseIds.Contains(e.ID))
                        .ToList();

                    var expenseLines = db.EXPENSE_PRODUCTS
                        .Where(ep => expenseIds.Contains(ep.ExpenseID))
                        .ToList();

                    // Qoldiq chiqim qilingan omborga qaytariladi (avval har doim "sotuv ombori" deb olinardi)
                    var senderByExpense = expenses.ToDictionary(e => e.ID, e => e.SenderWHID);
                    foreach (var line in expenseLines)
                    {
                        var expense = expenses.First(e => e.ID == line.ExpenseID);
                        if (expense.IsDeleted == true)
                            continue;
                        StockLedger.ReverseIssueLine(db, senderByExpense[line.ExpenseID], line.ProductID, line.Amount,
                            line.UnitID > 0 ? line.UnitID : 1, line.ID);
                    }

                    var expenseDocs = db.WRHS_EXPENSE_DOCS
                        .Where(d => expenseIds.Contains(d.ExpenseID))
                        .ToList();
                    if (expenseDocs.Any())
                    {
                        db.WRHS_EXPENSE_DOCS.RemoveRange(expenseDocs);
                    }

                    db.EXPENSE_PRODUCTS.RemoveRange(expenseLines);
                    db.EXPENSES.RemoveRange(expenses);

                    db.SaveChanges();

                    LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "RollbackSalesInvoiceStockAndDeleteAutoExpenses",
                        $"Invoice {invoiceID} uchun {expenseIds.Count} ta auto-expense fizik o'chirildi va stock qaytarildi.");
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "RollbackSalesInvoiceStockAndDeleteAutoExpenses",
                    $"Xatolik: {ex.Message}, InvoiceID: {invoiceID}");
                throw;
            }
        }

        /// <summary>
        /// Get current stock in Sales Warehouse for a product
        /// </summary>
        public static double GetSalesStock(int productID, DBTHSNEntities db)
        {
            var salesWH = GetSalesWarehouse(db);
            if (salesWH == null)
                return 0;

            return InventoryValuationService.GetQty(db, salesWH.ID, productID);
        }

        /// <summary>
        /// Get current stock in Production Warehouse for a product
        /// </summary>
        public static double GetProductionStock(int productID, DBTHSNEntities db, int? shopID = null)
        {
            var productionWH = GetProductionWarehouse(db, shopID);
            if (productionWH == null)
                return 0;

            return InventoryValuationService.GetQty(db, productionWH.ID, productID);
        }
    }
}
