using System;
using System.Data.Entity;
using System.Linq;
using System.Transactions;
using tahsinERP.Models;
using tahsinERP.Controllers;
using tahsinERP.Services.DlocFifo;

namespace tahsinERP.Services
{
    /// <summary>
    /// Service for managing warehouse flow: Production WH → Sales WH → Customer
    /// Handles finished goods flow with proper stock management and document traceability
    /// </summary>
    public static class WarehouseFlowService
    {
        // Warehouse type identification constants
        private const string PRODUCTION_WH_KEYWORDS = "PRODUCTION,ISHLAB CHIQARISH,PROD";
        private const string SALES_WH_KEYWORDS = "SALES,SOTUV,SALE,SAVDO";
        private const string SUPPLY_WH_KEYWORDS = "SUPPLY,XOMASHYO,SUPPLIER";

        private static string[] SplitKeywords(string keywords)
        {
            return (keywords ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => k.Length > 0)
                .ToArray();
        }

        /// <summary>
        /// CURRENCIES dan Shortname = "UZS" bo'lganini oladi, yo'q bo'lsa birinchi valyuta ID si.
        /// </summary>
        private static int GetDefaultCurrencyId(DBTHSNEntities db)
        {
            var c = db.CURRENCIES.FirstOrDefault(x => x.Shortname != null && x.Shortname.Trim().ToUpper() == "UZS")
                ?? db.CURRENCIES.OrderBy(x => x.ID).FirstOrDefault();
            return c != null ? c.ID : 1;
        }

        private static bool NameMatchesKeywords(string name, string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(name) || keywords == null || keywords.Length == 0)
            {
                return false;
            }

