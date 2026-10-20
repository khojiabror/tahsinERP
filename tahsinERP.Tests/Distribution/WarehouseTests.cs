using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Areas.Distribution.Services.Warehouse;
using tahsinERP.Configuration;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.Services.Stock;

namespace tahsinERP.Tests.Distribution
{
    /// <summary>3-bosqich: aksiya qoidalari va landed cost taqsimoti (bazasiz).</summary>
    [TestClass]
    public class PromotionAndAllocationTests
    {
        private static Promotion Discount(int id, decimal pct, decimal minQty = 0, int? product = null, int? category = null) =>
            new Promotion { ID = id, Kind = PromotionKinds.Discount, DiscountPct = pct, MinQtyBase = minQty, ProductID = product, CategoryID = category, IsActive = true };

        [TestMethod]
        public void Discount_BestMatchingTierWins()
        {
            var promos = new[] { Discount(1, 3, 50, product: 10), Discount(2, 5, 100, product: 10), Discount(3, 2, category: 7) };
            var r = PromotionService.Apply(promos, new[]
            {
                new PromotionService.LineInfo { Index = 0, ProductID = 10, CategoryID = 7, QtyBase = 120 },
                new PromotionService.LineInfo { Index = 1, ProductID = 10, CategoryID = 7, QtyBase = 60 },
                new PromotionService.LineInfo { Index = 2, ProductID = 11, CategoryID = 7, QtyBase = 1 },
                new PromotionService.LineInfo { Index = 3, ProductID = 12, CategoryID = 8, QtyBase = 500 }
            });
            Assert.AreEqual(5m, r.Discounts[0].Pct);
            Assert.AreEqual(3m, r.Discounts[1].Pct);
            Assert.AreEqual(2m, r.Discounts[2].Pct, "kategoriya bo'yicha");
            Assert.IsFalse(r.Discounts.ContainsKey(3));
        }

        [TestMethod]
        public void Free_TenPlusOne_RepeatsAndSumsLines()
        {
            var p = new Promotion { ID = 5, Kind = PromotionKinds.Free, ProductID = 10, BuyQtyBase = 10, FreeQtyBase = 1, Repeatable = true, IsActive = true };
            var r = PromotionService.Apply(new[] { p }, new[]
            {
                new PromotionService.LineInfo { Index = 0, ProductID = 10, QtyBase = 15 },
                new PromotionService.LineInfo { Index = 1, ProductID = 10, QtyBase = 12 }
            });
            Assert.AreEqual(1, r.Free.Count);
            Assert.AreEqual(2m, r.Free[0].QtyBase, "27 dona → 2 sovg'a");
            Assert.AreEqual(10, r.Free[0].ProductID);

            p.Repeatable = false;
            p.FreeProductID = 99;
            r = PromotionService.Apply(new[] { p }, new[] { new PromotionService.LineInfo { Index = 0, ProductID = 10, QtyBase = 40 } });
            Assert.AreEqual(1m, r.Free[0].QtyBase);
            Assert.AreEqual(99, r.Free[0].ProductID);
            Assert.AreEqual(0, PromotionService.Apply(new[] { p }, new[] { new PromotionService.LineInfo { ProductID = 10, QtyBase = 9 } }).Free.Count);
        }

        [TestMethod]
        public void ManualPct_SplitsPromoFromTotal()
        {
            decimal total = Math.Round(OrderCalculator.EffectiveDiscountPct(5, 2), 2);    // 6.9
            Assert.AreEqual(6.9m, total);
            Assert.AreEqual(2m, OrderCalculator.ManualPct(total, 5));
            Assert.AreEqual(4m, OrderCalculator.ManualPct(4, 0));
            Assert.AreEqual(0m, OrderCalculator.ManualPct(100, 100));
        }

