using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services.Money;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Models;
using tahsinERP.Services.Stock;

namespace tahsinERP.Areas.Distribution.Services.Orders
{
    /// <summary>Kim buyurtma bilan ishlayapti va qanday huquqlari bor (web, bot, API bir xil).</summary>
    public sealed class OrderActor
    {
        public int UserId { get; set; }
        public string Source { get; set; } = AuditActor.Web;
        public decimal? MaxDiscountPct { get; set; }      // rollar bo'yicha eng katta limit; null — chegirma yo'q
        public bool DiscountUnlimited { get; set; }       // DstDiscountUnlimited: limit va minimal narxdan ozod
        public bool CanOverrideCredit { get; set; }       // DstOverrideCredit

        /// <summary>Joriy web foydalanuvchisi uchun huquqlarni yig'adi.</summary>
        public static OrderActor ForUser(DistributionDbContext db, string userName, string source = AuditActor.Web)
        {
            var userId = RoleHelper.GetUserId(userName) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var roleIds = db.Database.SqlQuery<int>("SELECT RoleID FROM dbo.USERROLES WHERE UserID = @p0", userId).ToList();
            var max = db.DiscountLimits.Where(d => roleIds.Contains(d.RoleID)).Select(d => (decimal?)d.MaxPercent).Max();
            return new OrderActor
            {
                UserId = userId,
                Source = source,
                MaxDiscountPct = max,
                DiscountUnlimited = DstPermissions.Has(userName, DstPermissions.DiscountUnlimited),
                CanOverrideCredit = DstPermissions.Has(userName, DstPermissions.OverrideCredit)
            };
        }
    }

    public sealed class OrderLineInput
    {
        public int ProductID { get; set; }
        public int? ProductUnitID { get; set; }
        public decimal Qty { get; set; }
        public decimal DiscountPct { get; set; }
    }

    public sealed class OrderInput
    {
        public int? ID { get; set; }
        public int CustomerID { get; set; }
        public int? AgentUserID { get; set; }
        public int? WarehouseID { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public bool IsPickup { get; set; }
        public string Comment { get; set; }
        public decimal DiscountPct { get; set; }
        public Guid? ClientRequestId { get; set; }
        public List<OrderLineInput> Lines { get; set; } = new List<OrderLineInput>();
    }

    /// <summary>Qarz limiti sabab tasdiqlanmadi — muammolar ro'yxati bilan (UI ruxsat so'rash uchun).</summary>
    public class CreditBlockedException : DstValidationException
    {
        public CreditBlockedException(IReadOnlyList<string> problems)
            : base(string.Join(" ", problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }
    }

    /// <summary>
    /// Buyurtma hayot sikli (01_PHASE1_MVP.md, 5-bo'lim). Barcha usullar chaqiruvchining
    /// <see cref="DistributionUnitOfWork"/>'ida ishlaydi; Commit — chaqiruvchida.
    /// </summary>
    public static class OrderService
    {
        public const string OrderPrefix = "D";
        public const string ShipmentPrefix = "DJ";

        // ---------- qoralama ----------

