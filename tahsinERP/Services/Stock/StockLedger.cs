using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Models;
using tahsinERP.Services.DlocFifo;

namespace tahsinERP.Services.Stock
{
    /// <summary>
    /// Qoldiq yetarli bo'lmaganda (manfiy qoldiqqa olib keladigan harakat) tashlanadi.
    /// Avval qoldiq jimgina 0 ga "clamp" qilinardi — bu oversell'ni yashirardi (SRM-B01).
    /// </summary>
    public class InsufficientStockException : InvalidOperationException
    {
        public int WarehouseId { get; }
        public int ProductId { get; }
        public double Requested { get; }
        public double Available { get; }

        public InsufficientStockException(int warehouseId, int productId, double requested, double available)
            : base($"Omborda yetarli qoldiq yo'q (ombor #{warehouseId}, mahsulot #{productId}). " +
                   $"Mavjud: {available:N3}, talab: {requested:N3}.")
        {
            WarehouseId = warehouseId;
            ProductId = productId;
            Requested = requested;
            Available = available;
        }
    }

    /// <summary>
    /// Ombor qoldig'ini o'zgartiradigan YAGONA joy (SRM, MES, CRM uchun umumiy API).
    ///
    /// Qoidalar:
    ///  1. <c>PRODUCT_STOCKS.Amount</c> faqat <see cref="Adjust"/> orqali o'zgaradi — bitta atomik SQL
    ///     (UPDLOCK+HOLDLOCK), shuning uchun parallel harakatlar bir-birini yo'qotmaydi; dublikat
    ///     (WHID, ProductID) qatorlari shu yerning o'zida bittaga birlashtiriladi.
    ///  2. Manfiy qoldiq hech qachon yozilmaydi: <see cref="InsufficientStockException"/> tashlanadi.
    ///  3. Hujjat (header + qatorlar) va qoldiq bitta tranzaksiyada bo'lishi kerak. Chaqiruvchi
    ///     <c>db.Database.BeginTransaction()</c> ochadi; <see cref="InTransaction"/> yordamchisi
    ///     mavjud tranzaksiyani qayta ishlatadi yoki yangisini ochadi. SQL buyruqlar darhol bajariladi
    ///     (SaveChanges'ni kutmaydi), shuning uchun tranzaksiyasiz chaqirish xato bo'ladi.
    ///  4. DLOC (joylashuv/lot) yoqilgan omborlarda qabul/chiqim/teskari amallar ledger
    ///     (INCOME_LOTS, LOT_LOCATIONS, EXPENSE_PRODUCT_ALLOCATIONS) bilan birga yuritiladi;
    ///     chaqiruvchi qaysi yo'lni tanlashni o'ylamaydi.
    ///
    /// Asosiy amallar:
    ///  - <see cref="ReceiveLine"/> — kirim qatorini omborga qabul qilish (DLOC bo'lsa lot + joy);
    ///  - <see cref="IssueLineAsync"/> / <see cref="IssueLine"/> — chiqim qatori (DLOC FIFO, keyin joylashtirilmagan qoldiq);
    ///  - <see cref="ReverseIssueLine"/> — chiqim qatorini bekor qilish (lot ajratmalari tiklanadi);
    ///  - <see cref="ReverseIncomeReceipt"/> — qabul qilingan kirimni bekor qilish (lotdan foydalanilgan bo'lsa — xato);
    ///  - <see cref="GetOnHand"/> — umumiy qoldiq (yagona haqiqat manbai).
    /// </summary>
    public static class StockLedger
    {
        /// <summary>Suzuvchi nuqta xatolari uchun chegara (bundan kichik farq "nol" hisoblanadi).</summary>
        public const double Epsilon = 1e-6;

        private class AdjustResult
        {
            public bool Ok { get; set; }
            public double Available { get; set; }
        }

