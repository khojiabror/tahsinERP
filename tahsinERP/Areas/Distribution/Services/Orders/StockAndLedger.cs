using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;

namespace tahsinERP.Areas.Distribution.Services.Orders
{
    /// <summary>Ombor qoldig'i: haqiqiy (dbo.PRODUCT_STOCKS), rezerv (dst.STOCK_RESERVATIONS), mavjud = haqiqiy − rezerv.</summary>
    public sealed class StockLevel
    {
        public int WarehouseID { get; set; }
        public int ProductID { get; set; }
        public decimal OnHand { get; set; }
        public decimal Reserved { get; set; }
        public decimal Available => OnHand - Reserved;
    }

    /// <summary>
    /// Qoldiqlar asosiy birlikda (PRODUCTS.UnitID). Ombor hisobi ham shu birlikda yuritiladi deb olinadi
    /// (PRODUCT_STOCKS.UnitID boshqa bo'lsa — ombor ledgeri chiqimda o'zi o'giradi, bu hisobot esa taxminiy).
    /// </summary>
    public static class StockService
    {
        public static List<StockLevel> Levels(DistributionDbContext db, IReadOnlyCollection<int> productIds, IReadOnlyCollection<int> warehouseIds = null)
        {
            var ids = productIds.Distinct().ToList();
            var wh = warehouseIds?.Distinct().ToList();
            var stocks = db.Stocks.AsNoTracking().Where(s => ids.Contains(s.ProductID));
            var reservations = db.StockReservations.AsNoTracking().Where(r => ids.Contains(r.ProductID));
            if (wh != null)
            {
                stocks = stocks.Where(s => wh.Contains(s.WHID));
                reservations = reservations.Where(r => wh.Contains(r.WarehouseID));
            }
            var onHand = stocks
                .GroupBy(s => new { s.WHID, s.ProductID })
                .Select(g => new { g.Key.WHID, g.Key.ProductID, Qty = g.Sum(x => x.Amount) })
                .ToList();
            var reserved = reservations
                .GroupBy(r => new { r.WarehouseID, r.ProductID })
                .Select(g => new { g.Key.WarehouseID, g.Key.ProductID, Qty = g.Sum(x => x.QtyBase) })
                .ToList();

            var result = onHand.ToDictionary(x => (x.WHID, x.ProductID),
                x => new StockLevel { WarehouseID = x.WHID, ProductID = x.ProductID, OnHand = x.Qty });
            foreach (var r in reserved)
            {
                if (!result.TryGetValue((r.WarehouseID, r.ProductID), out var level))
                    result[(r.WarehouseID, r.ProductID)] = level = new StockLevel { WarehouseID = r.WarehouseID, ProductID = r.ProductID };
                level.Reserved = r.Qty;
            }
            return result.Values.ToList();
        }

        /// <summary>
        /// Buyurtma uchun ombor taklifi: savdo omborlari ichidan hamma qatorni to'liq qoplaydigani, bo'lmasa —
        /// eng ko'p qoplaydigani (qoplash ulushi bo'yicha). Hech biri bo'lmasa null.
        /// </summary>
        public static int? SuggestWarehouse(IEnumerable<StockLevel> levels, IReadOnlyDictionary<int, decimal> needByProduct,
            IReadOnlyCollection<int> candidateWarehouses)
        {
            if (candidateWarehouses == null || candidateWarehouses.Count == 0 || needByProduct.Count == 0) return null;
            var byWh = levels.ToLookup(l => l.WarehouseID);
            decimal totalNeed = needByProduct.Values.Sum();
            return candidateWarehouses
                .Select(wh => new
                {
                    wh,
                    Covered = needByProduct.Sum(n => Math.Min(n.Value, Math.Max(0, byWh[wh].Where(l => l.ProductID == n.Key).Sum(l => l.Available)))),
                    Full = needByProduct.All(n => byWh[wh].Where(l => l.ProductID == n.Key).Sum(l => l.Available) >= n.Value)
                })
                .Where(x => x.Covered > 0)
                .OrderByDescending(x => x.Full).ThenByDescending(x => x.Covered / totalNeed).ThenBy(x => x.wh)
                .Select(x => (int?)x.wh)
                .FirstOrDefault();
        }

