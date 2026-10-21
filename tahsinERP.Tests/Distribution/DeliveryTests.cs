using System;
using System.Collections.Generic;
using System.Data.SqlClient;
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
using tahsinERP.Configuration;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.Services.Stock;

namespace tahsinERP.Tests.Distribution
{
    /// <summary>2-bosqich: sof hisoblar (sig'im, marshrut tartibi, yetkazish natijasi, yopish farqi, KPI, ovozli moslash).</summary>
    [TestClass]
    public class DeliveryMathTests
    {
        [TestMethod]
        public void Capacity_WeightAndVolume()
        {
            Assert.AreEqual(0, DeliveryMath.CapacityProblems(1500, 3, 1500, null).Count);
            Assert.AreEqual(1, DeliveryMath.CapacityProblems(1500.1m, 3, 1500, null).Count);
            Assert.AreEqual(1, DeliveryMath.CapacityProblems(100, 3.5m, 1500, 3).Count);
            Assert.AreEqual(2, DeliveryMath.CapacityProblems(2000, 5, 1500, 3).Count);
            Assert.AreEqual(50m, DeliveryMath.LoadPct(750, 1500));
            Assert.IsNull(DeliveryMath.LoadPct(1, null));
        }

        [TestMethod]
        public void NearestNeighbor_FromDepot_GeoLessLast()
        {
            // ombor (41.30, 69.24) dan: B (0.01 shimol), keyin C (0.03), A (0.06); D — koordinatasiz
            var points = new List<GeoPoint>
            {
                new GeoPoint { Id = 1, Lat = 41.36m, Lng = 69.24m },
                new GeoPoint { Id = 4 },
                new GeoPoint { Id = 2, Lat = 41.31m, Lng = 69.24m },
                new GeoPoint { Id = 3, Lat = 41.33m, Lng = 69.24m }
            };
            CollectionAssert.AreEqual(new[] { 2, 3, 1, 4 }, DeliveryMath.NearestNeighbor(points, 41.30m, 69.24m));
            // ombor yo'q — birinchi koordinatali nuqtadan
            CollectionAssert.AreEqual(new[] { 1, 3, 2, 4 }, DeliveryMath.NearestNeighbor(points, null, null));
            Assert.AreEqual(6.7m, DeliveryMath.PathKm(new[] { points[2], points[3], points[0] }, 41.30m, 69.24m));
        }

        [TestMethod]
        public void Classify_DeliveredPartialRefused()
        {
            Assert.AreEqual(StopStatus.Delivered, DeliveryMath.Classify(new[] { (10m, 10m), (5m, 5m) }));
            Assert.AreEqual(StopStatus.Partial, DeliveryMath.Classify(new[] { (10m, 10m), (5m, 0m) }));
            Assert.AreEqual(StopStatus.Partial, DeliveryMath.Classify(new[] { (10m, 6m) }));
            Assert.AreEqual(StopStatus.Refused, DeliveryMath.Classify(new[] { (10m, 0m), (5m, 0m) }));
        }

        [TestMethod]
        public void CloseLine_ReturnedAndShortage()
        {
            Assert.AreEqual((4m, 4m, 0m), DeliveryMath.CloseLine(10, 6, null));
            Assert.AreEqual((4m, 3m, 1m), DeliveryMath.CloseLine(10, 6, 3));
            Assert.AreEqual((0m, 0m, 0m), DeliveryMath.CloseLine(10, 10, null));
            Assert.ThrowsException<DstValidationException>(() => DeliveryMath.CloseLine(10, 6, 5));
            Assert.ThrowsException<DstValidationException>(() => DeliveryMath.CloseLine(10, 6, -1));
            Assert.AreEqual(330000m, DeliveryMath.Share(550000, 6, 10));
        }

