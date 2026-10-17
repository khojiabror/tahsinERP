using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Transactions;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services.DlocFifo;
using tahsinERP.ViewModels.Sales;

namespace tahsinERP.Services.Crm
{
    /// <summary>Sotuv invoysi holatlari (avval "CANCELED"/"CANCELLED" 6+ joyda qo'lda yozilgan).</summary>
    public static class SalesInvoiceStatus
    {
        public const string Approved = "APPROVED";
        public const string Canceled = "CANCELED";
        public const string CanceledAlt = "CANCELLED";
        public const string Paid = "PAID";
        public const string Finalized = "FINALIZED";

        public static bool IsCanceled(string status) => status == Canceled || status == CanceledAlt;

        /// <summary>Tahrirlab bo'lmaydigan holat.</summary>
        public static bool IsLocked(string status) => status == Finalized || status == Paid || IsCanceled(status);
    }

    /// <summary>"Faol invoys" filtri — hisobot, dashboard va ro'yxatlar uchun yagona joy.</summary>
    public static class SalesInvoiceQueries
    {
        public static IQueryable<SALES_INVOICES> Active(this IQueryable<SALES_INVOICES> query)
        {
            return query.Where(i => i.StatusCode == null || (i.StatusCode != "CANCELED" && i.StatusCode != "CANCELLED"));
        }

        public static IQueryable<SALES_INVOICE_LINES> Active(this IQueryable<SALES_INVOICE_LINES> query)
        {
            return query.Where(l => l.SALES_INVOICES.StatusCode == null
                || (l.SALES_INVOICES.StatusCode != "CANCELED" && l.SALES_INVOICES.StatusCode != "CANCELLED"));
        }
    }

    /// <summary>Foydalanuvchiga ko'rsatiladigan biznes-qoida xatosi (ModelState'ga yoziladi).</summary>
    public sealed class SalesInvoiceException : Exception
    {
        public SalesInvoiceException(string message, bool notFound = false) : base(message)
        {
            NotFound = notFound;
        }

        public bool NotFound { get; }
    }

    /// <summary>
    /// Sotuv invoysini yaratish/tahrirlash/bekor qilish (CRM-B01, B02, B03; D6–D8).
    ///
    /// Sarlavha + qatorlar + ombor chiqimi (<see cref="WarehouseFlowService.DecreaseSalesStock"/>) +
    /// finance posting bitta tashqi <see cref="TransactionScope"/> ichida bajariladi: biror qadam
    /// yiqilsa — hammasi bekor qilinadi (avval invoys, qatorlar va qisman chiqim commit bo'lib qolardi,
    /// ombor va posting xatolari esa yutilardi).
    ///
    /// Nega TransactionScope (db.Database.BeginTransaction emas): WarehouseFlowService ombor amallarini
    /// o'zining <c>TransactionScope(Required)</c>'ida bajaradi — u tashqi ambient tranzaksiyaga qo'shiladi,
    /// lokal (BeginTransaction) tranzaksiya bilan esa SqlConnection enlist qila olmaydi.
    /// Shu sababli invoys raqami uchun qulf ham ambient tranzaksiya ichida olinadi (DocumentNumberService
    /// faqat db.Database.CurrentTransaction bilan ishlaydi).
    /// </summary>
    public static class SalesInvoiceService
    {
        private const string DefaultPrefix = "INV_";
        private const int DefaultPad = 4;

        private const string NumberLockSql =
            "DECLARE @r int; " +
            "EXEC @r = sp_getapplock @Resource = @p0, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; " +
            "IF @r < 0 THROW 50001, N'Invoys raqami uchun qulf olinmadi, qayta urinib ko''ring.', 1;";

        // ───────────────────────────── Raqamlash (CRM-B02) ─────────────────────────────

        /// <summary>Formada ko'rsatish uchun taxminiy keyingi raqam (qulfsiz; haqiqiy raqam saqlashda beriladi).</summary>
        public static string SuggestNextInvoiceNo(DBTHSNEntities db)
        {
            return ComputeNextInvoiceNo(db);
        }