        /// <summary>Savdoda ishlatiladigan omborlar (WAREHOUSE_SETTINGS.IsSalesPoint; sozlanmagan bo'lsa — hammasi).</summary>
        public static List<int> SalesWarehouses(DistributionDbContext db)
        {
            var configured = db.WarehouseSettings.AsNoTracking().ToList();
            var all = db.Warehouses.Where(w => w.IsDeleted != true).Select(w => w.ID).ToList();
            if (configured.Count == 0) return all;
            return all.Where(id => configured.Any(c => c.WarehouseID == id && c.IsSalesPoint)).ToList();
        }
    }

    /// <summary>Mijoz qarzi (dst.CUSTOMER_LEDGER).</summary>
    public static class LedgerService
    {
        public static CreditState CreditState(DistributionDbContext db, int customerId, DateTime today)
        {
            var profile = db.CustomerProfiles.AsNoTracking().FirstOrDefault(p => p.CustomerID == customerId);
            var rows = db.CustomerLedger.Where(l => l.CustomerID == customerId);
            decimal debit = rows.Sum(l => (decimal?)l.Debit) ?? 0;
            decimal credit = rows.Sum(l => (decimal?)l.Credit) ?? 0;
            var day = today.Date;
            decimal overdueDebit = rows.Where(l => l.DueDate != null && l.DueDate < day).Sum(l => (decimal?)l.Debit) ?? 0;
            return new CreditState
            {
                HasProfile = profile != null,
                CreditLimit = profile?.CreditLimit ?? 0,
                Balance = debit - credit,
                Overdue = OrderRules.Overdue(overdueDebit, credit)
            };
        }

        public static CustomerLedgerEntry Debit(DistributionDbContext db, int customerId, string docType, int docId, int? orderId,
            decimal amount, DateTime at, int paymentDays, int? agentUserId, int? userId, string note)
        {
            var entry = new CustomerLedgerEntry
            {
                CustomerID = customerId, EntryDate = at, DocType = docType, DocID = docId, OrderID = orderId,
                Debit = amount, DueDate = at.Date.AddDays(Math.Max(0, paymentDays)), AgentUserID = agentUserId,
                CreatedBy = userId, Note = note
            };
            db.CustomerLedger.Add(entry);
            return entry;
        }
    }

    /// <summary>Distributsiya hujjatlari raqami: PREFIKS-YYYY-00001 (tranzaksiya ichida, sp_getapplock bilan).</summary>
    public static class DstNumbering
    {
        private static readonly string[] Tables = { "D_ORDERS", "D_SHIPMENTS", "D_RETURNS", "D_PAYMENTS" };

        public static string Next(DistributionDbContext db, string table, string prefix, DateTime date, int pad = 5)
        {
            if (!Tables.Contains(table)) throw new ArgumentOutOfRangeException(nameof(table));
            if (db.Database.CurrentTransaction == null)
                throw new InvalidOperationException("Hujjat raqami faqat tranzaksiya ichida olinadi.");

            string full = prefix + "-" + date.Year + "-";
            db.Database.ExecuteSqlCommand(System.Data.Entity.TransactionalBehavior.DoNotEnsureTransaction,
                "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @p0, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; " +
                "IF @r < 0 THROW 51001, 'docno lock timeout', 1;", "dst.docno:" + table + ":" + full);
            long? max = db.Database.SqlQuery<long?>(
                "SELECT MAX(TRY_CAST(SUBSTRING(DocNo, @p0, 30) AS bigint)) FROM dst.[" + table + "] WHERE DocNo LIKE @p1",
                full.Length + 1, full.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%").FirstOrDefault();
            return full + ((max ?? 0) + 1).ToString().PadLeft(pad, '0');
        }
    }
}