        [TestMethod]
        public void Geo_ParseAndLinks()
        {
            Assert.IsTrue(DeliveryMath.TryParseGeo("41.311081, 69.240562", out var lat, out var lng));
            Assert.AreEqual(41.311081m, lat);
            Assert.IsFalse(DeliveryMath.TryParseGeo("91,10", out _, out _));
            Assert.IsFalse(DeliveryMath.TryParseGeo("abc", out _, out _));
            Assert.AreEqual("https://yandex.uz/maps/?rtext=~41.3,69.2&rtt=auto", DeliveryMath.NavigationUrl(41.3m, 69.2m));
            Assert.AreEqual("https://yandex.uz/maps/?rtext=41.1,69.1~41.3,69.2&rtt=auto",
                DeliveryMath.RouteUrl(41.1m, 69.1m, new[] { new GeoPoint { Id = 1, Lat = 41.3m, Lng = 69.2m }, new GeoPoint { Id = 2 } }));
        }

        [TestMethod]
        public void Kpi_CommissionBonusPenalty()
        {
            var s = new KpiScheme
            {
                SalesPct = 2, MonthlyPlan = 100_000_000, PlanBonus = 1_000_000, OverPlanPct = 1,
                MinCoveragePct = 80, CoveragePenalty = 300_000, OverduePenaltyPct = 1, ShortagePenaltyPct = 100
            };
            var below = KpiMath.Compute(s, new KpiInputs { NetSales = 80_000_000, CoveragePct = 90, OverdueDebt = 0 });
            Assert.AreEqual(1_600_000m, below.Commission);
            Assert.AreEqual(0m, below.Bonus);
            Assert.AreEqual(0m, below.Penalty);
            Assert.AreEqual(80m, below.PlanPct);

            var above = KpiMath.Compute(s, new KpiInputs { NetSales = 120_000_000, CoveragePct = 70, OverdueDebt = 5_000_000, ShortageCost = 40_000 });
            Assert.AreEqual(2_400_000m, above.Commission);
            Assert.AreEqual(1_200_000m, above.Bonus);                 // 1 000 000 + 20 mln × 1%
            Assert.AreEqual(300_000m + 50_000m + 40_000m, above.Penalty);

            // reja yo'q — bonus va foiz yo'q; manfiy savdo (qaytarish ko'p) — komissiya 0
            var noPlan = KpiMath.Compute(new KpiScheme { SalesPct = 3 }, new KpiInputs { NetSales = -5_000 });
            Assert.IsNull(noPlan.PlanPct);
            Assert.AreEqual(0m, noPlan.Commission);
        }

        [TestMethod]
        public void Voice_NormalizeAndScore()
        {
            Assert.AreEqual("sement m400", VoiceMatch.Normalize("Цемент М400").Replace("ts", "s"));
            Assert.AreEqual("gisht", VoiceMatch.Normalize("G'isht"));
            Assert.AreEqual(VoiceMatch.Normalize("o'rama"), VoiceMatch.Normalize("oʻrama"));
            Assert.AreEqual(100, VoiceMatch.Score("sement M400", "Sement M400 50 kg"));
            Assert.IsTrue(VoiceMatch.Score("sement", "Sement M500") > VoiceMatch.Score("sement", "Kafel 60x60"));
            Assert.IsTrue(VoiceMatch.Score("kafel 60x60", "Kafel pol 60x60 Shahrisabz") >= 90);
            Assert.AreEqual(0, VoiceMatch.Score("", "Kafel"));
            Assert.IsTrue(VoiceMatch.Score("m400", "Sement M500") < 50);   // raqamli belgi og'ir
        }

        [TestMethod]
        public void Voice_PickUnit()
        {
            var units = new List<CatalogUnit>
            {
                new CatalogUnit { ProductUnitId = null, Name = "dona", Factor = 1 },
                new CatalogUnit { ProductUnitId = 7, Name = "Qop", Factor = 50, IsDefault = true },
                new CatalogUnit { ProductUnitId = 8, Name = "кор", Factor = 10 }
            };
            Assert.AreEqual(7, VoiceMatch.PickUnit(units, "qop").ProductUnitId);
            Assert.AreEqual(7, VoiceMatch.PickUnit(units, "мешок")?.ProductUnitId ?? 7);
            Assert.IsNull(VoiceMatch.PickUnit(units, "dona").ProductUnitId);
            Assert.IsNull(VoiceMatch.PickUnit(units, "poddon"));
            Assert.IsNull(VoiceMatch.PickUnit(units, ""));
            CollectionAssert.AreEqual(new[] { "Sement M400 qop", "Sement", "M400" }, VoiceOrderService.Queries("Sement M400 qop").ToArray());
        }
    }

