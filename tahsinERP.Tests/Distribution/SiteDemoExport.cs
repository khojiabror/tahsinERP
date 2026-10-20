using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Site.Controllers;
using tahsinERP.Areas.Site.Data;
using tahsinERP.Areas.Site.Services;

namespace tahsinERP.Tests.Distribution
{
    /// <summary>
    /// Namoyish/sinov uchun: lokal bazadagi tovarlardan sayt nusxasi (snapshot JSON + rasmlar) — ERP litsenziyasiz ham saytni ko'rish uchun.
    /// Faqat SITE_DEMO_OUT muhit o'zgaruvchisi berilganda ishlaydi; bazaga yozilganlarning hammasi rollback qilinadi.
    ///   set SITE_DEMO_OUT=C:\tmp\site-demo  →  snapshot.json va orig\{id}_{hash}
    ///   dotnet tahsinERP.Site.dll snapshot import demo C:\tmp\site-demo\snapshot.json  (+ orig papkasini {DataRoot}\demo\orig ga)
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class SiteDemoExport
    {
        [TestMethod]
        public void ExportDemoSnapshot()
        {
            var output = Environment.GetEnvironmentVariable("SITE_DEMO_OUT");
            if (string.IsNullOrWhiteSpace(output)) Assert.Inconclusive("SITE_DEMO_OUT berilmagan — eksport o'tkazib yuborildi.");
            Directory.CreateDirectory(Path.Combine(output, "orig"));

            using (var uow = new DistributionUnitOfWork(new AuditActor(null, AuditActor.System)))
            {
                var db = uow.Dst;
                var now = DateTime.Now;
                void Set(string k, string v) => SiteSettingsStore.Set(db, k, v, now);
                Set(SiteSettingKeys.Enabled, "1");
                Set(SiteSettingKeys.Name, "Demo Qurilish");
                Set(SiteSettingKeys.Tagline, "Qurilish va qadoqlash materiallari — ulgurji narxlarda, tez yetkazib berish");
                Set(SiteSettingKeys.PriceMode, PriceModes.Show);
                Set(SiteSettingKeys.StockMode, StockModes.Status);
                Set(SiteSettingKeys.Phone, "+998712000000");
                Set(SiteSettingKeys.Email, "info@demo.uz");
                Set(SiteSettingKeys.Address, "Toshkent sh., Chilonzor tumani, Bunyodkor ko'chasi, 1");
                Set(SiteSettingKeys.WorkingHours, "Du–Sh 9:00–18:00");
                Set(SiteSettingKeys.MapLat, "41.285");
                Set(SiteSettingKeys.MapLng, "69.204");
                Set(SiteSettingKeys.Telegram, "demo_qurilish");
                Set(SiteSettingKeys.PrimaryColor, "#111827");
                Set(SiteSettingKeys.Font, "Inter");
                db.SaveChanges();

                // narx: asosiy narx turida yo'q tovarlarga namoyish narxi
                var pricing = new PricingService(db);
                var type = pricing.DefaultPriceTypeId() ?? db.PriceTypes.Select(t => (int?)t.ID).FirstOrDefault();
                var products = db.Products.Where(p => !p.IsDeleted).OrderBy(p => p.ID).Take(400).ToList();
                foreach (var p in products)
                {
                    var c = SiteCatalogController.EnsureContent(db, p.ID);
                    c.IsPublished = true;
                    c.SortOrder = p.ID % 7 == 0 ? -1 : 0;
                    c.Badges = p.ID % 11 == 0 ? "SALE" : p.ID % 13 == 0 ? "NEW" : p.ID % 17 == 0 ? "HIT" : null;
                    c.DescriptionUz = "**" + p.Name + "** — sifatli mahsulot, ulgurji va chakana.\n\n- Sertifikatlangan\n- Ombordan tez yetkazib berish\n- Ulgurji xaridga chegirma";
                    db.SaveChanges();
                    if (type.HasValue && !db.PriceListItems.Any(x => x.ProductID == p.ID && x.PriceTypeID == type))
                        pricing.SetPrice(type.Value, p.ID, 5000m + (p.ID * 7919 % 400) * 500m, null, null, now.AddMinutes(-1));
                    if (!db.SiteProductAttributes.Any(a => a.ProductID == p.ID))
                    {
                        db.SiteProductAttributes.Add(new SiteProductAttribute { ProductID = p.ID, NameUz = "Ishlab chiqaruvchi", NameRu = "Производитель", ValueUz = new[] { "O'zbekiston", "Turkiya", "Xitoy" }[p.ID % 3], ValueRu = new[] { "Узбекистан", "Турция", "Китай" }[p.ID % 3] });
                        db.SiteProductAttributes.Add(new SiteProductAttribute { ProductID = p.ID, NameUz = "Qadoq", NameRu = "Упаковка", ValueUz = new[] { "Qop", "Quti", "Rulon", "Dona" }[p.ID % 4], ValueRu = new[] { "Мешок", "Коробка", "Рулон", "Штука" }[p.ID % 4], SortOrder = 1 });
                    }
                    SiteMediaService.ImportProductImages(db, p.ID, null, now);
                }
                foreach (var catId in db.Categories.Where(x => x.IsDeleted != true).Select(x => x.ID).ToList())
                    SiteCatalogController.EnsureCategory(db, catId).IsPublished = true;
                if (!db.SitePages.Any(x => x.Slug == "yetkazib-berish"))
                    db.SitePages.Add(new SitePage
                    {
                        Slug = "yetkazib-berish", TitleUz = "Yetkazib berish", TitleRu = "Доставка", IsPublished = true, ShowInMenu = true, UpdatedAt = now,
                        BodyUz = "Toshkent bo'ylab yetkazib berish — buyurtmadan keyingi kun.\n\n## Viloyatlarga\n\n- Transport kompaniyalari orqali\n- 2–4 ish kuni",
                        BodyRu = "Доставка по Ташкенту — на следующий день после заказа.\n\n## В регионы\n\n- Через транспортные компании\n- 2–4 рабочих дня"
                    });
                db.SaveChanges();

                var snap = SiteSnapshotBuilder.Build(db, now);
                var json = SiteSnapshotBuilder.Serialize(snap).Json;
                File.WriteAllText(Path.Combine(output, "snapshot.json"), json);
                foreach (var m in snap.Media)
                {
                    var bytes = db.SiteMedia.Where(x => x.ID == m.Id).Select(x => x.Content).Single();
                    File.WriteAllBytes(Path.Combine(output, "orig", m.Id + "_" + m.Hash), bytes);
                }
                Console.WriteLine("Eksport: " + snap.Products.Count + " tovar, " + snap.Media.Count + " rasm -> " + output);
                Assert.IsTrue(snap.Products.Count > 0);
            }   // rollback
        }
    }
}