        /// <summary>Yangi buyurtma yoki yangi holatdagisini qayta yozish. Narx har doim narx varag'idan.</summary>
        public static int SaveDraft(DistributionUnitOfWork uow, OrderInput input, OrderActor actor, DateTime now)
        {
            var db = uow.Dst;
            if (input.ClientRequestId.HasValue)
            {
                var existing = db.Orders.Where(o => o.ClientRequestId == input.ClientRequestId).Select(o => (int?)o.ID).FirstOrDefault();
                if (existing.HasValue && input.ID == null) return existing.Value;   // bot qayta yubordi
            }

            var lines = (input.Lines ?? new List<OrderLineInput>()).Where(l => l != null && l.Qty != 0).ToList();
            if (lines.Count == 0) throw new DstValidationException(DstLocalization.T("Order_NoLines"));
            if (lines.Any(l => l.Qty < 0)) throw new DstValidationException(DstLocalization.T("Order_QtyPositive"));

            var customer = db.Customers.FirstOrDefault(c => c.ID == input.CustomerID && c.IsDeleted != true)
                           ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var profile = db.CustomerProfiles.Find(input.CustomerID)
                          ?? throw new DstValidationException(DstLocalization.T("Order_CustomerNotDistribution"));

            // avval hamma tekshiruv (kontekst o'zgarmaydi) — xato bo'lsa qisman o'zgarish qolmasin
            DstOrder existingOrder = null;
            if (input.ID.HasValue)
            {
                existingOrder = db.Orders.Find(input.ID.Value) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                if (!OrderStateMachine.IsEditable((OrderStatus)existingOrder.OrderStatus))
                    throw new DstValidationException(DstLocalization.T("Order_NotEditable"));
            }
            DirectoryRules.ValidateDiscountPercent(input.DiscountPct);

            // narxlar va birliklar
            var productIds = lines.Select(l => l.ProductID).Distinct().ToList();
            var products = db.Products.Where(p => productIds.Contains(p.ID) && !p.IsDeleted).ToDictionary(p => p.ID);
            var names = products.ToDictionary(p => p.Key, p => p.Value.PNo + " " + p.Value.Name);
            var prices = new PricingService(db).Resolve(input.CustomerID, productIds, now);
            var units = new UnitService(db);
            var saleActive = db.ProductProfiles.Where(p => productIds.Contains(p.ProductID) && !p.IsSaleActive).Select(p => p.ProductID).ToList();

            var calcs = new List<LineCalc>();
            var entities = new List<DstOrderLine>();
            int lineNo = 0;
            foreach (var l in lines)
            {
                if (!products.ContainsKey(l.ProductID)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                if (saleActive.Contains(l.ProductID)) throw new DstValidationException(DstLocalization.T("Order_ProductNotOnSale", names[l.ProductID]));

                var unit = units.UnitsOf(l.ProductID).FirstOrDefault(u => u.ProductUnitID == l.ProductUnitID)
                           ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
                var price = prices[l.ProductID];
                if (!price.Found) throw new DstValidationException(DstLocalization.T("Order_NoPrice", names[l.ProductID]));

                var calc = new LineCalc
                {
                    Qty = l.Qty, Factor = unit.Factor, UnitPrice = UnitMath.UnitPrice(price.Price.Value, unit.Factor),
                    DiscountPct = l.DiscountPct, UnitWeightKg = unit.WeightKg, UnitVolumeM3 = unit.VolumeM3
                };
                DirectoryRules.ValidateDiscountPercent(l.DiscountPct);
                OrderRules.CheckDiscount(OrderCalculator.EffectiveDiscountPct(l.DiscountPct, input.DiscountPct),
                    actor.MaxDiscountPct, actor.DiscountUnlimited);

                decimal? minUnit = price.MinPrice.HasValue ? UnitMath.UnitPrice(price.MinPrice.Value, unit.Factor) : (decimal?)null;
                decimal netAfterOrderDiscount = Math.Round(calc.NetUnitPrice * (100m - input.DiscountPct) / 100m, 2);
                OrderRules.CheckMinPrice(names[l.ProductID], netAfterOrderDiscount, minUnit, actor.DiscountUnlimited);

                calcs.Add(calc);
                entities.Add(new DstOrderLine
                {
                    LineNo = ++lineNo, ProductID = l.ProductID, ProductUnitID = unit.ProductUnitID, UnitID = unit.UnitID,
                    Factor = unit.Factor, Qty = calc.Qty, QtyBase = calc.QtyBase, UnitPrice = calc.UnitPrice, MinUnitPrice = minUnit,
                    PriceSource = (byte)price.Source, DiscountPct = calc.DiscountPct, DiscountSum = calc.DiscountSum, Amount = calc.Amount
                });
            }

            DstOrder order;
            if (existingOrder != null)
            {
                order = existingOrder;
                db.OrderLines.RemoveRange(db.OrderLines.Where(l => l.OrderID == order.ID).ToList());
            }
            else
            {
                order = new DstOrder
                {
                    DocNo = DstNumbering.Next(db, "D_ORDERS", OrderPrefix, now),
                    DocDate = now.Date, Status = DocumentStatus.Draft, OrderStatus = (byte)OrderStatus.New,
                    CreatedBy = actor.UserId, CreatedAt = now, Source = actor.Source, ClientRequestId = input.ClientRequestId
                };
                db.Orders.Add(order);
            }
            order.CustomerID = input.CustomerID;
            order.AgentUserID = input.AgentUserID ?? profile.AgentUserID;
            order.WarehouseID = input.WarehouseID;
            order.PriceTypeID = profile.PriceTypeID;
            order.DeliveryDate = input.DeliveryDate?.Date;
            order.IsPickup = input.IsPickup;
            order.Comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim();
            order.DiscountPct = input.DiscountPct;
            foreach (var e in entities) e.Order = order;

            var totals = OrderCalculator.Totals(calcs, input.DiscountPct);
            order.Subtotal = totals.Subtotal;
            order.DiscountSum = totals.OrderDiscountSum;
            order.Total = totals.Total;
            order.WeightKg = Math.Round(totals.WeightKg, 3);
            order.VolumeM3 = Math.Round(totals.VolumeM3, 4);
            db.OrderLines.AddRange(entities);
            db.SaveChanges();

            if (input.ID == null)
            {
                Log(db, order, null, OrderStatus.New, actor.UserId, now, null);
                NotifyNewOrder(db, order, customer.Name, actor.UserId, now);
            }
            db.SaveChanges();
            return order.ID;
        }

        /// <summary>Yangi (qoralama) buyurtmani o'chirish.</summary>
        public static void DeleteDraft(DistributionUnitOfWork uow, int orderId)
        {
            var db = uow.Dst;
            var order = Load(db, orderId);
            if (!OrderStateMachine.IsEditable((OrderStatus)order.OrderStatus))
                throw new DstValidationException(DstLocalization.T("Order_NotEditable"));
            db.OrderStatusLog.RemoveRange(db.OrderStatusLog.Where(x => x.OrderID == orderId).ToList());
            db.OrderLines.RemoveRange(db.OrderLines.Where(l => l.OrderID == orderId).ToList());
            db.SaveChanges();   // jurnalda navigatsiya yo'q — EF tartibni bilmaydi, avval bolalar
            db.Orders.Remove(order);
            db.SaveChanges();
        }

        // ---------- tasdiqlash va rezerv ----------

        /// <summary>
        /// Tasdiqlash: mijoz tasdiqlangan, minimal summa, qarz limiti (ruxsat bilan aylanib o'tish), ombor tanlash
        /// va rezerv. Yetmagan miqdor rezervsiz qoladi ("kirimni kutmoqda").
        /// </summary>
        public static void Confirm(DistributionUnitOfWork uow, int orderId, OrderActor actor, string creditOverrideNote, DateTime now)
        {
            var db = uow.Dst;
            var order = Load(db, orderId);
            OrderStateMachine.EnsureCanMove((OrderStatus)order.OrderStatus, OrderStatus.Confirmed);

            var profile = db.CustomerProfiles.Find(order.CustomerID);
            if (profile == null || !profile.IsApproved)
                throw new DstValidationException(DstLocalization.T("Order_CustomerNotApproved"));

            if (!order.IsPickup)
            {
                var min = MinOrderAmount(db);
                if (min > 0 && order.Total < min)
                    throw new DstValidationException(DstLocalization.T("Order_BelowMinAmount", min.ToString("N0")));
            }

            var credit = OrderRules.CheckCredit(LedgerService.CreditState(db, order.CustomerID, now), order.Total);
            if (!credit.Ok)
            {
                if (!actor.CanOverrideCredit || string.IsNullOrWhiteSpace(creditOverrideNote))
                    throw new CreditBlockedException(credit.Problems);
                order.CreditOverrideBy = actor.UserId;
                order.CreditOverrideAt = now;
                order.CreditOverrideNote = creditOverrideNote.Trim().Length > 300 ? creditOverrideNote.Trim().Substring(0, 300) : creditOverrideNote.Trim();
            }

            var lines = db.OrderLines.Where(l => l.OrderID == orderId).ToList();
            var need = lines.GroupBy(l => l.ProductID).ToDictionary(g => g.Key, g => g.Sum(l => l.QtyRemainingBase));
            var salesWarehouses = StockService.SalesWarehouses(db);
            if (order.WarehouseID == null)
            {
                order.WarehouseID = StockService.SuggestWarehouse(StockService.Levels(db, need.Keys.ToList(), salesWarehouses), need, salesWarehouses)
                                    ?? throw new DstValidationException(DstLocalization.T("Order_NoStockAnywhere"));
            }

            Reserve(db, order.WarehouseID.Value, lines, now);

            order.ConfirmedBy = actor.UserId;
            order.ConfirmedAt = now;
            if (order.Status == DocumentStatus.Draft)
                DocumentLifecycle.Post(order, actor.UserId, now);
            Move(db, order, OrderStatus.Confirmed, actor.UserId, now, order.CreditOverrideNote);
            if (order.AgentUserID.HasValue && order.AgentUserID != actor.UserId)
                NotificationService.ToUser(db, order.AgentUserID, NotificationService.OrderConfirmed,
                    l => DstLocalization.TFor(l, "Notify_OrderConfirmed", order.DocNo, CustomerName(db, order.CustomerID), order.Total.ToString("N0")), null, now);
            NotifyStorekeepers(db, order, lines.Count, actor.UserId, now);
            db.SaveChanges();
        }

        /// <summary>Omborchiga: tasdiqlangan buyurtmani yig'ish kerak (ombor scope'i bo'yicha).</summary>
        private static void NotifyStorekeepers(DistributionDbContext db, DstOrder order, int lineCount, int userId, DateTime now)
        {
            int warehouseId = order.WarehouseID.Value;
            var warehouse = db.Warehouses.Where(w => w.ID == warehouseId).Select(w => w.WHName).FirstOrDefault();
            var customer = CustomerName(db, order.CustomerID);
            NotificationService.ToStorekeepers(db, warehouseId, NotificationService.StoreOrderConfirmed,
                l => DstLocalization.TFor(l, "Notify_StoreOrder", order.DocNo, customer, warehouse, lineCount, order.WeightKg.ToString("0.#"),
                    order.Total.ToString("N0"),
                    order.IsPickup ? DstLocalization.TFor(l, "Notify_Pickup") : order.DeliveryDate?.ToString("dd.MM.yyyy") ?? "—"),
                null, now, userId);
        }

        /// <summary>
        /// Qatorlar uchun rezerv: (ombor, tovar) bo'yicha eksklyuziv qulf ostida mavjud qoldiq hisoblanadi —
        /// ikki operator bir vaqtda oxirgi qoldiqni ikki marta rezerv qila olmaydi.
        /// </summary>
        private static void Reserve(DistributionDbContext db, int warehouseId, List<DstOrderLine> lines, DateTime now)
        {
            foreach (var productId in lines.Select(l => l.ProductID).Distinct().OrderBy(x => x))
                Lock(db, warehouseId, productId);

            var levels = StockService.Levels(db, lines.Select(l => l.ProductID).ToList(), new[] { warehouseId })
                .ToDictionary(x => x.ProductID);
            var lineIds = lines.Select(l => l.ID).ToList();
            var existing = db.StockReservations.Where(r => lineIds.Contains(r.OrderLineID)).ToList();

            foreach (var line in lines)
            {
                var current = existing.FirstOrDefault(r => r.OrderLineID == line.ID);
                decimal mine = current?.QtyBase ?? 0;
                decimal available = levels.TryGetValue(line.ProductID, out var lvl) ? lvl.Available + mine : 0;
                decimal want = Math.Min(line.QtyRemainingBase, Math.Max(0, available));
                if (lvl != null) lvl.Reserved += want - mine;

                if (want <= 0)
                {
                    if (current != null) db.StockReservations.Remove(current);
                }
                else if (current == null)
                {
                    db.StockReservations.Add(new StockReservation
                    {
                        WarehouseID = warehouseId, ProductID = line.ProductID, OrderLineID = line.ID, QtyBase = want, CreatedAt = now
                    });
                }
                else
                {
                    current.QtyBase = want;
                    current.WarehouseID = warehouseId;
                }
            }
        }

        private static void Lock(DistributionDbContext db, int warehouseId, int productId)
        {
            db.Database.ExecuteSqlCommand(System.Data.Entity.TransactionalBehavior.DoNotEnsureTransaction,
                "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @p0, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; " +
                "IF @r < 0 THROW 51002, 'stock lock timeout', 1;", "dst.stock:" + warehouseId + ":" + productId);
        }

        private static void ReleaseReservations(DistributionDbContext db, int orderId)
        {
            var ids = db.OrderLines.Where(l => l.OrderID == orderId).Select(l => l.ID).ToList();
            db.StockReservations.RemoveRange(db.StockReservations.Where(r => ids.Contains(r.OrderLineID)).ToList());
        }

        /// <summary>Tasdiqni qaytarish (tahrirlash uchun): rezerv yechiladi, buyurtma yana yangi.</summary>
        public static void Unconfirm(DistributionUnitOfWork uow, int orderId, int userId, DateTime now)
        {
            var db = uow.Dst;
            var order = Load(db, orderId);
            OrderStateMachine.EnsureCanMove((OrderStatus)order.OrderStatus, OrderStatus.New);
            if (db.Shipments.Any(s => s.OrderID == orderId && s.Status != DocumentStatus.Cancelled))
                throw new DstValidationException(DstLocalization.T("Order_HasShipments"));
            ReleaseReservations(db, orderId);
            order.Status = DocumentStatus.Draft;
            order.PostedBy = null;
            order.PostedAt = null;
            order.ConfirmedBy = null;
            order.ConfirmedAt = null;
            order.CreditOverrideBy = null;
            order.CreditOverrideAt = null;
            order.CreditOverrideNote = null;
            Move(db, order, OrderStatus.New, userId, now, null);
            db.SaveChanges();
        }

        /// <summary>Rezervni yangilash (kirimdan keyin "kutmoqda" qatorlar uchun).</summary>
        public static void Rereserve(DistributionUnitOfWork uow, int orderId, DateTime now)
        {
            var db = uow.Dst;
            var order = Load(db, orderId);
            if (!OrderStateMachine.HoldsReservation((OrderStatus)order.OrderStatus) || order.WarehouseID == null)
                throw new DstValidationException(DstLocalization.T("Order_NotEditable"));
            Reserve(db, order.WarehouseID.Value, db.OrderLines.Where(l => l.OrderID == orderId).ToList(), now);
            db.SaveChanges();
        }

        public static void StartPicking(DistributionUnitOfWork uow, int orderId, int userId, DateTime now)
        {
            var db = uow.Dst;
            var order = Load(db, orderId);
            Move(db, order, OrderStatus.Picking, userId, now, null);
            db.SaveChanges();
        }

        // ---------- jo'natish ----------

        /// <summary>
        /// Jo'natish: ombor chiqimi (dbo.EXPENSES + EXPENSE_PRODUCTS) → sof FIFO (StockLedger.IssueLine) → tannarx,
        /// rezerv kamayadi, mijoz qarziga debet (muddati = bugun + PaymentDays). <paramref name="qtyBaseByLine"/>
        /// null — har qatorning rezervdagi miqdori. O'zi olib ketishda darhol "yetkazildi".
        /// </summary>
        public static int Ship(DistributionUnitOfWork uow, int orderId, IDictionary<int, decimal> qtyBaseByLine, int userId, DateTime now)
        {
            var db = uow.Dst;
            var erp = uow.Erp;
            var order = Load(db, orderId);
            if (!OrderStateMachine.CanShip((OrderStatus)order.OrderStatus))
                throw new DstValidationException(DstLocalization.T("Order_CannotShip"));
            int warehouseId = order.WarehouseID ?? throw new DstValidationException(DstLocalization.T("Order_NoWarehouse"));

            var lines = db.OrderLines.Where(l => l.OrderID == orderId).OrderBy(l => l.LineNo).ToList();
            var lineIds = lines.Select(l => l.ID).ToList();
            var reservations = db.StockReservations.Where(r => lineIds.Contains(r.OrderLineID)).ToList();

            foreach (var productId in lines.Select(l => l.ProductID).Distinct().OrderBy(x => x))
                Lock(db, warehouseId, productId);
            var levels = StockService.Levels(db, lines.Select(l => l.ProductID).ToList(), new[] { warehouseId }).ToDictionary(x => x.ProductID);

            var plan = new List<(DstOrderLine Line, decimal Qty)>();
            foreach (var line in lines)
            {
                var mine = reservations.FirstOrDefault(r => r.OrderLineID == line.ID)?.QtyBase ?? 0;
                decimal qty = qtyBaseByLine == null ? mine : (qtyBaseByLine.TryGetValue(line.ID, out var q) ? q : 0);
                if (qty <= 0) continue;
                if (qty > line.QtyRemainingBase) throw new DstValidationException(DstLocalization.T("Ship_OverOrdered", line.LineNo));
                decimal canTake = levels.TryGetValue(line.ProductID, out var lvl) ? lvl.Available + mine : 0;   // boshqalarning rezervi daxlsiz
                if (qty > canTake) throw new DstValidationException(DstLocalization.T("Ship_NotEnoughStock", line.LineNo, Math.Max(0, canTake).ToString("0.####")));
                if (lvl != null) lvl.Reserved += qty - Math.Min(qty, mine);
                plan.Add((line, qty));
            }
            if (plan.Count == 0) throw new DstValidationException(DstLocalization.T("Ship_Nothing"));

            var docNo = DstNumbering.Next(db, "D_SHIPMENTS", ShipmentPrefix, now);
            var products = plan.Select(p => p.Line.ProductID).Distinct().ToList();
            var baseUnits = db.Products.Where(p => products.Contains(p.ID)).ToDictionary(p => p.ID, p => p.UnitID);

            // ombor chiqimi hujjati (mavjud ombor moduli bilan bir xil)
            var expense = new EXPENS
            {
                DocNo = docNo, SenderWHID = warehouseId, IssueDateTime = now, CurrencyID = WarehouseAccess.DefaultCurrencyId(erp),
                IsDeleted = false, SendStatus = true,
                Description = "Distributsiya: " + order.DocNo + " → " + docNo + " URL: /Distribution/DstOrders/Details/" + order.ID
            };
            erp.EXPENSES.Add(expense);
            erp.SaveChanges();

            var shipment = new DstShipment
            {
                DocNo = docNo, DocDate = now.Date, Status = DocumentStatus.Draft, OrderID = order.ID, WarehouseID = warehouseId,
                ExpenseID = expense.ID, IsPickup = order.IsPickup, CreatedBy = userId, CreatedAt = now
            };
            db.Shipments.Add(shipment);

            decimal total = 0, costTotal = 0, totalQty = 0;
            foreach (var (line, qty) in plan)
            {
                var ep = new EXPENSE_PRODUCTS
                {
                    ExpenseID = expense.ID, ProductID = line.ProductID, Amount = qty, PiecePrice = 0,
                    UnitID = baseUnits[line.ProductID], Comment = order.DocNo + " #" + line.LineNo
                };
                erp.EXPENSE_PRODUCTS.Add(ep);
                erp.SaveChanges();
                try
                {
                    StockLedger.IssueLine(erp, warehouseId, line.ProductID, qty, ep.UnitID, ep.ID);
                }
                catch (Exception ex) when (!(ex is DstValidationException))
                {
                    throw new DstValidationException(DstLocalization.T("Ship_StockError", line.LineNo) + " " + ex.Message);
                }
                var unitCost = StockCostService.IssuedLineUnitCost(erp, warehouseId, line.ProductID, qty, ep.UnitID, ep.ID);
                ep.PiecePrice = StockCostService.StoredUnitCost(unitCost);
                decimal cost = Math.Round(qty * ep.PiecePrice, 2, MidpointRounding.AwayFromZero);

                // sotuv summasi: qator summasining ulushi, buyurtma chegirmasi bilan
                decimal amount = Math.Round(line.Amount * qty / line.QtyBase * (100m - order.DiscountPct) / 100m, 2, MidpointRounding.AwayFromZero);
                db.ShipmentLines.Add(new DstShipmentLine
                {
                    Shipment = shipment, OrderLineID = line.ID, ProductID = line.ProductID, QtyBase = qty, Amount = amount, CostAmount = cost
                });

                line.QtyShippedBase += qty;
                var res = reservations.FirstOrDefault(r => r.OrderLineID == line.ID);
                if (res != null)
                {
                    res.QtyBase -= Math.Min(qty, res.QtyBase);
                    if (res.QtyBase <= 0) db.StockReservations.Remove(res);
                }
                total += amount;
                costTotal += cost;
                totalQty += qty;
            }
            expense.Amount = totalQty;
            expense.TotalPrice = costTotal;
            erp.SaveChanges();

            shipment.Total = total;
            shipment.CostTotal = costTotal;
            DocumentLifecycle.Post(shipment, userId, now);
            db.SaveChanges();

            var profile = db.CustomerProfiles.Find(order.CustomerID);
            LedgerService.Debit(db, order.CustomerID, LedgerDocTypes.Shipment, shipment.ID, order.ID, total, now,
                profile?.PaymentDays ?? 0, order.AgentUserID, userId, docNo);

            order.ShippedTotal += total;
            bool all = lines.All(l => l.QtyRemainingBase <= 0);
            Move(db, order, all ? OrderStatus.Shipped : OrderStatus.PartiallyShipped, userId, now, docNo);
            if (order.IsPickup && all)
            {
                order.DeliveredAt = now;
                Move(db, order, OrderStatus.Delivered, userId, now, null);
            }
            NotificationService.ToCustomer(db, order.CustomerID, NotificationService.OrderShipped,
                l => DstLocalization.TFor(l, "Notify_Shipped", order.DocNo, total.ToString("N0"),
                    LedgerService.CreditState(db, order.CustomerID, now).Balance.ToString("N0")), now);
            db.SaveChanges();
            DebtService.SettleOrders(db, order.CustomerID, userId, now);   // oldindan to'langan bo'lsa — darhol yopiladi
            db.SaveChanges();
            return shipment.ID;
        }

        /// <summary>
        /// Jo'natishni bekor qilish (xato jo'natildi): ombor chiqimi teskari (StockLedger.ReverseIssueLine), qarz teskari
        /// yozuv bilan, buyurtma qatorlari va holati qaytariladi, tovar yana rezervga olinadi. Yetkazilgan/yopilgan yoki
        /// qaytarish qilingan jo'natish bekor qilinmaydi — qaytarish hujjati bilan.
        /// </summary>
        public static void CancelShipment(DistributionUnitOfWork uow, int shipmentId, int userId, string reason, DateTime now)
        {
            var db = uow.Dst;
            var erp = uow.Erp;
            var shipment = db.Shipments.Find(shipmentId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var order = Load(db, shipment.OrderID);
            var status = (OrderStatus)order.OrderStatus;
            if (status != OrderStatus.Shipped && status != OrderStatus.PartiallyShipped)
                throw new DstValidationException(DstLocalization.T("Shipment_CannotCancel"));
            var shipmentLines = db.ShipmentLines.Where(l => l.ShipmentID == shipmentId).ToList();
            var slIds = shipmentLines.Select(l => l.ID).ToList();
            if (db.ReturnLines.Any(r => r.ShipmentLineID != null && slIds.Contains(r.ShipmentLineID.Value) && r.Return.Status != DocumentStatus.Cancelled))
                throw new DstValidationException(DstLocalization.T("Shipment_HasReturns"));

            DocumentLifecycle.Cancel(shipment, userId, reason, now);

            if (shipment.ExpenseID.HasValue)
            {
                var expense = erp.EXPENSES.Find(shipment.ExpenseID.Value);
                var eps = erp.EXPENSE_PRODUCTS.Where(p => p.ExpenseID == shipment.ExpenseID.Value).ToList();
                foreach (var ep in eps)
                {
                    StockLedger.ReverseIssueLine(erp, shipment.WarehouseID, ep.ProductID, ep.Amount, ep.UnitID, ep.ID);
                    erp.EXPENSE_PRODUCTS.Remove(ep);
                }
                if (expense != null) expense.IsDeleted = true;
                erp.SaveChanges();
            }

            var lines = db.OrderLines.Where(l => l.OrderID == order.ID).ToList();
            foreach (var sl in shipmentLines)
            {
                var line = lines.First(l => l.ID == sl.OrderLineID);
                line.QtyShippedBase = Math.Max(0, line.QtyShippedBase - sl.QtyBase);
            }
            order.ShippedTotal = Math.Max(0, order.ShippedTotal - shipment.Total);
            DebtService.Reverse(db, LedgerDocTypes.Shipment, shipment.ID, now, userId, shipment.DocNo + ": " + shipment.CancelReason);

            bool anyShipped = lines.Any(l => l.QtyShippedBase > 0);
            Move(db, order, anyShipped ? OrderStatus.PartiallyShipped : OrderStatus.Confirmed, userId, now, shipment.DocNo + ": " + shipment.CancelReason);
            db.SaveChanges();
            if (order.WarehouseID.HasValue)
                Reserve(db, order.WarehouseID.Value, lines, now);
            db.SaveChanges();
        }

        /// <summary>Qisman jo'natilganda qolganidan voz kechish: rezerv yechiladi, buyurtma "jo'natildi".</summary>
        public static void CloseRemainder(DistributionUnitOfWork uow, int orderId, int userId, string reason, DateTime now)
        {
            var db = uow.Dst;
            var order = Load(db, orderId);
            if ((OrderStatus)order.OrderStatus != OrderStatus.PartiallyShipped)
                throw new DstValidationException(DstLocalization.T("Order_NotEditable"));
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < DocumentLifecycle.MinCancelReasonLength)
                throw new DstValidationException(DstLocalization.T("Error_CancelReasonRequired"));
            ReleaseReservations(db, orderId);
            Move(db, order, OrderStatus.Shipped, userId, now, reason.Trim());
            db.SaveChanges();
        }

        public static void MarkDelivered(DistributionUnitOfWork uow, int orderId, int userId, DateTime now)
        {
            var db = uow.Dst;
            var order = Load(db, orderId);
            if ((OrderStatus)order.OrderStatus == OrderStatus.Closed && order.DeliveredAt == null && order.ShippedTotal > 0)
            {
                // oldindan to'langan: jo'natishda qarz yopilib buyurtma "Yopildi" bo'lgan — yetkazish baribir qayd qilinadi
                Log(db, order, OrderStatus.Closed, OrderStatus.Closed, userId, now, DstLocalization.T("OrderStatus_Delivered"));
            }
            else
                Move(db, order, OrderStatus.Delivered, userId, now, null);
            order.DeliveredAt = now;
            NotificationService.ToCustomer(db, order.CustomerID, NotificationService.OrderDelivered,
                l => DstLocalization.TFor(l, "Notify_Delivered", order.DocNo, order.ShippedTotal.ToString("N0")), now);
            db.SaveChanges();
        }

        /// <summary>Bekor qilish — faqat jo'natilmagan buyurtma (jo'natilgani qaytarish hujjati bilan, S3).</summary>
        public static void Cancel(DistributionUnitOfWork uow, int orderId, int userId, string reason, DateTime now)
        {
            var db = uow.Dst;
            var order = Load(db, orderId);
            if (db.ShipmentLines.Any(s => s.Shipment.OrderID == orderId && s.Shipment.Status != DocumentStatus.Cancelled))
                throw new DstValidationException(DstLocalization.T("Order_HasShipments"));
            OrderStateMachine.EnsureCanMove((OrderStatus)order.OrderStatus, OrderStatus.Cancelled);
            ReleaseReservations(db, orderId);
            DocumentLifecycle.Cancel(order, userId, reason, now);
            Move(db, order, OrderStatus.Cancelled, userId, now, order.CancelReason);
            if (order.AgentUserID.HasValue && order.AgentUserID != userId)
                NotificationService.ToUser(db, order.AgentUserID, NotificationService.OrderCancelled,
                    l => DstLocalization.TFor(l, "Notify_OrderCancelled", order.DocNo, CustomerName(db, order.CustomerID), order.CancelReason), null, now);
            db.SaveChanges();
        }

        // ---------- yordamchilar ----------

        public static decimal MinOrderAmount(DistributionDbContext db)
        {
            var raw = db.Settings.Where(s => s.Key == DstSetting.MinOrderAmount).Select(s => s.Value).FirstOrDefault();
            return DstModelBinder.TryParseDecimal(raw ?? "0", out var v) ? v : 0;
        }

        /// <summary>Yangi buyurtma: katta summa yoki limitdan oshsa — rahbarlarga (ruxsat tugmasi bilan).</summary>
        private static void NotifyNewOrder(DistributionDbContext db, DstOrder order, string customer, int userId, DateTime now)
        {
            var large = DstSettings.Decimal(db, DstSetting.LargeOrderAmount);
            if (large > 0 && order.Total >= large)
                NotificationService.ToManagers(db, NotificationService.LargeOrder,
                    l => DstLocalization.TFor(l, "Notify_LargeOrder", order.DocNo, customer, order.Total.ToString("N0")), null, now, userId);
            var credit = OrderRules.CheckCredit(LedgerService.CreditState(db, order.CustomerID, now), order.Total);
            if (!credit.Ok)
                NotificationService.ToManagers(db, NotificationService.CreditOverLimit,
                    l => DstLocalization.TFor(l, "Notify_CreditOverLimit", order.DocNo, customer, order.Total.ToString("N0")) + Environment.NewLine
                         + string.Join(Environment.NewLine, credit.Problems),
                    "{\"action\":\"override\",\"orderId\":" + order.ID + "}", now, userId);
        }

        private static string CustomerName(DistributionDbContext db, int customerId) =>
            db.Customers.Where(c => c.ID == customerId).Select(c => c.Name).FirstOrDefault();

        private static DstOrder Load(DistributionDbContext db, int orderId)
        {
            return db.Orders.Find(orderId) ?? throw new DstValidationException(DstLocalization.T("Error_NotFound"));
        }

        private static void Move(DistributionDbContext db, DstOrder order, OrderStatus to, int userId, DateTime now, string note)
        {
            var from = (OrderStatus)order.OrderStatus;
            OrderStateMachine.EnsureCanMove(from, to);
            order.OrderStatus = (byte)to;
            Log(db, order, from, to, userId, now, note);
        }

        private static void Log(DistributionDbContext db, DstOrder order, OrderStatus? from, OrderStatus to, int userId, DateTime now, string note)
        {
            db.OrderStatusLog.Add(new DstOrderStatusLog
            {
                OrderID = order.ID, FromStatus = (byte?)from, ToStatus = (byte)to, UserID = userId, At = now,
                Note = note != null && note.Length > 500 ? note.Substring(0, 500) : note
            });
        }
    }
}