        /// <summary>
        /// Takrorlanmaydigan keyingi raqam. Ambient tranzaksiya (TransactionScope) ichida chaqiriladi:
        /// sp_getapplock tranzaksiya oxirigacha ushlanadi, shuning uchun raqam hisoblash + INSERT atomik.
        /// Bekor qilingan invoyslar ham hisobga olinadi (avval ularning raqami qayta berilardi).
        /// </summary>
        public static string NextInvoiceNo(DBTHSNEntities db)
        {
            if (Transaction.Current == null && db.Database.CurrentTransaction == null)
                throw new InvalidOperationException("NextInvoiceNo tranzaksiya ichida chaqirilishi kerak.");

            db.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, NumberLockSql,
                "docno:SALES_INVOICES.InvoiceNo");
            return ComputeNextInvoiceNo(db);
        }

        /// <summary>
        /// Prefiks va raqam uzunligi oxirgi invoysdan olinadi (mavjud raqamlash uslubi saqlanadi),
        /// keyingi raqam esa shu prefiksli barcha invoyslarning sonli MAX'idan (+1) hisoblanadi.
        /// </summary>
        private static string ComputeNextInvoiceNo(DBTHSNEntities db)
        {
            var last = db.SALES_INVOICES
                .OrderByDescending(i => i.ID)
                .Select(i => i.InvoiceNo)
                .FirstOrDefault();

            string prefix = DefaultPrefix;
            int pad = DefaultPad;
            if (!string.IsNullOrWhiteSpace(last))
            {
                var m = Regex.Match(last.Trim(), @"^(.*?)(\d+)$");
                if (m.Success)
                {
                    prefix = m.Groups[1].Value;
                    pad = m.Groups[2].Length;
                }
            }

            const string sql =
                "SELECT MAX(TRY_CAST(SUBSTRING(InvoiceNo, @p0, 50) AS BIGINT)) FROM SALES_INVOICES WHERE InvoiceNo LIKE @p1";
            long? max = db.Database.SqlQuery<long?>(sql, prefix.Length + 1, EscapeLike(prefix) + "%").FirstOrDefault();
            long next = (max ?? 0) + 1;
            return prefix + next.ToString("D" + pad);
        }

        private static string EscapeLike(string value)
        {
            return value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
        }

        private static void EnsureInvoiceNoFree(DBTHSNEntities db, string invoiceNo, long selfId)
        {
            if (db.SALES_INVOICES.Any(i => i.InvoiceNo == invoiceNo && i.ID != selfId))
                throw new SalesInvoiceException($"\"{invoiceNo}\" raqamli invoys allaqachon mavjud. Raqamni bo'sh qoldiring — avtomatik beriladi.");
        }

        // ───────────────────────────── Hisob-kitob (D6, D7) ─────────────────────────────

        /// <summary>Qator summalari va sarlavha jamilarini hisoblaydi.</summary>
        public static void ComputeTotals(SALES_INVOICES header, IEnumerable<SalesInvoiceLineViewModel> lines)
        {
            decimal totalAmount = 0, totalVat = 0;
            foreach (var line in lines)
            {
                line.LineAmount = line.Qty * line.UnitPrice;
                line.VATAmount = line.LineAmount * line.VATRate / 100m;
                line.TotalWithVAT = line.LineAmount + line.VATAmount;
                totalAmount += line.LineAmount;
                totalVat += line.VATAmount;
            }

            header.TotalAmount = totalAmount;
            header.TotalVAT = totalVat;
            header.TotalWithVAT = totalAmount + totalVat;
        }

        public static List<SALES_INVOICE_LINES> BuildLines(IEnumerable<SalesInvoiceLineViewModel> lines)
        {
            int lineNo = 1;
            return lines.Select(line => new SALES_INVOICE_LINES
            {
                LineNo = lineNo++,
                ProductID = line.ProductID,
                Description = line.Description,
                Qty = line.Qty,
                UnitPrice = line.UnitPrice,
                LineAmount = line.LineAmount,
                VATRate = line.VATRate,
                VATAmount = line.VATAmount,
                TotalWithVAT = line.TotalWithVAT
            }).ToList();
        }

