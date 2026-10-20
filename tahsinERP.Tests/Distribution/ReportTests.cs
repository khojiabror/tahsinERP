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
using tahsinERP.Areas.Distribution.Services.Reports;
using tahsinERP.Configuration;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.Services.Stock;

namespace tahsinERP.Tests.Distribution
{
    [TestClass]
    public class ReportMathTests
    {
        [TestMethod]
        public void WeekdayOccurrences_MondayFirst()
        {
            // 2026-10-19 — dushanba; 14 kun = har kun 2 marta
            var two = VisitReportService.WeekdayOccurrences(new DateTime(2026, 10, 19), new DateTime(2026, 11, 1));
            CollectionAssert.AreEqual(new[] { 2, 2, 2, 2, 2, 2, 2 }, two);
            var tueToThu = VisitReportService.WeekdayOccurrences(new DateTime(2026, 10, 20), new DateTime(2026, 10, 22));
            CollectionAssert.AreEqual(new[] { 0, 1, 1, 1, 0, 0, 0 }, tueToThu);
            Assert.AreEqual(1, VisitDays.For(DayOfWeek.Tuesday) >> 1, "VisitDays biti bilan bir xil tartib");
        }

        [TestMethod]
        public void Planned_SumsCustomersTimesDays()
        {
            var perDay = new[] { 3, 0, 5, 0, 1, 0, 0 };   // dushanba 3, chorshanba 5, juma 1
            var occurrences = VisitReportService.WeekdayOccurrences(new DateTime(2026, 10, 19), new DateTime(2026, 10, 31)); // 2 hafta − yakshanba
            Assert.AreEqual(3 * 2 + 5 * 2 + 1 * 2, VisitReportService.Planned(perDay, occurrences));
        }

        [TestMethod]
        public void SalesRow_NetProfitMargin()
        {
            var r = new SalesRow { Amount = 1000, ReturnAmount = 200, Cost = 600, ReturnCost = 120 };
            Assert.AreEqual(800m, r.Net);
            Assert.AreEqual(480m, r.NetCost);
            Assert.AreEqual(320m, r.Profit);
            Assert.AreEqual(40m, r.MarginPct);
            Assert.IsNull(new SalesRow().MarginPct, "tushum 0 — marja yo'q");
        }

        [TestMethod]
        public void Filter_Normalized_SwapsAndCaps()
        {
            var f = new ReportFilter { From = new DateTime(2026, 10, 20, 15, 0, 0), To = new DateTime(2026, 10, 1) }.Normalized();
            Assert.AreEqual(new DateTime(2026, 10, 1), f.From);
            Assert.AreEqual(new DateTime(2026, 10, 20), f.To);
            var wide = new ReportFilter { From = new DateTime(2000, 1, 1), To = new DateTime(2026, 1, 1) }.Normalized();
            Assert.IsTrue((wide.To - wide.From).TotalDays <= 366 * 3);
        }
    }

    /// <summary>S5 hisobotlari lokal bazada: S2/S3 oqimi bilan hujjatlar yaratiladi, hisobot natijasi tekshiriladi, oxirida rollback.</summary>
    [TestClass]
    [TestCategory("Integration")]
    public class ReportIntegrationTests
    {
        private static string _sql;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            try
            {
                _sql = DbConfig.GetSqlConnectionString();
                using (var c = new SqlConnection(_sql))
                using (var cmd = new SqlCommand("SELECT OBJECT_ID('dst.VISITS', 'U')", c))
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
            if (_sql == null) Assert.Inconclusive("Lokal baza yoki migratsiya 020 yo'q.");
        }

