using System.Linq;
using System.Web.Mvc;
using tahsinERP.Areas.Site.Data;
using tahsinERP.Areas.Site.Services;

namespace tahsinERP.Areas.Site.Controllers
{
    /// <summary>Sayt boshqaruvi: holat, ko'rsatkichlar, oxirgi arizalar, katalogdagi kamchiliklar.</summary>
    public class SiteHomeController : SiteControllerBase
    {
        public sealed class Dashboard
        {
            public bool Enabled { get; set; }
            public string SiteUrl { get; set; }
            public int Products { get; set; }
            public int Published { get; set; }
            public int Categories { get; set; }
            public int Pages { get; set; }
            public int Banners { get; set; }
            public int NewLeads { get; set; }
            public int NoPhoto { get; set; }
            public int NoDescription { get; set; }
            public int NoPrice { get; set; }
            public string PriceMode { get; set; }
        }

        public ActionResult Index()
        {
            using (var db = NewDb())
            {
                var settings = SiteSettingsStore.All(db);
                var priceMode = SiteSettingsStore.Get(settings, SiteSettingKeys.PriceMode, PriceModes.Ask);
                var published = db.SiteProducts.Where(p => p.IsPublished);
                int? priceType = int.TryParse(SiteSettingsStore.Get(settings, SiteSettingKeys.PriceTypeId), out var pt) ? pt
                    : db.PriceTypes.Where(t => t.IsDefault && t.IsActive).Select(t => (int?)t.ID).FirstOrDefault();
                var model = new Dashboard
                {
                    Enabled = SiteSettingsStore.Get(settings, SiteSettingKeys.Enabled) == "1",
                    SiteUrl = SiteUrl(db),
                    Products = db.Products.Count(p => !p.IsDeleted),
                    Published = published.Count(),
                    Categories = db.SiteCategories.Count(c => c.IsPublished),
                    Pages = db.SitePages.Count(p => p.IsPublished),
                    Banners = db.SiteBanners.Count(b => b.IsActive),
                    NewLeads = db.SiteLeads.Count(l => l.Status == (byte)LeadStatus.New),
                    NoPhoto = published.Count(p => !db.SiteProductMedia.Any(m => m.ProductID == p.ProductID)),
                    NoDescription = published.Count(p => p.DescriptionUz == null || p.DescriptionUz == ""),
                    NoPrice = priceMode != PriceModes.Show ? 0
                        : published.Count(p => !db.PriceListItems.Any(x => x.ProductID == p.ProductID && x.PriceTypeID == priceType)),
                    PriceMode = priceMode
                };
                ViewBag.Leads = db.SiteLeads.OrderByDescending(l => l.ID).Take(8).ToList();
                return View(model);
            }
        }
    }
}