            return keywords.Any(k =>
                name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Get Production Warehouse (for finished goods from production)
        /// </summary>
        public static Warehouse GetProductionWarehouse(DBTHSNEntities db, int? shopID = null)
        {
            var query = db.WAREHOUSES.Where(w => w.IsDeleted != true);

            // Try to find by name keywords
            var productionKeywords = SplitKeywords(PRODUCTION_WH_KEYWORDS);
            var productionWH = query
                .AsEnumerable()
                .FirstOrDefault(w => NameMatchesKeywords(w.WHName, productionKeywords));

            if (productionWH != null)
                return productionWH;

            // If shopID provided, try to find warehouse for that shop
            if (shopID.HasValue)
            {
                productionWH = query
                    .Where(w => w.ShopID == shopID.Value)
                    .FirstOrDefault();
            }

            // Fallback: get first active warehouse
            if (productionWH == null)
            {
                productionWH = query.FirstOrDefault();
            }

            return productionWH;
        }

        /// <summary>
        /// Get Sales Warehouse (for finished goods ready for sale)
        /// </summary>
        public static Warehouse GetSalesWarehouse(DBTHSNEntities db)
        {
            // Try to find by name keywords (Savdo Ombori, Sotuv ombori, Sales)
            var salesKeywords = SplitKeywords(SALES_WH_KEYWORDS);
            var salesWH = db.WAREHOUSES
                .Where(w => w.IsDeleted != true)
                .AsEnumerable()
                .FirstOrDefault(w => NameMatchesKeywords(w.WHName, salesKeywords));

            // Fallback: Warehouse ID 7 — odatda Savdo/Sotuv ombori
            if (salesWH == null)
            {
                salesWH = db.WAREHOUSES.FirstOrDefault(w => w.ID == 7 && w.IsDeleted != true);
            }

            // Oxirgi fallback: birinchi faol ombor
            if (salesWH == null)
            {
                salesWH = db.WAREHOUSES
                    .Where(w => w.IsDeleted != true)
                    .FirstOrDefault();
            }

            return salesWH;
        }

        /// <summary>
        /// Get Supply Warehouse (raw materials)
        /// </summary>
        public static Warehouse GetSupplyWarehouse(DBTHSNEntities db)
        {
            var supplyKeywords = SplitKeywords(SUPPLY_WH_KEYWORDS);
            var supplyWH = db.WAREHOUSES
                .Where(w => w.IsDeleted != true)
                .AsEnumerable()
                .FirstOrDefault(w => NameMatchesKeywords(w.WHName, supplyKeywords));

            if (supplyWH == null)
            {
                supplyWH = db.WAREHOUSES
                    .Where(w => w.IsDeleted != true)
                    .FirstOrDefault();
            }

            return supplyWH;
        }

        public const string AutoTransferIncomeMarker = "Chiqimdan avtomatik bog'langan:";
        public const string ProductionIncomeMarker = "ProductionID:";

        /// <summary>Ushbu ishlab chiqarish uchun avtomatik kirim yaratilganmi.</summary>
        public static bool HasProductionIncome(DBTHSNEntities db, int productionId)
        {
            if (productionId <= 0)
                return false;
            var marker = ProductionIncomeMarker + productionId;
            return db.INCOMES.Any(i => i.IsDeleted != true && i.Description != null && i.Description.Contains(marker));
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

            var marker = ProductionIncomeMarker + productionId;
            var incomeIds = db.INCOMES
                .Where(i => i.IsDeleted != true && i.Description != null && i.Description.Contains(marker))
                .Select(i => i.ID)
                .ToList();

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
                    AdjustProductStock(db, warehouseId, productId, -(double)totalAvailable);
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

        public static bool IsProductionWarehouse(Warehouse warehouse)
        {
            return IsWarehouseNameMatch(warehouse, PRODUCTION_WH_KEYWORDS);
        }

        public static bool IsSalesWarehouse(Warehouse warehouse)
        {
            return IsWarehouseNameMatch(warehouse, SALES_WH_KEYWORDS);
        }

        public static bool IsSupplyWarehouse(Warehouse warehouse)
        {
            return IsWarehouseNameMatch(warehouse, SUPPLY_WH_KEYWORDS);
        }

        private static bool IsWarehouseNameMatch(Warehouse warehouse, string keywordsCsv)
        {
            if (warehouse == null)
            {
                return false;
            }

            return NameMatchesKeywords(warehouse.WHName, SplitKeywords(keywordsCsv));
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
            var warehouse = db.WAREHOUSES.FirstOrDefault(w => w.ID == receivingWhId && w.IsDeleted != true);
            if (warehouse == null)
            {
                return false;
            }

            return IsSupplyWarehouse(warehouse);
        }

        /// <summary>
        /// Qo'lda kirim yaratish validatsiyasi. null — ruxsat.
        /// </summary>
        public static string ValidateManualIncomeCreate(DBTHSNEntities db, int receivingWhId, int? invoiceId)
        {
            var warehouse = db.WAREHOUSES.FirstOrDefault(w => w.ID == receivingWhId && w.IsDeleted != true);
            if (warehouse == null)
            {
                return "Ombor topilmadi.";
            }

            if (IsProductionWarehouse(warehouse) || IsSalesWarehouse(warehouse))
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

            var warehouse = db.WAREHOUSES.FirstOrDefault(w => w.ID == income.ReceivingWHID && w.IsDeleted != true);
            if (warehouse == null)
            {
                return "Ombor topilmadi.";
            }

            if (IsProductionWarehouse(warehouse) || IsSalesWarehouse(warehouse))
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

            var warehouse = db.WAREHOUSES.FirstOrDefault(w => w.ID == income.ReceivingWHID && w.IsDeleted != true);
            if (warehouse == null)
            {
                return "Ombor topilmadi.";
            }

            if (IsProductionWarehouse(warehouse) || IsSalesWarehouse(warehouse))
            {
                return "Ishlab chiqarish va Savdo omborlaridagi kirimlarni qo'lda tahrirlash mumkin emas.";
            }

            return null;
        }

        /// <summary>
        /// Ishlab chiqarish kiritilganda: avtomatik kirim + ombor qoldig'i.
        /// Sifat nazorati qoldiqni bloklamaydi (transfer alohida tekshiriladi).
        /// Faqat DLOC orqali qabul; DLOC yo'q bo'lsa kirim yaratilmaydi va xato qaytariladi.
        /// </summary>
        public static void IncreaseProductionStock(int productID, double quantity, int shopID,
            DBTHSNEntities db, string userEmail, int? productionID = null)
        {
            if (quantity <= 0)
                return;

            try
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

                db.INCOMES.Add(income);
                db.SaveChanges();

                var incomeProduct = new INCOME_PRODUCTS
                {
                    IncomeID = income.ID,
                    ProductID = productID,
                    Amount = quantity,
                    PiecePrice = 0,
                    UnitID = unitId
                };
                db.INCOME_PRODUCTS.Add(incomeProduct);
                db.SaveChanges();

                ApplyProductionIncomeToStock(db, productionWH.ID, income, incomeProduct, productID, quantity, unitId);

                LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "IncreaseProductionStock",
                    $"Mahsulot {product.PNo} uchun {quantity} miqdor ishlab chiqarish omboriga qo'shildi. Kirim ID: {income.ID}, Ishlab chiqarish ID: {productionID}");
            }
            catch (Exception ex)
            {
                LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "IncreaseProductionStock",
                    $"Xatolik: {ex.Message}");
                throw;
            }
        }

