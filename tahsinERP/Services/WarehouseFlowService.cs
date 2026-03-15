using System;
using System.Data.Entity;
using System.Linq;
using System.Transactions;
using tahsinERP.Models;
using tahsinERP.Controllers;

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

        /// <summary>
        /// Increase stock in Production Warehouse for finished goods
        /// Called when production is completed
        /// </summary>
        public static void IncreaseProductionStock(int productID, double quantity, int shopID, 
            DBTHSNEntities db, string userEmail, int? productionID = null)
        {
            using (var transaction = new TransactionScope(TransactionScopeOption.Required))
            {
                try
                {
                    var product = db.PRODUCTS.Find(productID);
                    if (product == null)
                        throw new Exception($"Mahsulot topilmadi: {productID}");

                    // Only process if it's a finished good
                    if (product.IsFinishedGood != true)
                    {
                        LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "IncreaseProductionStock",
                            $"Mahsulot {product.PNo} tayyor mahsulot emas, o'tkazib yuborildi");
                        return;
                    }

                    var productionWH = GetProductionWarehouse(db, shopID);
                    if (productionWH == null)
                        throw new Exception("Ishlab chiqarish ombori topilmadi");

                    int currencyId = GetDefaultCurrencyId(db);
                    // Create INCOME document
                    var income = new INCOME
                    {
                        DocNo = $"PROD-{DateTime.Now:yyyyMMddHHmmss}-{productID}",
                        ReceivingWHID = productionWH.ID,
                        IssueDateTime = DateTime.Now,
                        CurrencyID = currencyId,
                        IsDeleted = false,
                        RecieveStatus = true,
                        Description = $"Ishlab chiqarishdan tayyor mahsulot: {product.PNo} - {product.Name}"
                    };

                    db.INCOMES.Add(income);
                    db.SaveChanges(); // Save to get ID

                    // Create INCOME_PRODUCTS
                    var incomeProduct = new INCOME_PRODUCTS
                    {
                        IncomeID = income.ID,
                        ProductID = productID,
                        Amount = quantity,
                        PiecePrice = 0 // Production cost calculated separately
                    };

                    db.INCOME_PRODUCTS.Add(incomeProduct);

                    // Update or create PRODUCT_STOCKS
                    var stock = db.PRODUCT_STOCKS
                        .FirstOrDefault(s => s.WHID == productionWH.ID && s.ProductID == productID);

                    if (stock != null)
                    {
                        stock.Amount += quantity;
                        db.Entry(stock).State = EntityState.Modified;
                    }
                    else
                    {
                        var newStock = new PRODUCT_STOCKS
                        {
                            WHID = productionWH.ID,
                            ProductID = productID,
                            Amount = quantity,
                            UnitID = product.UnitID
                        };
                        db.PRODUCT_STOCKS.Add(newStock);
                    }

                    db.SaveChanges();
                    transaction.Complete();

                    LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "IncreaseProductionStock",
                        $"Tayyor mahsulot {product.PNo} uchun {quantity} miqdor ishlab chiqarish omboriga qo'shildi. Kirim ID: {income.ID}, Ishlab chiqarish ID: {productionID}");
                }
                catch (Exception ex)
                {
                    LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "IncreaseProductionStock",
                        $"Xatolik: {ex.Message}");
                    throw;
                }
            }
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

                if (product.IsFinishedGood != true)
                {
                    LogHelper.LogToDatabase(userEmail, "WarehouseFlowService", "IncreaseProductionStockWithoutIncome",
                        $"Mahsulot {product.PNo} tayyor mahsulot emas, o'tkazib yuborildi");
                    return;
                }

                var productionWH = GetProductionWarehouse(db, shopID);
                if (productionWH == null)
                    throw new Exception("Ishlab chiqarish ombori topilmadi");

                var stock = db.PRODUCT_STOCKS
                    .FirstOrDefault(s => s.WHID == productionWH.ID && s.ProductID == productID);

                if (stock != null)
                {
                    stock.Amount += quantity;
                    db.Entry(stock).State = EntityState.Modified;
                }
                else
                {
                    var newStock = new PRODUCT_STOCKS
                    {
                        WHID = productionWH.ID,
                        ProductID = productID,
                        Amount = quantity,
                        UnitID = product.UnitID
                    };
                    db.PRODUCT_STOCKS.Add(newStock);
                }

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
                .Where(e => e.DocNo.StartsWith(prefix))
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

                    // Check stock availability in Production WH
                    var productionStock = db.PRODUCT_STOCKS
                        .FirstOrDefault(s => s.WHID == productionWH.ID && s.ProductID == productID);

                    if (productionStock == null || productionStock.Amount < quantity)
                    {
                        throw new Exception(
                            $"Ishlab chiqarish omborida yetarli mahsulot yo'q. " +
                            $"Mavjud: {productionStock?.Amount ?? 0}, Talab qilinmoqda: {quantity}");
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

                    // Decrease Production WH stock
                    productionStock.Amount -= quantity;
                    if (productionStock.Amount < 0)
                        productionStock.Amount = 0; // Safety check
                    db.Entry(productionStock).State = EntityState.Modified;

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
                        Description = $"Ishlab chiqarishdan sotuv omboriga o'tkazma: {product.PNo}. EXPENS ID: {expense.ID}"
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
        /// Decrease stock in Sales Warehouse when sales invoice is confirmed
        /// Prevents negative stock
        /// </summary>
        public static void DecreaseSalesStock(int productID, decimal quantity, 
            DBTHSNEntities db, string userEmail, long invoiceID, string invoiceNo)
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

                    // Get current stock
                    var salesStock = db.PRODUCT_STOCKS
                        .FirstOrDefault(s => s.WHID == salesWH.ID && s.ProductID == productID);

                    double currentStock = salesStock?.Amount ?? 0;
                    double qtyToDeduct = (double)quantity;

                    // Prevent negative stock
                    if (currentStock < qtyToDeduct)
                    {
                        throw new Exception(
                            $"Sotuv omborida yetarli mahsulot yo'q. " +
                            $"Mahsulot: {product.PNo}, Mavjud: {currentStock}, Talab qilinmoqda: {qtyToDeduct}, " +
                            $"Invoys: {invoiceNo}");
                    }

                    int currencyId = GetDefaultCurrencyId(db);
                    // Create EXPENS document
                    var expense = new EXPENS
                    {
                        DocNo = $"SALES-{invoiceNo}-{DateTime.Now:yyyyMMddHHmmss}",
                        SenderWHID = salesWH.ID,
                        IssueDateTime = DateTime.Now,
                        CurrencyID = currencyId,
                        IsDeleted = false,
                        SendStatus = true,
                        Description = $"Sotuv invoysi: {invoiceNo}, Mahsulot: {product.PNo}"
                    };

                    db.EXPENSES.Add(expense);
                    db.SaveChanges();

                    // Create EXPENSE_PRODUCTS
                    var expenseProduct = new EXPENSE_PRODUCTS
                    {
                        ExpenseID = expense.ID,
                        ProductID = productID,
                        Amount = qtyToDeduct,
                        PiecePrice = 0
                    };

                    db.EXPENSE_PRODUCTS.Add(expenseProduct);

                    // Decrease stock
                    if (salesStock != null)
                    {
                        salesStock.Amount -= qtyToDeduct;
                        if (salesStock.Amount < 0)
                            salesStock.Amount = 0; // Safety check
                        db.Entry(salesStock).State = EntityState.Modified;
                    }
                    else
                    {
                        throw new Exception($"Sotuv omborida mahsulot qoldig'i topilmadi: {product.PNo}");
                    }

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
        /// Get current stock in Sales Warehouse for a product
        /// </summary>
        public static double GetSalesStock(int productID, DBTHSNEntities db)
        {
            var salesWH = GetSalesWarehouse(db);
            if (salesWH == null)
                return 0;

            var stock = db.PRODUCT_STOCKS
                .FirstOrDefault(s => s.WHID == salesWH.ID && s.ProductID == productID);

            return stock?.Amount ?? 0;
        }

        /// <summary>
        /// Get current stock in Production Warehouse for a product
        /// </summary>
        public static double GetProductionStock(int productID, DBTHSNEntities db, int? shopID = null)
        {
            var productionWH = GetProductionWarehouse(db, shopID);
            if (productionWH == null)
                return 0;

            var stock = db.PRODUCT_STOCKS
                .FirstOrDefault(s => s.WHID == productionWH.ID && s.ProductID == productID);

            return stock?.Amount ?? 0;
        }
    }
}
