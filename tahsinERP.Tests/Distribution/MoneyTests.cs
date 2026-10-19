using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Money;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Configuration;
using tahsinERP.Models;
using tahsinERP.Services.Common;

namespace tahsinERP.Tests.Distribution
{
    [TestClass]
    public class DebtMathTests
    {
        private static readonly DateTime Today = new DateTime(2026, 10, 19);

        private static DebitItem D(int daysAgoDue, decimal amount, int? order = null) =>
            new DebitItem { Date = Today.AddDays(-daysAgoDue - 10), DueDate = Today.AddDays(-daysAgoDue), Amount = amount, OrderID = order };

        [TestMethod]
        public void Aging_CreditsCoverOldestFirst()
        {
            var debits = new[] { D(70, 100), D(40, 200), D(20, 300), D(5, 400), D(-3, 500) };   // oxirgisi hali muddatsiz
            var a = DebtMath.Aging(debits, 250m, Today);
            Assert.AreEqual(0m, a.Over60, "eng eskisi to'liq yopildi");
            Assert.AreEqual(50m, a.Days31To60);
            Assert.AreEqual(300m, a.Days8To30);
            Assert.AreEqual(400m, a.Days1To7);
            Assert.AreEqual(500m, a.NotDue);
            Assert.AreEqual(750m, a.Overdue);
            Assert.AreEqual(1250m, a.Total);
            Assert.AreEqual(40, a.MaxOverdueDays);

            var advance = DebtMath.Aging(new[] { D(1, 100) }, 150m, Today);
            Assert.AreEqual(0m, advance.Total);
            Assert.AreEqual(50m, advance.Advance);
        }

