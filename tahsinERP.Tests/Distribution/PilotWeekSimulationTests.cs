using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
    /// S6 pilot mashqi вЂ” namunaviy kompaniya bilan 5 agent Г— 5 ish kuni, HAQIQIY yozuvlar bilan (faqat sinov bazasida,
    /// DST_PILOT_E2E = baza nomi). Oqim: Excel import (PilotSampleData) в†’ boshlang'ich inventarizatsiya в†’ har kuni har agent
    /// o'z marshruti: "Keldim", buyurtma / buyurtmasiz, to'lov (naqd/karta/Click) в†’ ofis tasdig'i (limit в†’ rahbar ruxsati) в†’
    /// jo'natish (ba'zan qisman) в†’ yetkazildi в†’ kechqurun kassaga topshirish в†’ ba'zan qaytarish, xato jurnaliga yozuv.
    /// Oxirida TZ qabul mezoni mustaqil model bilan: nazorat sanog'i (sanalgan = kirim в€’ jo'natilgan + qaytgan) farqsiz,
    /// har mijoz qarzi = model = aging = akt sverki, kassa/bank = qabul qilingan pul, agentlar qo'lida 0.
    /// Natija (metrikalar) DST_PILOT_REPORT faylga yoziladi (berilgan bo'lsa).
    /// </summary>
    [TestClass]
    [TestCategory("PilotE2E")]
    public class PilotWeekSimulationTests
    {
        private const int CashAccountId = 1, BankAccountId = 2;
        private static string _sql;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            try
            {
                var cs = DbConfig.GetSqlConnectionString();
                _sql = string.Equals(Environment.GetEnvironmentVariable("DST_PILOT_E2E"), new SqlConnectionStringBuilder(cs).InitialCatalog, StringComparison.OrdinalIgnoreCase) ? cs : null;
            }
            catch (Exception)
            {
                _sql = null;
            }
        }

        [TestInitialize]
        public void RequireDatabase()
        {
            if (_sql == null) Assert.Inconclusive("DST_PILOT_E2E joriy sinov bazasi nomiga teng emas вЂ” simulyatsiya yozadi, faqat sinov bazasida.");
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

        private static void Ok(ImportResult r, string what) =>
            Assert.IsTrue(r.Ok, what + ": " + string.Join("; ", r.Errors.Take(10).Select(e => e.Row + ": " + e.Message)));

        private static decimal CashBalance(int accountId) =>
            Convert.ToDecimal(Scalar("SELECT ISNULL(SUM(CASE WHEN Direction = 1 THEN Amount ELSE -Amount END), 0) FROM dbo.FIN_TRANSACTIONS " +
                                     "WHERE AccountID = " + accountId + " AND ISNULL(IsDeleted, 0) = 0"));

        [TestMethod]
        public void FiveAgents_FiveDays_AcceptanceCriteria()
        {
            var start = DateTime.Today.AddDays(1).AddHours(9);   // narxlar import paytidan amal qiladi — pilot ertasi kundan
            var rnd = new Random(20261020);
            var report = new StringBuilder();
            int office = Convert.ToInt32(Scalar("SELECT ID FROM dbo.USERS WHERE Uname = 'admin'"));
            Func<DistributionUnitOfWork> asOffice = () => new DistributionUnitOfWork(new AuditActor(office, AuditActor.Web));

            // ---------- 0. jamoa: 5 agent (ERP foydalanuvchisi + Jamoalar) va to'lov sozlamalari ----------
            var agentIds = new Dictionary<string, int>();
            int warehouse;
            using (var uow = asOffice())
            {
                warehouse = uow.Dst.Warehouses.Where(w => w.IsDeleted != true).ToList().Single(w => string.Equals(w.WarehouseType, "SALES", StringComparison.OrdinalIgnoreCase)).ID;
                for (int i = 0; i < PilotSampleData.Agents.Length; i++)
                {
                    var login = PilotSampleData.Agents[i];
                    uow.Dst.Database.ExecuteSqlCommand(
                        "IF NOT EXISTS (SELECT 1 FROM dbo.USERS WHERE Uname = @p0) INSERT dbo.USERS (Uname, FullName, Password, IsDeleted, IsActive, MustChangePassword) VALUES (@p0, @p1, '!', 0, 1, 0)",
                        login, PilotSampleData.AgentNames[i]);
                    int id = uow.Dst.Database.SqlQuery<int>("SELECT ID FROM dbo.USERS WHERE Uname = @p0", login).Single();
                    agentIds[login] = id;
                    var a = uow.Dst.SalesAgents.Find(id);
                    if (a == null) uow.Dst.SalesAgents.Add(a = new SalesAgent { UserID = id });
                    a.IsActive = true;
                    a.IsExpeditor = false;
                    a.WarehouseID = warehouse;
                }
                DstSettings.Set(uow.Dst, DstSetting.AgentPayMethods, "CASH,CARD,CLICK");
                DstSettings.Set(uow.Dst, AgentPayments.AccountKey(PaymentMethods.Card), BankAccountId.ToString());
                DstSettings.Set(uow.Dst, AgentPayments.AccountKey(PaymentMethods.Click), BankAccountId.ToString());
                DstSettings.Set(uow.Dst, DstSetting.AgentCashAccountId, CashAccountId.ToString());
                uow.Dst.SaveChanges();
                uow.Commit();
            }

            // ---------- 1. boshlang'ich ma'lumot (namunaviy fayllar, Import sahifasi tartibida) ----------
            var files = PilotSampleData.Files(start.Date);
            var outDir = Environment.GetEnvironmentVariable("DST_PILOT_SAMPLE_OUT");
            if (!string.IsNullOrEmpty(outDir))
            {
                Directory.CreateDirectory(outDir);
                foreach (var f in files) File.WriteAllBytes(Path.Combine(outDir, f.Key), f.Value());
            }
            Stream F(string name) => new MemoryStream(files[name]());
            Ok(ImportService.ImportTerritories(F("1_hududlar.xlsx"), asOffice), "hududlar");
            Ok(ImportService.ImportPriceTypes(F("2_narx_turlari.xlsx"), asOffice), "narx turlari");
            bool firstRun = (int)Scalar("SELECT COUNT(*) FROM dbo.PRODUCTS WHERE PNo = 'QM-SEM400'") == 0;
            if (firstRun)
            {
                Ok(ImportService.ImportCustomers(F("3_mijozlar.xlsx"), office, asOffice), "mijozlar");
                Ok(ImportService.ImportRoutes(F("4_marshrutlar.xlsx"), asOffice), "marshrutlar");
                Ok(ImportService.ImportProducts(F("5_tovarlar.xlsx"), asOffice), "tovarlar");
                Ok(ImportService.ImportUnits(F("6_birliklar.xlsx"), asOffice), "birliklar");
                Ok(ImportService.ImportPrices(F("7_narxlar.xlsx"), office, asOffice), "narxlar");
                Ok(ImportService.ImportOpening(F("8_kirish_qarzlari.xlsx"), office, asOffice), "kirish qarzlari");
            }
            else
            {
                // takror ishga tushirish: ma'lumotnomalar takror yuklash xavfsiz, qarzlar takror yozilmaydi
                Ok(ImportService.ImportCustomers(F("3_mijozlar.xlsx"), office, asOffice), "mijozlar (takror)");
                Assert.IsFalse(ImportService.ImportOpening(F("8_kirish_qarzlari.xlsx"), office, asOffice).Ok, "kirish qarzi ikki marta yozilmaydi");
            }

            var sample = PilotSampleData.Customers();
            Dictionary<string, int> custByInn;
            Dictionary<string, (int Id, int UnitId)> products;
            Dictionary<int, int> unit2;   // productId -> ProductUnitID
            using (var db = new DistributionDbContext())
            {
                var inns = sample.Select(c => c.Inn).ToList();
                custByInn = db.CustomerProfiles.Where(p => inns.Contains(p.INN)).ToList().ToDictionary(p => p.INN, p => p.CustomerID);
                var pnos = PilotSampleData.Products.Select(p => p.PNo).ToList();
                products = db.Products.Where(p => pnos.Contains(p.PNo)).ToList().ToDictionary(p => p.PNo, p => (p.ID, p.UnitID));
                var ids = products.Values.Select(v => v.Id).ToList();
                unit2 = db.ProductUnits.Where(u => ids.Contains(u.ProductID) && u.IsActive).ToList().ToDictionary(u => u.ProductID, u => u.ID);
            }
            Assert.AreEqual(40, custByInn.Count, "40 savdo nuqtasi");
            Assert.AreEqual(PilotSampleData.Products.Length, products.Count);

            // ---------- 2. boshlang'ich inventarizatsiya (birinchi ishga tushishda) ----------
            if (firstRun)
            {
                int countId;
                using (var uow = asOffice())
                {
                    countId = InventoryCountService.Create(uow, warehouse, InventoryKinds.Opening, "Pilot boshi", office, start);
                    uow.Commit();
                }
                using (var db = new DistributionDbContext { Actor = new AuditActor(office, AuditActor.Web) })
                {
                    // bu omborning boshqa tovarlari вЂ” hozirgi qoldiq bilan (o'zgarishsiz), pilot tovarlari вЂ” fayldan
                    var count = db.InventoryCounts.Find(countId);
                    var rows = InventoryCountService.Rows(db, count);
                    InventoryCountService.SaveCounts(db, countId, rows.Select(r => (r.LineID, (decimal?)r.System, (decimal?)null, (string)null)));
                    Ok(InventoryCountService.ImportSheet(db, countId, F("9_boshlangich_qoldiq.xlsx")), "boshlang'ich qoldiq");
                }
                using (var uow = asOffice())
                {
                    InventoryCountService.PostAsync(uow, countId, false, office, start).GetAwaiter().GetResult();
                    uow.Commit();
                }
                using (var db = new DistributionDbContext())
                {
                    var c = db.InventoryCounts.Find(countId);
                    Assert.IsTrue(c.Applied && c.AdjustmentID.HasValue, "boshlang'ich sanoq qoldiqqa yozildi");
                }
            }

            var productIds = products.Values.Select(p => p.Id).ToList();
            Dictionary<int, decimal> stock;   // mustaqil model: qoldiq
            using (var db = new DistributionDbContext())
                stock = StockService.Levels(db, productIds, new[] { warehouse }).ToDictionary(l => l.ProductID, l => l.OnHand);
            foreach (var p in PilotSampleData.Products.Where(p => firstRun))
                Assert.AreEqual(p.OpeningQty, stock[products[p.PNo].Id], "boshlang'ich qoldiq " + p.PNo);

            var balance = new Dictionary<int, decimal>();   // mustaqil model: mijoz qarzi
            using (var db = new DistributionDbContext())
                foreach (var id in custByInn.Values)
                    balance[id] = LedgerService.CreditState(db, id, start).Balance;
            decimal cashBefore = CashBalance(CashAccountId), bankBefore = CashBalance(BankAccountId), cashExpected = 0, bankExpected = 0;

            // metrikalar
            int noStock = 0, planned = 0, visited = 0, orders = 0, noOrders = 0, overrides = 0, partial = 0, returns = 0, issues = 0, payments = 0, replays = 0;
            decimal ordered = 0, shipped = 0, collected = 0, shortage = 0;
            var shippedLines = new List<(int CustomerId, int OrderId, int ShipmentLineId, int ProductId, decimal Qty, decimal UnitPrice)>();
            var noOrderReason = 0;
            var returnReason = 0;
            using (var db = new DistributionDbContext())
            {
                noOrderReason = db.Reasons.First(r => r.Type == ReasonTypes.NoOrder).ID;
                returnReason = db.Reasons.First(r => r.Type == ReasonTypes.Return).ID;
            }
            var sellable = PilotSampleData.Products;

            var workDays = Enumerable.Range(0, 7).Select(i => start.AddDays(i)).Where(d => d.DayOfWeek != DayOfWeek.Sunday).Take(5).ToList();
            for (int dayNo = 0; dayNo < workDays.Count; dayNo++)
            {
                var day = workDays[dayNo];
                var dayOrders = new List<int>();
                foreach (var login in PilotSampleData.Agents)
                {
                    int agent = agentIds[login];
                    var actor = new OrderActor { UserId = agent, Source = AuditActor.Bot };
                    List<RouteStop> route;
                    using (var db = new DistributionDbContext { Actor = new AuditActor(agent, AuditActor.Bot) })
                    {
                        FieldService.StartDay(db, agent, 41.30m, 69.24m, null, day.AddMinutes(-30));
                        db.SaveChanges();
                        route = FieldService.Route(db, agent, day).Where(r => r.Planned).ToList();
                    }
                    planned += route.Count;
                    foreach (var stop in route)
                    {
                        var t = day.AddMinutes(rnd.Next(0, 480));
                        int visitId;
                        using (var db = new DistributionDbContext { Actor = new AuditActor(agent, AuditActor.Bot) })
                            visitId = FieldService.CheckIn(db, agent, stop.CustomerID, stop.Lat.Value + 0.0003m, stop.Lng.Value, 15, true, t).VisitId;
                        visited++;

                        double roll = rnd.NextDouble();
                        if (roll < 0.70)
                        {
                            var input = new OrderInput { CustomerID = stop.CustomerID, AgentUserID = agent, WarehouseID = warehouse, ClientRequestId = Guid.NewGuid() };
                            foreach (var p in sellable.OrderBy(_ => rnd.Next()).Take(rnd.Next(1, 4)))
                            {
                                var pid = products[p.PNo].Id;
                                bool big = p.Unit2 != null && rnd.NextDouble() < 0.3;
                                decimal qty = big ? 1 : p.Unit == "dona" ? rnd.Next(2, 21) : rnd.Next(5, 61);
                                input.Lines.Add(new OrderLineInput { ProductID = pid, ProductUnitID = big ? unit2[pid] : (int?)null, Qty = qty });
                            }
                            int orderId;
                            using (var uow = new DistributionUnitOfWork(new AuditActor(agent, AuditActor.Bot)))
                            {
                                orderId = OrderService.SaveDraft(uow, input, actor, t);
                                FieldService.SetResult(uow.Dst, visitId, agent, null, null, orderId, null, null, null, t);
                                uow.Dst.SaveChanges();
                                uow.Commit();
                            }
                            if (rnd.NextDouble() < 0.1)
                            {
                                // bot qayta yubordi вЂ” o'sha buyurtma
                                using (var uow = new DistributionUnitOfWork(new AuditActor(agent, AuditActor.Bot)))
                                    Assert.AreEqual(orderId, OrderService.SaveDraft(uow, input, actor, t));
                                replays++;
                            }
                            dayOrders.Add(orderId);
                            orders++;
                        }
                        else if (roll < 0.85)
                        {
                            using (var db = new DistributionDbContext { Actor = new AuditActor(agent, AuditActor.Bot) })
                            {
                                FieldService.SetResult(db, visitId, agent, VisitResults.NoOrder, noOrderReason, null, null, null, null, t);
                                db.SaveChanges();
                            }
                            noOrders++;
                        }

                        // to'lov: qarzi bo'lsa вЂ” qismi; naqd 60%, karta 25%, Click 15%
                        if (balance[stop.CustomerID] > 0 && rnd.NextDouble() < 0.45)
                        {
                            decimal amount = Math.Max(10000m, Math.Round(balance[stop.CustomerID] * (decimal)(0.2 + rnd.NextDouble() * 0.6) / 1000) * 1000);
                            double m = rnd.NextDouble();
                            string method = m < 0.6 ? PaymentMethods.Cash : m < 0.85 ? PaymentMethods.Card : PaymentMethods.Click;
                            using (var uow = new DistributionUnitOfWork(new AuditActor(agent, AuditActor.Bot)))
                            {
                                int? account = AgentPayments.ResolveAccount(uow.Dst, method);   // DstApi/Payment bilan bir xil
                                var id = PaymentService.Receive(uow, new PaymentInput
                                {
                                    CustomerID = stop.CustomerID, Amount = amount, Method = method, FinAccountID = account, ReceivedByUserID = agent, ClientRequestId = Guid.NewGuid()
                                }, agent, AuditActor.Bot, t);
                                FieldService.SetResult(uow.Dst, visitId, agent, null, null, null, id, null, null, t);
                                uow.Dst.SaveChanges();
                                uow.Commit();
                            }
                            balance[stop.CustomerID] -= amount;
                            collected += amount;
                            payments++;
                            if (method != PaymentMethods.Cash) bankExpected += amount;
                        }
                    }
                }

                // ---------- ofis: tasdiq (limit в†’ rahbar ruxsati), ombor: jo'natish, yetkazildi ----------
                var officeActor = new OrderActor { UserId = office, MaxDiscountPct = 5 };
                foreach (var orderId in dayOrders)
                {
                    bool blocked = false;
                    using (var uow = asOffice())
                    {
                        try
                        {
                            OrderService.Confirm(uow, orderId, officeActor, null, day.Date.AddHours(9));
                            uow.Commit();
                        }
                        catch (CreditBlockedException)
                        {
                            blocked = true;   // rad etilgan tranzaksiya commit qilinmaydi
                        }
                    }
                    if (blocked)
                    {
                        using (var uow = asOffice())
                        {
                            OrderService.Confirm(uow, orderId, new OrderActor { UserId = office, CanOverrideCredit = true }, "Rahbar ruxsati (pilot)", day.Date.AddHours(9));
                            uow.Commit();
                        }
                        overrides++;
                    }
                    using (var uow = asOffice())
                    {
                        var order = uow.Dst.Orders.Find(orderId);
                        var reserved = (from r in uow.Dst.StockReservations
                                        join l in uow.Dst.OrderLines on r.OrderLineID equals l.ID
                                        where l.OrderID == orderId
                                        select new { l.ID, r.QtyBase }).ToList()
                            .GroupBy(x => x.ID).ToDictionary(g => g.Key, g => g.Sum(x => x.QtyBase));
                        if (reserved.Values.Sum() == 0)
                        {
                            // qoldiq yetmadi (takroriy mashqda) — operator mijoz bilan kelishib bekor qiladi
                            OrderService.Cancel(uow, orderId, office, "Qoldiq yetmadi, mijozga aytildi", day.Date.AddHours(10));
                            uow.Commit();
                            noStock++;
                            continue;
                        }
                        ordered += order.Total;
                        OrderService.StartPicking(uow, orderId, office, day.Date.AddHours(10));
                        IDictionary<int, decimal> qtys = null;
                        bool isPartial = rnd.NextDouble() < 0.1;
                        if (isPartial)
                            qtys = reserved.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => Math.Max(1, Math.Floor(kv.Value / 2)));
                        var shipmentId = OrderService.Ship(uow, orderId, qtys, office, day.Date.AddHours(11));
                        if ((OrderStatus)uow.Dst.Orders.Find(orderId).OrderStatus == OrderStatus.PartiallyShipped)
                        {
                            OrderService.CloseRemainder(uow, orderId, office, "Mijoz qolganidan voz kechdi", day.Date.AddHours(11));
                            partial++;
                        }
                        OrderService.MarkDelivered(uow, orderId, office, day.Date.AddHours(15));
                        foreach (var sl in uow.Dst.ShipmentLines.Where(s => s.ShipmentID == shipmentId).ToList())
                        {
                            stock[sl.ProductID] -= sl.QtyBase;
                            balance[order.CustomerID] += sl.Amount;
                            shipped += sl.Amount;
                            shippedLines.Add((order.CustomerID, orderId, sl.ID, sl.ProductID, sl.QtyBase, sl.Amount / sl.QtyBase));
                        }
                        uow.Commit();
                    }
                }

                // ---------- qaytarish (avvalgi kunlar jo'natishidan, ~1 ta kuniga) ----------
                var candidates = shippedLines.Where(s => s.Qty >= 2).ToList();
                if (dayNo > 0 && candidates.Count > 0)
                {
                    var r = candidates[rnd.Next(candidates.Count)];
                    using (var uow = asOffice())
                    {
                        ReturnService.Create(uow, new ReturnInput
                        {
                            CustomerID = r.CustomerId, OrderID = r.OrderId, WarehouseID = warehouse, ReasonID = returnReason, IsDefect = false,
                            Lines = { new ReturnLineInput { ShipmentLineID = r.ShipmentLineId, ProductID = r.ProductId, QtyBase = 1 } }
                        }, office, AuditActor.Web, day.Date.AddHours(16));
                        var amount = uow.Dst.ReturnLines.Where(x => x.ShipmentLineID == r.ShipmentLineId).OrderByDescending(x => x.ID).Select(x => x.Amount).First();
                        uow.Commit();
                        stock[r.ProductId] += 1;
                        balance[r.CustomerId] -= amount;
                    }
                    shippedLines.Remove(r);
                    returns++;
                }

                // ---------- kechqurun: kassaga topshirish ----------
                foreach (var login in PilotSampleData.Agents)
                {
                    int agent = agentIds[login];
                    using (var db = new DistributionDbContext())
                        if (!PaymentService.OnHand(db, agent).Any()) continue;
                    int handover;
                    using (var uow = new DistributionUnitOfWork(new AuditActor(agent, AuditActor.Bot)))
                    {
                        handover = PaymentService.SubmitHandover(uow, agent, CashAccountId, agent, null, day.Date.AddHours(18));
                        uow.Commit();
                    }
                    using (var uow = asOffice())
                    {
                        var declared = uow.Dst.CashHandovers.Find(handover).AmountDeclared;
                        decimal received = dayNo == 2 && login == "agent3" ? declared - 20000m : declared;   // bitta kamomad
                        shortage += declared - received;
                        PaymentService.ConfirmHandover(uow, handover, received, office, day.Date.AddHours(18.5));
                        uow.Commit();
                        cashExpected += received;
                    }
                    using (var db = new DistributionDbContext { Actor = new AuditActor(agent, AuditActor.Bot) })
                        FieldService.EndDay(db, agent, 41.30m, 69.24m, day.Date.AddHours(19));
                }

                // ---------- xatolar jurnali: kuniga bitta yozuv (botdan) ----------
                using (var uow = new DistributionUnitOfWork(new AuditActor(agentIds["agent1"], AuditActor.Bot)))
                {
                    IssueService.Create(uow.Dst, new IssueInput { Description = "Mashq: " + (dayNo + 1) + "-kun, katalogda qidiruv sekin", Place = "Telegram" },
                        agentIds[PilotSampleData.Agents[dayNo]], AuditActor.Bot, day.Date.AddHours(17));
                    uow.Commit();
                }
                issues++;

                using (var db = new DistributionDbContext())
                {
                    var f = DailySummaryService.Compute(db, day);
                    report.AppendLine(string.Format(CultureInfo.InvariantCulture, "| {0:dd.MM ddd} | {1} | {2:N0} | {3:N0} | {4:N0} | {5}/{6} |",
                        day, f.Orders, f.OrdersTotal, f.Shipped, f.Collected, f.AgentsActive, f.AgentsTotal));
                }
            }

            // ---------- 3. qabul mezonlari ----------
            int controlId;
            using (var uow = asOffice())
            {
                controlId = InventoryCountService.Create(uow, warehouse, InventoryKinds.Control, "Pilot oxiri", office, workDays.Last().AddDays(1));
                uow.Commit();
            }
            using (var db = new DistributionDbContext { Actor = new AuditActor(office, AuditActor.Web) })
            {
                // omborchi "sanadi": pilot tovarlari вЂ” mustaqil model bo'yicha, boshqalari вЂ” tizimdagidek
                var rows = InventoryCountService.Rows(db, db.InventoryCounts.Find(controlId));
                InventoryCountService.SaveCounts(db, controlId, rows.Select(r => (r.LineID, (decimal?)(stock.TryGetValue(r.ProductID, out var q) ? q : r.System), (decimal?)null, (string)null)));
            }
            using (var uow = asOffice())
            {
                InventoryCountService.PostAsync(uow, controlId, false, office, workDays.Last().AddDays(1)).GetAwaiter().GetResult();
                uow.Commit();
            }
            using (var db = new DistributionDbContext())
            {
                var rows = InventoryCountService.Rows(db, db.InventoryCounts.Find(controlId));
                var diffs = rows.Where(r => r.Diff != 0).Select(r => r.PNo + ": " + r.Diff).ToList();
                Assert.AreEqual(0, diffs.Count, "Q2: qoldiq inventarizatsiya bilan mos emas: " + string.Join(", ", diffs));
                Assert.IsFalse(db.InventoryCounts.Find(controlId).Applied, "nazorat sanog'i qoldiqni o'zgartirmaydi");

                foreach (var kv in balance)
                {
                    Assert.AreEqual(kv.Value, LedgerService.CreditState(db, kv.Key, workDays.Last().AddDays(1)).Balance, "Q4: balans " + kv.Key);
                    Assert.AreEqual(kv.Value, DebtService.Aging(db, kv.Key, workDays.Last().AddDays(1)).Total, "Q4: aging " + kv.Key);
                    Assert.AreEqual(kv.Value, DebtService.BuildStatement(db, kv.Key, start.AddMonths(-2), workDays.Last().AddDays(2)).Closing, "Q4: akt sverki " + kv.Key);
                }
                foreach (var id in agentIds.Values)
                    Assert.AreEqual(0, PaymentService.OnHand(db, id).Count, "Q3: agent qo'lida pul qolmadi");
                Assert.IsTrue(db.Issues.Count() >= issues, "xatolar jurnali");
            }
            Assert.AreEqual(cashExpected, CashBalance(CashAccountId) - cashBefore, "Q3: kassa = sanab qabul qilingan naqd");
            Assert.AreEqual(bankExpected, CashBalance(BankAccountId) - bankBefore, "karta/Click вЂ” bank hisobiga");

            var summary = new StringBuilder();
            summary.AppendLine("| Kun | Buyurtma | Summa | Jo'natildi | Yig'ildi | Faol agent |");
            summary.AppendLine("|---|---|---|---|---|---|");
            summary.Append(report);
            summary.AppendLine();
            summary.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "Rejadagi tashrif {0}, bajarildi {1}; buyurtma {2} ({3:N0}), buyurtmasiz {4}; bot qayta yuborishi {5} (ikki buyurtma bo'lmadi); " +
                "limit bo'yicha rahbar ruxsati {6}; qisman jo'natish {7} (qoldiq yetmay bekor {13}); qaytarish {8}; to'lov {9} ({10:N0}), kamomad {11:N0}; xato yozuvlari {12}. " +
                "Nazorat sanog'i: farq 0; mijoz qarzlari = model = akt sverki; kassa va bank mos.",
                planned, visited, orders, ordered, noOrders, replays, overrides, partial, returns, payments, collected, shortage, issues, noStock));
            var reportPath = Environment.GetEnvironmentVariable("DST_PILOT_REPORT");
            if (!string.IsNullOrEmpty(reportPath)) File.WriteAllText(reportPath, summary.ToString(), Encoding.UTF8);
            Console.WriteLine(summary);
        }
    }
}
