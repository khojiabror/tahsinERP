using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Models;
using tahsinERP.Services.Stock;

namespace tahsinERP.Areas.Distribution.Services.Warehouse
{
    /// <summary>
    /// 3-bosqich hujjatlari uchun ombor yozuvlari — mavjud ombor moduli bilan bir xil yo'l (dbo.EXPENSES / INCOMES,
    /// StockLedger, FIFO tannarx). Hammasi chaqiruvchining <see cref="DistributionUnitOfWork"/> tranzaksiyasida.
    /// Miqdorlar tovarning asosiy birligida (PRODUCTS.UnitID).
    /// </summary>
    public static class StockDocs
    {
        public sealed class IssueLine
        {
            public int ProductID { get; set; }
            public decimal QtyBase { get; set; }
            public string Comment { get; set; }
            // natija
            public decimal UnitCost { get; set; }
            public decimal Cost { get; set; }
        }

        public sealed class ReceiptLine
        {
            public int ProductID { get; set; }
            public decimal QtyBase { get; set; }
            public decimal UnitCost { get; set; }
            public string Comment { get; set; }
        }

        /// <summary>(ombor, tovar) qulfi — buyurtma rezervi va jo'natish bilan bir xil resurs nomi.</summary>
        public static void Lock(DistributionDbContext db, int warehouseId, int productId)
        {
            db.Database.ExecuteSqlCommand(System.Data.Entity.TransactionalBehavior.DoNotEnsureTransaction,
                "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @p0, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; " +
                "IF @r < 0 THROW 51002, 'stock lock timeout', 1;", "dst.stock:" + warehouseId + ":" + productId);
        }

        /// <summary>
        /// Rezervdan tashqari (mavjud) qoldiq yetadimi — buyurtmalar rezervidagi tovar hisobdan chiqarilmaydi.
        /// Qulf shu yerda olinadi. Qaytaradi: tovar → o'sha paytdagi haqiqiy qoldiq.
        /// </summary>
        public static Dictionary<int, decimal> LockAndCheckAvailable(DistributionDbContext db, int warehouseId, IEnumerable<(int ProductID, decimal QtyBase)> need)
        {
            var byProduct = need.GroupBy(n => n.ProductID).ToDictionary(g => g.Key, g => g.Sum(x => x.QtyBase));
            foreach (var pid in byProduct.Keys.OrderBy(x => x)) Lock(db, warehouseId, pid);
            var levels = StockService.Levels(db, byProduct.Keys.ToList(), new[] { warehouseId }).ToDictionary(l => l.ProductID);
            var names = db.Products.Where(p => byProduct.Keys.Contains(p.ID)).ToDictionary(p => p.ID, p => p.PNo + " " + p.Name);
            foreach (var kv in byProduct)
            {
                decimal available = levels.TryGetValue(kv.Key, out var l) ? l.Available : 0;
                if (kv.Value > available)
                    throw new DstValidationException(DstLocalization.T("Stock_NotAvailable", names.TryGetValue(kv.Key, out var n) ? n : "#" + kv.Key,
                        Math.Max(0, available).ToString("0.####"), kv.Value.ToString("0.####")));
            }
            return byProduct.Keys.ToDictionary(k => k, k => levels.TryGetValue(k, out var l) ? l.OnHand : 0);
        }