        // @p0 WHID, @p1 ProductID, @p2 delta, @p3 UnitID, @p4 epsilon
        private const string AdjustSql = @"
SET NOCOUNT ON;
DECLARE @keep INT, @cnt INT, @total FLOAT, @new FLOAT, @ok BIT = 1;
BEGIN TRAN;
SELECT @keep = MIN(ID), @cnt = COUNT(*), @total = ISNULL(SUM(Amount), 0)
FROM dbo.PRODUCT_STOCKS WITH (UPDLOCK, HOLDLOCK)
WHERE WHID = @p0 AND ProductID = @p1;
SET @new = @total + @p2;
IF @new < -@p4
    SET @ok = 0;
ELSE
BEGIN
    IF @new < 0 SET @new = 0;
    IF @keep IS NULL
        INSERT INTO dbo.PRODUCT_STOCKS (WHID, ProductID, Amount, UnitID) VALUES (@p0, @p1, @new, @p3);
    ELSE
    BEGIN
        IF @cnt > 1
            DELETE FROM dbo.PRODUCT_STOCKS WHERE WHID = @p0 AND ProductID = @p1 AND ID <> @keep;
        UPDATE dbo.PRODUCT_STOCKS
        SET Amount = @new, UnitID = CASE WHEN UnitID IS NULL OR UnitID <= 0 THEN @p3 ELSE UnitID END
        WHERE ID = @keep;
    END
END
COMMIT;
SELECT @ok AS Ok, CASE WHEN @ok = 1 THEN @new ELSE @total END AS Available;";

        /// <summary>
        /// PRODUCT_STOCKS qoldig'ini atomik o'zgartiradi (musbat — kirim, manfiy — chiqim).
        /// Natija manfiy bo'lsa hech narsa yozilmaydi va <see cref="InsufficientStockException"/> tashlanadi.
        /// DLOC ledger'ga tegmaydi — DLOC omborlar uchun <see cref="ReceiveLine"/>/<see cref="IssueLine"/> ishlating.
        /// </summary>
        /// <returns>Yangi umumiy qoldiq.</returns>
        public static double Adjust(DBTHSNEntities db, int warehouseId, int productId, double delta, int unitId = 0)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (Math.Abs(delta) < 1e-12)
                return GetOnHand(db, warehouseId, productId);

            var r = db.Database.SqlQuery<AdjustResult>(AdjustSql,
                warehouseId, productId, delta, unitId > 0 ? unitId : 1, Epsilon).Single();

            if (!r.Ok)
                throw new InsufficientStockException(warehouseId, productId, -delta, r.Available);
            return r.Available;
        }

        /// <summary>Kirim: qoldiqni oshiradi (faqat PRODUCT_STOCKS).</summary>
        public static double Increase(DBTHSNEntities db, int warehouseId, int productId, double qty, int unitId = 0)
        {
            if (qty < 0) throw new ArgumentOutOfRangeException(nameof(qty));
            return Adjust(db, warehouseId, productId, qty, unitId);
        }

        /// <summary>Chiqim: qoldiqni kamaytiradi (faqat PRODUCT_STOCKS), yetmasa — xato.</summary>
        public static double Decrease(DBTHSNEntities db, int warehouseId, int productId, double qty, int unitId = 0)
        {
            if (qty < 0) throw new ArgumentOutOfRangeException(nameof(qty));
            return Adjust(db, warehouseId, productId, -qty, unitId);
        }

        /// <summary>
        /// Umumiy qoldiq (PRODUCT_STOCKS, dublikat qatorlar yig'indisi) — yagona haqiqat manbai.
        /// <paramref name="lockForUpdate"/> = true bo'lsa, tranzaksiya oxirigacha qator qulflanadi
        /// (o'qib, keyin shu qiymat asosida yoziladigan holatlar uchun, masalan inventarizatsiya).
        /// </summary>
        public static double GetOnHand(DBTHSNEntities db, int warehouseId, int productId, bool lockForUpdate = false)
        {
            var sql = lockForUpdate
                ? "SELECT CAST(ISNULL(SUM(Amount), 0) AS FLOAT) FROM dbo.PRODUCT_STOCKS WITH (UPDLOCK, HOLDLOCK) WHERE WHID = @p0 AND ProductID = @p1"
                : "SELECT CAST(ISNULL(SUM(Amount), 0) AS FLOAT) FROM dbo.PRODUCT_STOCKS WHERE WHID = @p0 AND ProductID = @p1";
            return db.Database.SqlQuery<double>(sql, warehouseId, productId).FirstOrDefault();
        }