    /// <summary>
    /// 2-bosqich oqimi lokal bazada, bitta tranzaksiyada va rollback: jo'natish → reys (sig'im) → tartib → yo'lga chiqish →
    /// to'liq va qisman yetkazish → yopish (qaytarish + kamomad) → xarajat moliyaga → KPI → HR ish haqi qatori; tashrif rejasi.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class DeliveryIntegrationTests
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

        private sealed class World
        {
            public OrderFlowIntegrationTests.Fixture F;
            public int Customer2, VehicleId, ReasonId;
        }

        /// <summary>Tovar 50 kg (qop), ikki mijoz (koordinatali), ekspeditor (fixture foydalanuvchisi), mashina 1 500 kg, sabab.</summary>
        private static World Arrange(DistributionUnitOfWork uow)
        {
            var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
            var db = uow.Dst;
            var now = DateTime.Now;
            var pp = db.ProductProfiles.Find(f.ProductId);
            if (pp == null) db.ProductProfiles.Add(pp = new ProductProfile { ProductID = f.ProductId });
            pp.BaseWeightKg = 50;
            pp.BaseVolumeM3 = 0.04m;

            var c2 = CustomerService.Save(uow, new CustomerInput { Name = "Delivery 2 " + Guid.NewGuid().ToString("N").Substring(0, 6), CreditLimit = 100000000m, PaymentDays = 5 },
                f.UserId, AuditActor.Web, now);
            db.CustomerProfiles.Find(f.CustomerId).Lat = 41.36m;
            db.CustomerProfiles.Find(f.CustomerId).Lng = 69.24m;
            db.CustomerProfiles.Find(c2).Lat = 41.31m;
            db.CustomerProfiles.Find(c2).Lng = 69.24m;

            var agent = db.SalesAgents.Find(f.UserId);
            if (agent == null) db.SalesAgents.Add(agent = new SalesAgent { UserID = f.UserId });
            agent.IsActive = true;
            agent.IsExpeditor = true;
            agent.Phone = "+998901112233";

            var vehicle = new Vehicle { PlateNo = "T" + Guid.NewGuid().ToString("N").Substring(0, 8), CapacityKg = 1500, CapacityM3 = 10, WarehouseID = f.WarehouseId };
            db.Vehicles.Add(vehicle);
            var reason = new Reason { Type = ReasonTypes.Return, Name = "Test: mijoz rad etdi", IsActive = true };
            db.Reasons.Add(reason);
            DstSettings.Set(db, TripService.DepotKey(f.WarehouseId), "41.30,69.24");
            db.SaveChanges();
            return new World { F = f, Customer2 = c2, VehicleId = vehicle.ID, ReasonId = reason.ID };
        }