        /// <summary>Ombor chiqimi (EXPENSES, SendStatus = true) FIFO bo'yicha; har qatorga tannarx yoziladi. Qaytaradi: EXPENSES.ID.</summary>
        public static int Issue(DistributionUnitOfWork uow, int warehouseId, string docNo, string description, IList<IssueLine> lines, DateTime now)
        {
            var erp = uow.Erp;
            var productIds = lines.Select(l => l.ProductID).Distinct().ToList();
            InventoryCountService.EnsureNotCounting(uow.Dst, warehouseId, productIds);   // hisobdan chiqarish, pereso'rtitsa
            var baseUnits = uow.Dst.Products.Where(p => productIds.Contains(p.ID)).ToDictionary(p => p.ID, p => p.UnitID);
            var expense = new EXPENS
            {
                DocNo = docNo, SenderWHID = warehouseId, IssueDateTime = now, CurrencyID = WarehouseAccess.DefaultCurrencyId(erp),
                IsDeleted = false, SendStatus = true, Description = description
            };
            erp.EXPENSES.Add(expense);
            erp.SaveChanges();

            decimal totalQty = 0, totalCost = 0;
            foreach (var line in lines)
            {
                var ep = new EXPENSE_PRODUCTS
                {
                    ExpenseID = expense.ID, ProductID = line.ProductID, Amount = line.QtyBase, PiecePrice = 0,
                    UnitID = baseUnits[line.ProductID], Comment = Cut(line.Comment ?? docNo, 250)
                };
                erp.EXPENSE_PRODUCTS.Add(ep);
                erp.SaveChanges();
                try
                {
                    StockLedger.IssueLine(erp, warehouseId, line.ProductID, line.QtyBase, ep.UnitID, ep.ID);
                }
                catch (Exception ex) when (!(ex is DstValidationException))
                {
                    throw new DstValidationException(DstLocalization.T("Stock_WriteError") + " " + ex.Message);
                }
                ep.PiecePrice = StockCostService.StoredUnitCost(
                    StockCostService.IssuedLineUnitCost(erp, warehouseId, line.ProductID, line.QtyBase, ep.UnitID, ep.ID));
                line.UnitCost = ep.PiecePrice;
                line.Cost = Math.Round(line.QtyBase * ep.PiecePrice, 2, MidpointRounding.AwayFromZero);
                totalQty += line.QtyBase;
                totalCost += line.Cost;
            }
            expense.Amount = totalQty;
            expense.TotalPrice = totalCost;
            erp.SaveChanges();
            return expense.ID;
        }

        /// <summary>Ombor kirimi (INCOMES, qabul qilingan) berilgan tannarxda. DLOC omborda <paramref name="locationId"/> majburiy. Qaytaradi: INCOMES.ID.</summary>
        public static int Receive(DistributionUnitOfWork uow, int warehouseId, string docNo, string description, IList<ReceiptLine> lines, int? locationId, DateTime now)
        {
            var erp = uow.Erp;
            var productIds = lines.Select(l => l.ProductID).Distinct().ToList();
            var baseUnits = uow.Dst.Products.Where(p => productIds.Contains(p.ID)).ToDictionary(p => p.ID, p => p.UnitID);
            var income = new INCOME
            {
                DocNo = docNo, ReceivingWHID = warehouseId, IssueDateTime = now, CurrencyID = WarehouseAccess.DefaultCurrencyId(erp),
                IsDeleted = false, RecieveStatus = true, Description = description
            };
            erp.INCOMES.Add(income);
            erp.SaveChanges();
            decimal totalQty = 0, totalCost = 0;
            foreach (var line in lines)
            {
                var ip = new INCOME_PRODUCTS
                {
                    IncomeID = income.ID, ProductID = line.ProductID, Amount = line.QtyBase, PiecePrice = Math.Round(line.UnitCost, 6),
                    UnitID = baseUnits[line.ProductID], Comment = Cut(line.Comment ?? docNo, 250)
                };
                erp.INCOME_PRODUCTS.Add(ip);
                erp.SaveChanges();
                try
                {
                    StockLedger.ReceiveLine(erp, warehouseId, income.ID, ip.ID, line.ProductID, line.QtyBase, ip.UnitID, locationId, now);
                }
                catch (Exception ex) when (!(ex is DstValidationException))
                {
                    throw new DstValidationException(DstLocalization.T("Stock_WriteError") + " " + ex.Message);
                }
                totalQty += line.QtyBase;
                totalCost += Math.Round(line.QtyBase * line.UnitCost, 2, MidpointRounding.AwayFromZero);
            }
            income.Amount = totalQty;
            income.TotalPrice = totalCost;
            erp.SaveChanges();
            return income.ID;
        }

