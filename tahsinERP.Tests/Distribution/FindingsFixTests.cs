using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Areas.Distribution.Services.Money;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Areas.Distribution.Services.Warehouse;
using tahsinERP.Configuration;
using tahsinERP.Models;
using tahsinERP.Services.Common;

namespace tahsinERP.Tests.Distribution
{
    /// <summary>05_FINDINGS.md tuzatishlari — sof qoidalar.</summary>
    [TestClass]
    public class FindingsRulesTests
    {
        [TestMethod]
        public void K1_1_SpecialPermission_NeedsChange_NotView()
        {
            Assert.IsTrue(DstPermissions.Grants(true, false, false), "Admin/Developer — doim");
            Assert.IsFalse(DstPermissions.Grants(false, true, false), "faqat View (yangi rolga avtomatik) — huquq emas");
            Assert.IsTrue(DstPermissions.Grants(false, true, true));
            Assert.IsFalse(DstPermissions.Grants(false, false, true), "paketda yo'q");
        }

        [TestMethod]
        public void K1_2_Credit_CountsConfirmedNotShipped()
        {
            var credit = new CreditState { CreditLimit = 1000, Balance = 200, Committed = 500, HasProfile = true };
            Assert.IsTrue(OrderRules.CheckCredit(credit, 300).Ok, "200 + 500 + 300 = limit");
            var over = OrderRules.CheckCredit(credit, 301);
            Assert.IsFalse(over.Ok);
            Assert.AreEqual(300m, over.Available);
        }
    }

    /// <summary>05_FINDINGS.md tuzatishlari lokal bazada, bitta tranzaksiyada va rollback bilan.</summary>
    [TestClass]
    [TestCategory("Integration")]
    public class FindingsFixIntegrationTests
    {
        private static string _sql;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            try
            {
                _sql = DbConfig.GetSqlConnectionString();
                using (var c = new SqlConnection(_sql))
                using (var cmd = new SqlCommand("SELECT OBJECT_ID('dst.TRIPS', 'U')", c))
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
            if (_sql == null) Assert.Inconclusive("Lokal baza yoki migratsiya 026 yo'q.");
        }

        internal static DistributionUnitOfWork NewUow() => new DistributionUnitOfWork(new AuditActor(null, AuditActor.System));

        internal static int CashAccount(DistributionUnitOfWork uow, string name = "Test kassa")
        {
            var baseCurrency = CurrencyRateService.BaseCurrencyId() ?? uow.Erp.CURRENCIES.Select(c => c.ID).First();
            var account = new FIN_ACCOUNTS
            {
                Code = "TSTCASH" + Guid.NewGuid().ToString("N").Substring(0, 6), Name = name, CurrencyID = baseCurrency,
                IsCash = true, IsBank = false, IsActive = true, CreatedByUserID = uow.Dst.Users.First().ID, CreatedDT = DateTime.Now, IsDeleted = false
            };
            uow.Erp.FIN_ACCOUNTS.Add(account);
            uow.Erp.SaveChanges();
            return account.ID;
        }