        /// <summary>Kirim qatorini faqat DLOC orqali qabul qiladi.</summary>
        private static void ApplyProductionIncomeToStock(DBTHSNEntities db, int warehouseId, INCOME income,
            INCOME_PRODUCTS incomeProduct, int productId, double quantity, int unitId)
        {
            var locations = DlocFifoDataAccess.GetLocations(db, warehouseId, activeOnly: true);
            var locationId = locations.First().ID;
            if (!LocationLotStockLedger.TryReceive(
                db, warehouseId, income.ID, incomeProduct.ID, productId,
                (decimal)quantity, locationId, income.IssueDateTime, unitId,
                $"PROD-{income.ID}"))
            {
                throw new InvalidOperationException(
                    "DLOC ga qabul qilib bo'lmadi. Joylashuv, sig'im yoki lot sozlamalarini tekshiring.");
            }
        }

        /// <summary>
        /// Kunlik ishlab chiqarish kiritish/tahrir: tayyor mahsulot qoldig'i o'zgarishi.
        /// Musbat delta: <see cref="IncreaseProductionStock"/> (kirim hujjati + qoldiq).
        /// Manfiy delta: faqat ishlab chiqarish omboridagi qoldiq kamaytiriladi (kirim hujjati yo'q).
        /// </summary>
        public static void ApplyProductionWarehouseStockDelta(int productID, double delta, int shopID,
            DBTHSNEntities db, string userEmail, int? productionID = null)
        {
            if (Math.Abs(delta) < 1e-9)
            {
                return;
            }

            if (delta > 0)
            {
                IncreaseProductionStock(productID, delta, shopID, db, userEmail, productionID);
                return;
            }

            var product = db.PRODUCTS.Find(productID);
            if (product == null)
            {
                return;
            }

            var productionWH = GetProductionWarehouse(db, shopID);
            if (productionWH == null)
            {
                throw new Exception("Ishlab chiqarish ombori topilmadi");
            }

            AdjustProductStock(db, productionWH.ID, productID, delta, product.UnitID > 0 ? product.UnitID : 1);

            LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "ApplyProductionWarehouseStockDelta",
                $"Tayyor mahsulot {product.PNo} qoldig'i {delta:0.###} ga tuzatildi (ishlab chiqarish ombori). ProductionID: {productionID}");
        }

        /// <summary>
        /// Increase stock in Production Warehouse without creating Income document
        /// </summary>
        public static void IncreaseProductionStockWithoutIncome(int productID, double quantity, int shopID,
            DBTHSNEntities db, string userEmail, int? productionID = null)
        {
            try
            {
                var product = db.PRODUCTS.Find(productID);
                if (product == null)
                    throw new Exception($"Mahsulot topilmadi: {productID}");

                var productionWH = GetProductionWarehouse(db, shopID);
                if (productionWH == null)
                    throw new Exception("Ishlab chiqarish ombori topilmadi");

                AdjustProductStock(db, productionWH.ID, productID, quantity, product.UnitID > 0 ? product.UnitID : 1);

                LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "IncreaseProductionStockWithoutIncome",
                    $"Tayyor mahsulot {product.PNo} uchun {quantity} miqdor ishlab chiqarish omboriga qo'shildi. Ishlab chiqarish ID: {productionID}");
            }
            catch (Exception ex)
            {
                LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "IncreaseProductionStockWithoutIncome",
                    $"Xatolik: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Generates next transfer document number in format TRNSF_ddmmyy_N (e.g. TRNSF_150326_1).
        /// Same day increments N.
        /// </summary>
        public static string GetNextTransferDocNo(DBTHSNEntities db, DateTime? transferDate = null)
        {
            var dt = transferDate ?? DateTime.Now;
            var datePart = dt.ToString("ddMMyy"); // ddmmyy
            var prefix = "TRNSF_" + datePart + "_";

            var maxN = db.EXPENSES
                .Where(e => e.IsDeleted == false && e.DocNo != null && e.DocNo.StartsWith(prefix))
                .ToList()
                .Select(e =>
                {
                    var suffix = e.DocNo.Length > prefix.Length ? e.DocNo.Substring(prefix.Length) : "";
                    int n;
                    return int.TryParse(suffix, out n) ? n : 0;
                })
                .DefaultIfEmpty(0)
                .Max();

            return prefix + (maxN + 1);
        }

        /// <summary>
        /// Transfer finished goods from Production WH to Sales WH
        /// Creates linked EXPENS and INCOME documents
        /// lotAllocations: qaysi Production lotdan qancha olingani (ProdID:123:7|ProdID:124:3)
        /// </summary>
        public static void TransferProductionToSales(int productID, double quantity, 
            DBTHSNEntities db, string userEmail, string transferDocNo = null, 
            byte[] transferDocument = null, string description = null, 
            System.Collections.Generic.List<LotAllocation> lotAllocations = null, DateTime? transferDate = null)
        {
            using (var transaction = new TransactionScope(TransactionScopeOption.Required))
            {
                try
                {
                    var product = db.PRODUCTS.Find(productID);
                    if (product == null)
                        throw new Exception($"Mahsulot topilmadi: {productID}");

                    var productionWH = GetProductionWarehouse(db);
                    var salesWH = GetSalesWarehouse(db);

                    if (productionWH == null)
                        throw new Exception("Ishlab chiqarish ombori topilmadi");
                    if (salesWH == null)
                        throw new Exception("Sotuv ombori topilmadi");

                    var availableQty = InventoryValuationService.GetQty(db, productionWH.ID, productID);
                    if (availableQty + 1e-9 < quantity)
                    {
                        throw new Exception(
                            $"Ishlab chiqarish omborida yetarli mahsulot yo'q. " +
                            $"Mavjud: {availableQty}, Talab qilinmoqda: {quantity}");
                    }

                    // Generate transfer document number if not provided (TRNSF_ddmmyy_N format)
                    if (string.IsNullOrEmpty(transferDocNo))
                    {
                        transferDocNo = GetNextTransferDocNo(db, transferDate);
                    }

                    var issueDt = transferDate ?? DateTime.Now;

                    // PiecePrice from S_CONTRACT_PRODUCTS (OrderByDescending IssuedDate, FirstOrDefault)
                    double piecePrice = 0;
                    var contractProduct = db.S_CONTRACT_PRODUCTS
                        .Include(cp => cp.S_CONTRACTS)
                        .Where(cp => cp.ProductID == productID && (cp.S_CONTRACTS.IsDeleted == false || cp.S_CONTRACTS.IsDeleted == null))
                        .OrderByDescending(cp => cp.S_CONTRACTS.IssuedDate)
                        .FirstOrDefault();
                    if (contractProduct != null)
                    {
                        piecePrice = contractProduct.PiecePrice;
                    }

                    int currencyId = GetDefaultCurrencyId(db);
                    // Create EXPENS from Production WH
                    var expense = new EXPENS
                    {
                        DocNo = transferDocNo,
                        SenderWHID = productionWH.ID,
                        ReceiverWhID = salesWH.ID,
                        IssueDateTime = issueDt,
                        CurrencyID = currencyId,
                        IsDeleted = false,
                        SendStatus = true,
                        Description = description ?? $"Ishlab chiqarishdan sotuv omboriga o'tkazma: {product.PNo}"
                    };

                    db.EXPENSES.Add(expense);
                    db.SaveChanges(); // Save to get ID

                    // Create EXPENSE_PRODUCTS (Comment: ProdID:123:7|ProdID:124:3)
                    var allocationComment = ProductionLotService.BuildLotAllocationsComment(lotAllocations);
                    var expenseProduct = new EXPENSE_PRODUCTS
                    {
                        ExpenseID = expense.ID,
                        ProductID = productID,
                        Amount = quantity,
                        PiecePrice = piecePrice,
                        UnitID = product.UnitID,
                        Comment = allocationComment
                    };

                    db.EXPENSE_PRODUCTS.Add(expenseProduct);
                    db.SaveChanges();

                    var unitIdForLedger = product.UnitID > 0 ? product.UnitID : 1;

                    // Attach document if provided
                    if (transferDocument != null)
                    {
                        var expenseDoc = new WRHS_EXPENSE_DOCS
                        {
                            ExpenseID = expense.ID,
                            Doc = transferDocument
                        };
                        db.WRHS_EXPENSE_DOCS.Add(expenseDoc);
                    }

                    ExpenseStockIssueHelper.ApplyAutoIssueForExpenseProduct(
                        db, productionWH.ID, productID, quantity, unitIdForLedger, expenseProduct.ID);

                    // Create INCOME to Sales WH (RecieveStatus = false - MRP accepts manually)
                    var income = new INCOME
                    {
                        DocNo = transferDocNo + "-IN",
                        ReceivingWHID = salesWH.ID,
                        SenderWHID = productionWH.ID,
                        IssueDateTime = issueDt,
                        CurrencyID = currencyId,
                        IsDeleted = false,
                        RecieveStatus = false,
                        Description = $"Chiqimdan avtomatik bog'langan: {expense.DocNo} - Ishlab chiqarishdan sotuv omboriga o'tkazma: {product.PNo}. EXPENS ID: {expense.ID}"
                    };

                    db.INCOMES.Add(income);
                    db.SaveChanges(); // Save to get ID

                    // Create INCOME_PRODUCTS (UnitID FK talab qiladi)
                    var unitId = product.UnitID > 0
                        ? product.UnitID
                        : (db.UNITS.OrderBy(u => u.ID).Select(u => u.ID).FirstOrDefault());
                    if (unitId <= 0)
                        unitId = 1;

                    var incomeProduct = new INCOME_PRODUCTS
                    {
                        IncomeID = income.ID,
                        ProductID = productID,
                        Amount = quantity,
                        PiecePrice = piecePrice,
                        UnitID = unitId
                    };

                    db.INCOME_PRODUCTS.Add(incomeProduct);

                    // Attach document if provided
                    if (transferDocument != null)
                    {
                        var incomeDoc = new WRHS_INCOME_DOCS
                        {
                            IncomeID = income.ID,
                            Doc = transferDocument
                        };
                        db.WRHS_INCOME_DOCS.Add(incomeDoc);
                    }

                    // Sotuv ombori qoldig'i faqat MRP kirimni "Qabul qilish" (Receive) qilganda yangilanadi (IncomesController.Receive).

                    db.SaveChanges();
                    transaction.Complete();

                    LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "TransferProductionToSales",
                        $"Mahsulot {product.PNo} uchun {quantity} miqdor ishlab chiqarishdan sotuv omboriga o'tkazildi. " +
                        $"Chiqim ID: {expense.ID}, Kirim ID: {income.ID}");
                }
                catch (Exception ex)
                {
                    LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "TransferProductionToSales",
                        $"Xatolik: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// PRODUCT_STOCKS umumiy qoldiq (barcha DLOC yig'indisi). Har operatsiyada +/-.
        /// </summary>
        public static void AdjustProductStock(DBTHSNEntities db, int warehouseId, int productId, double delta, int unitId = 1)
        {
            if (Math.Abs(delta) < 1e-12)
                return;

            var row = GetOrCreateStockRow(db, warehouseId, productId, unitId > 0 ? unitId : 1);
            row.Amount += delta;
            if (row.Amount < 0)
                row.Amount = 0;
            if (row.UnitID <= 0 && unitId > 0)
                row.UnitID = unitId;
        }

        /// <summary>Kirim / ishlab chiqarish qabul — umumiy qoldiq oshadi.</summary>
        public static void ApplyIncomeReceiveToStock(DBTHSNEntities db, int warehouseId, int productId, double amount, int unitId)
        {
            if (amount <= 0)
                return;
            AdjustProductStock(db, warehouseId, productId, amount, unitId);
        }

        /// <summary>Chiqim — umumiy qoldiq kamayadi.</summary>
        public static void ApplyIncomeIssueFromStock(DBTHSNEntities db, int warehouseId, int productId, double amount, int unitId)
        {
            if (amount <= 0)
                return;
            AdjustProductStock(db, warehouseId, productId, -amount, unitId);
        }

        /// <summary>
        /// Returns a single stock row for (warehouse, product). If duplicates exist, merges them.
        /// If none exists, creates one.
        /// </summary>
        public static PRODUCT_STOCKS GetOrCreateStockRow(DBTHSNEntities db, int warehouseId, int productId, int unitId = 1)
        {
            var rows = db.PRODUCT_STOCKS
                .Where(s => s.WHID == warehouseId && s.ProductID == productId)
                .OrderBy(s => s.ID)
                .ToList();

            if (!rows.Any())
            {
                var stock = new PRODUCT_STOCKS
                {
                    WHID = warehouseId,
                    ProductID = productId,
                    Amount = 0,
                    UnitID = unitId > 0 ? unitId : 1
                };
                db.PRODUCT_STOCKS.Add(stock);
                return stock;
            }

            var keeper = rows.First();
            if (keeper.UnitID <= 0)
            {
                keeper.UnitID = unitId > 0 ? unitId : 1;
            }

            if (rows.Count > 1)
            {
                keeper.Amount = rows.Sum(x => x.Amount);
                foreach (var duplicate in rows.Skip(1))
                {
                    db.PRODUCT_STOCKS.Remove(duplicate);
                }
            }

            return keeper;
        }

        /// <summary>
        /// Decrease stock in Sales Warehouse when sales invoice is confirmed
        /// Prevents negative stock
        /// </summary>
        public static void DecreaseSalesStock(int productID, decimal quantity, 
            DBTHSNEntities db, string userEmail, long invoiceID, string invoiceNo, DateTime? invoiceDate = null, int? contractID = null)
        {
            using (var transaction = new TransactionScope(TransactionScopeOption.Required))
            {
                try
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
                    var invoiceUrl = $"/SalesInvoices/Details/{invoiceID}";

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

                    db.EXPENSES.Add(expense);
                    db.SaveChanges();

                    // Create EXPENSE_PRODUCTS
                    var expenseProduct = new EXPENSE_PRODUCTS
                    {
                        ExpenseID = expense.ID,
                        ProductID = productID,
                        Amount = qtyToDeduct,
                        PiecePrice = piecePrice,
                        TotalPrice = totalPrice,
                        UnitID = product.UnitID > 0 ? product.UnitID : 1,
                        Comment = $"Invoys raqami: {invoiceNo}. URL: {invoiceUrl}"
                    };

                    db.EXPENSE_PRODUCTS.Add(expenseProduct);
                    db.SaveChanges();

                    var unitIdForIssue = product.UnitID > 0 ? product.UnitID : 1;
                    ExpenseStockIssueHelper.ApplyAutoIssueForExpenseProduct(
                        db, salesWH.ID, productID, qtyToDeduct, unitIdForIssue, expenseProduct.ID);

                    db.SaveChanges();
                    transaction.Complete();

                    LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "DecreaseSalesStock",
                        $"Sotuv invoysi {invoiceNo} uchun mahsulot {product.PNo} dan {qtyToDeduct} miqdor sotuv omboridan chiqarildi. " +
                        $"Chiqim ID: {expense.ID}, Hisob-faktura ID: {invoiceID}");
                }
                catch (Exception ex)
                {
                    LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "DecreaseSalesStock",
                        $"Xatolik: {ex.Message}, Hisob-faktura ID: {invoiceID}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Sales invoice bekor qilinganda:
        /// 1) avtomatik yaratilgan EXPENS hujjatlarini topadi (invoice URL marker orqali),
        /// 2) sotuv ombori qoldig'ini qayta tiklaydi,
        /// 3) EXPENSES/EXPENSE_PRODUCTS/WRHS_EXPENSE_DOCS yozuvlarini fizik o'chiradi.
        /// </summary>
        public static void RollbackSalesInvoiceStockAndDeleteAutoExpenses(long invoiceID, DBTHSNEntities db, string userEmail)
        {
            using (var transaction = new TransactionScope(TransactionScopeOption.Required))
            {
                try
                {
                    var invoiceUrlMarker = $"/SalesInvoices/Details/{invoiceID}";
                    var salesWH = GetSalesWarehouse(db);
                    if (salesWH == null)
                    {
                        throw new Exception("Sotuv ombori topilmadi");
                    }

                    var expenseIdsFromHeader = db.EXPENSES
                        .Where(e => e.Description != null && e.Description.Contains(invoiceUrlMarker))
                        .Select(e => e.ID)
                        .ToList();

                    var expenseIdsFromLines = db.EXPENSE_PRODUCTS
                        .Where(ep => ep.Comment != null && ep.Comment.Contains(invoiceUrlMarker))
                        .Select(ep => ep.ExpenseID)
                        .ToList();

                    var expenseIds = expenseIdsFromHeader
                        .Union(expenseIdsFromLines)
                        .Distinct()
                        .ToList();

                    if (!expenseIds.Any())
                    {
                        LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "RollbackSalesInvoiceStockAndDeleteAutoExpenses",
                            $"Invoice {invoiceID} uchun auto-expense topilmadi.");
                        transaction.Complete();
                        return;
                    }

                    var expenseLines = db.EXPENSE_PRODUCTS
                        .Where(ep => expenseIds.Contains(ep.ExpenseID))
                        .ToList();

                    foreach (var line in expenseLines)
                    {
                        var product = db.PRODUCTS.Find(line.ProductID);
                        var unitId = product?.UnitID > 0 ? product.UnitID : line.UnitID;
                        ExpenseStockIssueHelper.ReverseExpenseProductIssue(
                            db, salesWH.ID, line.ProductID, line.Amount, unitId, line.ID);
                    }

                    var expenseDocs = db.WRHS_EXPENSE_DOCS
                        .Where(d => expenseIds.Contains(d.ExpenseID))
                        .ToList();
                    if (expenseDocs.Any())
                    {
                        db.WRHS_EXPENSE_DOCS.RemoveRange(expenseDocs);
                    }

                    db.EXPENSE_PRODUCTS.RemoveRange(expenseLines);

                    var expenses = db.EXPENSES
                        .Where(e => expenseIds.Contains(e.ID))
                        .ToList();
                    if (expenses.Any())
                    {
                        db.EXPENSES.RemoveRange(expenses);
                    }

                    db.SaveChanges();
                    transaction.Complete();

                    LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "RollbackSalesInvoiceStockAndDeleteAutoExpenses",
                        $"Invoice {invoiceID} uchun {expenseIds.Count} ta auto-expense fizik o'chirildi va stock qaytarildi.");
                }
                catch (Exception ex)
                {
                    LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "RollbackSalesInvoiceStockAndDeleteAutoExpenses",
                        $"Xatolik: {ex.Message}, InvoiceID: {invoiceID}");
                    throw;
                }
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
