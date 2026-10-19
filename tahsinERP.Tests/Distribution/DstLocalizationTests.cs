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
