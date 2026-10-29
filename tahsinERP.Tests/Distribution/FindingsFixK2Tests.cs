using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Areas.Distribution.Services.Delivery;
using tahsinERP.Areas.Distribution.Services.Money;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Areas.Distribution.Services.Reports;
using tahsinERP.Areas.Distribution.Services.Warehouse;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Finance;
using tahsinERP.Services.Stock;

namespace tahsinERP.Tests.Distribution
{
    /// <summary>05_FINDINGS.md K2/K3 — sof qoidalar va fayl tekshiruvlari.</summary>
    [TestClass]
    public class FindingsK2K3RulesTests
    {
        private static string Root()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "tahsinERP.sln"))) dir = dir.Parent;
            if (dir == null) Assert.Inconclusive("Yechim papkasi topilmadi.");
            return dir.FullName;
        }

        [TestMethod]
        public void K2_14_EditGet_ViewOnGet_NeedsOnlyView()
        {
            Assert.IsTrue(tahsinERP.Filters.PermissionAttribute.NeedsChange("GET", "Edit", false, false), "odatiy: Edit sahifasi — Change");
            Assert.IsFalse(tahsinERP.Filters.PermissionAttribute.NeedsChange("GET", "Edit", false, true), "karta: View yetarli");
            Assert.IsTrue(tahsinERP.Filters.PermissionAttribute.NeedsChange("POST", "Edit", false, true), "saqlash — Change");
            Assert.IsTrue(tahsinERP.Filters.PermissionAttribute.NeedsChange("GET", "Index", true, true), "Change = true — doim");
        }

        [TestMethod]
        public void K2_18_DailySummary_DoesNotCallCollectedMoneyRevenue()
        {
            var uz = DstLocalization.TFor(DstLocalization.Uzbek, "Summary_Text", "21.10.2026", 1, "1", "2", "3", "4", "5", 6, 7);
            Assert.IsFalse(uz.Contains("Tushum"), "tushum — jo'natish − qaytarish (panel/hisobotlar)");
            StringAssert.Contains(uz, "Yig'ilgan pul");
        }

        [TestMethod]
        public void K3_32_LocalImages_OnlyJpegPng_NoTraversal()
        {
            Assert.AreEqual("jpg", DstLocalFiles.ImageExtension(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0 }));
            Assert.AreEqual("png", DstLocalFiles.ImageExtension(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
            Assert.IsNull(DstLocalFiles.ImageExtension(System.Text.Encoding.ASCII.GetBytes("<script>1</script>")));
            var root = Path.Combine(Path.GetTempPath(), "dstfiles-" + Guid.NewGuid().ToString("N"));
            try
            {
                var key = DstLocalFiles.SaveImage(new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5 }), "writeoffs", root);
                StringAssert.StartsWith(key, "local/writeoffs/");
                Assert.IsTrue(File.Exists(DstLocalFiles.PathOf(key, root)));
                Assert.ThrowsException<DstValidationException>(() => DstLocalFiles.SaveImage(new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }), "writeoffs", root));
                Assert.ThrowsException<DstValidationException>(() => DstLocalFiles.PathOf("local/../web.config", root));
                Assert.ThrowsException<DstValidationException>(() => DstLocalFiles.PathOf("tg/abc.jpg", root), "bot kaliti — mahalliy emas");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void K3_33_AuditScreen_InDistributionEdition()
        {
            Assert.AreEqual(tahsinERP.Services.Editions.ProductModule.Commerce, tahsinERP.Services.Editions.EditionModules.ModuleOf("DstAudit"));   // Savdo qismi
            var seed = File.ReadAllText(Path.Combine(Root(), "tahsinERP", "Database", "Seed", "105_PermissionModules_Extra.sql"));
            StringAssert.Contains(seed, "'DstAudit'");
        }

        [TestMethod]
        public void K3_36_KpiOverdue_ByAgentOnDebit_Over30Days()
        {
            var today = new DateTime(2026, 10, 21);
            var debits = new[]
            {
                new DebitItem { Date = today.AddDays(-70), DueDate = today.AddDays(-60), Amount = 100, AgentUserID = 1 },   // eski agent
                new DebitItem { Date = today.AddDays(-50), DueDate = today.AddDays(-40), Amount = 200, AgentUserID = 2 },   // yangi agent
                new DebitItem { Date = today.AddDays(-30), DueDate = today.AddDays(-20), Amount = 300, AgentUserID = 2 }    // 20 kun — jarimasiz
            };
            Assert.AreEqual(50m, DebtMath.OverdueFor(debits, 50, today, 30, 1), "to'lov eng eskisini yopadi");
            Assert.AreEqual(200m, DebtMath.OverdueFor(debits, 50, today, 30, 2));
            Assert.AreEqual(0m, DebtMath.OverdueFor(debits, 1000, today, 30, 2));
        }

        [TestMethod]
        public void K3_29_30_37_MenuAndUnusedSettings()
        {
            var root = Root();
            var layout = File.ReadAllText(Path.Combine(root, "tahsinERP", "Views", "Shared", "_Layout.cshtml"));
            foreach (var c in new[] { "DstInventory", "DstIssues", "DstAudit" })
                StringAssert.Contains(layout, "C = \"" + c + "\"", c + " chap menyuda");
            var settings = File.ReadAllText(Path.Combine(root, "tahsinERP", "Areas", "Distribution", "Views", "DstSettings", "Index.cshtml"));
            Assert.IsFalse(settings.Contains("data-f=\"neg\"") || settings.Contains("data-f=\"zone\""), "ishlatilmaydigan ombor sozlamalari olib tashlandi");
            var product = File.ReadAllText(Path.Combine(root, "tahsinERP", "Areas", "Distribution", "Views", "DstProducts", "Edit.cshtml"));
            Assert.IsFalse(product.Contains("IsBulky") || product.Contains("IsWeighted"));
            var resx = File.ReadAllText(Path.Combine(root, "tahsinERP", "Areas", "Distribution", "Resources", "DstStrings.resx"));
            foreach (var key in new[] { "Settings_AgentAccount", "Warehouse_AllowNegative", "Warehouse_Zone", "Product_Bulky", "Product_Weighted" })
                Assert.IsFalse(resx.Contains("name=\"" + key + "\""), key);
        }
    }

    /// <summary>05_FINDINGS.md K2/K3 — lokal bazada, bitta tranzaksiyada va rollback bilan.</summary>
    [TestClass]
    [TestCategory("Integration")]
    public class FindingsK2K3IntegrationTests
    {
        private static bool _ok;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            try
            {
                using (var c = new System.Data.SqlClient.SqlConnection(tahsinERP.Configuration.DbConfig.GetSqlConnectionString()))
                using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT COL_LENGTH('dst.TELEGRAM_ACCOUNTS', 'Language')", c))
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
            if (!_ok) Assert.Inconclusive("Lokal baza yoki migratsiya 027 yo'q.");
        }

        private static DistributionUnitOfWork Uow() => FindingsFixIntegrationTests.NewUow();

        private static int LinkManager(DistributionDbContext db, int userId, DateTime now)
        {
            foreach (var a in db.TelegramAccounts.Where(a => a.IsActive && (a.Role == BotRoles.Manager || a.UserID == userId)).ToList()) a.IsActive = false;
            db.SaveChanges();
            long tg = 800000000 + new Random().Next(1000000);
            db.TelegramAccounts.Add(new TelegramAccount { TelegramUserId = tg, ChatId = tg, UserID = userId, Role = BotRoles.Manager, LinkedAt = now });
            db.SaveChanges();
            return userId;
        }

        [TestMethod]
        public void K2_7_8_9_10_26_Trip_WorkDay_Finish_Reset_Redeliver_OldPlannedTrip()
        {
            using (var uow = Uow())
            {
                var w = DeliveryIntegrationTests.Arrange(uow);
                var f = w.F;
                var db = uow.Dst;
                var now = DateTime.Now;
                foreach (var t in db.Trips.Where(t => t.ExpeditorUserID == f.UserId && (t.TripStatus == (byte)TripStatus.Planned || t.TripStatus == (byte)TripStatus.InTransit)).ToList())
                    t.TripStatus = (byte)TripStatus.Cancelled;
                db.AgentDays.RemoveRange(db.AgentDays.Where(d => d.UserID == f.UserId && d.WorkDate == now.Date).ToList());
                db.SaveChanges();

                var s1 = DeliveryIntegrationTests.Ship(uow, w, f.CustomerId, 10, now);
                var s2 = DeliveryIntegrationTests.Ship(uow, w, w.Customer2, 10, now);
                var tripId = TripService.Save(uow, new TripInput { DocDate = now.AddDays(-5), VehicleID = w.VehicleId, ExpeditorUserID = f.UserId, ShipmentIDs = { s1, s2 } }, f.UserId, now);
                Assert.AreEqual(tripId, TripQueries.CurrentTripId(db, f.UserId, now), "K2-26: 5 kun oldingi rejalangan reys ham ko'rinadi");

                // K2-7: reys boshlanganda ish kuni ochiladi — jonli joylashuv yoziladi
                Assert.IsFalse(FieldService.Track(db, f.UserId, 41.3m, 69.2m, 10, now));
                TripService.Start(uow, tripId, f.UserId, now);
                db.SaveChanges();
                Assert.IsNull(db.AgentDays.Single(d => d.UserID == f.UserId && d.WorkDate == now.Date).EndAt);
                Assert.IsTrue(FieldService.Track(db, f.UserId, 41.3m, 69.2m, 10, now));

                // K2-9: natijani qaytarish — buyurtma holati, dalil va yetkazish sanasi ham qaytadi
                var stops = db.TripStops.Where(s => s.TripID == tripId).ToList();
                var stop1 = stops.Single(s => s.ShipmentID == s1);
                var stop2 = stops.Single(s => s.ShipmentID == s2);
                TripService.Deliver(uow, new DeliverInput { StopID = stop1.ID, All = true, PhotoKey = "t/1.jpg", Note = "eshik oldida" }, f.UserId, f.UserId, true, now);
                Assert.AreEqual((byte)OrderStatus.Delivered, db.Orders.Find(stop1.OrderID).OrderStatus);
                TripService.ResetStop(uow, stop1.ID, f.UserId, now);
                var order1 = db.Orders.Find(stop1.OrderID);
                Assert.AreEqual((byte)OrderStatus.Shipped, order1.OrderStatus);
                Assert.IsNull(order1.DeliveredAt);
                Assert.IsNull(db.TripStops.Find(stop1.ID).PhotoKey);
                Assert.IsNull(db.TripStops.Find(stop1.ID).Note);
                TripService.Deliver(uow, new DeliverInput { StopID = stop1.ID, All = true, PhotoKey = "t/2.jpg" }, f.UserId, f.UserId, true, now);
                TripService.Deliver(uow, new DeliverInput { StopID = stop2.ID, Delivered = new Dictionary<int, decimal>(), ReasonID = w.ReasonId }, f.UserId, f.UserId, true, now);

                // K2-8: ekspeditor yakunlaydi — reys yo'lda qoladi, kamomad ofis yopishida
                var finished = TripService.Finish(uow, tripId, f.UserId, now);
                Assert.AreEqual(1, finished.Refused);
                Assert.AreEqual(550000m, finished.ReturnedAmount, "kutilayotgan qaytish");
                var trip = db.Trips.Find(tripId);
                Assert.AreEqual((byte)TripStatus.InTransit, trip.TripStatus);
                Assert.IsNotNull(trip.FinishedAt);
                Assert.IsNotNull(db.AgentDays.Single(d => d.UserID == f.UserId && d.WorkDate == now.Date).EndAt, "reys tugadi — ish kuni yopildi");
                var line2 = db.TripStopLines.Single(l => l.StopID == stop2.ID);
                var closed = TripService.Close(uow, new CloseInput { TripID = tripId, Returned = { { line2.ID, 8 } }, LocationID = f.LocationId }, f.UserId, null, AuditActor.Web, now);
                Assert.AreEqual(110000m, closed.ShortageAmount, "2 qop kamomad — ofis kiritdi");

                // K2-10: qaytgan 8 qop — yangi buyurtma qoralamasi, ikkinchi bosish — o'sha
                var redo = TripService.Redeliver(uow, stop2.ID, OrderFlowIntegrationTests.Actor(f), now);
                var redoLine = db.OrderLines.Single(l => l.OrderID == redo);
                Assert.AreEqual(8m, redoLine.QtyBase);
                Assert.AreEqual(w.Customer2, db.Orders.Find(redo).CustomerID);
                Assert.AreEqual(redo, TripService.Redeliver(uow, stop2.ID, OrderFlowIntegrationTests.Actor(f), now));
                Assert.ThrowsException<DstValidationException>(() => TripService.Redeliver(uow, stop1.ID, OrderFlowIntegrationTests.Actor(f), now), "to'liq yetkazilgan");
            }
        }

        [TestMethod]
        public void K2_11_12_CloseRemainderSettles_StopPicking_ReceiptRereserves()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                var account = FindingsFixIntegrationTests.CashAccount(uow);
                var a = FindingsFixIntegrationTests.Draft(uow, f, 10, now);
                OrderService.Confirm(uow, a, OrderFlowIntegrationTests.Actor(f), null, now);
                OrderService.Ship(uow, a, new Dictionary<int, decimal> { { db.OrderLines.Single(l => l.OrderID == a).ID, 4 } }, f.UserId, now);
                PaymentService.Receive(uow, new PaymentInput { CustomerID = f.CustomerId, OrderID = a, Amount = 220000, Method = PaymentMethods.Cash, FinAccountID = account }, f.UserId, AuditActor.Web, now);
                Assert.AreEqual((byte)OrderStatus.PartiallyShipped, db.Orders.Find(a).OrderStatus, "qolgani hali jo'natilishi mumkin");
                OrderService.CloseRemainder(uow, a, f.UserId, "qolgani kerak emas", now);
                Assert.AreEqual((byte)OrderStatus.Closed, db.Orders.Find(a).OrderStatus, "K2-11");

                // K2-12: 96 da 200 — 104 kirimni kutadi; yig'ish to'xtatiladi; kirimdan keyin rezerv o'zi yangilanadi
                var b = FindingsFixIntegrationTests.Draft(uow, f, 200, now);
                OrderService.Confirm(uow, b, OrderFlowIntegrationTests.Actor(f), null, now);
                var lineB = db.OrderLines.Single(l => l.OrderID == b);
                Assert.AreEqual(96m, db.StockReservations.Single(r => r.OrderLineID == lineB.ID).QtyBase);
                OrderService.StartPicking(uow, b, f.UserId, now);
                OrderService.StopPicking(uow, b, f.UserId, now);
                Assert.AreEqual((byte)OrderStatus.Confirmed, db.Orders.Find(b).OrderStatus);
                StockDocs.Receive(uow, f.WarehouseId, "TST-RCV-" + b, "test", new List<StockDocs.ReceiptLine>
                {
                    new StockDocs.ReceiptLine { ProductID = f.ProductId, QtyBase = 50, UnitCost = 40000 }
                }, f.LocationId, now);
                Assert.AreEqual(146m, db.StockReservations.Single(r => r.OrderLineID == lineB.ID).QtyBase);
            }
        }

        [TestMethod]
        public void K2_13_TerritoryScope_IncludesChildren()
        {
            using (var uow = Uow())
            {
                var db = uow.Dst;
                var user = db.Users.First(u => u.IsDeleted != true);
                var region = new Territory { Name = "T viloyat " + Guid.NewGuid().ToString("N").Substring(0, 5), Level = Territory.Region };
                db.Territories.Add(region);
                db.SaveChanges();
                var district = new Territory { Name = "T tuman", Level = Territory.District, ParentID = region.ID };
                db.Territories.Add(district);
                db.SaveChanges();
                db.UserScopes.RemoveRange(db.UserScopes.Where(s => s.UserID == user.ID && s.ScopeType == ScopeTypes.Territory).ToList());
                db.UserScopes.Add(new UserScope { UserID = user.ID, ScopeType = ScopeTypes.Territory, ScopeID = region.ID });
                db.SaveChanges();
                var scope = new ScopeService(db).For(user.ID, ScopeTypes.Territory, false);
                Assert.IsTrue(scope.Allows(region.ID));
                Assert.IsTrue(scope.Allows(district.ID), "quyi hudud ham");
            }
        }

        [TestMethod]
        public void K2_16_OpeningBalance_ReverseThenReimport()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                db.CustomerLedger.Add(new CustomerLedgerEntry { CustomerID = f.CustomerId, EntryDate = now, DocType = LedgerDocTypes.Opening, Debit = 100000, DueDate = now.Date, CreatedBy = f.UserId });
                db.SaveChanges();
                Assert.AreEqual(1, DebtService.LiveOpening(db, f.CustomerId).Count);
                Assert.ThrowsException<DstValidationException>(() => DebtService.ReverseOpening(db, f.CustomerId, f.UserId, "", now), "sabab majburiy");
                DebtService.ReverseOpening(db, f.CustomerId, f.UserId, "xato summa", now);
                Assert.AreEqual(0, DebtService.LiveOpening(db, f.CustomerId).Count);
                Assert.AreEqual(0m, LedgerService.CreditState(db, f.CustomerId, now).Balance);

                // qayta import (DocID yo'q) — amalda, teskari yozuv uni yopmaydi
                db.CustomerLedger.Add(new CustomerLedgerEntry { CustomerID = f.CustomerId, EntryDate = now, DocType = LedgerDocTypes.Opening, Debit = 70000, DueDate = now.Date, CreatedBy = f.UserId });
                db.SaveChanges();
                Assert.AreEqual(1, DebtService.LiveOpening(db, f.CustomerId).Count);
                var debts = DebtService.Load(db, new[] { f.CustomerId })[f.CustomerId];
                Assert.AreEqual(70000m, debts.Debits.Sum(d => d.Amount) - debts.Credits);
            }
        }

        [TestMethod]
        public void K2_17_DistributionPayment_NotAdvanceInFinanceCounterparties()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var account = FindingsFixIntegrationTests.CashAccount(uow);
                PaymentService.Receive(uow, new PaymentInput { CustomerID = f.CustomerId, Amount = 300000, Method = PaymentMethods.Cash, FinAccountID = account }, f.UserId, AuditActor.Web, DateTime.Now);
                Assert.IsTrue(uow.Erp.FIN_TRANSACTIONS.Any(t => t.SourceDocType == FinQueries.DistributionPayment && t.CounterpartyID == f.CustomerId), "pul moliyada bor");
                var rows = new CounterpartyBalanceService(uow.Erp).Customers(DateTime.Now.AddMinutes(1));
                Assert.IsFalse(rows.Any(r => r.CustomerID == f.CustomerId && r.ReceiptTotal != 0), "debitorlik/avansda qatnashmaydi");
            }
        }

        [TestMethod]
        public void K2_19_LowStockAlert_OnReservation_ByAvailable()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                LinkManager(db, f.UserId, now);
                db.ProductStockLimits.Add(new ProductStockLimit { WarehouseID = f.WarehouseId, ProductID = f.ProductId, MinQty = 95 });
                db.SaveChanges();
                var order = FindingsFixIntegrationTests.Draft(uow, f, 10, now);
                OrderService.Confirm(uow, order, new OrderActor { UserId = f.UserId + 100000, MaxDiscountPct = 5 }, null, now);
                Assert.IsTrue(db.Notifications.Any(n => n.Type == StockAlerts.LowStock && n.UserID == f.UserId && n.CreatedAt >= now),
                    "tasdiq rezervladi: mavjud 90 < min 95");
            }
        }

        [TestMethod]
        public void K2_21_TransferAct_SplitShortage_AndCancel()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var erp = uow.Erp;
                var now = DateTime.Now;
                var target = erp.WAREHOUSES.Where(w => w.IsDeleted != true && w.ID != f.WarehouseId).OrderBy(w => w.ID).FirstOrDefault();
                if (target == null) Assert.Inconclusive("Ikkinchi ombor yo'q.");
                int? targetLocation = null;
                if (StockLedger.IsDlocWarehouse(erp, target.ID))
                {
                    var loc = new WAREHOUSE_LOCATIONS
                    {
                        WarehouseID = target.ID, LocationCode = "TST" + Guid.NewGuid().ToString("N").Substring(0, 8), LocationName = "test",
                        MinCapacity = 0, MaxCapacity = 1000000, AllowMixedLots = true, AllowMixedProducts = true, IsActive = true, IsDeleted = false, CreatedAt = now
                    };
                    erp.WAREHOUSE_LOCATIONS.Add(loc);
                    erp.SaveChanges();
                    targetLocation = loc.ID;
                }
                var expenseId = StockDocs.Issue(uow, f.WarehouseId, "TST-TRS-" + f.ProductId, "test transfer",
                    new List<StockDocs.IssueLine> { new StockDocs.IssueLine { ProductID = f.ProductId, QtyBase = 20 } }, now);
                var unit = erp.PRODUCTS.Where(p => p.ID == f.ProductId).Select(p => p.UnitID).First();
                var income = new INCOME
                {
                    DocNo = "TST-TRSI-" + f.ProductId, ReceivingWHID = target.ID, SenderWHID = f.WarehouseId, IssueDateTime = now,
                    CurrencyID = WarehouseAccess.DefaultCurrencyId(erp), IsDeleted = false, RecieveStatus = false,
                    Description = WarehouseFlowService.AutoTransferIncomeMarker + " test | " + DocumentMarkers.For(DocumentMarkers.LinkedExpenseId, expenseId)
                };
                erp.INCOMES.Add(income);
                erp.SaveChanges();
                erp.INCOME_PRODUCTS.Add(new INCOME_PRODUCTS { IncomeID = income.ID, ProductID = f.ProductId, Amount = 20, UnitID = unit, Comment = "" });
                erp.SaveChanges();
                var ip = TransferActService.Lines(db, erp, income.ID).Single().IncomeProductID;
                decimal OnHand(int wh) => StockService.Levels(db, new[] { f.ProductId }, new[] { wh }).Sum(l => l.OnHand);

                Assert.ThrowsException<DstValidationException>(() => TransferActService.Receive(uow, income.ID, new[]
                {
                    new TransferReceiveLine { IncomeProductID = ip, ReceivedQty = 16, Resolution = TransferResolutions.Split, LossQty = 4 }
                }, targetLocation, f.LocationId, null, f.UserId, true, now), "bo'lishda yo'qotish kamomaddan kichik");
                var actId = TransferActService.Receive(uow, income.ID, new[]
                {
                    new TransferReceiveLine { IncomeProductID = ip, ReceivedQty = 16, Resolution = TransferResolutions.Split, LossQty = 1 }
                }, targetLocation, f.LocationId, null, f.UserId, true, now);
                var act = db.TransferActs.Find(actId);
                Assert.AreEqual(160000m, act.ShortageCost);
                Assert.AreEqual(40000m, act.LossCost, "1 dona yo'qotish");
                Assert.AreEqual(1m, db.TransferActLines.Single(l => l.ActID == actId).LossQty);
                Assert.AreEqual(83m, OnHand(f.WarehouseId), "100 − 20 + 3 qaytdi");
                Assert.AreEqual(16m, OnHand(target.ID));

                TransferActService.Cancel(uow, actId, f.UserId, true, "noto'g'ri sanaldi", now);
                Assert.AreEqual(DocumentStatus.Cancelled, db.TransferActs.Find(actId).Status);
                Assert.AreEqual(80m, OnHand(f.WarehouseId), "qaytarish bekor");
                Assert.AreEqual(0m, OnHand(target.ID), "qabul bekor");
                Assert.IsTrue(TransferActService.Pending(erp, new[] { target.ID }).Any(p => p.IncomeID == income.ID), "ko'chirma yana qabul kutadi");
                var again = TransferActService.Receive(uow, income.ID, new[] { new TransferReceiveLine { IncomeProductID = ip, ReceivedQty = 20 } },
                    targetLocation, f.LocationId, null, f.UserId, true, now);
                Assert.AreEqual(20m, OnHand(target.ID));
                Assert.AreNotEqual(actId, again);
            }
        }

        [TestMethod]
        public void K2_22_LandedCostOnSold_InPeriod()
        {
            using (var uow = Uow())
            {
                var db = uow.Dst;
                var now = DateTime.Now;
                var user = db.Users.First(u => u.IsDeleted != true);
                var before = SalesReportService.LandedCostOnSold(db, now, now);
                db.LandedCosts.Add(new LandedCost
                {
                    DocNo = "TST-LC-" + Guid.NewGuid().ToString("N").Substring(0, 8), DocDate = now.Date, Status = DocumentStatus.Posted, PostedAt = now, PostedBy = user.ID,
                    Basis = LandedCostBasis.Value, TotalAmount = 50000, ConsumedAmount = 12345, CreatedBy = user.ID, CreatedAt = now
                });
                db.SaveChanges();
                Assert.AreEqual(before + 12345m, SalesReportService.LandedCostOnSold(db, now, now));
            }
        }

        [TestMethod]
        public void K2_24_CustomerNotification_InCustomerLanguage()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                var code = TelegramLinkService.CreateCode(db, null, f.CustomerId, BotRoles.Customer, f.UserId, now);
                db.SaveChanges();
                long tg = 700000000 + new Random().Next(1000000);
                var account = TelegramLinkService.Redeem(db, code.Code, tg, tg, now, "ru");
                db.SaveChanges();
                Assert.AreEqual("ru", account.Language);
                NotificationService.ToCustomer(db, f.CustomerId, "TEST_LANG", l => "lang=" + l, now);
                db.SaveChanges();
                Assert.AreEqual("lang=ru", db.Notifications.Single(n => n.Type == "TEST_LANG" && n.CustomerID == f.CustomerId).Text);
            }
        }

        [TestMethod]
        public void K2_25_CheckIn_StaticLocation_RejectedWhenLiveOnly()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                DstSettings.Set(db, DstSetting.CheckInLiveOnly, "1");
                db.SaveChanges();
                Assert.ThrowsException<DstValidationException>(() => FieldService.CheckIn(db, f.UserId, f.CustomerId, 41.3m, 69.2m, 10, false, now));
                FieldService.CheckIn(db, f.UserId, f.CustomerId, 41.3m, 69.2m, 10, true, now);
                DstSettings.Set(db, DstSetting.CheckInLiveOnly, "0");
                db.SaveChanges();
                FieldService.CheckIn(db, f.UserId, f.CustomerId, 41.3m, 69.2m, 10, false, now.AddMinutes(1));   // standart — qabul, belgilanadi
            }
        }

        [TestMethod]
        public void K2_27_CreditNotification_OnEditAndOncePerDay()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                var manager = db.Users.Where(u => u.IsActive && u.IsDeleted != true && u.ID != f.UserId).Select(u => u.ID).FirstOrDefault();
                if (manager == 0) Assert.Inconclusive("Ikkinchi foydalanuvchi kerak.");
                LinkManager(db, manager, now);
                var order = FindingsFixIntegrationTests.Draft(uow, f, 1, now);           // 55 000 — limit ichida, xabar yo'q
                Assert.IsFalse(db.Notifications.Any(n => n.Type == NotificationService.CreditOverLimit && n.Payload.Contains("\"orderId\":" + order + "}")));
                OrderService.SaveDraft(uow, new OrderInput
                {
                    ID = order, CustomerID = f.CustomerId, WarehouseID = f.WarehouseId, Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = 5 } }
                }, OrderFlowIntegrationTests.Actor(f), now);                              // 275 000 — tahrirdan keyin oshdi
                Assert.AreEqual(1, db.Notifications.Count(n => n.Type == NotificationService.CreditOverLimit && n.Payload.Contains("\"orderId\":" + order + "}")));
                Assert.IsFalse(OrderService.NotifyCredit(db, db.Orders.Find(order), "x", f.UserId, now), "kuniga bir marta");
            }
        }

        [TestMethod]
        public void K2_28_Dashboard_PastDate_DebtAsOfDay_CurrentStateMarked()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var now = DateTime.Now;
                var order = FindingsFixIntegrationTests.Draft(uow, f, 10, now);
                OrderService.Confirm(uow, order, OrderFlowIntegrationTests.Actor(f), null, now);
                OrderService.Ship(uow, order, null, f.UserId, now);
                var today = DashboardService.Build(uow, now.Date, ReportScope.Unrestricted, true, false);
                var yesterday = DashboardService.Build(uow, now.Date.AddDays(-1), ReportScope.Unrestricted, true, false);
                Assert.IsFalse(today.IsPast);
                Assert.IsTrue(yesterday.IsPast);
                Assert.IsTrue(today.Debt.Total - yesterday.Debt.Total >= 550000m - 0.01m, "kechagi qarzda bugungi jo'natish yo'q");
            }
        }

        [TestMethod]
        public void K3_31_DefectReturn_OnlyToDefectWarehouse_NotForSale()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                var defect = db.Warehouses.Where(w => w.IsDeleted != true && w.ID != f.WarehouseId).Select(w => w.ID).FirstOrDefault();
                if (defect == 0) Assert.Inconclusive("Ikkinchi ombor yo'q.");
                DstSettings.Set(db, DstSetting.DefectWarehouseId, defect.ToString());
                db.SaveChanges();
                Assert.IsFalse(StockService.SalesWarehouses(db).Contains(defect));
                var order = FindingsFixIntegrationTests.Draft(uow, f, 5, now);
                OrderService.Confirm(uow, order, OrderFlowIntegrationTests.Actor(f), null, now);
                var shipment = OrderService.Ship(uow, order, null, f.UserId, now);
                var sl = db.ShipmentLines.Single(l => l.ShipmentID == shipment);
                Assert.ThrowsException<DstValidationException>(() => ReturnService.Create(uow, new ReturnInput
                {
                    CustomerID = f.CustomerId, WarehouseID = f.WarehouseId, LocationID = f.LocationId, IsDefect = true,
                    Lines = { new ReturnLineInput { ShipmentLineID = sl.ID, QtyBase = 1 } }
                }, f.UserId, AuditActor.Web, now));
                ReturnService.Create(uow, new ReturnInput
                {
                    CustomerID = f.CustomerId, WarehouseID = f.WarehouseId, LocationID = f.LocationId, IsDefect = false,
                    Lines = { new ReturnLineInput { ShipmentLineID = sl.ID, QtyBase = 1 } }
                }, f.UserId, AuditActor.Web, now);   // yaroqli — oddiy omborga
            }
        }
    }
}