        [TestMethod]
        public void Distribute_ByValueQuantityWeight_SumsExactly()
        {
            List<LandedCostService.Allocation> Lines() => new List<LandedCostService.Allocation>
            {
                new LandedCostService.Allocation { IncomeProductID = 1, Qty = 100, BaseValue = 4000000, WeightKg = 5000 },
                new LandedCostService.Allocation { IncomeProductID = 2, Qty = 30, BaseValue = 1000000, WeightKg = 300 },
                new LandedCostService.Allocation { IncomeProductID = 3, Qty = 1, BaseValue = 0, WeightKg = 1 }
            };
            var v = Lines();
            LandedCostService.Distribute(v, 100000, LandedCostBasis.Value);
            Assert.AreEqual(100000m, v.Sum(x => x.Allocated));
            Assert.AreEqual(80000m, v[0].Allocated);
            Assert.AreEqual(800m, v[0].UnitAdd);

            var q = Lines();
            LandedCostService.Distribute(q, 131, LandedCostBasis.Quantity);
            Assert.AreEqual(131m, q.Sum(x => x.Allocated));
            Assert.AreEqual(1m, q[2].Allocated);

            var w = Lines();
            LandedCostService.Distribute(w, 53010, LandedCostBasis.Weight);
            Assert.AreEqual(53010m, w.Sum(x => x.Allocated));
            Assert.AreEqual(50000m, w[0].Allocated);
        }
    }