        /// <summary>Saqlangan qatorlarni formaga qaytarish (xato bo'lganda kiritilgan qatorlar yo'qolmasligi uchun).</summary>
        public static void AttachLinesForRedisplay(SALES_INVOICES invoice, IEnumerable<SalesInvoiceLineViewModel> lines)
        {
            invoice.SALES_INVOICE_LINES = lines == null
                ? new List<SALES_INVOICE_LINES>()
                : BuildLines(lines);
        }

        // ───────────────────────────── Yaratish (CRM-B01) ─────────────────────────────

        public static long Create(SALES_INVOICES posted, IList<SalesInvoiceLineViewModel> lines, string userName, int userId)
        {
            lines = NormalizeLines(lines);

            using (var db = new DBTHSNEntities())
            {
                ValidateLines(db, posted.ContractID, posted.OrderID, lines);
                ApplyContractPrices(db, posted.ContractID, lines);
                EnsureStockAvailable(db, lines, null);
            }

            var invoice = new SALES_INVOICES
            {
                InvoiceNo = string.IsNullOrWhiteSpace(posted.InvoiceNo) ? null : posted.InvoiceNo.Trim(),
                InvoiceDate = posted.InvoiceDate,
                CustomerID = posted.CustomerID,
                ContractID = posted.ContractID,
                OrderID = posted.OrderID,
                CurrencyCode = posted.CurrencyCode,
                // CRM-M02: CurrencyID hech qachon yozilmasdi — posting "CurrencyID ?? 1" bilan yiqilardi (bazada 1-valyuta yo'q).
                // Kesh tranzaksiyadan OLDIN o'qiladi (scope ichida yangi ulanish DTC'ga olib keladi).
                CurrencyID = CurrencyRateService.CurrencyIdByCode(posted.CurrencyCode) ?? CurrencyRateService.BaseCurrencyId(),
                DueDate = posted.DueDate,
                // Biznes qoidasi: invoys yaratilganda darhol APPROVED
                StatusCode = SalesInvoiceStatus.Approved,
                CreatedDT = DateTime.Now,
                CreatedByUserID = userId
            };
            ComputeTotals(invoice, lines);

            using (var scope = NewScope())
            using (var db = new DBTHSNEntities())
            {
                if (invoice.InvoiceNo == null)
                    invoice.InvoiceNo = NextInvoiceNo(db);
                else
                    EnsureInvoiceNoFree(db, invoice.InvoiceNo, 0);

                foreach (var line in BuildLines(lines))
                    invoice.SALES_INVOICE_LINES.Add(line);

                db.SALES_INVOICES.Add(invoice);
                SaveChanges(db);

                IssueStock(db, invoice, lines, userName);
                FinancePostingService.PostSalesInvoice((int)invoice.ID, db, userId);

                scope.Complete();
            }

            return invoice.ID;
        }

        // ───────────────────────────── Tahrirlash (CRM-B03) ─────────────────────────────