        /// <summary>Bir nechta mahsulot qoldig'i bitta so'rovda (ProductID → qoldiq).</summary>
        public static Dictionary<int, double> GetOnHand(DBTHSNEntities db, int warehouseId, IEnumerable<int> productIds)
        {
            var ids = (productIds ?? Enumerable.Empty<int>()).Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, double>();

            return db.PRODUCT_STOCKS.AsNoTracking()
                .Where(s => s.WHID == warehouseId && ids.Contains(s.ProductID))
                .GroupBy(s => s.ProductID)
                .Select(g => new { g.Key, Qty = g.Sum(x => x.Amount) })
                .ToDictionary(x => x.Key, x => x.Qty);
        }

        /// <summary>Omborda DLOC (joylashuv/lot) yuritiladimi.</summary>
        public static bool IsDlocWarehouse(DBTHSNEntities db, int warehouseId)
        {
            return DlocFifoSchema.IsEnabled(db) && DlocFifoDataAccess.WarehouseHasLocations(db, warehouseId);
        }

        /// <summary>
        /// Kirim qatorini omborga qabul qiladi. DLOC omborda <paramref name="locationId"/> majburiy:
        /// lot yaratiladi, joy sig'imi tekshiriladi, ledger va PRODUCT_STOCKS birga yangilanadi.
        /// Oddiy omborda faqat PRODUCT_STOCKS oshadi.
        /// </summary>
        public static void ReceiveLine(DBTHSNEntities db, int warehouseId, int incomeId, int incomeProductId,
            int productId, double qty, int unitId, int? locationId, DateTime receivedAt, string lotNoPrefix = null)
        {
            if (qty <= Epsilon)
                return;

            if (IsDlocWarehouse(db, warehouseId))
            {
                if (!locationId.HasValue || locationId.Value <= 0)
                    throw new WarehouseDlocException("DLOC_REQUIRED", "Joylashuv (DLOC) tanlanmagan.");

                if (!LocationLotStockLedger.TryReceive(db, warehouseId, incomeId, incomeProductId, productId,
                        (decimal)qty, locationId.Value, receivedAt, unitId, lotNoPrefix))
                {
                    throw new WarehouseDlocException("DLOC_RECEIVE_FAILED",
                        $"Mahsulot #{productId}: DLOC ga kirim yozib bo'lmadi.");
                }
                return;
            }

            Increase(db, warehouseId, productId, qty, unitId);
        }

        /// <summary>
        /// Chiqim qatorini ombordan chiqaradi: DLOC omborda avval FIFO lotlardan (ajratmalar
        /// EXPENSE_PRODUCT_ALLOCATIONS ga yoziladi), yetmasa joylashtirilmagan qoldiqdan; oddiy omborda
        /// PRODUCT_STOCKS dan. Qoldiq yetmasa — xato (hech narsa yozilmaydi).
        /// </summary>
        public static Task IssueLineAsync(DBTHSNEntities db, int warehouseId, int productId, double qty,
            int unitId, int expenseProductId)
        {
            return ExpenseStockIssueHelper.ApplyAutoIssueForExpenseProductAsync(
                db, warehouseId, productId, qty, unitId, expenseProductId);
        }

        /// <summary>Sinxron variant (<see cref="IssueLineAsync"/>).</summary>
        public static void IssueLine(DBTHSNEntities db, int warehouseId, int productId, double qty,
            int unitId, int expenseProductId)
        {
            ExpenseStockIssueHelper.ApplyAutoIssueForExpenseProduct(
                db, warehouseId, productId, qty, unitId, expenseProductId);
        }

        /// <summary>
        /// Chiqim qatorini bekor qiladi: DLOC ajratmalari (lot/joy) tiklanadi, ajratma yozuvlari
        /// o'chiriladi va PRODUCT_STOCKS qaytariladi. EXPENSE_PRODUCTS qatorini o'chirishdan OLDIN chaqiring.
        /// </summary>
        public static void ReverseIssueLine(DBTHSNEntities db, int warehouseId, int productId, double qty,
            int unitId, int expenseProductId)
        {
            ExpenseStockIssueHelper.ReverseExpenseProductIssue(db, warehouseId, productId, qty, unitId, expenseProductId);
        }

