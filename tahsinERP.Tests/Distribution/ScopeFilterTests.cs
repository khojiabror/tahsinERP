using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Services;

namespace tahsinERP.Tests.Distribution
{
    [TestClass]
    public class ScopeFilterTests
    {
        private sealed class Row
        {
            public int Id { get; set; }
            public int WarehouseId { get; set; }
            public int? AgentId { get; set; }
        }

        private static readonly Row[] Rows =
        {
            new Row { Id = 1, WarehouseId = 10, AgentId = 100 },
            new Row { Id = 2, WarehouseId = 20, AgentId = null },
            new Row { Id = 3, WarehouseId = 30, AgentId = 300 },
        };

        [TestMethod]
        public void NoRows_IsUnrestricted()
        {
            var f = ScopeFilter.FromRows(Enumerable.Empty<int>(), false);
            Assert.IsTrue(f.IsUnrestricted);
            Assert.IsTrue(f.Allows(999));
            Assert.AreEqual(3, f.Apply(Rows.AsQueryable(), r => r.WarehouseId).Count());
        }

        [TestMethod]
        public void Admin_IsUnrestricted_EvenWithRows()
        {
            var f = ScopeFilter.FromRows(new[] { 10 }, true);
            Assert.IsTrue(f.IsUnrestricted);
        }

        [TestMethod]
        public void Rows_RestrictToListed()
        {
            var f = ScopeFilter.FromRows(new[] { 10, 30, 30 }, false);
            Assert.IsFalse(f.IsUnrestricted);
            Assert.AreEqual(2, f.Ids.Count);
            Assert.IsTrue(f.Allows(10));
            Assert.IsFalse(f.Allows(20));
            CollectionAssert.AreEquivalent(new[] { 1, 3 },
                f.Apply(Rows.AsQueryable(), r => r.WarehouseId).Select(r => r.Id).ToArray());
        }

        [TestMethod]
        public void NullableKey_NullRowsHiddenWhenRestricted()
        {
            var f = ScopeFilter.FromRows(new[] { 100, 300 }, false);
            Assert.IsFalse(f.Allows((int?)null));
            CollectionAssert.AreEquivalent(new[] { 1, 3 },
                f.Apply(Rows.AsQueryable(), r => r.AgentId).Select(r => r.Id).ToArray());

            Assert.AreEqual(3, ScopeFilter.Unrestricted.Apply(Rows.AsQueryable(), r => r.AgentId).Count());
        }

        [TestMethod]
        public void Nothing_SeesNothing()
        {
            Assert.IsFalse(ScopeFilter.Nothing.IsUnrestricted);
            Assert.IsFalse(ScopeFilter.Nothing.Allows(10));
            Assert.AreEqual(0, ScopeFilter.Nothing.Apply(Rows.AsQueryable(), r => r.WarehouseId).Count());
        }
    }
}
