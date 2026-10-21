using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Orders;

namespace tahsinERP.Tests.Distribution
{
    [TestClass]
    public class PricingAndRulesTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 19, 12, 0, 0);
        private const int Retail = 1, Wholesale = 2;

        private static PriceListItem Item(int id, int type, decimal price, DateTime from, decimal? min = null) =>
            new PriceListItem { ID = id, PriceTypeID = type, ProductID = 5, Price = price, MinPrice = min, ValidFrom = from };

        [TestMethod]
        public void Resolve_CustomerTypeLatestBeforeNow()
        {
            var list = new[]
            {
                Item(1, Wholesale, 100, Now.AddDays(-10), 90),
                Item(2, Wholesale, 110, Now.AddDays(-1), 95),
                Item(3, Wholesale, 999, Now.AddDays(1)),          // kelajakdagi narx hali amal qilmaydi
                Item(4, Retail, 130, Now.AddDays(-1))
            };
            var r = PriceResolver.Resolve(null, list, Wholesale, Retail, Now);
            Assert.AreEqual(110m, r.Price);
            Assert.AreEqual(95m, r.MinPrice);
            Assert.AreEqual(PriceSource.CustomerPriceType, r.Source);
        }

        [TestMethod]
        public void Resolve_FallsBackToDefaultType()
        {
            var list = new[] { Item(4, Retail, 130, Now.AddDays(-1)) };
            var r = PriceResolver.Resolve(null, list, Wholesale, Retail, Now);
            Assert.AreEqual(130m, r.Price);
            Assert.AreEqual(PriceSource.DefaultPriceType, r.Source);

            var none = PriceResolver.Resolve(null, new PriceListItem[0], Wholesale, Retail, Now);
            Assert.IsFalse(none.Found);
        }

        [TestMethod]
        public void Resolve_SpecialPriceWinsWithinDates_AndKeepsMinPrice()
        {
            var list = new[] { Item(1, Wholesale, 100, Now.AddDays(-10), 90) };
            var special = new List<CustomerPrice>
            {
                new CustomerPrice { ID = 1, Price = 80, ValidFrom = Now.Date.AddDays(-5), ValidTo = Now.Date.AddDays(-1) }, // tugagan
                new CustomerPrice { ID = 2, Price = 85, ValidFrom = Now.Date.AddDays(-3), ValidTo = null },
                new CustomerPrice { ID = 3, Price = 70, ValidFrom = Now.Date.AddDays(2) }                              // hali boshlanmagan
            };
            var r = PriceResolver.Resolve(special, list, Wholesale, Retail, Now);
            Assert.AreEqual(85m, r.Price);
            Assert.AreEqual(90m, r.MinPrice, "maxsus narx ham minimumdan ozod emas (K1-6)");
            Assert.AreEqual(PriceSource.CustomerSpecial, r.Source);
        }

        [TestMethod]
        public void Resolve_MinPrice_FromDefaultTypeWhenOwnHasNone_CappedByOwnPrice()
        {
            // minimum faqat Chakana'da (standart): Ulgurji mijozda ham ishlaydi, lekin Ulgurji narxidan oshmaydi
            var list = new[] { Item(1, Wholesale, 100, Now.AddDays(-2)), Item(2, Retail, 130, Now.AddDays(-2), 95) };
            var r = PriceResolver.Resolve(null, list, Wholesale, Retail, Now);
            Assert.AreEqual(100m, r.Price);
            Assert.AreEqual(95m, r.MinPrice);

            var low = new[] { Item(1, Wholesale, 90, Now.AddDays(-2)), Item(2, Retail, 130, Now.AddDays(-2), 95) };
            Assert.AreEqual(90m, PriceResolver.Resolve(null, low, Wholesale, Retail, Now).MinPrice, "chegirmasiz sotuv to'xtamaydi");

            var special = new List<CustomerPrice> { new CustomerPrice { ID = 1, Price = 80, ValidFrom = Now.Date.AddDays(-1) } };
            var s = PriceResolver.Resolve(special, list, Wholesale, Retail, Now);
            Assert.AreEqual(95m, s.MinPrice);
            Assert.ThrowsException<DstValidationException>(() => OrderRules.CheckMinPrice("X", s.Price.Value, s.MinPrice, false));
            OrderRules.CheckMinPrice("X", s.Price.Value, s.MinPrice, true);   // DstDiscountUnlimited
        }

        [TestMethod]
        public void ApplyPercent_RoundsAndClamps()
        {
            Assert.AreEqual(105000m, PriceResolver.ApplyPercent(100000m, 5m, 1000m));
            Assert.AreEqual(12000m, PriceResolver.ApplyPercent(12345m, -3m, 1000m));   // 11974.65 → 12000
            Assert.AreEqual(11974.65m, PriceResolver.ApplyPercent(12345m, -3m, 0m));
            Assert.ThrowsException<DstValidationException>(() => PriceResolver.ApplyPercent(100m, -100m, 0m));
        }

        [TestMethod]
        public void UnitMath_TileExample()
        {
            // kafel: 1 quti = 1.44 m², 1 poddon = 48 quti = 69.12 m²
            Assert.AreEqual(14.4m, UnitMath.ToBase(10, 1.44m));
            Assert.AreEqual(69.12m, UnitMath.ToBase(1, 48 * 1.44m));
            Assert.AreEqual(10m, UnitMath.FromBase(14.4m, 1.44m));
            Assert.AreEqual(122400m, UnitMath.UnitPrice(85000m, 1.44m));   // 1 m² 85 000 → quti 122 400
            Assert.AreEqual(36m, UnitMath.Weight(null, 25m, 1.44m));
            Assert.AreEqual(30m, UnitMath.Weight(30m, 25m, 1.44m));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => UnitMath.ToBase(1, 0));
        }

        [TestMethod]
        public void UnitValidate_Rules()
        {
            UnitService.Validate(1, 2, 1.44m, new[] { 3 });
            Assert.ThrowsException<DstValidationException>(() => UnitService.Validate(1, 1, 2m, null));
            Assert.ThrowsException<DstValidationException>(() => UnitService.Validate(1, 2, 0m, null));
            Assert.ThrowsException<DstValidationException>(() => UnitService.Validate(1, 2, 5m, new[] { 2 }));
        }

        [TestMethod]
        public void Inn_And_Phone()
        {
            Assert.AreEqual("123456789", DirectoryRules.NormalizeInn(" 123 456 789 "));
            Assert.AreEqual("12345678901234", DirectoryRules.NormalizeInn("12345678901234"));
            Assert.IsNull(DirectoryRules.NormalizeInn("  "));
            Assert.ThrowsException<DstValidationException>(() => DirectoryRules.NormalizeInn("12345"));
            Assert.ThrowsException<DstValidationException>(() => DirectoryRules.NormalizeInn("12345678A"));

            Assert.AreEqual("+998901234567", DirectoryRules.NormalizePhone("90 123-45-67"));
            Assert.AreEqual("+998901234567", DirectoryRules.NormalizePhone("+998 (90) 123 45 67"));
            Assert.IsNull(DirectoryRules.NormalizePhone(null));
            Assert.ThrowsException<DstValidationException>(() => DirectoryRules.NormalizePhone("12345"));
        }

        [TestMethod]
        public void Category_Geo_Credit()
        {
            Assert.AreEqual("B", DirectoryRules.NormalizeCategory(" b "));
            Assert.IsNull(DirectoryRules.NormalizeCategory(""));
            Assert.ThrowsException<DstValidationException>(() => DirectoryRules.NormalizeCategory("D"));

            DirectoryRules.ValidateGeo(41.311081m, 69.240562m);
            DirectoryRules.ValidateGeo(null, null);
            Assert.ThrowsException<DstValidationException>(() => DirectoryRules.ValidateGeo(41m, null));
            Assert.ThrowsException<DstValidationException>(() => DirectoryRules.ValidateGeo(91m, 69m));

            DirectoryRules.ValidateCredit(0, 0);
            Assert.ThrowsException<DstValidationException>(() => DirectoryRules.ValidateCredit(-1, 0));
            Assert.ThrowsException<DstValidationException>(() => DirectoryRules.ValidateCredit(0, 400));
        }

        [TestMethod]
        public void Barcode_GtinChecksum()
        {
            Assert.AreEqual("4006381333931", DirectoryRules.NormalizeBarcode(" 4006381333931 "));   // EAN-13
            Assert.AreEqual("73513537", DirectoryRules.NormalizeBarcode("73513537"));               // EAN-8
            Assert.AreEqual("036000291452", DirectoryRules.NormalizeBarcode("036000291452"));       // UPC-A
            Assert.AreEqual("ABC-123", DirectoryRules.NormalizeBarcode("ABC-123"));                 // ichki kod
            Assert.ThrowsException<DstValidationException>(() => DirectoryRules.NormalizeBarcode("4006381333932"));
            Assert.ThrowsException<DstValidationException>(() => DirectoryRules.NormalizeBarcode("12"));
            Assert.ThrowsException<DstValidationException>(() => DirectoryRules.NormalizeBarcode("AB C"));
        }

        [TestMethod]
        public void Territory_LevelAndCycle()
        {
            var region = new Territory { ID = 1, Level = Territory.Region };
            var district = new Territory { ID = 2, ParentID = 1, Level = Territory.District };
            var area = new Territory { ID = 3, ParentID = 2, Level = Territory.Area };
            var all = new Dictionary<int, Territory> { { 1, region }, { 2, district }, { 3, area } };

            Assert.AreEqual(Territory.Region, DirectoryRules.TerritoryLevel(null, id => all[id]));
            Assert.AreEqual(Territory.Area, DirectoryRules.TerritoryLevel(2, id => all[id]));
            Assert.ThrowsException<DstValidationException>(() => DirectoryRules.TerritoryLevel(3, id => all[id]));

            DirectoryRules.EnsureNoCycle(3, 1, id => all[id].ParentID);
            Assert.ThrowsException<DstValidationException>(() => DirectoryRules.EnsureNoCycle(1, 3, id => all[id].ParentID));
            Assert.ThrowsException<DstValidationException>(() => DirectoryRules.EnsureNoCycle(2, 2, id => all[id].ParentID));
        }

        [TestMethod]
        public void VisitDays_Bitmask()
        {
            Assert.AreEqual(1, VisitDays.For(DayOfWeek.Monday));
            Assert.AreEqual(64, VisitDays.For(DayOfWeek.Sunday));
            Assert.AreEqual(1 | 4 | 16, ImportService.ParseVisitDays("1,3,5"));
            Assert.IsTrue(VisitDays.Includes(ImportService.ParseVisitDays("1 3 5"), DayOfWeek.Wednesday));
            Assert.IsFalse(VisitDays.Includes(ImportService.ParseVisitDays("1;3;5"), DayOfWeek.Tuesday));
            Assert.ThrowsException<DstValidationException>(() => ImportService.ParseVisitDays("8"));
        }

        [TestMethod]
        public void DecimalParsing_BothSeparators()
        {
            decimal v;
            Assert.IsTrue(DstModelBinder.TryParseDecimal("41.311081", out v)); Assert.AreEqual(41.311081m, v);
            Assert.IsTrue(DstModelBinder.TryParseDecimal("41,311081", out v)); Assert.AreEqual(41.311081m, v);
            Assert.IsTrue(DstModelBinder.TryParseDecimal("1 234 567,50", out v)); Assert.AreEqual(1234567.50m, v);
            Assert.IsTrue(DstModelBinder.TryParseDecimal("1,234,567.50", out v)); Assert.AreEqual(1234567.50m, v);
            Assert.IsTrue(DstModelBinder.TryParseDecimal("1.234.567,50", out v)); Assert.AreEqual(1234567.50m, v);
            Assert.IsFalse(DstModelBinder.TryParseDecimal("abc", out v));
        }
    }
}
