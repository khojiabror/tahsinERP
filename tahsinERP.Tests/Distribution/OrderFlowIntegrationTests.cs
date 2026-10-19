using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Configuration;
using tahsinERP.Models;
using tahsinERP.Services.Stock;

namespace tahsinERP.Tests.Distribution
{
    /// <summary>
    /// S2 to'liq oqimi lokal bazada, BITTA tranzaksiyada va oxirida rollback (bazada iz qolmaydi):
    /// kirim (FIFO lot) → qoralama → tasdiq (limit, rezerv) → qisman jo'natish → to'liq jo'natish → yetkazildi.
    /// Ombor chiqimi mavjud StockLedger orqali, tannarx FIFO'dan, qarz CUSTOMER_LEDGER'ga.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class OrderFlowIntegrationTests
    {
        private static string _sql;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            try
            {
                _sql = DbConfig.GetSqlConnectionString();
                using (var c = new SqlConnection(_sql))
                using (var cmd = new SqlCommand("SELECT OBJECT_ID('dst.D_ORDERS', 'U')", c))
                {
                    c.Open();
                    if (cmd.ExecuteScalar() is DBNull) _sql = null;
                }
            }
            catch (Exception)
            {
                _sql = null;
            }
        }

        [TestInitialize]
        public void RequireDatabase()
        {
            if (_sql == null) Assert.Inconclusive("Lokal baza yoki migratsiya 018 yo'q.");
        }

        private sealed class Fixture
        {
            public int WarehouseId, ProductId, CustomerId, UserId, PriceTypeId;
        }

        /// <summary>Shu tranzaksiyada: yangi tovar, narx, DLOC'siz omborga 100 dona kirim (birlik narxi 40 000), mijoz.</summary>
        private static Fixture Arrange(DistributionUnitOfWork uow, decimal creditLimit)
        {
            var erp = uow.Erp;
            var db = uow.Dst;
            var now = DateTime.Now;
            var warehouses = erp.WAREHOUSES.Where(w => w.IsDeleted != true).ToList();
            var warehouse = warehouses.FirstOrDefault(w => !StockLedger.IsDlocWarehouse(erp, w.ID)) ?? warehouses.FirstOrDefault();
            if (warehouse == null) Assert.Inconclusive("Ombor yo'q.");
            int? locationId = null;
            if (StockLedger.IsDlocWarehouse(erp, warehouse.ID))
            {
                // DLOC ombor: shu tranzaksiyada vaqtinchalik, sig'imi katta joylashuv
                var location = new WAREHOUSE_LOCATIONS
                {
                    WarehouseID = warehouse.ID, LocationCode = "TST" + Guid.NewGuid().ToString("N").Substring(0, 8), LocationName = "test",
                    MinCapacity = 0, MaxCapacity = 1000000, AllowMixedLots = true, AllowMixedProducts = true, IsActive = true,
                    IsDeleted = false, CreatedAt = now
                };
                erp.WAREHOUSE_LOCATIONS.Add(location);
                erp.SaveChanges();
                locationId = location.ID;
            }
            var shop = erp.SHOPS.Where(s => s.IsDeleted != true).Select(s => (int?)s.ID).FirstOrDefault();
            if (shop == null) Assert.Inconclusive("SHOPS bo'sh.");
            var unitId = erp.UNITS.Where(u => u.IsDeleted != true).Select(u => u.ID).First();
            var user = db.Users.First(u => u.IsDeleted != true);

            var product = new PRODUCT
            {
                PNo = "T" + Guid.NewGuid().ToString("N").Substring(0, 12), Name = "Test sement", UnitID = unitId, ShopID = shop.Value,
                IsDeleted = false, IsFinishedGood = true, IsComponent = false, IsRawMaterial = false
            };
            erp.PRODUCTS.Add(product);
            erp.SaveChanges();

            var income = new INCOME
            {
                DocNo = "TST-" + product.PNo, ReceivingWHID = warehouse.ID, IssueDateTime = now.AddMinutes(-10),
                CurrencyID = WarehouseAccess.DefaultCurrencyId(erp), IsDeleted = false, RecieveStatus = true, Description = "test"
            };
            erp.INCOMES.Add(income);
            erp.SaveChanges();
            var ip = new INCOME_PRODUCTS { IncomeID = income.ID, ProductID = product.ID, Amount = 100, PiecePrice = 40000, UnitID = unitId, Comment = "" };
            erp.INCOME_PRODUCTS.Add(ip);
            erp.SaveChanges();
            StockLedger.ReceiveLine(erp, warehouse.ID, income.ID, ip.ID, product.ID, 100, unitId, locationId, income.IssueDateTime);
            erp.SaveChanges();

            var priceType = db.PriceTypes.First(t => t.IsDefault);
            new PricingService(db).SetPrice(priceType.ID, product.ID, 55000m, 50000m, null, now.AddMinutes(-5));
            db.SaveChanges();

            var customerId = CustomerService.Save(uow, new CustomerInput
            {
                Name = "Flow test " + product.PNo, CreditLimit = creditLimit, PaymentDays = 10
            }, user.ID, AuditActor.Web, now);

            return new Fixture { WarehouseId = warehouse.ID, ProductId = product.ID, CustomerId = customerId, UserId = user.ID, PriceTypeId = priceType.ID };
        }