        internal static int Draft(DistributionUnitOfWork uow, OrderFlowIntegrationTests.Fixture f, decimal qty, DateTime now) =>
            OrderService.SaveDraft(uow, new OrderInput
            {
                CustomerID = f.CustomerId, WarehouseID = f.WarehouseId,
                Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = qty } }
            }, OrderFlowIntegrationTests.Actor(f), now);

        [TestMethod]
        public void K1_2_SecondConfirmedOrder_BlockedByCommittedAmount()
        {
            using (var uow = NewUow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 1000000m);
                var now = DateTime.Now;
                var a = Draft(uow, f, 10, now);                                  // 550 000
                OrderService.Confirm(uow, a, OrderFlowIntegrationTests.Actor(f), null, now);
                Assert.AreEqual(550000m, LedgerService.Committed(uow.Dst, f.CustomerId));
                Assert.AreEqual(0m, LedgerService.Committed(uow.Dst, f.CustomerId, a), "o'zi hisobga olinmaydi");

                var b = Draft(uow, f, 10, now);                                  // 550 000 + 550 000 > 1 000 000
                var ex = Assert.ThrowsException<CreditBlockedException>(() => OrderService.Confirm(uow, b, OrderFlowIntegrationTests.Actor(f), null, now));
                StringAssert.Contains(ex.Message, DstLocalization.T("Credit_Committed", 550000m.ToString("N0")));

                // qisman jo'natilsa — jo'natilgani qarzga o'tadi, qolgani band bo'lib turadi
                OrderService.Ship(uow, a, new Dictionary<int, decimal> { { uow.Dst.OrderLines.Single(l => l.OrderID == a).ID, 4 } }, f.UserId, now);
                Assert.AreEqual(330000m, LedgerService.Committed(uow.Dst, f.CustomerId));
                Assert.AreEqual(220000m, LedgerService.CreditState(uow.Dst, f.CustomerId, now).Balance);
            }
        }

        [TestMethod]
        public void K1_3_K1_4_K2_15_Handover_AccountChoice_Cancel_EmployeeShortage()
        {
            using (var uow = NewUow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                var botAccount = CashAccount(uow, "Bot kassasi");
                var cashierAccount = CashAccount(uow, "Kassir kassasi");
                var employee = new HR_EMPLOYEES { TabNo = "T" + Guid.NewGuid().ToString("N").Substring(0, 6), FullName = "Agent", IsActive = true, CreatedDT = now, IsDeleted = false };
                uow.Erp.HR_EMPLOYEES.Add(employee);
                uow.Erp.SaveChanges();
                var scheme = db.KpiSchemes.Find(f.UserId);
                if (scheme == null) db.KpiSchemes.Add(scheme = new KpiScheme { AgentUserID = f.UserId, IsActive = true });
                scheme.EmployeeID = employee.ID;
                db.SaveChanges();

                var p1 = PaymentService.Receive(uow, new PaymentInput { CustomerID = f.CustomerId, Amount = 300000, Method = PaymentMethods.Cash }, f.UserId, AuditActor.Bot, now);
                var handoverId = PaymentService.SubmitHandover(uow, f.UserId, botAccount, f.UserId, null, now);
                var p2 = PaymentService.Receive(uow, new PaymentInput { CustomerID = f.CustomerId, Amount = 100000, Method = PaymentMethods.Cash }, f.UserId, AuditActor.Bot, now);
                Assert.AreEqual(300000m, db.CashHandovers.Find(handoverId).AmountDeclared, "keyingi to'lov hujjatga kirmaydi");

                // kassir boshqa kassani tanladi — tanlov hisobga olinadi; kamomad xodimga
                PaymentService.ConfirmHandover(uow, handoverId, 290000m, f.UserId, now, cashierAccount);
                Assert.AreEqual(cashierAccount, db.Payments.Find(p1).FinAccountID);
                Assert.IsNull(db.Payments.Find(p2).FinAccountID, "p2 hali agent qo'lida");
                var shortage = uow.Erp.FIN_TRANSACTIONS.Single(t => t.SourceDocType == DstFinancePosting.HandoverSource && t.SourceDocID == handoverId);
                Assert.AreEqual(10000m, shortage.Amount);
                Assert.AreEqual(DstFinancePosting.CounterpartyEmployee, shortage.CounterpartyType);
                Assert.AreEqual(employee.ID, shortage.CounterpartyID);

                Assert.ThrowsException<DstValidationException>(() => PaymentService.Cancel(uow, p1, f.UserId, "xato", now));

                // topshirishni bekor qilish → to'lov yana agentda → bekor qilinadi
                var finId = db.Payments.Find(p1).FinTransactionID.Value;
                PaymentService.CancelHandover(uow, handoverId, f.UserId, "kassir xato", now);
                Assert.AreEqual(DocumentStatus.Cancelled, db.CashHandovers.Find(handoverId).Status);
                Assert.IsTrue(uow.Erp.FIN_TRANSACTIONS.Find(finId).IsDeleted == true);
                Assert.IsTrue(uow.Erp.FIN_TRANSACTIONS.Find(shortage.ID).IsDeleted == true);
                Assert.AreEqual(2, PaymentService.OnHand(db, f.UserId).Count(p => p.ID == p1 || p.ID == p2));
                PaymentService.Cancel(uow, p1, f.UserId, "noto'g'ri mijoz", now);
                Assert.AreEqual(-100000m, LedgerService.CreditState(db, f.CustomerId, now).Balance);
            }
        }

        [TestMethod]
        public void K1_5_TransferDiffNotify_WithoutCostForManagersWithoutRight()
        {
            using (var uow = NewUow())
            {
                var db = uow.Dst;
                var now = DateTime.Now;
                var users = db.Users.Where(u => u.IsActive && u.IsDeleted != true).OrderBy(u => u.ID).Take(2).ToList();
                if (users.Count < 2) Assert.Inconclusive("Ikki foydalanuvchi kerak.");
                foreach (var a in db.TelegramAccounts.Where(a => a.IsActive && a.Role == BotRoles.Manager).ToList()) a.IsActive = false;
                long tg = 900000000 + new Random().Next(1000000);
                foreach (var u in users)
                    db.TelegramAccounts.Add(new TelegramAccount { TelegramUserId = ++tg, ChatId = tg, UserID = u.ID, Role = BotRoles.Manager, LinkedAt = now });
                db.SaveChanges();

                NotificationService.ToManagers(db, "TEST_COST", l => "summa 1 000", l => "summasiz", null, now, null, (id, _) => id == users[0].ID);
                db.SaveChanges();
                var sent = db.Notifications.Where(n => n.Type == "TEST_COST").ToList();
                Assert.AreEqual("summa 1 000", sent.Single(n => n.UserID == users[0].ID).Text);
                Assert.AreEqual("summasiz", sent.Single(n => n.UserID == users[1].ID).Text);
            }
        }

        [TestMethod]
        public void K1_5_K2_20_Inventory_CostKept_ShippingBlockedForCountedProduct_OpeningTakesNoMonthlyLock()
        {
            using (var uow = NewUow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                foreach (var c in db.InventoryCounts.Where(c => c.WarehouseID == f.WarehouseId && c.Status == DocumentStatus.Draft).ToList())
                    InventoryCountService.CancelDraft(db, c.ID, f.UserId, "test", now);
                var order = Draft(uow, f, 5, now);
                OrderService.Confirm(uow, order, OrderFlowIntegrationTests.Actor(f), null, now);

                var countId = InventoryCountService.Create(uow, f.WarehouseId, InventoryKinds.Opening, null, f.UserId, now);
                InventoryCountService.AddProduct(db, countId, f.ProductId);
                var line = db.InventoryCountRows.Single(l => l.CountID == countId && l.ProductID == f.ProductId);
                InventoryCountService.SaveCounts(db, countId, new[] { (line.ID, (decimal?)100m, (decimal?)41000m, (string)null) });
                InventoryCountService.SaveCounts(db, countId, new[] { (line.ID, (decimal?)100m, (decimal?)null, "huquqsiz") }, canSeeCost: false);
                Assert.AreEqual(41000m, db.InventoryCountRows.Find(line.ID).UnitCost, "tannarx huquqisiz o'chmaydi");

                Assert.ThrowsException<DstValidationException>(() => OrderService.Ship(uow, order, null, f.UserId, now), "sanalgan tovar chiqmaydi");

                var lockedBefore = uow.Erp.INVENTORY_ADJUSTMENTS.Count(a => a.WarehouseID == f.WarehouseId);
                foreach (var other in db.InventoryCountRows.Where(l => l.CountID == countId && l.CountedQty == null).ToList()) db.InventoryCountRows.Remove(other);
                db.SaveChanges();
                Task.Run(() => InventoryCountService.PostAsync(uow, countId, true, f.UserId, now)).GetAwaiter().GetResult();
                Assert.AreEqual(lockedBefore, uow.Erp.INVENTORY_ADJUSTMENTS.Count(a => a.WarehouseID == f.WarehouseId), "boshlang'ich — oylik qulfsiz");
                OrderService.Ship(uow, order, null, f.UserId, now);
            }
        }
    }
}