        /// <summary>
        /// Tahrirlash. Qatorlardagi mahsulot/miqdor o'zgarsa — avvalgi ombor chiqimi qaytariladi va yangisi
        /// yoziladi (avval qatorlar almashardi-yu, ombor qoldig'i o'zgarmay qolardi). Hammasi bitta tranzaksiyada.
        /// </summary>
        public static void Update(SALES_INVOICES posted, IList<SalesInvoiceLineViewModel> lines, string userName, int userId)
        {
            lines = NormalizeLines(lines);
            bool linesChanged, stockChanged;
            var currencyId = CurrencyRateService.CurrencyIdByCode(posted.CurrencyCode) ?? CurrencyRateService.BaseCurrencyId();

            using (var db = new DBTHSNEntities())
            {
                var current = db.SALES_INVOICES.AsNoTracking()
                    .Include(i => i.SALES_INVOICE_LINES)
                    .FirstOrDefault(i => i.ID == posted.ID);
                if (current == null)
                    throw new SalesInvoiceException("Invoys topilmadi", notFound: true);
                if (SalesInvoiceStatus.IsLocked(current.StatusCode))
                    throw new SalesInvoiceException("Yakunlangan/to'langan/bekor qilingan invoysni tahrirlash mumkin emas");

                ValidateLines(db, posted.ContractID, posted.OrderID, lines);
                ApplyContractPrices(db, posted.ContractID, lines);

                var oldLines = current.SALES_INVOICE_LINES.OrderBy(l => l.LineNo).ToList();
                stockChanged = !SameQuantities(oldLines, lines);
                linesChanged = stockChanged || !SameLines(oldLines, lines);

                if (linesChanged)
                {
                    bool hasDependents = db.SALES_SF_LINES.Any(l => l.SALES_INVOICE_LINES.InvoiceID == posted.ID)
                        || db.SALES_TTNK_LINES.Any(l => l.SALES_INVOICE_LINES.InvoiceID == posted.ID);
                    if (hasDependents)
                        throw new SalesInvoiceException("Bu invoys qatorlari asosida SF/TTNK yaratilgan — qatorlarni o'zgartirish mumkin emas. Avval SF/TTNK ni o'chiring yoki faqat sarlavhani tahrirlang.");
                }

                if (stockChanged)
                {
                    // O'z chiqimi qaytariladi — shu miqdor mavjud qoldiqqa qo'shib tekshiriladi
                    var credit = oldLines.GroupBy(l => l.ProductID).ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));
                    EnsureStockAvailable(db, lines, credit);
                }
            }

            using (var scope = NewScope())
            using (var db = new DBTHSNEntities())
            {
                if (stockChanged)
                    WarehouseFlowService.RollbackSalesInvoiceStockAndDeleteAutoExpenses(posted.ID, db, userName);

                var invoice = db.SALES_INVOICES
                    .Include(i => i.SALES_INVOICE_LINES)
                    .First(i => i.ID == posted.ID);

                var invoiceNo = string.IsNullOrWhiteSpace(posted.InvoiceNo) ? invoice.InvoiceNo : posted.InvoiceNo.Trim();
                if (invoiceNo != invoice.InvoiceNo)
                    EnsureInvoiceNoFree(db, invoiceNo, invoice.ID);

                invoice.InvoiceNo = invoiceNo;
                invoice.InvoiceDate = posted.InvoiceDate;
                invoice.CustomerID = posted.CustomerID;
                invoice.ContractID = posted.ContractID;
                invoice.OrderID = posted.OrderID;
                invoice.CurrencyCode = posted.CurrencyCode;
                invoice.CurrencyID = currencyId;
                // Tahrirlash formasida to'lov muddati maydoni yo'q — bo'sh kelsa saqlangani qoladi (avval NULL bo'lib ketardi)
                invoice.DueDate = posted.DueDate ?? invoice.DueDate;
                invoice.UpdatedDT = DateTime.Now;
                // Biznes qoidasi: tahrirlangan invoys ham APPROVED oqimida yuradi
                invoice.StatusCode = SalesInvoiceStatus.Approved;

                ComputeTotals(invoice, lines);
                if (linesChanged)
                {
                    db.SALES_INVOICE_LINES.RemoveRange(invoice.SALES_INVOICE_LINES.ToList());
                    foreach (var line in BuildLines(lines))
                        invoice.SALES_INVOICE_LINES.Add(line);
                }

                SaveChanges(db);

                if (stockChanged)
                    IssueStock(db, invoice, lines, userName);

                // Finance posting upsert
                FinancePostingService.PostSalesInvoice((int)invoice.ID, db, userId);

                scope.Complete();
            }
        }

        // ───────────────────────────── Bekor qilish ─────────────────────────────

        /// <summary>Status CANCELED + ombor chiqimini qaytarish + finance yozuvini olib tashlash — bitta tranzaksiyada.</summary>
        public static SALES_INVOICES Cancel(long id, string userName)
        {
            using (var scope = NewScope())
            using (var db = new DBTHSNEntities())
            {
                var invoice = db.SALES_INVOICES.FirstOrDefault(i => i.ID == id);
                if (invoice == null)
                    throw new SalesInvoiceException("Invoys topilmadi", notFound: true);
                if (invoice.StatusCode == SalesInvoiceStatus.Paid)
                    throw new SalesInvoiceException("To'langan invoysni o'chirib bo'lmaydi.");
                if (SalesInvoiceStatus.IsCanceled(invoice.StatusCode))
                    return invoice;

                // Avval ombor (WarehouseFlowService o'zi SaveChanges qiladi), keyin status
                WarehouseFlowService.RollbackSalesInvoiceStockAndDeleteAutoExpenses(id, db, userName);
                FinancePostingService.RemoveSalesInvoicePosting((int)id, db);

                invoice.StatusCode = SalesInvoiceStatus.Canceled;
                invoice.UpdatedDT = DateTime.Now;
                db.SaveChanges();

                scope.Complete();
                return invoice;
            }
        }

        // ───────────────────────────── Mahsulot ro'yxati (CRM-P03) ─────────────────────────────

        /// <summary>
        /// Sotuv omboridagi qoldig'i bor tayyor mahsulotlar (bitta bulk qoldiq so'rovi + bitta proyeksiya;
        /// avval har mahsulotga PRODUCTS.Find + GetQty — 2–3N so'rov).
        /// <paramref name="ownQty"/> — tahrirlanayotgan invoysning o'z qatorlari: ular qoldiq 0 bo'lsa ham
        /// ro'yxatda qoladi va mavjud miqdorga qo'shib ko'rsatiladi (CRM-B03).
        /// </summary>
        public static List<SelectListItem> SalesWarehouseProductItems(DBTHSNEntities db, IDictionary<int, decimal> ownQty = null)
        {
            var stock = GetSalesStockMap(db);
            if (stock == null)
                return new List<SelectListItem>();

            var ownIds = ownQty?.Keys.ToList() ?? new List<int>();
            var stockIds = stock.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();

            // Sotuv uchun: o'chirilmagan, tayyor mahsulot (PRODUCT yoki kategoriya), xomashyo emas
            var products = db.PRODUCTS.AsNoTracking()
                .Where(p => (stockIds.Contains(p.ID)
                        && p.IsDeleted == false
                        && (p.IsFinishedGood == true
                            || (p.PRODUCT_CATEGORIES != null && p.PRODUCT_CATEGORIES.IsFinishedGood == true))
                        && p.IsRawMaterial != true
                        && (p.PRODUCT_CATEGORIES == null || p.PRODUCT_CATEGORIES.IsRawMaterial != true))
                    || ownIds.Contains(p.ID))
                .Select(p => new { p.ID, p.PNo, p.Name })
                .ToList();

            return products
                .OrderBy(p => p.PNo)
                .Select(p =>
                {
                    double qty;
                    stock.TryGetValue(p.ID, out qty);
                    decimal own = 0;
                    if (ownQty != null) ownQty.TryGetValue(p.ID, out own);
                    return new SelectListItem
                    {
                        Value = p.ID.ToString(),
                        Text = $"{p.PNo} - {p.Name} (Mavjud: {qty + (double)own:N2})"
                    };
                })
                .ToList();
        }

        /// <summary>Sotuv ombori qoldig'i (ProductID → miqdor); ombor topilmasa null.</summary>
        public static Dictionary<int, double> GetSalesStockMap(DBTHSNEntities db)
        {
            var salesWH = WarehouseFlowService.GetSalesWarehouse(db);
            if (salesWH == null)
                return null;

            return InventoryValuationService.GetPositiveStocks(db, salesWH.ID)
                .GroupBy(r => r.ProductID)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Qty));
        }

        // ───────────────────────────── Ichki yordamchilar ─────────────────────────────

        /// <summary>
        /// WarehouseFlowService ichki scope'lari standart (Serializable) TransactionScope ochadi —
        /// ichma-ich scope izolyatsiyasi mos bo'lishi shart, shuning uchun standart konstruktor.
        /// </summary>
        private static TransactionScope NewScope()
        {
            return new TransactionScope(TransactionScopeOption.Required);
        }

        private static IList<SalesInvoiceLineViewModel> NormalizeLines(IList<SalesInvoiceLineViewModel> lines)
        {
            var result = (lines ?? new List<SalesInvoiceLineViewModel>())
                .Where(l => l != null && l.ProductID > 0)
                .ToList();

            if (!result.Any())
                throw new SalesInvoiceException("Kamida bitta maxsulot qo'shish kerak. Mahsulot tanlang, miqdor va QQS % kiriting.");
            if (result.Any(l => l.Qty <= 0))
                throw new SalesInvoiceException("Har bir qatorda miqdor 0 dan katta bo'lishi kerak.");

            foreach (var line in result)
                line.VATRate = Math.Max(0m, Math.Min(100m, line.VATRate));

            return result;
        }

        /// <summary>Tanlangan shartnoma/buyurtmada bo'lmagan mahsulotni rad etadi.</summary>
        private static void ValidateLines(DBTHSNEntities db, int? contractId, int? orderId, IList<SalesInvoiceLineViewModel> lines)
        {
            HashSet<int> contractProductIds = null, orderProductIds = null;
            if (contractId.HasValue)
            {
                contractProductIds = new HashSet<int>(db.S_CONTRACT_PRODUCTS
                    .Where(cp => cp.ContractID == contractId.Value)
                    .Select(cp => cp.ProductID)
                    .ToList());
            }
            if (orderId.HasValue)
            {
                orderProductIds = new HashSet<int>(db.S_ORDER_PRODUCTS
                    .Where(op => op.OrderID == orderId.Value)
                    .Select(op => op.ProductID)
                    .ToList());
            }

            var names = ProductLabels(db, lines.Select(l => l.ProductID));
            foreach (var line in lines)
            {
                if (contractProductIds != null && contractProductIds.Count > 0 && !contractProductIds.Contains(line.ProductID))
                    throw new SalesInvoiceException($"Mahsulot {names(line.ProductID)} tanlangan shartnomada mavjud emas.");
                if (orderProductIds != null && orderProductIds.Count > 0 && !orderProductIds.Contains(line.ProductID))
                    throw new SalesInvoiceException($"Mahsulot {names(line.ProductID)} tanlangan buyurtmada mavjud emas.");
            }
        }

        /// <summary>
        /// Shartnoma tanlangan bo'lsa birlik narxi shartnomadan olinadi (formada readonly — CRM-S03).
        /// Shartnomada bir mahsulot turli narxda takrorlangan bo'lsa, kiritilgan narx qoldiriladi.
        /// QQS % ham shartnoma qatoridan olinadi (formada readonly).
        /// </summary>
        private static void ApplyContractPrices(DBTHSNEntities db, int? contractId, IList<SalesInvoiceLineViewModel> lines)
        {
            if (!contractId.HasValue) return;

            var prices = db.S_CONTRACT_PRODUCTS
                .Where(cp => cp.ContractID == contractId.Value)
                .Select(cp => new { cp.ProductID, cp.PiecePrice })
                .ToList()
                .GroupBy(cp => cp.ProductID)
                .Where(g => g.Select(x => x.PiecePrice).Distinct().Count() == 1)
                .ToDictionary(g => g.Key, g => (decimal)g.First().PiecePrice);

            // QQS foizi ham shartnomadan (CRM-S03): formadagi qiymatga faqat shartnomada yo'q mahsulot uchun ishoniladi
            var vatRates = SContractVatHelper.GetProductVatRates(db, contractId);

            foreach (var line in lines)
            {
                decimal price;
                if (prices.TryGetValue(line.ProductID, out price))
                    line.UnitPrice = price;

                double vatRate;
                if (vatRates.TryGetValue(line.ProductID, out vatRate))
                    line.VATRate = (decimal)vatRate;
            }
        }

        private static void EnsureStockAvailable(DBTHSNEntities db, IList<SalesInvoiceLineViewModel> lines, IDictionary<int, decimal> credit)
        {
            var stock = GetSalesStockMap(db);
            if (stock == null)
                throw new SalesInvoiceException("Sotuv ombori topilmadi");

            var names = ProductLabels(db, lines.Select(l => l.ProductID));
            var errors = new List<string>();
            foreach (var g in lines.GroupBy(l => l.ProductID))
            {
                double available;
                stock.TryGetValue(g.Key, out available);
                decimal own = 0;
                if (credit != null) credit.TryGetValue(g.Key, out own);

                var total = (decimal)available + own;
                var needed = g.Sum(l => l.Qty);
                if (needed > total + 0.000000001m)
                    errors.Add($"Mahsulot {names(g.Key)} uchun sotuv omborida yetarli qoldiq yo'q. Mavjud: {total:N2}, Talab: {needed:N2}");
            }

            if (errors.Any())
                throw new SalesInvoiceException(string.Join(" ", errors));
        }

        private static Func<int, string> ProductLabels(DBTHSNEntities db, IEnumerable<int> productIds)
        {
            var ids = productIds.Distinct().ToList();
            var map = db.PRODUCTS.AsNoTracking()
                .Where(p => ids.Contains(p.ID))
                .Select(p => new { p.ID, p.PNo })
                .ToList()
                .ToDictionary(p => p.ID, p => p.PNo);
            return id => map.ContainsKey(id) && !string.IsNullOrEmpty(map[id]) ? map[id] : id.ToString();
        }

        /// <summary>Har qator uchun sotuv omboridan chiqim. Xato yutilmaydi — butun tranzaksiya bekor bo'ladi.</summary>
        private static void IssueStock(DBTHSNEntities db, SALES_INVOICES invoice, IEnumerable<SalesInvoiceLineViewModel> lines, string userName)
        {
            foreach (var line in lines)
            {
                WarehouseFlowService.DecreaseSalesStock(
                    line.ProductID,
                    line.Qty,
                    db,
                    userName,
                    invoice.ID,
                    invoice.InvoiceNo ?? invoice.ID.ToString(),
                    invoice.InvoiceDate,
                    invoice.ContractID);
            }
        }

        private static bool SameQuantities(IEnumerable<SALES_INVOICE_LINES> oldLines, IEnumerable<SalesInvoiceLineViewModel> newLines)
        {
            var a = oldLines.GroupBy(l => l.ProductID).ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));
            var b = newLines.GroupBy(l => l.ProductID).ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));
            return a.Count == b.Count && a.All(kv => b.ContainsKey(kv.Key) && b[kv.Key] == kv.Value);
        }

        private static bool SameLines(IList<SALES_INVOICE_LINES> oldLines, IList<SalesInvoiceLineViewModel> newLines)
        {
            if (oldLines.Count != newLines.Count) return false;
            for (int i = 0; i < oldLines.Count; i++)
            {
                var o = oldLines[i];
                var n = newLines[i];
                if (o.ProductID != n.ProductID || o.Qty != n.Qty || o.UnitPrice != n.UnitPrice || o.VATRate != n.VATRate
                    || (o.Description ?? "") != (n.Description ?? ""))
                    return false;
            }
            return true;
        }

        private static void SaveChanges(DBTHSNEntities db)
        {
            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                throw new SalesInvoiceException("Invoys raqami band (boshqa foydalanuvchi bir vaqtda saqladi). Qayta urinib ko'ring.");
            }
        }

        private static bool IsUniqueViolation(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                var sql = e as SqlException;
                if (sql != null && (sql.Number == 2601 || sql.Number == 2627))
                    return true;
            }
            return false;
        }
    }
}