        [TestMethod]
        public void SettledOrders_OnlyFullyCovered()
        {
            var debits = new[] { D(30, 100, 1), D(20, 200, 2), D(10, 50, 2), D(5, 70, 3) };
            CollectionAssert.AreEquivalent(new[] { 1 }, DebtMath.SettledOrders(debits, 250m).ToArray());
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, DebtMath.SettledOrders(debits, 350m).ToArray());
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, DebtMath.SettledOrders(debits, 999m).ToArray());
        }

        [TestMethod]
        public void Net_RemovesReversedPairs()
        {
            var rows = new List<CustomerLedgerEntry>
            {
                new CustomerLedgerEntry { DocType = "SHIPMENT", DocID = 1, Debit = 500, EntryDate = Today, DueDate = Today, OrderID = 7 },
                new CustomerLedgerEntry { DocType = "SHIPMENT", DocID = 2, Debit = 300, EntryDate = Today, DueDate = Today, OrderID = 7 },
                new CustomerLedgerEntry { DocType = "SHIPMENT_REV", DocID = 2, Credit = 300, EntryDate = Today },
                new CustomerLedgerEntry { DocType = "PAYMENT", DocID = 9, Credit = 100, EntryDate = Today },
                new CustomerLedgerEntry { DocType = "PAYMENT", DocID = 10, Credit = 50, EntryDate = Today },
                new CustomerLedgerEntry { DocType = "PAYMENT_REV", DocID = 10, Debit = 50, EntryDate = Today }
            };
            var net = DebtService.Net(rows);
            Assert.AreEqual(1, net.Debits.Count);
            Assert.AreEqual(500m, net.Debits[0].Amount);
            Assert.AreEqual(100m, net.Credits);
        }
    }

    /// <summary>S3 pul oqimi lokal bazada, bitta tranzaksiyada va rollback bilan.</summary>
    [TestClass]
    [TestCategory("Integration")]
    public class MoneyFlowIntegrationTests
    {
        private static string _sql;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            try
            {
                _sql = DbConfig.GetSqlConnectionString();
                using (var c = new SqlConnection(_sql))
                using (var cmd = new SqlCommand("SELECT OBJECT_ID('dst.D_PAYMENTS', 'U')", c))
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
            if (_sql == null) Assert.Inconclusive("Lokal baza yoki migratsiya 019 yo'q.");
        }

        private static int CashAccount(DistributionUnitOfWork uow)
        {
            var baseCurrency = CurrencyRateService.BaseCurrencyId() ?? uow.Erp.CURRENCIES.Select(c => c.ID).First();
            var account = new FIN_ACCOUNTS
            {
                Code = "TSTCASH" + Guid.NewGuid().ToString("N").Substring(0, 6), Name = "Test kassa", CurrencyID = baseCurrency,
                IsCash = true, IsBank = false, IsActive = true, CreatedByUserID = uow.Dst.Users.First().ID, CreatedDT = DateTime.Now, IsDeleted = false
            };
            uow.Erp.FIN_ACCOUNTS.Add(account);
            uow.Erp.SaveChanges();
            return account.ID;
        }

        private static (int OrderId, int LineId, int ShipmentId) ShipOrder(DistributionUnitOfWork uow, OrderFlowIntegrationTests.Fixture f, decimal qty, DateTime now)
        {
            var orderId = OrderService.SaveDraft(uow, new OrderInput
            {
                CustomerID = f.CustomerId, WarehouseID = f.WarehouseId,
                Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = qty } }
            }, OrderFlowIntegrationTests.Actor(f), now);
            OrderService.Confirm(uow, orderId, OrderFlowIntegrationTests.Actor(f), null, now);
            var lineId = uow.Dst.OrderLines.Single(l => l.OrderID == orderId).ID;
            var shipmentId = OrderService.Ship(uow, orderId, null, f.UserId, now);
            return (orderId, lineId, shipmentId);
        }

        [TestMethod]
        public void AgentCash_Handover_WithShortage_AndOfficePaymentClosesOrder()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                var account = CashAccount(uow);
                var (orderId, _, _) = ShipOrder(uow, f, 10, now);                 // qarz 550 000

                // naqd pul hisobsiz — faqat naqd; karta uchun hisob majburiy
                Assert.ThrowsException<DstValidationException>(() => PaymentService.Receive(uow,
                    new PaymentInput { CustomerID = f.CustomerId, Amount = 1, Method = PaymentMethods.Click }, f.UserId, AuditActor.Web, now));

                var p1 = PaymentService.Receive(uow, new PaymentInput { CustomerID = f.CustomerId, OrderID = orderId, Amount = 300000, Method = PaymentMethods.Cash },
                    f.UserId, AuditActor.Bot, now);
                Assert.AreEqual(1, PaymentService.OnHand(db, f.UserId).Count(p => p.ID == p1));
                Assert.AreEqual(250000m, LedgerService.CreditState(db, f.CustomerId, now).Balance);
                Assert.AreNotEqual((byte)OrderStatus.Closed, db.Orders.Find(orderId).OrderStatus);

                var handoverId = PaymentService.SubmitHandover(uow, f.UserId, account, f.UserId, null, now);
                Assert.ThrowsException<DstValidationException>(() => PaymentService.SubmitHandover(uow, f.UserId, account, f.UserId, null, now),
                    "qo'lida pul qolmadi");
                PaymentService.ConfirmHandover(uow, handoverId, 290000m, f.UserId, now);
                var payment = db.Payments.Find(p1);
                Assert.AreEqual(account, payment.FinAccountID);
                var fin = uow.Erp.FIN_TRANSACTIONS.Find(payment.FinTransactionID.Value);
                Assert.AreEqual(300000m, fin.Amount);
                Assert.AreEqual((byte)1, fin.Direction);
                Assert.AreEqual(f.CustomerId, fin.CounterpartyID);
                var shortage = uow.Erp.FIN_TRANSACTIONS.Single(t => t.SourceDocType == DstFinancePosting.HandoverSource && t.SourceDocID == handoverId);
                Assert.AreEqual(10000m, shortage.Amount);
                Assert.AreEqual((byte)2, shortage.Direction);
                Assert.AreEqual(-10000m, db.CashHandovers.Find(handoverId).Difference);

                Assert.ThrowsException<DstValidationException>(() => PaymentService.Cancel(uow, p1, f.UserId, "xato", now),
                    "kassaga topshirilgan to'lov bekor qilinmaydi");

                // ofisda qolgan qarz to'landi — buyurtma yopiladi
                var p2 = PaymentService.Receive(uow, new PaymentInput { CustomerID = f.CustomerId, Amount = 250000, Method = PaymentMethods.Click, FinAccountID = account },
                    f.UserId, AuditActor.Web, now);
                Assert.AreEqual(0m, LedgerService.CreditState(db, f.CustomerId, now).Balance);
                Assert.AreEqual((byte)OrderStatus.Closed, db.Orders.Find(orderId).OrderStatus);
                Assert.IsNotNull(db.Payments.Find(p2).FinTransactionID);

                PaymentService.Cancel(uow, p2, f.UserId, "noto'g'ri mijoz", now);
                Assert.AreEqual(250000m, LedgerService.CreditState(db, f.CustomerId, now).Balance);
                Assert.IsTrue(uow.Erp.FIN_TRANSACTIONS.Find(db.Payments.Find(p2).FinTransactionID.Value).IsDeleted == true);

                var statement = DebtService.BuildStatement(db, f.CustomerId, now.AddDays(-1), now);
                Assert.AreEqual(0m, statement.Opening);
                Assert.AreEqual(250000m, statement.Closing);
                Assert.AreEqual(4, statement.Rows.Count);   // jo'natish, 2 to'lov, bekor qilish
            }
        }

        [TestMethod]
        public void Return_ByShipment_RestoresStockAndDebt_CancelShipmentRestoresReservation()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                var (orderId, lineId, shipmentId) = ShipOrder(uow, f, 10, now);
                var shipmentLine = db.ShipmentLines.Single(l => l.ShipmentID == shipmentId);
                decimal OnHand() => StockService.Levels(db, new[] { f.ProductId }, new[] { f.WarehouseId }).Single().OnHand;
                Assert.AreEqual(90m, OnHand());

                var input = new ReturnInput
                {
                    CustomerID = f.CustomerId, WarehouseID = f.WarehouseId, LocationID = f.LocationId,
                    ReasonID = db.Reasons.Where(r => r.Type == ReasonTypes.Return).Select(r => (int?)r.ID).FirstOrDefault(),
                    Lines = { new ReturnLineInput { ShipmentLineID = shipmentLine.ID, QtyBase = 4 } }
                };
                var returnId = ReturnService.Create(uow, input, f.UserId, AuditActor.Web, now);
                var ret = db.Returns.Find(returnId);
                Assert.AreEqual(220000m, ret.Total);
                Assert.AreEqual(160000m, ret.CostTotal);
                Assert.AreEqual(94m, OnHand());
                Assert.AreEqual(330000m, LedgerService.CreditState(db, f.CustomerId, now).Balance);

                Assert.ThrowsException<DstValidationException>(() => ReturnService.Create(uow, new ReturnInput
                {
                    CustomerID = f.CustomerId, WarehouseID = f.WarehouseId, LocationID = f.LocationId,
                    Lines = { new ReturnLineInput { ShipmentLineID = shipmentLine.ID, QtyBase = 7 } }
                }, f.UserId, AuditActor.Web, now), "10 jo'natildi, 4 qaytdi — 7 bo'lmaydi");

                Assert.ThrowsException<DstValidationException>(() => OrderService.CancelShipment(uow, shipmentId, f.UserId, "xato jo'natildi", now),
                    "qaytarishi bor jo'natish bekor qilinmaydi");

                ReturnService.Cancel(uow, returnId, f.UserId, "xato kiritildi", now);
                Assert.AreEqual(90m, OnHand());
                Assert.AreEqual(550000m, LedgerService.CreditState(db, f.CustomerId, now).Balance);

                OrderService.CancelShipment(uow, shipmentId, f.UserId, "xato jo'natildi", now);
                Assert.AreEqual(100m, OnHand());
                var order = db.Orders.Find(orderId);
                Assert.AreEqual((byte)OrderStatus.Confirmed, order.OrderStatus);
                Assert.AreEqual(0m, order.ShippedTotal);
                Assert.AreEqual(10m, db.StockReservations.Single(r => r.OrderLineID == lineId).QtyBase, "yana rezervda");
                Assert.AreEqual(0m, LedgerService.CreditState(db, f.CustomerId, now).Balance);
                Assert.AreEqual(0m, DebtService.Aging(db, f.CustomerId, now).Total, "bekor qilingan jo'natish qarzga kirmaydi");
                Assert.IsTrue(uow.Erp.EXPENSES.Find(db.Shipments.Find(shipmentId).ExpenseID.Value).IsDeleted);
            }
        }
    }
}
