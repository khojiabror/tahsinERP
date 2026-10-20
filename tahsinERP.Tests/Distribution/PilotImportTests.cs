using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Configuration;

namespace tahsinERP.Tests.Distribution
{
    /// <summary>S6: pilot importining bazasiz qoidalari (ha/yo'q, tashrif kunlari, shablon).</summary>
    [TestClass]
    public class PilotImportRulesTests
    {
        [TestMethod]
        public void ParseYesNo_AcceptsUzRuAndDigits()
        {
            foreach (var v in new[] { "ha", "Ha", "да", "1", "+", "yes" }) Assert.AreEqual(true, ImportService.ParseYesNo(v), v);
            foreach (var v in new[] { "yo'q", "yo‘q", "нет", "0", "-", "no" }) Assert.AreEqual(false, ImportService.ParseYesNo(v), v);
            Assert.IsNull(ImportService.ParseYesNo(" "));
            Assert.ThrowsException<DstValidationException>(() => ImportService.ParseYesNo("balki"));
        }

        [TestMethod]
        public void VisitDaysText_IsInverseOfParse()
        {
            Assert.AreEqual("1,4,7", ImportService.VisitDaysText(ImportService.ParseVisitDays("7, 1;4")));
            Assert.AreEqual("—", ImportService.VisitDaysText(0));
        }

        [TestMethod]
        public void Template_SampleSheetIsNotImported()
        {
            foreach (var columns in new[] { ImportService.TerritoryColumns, ImportService.PriceTypeColumns, ImportService.RouteColumns, ImportService.UnitColumns, ImportService.CustomerColumns })
            {
                var bytes = ImportService.Template(columns, russian: false);
                using (var wb = new XLWorkbook(new MemoryStream(bytes)))
                    Assert.AreEqual(2, wb.Worksheets.Count, "Import + Namuna");
                var sheet = ImportService.Read(new MemoryStream(bytes));
                Assert.AreEqual(columns.Length, sheet.Headers.Count);
                Assert.AreEqual(0, sheet.Rows.Count, "namuna qatori importga tushmasin");
            }
        }
    }

    /// <summary>S6 pilot importi lokal bazada, faqat tekshirish (rollback) rejimida — hech narsa saqlanmaydi.</summary>
    [TestClass]
    [TestCategory("Integration")]
    public class PilotImportTests
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
            if (_sql == null) Assert.Inconclusive("Lokal baza yoki distributsiya migratsiyalari yo'q.");
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

        private static string Errors(ImportResult r) => string.Join("; ", r.Errors.Select(e => e.Row + ": " + e.Message));

        [TestMethod]
        public void Territories_CreatesTreeOnce_AndRejectsGaps()
        {
            string tag = Guid.NewGuid().ToString("N").Substring(0, 6);
            int before = (int)Scalar("SELECT COUNT(*) FROM dst.TERRITORIES");

            var ok = ImportService.ImportTerritories(Xlsx(
                new[] { "Viloyat", "Viloyat (rus)", "Tuman", "Tuman (rus)", "Hudud" },
                new[] { "V " + tag, "О " + tag, "T1 " + tag, null, "H1 " + tag },
                new[] { "v " + tag, null, "T2 " + tag, "Р2 " + tag, null },
                new[] { "О " + tag, null, "T1 " + tag, null, "H1 " + tag }), Uow, dryRun: true);   // ruscha nom bilan — o'sha tugun
            Assert.IsTrue(ok.Ok, Errors(ok));
            Assert.AreEqual(4, ok.Created, "viloyat, 2 tuman, hudud");
            CollectionAssert.AreEqual(new[] { ImportAction.New, ImportAction.New, ImportAction.Unchanged }, ok.Preview.Select(p => p.Action).ToArray());

            var bad = ImportService.ImportTerritories(Xlsx(
                new[] { "Viloyat", "Tuman", "Hudud" },
                new[] { "V " + tag, null, "H " + tag },
                new[] { null, "T " + tag, null }), Uow, dryRun: true);
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, bad.Errors.Select(e => e.Row).ToArray());

