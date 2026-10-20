using System;
using System.Linq;
using System.Web.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Services.Editions;

namespace tahsinERP.Tests.Editions
{
    [TestClass]
    public class EditionModulesTests
    {
        [TestMethod]
        public void EveryController_IsMapped()
        {
            // Yangi controller qo'shilsa — EditionModules xaritasiga (kerak bo'lsa Core'ga) qo'shing
            var controllers = typeof(MvcApplication).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(Controller).IsAssignableFrom(t)
                            && t.Name.EndsWith("Controller"))
                .Select(t => t.Name.Substring(0, t.Name.Length - "Controller".Length))
                .ToList();
            Assert.IsTrue(controllers.Count > 50);

            var mapped = EditionModules.MappedControllers.ToList();
            var missing = controllers.Where(c => !mapped.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
            Assert.AreEqual(0, missing.Count, "Xaritada yo'q: " + string.Join(", ", missing));

            var stale = mapped.Where(m => !controllers.Contains(m, StringComparer.OrdinalIgnoreCase)).ToList();
            Assert.AreEqual(0, stale.Count, "Xaritada ortiqcha: " + string.Join(", ", stale));
        }

        [TestMethod]
        public void Start_OnlySrmCrmTasks()
        {
            Assert.IsTrue(EditionModules.IsControllerAllowed("START", "POrders"));
            Assert.IsTrue(EditionModules.IsControllerAllowed("START", "Incomes"));
            Assert.IsTrue(EditionModules.IsControllerAllowed("START", "SOrder"));
            Assert.IsTrue(EditionModules.IsControllerAllowed("start", "TaskKanban"), "registr ahamiyatsiz");
            Assert.IsFalse(EditionModules.IsControllerAllowed("START", "ProductPlan"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("START", "Bom"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("START", "FinTransactions"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("START", "HrPayroll"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("START", "Waybill"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("START", "OfficialLetters"));
        }

        [TestMethod]
        public void Biznes_AddsMesEngineeringFinance()
        {
            Assert.IsTrue(EditionModules.IsControllerAllowed("BIZNES", "POrders"));
            Assert.IsTrue(EditionModules.IsControllerAllowed("BIZNES", "ProductPlan"));
            Assert.IsTrue(EditionModules.IsControllerAllowed("BIZNES", "Bom"));
            Assert.IsTrue(EditionModules.IsControllerAllowed("BIZNES", "FinTransactions"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("BIZNES", "HrPayroll"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("BIZNES", "Waybill"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("BIZNES", "CompanyOrders"));
        }

        [TestMethod]
        public void KorpAndTrial_Everything()
        {
            // Qo'shimcha xizmatlar (Sayt) paketga kirmaydi — ular "+SITE" bilan alohida ochiladi (SiteAddOnEditionTests)
            foreach (var e in new[] { "KORP", "TRIAL" })
                foreach (var c in EditionModules.MappedControllers.Where(c => !EditionModules.IsAddOn(EditionModules.ModuleOf(c))))
                    Assert.IsTrue(EditionModules.IsControllerAllowed(e, c), e + "/" + c);
        }

        [TestMethod]
        public void CoreControllers_AlwaysOpen()
        {
            foreach (var c in new[] { "Home", "Account", "License", "AdminPanel", "Product", "Warehouse", "Currency", "Reconciliation" })
                Assert.IsTrue(EditionModules.IsControllerAllowed("START", c), c);
            Assert.IsTrue(EditionModules.IsControllerAllowed("START", "SomethingNew"), "xaritada yo'q — Core");
        }

        [TestMethod]
        public void EmptyV1OrUnknownEdition_AllOpen()
        {
            foreach (var e in new[] { null, "", "  ", "Standard", "Pro", "Enterprise" })
            {
                Assert.IsFalse(EditionModules.IsRestrictedEdition(e));
                Assert.IsTrue(EditionModules.IsControllerAllowed(e, "HrPayroll"));
                Assert.IsTrue(EditionModules.IsControllerAllowed(e, "ProductPlan"));
            }
        }

        [TestMethod]
        public void CheapestEdition_ForUpgradePage()
        {
            Assert.AreEqual("START", EditionModules.CheapestEditionWith(ProductModule.Srm));
            Assert.AreEqual("BIZNES", EditionModules.CheapestEditionWith(ProductModule.Mes));
            Assert.AreEqual("KORP", EditionModules.CheapestEditionWith(ProductModule.Hr));
            Assert.AreEqual(ProductModule.Engineering, EditionModules.ModuleOf("BOM"));
        }
    }
}
