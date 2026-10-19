using System;
using System.Data.Entity;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Configuration;

namespace tahsinERP.Tests.Distribution
{
    /// <summary>
    /// S1 ma'lumotnomalari lokal bazada: model ↔ sxema mosligi, mijoz saqlash, narx aniqlash, Excel import
    /// (tekshirish rejimi). HECH NARSA saqlanmaydi — har test rollback bilan tugaydi.
    /// Migratsiya 014 qo'llanmagan bo'lsa — Inconclusive.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class DirectoryIntegrationTests
    {
        private static string _sql;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            try
            {
                _sql = DbConfig.GetSqlConnectionString();
                using (var c = new SqlConnection(_sql))
                using (var cmd = new SqlCommand("SELECT OBJECT_ID('dst.PRODUCT_UNITS', 'U')", c))
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
            if (_sql == null) Assert.Inconclusive("Lokal baza yoki migratsiya 014 yo'q.");
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

        private static DistributionUnitOfWork Uow() => new DistributionUnitOfWork(new AuditActor(null, AuditActor.System));

        [TestMethod]
        public void EveryDbSet_MatchesSchema()
        {
            using (var uow = Uow())
            {
                var db = uow.Dst;
                var sets = typeof(DistributionDbContext).GetProperties()
                    .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
                    .ToList();
                Assert.IsTrue(sets.Count >= 27, "DbSet soni: " + sets.Count);
                foreach (var p in sets)
                {
                    var set = (System.Collections.IEnumerable)p.GetValue(db);
                    try
                    {
                        // birinchi qatorni o'qish — ustun nomi/turi mos kelmasa EF istisno beradi
                        var q = (IQueryable)set;
                        var take = typeof(Queryable).GetMethods().First(m => m.Name == "Take" && m.GetParameters()[1].ParameterType == typeof(int))
                            .MakeGenericMethod(q.ElementType);
                        foreach (var _ in (System.Collections.IEnumerable)take.Invoke(null, new object[] { q, 1 })) { }
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail(p.Name + ": " + ex.GetBaseException().Message);
                    }
                }
            }
        }

        [TestMethod]
        public void ReadOnlyRef_CannotBeWritten()
        {
            using (var uow = Uow())
            {
                var c = uow.Dst.Customers.FirstOrDefault();
                if (c == null) Assert.Inconclusive("CUSTOMERS bo'sh.");
                c.Name = c.Name + "x";
                Assert.ThrowsException<InvalidOperationException>(() => uow.Dst.SaveChanges());
            }
        }

        [TestMethod]
        public void CustomerService_CreatesCustomerAndProfile_WithAudit()
        {
            using (var uow = Uow())
            {
                var id = CustomerService.Save(uow, new CustomerInput
                {
                    Name = "Test Do'kon " + Guid.NewGuid().ToString("N").Substring(0, 6), Telephone = "90 123 45 67",
                    INN = "999888777", Lat = 41.311081m, Lng = 69.240562m, Category = "a", CreditLimit = 5000000m, PaymentDays = 14,
                    VisitDays = 1 | 4
                }, null, AuditActor.Web, DateTime.Now);

                var profile = uow.Dst.CustomerProfiles.Find(id);
                Assert.IsNotNull(profile);
                Assert.AreEqual("A", profile.Category);
                Assert.AreEqual(41.311081m, profile.Lat, "decimal(9,6) aniqligi saqlanishi kerak");
                Assert.AreEqual("+998901234567", uow.Erp.CUSTOMERS.Find(id).Telephone);
                Assert.IsTrue(profile.IsApproved);
                Assert.AreEqual(1, uow.Dst.AuditTrail.Count(a => a.EntityName == "CustomerProfile" && a.EntityKey == id.ToString() && a.Action == "I"));

                // bot orqali qo'shilgan nuqta tasdiq kutadi
                var botId = CustomerService.Save(uow, new CustomerInput { Name = "Bot nuqta " + Guid.NewGuid().ToString("N").Substring(0, 6) },
                    null, AuditActor.Bot, DateTime.Now);
                Assert.IsFalse(uow.Dst.CustomerProfiles.Find(botId).IsApproved);

                // takror STIR rad etiladi
                Assert.ThrowsException<DstValidationException>(() => CustomerService.Save(uow,
                    new CustomerInput { Name = "Boshqa", INN = "999888777" }, null, AuditActor.Web, DateTime.Now));
            }
        }

        [TestMethod]
        public void Pricing_ResolveFromDatabase()
        {
            using (var uow = Uow())
            {
                var db = uow.Dst;
                var productId = db.Products.Where(p => !p.IsDeleted).Select(p => p.ID).FirstOrDefault();
                if (productId == 0) Assert.Inconclusive("PRODUCTS bo'sh.");
                var defaultType = db.PriceTypes.First(t => t.IsDefault);
                var other = db.PriceTypes.First(t => !t.IsDefault);
                var now = DateTime.Now;

                var pricing = new PricingService(db);
                Assert.IsNotNull(pricing.SetPrice(defaultType.ID, productId, 100000m, 90000m, null, now.AddMinutes(-5)));
                db.SaveChanges();
                Assert.IsNull(pricing.SetPrice(defaultType.ID, productId, 100000m, 90000m, null, now.AddMinutes(-4)), "o'zgarmagan narx yozilmaydi");
                Assert.IsNotNull(pricing.SetPrice(other.ID, productId, 95000m, null, null, now.AddMinutes(-3)));
                db.SaveChanges();
                Assert.ThrowsException<DstValidationException>(() => pricing.SetPrice(other.ID, productId, 100m, 200m, null, now));

                var prices = pricing.Resolve(null, new[] { productId }, now);
                Assert.AreEqual(100000m, prices[productId].Price);
                Assert.AreEqual(PriceSource.DefaultPriceType, prices[productId].Source);

                var byType = pricing.CurrentByType(productId, now);
                Assert.AreEqual(95000m, byType.Single(x => x.Type.ID == other.ID).Item.Price);
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
                        if (rows[r][c] != null) ws.Cell(r + 1, c + 1).Value = rows[r][c];
                wb.SaveAs(ms);
            }
            ms.Position = 0;
            return ms;
        }

        [TestMethod]
        public void ImportCustomers_DryRun_ValidatesAllThenWritesNothing()
        {
            string tag = Guid.NewGuid().ToString("N").Substring(0, 6);
            int before = (int)Scalar("SELECT COUNT(*) FROM dst.CUSTOMER_PROFILES");

            var ok = ImportService.ImportCustomers(Xlsx(
                new[] { "Nomi", "STIR", "Telefon", "Toifa (A/B/C)", "Kredit limiti", "Tashrif kunlari (1-7)" },
                new[] { "Imp A " + tag, null, "901112233", "B", "1000000", "1,4" },
                new[] { "Imp B " + tag, null, null, null, "0", null }), null, Uow, dryRun: true);
            Assert.IsTrue(ok.Ok, string.Join("; ", ok.Errors.Select(e => e.Row + ": " + e.Message)));
            Assert.AreEqual(2, ok.Created);

            var bad = ImportService.ImportCustomers(Xlsx(
                new[] { "Наименование", "Категория (A/B/C)" },
                new[] { "Imp C " + tag, "Z" },
                new[] { null, "A" },
                new[] { "Imp D " + tag, "C" }), null, Uow, dryRun: false);
            Assert.IsFalse(bad.Ok);
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, bad.Errors.Select(e => e.Row).ToArray());
            Assert.AreEqual(0, bad.Created, "xato bo'lsa hech narsa yozilmaydi");

            var noColumn = ImportService.ImportCustomers(Xlsx(new[] { "Telefon" }, new[] { "901112233" }), null, Uow, dryRun: true);
            Assert.IsFalse(noColumn.Ok);

            Assert.AreEqual(before, (int)Scalar("SELECT COUNT(*) FROM dst.CUSTOMER_PROFILES"));
        }

        [TestMethod]
        public void ImportProducts_DryRun_WithUnitsAndBarcodes()
        {
            var units = (string)Scalar("SELECT STRING_AGG(ShortName, '|') WITHIN GROUP (ORDER BY ID) FROM (SELECT TOP 2 ID, ShortName FROM dbo.UNITS WHERE ISNULL(IsDeleted,0)=0 AND ShortName IN (SELECT ShortName FROM dbo.UNITS GROUP BY ShortName HAVING COUNT(*)=1) ORDER BY ID) u");
            var parts = units?.Split('|');
            if (parts == null || parts.Length < 2) Assert.Inconclusive("UNITS da 2 ta noyob birlik yo'q.");
            if ((int)Scalar("SELECT COUNT(*) FROM dbo.SHOPS WHERE ISNULL(IsDeleted,0)=0") == 0) Assert.Inconclusive("SHOPS bo'sh.");

            string pno = "T" + Guid.NewGuid().ToString("N").Substring(0, 10);
            var result = ImportService.ImportProducts(Xlsx(
                new[] { "Artikul", "Nomi", "Asosiy birlik", "Brend", "Shtrix-kod", "Birlik 2", "Koeffitsient 2", "Shtrix-kod 2" },
                new[] { pno, "Kafel test", parts[0], "TestBrand " + pno, "TB" + pno, parts[1], "1,44", "TC" + pno }), Uow, dryRun: true);
            Assert.IsTrue(result.Ok, string.Join("; ", result.Errors.Select(e => e.Row + ": " + e.Message)));
            Assert.AreEqual(1, result.Created);
            Assert.AreEqual(0, (int)Scalar("SELECT COUNT(*) FROM dbo.PRODUCTS WHERE PNo = '" + pno + "'"));

            var dup = ImportService.ImportProducts(Xlsx(
                new[] { "Artikul", "Nomi", "Asosiy birlik", "Birlik 2", "Koeffitsient 2" },
                new[] { pno, "A", parts[0], parts[0], "2" },          // qo'shimcha birlik = asosiy
                new[] { pno, "B", parts[0], null, null }), Uow, dryRun: true);   // faylda takror
            Assert.AreEqual(2, dup.Errors.Count);
        }
    }
}