            Assert.AreEqual(before, (int)Scalar("SELECT COUNT(*) FROM dst.TERRITORIES"));
        }

        [TestMethod]
        public void PriceTypes_SingleDefault()
        {
            string tag = Guid.NewGuid().ToString("N").Substring(0, 6);
            var ok = ImportService.ImportPriceTypes(Xlsx(
                new[] { "Nomi", "Nomi (rus)", "Standart (ha/yo'q)", "Tartib" },
                new[] { "Diler " + tag, "Дилер " + tag, "ha", "5" },
                new[] { "Chakana " + tag, null, null, null }), Uow, dryRun: true);
            Assert.IsTrue(ok.Ok, Errors(ok));
            Assert.AreEqual(2, ok.Created);
            Assert.IsNotNull(ok.Preview[0].Details, "standart belgilandi");

            var two = ImportService.ImportPriceTypes(Xlsx(
                new[] { "Nomi", "Standart (ha/yo'q)" },
                new[] { "A " + tag, "ha" },
                new[] { "B " + tag, "да" }), Uow, dryRun: true);
            Assert.IsFalse(two.Ok);

            var dup = ImportService.ImportPriceTypes(Xlsx(new[] { "Nomi" }, new[] { "X " + tag }, new[] { "x " + tag }), Uow, dryRun: true);
            CollectionAssert.AreEqual(new[] { 3 }, dup.Errors.Select(e => e.Row).ToArray());
            Assert.AreEqual(0, (int)Scalar("SELECT COUNT(*) FROM dst.PRICE_TYPES WHERE Name LIKE '% " + tag + "'"));
        }

        [TestMethod]
        public void Routes_UpdateAgentAndDays_OnlyExistingCustomers()
        {
            var name = Scalar("SELECT TOP 1 c.Name FROM dst.CUSTOMER_PROFILES p JOIN dbo.CUSTOMERS c ON c.ID = p.CustomerID " +
                              "WHERE ISNULL(c.IsDeleted,0)=0 AND c.Name IN (SELECT c2.Name FROM dbo.CUSTOMERS c2 JOIN dst.CUSTOMER_PROFILES p2 ON p2.CustomerID = c2.ID " +
                              "WHERE ISNULL(c2.IsDeleted,0)=0 GROUP BY c2.Name HAVING COUNT(*) = 1) ORDER BY p.CustomerID") as string;
            if (name == null) Assert.Inconclusive("Distributsiya mijozi yo'q.");
            var days = Convert.ToInt32(Scalar("SELECT p.VisitDays FROM dst.CUSTOMER_PROFILES p JOIN dbo.CUSTOMERS c ON c.ID = p.CustomerID WHERE c.Name = N'" + name.Replace("'", "''") + "'"));
            string newDays = days == 0x41 ? "2" : "1,7";

            var ok = ImportService.ImportRoutes(Xlsx(
                new[] { "Mijoz (STIR yoki nomi)", "Agent (login)", "Tashrif kunlari (1-7)" },
                new[] { name, "-", newDays }), Uow, dryRun: true);
            Assert.IsTrue(ok.Ok, Errors(ok));
            Assert.AreEqual(1, ok.Updated);
            StringAssert.Contains(ok.Preview[0].Details, newDays);
            Assert.AreEqual(days, Convert.ToInt32(Scalar("SELECT p.VisitDays FROM dst.CUSTOMER_PROFILES p JOIN dbo.CUSTOMERS c ON c.ID = p.CustomerID WHERE c.Name = N'" + name.Replace("'", "''") + "'")), "rollback");

            var bad = ImportService.ImportRoutes(Xlsx(
                new[] { "Mijoz (STIR yoki nomi)", "Agent (login)", "Tashrif kunlari (1-7)" },
                new[] { "Yo'q mijoz " + Guid.NewGuid().ToString("N"), null, "1" },
                new[] { name, "no-such-login-" + Guid.NewGuid().ToString("N").Substring(0, 6), null },
                new[] { name, null, "8" }), Uow, dryRun: true);
            CollectionAssert.AreEquivalent(new[] { 2, 3, 4 }, bad.Errors.Select(e => e.Row).ToArray());
        }

        [TestMethod]
        public void Units_AddToExistingProduct_WithRules()
        {
            var row = Scalar("SELECT TOP 1 p.PNo + '|' + bu.ShortName + '|' + ou.ShortName FROM dbo.PRODUCTS p " +
                             "JOIN dbo.UNITS bu ON bu.ID = p.UnitID " +
                             "CROSS APPLY (SELECT TOP 1 u.ShortName FROM dbo.UNITS u WHERE ISNULL(u.IsDeleted,0)=0 AND u.ID <> p.UnitID " +
                             "  AND u.ShortName IN (SELECT ShortName FROM dbo.UNITS WHERE ISNULL(IsDeleted,0)=0 GROUP BY ShortName HAVING COUNT(*)=1) ORDER BY u.ID) ou " +
                             "WHERE ISNULL(p.IsDeleted,0)=0 AND p.PNo IS NOT NULL AND bu.ShortName IN (SELECT ShortName FROM dbo.UNITS WHERE ISNULL(IsDeleted,0)=0 GROUP BY ShortName HAVING COUNT(*)=1) " +
                             "AND p.PNo IN (SELECT PNo FROM dbo.PRODUCTS WHERE ISNULL(IsDeleted,0)=0 GROUP BY PNo HAVING COUNT(*)=1) ORDER BY p.ID") as string;
            if (row == null) Assert.Inconclusive("Mos tovar/birlik yo'q.");
            var parts = row.Split('|');
            int before = (int)Scalar("SELECT COUNT(*) FROM dst.PRODUCT_UNITS");

            var ok = ImportService.ImportUnits(Xlsx(
                new[] { "Artikul", "Birlik", "Koeffitsient", "Og'irlik (kg)", "Shtrix-kod", "Odatiy sotish (ha/yo'q)" },
                new[] { parts[0], parts[2], "12", "24.5", "TU" + Guid.NewGuid().ToString("N").Substring(0, 10), "ha" }), Uow, dryRun: true);
            Assert.IsTrue(ok.Ok, Errors(ok));
            Assert.AreEqual(1, ok.Created + ok.Updated);

            var bad = ImportService.ImportUnits(Xlsx(
                new[] { "Artikul", "Birlik", "Koeffitsient" },
                new[] { parts[0], parts[1], "2" },        // asosiy birlik
                new[] { parts[0], parts[2], "0" },        // koeffitsient > 0
                new[] { "NOPNO" + Guid.NewGuid().ToString("N").Substring(0, 8), parts[2], "2" }), Uow, dryRun: true);
            CollectionAssert.AreEquivalent(new[] { 2, 3, 4 }, bad.Errors.Select(e => e.Row).ToArray());

            Assert.AreEqual(before, (int)Scalar("SELECT COUNT(*) FROM dst.PRODUCT_UNITS"));
        }
    }
}