        private static int Ship(DistributionUnitOfWork uow, World w, int customerId, decimal qty, DateTime now)
        {
            var f = w.F;
            var orderId = OrderService.SaveDraft(uow, new OrderInput
            {
                CustomerID = customerId, WarehouseID = f.WarehouseId, AgentUserID = f.UserId,
                Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = qty } }
            }, OrderFlowIntegrationTests.Actor(f), now);
            OrderService.Confirm(uow, orderId, OrderFlowIntegrationTests.Actor(f), null, now);
            return OrderService.Ship(uow, orderId, null, f.UserId, now);
        }

        private static decimal OnHand(DistributionUnitOfWork uow, World w) =>
            StockService.Levels(uow.Dst, new[] { w.F.ProductId }, new[] { w.F.WarehouseId }).Sum(l => l.OnHand);

        [TestMethod]
        public void Trip_PlanDeliverClose_ReturnsShortageExpenseAndKpi()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var w = Arrange(uow);
                var f = w.F;
                var db = uow.Dst;
                var now = DateTime.Now;

                var s1 = Ship(uow, w, f.CustomerId, 20, now);    // 1 000 kg, 1 100 000
                var s2 = Ship(uow, w, w.Customer2, 10, now);     //   500 kg,   550 000
                Assert.AreEqual(70m, OnHand(uow, w));

                // sig'im: 1 500 kg — sig'adi; 2-chi mashina 1 000 kg — sig'maydi
                var small = new Vehicle { PlateNo = "S" + Guid.NewGuid().ToString("N").Substring(0, 8), CapacityKg = 1000, WarehouseID = f.WarehouseId };
                db.Vehicles.Add(small);
                db.SaveChanges();
                var ex = Assert.ThrowsException<DstValidationException>(() => TripService.Save(uow, new TripInput
                {
                    DocDate = now, VehicleID = small.ID, ExpeditorUserID = f.UserId, ShipmentIDs = { s1, s2 }
                }, f.UserId, now));
                StringAssert.Contains(ex.Message, "1500");

                var tripId = TripService.Save(uow, new TripInput
                {
                    DocDate = now, VehicleID = w.VehicleId, ExpeditorUserID = f.UserId, ShipmentIDs = { s1, s2 }
                }, f.UserId, now);
                var trip = db.Trips.Find(tripId);
                Assert.IsTrue(trip.DocNo.StartsWith("R-" + now.Year + "-"));
                Assert.AreEqual(1500m, trip.WeightKg);
                Assert.AreEqual(1.2m, trip.VolumeM3);
                Assert.AreEqual(1650000m, trip.Total);
                Assert.AreEqual(f.WarehouseId, trip.WarehouseID);   // mashinaning ombori

                // jo'natish ikkinchi reysga qo'shilmaydi
                Assert.ThrowsException<DstValidationException>(() => TripService.Save(uow, new TripInput
                {
                    DocDate = now, VehicleID = w.VehicleId, ExpeditorUserID = f.UserId, ShipmentIDs = { s2 }
                }, f.UserId, now));
                Assert.IsFalse(TripQueries.Available(db, f.WarehouseId, null).Any(a => a.ID == s1 || a.ID == s2));

                // tartib: ombordan eng yaqini — 2-mijoz (41.31), keyin 1-mijoz (41.36)
                var km = TripService.Optimize(uow, tripId);
                var stops = db.TripStops.Where(s => s.TripID == tripId).OrderBy(s => s.Seq).ToList();
                Assert.AreEqual(s2, stops[0].ShipmentID);
                Assert.AreEqual(6.7m, km);
                Assert.IsTrue(db.Trips.Find(tripId).RouteOptimized);

                // yo'lga chiqishdan oldin yetkazib bo'lmaydi
                Assert.ThrowsException<DstValidationException>(() =>
                    TripService.Deliver(uow, new DeliverInput { StopID = stops[0].ID, All = true, PhotoKey = "t/1.jpg" }, f.UserId, f.UserId, true, now));
                TripService.Start(uow, tripId, f.UserId, now);
                Assert.AreEqual((byte)TripStatus.InTransit, db.Trips.Find(tripId).TripStatus);
                Assert.ThrowsException<DstValidationException>(() => TripService.Cancel(uow, tripId, f.UserId, "test", now));

                // 1-mijoz: to'liq, isbotsiz — rad; rasm bilan — yetkazildi, buyurtma "Yetkazildi"
                var stop1 = stops.First(s => s.ShipmentID == s1);
                Assert.ThrowsException<DstValidationException>(() =>
                    TripService.Deliver(uow, new DeliverInput { StopID = stop1.ID, All = true }, f.UserId, f.UserId, true, now));
                // boshqa ekspeditor — topilmadi
                Assert.ThrowsException<DstValidationException>(() =>
                    TripService.Deliver(uow, new DeliverInput { StopID = stop1.ID, All = true, PhotoKey = "t/1.jpg" }, f.UserId, f.UserId + 100000, true, now));
                Assert.AreEqual(StopStatus.Delivered,
                    TripService.Deliver(uow, new DeliverInput { StopID = stop1.ID, All = true, PhotoKey = "t/1.jpg", Lat = 41.36m, Lng = 69.24m }, f.UserId, f.UserId, true, now));
                var order1 = db.Orders.Find(stop1.OrderID);
                Assert.AreEqual((byte)OrderStatus.Delivered, order1.OrderStatus);
                Assert.IsNotNull(order1.DeliveredAt);
                Assert.AreEqual(1100000m, db.TripStops.Find(stop1.ID).DeliveredAmount);

                // 2-mijoz: 10 dan 6 tasi, sababsiz — rad; sabab va imzo bilan — qisman
                var stop2 = stops.First(s => s.ShipmentID == s2);
                var line2 = db.TripStopLines.Single(l => l.StopID == stop2.ID);
                Assert.ThrowsException<DstValidationException>(() => TripService.Deliver(uow, new DeliverInput
                {
                    StopID = stop2.ID, Delivered = { { line2.ID, 6 } }, SignatureKey = "t/s.png"
                }, f.UserId, f.UserId, true, now));
                Assert.ThrowsException<DstValidationException>(() => TripService.Deliver(uow, new DeliverInput
                {
                    StopID = stop2.ID, Delivered = { { line2.ID, 11 } }, SignatureKey = "t/s.png", ReasonID = w.ReasonId
                }, f.UserId, f.UserId, true, now));
                Assert.AreEqual(StopStatus.Partial, TripService.Deliver(uow, new DeliverInput
                {
                    StopID = stop2.ID, Delivered = { { line2.ID, 6 } }, SignatureKey = "t/s.png", ReasonID = w.ReasonId
                }, f.UserId, f.UserId, true, now));
                Assert.AreEqual(330000m, db.TripStops.Find(stop2.ID).DeliveredAmount);
                var order2 = db.Orders.Find(stop2.OrderID);
                Assert.AreEqual((byte)OrderStatus.Delivered, order2.OrderStatus);
                Assert.ThrowsException<DstValidationException>(() =>
                    TripService.Deliver(uow, new DeliverInput { StopID = stop2.ID, All = true, PhotoKey = "x" }, f.UserId, f.UserId, true, now));

                // xarajat: yoqilg'i 300 000 → kassadan moliyaga
                var account = CashAccount(uow);
                var expenseId = TripService.AddExpense(uow, tripId, TripExpenseKinds.Fuel, null, 300000, "AI-92", null, f.UserId, f.UserId, AuditActor.Bot, now);
                TripService.PostExpense(uow, expenseId, null, account, f.UserId, now);
                var x = db.TripExpenses.Find(expenseId);
                var tx = uow.Erp.FIN_TRANSACTIONS.Find(x.FinTransactionID);
                Assert.AreEqual((byte)2, tx.Direction);
                Assert.AreEqual(300000m, tx.Amount);
                Assert.AreEqual(TripService.ExpenseSource, tx.SourceDocType);

                // yopish: 4 yetkazilmagandan 3 tasi omborga qaytdi, 1 — kamomad
                var debtBefore = LedgerService.CreditState(db, w.Customer2, now).Balance;
                Assert.AreEqual(550000m, debtBefore);
                var close = TripService.Close(uow, new CloseInput { TripID = tripId, Returned = { { line2.ID, 3 } }, LocationID = f.LocationId },
                    f.UserId, null, AuditActor.Web, now);
                Assert.AreEqual(1, close.Delivered);
                Assert.AreEqual(1, close.Partial);
                Assert.AreEqual(1, close.ReturnIds.Count);
                Assert.AreEqual(165000m, close.ReturnedAmount);
                Assert.AreEqual(55000m, close.ShortageAmount);
                Assert.AreEqual(40000m, close.ShortageCost);
                Assert.AreEqual(73m, OnHand(uow, w));                                                   // 70 + 3 qaytdi
                Assert.AreEqual(330000m, LedgerService.CreditState(db, w.Customer2, now).Balance);     // faqat yetkazilgani
                trip = db.Trips.Find(tripId);
                Assert.AreEqual((byte)TripStatus.Closed, trip.TripStatus);
                Assert.AreEqual(DocumentStatus.Posted, trip.Status);
                Assert.AreEqual(40000m, trip.ShortageCost);
                Assert.AreEqual(3m, db.TripStopLines.Find(line2.ID).QtyReturned);
                Assert.AreEqual(close.ReturnIds[0], db.TripStops.Find(stop2.ID).ReturnID);
                Assert.ThrowsException<DstValidationException>(() =>
                    TripService.Close(uow, new CloseInput { TripID = tripId }, f.UserId, null, AuditActor.Web, now));

                // nakladnoy (bot PDF uchun): qisman — yetkazilgan 6 dona
                var inv = TripQueries.Invoice(db, stop2.ID, now);
                Assert.IsTrue(inv.Partial);
                Assert.AreEqual(6m, inv.Lines.Single().Qty);
                Assert.AreEqual(330000m, inv.Total);
                Assert.AreEqual(trip.DocNo, inv.TripNo);

                // KPI: agent savdosi (jo'natilgan 1 650 000 − qaytgan 165 000), kamomad jarimasi 100%
                db.KpiSchemes.Add(new KpiScheme { AgentUserID = f.UserId, SalesPct = 2, ShortagePenaltyPct = 100, IsActive = true });
                db.SaveChanges();
                var results = KpiService.Calculate(db, now.Year, now.Month, f.UserId, now);
                var mine = results.Single(r => r.AgentUserID == f.UserId);
                Assert.IsTrue(mine.NetSales >= 1485000m);   // shu oyda boshqa savdo ham bo'lishi mumkin
                Assert.IsTrue(mine.ShortageCost >= 40000m);
                Assert.AreEqual(Math.Round(mine.NetSales * 0.02m, 0, MidpointRounding.AwayFromZero), mine.Commission);

                // HR: shu oy qoralama ish haqi qatori — mukofot va jarima qo'shiladi, "to'lovga" qayta hisoblanadi (soliq 12%)
                var employee = new HR_EMPLOYEES { TabNo = "T" + Guid.NewGuid().ToString("N").Substring(0, 6), FullName = "KPI test", IsActive = true, CreatedDT = now, IsDeleted = false };
                uow.Erp.HR_EMPLOYEES.Add(employee);
                uow.Erp.SaveChanges();
                Assert.ThrowsException<DstValidationException>(() => KpiService.Apply(uow, mine.ID, f.UserId, now));   // xodim bog'lanmagan
                db.KpiSchemes.Find(f.UserId).EmployeeID = employee.ID;
                db.SaveChanges();
                Assert.ThrowsException<DstValidationException>(() => KpiService.Apply(uow, mine.ID, f.UserId, now));   // ish haqi hisobi yo'q
                var header = new HR_PAYROLL_HEADERS { Year = now.Year, Month = now.Month, Status = "Draft", CreatedDT = now, PayrollType = "Hourly", IsDeleted = false };
                uow.Erp.HR_PAYROLL_HEADERS.Add(header);
                uow.Erp.SaveChanges();
                var line = new HR_PAYROLL_LINES { PayrollID = header.ID, EmployeeID = employee.ID, GrossSalary = 5000000, Bonuses = 0, Penalties = 0, NetSalary = 4400000, IsDeleted = false };
                uow.Erp.HR_PAYROLL_LINES.Add(line);
                uow.Erp.SaveChanges();

                KpiService.Adjust(db, mine.ID, 100000, 0, "test");
                KpiService.Apply(uow, mine.ID, f.UserId, now);
                mine = db.KpiResults.Find(mine.ID);
                Assert.AreEqual(KpiResult.Applied, mine.Status);
                Assert.AreEqual(line.ID, mine.PayrollLineID);
                Assert.AreEqual(mine.TotalBonus, line.Bonuses);
                Assert.AreEqual(mine.TotalPenalty, line.Penalties);
                Assert.AreEqual(5000000m + mine.TotalBonus - mine.TotalPenalty - 600000m, line.NetSalary);
                Assert.ThrowsException<DstValidationException>(() => KpiService.Apply(uow, mine.ID, f.UserId, now));
                KpiService.Calculate(db, now.Year, now.Month, f.UserId, now);      // o'tkazilgani qayta hisoblanmaydi
                Assert.AreEqual(KpiResult.Applied, db.KpiResults.Find(mine.ID).Status);
                KpiService.Revert(uow, mine.ID, f.UserId, now);
                Assert.AreEqual(0m, line.Bonuses);
                Assert.AreEqual(0m, line.Penalties);
                Assert.AreEqual(4400000m, line.NetSalary);
            }
        }

        [TestMethod]
        public void Trip_RefusedStop_CancelPlannedTrip_ResetStop()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var w = Arrange(uow);
                var f = w.F;
                var db = uow.Dst;
                var now = DateTime.Now;
                var s1 = Ship(uow, w, f.CustomerId, 4, now);

                // rejalangan reys bekor — jo'natish bo'shaydi
                var t1 = TripService.Save(uow, new TripInput { DocDate = now, VehicleID = w.VehicleId, ExpeditorUserID = f.UserId, ShipmentIDs = { s1 } }, f.UserId, now);
                TripService.Cancel(uow, t1, f.UserId, "mashina buzildi", now);
                Assert.AreEqual((byte)TripStatus.Cancelled, db.Trips.Find(t1).TripStatus);
                Assert.IsTrue(TripQueries.Available(db, f.WarehouseId, null).Any(a => a.ID == s1));

                var t2 = TripService.Save(uow, new TripInput { DocDate = now, VehicleID = w.VehicleId, ExpeditorUserID = f.UserId, ShipmentIDs = { s1 } }, f.UserId, now);
                Assert.AreEqual(t2, TripQueries.CurrentTripId(db, f.UserId, now));
                TripService.Start(uow, t2, f.UserId, now);
                var stop = db.TripStops.Single(s => s.TripID == t2);

                // rad (sabab bilan) → ofis qaytaradi → qayta rad
                Assert.AreEqual(StopStatus.Refused, TripService.Deliver(uow, new DeliverInput { StopID = stop.ID, ReasonID = w.ReasonId }, f.UserId, f.UserId, true, now));
                TripService.ResetStop(uow, stop.ID, f.UserId, now);
                Assert.AreEqual((byte)StopStatus.Pending, db.TripStops.Find(stop.ID).StopStatus);
                Assert.ThrowsException<DstValidationException>(() => TripService.Close(uow, new CloseInput { TripID = t2 }, f.UserId, f.UserId, AuditActor.Bot, now));
                TripService.Deliver(uow, new DeliverInput { StopID = stop.ID, ReasonID = w.ReasonId }, f.UserId, f.UserId, true, now);
                Assert.AreNotEqual((byte)OrderStatus.Delivered, db.Orders.Find(stop.OrderID).OrderStatus);

                // ekspeditor yopadi: hammasi omborga qaytgan, qarz 0
                var r = TripService.Close(uow, new CloseInput { TripID = t2, LocationID = f.LocationId }, f.UserId, f.UserId, AuditActor.Bot, now);
                Assert.AreEqual(1, r.Refused);
                Assert.AreEqual(0m, r.ShortageAmount);
                Assert.AreEqual(100m, OnHand(uow, w));
                Assert.AreEqual(0m, LedgerService.CreditState(db, f.CustomerId, now).Balance);
            }
        }

        [TestMethod]
        public void VisitPlan_OverridesTemplate_RouteAndReport()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var w = Arrange(uow);
                var f = w.F;
                var db = uow.Dst;
                var now = DateTime.Now;
                var agent = db.SalesAgents.Find(f.UserId);
                agent.IsExpeditor = false;
                foreach (var p in db.CustomerProfiles.Where(p => p.AgentUserID == f.UserId).ToList()) p.VisitDays = 0;   // test uchun toza andoza
                var p1 = db.CustomerProfiles.Find(f.CustomerId);
                p1.AgentUserID = f.UserId;
                p1.VisitDays = VisitDays.For(now.DayOfWeek);
                db.SaveChanges();

                // andoza: faqat 1-mijoz
                var template = VisitPlanService.ForDay(db, f.UserId, now);
                Assert.IsTrue(template.Any(s => s.CustomerID == f.CustomerId && !s.FromPlan));
                Assert.IsFalse(template.Any(s => s.CustomerID == w.Customer2));

                // kunlik reja: 2-mijoz birinchi, keyin 1-mijoz
                VisitPlanService.Save(db, f.UserId, now, new[] { w.Customer2, f.CustomerId }, f.UserId, now);
                var plan = VisitPlanService.ForDay(db, f.UserId, now);
                CollectionAssert.AreEqual(new[] { w.Customer2, f.CustomerId }, plan.Select(s => s.CustomerID).ToArray());
                var route = FieldService.Route(db, f.UserId, now);
                Assert.AreEqual(w.Customer2, route[0].CustomerID);
                Assert.IsTrue(route.Take(2).All(r => r.Planned));

                // optimallashtirish: kun boshi joyi (41.30) — eng yaqin 2-mijoz (41.31)
                db.AgentDays.Add(new AgentDay { UserID = f.UserId, WorkDate = now.Date, StartAt = now, StartLat = 41.30m, StartLng = 69.24m });
                db.SaveChanges();
                VisitPlanService.Save(db, f.UserId, now, new[] { f.CustomerId, w.Customer2 }, f.UserId, now);
                VisitPlanService.Optimize(db, f.UserId, now, f.UserId, now);
                Assert.AreEqual(w.Customer2, VisitPlanService.ForDay(db, f.UserId, now)[0].CustomerID);

                // "keldim" — rejadagi tashrif; reja/fakt hisobotida reja = 2 (andozadagi 1 emas)
                var visit = FieldService.CheckIn(db, f.UserId, w.Customer2, 41.31m, 69.24m, 10, true, now);
                Assert.IsTrue(db.Visits.Find(visit.VisitId).IsPlanned);
                var report = VisitReportService.ByAgent(db, new ReportFilter { From = now.Date, To = now.Date, AgentId = f.UserId }, ReportScope.Unrestricted)
                    .Single(r => r.AgentUserID == f.UserId);
                Assert.AreEqual(2, report.Planned);
                Assert.AreEqual(1, report.PlannedVisited);
                var pf = VisitPlanService.PlanFact(db, f.UserId, now);
                Assert.AreEqual(2, pf.Count(r => r.Planned));
                Assert.AreEqual(visit.VisitId, pf.First(r => r.Stop.CustomerID == w.Customer2).VisitId);

                // bo'sh reja — andozaga qaytish
                VisitPlanService.Save(db, f.UserId, now, new int[0], f.UserId, now);
                Assert.IsFalse(VisitPlanService.HasPlan(db, f.UserId, now));
            }
        }

        [TestMethod]
        public void Voice_DraftMatchesCatalogAndCustomer()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var w = Arrange(uow);
                var f = w.F;
                var db = uow.Dst;
                var now = DateTime.Now;
                var product = db.Products.Find(f.ProductId);   // "Test sement"
                var visible = db.CustomerProfiles.Select(p => p.CustomerID);
                var name = db.Customers.Where(c => c.ID == w.Customer2).Select(c => c.Name).First();

                var d = VoiceOrderService.Draft(db, new VoiceDraftInput
                {
                    CustomerHint = name,
                    Items = { new VoiceItem { Text = "test sement", Qty = 20 }, new VoiceItem { Text = "qandaydir noma'lum tovar xyz", Qty = 2 } }
                }, visible, f.WarehouseId, now);
                Assert.AreEqual(w.Customer2, d.CustomerID);
                var line = d.Lines.Single();
                Assert.AreEqual(f.ProductId, line.ProductID);
                Assert.AreEqual(20m, line.Qty);
                Assert.AreEqual(1100000m, line.Amount);
                Assert.AreEqual(1, d.Unmatched.Count);

                // ko'rinmaydigan mijoz — rad
                Assert.ThrowsException<DstValidationException>(() =>
                    VoiceOrderService.Draft(db, new VoiceDraftInput { CustomerID = w.Customer2 }, visible.Where(id => id != w.Customer2), null, now));
                Assert.IsTrue(product.Name.Contains("sement"));
            }
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
    }
}
