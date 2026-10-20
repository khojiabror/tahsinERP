using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Bot;
using tahsinERP.Areas.Distribution.Services.Money;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Configuration;

namespace tahsinERP.Tests.Distribution
{
    /// <summary>
    /// S6 pilot kuni — uchidan-uchiga, HAQIQIY yozuvlar bilan (rollback yo'q): Excel'dan boshlang'ich ma'lumot →
    /// agent (bot API'si chaqiradigan servislar: kun boshi, "Keldim", buyurtma, to'lov, buyurtmasiz) → operator tasdig'i
    /// (limit va rahbar ruxsati) → ombor (yig'ish, jo'natish, qisman + qoldiqni yopish) → yetkazildi → kassaga topshirish →
    /// qaytarish → kun yakuni. Oxirida qabul mezonlari: qoldiq harakati = −jo'natilgan + qaytgan, mijoz balansi = kirish
    /// qarzi + jo'natish − to'lov − qaytarish = akt sverki, kassa = qabul qilingan naqd.
    ///
    /// Faqat sinov bazasida: DST_PILOT_E2E muhit o'zgaruvchisi joriy baza nomiga teng bo'lsa ishlaydi
    /// (masalan TAHSINERP_DB=...Initial Catalog=DB_DST_S6... va DST_PILOT_E2E=DB_DST_S6). Har ishga tushish yangi mijozlar
    /// bilan — takror ishlatish mumkin (pilot mashqi).
    /// </summary>
    [TestClass]
    [TestCategory("PilotE2E")]
    public class PilotDayScenarioTests
    {
        private const string AgentLogin = "demo";
        private const string OfficeLogin = "admin";
        private const int CashAccountId = 1;   // demo bazada: Kassa
        private const int BankAccountId = 2;   // demo bazada: Hisob raqam

        private static string _sql;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            try
            {
                var cs = DbConfig.GetSqlConnectionString();
                var db = new SqlConnectionStringBuilder(cs).InitialCatalog;
                _sql = string.Equals(Environment.GetEnvironmentVariable("DST_PILOT_E2E"), db, StringComparison.OrdinalIgnoreCase) ? cs : null;
            }
            catch (Exception)
            {
                _sql = null;
            }
        }

        [TestInitialize]
        public void RequireDatabase()
        {
            if (_sql == null) Assert.Inconclusive("DST_PILOT_E2E joriy sinov bazasi nomiga teng emas — ssenariy yozadi, faqat sinov bazasida.");
        }

        private static object Scalar(string sql)
        {
            using (var c = new SqlConnection(_sql))
            using (var cmd = new SqlCommand(sql, c))
            {
                c.Open();
                return cmd.ExecuteScalar();
            }
        }