        private static int Ship(DistributionUnitOfWork uow, OrderFlowIntegrationTests.Fixture f, decimal qty, DateTime now)
        {
            var orderId = OrderService.SaveDraft(uow, new OrderInput
            {
                CustomerID = f.CustomerId, WarehouseID = f.WarehouseId,
                Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = qty } }
            }, OrderFlowIntegrationTests.Actor(f), now);
            OrderService.Confirm(uow, orderId, OrderFlowIntegrationTests.Actor(f), null, now);
            return OrderService.Ship(uow, orderId, null, f.UserId, now);
        }

        private static ReportScope OnlyCustomer(int customerId) => new ReportScope
        {
            Customers = ScopeFilter.FromRows(new[] { customerId }, false), Territories = ScopeFilter.Unrestricted,
            Warehouses = ScopeFilter.Unrestricted, Agents = ScopeFilter.Unrestricted
        };

        [TestMethod]
        public void Sales_ShipmentMinusReturn_FifoProfit_ByDimensions()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                var shipmentId = Ship(uow, f, 10, now);                    // 10 × 55 000, tannarx 10 × 40 000
                var line = db.ShipmentLines.Single(l => l.ShipmentID == shipmentId);
                ReturnService.Create(uow, new ReturnInput
                {
                    CustomerID = f.CustomerId, WarehouseID = f.WarehouseId, LocationID = f.LocationId,
                    ReasonID = db.Reasons.Where(r => r.Type == ReasonTypes.Return).Select(r => (int?)r.ID).FirstOrDefault(),
                    Lines = { new ReturnLineInput { ShipmentLineID = line.ID, QtyBase = 2 } }
                }, f.UserId, AuditActor.Web, now);

                var filter = new ReportFilter { From = now.Date, To = now.Date, CustomerId = f.CustomerId };
                var facts = SalesReportService.Facts(db, filter, ReportScope.Unrestricted);
                var t = SalesReportService.Totals(facts);
                Assert.AreEqual(550000m, t.Amount);
                Assert.AreEqual(110000m, t.ReturnAmount);
                Assert.AreEqual(440000m, t.Net);
                Assert.AreEqual(400000m - 80000m, t.Cost - t.ReturnCost, "FIFO tannarx jo'natish va qaytarish hujjatidan");
                Assert.AreEqual(120000m, t.Profit);
                Assert.AreEqual(1, t.Orders);
                Assert.AreEqual(1, t.Customers);

                var byProduct = SalesReportService.ByDimension(db, facts, SalesDimension.Product).ToList();
                Assert.AreEqual(1, byProduct.Count);
                Assert.AreEqual(f.ProductId, byProduct[0].Key);
                Assert.AreEqual(10m, byProduct[0].Qty);
                Assert.AreEqual(2m, byProduct[0].ReturnQty);
                Assert.IsTrue(byProduct[0].Name.Contains("Test sement"));
                Assert.AreEqual(27.3m, byProduct[0].MarginPct);

                var byCustomer = SalesReportService.ByDimension(db, facts, SalesDimension.Customer).Single();
                Assert.AreEqual(f.CustomerId, byCustomer.Key);
                Assert.AreEqual(440000m, byCustomer.Net);
                var byAgent = SalesReportService.ByDimension(db, facts, SalesDimension.Agent).Single();
                Assert.IsNull(byAgent.Key, "agentsiz buyurtma — bitta 'ko'rsatilmagan' qator");
                var byCategory = SalesReportService.ByDimension(db, facts, SalesDimension.Category).Single();
                Assert.AreEqual(440000m, byCategory.Net);

                // saralash va sahifalash SQL'da ishlaydi
                var page = SalesReportService.Order(SalesReportService.ByDimension(db, facts, SalesDimension.Product), "profit", true).Skip(0).Take(5).ToList();
                Assert.AreEqual(1, page.Count);

                // davrdan tashqari va scope
                Assert.AreEqual(0m, SalesReportService.Totals(SalesReportService.Facts(db,
                    new ReportFilter { From = now.Date.AddDays(-3), To = now.Date.AddDays(-1), CustomerId = f.CustomerId }, ReportScope.Unrestricted)).Net);
                var otherCustomer = new ReportScope
                {
                    Customers = ScopeFilter.FromRows(new[] { -1 }, false), Territories = ScopeFilter.Unrestricted,
                    Warehouses = ScopeFilter.Unrestricted, Agents = ScopeFilter.Unrestricted
                };
                Assert.AreEqual(0m, SalesReportService.Totals(SalesReportService.Facts(db, filter, otherCustomer)).Amount, "scope mijozni yashiradi");

                // bekor qilingan jo'natish tushumga kirmaydi
                var returnId = db.Returns.Where(r => r.CustomerID == f.CustomerId).Select(r => r.ID).Single();
                ReturnService.Cancel(uow, returnId, f.UserId, "test", now);
                OrderService.CancelShipment(uow, shipmentId, f.UserId, "test", now);
                Assert.AreEqual(0m, SalesReportService.Totals(SalesReportService.Facts(db, filter, ReportScope.Unrestricted)).Net);
            }
        }

        [TestMethod]
        public void StockValue_RemainingQtyIsNewestLayers()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var erp = uow.Erp;
                var db = uow.Dst;
                var now = DateTime.Now;
                decimal Value() => StockValuationService.Lines(db, new[] { f.WarehouseId }, new[] { f.ProductId }).Single().Value;
                Assert.AreEqual(4000000m, Value(), "100 × 40 000");

                // yangiroq kirim: 50 × 46 000
                var unitId = erp.PRODUCTS.Find(f.ProductId).UnitID;
                var income = new INCOME
                {
                    DocNo = "TST2-" + f.ProductId, ReceivingWHID = f.WarehouseId, IssueDateTime = now.AddMinutes(-5),
                    CurrencyID = WarehouseAccess.DefaultCurrencyId(erp), IsDeleted = false, RecieveStatus = true, Description = "test"
                };
                erp.INCOMES.Add(income);
                erp.SaveChanges();
                var ip = new INCOME_PRODUCTS { IncomeID = income.ID, ProductID = f.ProductId, Amount = 50, PiecePrice = 46000, UnitID = unitId, Comment = "" };
                erp.INCOME_PRODUCTS.Add(ip);
                erp.SaveChanges();
                StockLedger.ReceiveLine(erp, f.WarehouseId, income.ID, ip.ID, f.ProductId, 50, unitId, f.LocationId, income.IssueDateTime);
                erp.SaveChanges();
                Assert.AreEqual(6300000m, Value());

                // 120 jo'natildi: FIFO — 100 × 40 000 + 20 × 46 000; qolgan 30 — eng yangi qatlamdan
                var shipmentId = Ship(uow, f, 120, now);
                Assert.AreEqual(4920000m, db.Shipments.Find(shipmentId).CostTotal);
                Assert.AreEqual(30m * 46000m, Value());

                var byWh = StockValuationService.ByWarehouse(db, new[] { f.WarehouseId }).Single();
                Assert.IsTrue(byWh.Value >= 30m * 46000m);
                Assert.AreEqual(0, StockValuationService.Lines(db, new int[0]).Count);
            }
        }

        [TestMethod]
        public void Visits_PlanFactAndEfficiency()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                var today = now.Date;
                if (db.SalesAgents.Find(f.UserId) == null)
                {
                    db.SalesAgents.Add(new SalesAgent { UserID = f.UserId, IsActive = true });
                    db.SaveChanges();
                }
                var filter = new ReportFilter { From = today, To = today, AgentId = f.UserId };
                var before = VisitReportService.ByAgent(db, filter, ReportScope.Unrestricted).Single();

                // mijoz agentga biriktiriladi, har kuni tashrif
                var profile = db.CustomerProfiles.Find(f.CustomerId);
                profile.AgentUserID = f.UserId;
                profile.VisitDays = 127;
                db.SaveChanges();
                var orderId = OrderService.SaveDraft(uow, new OrderInput
                {
                    CustomerID = f.CustomerId, WarehouseID = f.WarehouseId, Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = 2 } }
                }, OrderFlowIntegrationTests.Actor(f), now);
                db.Visits.Add(new Visit { UserID = f.UserId, CustomerID = f.CustomerId, VisitDate = today, CheckInAt = now, IsPlanned = true, Result = VisitResults.Order, OrderID = orderId });
                db.Visits.Add(new Visit { UserID = f.UserId, CustomerID = f.CustomerId, VisitDate = today, CheckInAt = now, IsPlanned = false, Result = VisitResults.NoOrder });
                db.SaveChanges();

                var after = VisitReportService.ByAgent(db, filter, ReportScope.Unrestricted).Single();
                Assert.AreEqual(before.Planned + 1, after.Planned);
                Assert.AreEqual(before.Visits + 2, after.Visits);
                Assert.AreEqual(before.PlannedVisited + 1, after.PlannedVisited);
                Assert.AreEqual(before.WithOrder + 1, after.WithOrder);
                Assert.AreEqual(before.NoOrder + 1, after.NoOrder);
                Assert.AreEqual(before.OrderTotal + 110000m, after.OrderTotal);
                if (before.Visits == 0)
                {
                    Assert.AreEqual(50m, after.StrikePct);
                    Assert.AreEqual(55000m * 2, after.AvgOrder);
                }

                // ertasi kunga reja 1, fakt 0
                var tomorrow = VisitReportService.ByAgent(db, new ReportFilter { From = today.AddDays(1), To = today.AddDays(1), AgentId = f.UserId }, ReportScope.Unrestricted).Single();
                Assert.IsTrue(tomorrow.Planned >= 1);

                // agent scope: boshqa agentni ko'rmaydi
                var scope = new ReportScope
                {
                    Customers = ScopeFilter.Unrestricted, Territories = ScopeFilter.Unrestricted, Warehouses = ScopeFilter.Unrestricted,
                    Agents = ScopeFilter.FromRows(new[] { -1 }, false)
                };
                Assert.AreEqual(0, VisitReportService.ByAgent(db, filter, scope).Count);
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

        [TestMethod]
        public void Cash_HandoversOnHandAndAccountBalance()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                var account = CashAccount(uow);
                Ship(uow, f, 10, now);
                var filter = new ReportFilter { From = now.Date, To = now.Date, AgentId = f.UserId };
                CashReportService.AgentSummary Summary() =>
                    CashReportService.ByAgent(db, filter, ReportScope.Unrestricted).SingleOrDefault() ?? new CashReportService.AgentSummary();
                var before = Summary();
                if (PaymentService.OnHand(db, f.UserId).Count > 0 || db.CashHandovers.Any(h => h.AgentUserID == f.UserId && h.Status == DocumentStatus.Draft))
                    Assert.Inconclusive("Lokal bazada bu foydalanuvchining topshirilmagan puli bor.");

                PaymentService.Receive(uow, new PaymentInput { CustomerID = f.CustomerId, Amount = 300000, Method = PaymentMethods.Cash }, f.UserId, AuditActor.Bot, now);
                var onHand = Summary();
                Assert.AreEqual(before.OnHand + 300000m, onHand.OnHand);
                Assert.AreEqual(1, onHand.OnHandCount);

                var handoverId = PaymentService.SubmitHandover(uow, f.UserId, account, f.UserId, null, now);
                var pending = Summary();
                Assert.AreEqual(before.Pending + 300000m, pending.Pending);
                Assert.AreEqual(300000m, pending.OnHand, "tasdiqlanguncha pul agentda");

                PaymentService.ConfirmHandover(uow, handoverId, 290000m, f.UserId, now);
                var done = Summary();
                Assert.AreEqual(before.Declared + 300000m, done.Declared);
                Assert.AreEqual(before.Received + 290000m, done.Received);
                Assert.AreEqual(before.Difference - 10000m, done.Difference);
                Assert.AreEqual(0m, done.OnHand);
                Assert.AreEqual(before.Pending, done.Pending);

                var row = CashReportService.Handovers(db, filter, ReportScope.Unrestricted, null).Single(h => h.ID == handoverId);
                Assert.AreEqual(-10000m, row.Difference);
                Assert.AreEqual("Test kassa", row.Account);
                Assert.AreEqual(0, CashReportService.Handovers(db, filter, ReportScope.Unrestricted, DocumentStatus.Draft).Count(h => h.ID == handoverId));

                // kassa qoldig'i: 300 000 kirim − 10 000 kamomad
                var balance = CashReportService.Accounts(uow.Erp, now).Single(a => a.AccountID == account);
                Assert.AreEqual(290000m, balance.Balance);
                Assert.IsTrue(balance.IsCash);
            }
        }

        [TestMethod]
        public void Dashboard_DayFigures_FromSameSources()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                var account = CashAccount(uow);
                Ship(uow, f, 10, now);                                          // qarz 550 000, foyda 150 000
                PaymentService.Receive(uow, new PaymentInput { CustomerID = f.CustomerId, Amount = 200000, Method = PaymentMethods.Click, FinAccountID = account },
                    f.UserId, AuditActor.Web, now);
                OrderService.SaveDraft(uow, new OrderInput
                {
                    CustomerID = f.CustomerId, WarehouseID = f.WarehouseId, Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = 3 } }
                }, OrderFlowIntegrationTests.Actor(f), now);

                var m = DashboardService.Build(uow, now.Date, OnlyCustomer(f.CustomerId), withCost: true, withCash: true);
                Assert.AreEqual(550000m, m.Day.Net);
                Assert.AreEqual(150000m, m.Day.Profit);
                Assert.IsTrue(m.Month.Net >= 550000m);
                Assert.AreEqual(200000m, m.CollectedToday);
                Assert.AreEqual(350000m, m.Debt.Total, "CUSTOMER_LEDGER: 550 000 − 200 000");
                Assert.AreEqual(0m, m.Debt.Overdue);
                Assert.AreEqual(1, m.DebtorCount);
                Assert.AreEqual(2, m.OrdersToday);
                Assert.AreEqual(1, m.AwaitingConfirm);
                Assert.AreEqual(165000m, m.AwaitingConfirmTotal);
                Assert.AreEqual(200000m, m.Accounts.Single(a => a.AccountID == account).Balance);
                Assert.IsNotNull(m.Stock);
                Assert.IsTrue(m.Trend.Any(p => p.Date == now.Date && p.Net == 550000m));

                // muddati o'tgan: 15 kundan keyin (PaymentDays = 10)
                var later = DashboardService.Build(uow, now.Date.AddDays(15), OnlyCustomer(f.CustomerId), withCost: false, withCash: false);
                Assert.AreEqual(350000m, later.Debt.Days1To7);
                Assert.AreEqual(1, later.OverdueCount);
                Assert.AreEqual(f.CustomerId, later.TopOverdue.Single().CustomerID);
                Assert.IsNull(later.Stock, "tannarx huquqisiz qoldiq qiymati hisoblanmaydi");
                Assert.IsNull(later.Accounts);
            }
        }
    }
}