        private class LotRow
        {
            public int ID { get; set; }
            public int ProductID { get; set; }
            public string LotNo { get; set; }
            public decimal QtyReceived { get; set; }
            public decimal QtyConsumed { get; set; }
            public decimal QtyAtLocations { get; set; }
        }

        private class IncomeLineRow
        {
            public int ProductID { get; set; }
            public double Amount { get; set; }
            public int UnitID { get; set; }
        }

        /// <summary>
        /// Qabul qilingan kirimni ombordan to'liq qaytaradi (o'chirish/bekor qilish uchun):
        /// DLOC lotlari o'chirilgan deb belgilanadi (lotdan qisman bo'lsa ham foydalanilgan bo'lsa —
        /// <see cref="WarehouseDlocException"/> "LOT_CONSUMED"), so'ng har qator miqdori PRODUCT_STOCKS dan ayiriladi
        /// (qoldiq yetmasa — <see cref="InsufficientStockException"/>).
        /// Faqat <c>RecieveStatus == true</c> bo'lgan kirim uchun chaqiring — qabul qilinmagan kirim qoldiqqa tegmagan.
        /// </summary>
        public static void ReverseIncomeReceipt(DBTHSNEntities db, int incomeId, int warehouseId)
        {
            if (DlocFifoSchema.IsEnabled(db))
            {
                const string lotsSql = @"
SELECT il.ID, il.ProductID, il.LotNo, il.QtyReceived, il.QtyConsumed,
       ISNULL((SELECT SUM(ll.QtyAvailable) FROM dbo.LOT_LOCATIONS ll WHERE ll.LotID = il.ID), 0) AS QtyAtLocations
FROM dbo.INCOME_LOTS il WITH (UPDLOCK, HOLDLOCK)
WHERE il.IncomeID = @p0 AND il.IsDeleted = 0";
                var lots = db.Database.SqlQuery<LotRow>(lotsSql, incomeId).ToList();

                foreach (var lot in lots)
                {
                    if (lot.QtyConsumed > 0.0001m || lot.QtyAtLocations + 0.0001m < lot.QtyReceived)
                    {
                        throw new WarehouseDlocException("LOT_CONSUMED",
                            $"Lot {lot.LotNo} dan allaqachon chiqim qilingan ({lot.QtyReceived - lot.QtyAtLocations:N3}). " +
                            "Avval shu lotdan qilingan chiqimlarni bekor qiling.");
                    }

                    db.Database.ExecuteSqlCommand("DELETE FROM dbo.LOT_LOCATIONS WHERE LotID = @p0", lot.ID);
                    db.Database.ExecuteSqlCommand(
                        "UPDATE dbo.INCOME_LOTS SET IsDeleted = 1, QtyConsumed = QtyReceived WHERE ID = @p0", lot.ID);
                }
            }

            var lines = db.Database.SqlQuery<IncomeLineRow>(
                "SELECT ProductID, Amount, UnitID FROM dbo.INCOME_PRODUCTS WHERE IncomeID = @p0", incomeId).ToList();

            foreach (var g in lines.GroupBy(l => l.ProductID))
            {
                var qty = g.Sum(x => x.Amount);
                if (qty > Epsilon)
                    Decrease(db, warehouseId, g.Key, qty, g.First().UnitID);
            }
        }

        /// <summary>
        /// Amalni tranzaksiya ichida bajaradi: chaqiruvchida tranzaksiya (BeginTransaction yoki
        /// TransactionScope) bo'lsa — o'shani ishlatadi, aks holda o'zi ochadi va commit qiladi.
        /// </summary>
        public static void InTransaction(DBTHSNEntities db, Action body)
        {
            if (db.Database.CurrentTransaction != null || System.Transactions.Transaction.Current != null)
            {
                body();
                return;
            }

            using (var tx = db.Database.BeginTransaction())
            {
                body();
                tx.Commit();
            }
        }
    }
}