        private static Stream Xlsx(params string[][] rows)
        {
            var ms = new MemoryStream();
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Import");
                for (int r = 0; r < rows.Length; r++)
                    for (int c = 0; c < rows[r].Length; c++)
                        if (rows[r][c] != null) ws.Cell(r + 1, c + 1).SetValue(rows[r][c]);
                wb.SaveAs(ms);
            }
            ms.Position = 0;
            return ms;
        }

        private static void Ok(ImportResult r, string what) =>
            Assert.IsTrue(r.Ok, what + ": " + string.Join("; ", r.Errors.Select(e => e.Row + ": " + e.Message)));

        [TestMethod]
        public void FullDay_FromImportToDayEnd()
        {
            var now = DateTime.Now;
            string tag = now.ToString("MMddHHmmss");
            string today = (((int)now.DayOfWeek + 6) % 7 + 1).ToString();   // Du=1 … Ya=7
            int agent = Convert.ToInt32(Scalar("SELECT ID FROM dbo.USERS WHERE Uname = '" + AgentLogin + "'"));
            int office = Convert.ToInt32(Scalar("SELECT ID FROM dbo.USERS WHERE Uname = '" + OfficeLogin + "'"));
            Func<DistributionUnitOfWork> uowAsOffice = () => new DistributionUnitOfWork(new AuditActor(office, AuditActor.Web));
            Func<DistributionUnitOfWork> uowAsBot = () => new DistributionUnitOfWork(new AuditActor(agent, AuditActor.Bot));

            // ---------- 0. jamoa: agent (Jamoalar sahifasida qo'lda qilinadi) ----------
            int warehouse;
            using (var uow = uowAsOffice())
            {
                warehouse = uow.Dst.Warehouses.Where(w => w.IsDeleted != true).ToList()
                    .Single(w => string.Equals(w.WarehouseType, "SALES", StringComparison.OrdinalIgnoreCase)).ID;
                var a = uow.Dst.SalesAgents.Find(agent);
                if (a == null) uow.Dst.SalesAgents.Add(a = new SalesAgent { UserID = agent });
                a.IsActive = true;
                a.IsExpeditor = false;
                a.WarehouseID = warehouse;
                uow.Dst.SaveChanges();
                uow.Commit();
            }

            // ---------- 1. boshlang'ich ma'lumot (Import sahifasi tartibida, haqiqiy yozuv) ----------
            var territories = new[]
            {
                new[] { "Viloyat", "Viloyat (rus)", "Tuman", "Tuman (rus)", "Hudud" },
                new[] { "Toshkent sh.", "г. Ташкент", "Chilonzor", "Чиланзар", "Chilonzor bozori" },
                new[] { "Toshkent sh.", null, "Yunusobod", "Юнусабад", null },
            };
            Ok(ImportService.ImportTerritories(Xlsx(territories), uowAsOffice), "hududlar");
            var again = ImportService.ImportTerritories(Xlsx(territories), uowAsOffice);
            Ok(again, "hududlar (takror)");
            Assert.AreEqual(0, again.Created, "takror yuklash yangi hudud yaratmaydi");

            Ok(ImportService.ImportPriceTypes(Xlsx(
                new[] { "Nomi", "Nomi (rus)", "Standart (ha/yo'q)" },
                new[] { "Chakana", "Розница", "ha" },
                new[] { "Ulgurji", "Опт", null }), uowAsOffice), "narx turlari");

            string c1 = "Pilot Baraka " + tag, c2 = "Pilot Usta " + tag, c3 = "Pilot Mega " + tag;
            const decimal lat1 = 41.2856m, lng1 = 69.2034m;
            Ok(ImportService.ImportCustomers(Xlsx(
                new[] { "Nomi", "Telefon", "Hudud", "Agent (login)", "Narx turi", "Tashrif kunlari (1-7)", "Kredit limiti", "Kechiktirish (kun)", "Kenglik", "Uzunlik", "Toifa (A/B/C)" },
                new[] { c1, "+998901112233", "Chilonzor / Chilonzor bozori", AgentLogin, "Ulgurji", today, "10000000", "10", lat1.ToString(System.Globalization.CultureInfo.InvariantCulture), lng1.ToString(System.Globalization.CultureInfo.InvariantCulture), "A" },
                new[] { c2, null, "Yunusobod", AgentLogin, "Ulgurji", today, "300000", "7", null, null, "B" },
                new[] { c3, null, "Chilonzor", null, null, null, "0", "0", null, null, "C" }), office, uowAsOffice), "mijozlar");
            // marshrut: c3 ni ham agentga, bugungi kunga
            Ok(ImportService.ImportRoutes(Xlsx(
                new[] { "Mijoz (STIR yoki nomi)", "Agent (login)", "Tashrif kunlari (1-7)" },
                new[] { c3, AgentLogin, today }), uowAsOffice), "marshrutlar");

            Ok(ImportService.ImportUnits(Xlsx(
                new[] { "Artikul", "Birlik", "Koeffitsient", "Odatiy sotish (ha/yo'q)" },
                new[] { "TM-011", "juft", "2", "yo'q" }), uowAsOffice), "birliklar");
            Ok(ImportService.ImportPrices(Xlsx(
                new[] { "Artikul", "Chakana", "Ulgurji", "Ulgurji min" },
                new[] { "TM-001", "35000", "30000", "28000" },
                new[] { "TM-002", "52000", "45000", "42000" },
                new[] { "TM-011", "140000", "120000", "110000" }), office, uowAsOffice), "narxlar");
            Ok(ImportService.ImportOpening(Xlsx(
                new[] { "Mijoz (STIR yoki nomi)", "Qarz (+) / avans (-)", "To'lov muddati" },
                new[] { c1, "1500000", now.AddDays(10).ToString("dd.MM.yyyy") },
                new[] { c2, "50000", now.AddDays(-1).ToString("dd.MM.yyyy") }), office, uowAsOffice), "kirish qarzlari");

            int id1, id2, id3, p001, p002, p011, juftUnit;
            using (var db = new DistributionDbContext())
            {
                id1 = db.Customers.Single(c => c.Name == c1).ID;
                id2 = db.Customers.Single(c => c.Name == c2).ID;
                id3 = db.Customers.Single(c => c.Name == c3).ID;
                p001 = db.Products.Single(p => p.PNo == "TM-001").ID;
                p002 = db.Products.Single(p => p.PNo == "TM-002").ID;
                p011 = db.Products.Single(p => p.PNo == "TM-011").ID;
                var juft = db.Units.Single(u => u.ShortName == "juft").ID;
                juftUnit = db.ProductUnits.Single(u => u.ProductID == p011 && u.UnitID == juft).ID;
                // ma'lumotnoma: mijozlar tasdiqlangan (import — ofis), hudud va narx turi bog'langan
                Assert.IsTrue(db.CustomerProfiles.Where(p => new[] { id1, id2, id3 }.Contains(p.CustomerID)).All(p => p.IsApproved));
            }
            var products = new[] { p001, p002, p011 };
            Dictionary<int, decimal> stockBefore;
            using (var db = new DistributionDbContext())
                stockBefore = StockService.Levels(db, products, new[] { warehouse }).ToDictionary(l => l.ProductID, l => l.OnHand);

            // ---------- 2. agent kuni (bot: /DstApi/DayStart, Route, CheckIn, Order, Payment, VisitResult) ----------
            int visit1, visit2, visit3;
            using (var db = new DistributionDbContext { Actor = new AuditActor(agent, AuditActor.Bot) })
            {
                FieldService.StartDay(db, agent, lat1, lng1, null, now);
                db.SaveChanges();
                var route = FieldService.Route(db, agent, now);
                CollectionAssert.IsSubsetOf(new[] { id1, id2, id3 }, route.Where(r => r.Planned).Select(r => r.CustomerID).ToList(), "bugungi marshrut");
                Assert.AreEqual(1500000m, route.Single(r => r.CustomerID == id1).Debt);

                Assert.ThrowsException<DstValidationException>(() => FieldService.CheckIn(db, agent, id1, lat1 + 0.01m, lng1, 10, true, now), "1 km uzoqdan — rad");
                visit1 = FieldService.CheckIn(db, agent, id1, lat1 + 0.0005m, lng1, 15, true, now).VisitId;   // ~55 m
                visit2 = FieldService.CheckIn(db, agent, id2, 41.36m, 69.28m, 20, true, now).VisitId;        // koordinatasiz mijoz
                visit3 = FieldService.CheckIn(db, agent, id3, 41.29m, 69.21m, 20, true, now).VisitId;
            }

            var request1 = Guid.NewGuid();
            int order1 = AgentOrder(uowAsBot, agent, warehouse, id1, request1, visit1,
                new OrderLineInput { ProductID = p001, Qty = 20 },
                new OrderLineInput { ProductID = p011, ProductUnitID = juftUnit, Qty = 3 });
            Assert.AreEqual(order1, AgentOrder(uowAsBot, agent, warehouse, id1, request1, visit1,
                new OrderLineInput { ProductID = p001, Qty = 20 }), "bot qayta yuborsa — o'sha buyurtma");
            int order2 = AgentOrder(uowAsBot, agent, warehouse, id2, Guid.NewGuid(), visit2, new OrderLineInput { ProductID = p002, Qty = 10 });

            using (var db = new DistributionDbContext { Actor = new AuditActor(agent, AuditActor.Bot) })
            {
                var o1 = db.Orders.Find(order1);
                Assert.AreEqual(20 * 30000m + 3 * 2 * 120000m, o1.Total, "narx: mijoz narx turi (Ulgurji), juft = 2 dona");
                Assert.AreEqual(AuditActor.Bot, o1.Source);
                Assert.AreEqual(450000m, db.Orders.Find(order2).Total);
                var noOrder = db.Reasons.First(r => r.Type == ReasonTypes.NoOrder).ID;
                Assert.ThrowsException<DstValidationException>(() => FieldService.SetResult(db, visit3, agent, VisitResults.NoOrder, null, null, null, null, null, now));
                FieldService.SetResult(db, visit3, agent, VisitResults.NoOrder, noOrder, null, null, null, "do'kon yopiq", now);
                db.SaveChanges();
            }

            // ---------- 3. operator: tasdiq, limit, rahbar ruxsati ----------
            var officeActor = new OrderActor { UserId = office, MaxDiscountPct = 5 };
            using (var uow = uowAsOffice())
            {
                OrderService.Confirm(uow, order1, officeActor, null, now);
                uow.Commit();
            }
            using (var uow = uowAsOffice())
            {
                var blocked = Assert.ThrowsException<CreditBlockedException>(() => OrderService.Confirm(uow, order2, officeActor, null, now));
                Assert.IsTrue(blocked.Problems.Count >= 1, "muddati o'tgan qarz va limit");
            }
            using (var uow = uowAsOffice())
            {
                OrderService.Confirm(uow, order2, new OrderActor { UserId = office, CanOverrideCredit = true }, "Rahbar ruxsati — pilot", now);
                uow.Commit();
            }
            using (var db = new DistributionDbContext())
            {
                Assert.AreEqual((byte)OrderStatus.Confirmed, db.Orders.Find(order2).OrderStatus);
                Assert.IsNotNull(db.Orders.Find(order2).CreditOverrideBy);
                Assert.AreEqual(26m, (from r in db.StockReservations join l in db.OrderLines on r.OrderLineID equals l.ID where l.OrderID == order1 select (decimal?)r.QtyBase).Sum() ?? 0, "rezerv: 20 + 3×2");
            }

            // ---------- 4. ombor: yig'ish, jo'natish (qisman), yetkazildi ----------
            int shipment1;
            using (var uow = uowAsOffice())
            {
                OrderService.StartPicking(uow, order1, office, now);
                shipment1 = OrderService.Ship(uow, order1, null, office, now);
                OrderService.MarkDelivered(uow, order1, office, now);

                OrderService.StartPicking(uow, order2, office, now);
                var line2 = uow.Dst.OrderLines.Single(l => l.OrderID == order2);
                OrderService.Ship(uow, order2, new Dictionary<int, decimal> { { line2.ID, 6 } }, office, now);
                OrderService.CloseRemainder(uow, order2, office, "Qoldiq keyingi reysda emas — mijoz rozi", now);
                OrderService.MarkDelivered(uow, order2, office, now);
                uow.Commit();
            }
            using (var db = new DistributionDbContext())
            {
                Assert.AreEqual((byte)OrderStatus.Delivered, db.Orders.Find(order1).OrderStatus);
                Assert.AreEqual((byte)OrderStatus.Delivered, db.Orders.Find(order2).OrderStatus);
                Assert.IsFalse((from r in db.StockReservations join l in db.OrderLines on r.OrderLineID equals l.ID where l.OrderID == order1 || l.OrderID == order2 select r).Any(), "rezerv yechildi");
                Assert.IsNotNull(db.Shipments.Find(shipment1).CostTotal, "FIFO tannarx");
            }

            // ---------- 5. pul: agent naqdi (bot), ofis to'lovi, kassaga topshirish ----------
            var payRequest = Guid.NewGuid();
            int pay1 = AgentCash(uowAsBot, agent, id1, order1, 1000000m, payRequest, visit1);
            Assert.AreEqual(pay1, AgentCash(uowAsBot, agent, id1, order1, 1000000m, payRequest, visit1), "to'lov tugmasi qayta bosilsa — bitta to'lov");
            using (var uow = uowAsOffice())
            {
                PaymentService.Receive(uow, new PaymentInput { CustomerID = id2, Amount = 200000m, Method = PaymentMethods.Card, FinAccountID = BankAccountId }, office, AuditActor.Web, now);
                uow.Commit();
            }
            decimal cashBefore = CashBalance(CashAccountId);
            int handover;
            using (var uow = uowAsBot())
            {
                handover = PaymentService.SubmitHandover(uow, agent, CashAccountId, agent, null, now);
                uow.Commit();
            }
            using (var uow = uowAsOffice())
            {
                Assert.AreEqual(1000000m, uow.Dst.CashHandovers.Find(handover).AmountDeclared);
                PaymentService.ConfirmHandover(uow, handover, 990000m, office, now);   // kassir sanadi: 10 000 kam
                uow.Commit();
            }
            Assert.AreEqual(990000m, CashBalance(CashAccountId) - cashBefore, "kassa = haqiqatda qabul qilingan naqd");

            // ---------- 6. qaytarish (yaroqli, buyurtma bo'yicha) ----------
            using (var uow = uowAsOffice())
            {
                var shipLine = uow.Dst.ShipmentLines.Single(l => l.ShipmentID == shipment1 && l.ProductID == p001);
                ReturnService.Create(uow, new ReturnInput
                {
                    CustomerID = id1, OrderID = order1, WarehouseID = warehouse, IsDefect = false,
                    ReasonID = uow.Dst.Reasons.First(r => r.Type == ReasonTypes.Return).ID,
                    Lines = { new ReturnLineInput { ShipmentLineID = shipLine.ID, ProductID = p001, QtyBase = 2 } }
                }, office, AuditActor.Web, now);
                uow.Commit();
            }

            // ---------- 7. kun yakuni ----------
            using (var db = new DistributionDbContext { Actor = new AuditActor(agent, AuditActor.Bot) })
            {
                var summary = FieldService.EndDay(db, agent, lat1, lng1, now);
                Assert.IsTrue(summary.Visited >= 3);
                Assert.IsTrue(summary.NoOrder >= 1);
                Assert.AreEqual(0m, summary.CashOnHand, "agent qo'lida pul qolmadi");
                var daily = DailySummaryService.Compute(db, now);
                Assert.IsTrue(daily.Orders >= 2 && daily.Shipped >= 720000m + 600000m + 270000m && daily.Collected >= 1200000m);
            }

            // ---------- 8. qabul mezonlari ----------
            using (var db = new DistributionDbContext())
            {
                var after = StockService.Levels(db, products, new[] { warehouse }).ToDictionary(l => l.ProductID, l => l.OnHand);
                Assert.AreEqual(stockBefore[p001] - 20 + 2, after[p001], "TM-001: −20 jo'natildi, +2 qaytdi");
                Assert.AreEqual(stockBefore[p011] - 6, after[p011], "TM-011: 3 juft = 6 dona");
                Assert.AreEqual(stockBefore[p002] - 6, after[p002], "TM-002: 10 dan 6 tasi jo'natildi");

                AssertBalance(db, id1, 1500000m + 1320000m - 1000000m - 2 * 30000m, now);
                AssertBalance(db, id2, 50000m + 6 * 45000m - 200000m, now);
                AssertBalance(db, id3, 0m, now);

                var since = now.AddMinutes(-1);
                Assert.IsTrue(db.Notifications.Any(n => n.CreatedAt >= since) || !db.TelegramAccounts.Any(),
                    "bildirishnoma faqat Telegram'ga ulanganlarga yoziladi");
                Assert.IsTrue(db.AuditTrail.Any(a => a.EntityName == nameof(DstOrder) && a.EntityKey == order1.ToString()), "audit: buyurtma o'zgarishlari");
            }
        }

        /// <summary>Bot API'si (DstApi/Order) qiladigan ish: agent nomidan, agent omboridan, tashrifga bog'lab.</summary>
        private static int AgentOrder(Func<DistributionUnitOfWork> openUow, int agent, int warehouse, int customerId, Guid request, int visitId, params OrderLineInput[] lines)
        {
            using (var uow = openUow())
            {
                var input = new OrderInput { CustomerID = customerId, AgentUserID = agent, WarehouseID = warehouse, ClientRequestId = request };
                input.Lines.AddRange(lines);
                var id = OrderService.SaveDraft(uow, input, new OrderActor { UserId = agent, Source = AuditActor.Bot }, DateTime.Now);
                FieldService.SetResult(uow.Dst, visitId, agent, null, null, id, null, null, null, DateTime.Now);
                uow.Dst.SaveChanges();
                uow.Commit();
                return id;
            }
        }

        /// <summary>Bot API'si (DstApi/Payment): naqd — agent qo'lida (hisobsiz).</summary>
        private static int AgentCash(Func<DistributionUnitOfWork> openUow, int agent, int customerId, int orderId, decimal amount, Guid request, int visitId)
        {
            using (var uow = openUow())
            {
                var id = PaymentService.Receive(uow, new PaymentInput
                {
                    CustomerID = customerId, OrderID = orderId, Amount = amount, Method = PaymentMethods.Cash, ReceivedByUserID = agent, ClientRequestId = request
                }, agent, AuditActor.Bot, DateTime.Now);
                FieldService.SetResult(uow.Dst, visitId, agent, null, null, null, id, null, null, DateTime.Now);
                uow.Dst.SaveChanges();
                uow.Commit();
                return id;
            }
        }

        private static decimal CashBalance(int accountId) =>
            Convert.ToDecimal(Scalar("SELECT ISNULL(SUM(CASE WHEN Direction = 1 THEN Amount ELSE -Amount END), 0) FROM dbo.FIN_TRANSACTIONS " +
                                     "WHERE AccountID = " + accountId + " AND ISNULL(IsDeleted, 0) = 0"));

        private static void AssertBalance(DistributionDbContext db, int customerId, decimal expected, DateTime now)
        {
            Assert.AreEqual(expected, LedgerService.CreditState(db, customerId, now).Balance, "balans " + customerId);
            Assert.AreEqual(expected, DebtService.Aging(db, customerId, now).Total, "aging " + customerId);
            Assert.AreEqual(expected, DebtService.BuildStatement(db, customerId, now.AddMonths(-1), now).Closing, "akt sverki " + customerId);
        }
    }
}
