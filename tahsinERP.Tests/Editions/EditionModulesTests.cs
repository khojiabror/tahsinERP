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

        [TestMethod]
        public void Dist_DistributionWithoutMesAndBom()
        {
            Assert.IsTrue(EditionModules.IsRestrictedEdition("DIST"));
            Assert.IsTrue(EditionModules.IsControllerAllowed("DIST", "POrders"));
            Assert.IsTrue(EditionModules.IsControllerAllowed("DIST", "SOrder"));
            Assert.IsTrue(EditionModules.IsControllerAllowed("DIST", "TaskKanban"));
            Assert.IsTrue(EditionModules.IsControllerAllowed("dist", "FinTransactions"));
            Assert.IsTrue(EditionModules.IsControllerAllowed("DIST", "DstOrders"));
            Assert.IsTrue(EditionModules.IsControllerAllowed("DIST", "DstApi"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("DIST", "ProductPlan"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("DIST", "Bom"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("DIST", "Tannarx"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("DIST", "HrPayroll"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("DIST", "Waybill"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("DIST", "OfficialLetters"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("DIST", "SiteHome"), "sayt — alohida xizmat");
            Assert.IsTrue(EditionModules.IsControllerAllowed("DIST+SITE", "SiteHome"));
            Assert.AreEqual("Distributsiya", EditionModules.EditionDisplayName("DIST"));
            CollectionAssert.AreEqual(
                new[] { ProductModule.Srm, ProductModule.Crm, ProductModule.Tasks, ProductModule.Finance, ProductModule.Commerce, ProductModule.Distribution },
                EditionModules.ModulesOf("DIST").ToArray());
            // Distributsiya KORP va TRIAL'da ham qoladi; asosiy zanjir o'zgarmaydi
            Assert.IsTrue(EditionModules.IsControllerAllowed("KORP", "DstOrders"));
            Assert.IsTrue(EditionModules.IsControllerAllowed("TRIAL", "DstOrders"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("BIZNES", "DstOrders"));
            CollectionAssert.AreEqual(new[] { "START", "BIZNES", "KORP" }, EditionModules.UpgradeOrder.ToArray());
        }

        [TestMethod]
        public void Suggestions_DistIsSideBranch()
        {
            CollectionAssert.AreEqual(new[] { "DIST", "KORP" }, EditionModules.SuggestEditionsFor("START", ProductModule.Distribution).ToArray());
            CollectionAssert.AreEqual(new[] { "KORP", "DIST" }, EditionModules.SuggestEditionsFor("BIZNES", ProductModule.Distribution).ToArray());
            CollectionAssert.AreEqual(new[] { "KORP", "BIZNES" }, EditionModules.SuggestEditionsFor("DIST", ProductModule.Mes).ToArray());
            CollectionAssert.AreEqual(new[] { "KORP", "BIZNES" }, EditionModules.SuggestEditionsFor("DIST+SITE", ProductModule.Engineering).ToArray());
            CollectionAssert.AreEqual(new[] { "KORP" }, EditionModules.SuggestEditionsFor("DIST", ProductModule.Hr).ToArray());
            CollectionAssert.AreEqual(new[] { "BIZNES", "KORP" }, EditionModules.SuggestEditionsFor("START", ProductModule.Mes).ToArray());
            Assert.AreEqual(0, EditionModules.SuggestEditionsFor("START", ProductModule.Website).Count);
            Assert.AreEqual("Distributsiya yoki Korporativ", EditionModules.SuggestionText("START", ProductModule.Distribution));
            Assert.AreEqual("Korporativ yoki Biznes", EditionModules.SuggestionText("DIST", ProductModule.Mes));
            Assert.IsTrue(EditionModules.Covers("KORP", "DIST"));
            Assert.IsTrue(EditionModules.Covers("DIST", "START"));
            Assert.IsFalse(EditionModules.Covers("BIZNES", "DIST"));
            Assert.IsFalse(EditionModules.Covers("DIST", "BIZNES"));
            Assert.AreEqual("KORP", EditionModules.CheapestEditionWith(ProductModule.Distribution), "zanjir bo'yicha — o'zgarmagan");
        }

        [TestMethod]
        public void Savdo_CommerceWithoutField()
        {
            Assert.IsTrue(EditionModules.IsRestrictedEdition("SAVDO"));
            CollectionAssert.AreEqual(
                new[] { ProductModule.Srm, ProductModule.Crm, ProductModule.Tasks, ProductModule.Finance, ProductModule.Commerce },
                EditionModules.ModulesOf("SAVDO").ToArray());
            // buyurtma, ombor, narx, qarz, bot API va kanallar sahifasi — ochiq
            foreach (var c in new[] { "DstHome", "DstOrders", "DstPrices", "DstStock", "DstDebts", "DstCustomers", "DstTeams", "DstApi", "DstChannels", "DstSettings" })
                Assert.IsTrue(EditionModules.IsControllerAllowed("SAVDO", c), c);
            // dala — yopiq
            foreach (var c in new[] { "DstField", "DstRoutes", "DstTrips", "DstVehicles", "DstTerritories", "DstKpi" })
                Assert.IsFalse(EditionModules.IsControllerAllowed("SAVDO", c), c);
            Assert.IsFalse(EditionModules.IsControllerAllowed("SAVDO", "SiteHome"), "sayt — alohida xizmat");
            Assert.IsTrue(EditionModules.IsControllerAllowed("SAVDO+SITE", "SiteCatalog"));
            Assert.IsTrue(EditionModules.IsControllerAllowed("SAVDO+SITE", "SiteApi"));
            Assert.AreEqual("Savdo", EditionModules.EditionDisplayName("SAVDO"));
            Assert.IsTrue(EditionModules.Covers("DIST", "SAVDO"));
            Assert.IsTrue(EditionModules.Covers("KORP", "SAVDO"));
            Assert.IsFalse(EditionModules.Covers("SAVDO", "DIST"));
            CollectionAssert.AreEqual(new[] { "SAVDO", "DIST", "KORP" }, EditionModules.SuggestEditionsFor("START", ProductModule.Commerce).ToArray());
            CollectionAssert.AreEqual(new[] { "DIST", "KORP" }, EditionModules.SuggestEditionsFor("SAVDO", ProductModule.Distribution).ToArray());
        }

        [TestMethod]
        public void Commerce_FieldDependsOnIt_AndLegacyTrialKeepsIt()
        {
            // eski sinov kaliti: Distribution oldin buyurtma/ombor qismini ham o'z ichiga olardi
            Assert.IsTrue(EditionModules.IsControllerAllowed("TRIAL:Srm,Distribution", "DstOrders"));
            Assert.IsTrue(EditionModules.IsControllerAllowed("TRIAL:Srm,Distribution", "DstField"));
            Assert.IsTrue(EditionModules.IsControllerAllowed("TRIAL:Srm,Commerce", "DstOrders"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("TRIAL:Srm,Commerce", "DstField"));
            // tanlovda dala Savdo va Moliyani o'zi bilan olib keladi
            Assert.AreEqual("TRIAL:Finance,Commerce,Distribution",
                EditionModules.ComposeModules("TRIAL", new[] { ProductModule.Distribution }));
            Assert.AreEqual("SAVDO", EditionModules.ComposeModules("SAVDO",
                new[] { ProductModule.Srm, ProductModule.Crm, ProductModule.Tasks, ProductModule.Commerce }));   // Moliya bog'liqlik bilan qo'shiladi
        }
    }
}
