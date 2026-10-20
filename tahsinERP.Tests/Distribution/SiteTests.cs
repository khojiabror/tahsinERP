using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Distribution.Services.Orders;
using tahsinERP.Areas.Site.Controllers;
using tahsinERP.Areas.Site.Data;
using tahsinERP.Areas.Site.Services;
using tahsinERP.Configuration;
using tahsinERP.Services.Editions;

namespace tahsinERP.Tests.Distribution
{
    [TestClass]
    public class SiteTextTests
    {
        [TestMethod]
        public void Slugify_LatinCyrillicApostrophes()
        {
            Assert.AreEqual("sement-m400-50-kg", SiteText.Slugify("Sement M400 (50 kg)"));
            Assert.AreEqual("tsement-m-400", SiteText.Slugify("Цемент М-400"));
            Assert.AreEqual("gisht-qizil", SiteText.Slugify("G‘isht  qizil!"));
            Assert.AreEqual("ozbekiston", SiteText.Slugify("O'zbekiston"));
            Assert.AreEqual("", SiteText.Slugify("  ***  "));
            Assert.IsTrue(SiteText.Slugify(new string('a', 300)).Length <= SiteText.SlugMaxLength);
        }

        [TestMethod]
        public void Unique_AndValidation()
        {
            var taken = new[] { "sement", "sement-2" };
            Assert.AreEqual("sement-3", SiteText.Unique("sement", taken.Contains, "p-1"));
            Assert.AreEqual("p-1", SiteText.Unique("", taken.Contains, "p-1"));
            Assert.IsTrue(SiteText.IsValidSlug("sement-m400"));
            foreach (var bad in new[] { "", "Sement", "a--b", "-a", "a-", "a b", "а" })
                Assert.IsFalse(SiteText.IsValidSlug(bad), bad);
        }

        [TestMethod]
        public void Host_Color_Badges()
        {
            Assert.AreEqual("shop.example.uz", SiteText.NormalizeHost(" https://Shop.Example.uz/ "));
            foreach (var bad in new[] { "localhost", "a..b", "-a.uz", "a.uz.", "a b.uz", "a/b.uz", "" })
                Assert.IsNull(SiteText.NormalizeHost(bad), bad);
            Assert.IsTrue(SiteText.IsColor("#1a2B3c"));
            Assert.IsFalse(SiteText.IsColor("red"));
            Assert.AreEqual("NEW,HIT", SiteText.NormalizeBadges(new[] { "new", "HIT", "x", "NEW" }));
        }
    }

    [TestClass]
    public class SiteAddOnEditionTests
    {
        [TestMethod]
        public void SiteIsAddOn_NotPartOfAnyEdition()
        {
            Assert.IsFalse(EditionModules.IsControllerAllowed("KORP", "SiteCatalog"), "KORP'ga kirmaydi");
            Assert.IsTrue(EditionModules.IsControllerAllowed("KORP+SITE", "SiteCatalog"));
            Assert.IsTrue(EditionModules.IsControllerAllowed("KORP+SITE", "DstOrders"), "asosiy paket saqlanadi");
            Assert.IsTrue(EditionModules.IsControllerAllowed("start+site", "SiteApi"));
            Assert.IsFalse(EditionModules.IsControllerAllowed("START+SITE", "DstOrders"));
            Assert.IsTrue(EditionModules.IsControllerAllowed(null, "SiteCatalog"), "eski/cheklovsiz kalit — hammasi ochiq");
            Assert.IsTrue(EditionModules.IsRestrictedEdition("BIZNES+SITE"));
            Assert.IsTrue(EditionModules.IsAddOn(ProductModule.Website));
            CollectionAssert.Contains(EditionModules.ModulesOf("START+SITE").ToList(), ProductModule.Website);
            CollectionAssert.DoesNotContain(EditionModules.ModulesOf("KORP").ToList(), ProductModule.Website);
            Assert.AreEqual(0, EditionModules.AddOnsOf("KORP+UNKNOWN").Count);
            StringAssert.Contains(EditionModules.EditionDisplayName("KORP+SITE"), "Sayt");
        }
    }

