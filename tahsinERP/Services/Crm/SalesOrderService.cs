using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using tahsinERP.Controllers;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Services.Crm
{
    /// <summary>Buyurtma amali rad etildi — foydalanuvchiga ko'rsatiladigan xabarlar bilan.</summary>
    public sealed class SalesOrderException : Exception
    {
        public SalesOrderException(IEnumerable<string> errors, bool notFound = false)
            : this((errors ?? Enumerable.Empty<string>()).Distinct().ToList(), notFound)
        {
        }

        public SalesOrderException(string error, bool notFound = false)
            : this(new List<string> { error }, notFound)
        {
        }

        private SalesOrderException(List<string> errors, bool notFound) : base(string.Join(" ", errors))
        {
            Errors = errors;
            NotFound = notFound;
        }

        public IReadOnlyList<string> Errors { get; }
        public bool NotFound { get; }
    }

    /// <summary>
    /// Sotuv buyurtmasi biznes mantiqi (CRM-M01: avval hammasi SOrderController ichida edi).
    /// Controller faqat formani o'qiydi, ro'yxatlarni quradi va xatoni ko'rsatadi.
    /// Narx/birlik doim shartnoma qatoridan olinadi (CRM-S03).
    /// </summary>
    public static class SalesOrderService
    {
        public static IQueryable<S_ORDERS> Active(this IQueryable<S_ORDERS> query)
        {
            return query.Where(o => o.IsDeleted == false || o.IsDeleted == null);
        }

        public static List<ContractProductQuantity> LoadContractProducts(DBTHSNEntities db, int contractId)
        {
            return db.S_CONTRACT_PRODUCTS.AsNoTracking()
                .Where(cp => cp.ContractID == contractId)
                .OrderBy(cp => cp.PRODUCT.PNo)
                .Select(cp => new
                {
                    cp.ID,
                    cp.ProductID,
                    PNo = cp.PRODUCT.PNo,
                    Name = cp.PRODUCT.Name,
                    cp.PiecePrice,
                    cp.Amount,
                    cp.UnitID,
                    UnitName = cp.UNIT.ShortName,
                    cp.vatamount
                })
                .ToList()
                .Select(cp => new ContractProductQuantity
                {
                    ContractProductID = cp.ID,
                    ProductID = cp.ProductID,
                    ProductPNo = cp.PNo,
                    ProductName = cp.Name,
                    PiecePrice = cp.PiecePrice,
                    ContractAmount = cp.Amount,
                    UnitID = cp.UnitID,
                    UnitName = cp.UnitName,
                    OrderQuantity = 0,
                    VatRate = SContractVatHelper.GetVatPercentFromLine(
                        SContractVatHelper.GetLineBaseAmount(cp.PiecePrice, cp.Amount), cp.vatamount)
                })
                .ToList();
        }

        /// <summary>Yangi buyurtma: sarlavha va qatorlar bitta SaveChanges'da.</summary>
        public static S_ORDERS Create(DBTHSNEntities db, SOrderViewModel model, string userName)
        {
            RequireLines(model);
            ResolveContractLines(db, model);
            ValidateOrderQuantities(db, model, null);

            var vatByProduct = SContractVatHelper.GetProductVatRates(db, model.ContractID);
            var order = new S_ORDERS
            {
                OrderNo = model.OrderNo,
                IssuedDate = model.IssuedDate,
                CompanyID = int.Parse(ConfigurationManager.AppSettings["companyID"]),
                CustomerID = model.CustomerID,
                ContractID = model.ContractID,
                CurrencyID = ContractCurrencyId(db, model.ContractID) ?? 0,
                Description = model.Description,
                IsDeleted = false
            };

            double totalAmount = 0;
            foreach (var q in model.ProductQuantities)
            {
                var line = new S_ORDER_PRODUCTS { MOQ = 0 };
                totalAmount += ApplyLine(line, q, vatByProduct);
                order.S_ORDER_PRODUCTS.Add(line);
            }
            order.Amount = totalAmount;

            db.S_ORDERS.Add(order);
            db.SaveChanges();

            LogHelper.LogToDatabase(userName, "SOrderController", $"{order.OrderNo} - Buyurtmani yaratdi");
            return order;
        }

        /// <summary>
        /// Tahrirlash — qatorlar joyida yangilanadi (CRM-B10). Invoys qilingan miqdordan kamaytirish
        /// va rejasi bor qatorni olib tashlash taqiqlanadi.
        /// </summary>
        public static S_ORDERS Update(DBTHSNEntities db, int id, SOrderViewModel model, string userName)
        {
            RequireLines(model);
            ResolveContractLines(db, model);
            ValidateOrderQuantities(db, model, id);

            var order = db.S_ORDERS
                .Include(o => o.S_ORDER_PRODUCTS)
                .Active()
                .FirstOrDefault(o => o.ID == id);
            if (order == null)
            {
                throw new SalesOrderException("Buyurtma topilmadi.", notFound: true);
            }

            // Qatorlarni moslash: avval shu mahsulot + narx, keyin shu mahsulot
            var unmatched = order.S_ORDER_PRODUCTS.ToList();
            var pairs = new List<Tuple<S_ORDER_PRODUCTS, ContractProductQuantity>>();
            foreach (var q in model.ProductQuantities)
            {
                var match = unmatched.FirstOrDefault(l => l.ProductID == q.ProductID && Math.Abs(l.Price - q.PiecePrice) < 0.000001)
                            ?? unmatched.FirstOrDefault(l => l.ProductID == q.ProductID);
                if (match != null) unmatched.Remove(match);
                pairs.Add(Tuple.Create(match, q));
            }

            ValidateEditAgainstDocuments(db, order, pairs, unmatched);

            order.OrderNo = model.OrderNo;
            order.IssuedDate = model.IssuedDate;
            order.CustomerID = model.CustomerID;
            order.ContractID = model.ContractID;
            order.Description = model.Description;
            var contractCurrencyId = ContractCurrencyId(db, model.ContractID);
            if (contractCurrencyId.HasValue)
            {
                order.CurrencyID = contractCurrencyId.Value;
            }

            var vatByProduct = SContractVatHelper.GetProductVatRates(db, model.ContractID);
            double totalAmount = 0;
            foreach (var pair in pairs)
            {
                var line = pair.Item1;
                if (line == null)
                {
                    line = new S_ORDER_PRODUCTS { OrderID = order.ID, MOQ = 0 };
                    db.S_ORDER_PRODUCTS.Add(line);
                }
                totalAmount += ApplyLine(line, pair.Item2, vatByProduct);
            }

            db.S_ORDER_PRODUCTS.RemoveRange(unmatched);
            order.Amount = totalAmount;
            db.SaveChanges();

            LogHelper.LogToDatabase(userName, "SOrderController", $"{order.OrderNo} - Buyurtmani tahrirladi");
            return order;
        }

        /// <summary>
        /// Yetkazib berish rejasini saqlash — ID bo'yicha upsert (CRM-B06). Yetkazilgan miqdor va
        /// QUALITY_INSPECTION.DeliveryScheduleID bog'lanishi saqlanadi.
        /// </summary>
        public static S_ORDERS SaveDeliverySchedule(DBTHSNEntities db, int orderId, IList<DeliveryScheduleItem> posted, string userName)
        {
            var order = db.S_ORDERS
                .Include(o => o.S_ORDER_PRODUCTS.Select(p => p.PRODUCT))
                .Active()
                .FirstOrDefault(o => o.ID == orderId);
            if (order == null)
            {
                throw new SalesOrderException("Buyurtma topilmadi.", notFound: true);
            }

            var existing = db.SORDER_DELIVERY_SCHEDULE.Where(s => s.SOrderID == orderId).ToList();
            var existingById = existing.ToDictionary(s => s.ID);
            var errors = new List<string>();

            foreach (var item in posted)
            {
                if (order.S_ORDER_PRODUCTS.All(p => p.ID != item.SOrderProductID))
                    errors.Add("Reja qatori buyurtmada yo'q mahsulotga bog'langan.");
                if (item.ID.HasValue)
                {
                    SORDER_DELIVERY_SCHEDULE row;
                    if (!existingById.TryGetValue(item.ID.Value, out row))
                        errors.Add("Reja qatori bu buyurtmaga tegishli emas.");
                    else if (item.QtyPlanned < row.QtyShipped)
                        errors.Add($"{row.DeliveryDate:dd.MM.yyyy} rejasida {row.QtyShipped:N2} yetkazilgan — reja miqdori undan kam bo'lishi mumkin emas.");
                }
            }

            foreach (var g in posted.GroupBy(i => i.SOrderProductID))
            {
                var orderProduct = order.S_ORDER_PRODUCTS.FirstOrDefault(p => p.ID == g.Key);
                var totalPlanned = (double)g.Sum(i => i.QtyPlanned);
                if (orderProduct != null && totalPlanned > orderProduct.Amount + 0.0001)
                    errors.Add($"{orderProduct.PRODUCT?.PNo} - Rejalashtirilgan miqdor ({totalPlanned:N2}) buyurtma miqdoridan ({orderProduct.Amount:N2}) oshib ketdi");
            }

            var postedIds = new HashSet<long>(posted.Where(i => i.ID.HasValue).Select(i => i.ID.Value));
            var toRemove = existing.Where(s => !postedIds.Contains(s.ID)).ToList();
            if (toRemove.Any())
            {
                var removeIds = toRemove.Select(s => (long?)s.ID).ToList();
                var inspected = new HashSet<long?>(db.QUALITY_INSPECTION
                    .Where(q => removeIds.Contains(q.DeliveryScheduleID))
                    .Select(q => q.DeliveryScheduleID)
                    .Distinct()
                    .ToList());
                foreach (var s in toRemove)
                {
                    if (s.QtyShipped > 0)
                        errors.Add($"{s.DeliveryDate:dd.MM.yyyy} rejasi bo'yicha yetkazib berilgan — uni o'chirib bo'lmaydi.");
                    else if (inspected.Contains(s.ID))
                        errors.Add($"{s.DeliveryDate:dd.MM.yyyy} rejasiga sifat nazorati bog'langan — uni o'chirib bo'lmaydi.");
                }
            }

            if (errors.Any())
            {
                throw new SalesOrderException(errors);
            }

            int? userId = RoleHelper.GetUserId(userName);
            foreach (var item in posted)
            {
                var productId = order.S_ORDER_PRODUCTS.First(p => p.ID == item.SOrderProductID).ProductID;
                if (item.ID.HasValue)
                {
                    var row = existingById[item.ID.Value];
                    row.SOrderProductID = item.SOrderProductID;
                    row.ProductID = productId;
                    row.DeliveryDate = item.DeliveryDate;
                    row.QtyPlanned = item.QtyPlanned;
                    row.UpdatedDT = DateTime.Now;
                }
                else
                {
                    db.SORDER_DELIVERY_SCHEDULE.Add(new SORDER_DELIVERY_SCHEDULE
                    {
                        SOrderID = orderId,
                        SOrderProductID = item.SOrderProductID,
                        ProductID = productId,
                        DeliveryDate = item.DeliveryDate,
                        QtyPlanned = item.QtyPlanned,
                        QtyShipped = 0,
                        StatusCode = "PLANNED",
                        CreatedByUserID = userId,
                        CreatedDT = DateTime.Now
                    });
                }
            }

            db.SORDER_DELIVERY_SCHEDULE.RemoveRange(toRemove);
            db.SaveChanges();

            LogHelper.LogToDatabase(userName, "SOrderController", $"Yetkazib berish rejasi saqlandi (Buyurtma: {order.OrderNo})");
            return order;
        }

        /// <summary>Soft-delete. Faol invoysi bor buyurtmani o'chirish taqiqlanadi (CRM-B10).</summary>
        public static async Task DeleteAsync(DBTHSNEntities db, int id, string userName)
        {
            var order = await db.S_ORDERS.Active().FirstOrDefaultAsync(o => o.ID == id);
            if (order == null)
            {
                throw new SalesOrderException("Buyurtma topilmadi.", notFound: true);
            }

            if (await db.SALES_INVOICES.Active().AnyAsync(i => i.OrderID == id))
            {
                throw new SalesOrderException("Buyurtmaga faol invoys bog'langan — avval invoysni bekor qiling.");
            }

            order.IsDeleted = true;
            await SoftDeleteReferenceService.AfterSOrderSoftDeletedAsync(db, id);
            await db.SaveChangesAsync();
            LogHelper.LogToDatabase(userName, "SOrderController", $"{order.OrderNo} - Buyurtmani o'chirdi");
        }

        // ───────────────────────────── yordamchi metodlar ─────────────────────────────

        private static void RequireLines(SOrderViewModel model)
        {
            if (model.ProductQuantities == null || !model.ProductQuantities.Any())
            {
                throw new SalesOrderException("Kamida bitta maxsulot uchun miqdor kiritish kerak");
            }
        }

        private static int? ContractCurrencyId(DBTHSNEntities db, int contractId)
        {
            return db.S_CONTRACTS.Where(c => c.ID == contractId).Select(c => (int?)c.CurrencyID).FirstOrDefault();
        }

        /// <summary>Har qator uchun mahsulot, narx va birlik shartnoma qatoridan olinadi (brauzerdagi yashirin maydonga ishonilmaydi).</summary>
        private static void ResolveContractLines(DBTHSNEntities db, SOrderViewModel model)
        {
            if (model.ContractID <= 0)
            {
                throw new SalesOrderException("Shartnoma tanlanmagan yoki noto'g'ri.");
            }

            var contractLines = db.S_CONTRACT_PRODUCTS.AsNoTracking()
                .Where(cp => cp.ContractID == model.ContractID)
                .Select(cp => new { cp.ID, cp.ProductID, cp.PiecePrice, cp.UnitID })
                .ToList()
                .ToDictionary(cp => cp.ID);

            foreach (var q in model.ProductQuantities)
            {
                if (!contractLines.TryGetValue(q.ContractProductID, out var cp))
                {
                    throw new SalesOrderException("Tanlangan mahsulot shu shartnomaga tegishli emas. Sahifani yangilab qayta urinib ko'ring.");
                }
                q.ProductID = cp.ProductID;
                q.PiecePrice = cp.PiecePrice;
                q.UnitID = cp.UnitID;
            }
        }

        /// <summary>Qator maydonlarini to'ldiradi va QQS bilan summani qaytaradi.</summary>
        private static double ApplyLine(S_ORDER_PRODUCTS line, ContractProductQuantity q, IDictionary<int, double> vatByProduct)
        {
            var lineBase = q.PiecePrice * q.OrderQuantity;
            double vatRate;
            vatByProduct.TryGetValue(q.ProductID, out vatRate);

            line.ProductID = q.ProductID;
            line.Price = q.PiecePrice;
            line.Amount = q.OrderQuantity;
            line.UnitID = q.UnitID;
            line.TotalPrice = lineBase;
            return lineBase + SContractVatHelper.CalculateVatAmount(lineBase, vatRate);
        }

        /// <summary>Shartnoma bo'yicha qolgan miqdordan oshmaslik (mahsulot takrorlansa yig'iladi — CRM-B13).</summary>
        private static void ValidateOrderQuantities(DBTHSNEntities db, SOrderViewModel model, int? currentOrderId)
        {
            var productIds = model.ProductQuantities.Select(pq => pq.ProductID).Distinct().ToList();

            var contractAmounts = db.S_CONTRACT_PRODUCTS
                .Where(cp => cp.ContractID == model.ContractID)
                .GroupBy(cp => cp.ProductID)
                .Select(g => new { ProductID = g.Key, Amount = g.Sum(x => x.Amount) })
                .ToList()
                .ToDictionary(cp => cp.ProductID, cp => cp.Amount);

            var orderedSumsQuery = db.S_ORDER_PRODUCTS
                .Where(p => p.S_ORDERS.ContractID == model.ContractID &&
                            (p.S_ORDERS.IsDeleted == false || p.S_ORDERS.IsDeleted == null));

            if (currentOrderId.HasValue)
            {
                orderedSumsQuery = orderedSumsQuery.Where(p => p.OrderID != currentOrderId.Value);
            }

            var orderedSums = orderedSumsQuery
                .GroupBy(p => p.ProductID)
                .Select(g => new { ProductID = g.Key, Total = g.Sum(x => x.Amount) })
                .ToList()
                .ToDictionary(x => x.ProductID, x => x.Total);

            var productNames = ProductLabels(db, productIds);
            var errors = new List<string>();

            foreach (var g in model.ProductQuantities.GroupBy(q => q.ProductID))
            {
                var label = productNames(g.Key);
                if (!contractAmounts.TryGetValue(g.Key, out var contractAmount))
                {
                    errors.Add($"{label} uchun shartnoma miqdori topilmadi.");
                    continue;
                }

                var alreadyOrdered = orderedSums.ContainsKey(g.Key) ? orderedSums[g.Key] : 0d;
                var remaining = Math.Max(0, contractAmount - alreadyOrdered);
                var requested = g.Sum(q => q.OrderQuantity);

                if (requested > remaining + 0.0001)
                {
                    errors.Add(
                        $"{label} bo'yicha shartnoma miqdori {contractAmount:N2}, " +
                        $"oldin buyurtma {alreadyOrdered:N2}, qolgan {remaining:N2}. " +
                        $"Kiritilgan {requested:N2}.");
                }
            }

            if (errors.Any())
            {
                throw new SalesOrderException(errors);
            }
        }

        /// <summary>Invoys va yetkazib berish rejasiga zid o'zgarishlarni rad etadi.</summary>
        private static void ValidateEditAgainstDocuments(DBTHSNEntities db, S_ORDERS order,
            List<Tuple<S_ORDER_PRODUCTS, ContractProductQuantity>> pairs, List<S_ORDER_PRODUCTS> removed)
        {
            var invoiced = db.SALES_INVOICE_LINES.Active()
                .Where(l => l.SALES_INVOICES.OrderID == order.ID)
                .GroupBy(l => l.ProductID)
                .Select(g => new { ProductID = g.Key, Qty = g.Sum(x => x.Qty) })
                .ToList()
                .ToDictionary(x => x.ProductID, x => (double)x.Qty);

            var lineIds = order.S_ORDER_PRODUCTS.Select(l => l.ID).ToList();
            var planned = db.SORDER_DELIVERY_SCHEDULE
                .Where(s => s.SOrderID == order.ID && lineIds.Contains(s.SOrderProductID))
                .GroupBy(s => s.SOrderProductID)
                .Select(g => new { LineID = g.Key, Qty = g.Sum(x => x.QtyPlanned) })
                .ToList()
                .ToDictionary(x => x.LineID, x => (double)x.Qty);

            var names = ProductLabels(db, pairs.Select(p => p.Item2.ProductID).Concat(removed.Select(r => r.ProductID)));
            var newTotals = pairs.GroupBy(p => p.Item2.ProductID).ToDictionary(g => g.Key, g => g.Sum(p => p.Item2.OrderQuantity));
            var errors = new List<string>();

            foreach (var kv in invoiced)
            {
                double newQty;
                newTotals.TryGetValue(kv.Key, out newQty);
                if (newQty + 0.0001 < kv.Value)
                {
                    errors.Add($"{names(kv.Key)}: {kv.Value:N2} invoys qilingan — buyurtma miqdori undan kam bo'lishi mumkin emas.");
                }
            }

            foreach (var pair in pairs.Where(p => p.Item1 != null))
            {
                double plannedQty;
                if (planned.TryGetValue(pair.Item1.ID, out plannedQty) && pair.Item2.OrderQuantity + 0.0001 < plannedQty)
                {
                    errors.Add($"{names(pair.Item2.ProductID)}: yetkazib berish rejasida {plannedQty:N2} — miqdorni avval rejada kamaytiring.");
                }
            }

            foreach (var line in removed)
            {
                if (planned.ContainsKey(line.ID))
                {
                    errors.Add($"{names(line.ProductID)}: yetkazib berish rejasi bor — qatorni olib tashlashdan oldin rejadan o'chiring.");
                }
            }

            if (errors.Any())
            {
                throw new SalesOrderException(errors);
            }
        }

        private static Func<int, string> ProductLabels(DBTHSNEntities db, IEnumerable<int> productIds)
        {
            var ids = productIds.Distinct().ToList();
            var map = db.PRODUCTS.AsNoTracking()
                .Where(p => ids.Contains(p.ID))
                .Select(p => new { p.ID, p.PNo, p.Name })
                .ToList()
                .ToDictionary(p => p.ID, p => $"{p.PNo} {p.Name}".Trim());
            return id => map.ContainsKey(id) ? map[id] : $"ProductID={id}";
        }
    }
}