    /// <summary>
    /// 3-bosqich ombor hujjatlari lokal bazada, bitta tranzaksiyada va rollback bilan: hisobdan chiqarish tasdig'i, pereso'rtitsa,
    /// landed cost, ko'chirma dalolatnomasi, aksiya buyurtmada, minimal qoldiq.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class WarehouseIntegrationTests
    {
        private static string _sql;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            try
            {
                _sql = DbConfig.GetSqlConnectionString();
                using (var c = new SqlConnection(_sql))
                using (var cmd = new SqlCommand("SELECT OBJECT_ID('dst.WRITE_OFFS', 'U')", c))
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
            if (_sql == null) Assert.Inconclusive("Lokal baza yoki migratsiya 025 yo'q.");
        }

        private static DistributionUnitOfWork Uow() => new DistributionUnitOfWork(new AuditActor(null, AuditActor.System));

        /// <summary>O'sha omborga yana bir tovar (qty dona, narxi price).</summary>
        private static int AddProduct(DistributionUnitOfWork uow, OrderFlowIntegrationTests.Fixture f, decimal qty, decimal price)
        {
            var erp = uow.Erp;
            var unitId = erp.PRODUCTS.Where(p => p.ID == f.ProductId).Select(p => p.UnitID).First();
            var shop = erp.PRODUCTS.Where(p => p.ID == f.ProductId).Select(p => p.ShopID).First();
            var product = new PRODUCT
            {
                PNo = "T" + Guid.NewGuid().ToString("N").Substring(0, 12), Name = "Test 2", UnitID = unitId, ShopID = shop,
                IsDeleted = false, IsFinishedGood = true, IsComponent = false, IsRawMaterial = false
            };
            erp.PRODUCTS.Add(product);
            erp.SaveChanges();
            if (qty > 0)
                StockDocs.Receive(uow, f.WarehouseId, "TST-" + product.PNo, "test", new List<StockDocs.ReceiptLine>
                {
                    new StockDocs.ReceiptLine { ProductID = product.ID, QtyBase = qty, UnitCost = price }
                }, f.LocationId, DateTime.Now.AddMinutes(-9));
            return product.ID;
        }

        private static decimal OnHand(DistributionDbContext db, int productId, int warehouseId) =>
            StockService.Levels(db, new[] { productId }, new[] { warehouseId }).Select(l => l.OnHand).DefaultIfEmpty(0).Sum();

        [TestMethod]
        public void WriteOff_SubmitApprove_IssuesAtFifoCost_AndCancelRestores()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                db.ProductStockLimits.Add(new ProductStockLimit { WarehouseID = f.WarehouseId, ProductID = f.ProductId, MinQty = 90 });
                db.SaveChanges();

                var id = WriteOffService.SaveDraft(uow, new WriteOffInput
                {
                    WarehouseID = f.WarehouseId, Note = "sinish",
                    Lines = { new WriteOffLineInput { ProductID = f.ProductId, QtyBase = 15 } }
                }, f.UserId, now);
                var doc = db.WriteOffs.Find(id);
                Assert.IsTrue(doc.DocNo.StartsWith("DW-"));

                Assert.ThrowsException<DstValidationException>(() => WriteOffService.Approve(uow, id, f.UserId, true, true, now), "yuborilmagan");
                WriteOffService.Submit(db, id, f.UserId, now);
                Assert.IsTrue(db.WriteOffs.Find(id).IsSubmitted);
                Assert.ThrowsException<DstValidationException>(() => WriteOffService.Approve(uow, id, f.UserId, false, true, now), "huquq yo'q");
                Assert.ThrowsException<DstValidationException>(() => WriteOffService.Approve(uow, id, f.UserId, true, false, now), "o'zini tasdiqlash");
                Assert.AreEqual(100m, OnHand(db, f.ProductId, f.WarehouseId), "rad etilgan urinishlar qoldiqqa tegmadi");

                WriteOffService.Approve(uow, id, f.UserId, true, true, now);
                doc = db.WriteOffs.Find(id);
                Assert.AreEqual(DocumentStatus.Posted, doc.Status);
                Assert.AreEqual(600000m, doc.CostTotal, "15 × 40 000");
                Assert.IsNotNull(doc.ExpenseID);
                Assert.AreEqual(85m, OnHand(db, f.ProductId, f.WarehouseId));

                WriteOffService.CancelPosted(uow, id, f.UserId, true, "xato kiritildi", now);
                Assert.AreEqual(DocumentStatus.Cancelled, db.WriteOffs.Find(id).Status);
                Assert.AreEqual(100m, OnHand(db, f.ProductId, f.WarehouseId));
            }
        }

        [TestMethod]
        public void WriteOff_ReservedStockIsProtected()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var now = DateTime.Now;
                var orderId = OrderService.SaveDraft(uow, new OrderInput
                {
                    CustomerID = f.CustomerId, WarehouseID = f.WarehouseId, Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = 95 } }
                }, OrderFlowIntegrationTests.Actor(f), now);
                OrderService.Confirm(uow, orderId, OrderFlowIntegrationTests.Actor(f), null, now);

                var id = WriteOffService.SaveDraft(uow, new WriteOffInput
                {
                    WarehouseID = f.WarehouseId, Lines = { new WriteOffLineInput { ProductID = f.ProductId, QtyBase = 10 } }
                }, f.UserId, now);
                WriteOffService.Submit(uow.Dst, id, f.UserId, now);
                Assert.ThrowsException<DstValidationException>(() => WriteOffService.Approve(uow, id, f.UserId, true, true, now), "95 rezervda, mavjud 5");
                WriteOffService.Reject(uow.Dst, id, f.UserId, true, "yetmaydi", now);
                Assert.AreEqual(DocumentStatus.Cancelled, uow.Dst.WriteOffs.Find(id).Status);
            }
        }

        [TestMethod]
        public void Regrade_MovesCostToOtherProduct_AndCancel()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                var other = AddProduct(uow, f, 0, 0);

                var id = RegradeService.SaveDraft(uow, new RegradeInput
                {
                    WarehouseID = f.WarehouseId,
                    Lines = { new RegradeLineInput { FromProductID = f.ProductId, ToProductID = other, FromQtyBase = 10, ToQtyBase = 8 } }
                }, f.UserId, now);
                RegradeService.Post(uow, id, f.LocationId, f.UserId, now);
                var doc = db.Regrades.Find(id);
                Assert.AreEqual(400000m, doc.CostTotal);
                Assert.AreEqual(90m, OnHand(db, f.ProductId, f.WarehouseId));
                Assert.AreEqual(8m, OnHand(db, other, f.WarehouseId));
                var ip = uow.Erp.INCOME_PRODUCTS.Single(x => x.IncomeID == doc.IncomeID);
                Assert.AreEqual(50000m, ip.PiecePrice, "400 000 / 8");

                RegradeService.Cancel(uow, id, f.UserId, "noto'g'ri", now);
                Assert.AreEqual(100m, OnHand(db, f.ProductId, f.WarehouseId));
                Assert.AreEqual(0m, OnHand(db, other, f.WarehouseId));
            }
        }

        [TestMethod]
        public void LandedCost_RaisesFifoCost_ConsumedPartReported_CancelReverts()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var erp = uow.Erp;
                var now = DateTime.Now;
                var incomeId = erp.INCOME_PRODUCTS.Where(x => x.ProductID == f.ProductId).Select(x => x.IncomeID).Single();

                // 20 dona allaqachon sotilgan (FIFO 40 000)
                var orderId = OrderService.SaveDraft(uow, new OrderInput
                {
                    CustomerID = f.CustomerId, WarehouseID = f.WarehouseId, Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = 20 } }
                }, OrderFlowIntegrationTests.Actor(f), now);
                OrderService.Confirm(uow, orderId, OrderFlowIntegrationTests.Actor(f), null, now);
                OrderService.Ship(uow, orderId, null, f.UserId, now);

                var id = LandedCostService.Create(uow, LandedCostBasis.Value, "yo'l", f.UserId, now);
                LandedCostService.AddIncome(db, erp, id, incomeId);
                LandedCostService.AddItem(db, id, LandedCostKinds.Freight, "fura", 300000);
                LandedCostService.AddItem(db, id, LandedCostKinds.Customs, null, 200000);
                LandedCostService.Post(uow, id, f.UserId, now);

                var doc = db.LandedCosts.Find(id);
                Assert.AreEqual(500000m, doc.TotalAmount);
                Assert.AreEqual(100000m, doc.ConsumedAmount, "20 / 100 qismi sotilgan");
                Assert.AreEqual(45000m, erp.INCOME_PRODUCTS.Single(x => x.IncomeID == incomeId).PiecePrice);

                // keyingi jo'natish yangi tannarxda
                var order2 = OrderService.SaveDraft(uow, new OrderInput
                {
                    CustomerID = f.CustomerId, WarehouseID = f.WarehouseId, Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = 10 } }
                }, OrderFlowIntegrationTests.Actor(f), now);
                OrderService.Confirm(uow, order2, OrderFlowIntegrationTests.Actor(f), null, now);
                var s2 = OrderService.Ship(uow, order2, null, f.UserId, now);
                Assert.AreEqual(450000m, db.Shipments.Find(s2).CostTotal);

                Assert.ThrowsException<DstValidationException>(() => LandedCostService.AddItem(db, id, LandedCostKinds.Other, null, 1), "o'tkazilgan");
                LandedCostService.Cancel(uow, id, f.UserId, "xato summa", now);
                Assert.AreEqual(40000m, erp.INCOME_PRODUCTS.Single(x => x.IncomeID == incomeId).PiecePrice);
            }
        }

        [TestMethod]
        public void TransferAct_ShortageLossAndReturn_SurplusReceived()
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
                var p2 = AddProduct(uow, f, 50, 10000);
                var p3 = AddProduct(uow, f, 10, 5000);

                // jo'natuvchi chiqimi (qoldiq darhol kamayadi) + qabul qiluvchida kutilayotgan kirim — ombor modulidagi kabi
                var issue = new List<StockDocs.IssueLine>
                {
                    new StockDocs.IssueLine { ProductID = f.ProductId, QtyBase = 20 },
                    new StockDocs.IssueLine { ProductID = p2, QtyBase = 10 },
                    new StockDocs.IssueLine { ProductID = p3, QtyBase = 4 }
                };
                var expenseId = StockDocs.Issue(uow, f.WarehouseId, "TST-TR-" + p2, "test transfer", issue, now);
                var units = erp.PRODUCTS.Where(p => p.ID == f.ProductId).Select(p => p.UnitID).First();
                var income = new INCOME
                {
                    DocNo = "TST-TRI-" + p2, ReceivingWHID = target.ID, SenderWHID = f.WarehouseId, IssueDateTime = now,
                    CurrencyID = WarehouseAccess.DefaultCurrencyId(erp), IsDeleted = false, RecieveStatus = false,
                    Description = WarehouseFlowService.AutoTransferIncomeMarker + " test | " + DocumentMarkers.For(DocumentMarkers.LinkedExpenseId, expenseId)
                };
                erp.INCOMES.Add(income);
                erp.SaveChanges();
                foreach (var l in issue)
                    erp.INCOME_PRODUCTS.Add(new INCOME_PRODUCTS { IncomeID = income.ID, ProductID = l.ProductID, Amount = l.QtyBase, UnitID = units, Comment = "" });
                erp.SaveChanges();

                Assert.IsTrue(TransferActService.Pending(erp, new[] { target.ID }).Any(p => p.IncomeID == income.ID));
                var lines = TransferActService.Lines(db, erp, income.ID);
                Assert.AreEqual(40000m, lines.Single(l => l.ProductID == f.ProductId).UnitCost);
                int ip(int pid) => lines.Single(l => l.ProductID == pid).IncomeProductID;

                Assert.ThrowsException<DstValidationException>(() => TransferActService.Receive(uow, income.ID, new[]
                {
                    new TransferReceiveLine { IncomeProductID = ip(f.ProductId), ReceivedQty = 18 },
                    new TransferReceiveLine { IncomeProductID = ip(p2), ReceivedQty = 10 },
                    new TransferReceiveLine { IncomeProductID = ip(p3), ReceivedQty = 4 }
                }, targetLocation, f.LocationId, null, f.UserId, true, now), "kamomadda yechim majburiy");

                var actId = TransferActService.Receive(uow, income.ID, new[]
                {
                    new TransferReceiveLine { IncomeProductID = ip(f.ProductId), ReceivedQty = 18, Resolution = TransferResolutions.Loss, Note = "singan" },
                    new TransferReceiveLine { IncomeProductID = ip(p2), ReceivedQty = 7, Resolution = TransferResolutions.Return },
                    new TransferReceiveLine { IncomeProductID = ip(p3), ReceivedQty = 5 }
                }, targetLocation, f.LocationId, "dalolatnoma", f.UserId, true, now);

                var act = db.TransferActs.Find(actId);
                Assert.AreEqual(DocumentStatus.Posted, act.Status);
                Assert.AreEqual(110000m, act.ShortageCost, "2 × 40 000 + 3 × 10 000");
                Assert.AreEqual(80000m, act.LossCost);
                Assert.AreEqual(5000m, act.SurplusCost);
                Assert.IsNotNull(act.ReturnIncomeID);
                Assert.AreEqual(18m, OnHand(db, f.ProductId, target.ID));
                Assert.AreEqual(7m, OnHand(db, p2, target.ID));
                Assert.AreEqual(5m, OnHand(db, p3, target.ID));
                Assert.AreEqual(80m, OnHand(db, f.ProductId, f.WarehouseId), "yo'qotish qaytmaydi");
                Assert.AreEqual(43m, OnHand(db, p2, f.WarehouseId), "50 − 10 + 3 qaytdi");
                Assert.IsTrue(erp.INCOMES.Find(income.ID).RecieveStatus == true);
                Assert.IsTrue(erp.EXPENSES.Find(expenseId).SendStatus == true);
                Assert.ThrowsException<DstValidationException>(() => TransferActService.Receive(uow, income.ID, new TransferReceiveLine[0], null, null, null, f.UserId, true, now),
                    "ikki marta qabul yo'q");
            }
        }

        [TestMethod]
        public void Promotion_DiscountAndGiftLine_InOrder_ExemptFromRoleLimit()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                var now = DateTime.Now;
                var gift = AddProduct(uow, f, 10, 1000);
                db.Promotions.Add(new Promotion { Name = "hajm", Kind = PromotionKinds.Discount, DateFrom = now.Date, ProductID = f.ProductId, MinQtyBase = 20, DiscountPct = 10, CreatedAt = now });
                db.Promotions.Add(new Promotion { Name = "10+1", Kind = PromotionKinds.Free, DateFrom = now.Date, ProductID = f.ProductId, BuyQtyBase = 10, FreeQtyBase = 1, FreeProductID = gift, Repeatable = true, CreatedAt = now });
                db.Promotions.Add(new Promotion { Name = "eski", Kind = PromotionKinds.Discount, DateFrom = now.Date.AddDays(-30), DateTo = now.Date.AddDays(-1), ProductID = f.ProductId, DiscountPct = 50, CreatedAt = now });
                db.SaveChanges();

                // aksiya 10% — rol limiti 5% va min narx (55 000 × 0.9 = 49 500 < 50 000) dan ozod
                var orderId = OrderService.SaveDraft(uow, new OrderInput
                {
                    CustomerID = f.CustomerId, WarehouseID = f.WarehouseId,
                    Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = 25, DiscountPct = 2 } }
                }, OrderFlowIntegrationTests.Actor(f), now);
                var lines = db.OrderLines.Where(l => l.OrderID == orderId).OrderBy(l => l.LineNo).ToList();
                Assert.AreEqual(2, lines.Count);
                Assert.AreEqual(10m, lines[0].PromoPct);
                Assert.AreEqual(11.8m, lines[0].DiscountPct, "10% + 2% ketma-ket");
                Assert.AreEqual(1212750m, lines[0].Amount, "25 × 55 000 × 0.882");
                Assert.IsTrue(lines[1].IsFree);
                Assert.AreEqual(gift, lines[1].ProductID);
                Assert.AreEqual(2m, lines[1].QtyBase);
                Assert.AreEqual(0m, lines[1].Amount);
                Assert.AreEqual(1212750m, db.Orders.Find(orderId).Total);

                // qayta saqlash: sovg'a qayta hisoblanadi (ikki marta qo'shilmaydi), qo'lda chegirma limiti tekshiriladi
                OrderService.SaveDraft(uow, new OrderInput
                {
                    ID = orderId, CustomerID = f.CustomerId, WarehouseID = f.WarehouseId,
                    Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = 10 } }
                }, OrderFlowIntegrationTests.Actor(f), now);
                lines = db.OrderLines.Where(l => l.OrderID == orderId).OrderBy(l => l.LineNo).ToList();
                Assert.AreEqual(2, lines.Count);
                Assert.AreEqual(0m, lines[0].PromoPct, "20 dan kam");
                Assert.AreEqual(1m, lines[1].QtyBase);
                Assert.ThrowsException<DstValidationException>(() => OrderService.SaveDraft(uow, new OrderInput
                {
                    ID = orderId, CustomerID = f.CustomerId, Lines = { new OrderLineInput { ProductID = f.ProductId, Qty = 30, DiscountPct = 6 } }
                }, OrderFlowIntegrationTests.Actor(f), now));

                // sovg'a qatori ham rezervga olinadi va narxsiz jo'natiladi
                OrderService.Confirm(uow, orderId, OrderFlowIntegrationTests.Actor(f), null, now);
                var shipmentId = OrderService.Ship(uow, orderId, null, f.UserId, now);
                Assert.AreEqual(550000m, db.Shipments.Find(shipmentId).Total);
                Assert.AreEqual(9m, OnHand(db, gift, f.WarehouseId));
            }
        }

        [TestMethod]
        public void Reorder_BelowMin_SuggestsUpToTarget()
        {
            using (var uow = Uow())
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                db.ProductStockLimits.Add(new ProductStockLimit { WarehouseID = f.WarehouseId, ProductID = f.ProductId, MinQty = 120, MaxQty = 300 });
                db.SaveChanges();
                var rows = ReorderService.BelowMin(db, uow.Erp, f.WarehouseId, null, DateTime.Now);
                var row = rows.Single(r => r.ProductID == f.ProductId);
                Assert.AreEqual(100m, row.Available);
                Assert.AreEqual(200m, row.Suggested);

                db.ProductStockLimits.Find(f.WarehouseId, f.ProductId).MinQty = 50;
                db.SaveChanges();
                Assert.IsFalse(ReorderService.BelowMin(db, uow.Erp, f.WarehouseId, null, DateTime.Now).Any(r => r.ProductID == f.ProductId));
            }
        }
    }
}