    [TestClass]
    public class SiteMediaTests
    {
        private static byte[] Image(int w, int h, ImageFormat format, bool alpha)
        {
            using (var bmp = new Bitmap(w, h, alpha ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb))
            using (var g = Graphics.FromImage(bmp))
            using (var ms = new MemoryStream())
            {
                g.Clear(alpha ? Color.FromArgb(0, 255, 255, 255) : Color.SteelBlue);
                g.FillEllipse(Brushes.OrangeRed, w / 4, h / 4, w / 2, h / 2);
                bmp.Save(ms, format);
                return ms.ToArray();
            }
        }

        [TestMethod]
        public void Prepare_ResizesLargeJpeg_KeepsTransparentPng()
        {
            var jpg = SiteMediaService.Prepare(Image(3000, 1500, ImageFormat.Jpeg, false));
            Assert.AreEqual("image/jpeg", jpg.ContentType);
            Assert.AreEqual(2000, jpg.Width);
            Assert.AreEqual(1000, jpg.Height);
            Assert.AreEqual(64, jpg.Hash.Length);

            var png = SiteMediaService.Prepare(Image(400, 200, ImageFormat.Png, true));
            Assert.AreEqual("image/png", png.ContentType);
            Assert.AreEqual(400, png.Width);

            Assert.AreEqual(SiteMediaService.Prepare(Image(50, 50, ImageFormat.Png, true)).Hash,
                SiteMediaService.Prepare(Image(50, 50, ImageFormat.Png, true)).Hash, "bir xil rasm — bir xil hash (takror saqlanmaydi)");
            Assert.ThrowsException<DstValidationException>(() => SiteMediaService.Prepare(new byte[] { 1, 2, 3, 4 }));
            Assert.ThrowsException<DstValidationException>(() => SiteMediaService.Prepare(new byte[0]));
        }
    }

    /// <summary>Snapshot va arizalar — lokal bazada, rollback bilan.</summary>
    [TestClass]
    [TestCategory("Integration")]
    public class SiteIntegrationTests
    {
        private static string _sql;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            try
            {
                _sql = DbConfig.GetSqlConnectionString();
                using (var c = new SqlConnection(_sql))
                using (var cmd = new SqlCommand("SELECT OBJECT_ID('site.PRODUCT_CONTENT', 'U')", c))
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
            if (_sql == null) Assert.Inconclusive("Lokal baza yoki migratsiya 021 yo'q.");
        }

        private static void Settings(DistributionDbContext db, string priceMode, string stockMode)
        {
            SiteSettingsStore.Set(db, SiteSettingKeys.PriceMode, priceMode, DateTime.Now);
            SiteSettingsStore.Set(db, SiteSettingKeys.StockMode, stockMode, DateTime.Now);
            SiteSettingsStore.Set(db, SiteSettingKeys.PriceTypeId, null, DateTime.Now);
            SiteSettingsStore.Set(db, SiteSettingKeys.LeadsEnabled, "1", DateTime.Now);
            db.SaveChanges();
        }

        [TestMethod]
        public void Snapshot_PublishedOnly_PriceAndStockByMode_StableETag()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 0);
                var db = uow.Dst;
                Settings(db, PriceModes.Show, StockModes.Exact);

                var c = SiteCatalogController.EnsureContent(db, f.ProductId);
                db.SaveChanges();
                var snap = SiteSnapshotBuilder.Build(db, DateTime.Now);
                Assert.IsFalse(snap.Products.Any(x => x.Id == f.ProductId), "nashr qilinmagan — yo'q");

                c.IsPublished = true;
                c.TitleRu = "Тест цемент";
                db.SiteProductAttributes.Add(new SiteProductAttribute { ProductID = f.ProductId, NameUz = "Marka", ValueUz = "M400" });
                db.SaveChanges();
                snap = SiteSnapshotBuilder.Build(db, DateTime.Now);
                var p = snap.Products.Single(x => x.Id == f.ProductId);
                Assert.AreEqual(c.Slug, p.Slug);
                Assert.AreEqual("Тест цемент", p.NameRu);
                Assert.AreEqual(55000m, p.Price);
                Assert.AreEqual(55000m, p.Units.Single(u => u.Id == null).Price);
                Assert.AreEqual(100m, p.StockQty);
                Assert.AreEqual("in", p.StockStatus);
                Assert.AreEqual("M400", p.Attributes.Single().ValueUz);
                var json = SiteSnapshotBuilder.Serialize(snap).Json;
                StringAssert.DoesNotMatch(json, new System.Text.RegularExpressions.Regex("50000"), "minimal narx / tannarx chiqmaydi");

