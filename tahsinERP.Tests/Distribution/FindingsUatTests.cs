using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Areas.Distribution.Services.Delivery;
using tahsinERP.Areas.Distribution.Services.Money;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Models;
using tahsinERP.Services.Stock;

namespace tahsinERP.Tests.Distribution
{
    /// <summary>05_FINDINGS.md 38–49 (UAT yozilganda topilgan) — sof qoidalar.</summary>
    [TestClass]
    public class FindingsUatRulesTests
    {
        [TestMethod]
        public void F38_ImportSampleBarcodes_PassEanCheck()
        {
            var samples = ImportService.ProductColumns.Concat(ImportService.UnitColumns)
                .Where(c => c.Key == "barcode" && c.Example != null).Select(c => c.Example).ToList();
            Assert.AreEqual(2, samples.Count);
            foreach (var code in samples) Assert.AreEqual(code, DirectoryRules.NormalizeBarcode(code));
        }

        [TestMethod]
        public void F43_WarehouseScope_WithNulls_ShowsOrdersWithoutWarehouse()
        {
            var rows = new[] { new { W = (int?)1 }, new { W = (int?)null }, new { W = (int?)2 } }.AsQueryable();
            var scope = ScopeFilter.FromRows(new[] { 1 }, false);
            Assert.AreEqual(1, scope.Apply(rows, x => x.W).Count(), "odatiy: NULL yashirin");
            Assert.AreEqual(2, scope.WithNulls().Apply(rows, x => x.W).Count(), "buyurtma: ombori tanlanmagani ham");
            Assert.AreEqual(3, ScopeFilter.Unrestricted.WithNulls().Apply(rows, x => x.W).Count());
        }

        [TestMethod]
        public void F39_PartiallyShipped_CannotBeDelivered()
        {
            Assert.IsFalse(OrderStateMachine.CanMove(OrderStatus.PartiallyShipped, OrderStatus.Delivered));
            Assert.IsTrue(OrderStateMachine.CanMove(OrderStatus.Shipped, OrderStatus.Delivered));
        }
    }

    /// <summary>05_FINDINGS.md 38–49 — lokal bazada, rollback bilan.</summary>
    [TestClass]
    [TestCategory("Integration")]
    public class FindingsUatIntegrationTests
    {
        private static bool _ok;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            try
            {
                using (var c = new System.Data.SqlClient.SqlConnection(tahsinERP.Configuration.DbConfig.GetSqlConnectionString()))
                using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT OBJECT_ID('dst.TRIPS', 'U')", c))
                {
                    c.Open();
                    _ok = !(cmd.ExecuteScalar() is DBNull);
                }
            }
            catch (Exception)
            {
                _ok = false;
            }
        }

        [TestInitialize]
        public void RequireDatabase()
        {
            if (!_ok) Assert.Inconclusive("Lokal baza yoki migratsiya 026 yo'q.");
        }

        private static DistributionUnitOfWork Uow() => FindingsFixIntegrationTests.NewUow();

        [TestMethod]
        public void F40_41_CancelPayment_ReopensClosedOrder_WebCashGoesToAgent()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                var agentUser = db.Users.Where(u => u.IsActive && u.IsDeleted != true && u.ID != f.UserId).Select(u => u.ID).FirstOrDefault();
                if (agentUser == 0) Assert.Inconclusive("Ikkinchi foydalanuvchi kerak.");
                db.CustomerProfiles.Find(f.CustomerId).AgentUserID = agentUser;
                db.SaveChanges();
                var order = FindingsFixIntegrationTests.Draft(uow, f, 2, now);              // 110 000
                OrderService.Confirm(uow, order, OrderFlowIntegrationTests.Actor(f), null, now);
                OrderService.Ship(uow, order, null, f.UserId, now);

                // 41: kassir (f.UserId) web'da "agent qo'lida" — pul agentda
                var cash = PaymentService.Receive(uow, new PaymentInput { CustomerID = f.CustomerId, OrderID = order, Amount = 110000, Method = PaymentMethods.Cash },
                    f.UserId, AuditActor.Web, now);
                Assert.AreEqual(agentUser, db.Payments.Find(cash).ReceivedByUserID);
                Assert.AreEqual((byte)OrderStatus.Closed, db.Orders.Find(order).OrderStatus);