        /// <summary>Chiqimni bekor qilish: qoldiq (va DLOC lotlari) qaytadi, hujjat o'chirilgan deb belgilanadi.</summary>
        public static void ReverseIssue(DistributionUnitOfWork uow, int expenseId, int warehouseId)
        {
            var erp = uow.Erp;
            var expense = erp.EXPENSES.Find(expenseId);
            foreach (var ep in erp.EXPENSE_PRODUCTS.Where(p => p.ExpenseID == expenseId).ToList())
            {
                StockLedger.ReverseIssueLine(erp, warehouseId, ep.ProductID, ep.Amount, ep.UnitID, ep.ID);
                erp.EXPENSE_PRODUCTS.Remove(ep);
            }
            if (expense != null) expense.IsDeleted = true;
            erp.SaveChanges();
        }

        /// <summary>Kirimni bekor qilish: tovar shu kirimdan chiqarilgan bo'lsa — rad.</summary>
        public static void ReverseReceive(DistributionUnitOfWork uow, int incomeId, int warehouseId)
        {
            try
            {
                StockLedger.ReverseIncomeReceipt(uow.Erp, incomeId, warehouseId);
            }
            catch (Exception ex) when (!(ex is DstValidationException))
            {
                throw new DstValidationException(DstLocalization.T("Stock_ReceiptConsumed") + " " + ex.Message);
            }
            var income = uow.Erp.INCOMES.Find(incomeId);
            if (income != null) income.IsDeleted = true;
            uow.Erp.SaveChanges();
        }

        private static string Cut(string s, int max) => s == null ? null : s.Length <= max ? s : s.Substring(0, max);
    }

    /// <summary>
    /// Minimal qoldiq ogohlantirishi: amaldan keyin qoldiq minimaldan (dst.PRODUCT_STOCK_LIMITS) pastga TUSHSA —
    /// rahbarlar va o'sha ombor omborchilariga bir marta xabar (allaqachon past bo'lsa takrorlanmaydi).
    /// </summary>
    public static class StockAlerts
    {
        public const string LowStock = "LOW_STOCK";

        /// <param name="onHandBefore">tovar → amaldan oldingi haqiqiy qoldiq (asosiy birlikda).</param>
        public static void AfterDecrease(DistributionDbContext db, int warehouseId, IReadOnlyDictionary<int, decimal> onHandBefore, DateTime now, int? exceptUserId = null)
        {
            if (onHandBefore == null || onHandBefore.Count == 0) return;
            var ids = onHandBefore.Keys.ToList();
            var limits = db.ProductStockLimits.AsNoTracking().Where(l => l.WarehouseID == warehouseId && ids.Contains(l.ProductID))
                .ToDictionary(l => l.ProductID, l => l.MinQty);
            if (limits.Count == 0) return;
            var after = StockService.Levels(db, limits.Keys.ToList(), new[] { warehouseId }).ToDictionary(l => l.ProductID, l => l.OnHand);
            var crossed = limits.Where(l => onHandBefore[l.Key] >= l.Value && (after.TryGetValue(l.Key, out var q) ? q : 0) < l.Value).ToList();
            if (crossed.Count == 0) return;

            var warehouse = db.Warehouses.Where(w => w.ID == warehouseId).Select(w => w.WHName).FirstOrDefault();
            var crossedIds = crossed.Select(c => c.Key).ToList();
            var names = db.Products.Where(p => crossedIds.Contains(p.ID)).ToDictionary(p => p.ID, p => p.PNo + " " + p.Name);
            Func<string, string> text = lang => DstLocalization.TFor(lang, "Notify_LowStock", warehouse) + Environment.NewLine + string.Join(Environment.NewLine,
                crossed.Select(c => "• " + (names.TryGetValue(c.Key, out var n) ? n : "#" + c.Key) + ": " +
                                    (after.TryGetValue(c.Key, out var q) ? q : 0).ToString("0.##") + " / min " + c.Value.ToString("0.##")));
            NotificationService.ToManagers(db, LowStock, text, null, now, exceptUserId);
            NotificationService.ToStorekeepers(db, warehouseId, LowStock, text, null, now, exceptUserId);
        }
    }
}