                var e1 = SiteSnapshotBuilder.Serialize(SiteSnapshotBuilder.Build(db, DateTime.Now)).ETag;
                var e2 = SiteSnapshotBuilder.Serialize(SiteSnapshotBuilder.Build(db, DateTime.Now.AddSeconds(5))).ETag;
                Assert.AreEqual(e1, e2, "vaqt o'zgarishi yangi versiya emas");

                Settings(db, PriceModes.Ask, StockModes.Status);
                snap = SiteSnapshotBuilder.Build(db, DateTime.Now);
                p = snap.Products.Single(x => x.Id == f.ProductId);
                Assert.IsNull(p.Price);
                Assert.IsTrue(p.Units.All(u => u.Price == null));
                Assert.IsNull(p.StockQty);
                Assert.AreEqual("in", p.StockStatus);
                Assert.AreNotEqual(e1, SiteSnapshotBuilder.Serialize(snap).ETag);
            }
        }

        [TestMethod]
        public void Lead_Receive_Idempotent_ConvertToOrderOnce()
        {
            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var f = OrderFlowIntegrationTests.Arrange(uow, 100000000m);
                var db = uow.Dst;
                Settings(db, PriceModes.Show, StockModes.Status);
                var content = SiteCatalogController.EnsureContent(db, f.ProductId);
                db.SaveChanges();

                var input = new LeadInput
                {
                    ClientRequestId = Guid.NewGuid(), Name = "Ali Valiyev", Phone = "90 123 45 67", CompanyName = "Qurilish MChJ " + f.ProductId,
                    Language = "ru", Host = "test.example.uz", Items = { new LeadItemInput { ProductId = f.ProductId, Qty = 3 } }
                };
                Assert.ThrowsException<DstValidationException>(() => SiteLeadService.Receive(db, input, DateTime.Now), "nashr qilinmagan tovar");

                content.IsPublished = true;
                db.SaveChanges();
                var id = SiteLeadService.Receive(db, input, DateTime.Now);
                Assert.AreEqual(id, SiteLeadService.Receive(db, input, DateTime.Now), "takroriy yuborish — o'sha ariza");
                var lead = db.SiteLeads.Find(id);
                Assert.AreEqual("+998901234567", lead.Phone);
                var item = SiteLeadService.ItemsOf(lead).Single();
                Assert.AreEqual(55000m, item.Price);
                Assert.AreEqual(3m, item.Qty);

                Assert.ThrowsException<DstValidationException>(() => SiteLeadService.Receive(db, new LeadInput
                {
                    ClientRequestId = Guid.NewGuid(), Name = "A", Phone = "123", Language = "uz"
                }, DateTime.Now));

                var actor = new OrderActor { UserId = f.UserId, MaxDiscountPct = 5 };
                var orderId = SiteLeadService.ConvertToOrder(uow, id, null, actor, DateTime.Now);
                Assert.AreEqual(orderId, SiteLeadService.ConvertToOrder(uow, id, null, actor, DateTime.Now));
                var order = db.Orders.Find(orderId);
                Assert.AreEqual(lead.CustomerID, order.CustomerID);
                Assert.AreEqual(3m, db.OrderLines.Single(l => l.OrderID == orderId).Qty);
                Assert.AreEqual((byte)LeadStatus.Ordered, db.SiteLeads.Find(id).Status);
                Assert.AreEqual("+998901234567", db.Customers.Find(order.CustomerID).Telephone);
                Assert.ThrowsException<DstValidationException>(() => SiteLeadService.Reject(db, id, "x", f.UserId, DateTime.Now));
                Assert.AreEqual(1, SiteLeadService.MatchingCustomers(db, "+998901234567").Count(m => m.Id == order.CustomerID));
            }
        }
    }
}