                // 40: to'lov bekor — buyurtma yana ochiq (jo'natildi)
                PaymentService.Cancel(uow, cash, f.UserId, "xato kiritildi", now);
                Assert.AreEqual((byte)OrderStatus.Shipped, db.Orders.Find(order).OrderStatus);
            }
        }

        [TestMethod]
        public void F42_45_ManualDeliver_BlockedOnTrip_PickupSendsDelivered()
        {
            using (var uow = Uow())
            {
                var w = DeliveryIntegrationTests.Arrange(uow);
                var f = w.F;
                var db = uow.Dst;
                var now = DateTime.Now;
                var s1 = DeliveryIntegrationTests.Ship(uow, w, f.CustomerId, 2, now);
                var tripId = TripService.Save(uow, new TripInput { DocDate = now, VehicleID = w.VehicleId, ExpeditorUserID = f.UserId, ShipmentIDs = { s1 } }, f.UserId, now);
                var orderId = db.Shipments.Find(s1).OrderID;
                Assert.ThrowsException<DstValidationException>(() => OrderService.MarkDelivered(uow, orderId, f.UserId, now), "reysda — reys orqali");
                TripService.Cancel(uow, tripId, f.UserId, "test", now);
                OrderService.MarkDelivered(uow, orderId, f.UserId, now);                      // reysdan chiqdi — qo'lda mumkin

                // 45: o'zi olib ketish — mijozga bitta "yetkazildi"
                long tg = 600000000 + new Random().Next(1000000);
                db.TelegramAccounts.Add(new TelegramAccount { TelegramUserId = tg, ChatId = tg, CustomerID = w.Customer2, Role = BotRoles.Customer, LinkedAt = now });
                db.SaveChanges();
                var pickup = OrderService.SaveDraft(uow, new OrderInput
                {
                    CustomerID = w.Customer2, WarehouseID = f.WarehouseId, IsPickup = true, Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = 1 } }
                }, OrderFlowIntegrationTests.Actor(f), now);
                OrderService.Confirm(uow, pickup, OrderFlowIntegrationTests.Actor(f), null, now);
                OrderService.Ship(uow, pickup, null, f.UserId, now);
                var types = db.Notifications.Where(n => n.CustomerID == w.Customer2 && n.CreatedAt >= now).Select(n => n.Type).ToList();
                CollectionAssert.Contains(types, NotificationService.OrderDelivered);
                CollectionAssert.DoesNotContain(types, NotificationService.OrderShipped);
            }
        }

        [TestMethod]
        public void F44_Supervisor_SeesUnassignedCustomers()
        {
            using (var uow = Uow())
            {
                var db = uow.Dst;
                var users = db.Users.Where(u => u.IsActive && u.IsDeleted != true).OrderBy(u => u.ID).Select(u => u.ID).Take(2).ToList();
                if (users.Count < 2) Assert.Inconclusive("Ikki foydalanuvchi kerak.");
                var team = new SalesTeam { Name = "T jamoa " + Guid.NewGuid().ToString("N").Substring(0, 5), SupervisorUserID = users[0], IsActive = true };
                db.SalesTeams.Add(team);
                db.SaveChanges();
                var filter = new ScopeService(db).AgentsFor(users[0], false);
                var rows = new[] { new { A = (int?)users[0] }, new { A = (int?)null }, new { A = (int?)users[1] + 1000000 } }.AsQueryable();
                var visible = filter.Apply(rows, x => x.A).ToList();
                Assert.AreEqual(2, visible.Count, "o'zi va agentsiz mijoz");
                Assert.IsTrue(visible.Any(x => x.A == null));
            }
        }

        [TestMethod]
        public void F46_DeletedDraftNumber_NotReused()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                var a = FindingsFixIntegrationTests.Draft(uow, f, 1, now);
                var docA = db.Orders.Find(a).DocNo;
                OrderService.DeleteDraft(uow, a);
                var b = FindingsFixIntegrationTests.Draft(uow, f, 1, now);
                Assert.AreNotEqual(docA, db.Orders.Find(b).DocNo);
            }
        }

        [TestMethod]
        public void F48_ErpExpense_CannotGoBelowDistributionReservation()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var now = DateTime.Now;
                var order = FindingsFixIntegrationTests.Draft(uow, f, 95, now);
                OrderService.Confirm(uow, order, OrderFlowIntegrationTests.Actor(f), null, now);   // 95 rezervda, qoldiq 100
                var erp = uow.Erp;
                var unit = erp.PRODUCTS.Where(p => p.ID == f.ProductId).Select(p => p.UnitID).First();
                var expense = new EXPENS
                {
                    DocNo = "TST-EXP-" + f.ProductId, SenderWHID = f.WarehouseId, IssueDateTime = now, CurrencyID = WarehouseAccess.DefaultCurrencyId(erp),
                    IsDeleted = false, SendStatus = true, Description = "test"
                };
                erp.EXPENSES.Add(expense);
                erp.SaveChanges();
                var ex = Assert.ThrowsException<InvalidOperationException>(() => ExpenseDocumentService.PostLinesAsync(erp, expense, new List<tahsinERP.ViewModels.ExpenseProductViewModel>
                {
                    new tahsinERP.ViewModels.ExpenseProductViewModel { ProductID = f.ProductId, Amount = 10, UnitID = unit }
                }).GetAwaiter().GetResult());
                StringAssert.Contains(ex.Message, "rezervda");
            }
        }
    }
}