        private static OrderActor Actor(Fixture f, decimal? maxDiscount = 5, bool overrideCredit = false) =>
            new OrderActor { UserId = f.UserId, MaxDiscountPct = maxDiscount, CanOverrideCredit = overrideCredit };

        [TestMethod]
        public void FullFlow_ReserveShipPartiallyThenFully_PostsCostAndDebt()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var f = Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;

                var orderId = OrderService.SaveDraft(uow, new OrderInput
                {
                    CustomerID = f.CustomerId, WarehouseID = f.WarehouseId, DiscountPct = 0,
                    Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = 30, DiscountPct = 5 } }
                }, Actor(f), now);
                var order = db.Orders.Find(orderId);
                Assert.IsTrue(order.DocNo.StartsWith("D-" + now.Year + "-"));
                Assert.AreEqual((byte)OrderStatus.New, order.OrderStatus);
                Assert.AreEqual(1567500m, order.Total);          // 30 × 55 000 × 0.95

                // chegirma limitdan oshsa va min narxdan past bo'lsa — rad
                Assert.ThrowsException<DstValidationException>(() => OrderService.SaveDraft(uow, new OrderInput
                {
                    ID = orderId, CustomerID = f.CustomerId,
                    Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = 30, DiscountPct = 6 } }
                }, Actor(f), now));
                Assert.ThrowsException<DstValidationException>(() => OrderService.SaveDraft(uow, new OrderInput
                {
                    ID = orderId, CustomerID = f.CustomerId,
                    Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = 30, DiscountPct = 10 } }
                }, Actor(f, maxDiscount: 20), now), "55 000 × 0.9 = 49 500 < min 50 000");

                OrderService.Confirm(uow, orderId, Actor(f), null, now);
                order = db.Orders.Find(orderId);
                Assert.AreEqual((byte)OrderStatus.Confirmed, order.OrderStatus);
                Assert.AreEqual(DocumentStatus.Posted, order.Status);
                var line = db.OrderLines.Single(l => l.OrderID == orderId);
                Assert.AreEqual(30m, db.StockReservations.Single(r => r.OrderLineID == line.ID).QtyBase);
                var level = StockService.Levels(db, new[] { f.ProductId }, new[] { f.WarehouseId }).Single();
                Assert.AreEqual(100m, level.OnHand);
                Assert.AreEqual(70m, level.Available);

                // qisman: 12 dona
                var s1 = OrderService.Ship(uow, orderId, new Dictionary<int, decimal> { { line.ID, 12 } }, f.UserId, now);
                order = db.Orders.Find(orderId);
                Assert.AreEqual((byte)OrderStatus.PartiallyShipped, order.OrderStatus);
                var shipment = db.Shipments.Find(s1);
                Assert.AreEqual(627000m, shipment.Total);          // 12 × 52 250
                Assert.AreEqual(480000m, shipment.CostTotal);      // 12 × 40 000 (FIFO)
                Assert.IsNotNull(shipment.ExpenseID);
                Assert.AreEqual(18m, db.StockReservations.Single(r => r.OrderLineID == line.ID).QtyBase);
                level = StockService.Levels(db, new[] { f.ProductId }, new[] { f.WarehouseId }).Single();
                Assert.AreEqual(88m, level.OnHand);
                var credit = LedgerService.CreditState(db, f.CustomerId, now);
                Assert.AreEqual(627000m, credit.Balance);
                Assert.AreEqual(0m, credit.Overdue);
                var entry = db.CustomerLedger.Single(x => x.CustomerID == f.CustomerId);
                Assert.AreEqual(now.Date.AddDays(10), entry.DueDate);

                // buyurtmadan ko'p — rad
                Assert.ThrowsException<DstValidationException>(() =>
                    OrderService.Ship(uow, orderId, new Dictionary<int, decimal> { { line.ID, 19 } }, f.UserId, now));

                // qolgani (rezerv bo'yicha)
                OrderService.Ship(uow, orderId, null, f.UserId, now);
                order = db.Orders.Find(orderId);
                Assert.AreEqual((byte)OrderStatus.Shipped, order.OrderStatus);
                Assert.AreEqual(order.Total, order.ShippedTotal);
                Assert.IsFalse(db.StockReservations.Any(r => r.OrderLineID == line.ID));
                Assert.AreEqual(70m, StockService.Levels(db, new[] { f.ProductId }, new[] { f.WarehouseId }).Single().OnHand);
                Assert.AreEqual(1567500m, LedgerService.CreditState(db, f.CustomerId, now).Balance);

                Assert.ThrowsException<DstValidationException>(() => OrderService.Cancel(uow, orderId, f.UserId, "sabab bor", now),
                    "jo'natilgan buyurtma bekor qilinmaydi");
                OrderService.MarkDelivered(uow, orderId, f.UserId, now);
                Assert.AreEqual((byte)OrderStatus.Delivered, db.Orders.Find(orderId).OrderStatus);
                Assert.IsTrue(db.OrderStatusLog.Count(x => x.OrderID == orderId) >= 5);
                // Commit yo'q — rollback
            }
        }

        [TestMethod]
        public void Confirm_CreditBlock_OverrideAndCancelReleasesReservation()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var f = Arrange(uow, 0m);   // faqat oldindan to'lov
                var db = uow.Dst;
                var now = DateTime.Now;
                var orderId = OrderService.SaveDraft(uow, new OrderInput
                {
                    CustomerID = f.CustomerId, WarehouseID = null,
                    Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = 150 } }   // 100 bor — 50 "kutmoqda"
                }, Actor(f), now);

                var blocked = Assert.ThrowsException<CreditBlockedException>(() => OrderService.Confirm(uow, orderId, Actor(f), null, now));
                Assert.AreEqual(1, blocked.Problems.Count);
                Assert.ThrowsException<CreditBlockedException>(() => OrderService.Confirm(uow, orderId, Actor(f, overrideCredit: true), "  ", now),
                    "ruxsat izohsiz berilmaydi");

                OrderService.Confirm(uow, orderId, Actor(f, overrideCredit: true), "Rahbar ruxsati", now);
                var order = db.Orders.Find(orderId);
                Assert.IsNotNull(order.WarehouseID, "ombor avtomatik tanlandi");
                Assert.AreEqual("Rahbar ruxsati", order.CreditOverrideNote);
                var line = db.OrderLines.Single(l => l.OrderID == orderId);
                var reserved = db.StockReservations.Single(r => r.OrderLineID == line.ID).QtyBase;
                Assert.AreEqual(100m, reserved, "mavjud qism rezerv, qolgani kutmoqda");

                Assert.ThrowsException<DstValidationException>(() => OrderService.Cancel(uow, orderId, f.UserId, "", now));
                OrderService.Cancel(uow, orderId, f.UserId, "Mijoz voz kechdi", now);
                order = db.Orders.Find(orderId);
                Assert.AreEqual((byte)OrderStatus.Cancelled, order.OrderStatus);
                Assert.AreEqual(DocumentStatus.Cancelled, order.Status);
                Assert.IsFalse(db.StockReservations.Any(r => r.OrderLineID == line.ID));
            }
        }

        [TestMethod]
        public void SaveDraft_IdempotentByClientRequestId_AndDeleteDraft()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var f = Arrange(uow, 1000000m);
                var request = Guid.NewGuid();
                var input = new OrderInput
                {
                    CustomerID = f.CustomerId, ClientRequestId = request,
                    Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = 2 } }
                };
                var id1 = OrderService.SaveDraft(uow, input, Actor(f), DateTime.Now);
                var id2 = OrderService.SaveDraft(uow, input, Actor(f), DateTime.Now);
                Assert.AreEqual(id1, id2, "bot qayta yuborsa — o'sha buyurtma");

                OrderService.DeleteDraft(uow, id1);
                Assert.IsNull(uow.Dst.Orders.Find(id1));
            }
        }
    }
}
