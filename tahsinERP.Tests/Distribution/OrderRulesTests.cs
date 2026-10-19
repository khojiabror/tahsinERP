using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Orders;

namespace tahsinERP.Tests.Distribution
{
    [TestClass]
    public class OrderRulesTests
    {
        [TestMethod]
        public void StateMachine_Transitions()
        {
            Assert.IsTrue(OrderStateMachine.CanMove(OrderStatus.New, OrderStatus.Confirmed));
            Assert.IsTrue(OrderStateMachine.CanMove(OrderStatus.Confirmed, OrderStatus.New));
            Assert.IsTrue(OrderStateMachine.CanMove(OrderStatus.PartiallyShipped, OrderStatus.PartiallyShipped));
            Assert.IsTrue(OrderStateMachine.CanMove(OrderStatus.Shipped, OrderStatus.Delivered));
            Assert.IsFalse(OrderStateMachine.CanMove(OrderStatus.New, OrderStatus.Shipped), "tasdiqsiz jo'natib bo'lmaydi");
            Assert.IsFalse(OrderStateMachine.CanMove(OrderStatus.Shipped, OrderStatus.Cancelled), "jo'natilgan — faqat qaytarish");
            Assert.IsFalse(OrderStateMachine.CanMove(OrderStatus.Cancelled, OrderStatus.New));
            Assert.IsFalse(OrderStateMachine.CanMove(OrderStatus.Closed, OrderStatus.Delivered));
            Assert.ThrowsException<DstValidationException>(() => OrderStateMachine.EnsureCanMove(OrderStatus.Delivered, OrderStatus.Picking));

            Assert.IsTrue(OrderStateMachine.IsEditable(OrderStatus.New));
            Assert.IsFalse(OrderStateMachine.IsEditable(OrderStatus.Confirmed));
            Assert.IsTrue(OrderStateMachine.CanShip(OrderStatus.PartiallyShipped));
            Assert.IsFalse(OrderStateMachine.CanShip(OrderStatus.New));
        }

        [TestMethod]
        public void Calculator_LineAndOrderTotals()
        {
            // 10 quti kafel (1 quti = 1.44 m², m² narxi 85 000 → quti 122 400), qator chegirmasi 5%
            var tile = new LineCalc { Qty = 10, Factor = 1.44m, UnitPrice = 122400m, DiscountPct = 5, UnitWeightKg = 30m };
            Assert.AreEqual(14.4m, tile.QtyBase);
            Assert.AreEqual(1224000m, tile.Gross);
            Assert.AreEqual(61200m, tile.DiscountSum);
            Assert.AreEqual(1162800m, tile.Amount);
            Assert.AreEqual(116280m, tile.NetUnitPrice);

            var cement = new LineCalc { Qty = 20, UnitPrice = 55000m, UnitWeightKg = null };
            var totals = OrderCalculator.Totals(new List<LineCalc> { tile, cement }, 2m);
            Assert.AreEqual(2262800m, totals.Subtotal);
            Assert.AreEqual(45256m, totals.OrderDiscountSum);
            Assert.AreEqual(2217544m, totals.Total);
            Assert.AreEqual(300m, totals.WeightKg);
            Assert.IsFalse(totals.WeightComplete, "sementda og'irlik yo'q");

            Assert.AreEqual(6.9m, OrderCalculator.EffectiveDiscountPct(5, 2));   // 1 - 0.95*0.98
        }

        [TestMethod]
        public void Credit_LimitAndOverdue()
        {
            var ok = OrderRules.CheckCredit(new CreditState { CreditLimit = 10000000, Balance = 2000000 }, 3000000);
            Assert.IsTrue(ok.Ok);
            Assert.AreEqual(8000000m, ok.Available);

            var over = OrderRules.CheckCredit(new CreditState { CreditLimit = 10000000, Balance = 8000000 }, 3000000);
            Assert.IsFalse(over.Ok);
            Assert.AreEqual(1, over.Problems.Count);

            var prepaidOnly = OrderRules.CheckCredit(new CreditState { CreditLimit = 0, Balance = 0 }, 1);
            Assert.IsFalse(prepaidOnly.Ok, "limit 0 — faqat oldindan to'lov");

            var overdue = OrderRules.CheckCredit(new CreditState { CreditLimit = 100, Balance = 0, Overdue = 5 }, 1);
            Assert.AreEqual(1, overdue.Problems.Count);

            // to'lovlar eng eski qarzlarni yopadi
            Assert.AreEqual(300m, OrderRules.Overdue(1000m, 700m));
            Assert.AreEqual(0m, OrderRules.Overdue(1000m, 1500m));
        }

        [TestMethod]
        public void Discount_And_MinPrice()
        {
            OrderRules.CheckDiscount(0, null, false);
            OrderRules.CheckDiscount(5, 5, false);
            OrderRules.CheckDiscount(50, null, true);
            Assert.ThrowsException<DstValidationException>(() => OrderRules.CheckDiscount(5.1m, 5, false));
            Assert.ThrowsException<DstValidationException>(() => OrderRules.CheckDiscount(1, null, false), "limit yo'q — chegirma yo'q");

            OrderRules.CheckMinPrice("X", 100, 100, false);
            OrderRules.CheckMinPrice("X", 50, null, false);
            OrderRules.CheckMinPrice("X", 50, 100, true);
            Assert.ThrowsException<DstValidationException>(() => OrderRules.CheckMinPrice("X", 99.99m, 100, false));
        }

        [TestMethod]
        public void SuggestWarehouse_PrefersFullCoverage()
        {
            var levels = new List<StockLevel>
            {
                new StockLevel { WarehouseID = 1, ProductID = 10, OnHand = 100 },                 // faqat 10-tovar
                new StockLevel { WarehouseID = 2, ProductID = 10, OnHand = 5 },
                new StockLevel { WarehouseID = 2, ProductID = 20, OnHand = 50, Reserved = 10 },
                new StockLevel { WarehouseID = 3, ProductID = 10, OnHand = 20 },
                new StockLevel { WarehouseID = 3, ProductID = 20, OnHand = 40 }                   // ikkalasi to'liq
            };
            var need = new Dictionary<int, decimal> { { 10, 10 }, { 20, 30 } };
            Assert.AreEqual(3, StockService.SuggestWarehouse(levels, need, new[] { 1, 2, 3 }));
            Assert.AreEqual(2, StockService.SuggestWarehouse(levels, need, new[] { 1, 2 }), "to'liq yo'q — eng ko'p qoplaydigani");
            Assert.IsNull(StockService.SuggestWarehouse(levels, need, new[] { 4 }));

            var lvl = new StockLevel { OnHand = 5, Reserved = 7 };
            Assert.AreEqual(-2m, lvl.Available);
        }
    }
}
