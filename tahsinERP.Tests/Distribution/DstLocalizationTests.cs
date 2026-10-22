using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services;

namespace tahsinERP.Tests.Distribution
{
    [TestClass]
    public class DstLocalizationTests
    {
        private static string ResourcesDir()
        {
            // bin\Debug → yechim ildizi
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "tahsinERP.sln")))
                dir = dir.Parent;
            Assert.IsNotNull(dir, "tahsinERP.sln topilmadi");
            return Path.Combine(dir.FullName, "tahsinERP", "Areas", "Distribution", "Resources");
        }

        private static string[] Keys(string file)
        {
            return XDocument.Load(Path.Combine(ResourcesDir(), file)).Root
                .Elements("data").Select(d => (string)d.Attribute("name")).OrderBy(k => k).ToArray();
        }

        [TestMethod]
        public void EveryUzbekKey_HasRussianTranslation()
        {
            var uz = Keys("DstStrings.resx");
            var ru = Keys("DstStrings.ru.resx");
            Assert.IsTrue(uz.Length > 0);
            CollectionAssert.AreEqual(uz, ru, "uz va ru kalitlari bir xil bo'lishi kerak: farq = " +
                string.Join(", ", uz.Except(ru).Concat(ru.Except(uz))));
        }

        [TestMethod]
        public void EveryKeyUsedInCode_ExistsInResx()
        {
            var keys = tahsinERP.Tests.Localization.LocalizationTests.AllErpKeys();   // DstStrings + ErpStrings + umumiy
            var root = Path.GetFullPath(Path.Combine(ResourcesDir(), "..", "..", ".."));   // tahsinERP loyiha papkasi
            var files = Directory.GetFiles(Path.Combine(root, "Areas", "Distribution"), "*.*", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(Path.Combine(root, "Areas", "Site"), "*.*", SearchOption.AllDirectories))
                .Where(f => f.EndsWith(".cs") || f.EndsWith(".cshtml"))
                .Concat(new[] { Path.Combine(root, "Views", "Shared", "_Layout.cshtml") });
            var used = files.SelectMany(f => System.Text.RegularExpressions.Regex
                    .Matches(File.ReadAllText(f), "(?:T|LocalizedKey)\\(\"([A-Za-z0-9_]+)\"")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => m.Groups[1].Value))
                .Where(k => !k.EndsWith("_"))   // "PointType_" + kod — quyida alohida
                .Distinct().ToList();
            Assert.IsTrue(used.Count > 100);
            var missing = used.Where(k => !keys.Contains(k)).ToList();
            Assert.AreEqual(0, missing.Count, "resx'da yo'q: " + string.Join(", ", missing));

            foreach (var p in tahsinERP.Areas.Distribution.Data.Entities.PointTypes.All)
                Assert.IsTrue(keys.Contains("PointType_" + p), p);
            foreach (var r in tahsinERP.Areas.Distribution.Data.Entities.ReasonTypes.All)
                Assert.IsTrue(keys.Contains("ReasonType_" + r), r);
            foreach (var m in tahsinERP.Areas.Distribution.Data.Entities.PaymentMethods.All)
                Assert.IsTrue(keys.Contains("PayMethod_" + m), m);
            foreach (var d in new[] { "SHIPMENT", "PAYMENT", "RETURN", "ADJUSTMENT", "OPENING" })
                Assert.IsTrue(keys.Contains("Ledger_" + d) && keys.Contains("Ledger_" + d + "_REV") || d == "ADJUSTMENT", d);
            foreach (var st in System.Enum.GetNames(typeof(tahsinERP.Areas.Distribution.Services.Orders.OrderStatus)))
                Assert.IsTrue(keys.Contains("OrderStatus_" + st), st);
            foreach (var b in tahsinERP.Areas.Site.Data.ProductBadges.All)
                Assert.IsTrue(keys.Contains("Site_Badge_" + b), b);
            foreach (var m in tahsinERP.Areas.Site.Data.PriceModes.All)
                Assert.IsTrue(keys.Contains("Site_PriceMode_" + m), m);
            foreach (var m in tahsinERP.Areas.Site.Data.StockModes.All)
                Assert.IsTrue(keys.Contains("Site_StockMode_" + m), m);
            foreach (var st in new[] { "all", "published", "unpublished", "nophoto", "nodesc" })
                Assert.IsTrue(keys.Contains("Site_State_" + st), st);
        }

        [TestMethod]
        public void T_UsesCurrentUiCulture()
        {
            var saved = Thread.CurrentThread.CurrentUICulture;
            try
            {
                Thread.CurrentThread.CurrentUICulture = DstLocalization.CultureFor("uz");
                Assert.AreEqual("Distributsiya", DstLocalization.T("Module_Title"));

                Thread.CurrentThread.CurrentUICulture = DstLocalization.CultureFor("ru");
                Assert.AreEqual("Дистрибуция", DstLocalization.T("Module_Title"));

                Assert.AreEqual("Unknown_Key_X", DstLocalization.T("Unknown_Key_X"), "topilmasa kalit qaytadi");
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = saved;
            }
        }

        [TestMethod]
        public void Normalize_DefaultsToUzbek()
        {
            Assert.AreEqual("ru", DstLocalization.Normalize("RU"));
            Assert.AreEqual("uz", DstLocalization.Normalize(null));
            Assert.AreEqual("uz", DstLocalization.Normalize("en"));
            Assert.AreEqual("oz", DstLocalization.Normalize("oz"));
        }

        [TestMethod]
        public void AuditFormat_IsCultureInvariant()
        {
            var saved = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
                Assert.AreEqual("1234.5", DistributionDbContext.Format(1234.5m));
                Assert.AreEqual("0.1", DistributionDbContext.Format(0.1d));
                Assert.AreEqual("2026-10-19", DistributionDbContext.Format(new DateTime(2026, 10, 19)));
                Assert.AreEqual("2026-10-19 08:05:03.000", DistributionDbContext.Format(new DateTime(2026, 10, 19, 8, 5, 3)));
                Assert.AreEqual("Posted", DistributionDbContext.Format(tahsinERP.Areas.Distribution.Data.Entities.DocumentStatus.Posted));
                Assert.IsNull(DistributionDbContext.Format(null));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = saved;
            }
        }
    }
}
