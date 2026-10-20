using tahsinERP.Site.Catalog;
using tahsinERP.Site.Tenancy;

namespace tahsinERP.Site.Pages;

public sealed class IndexModel(SiteContext site) : SitePageModel(site)
{
    public List<(string Key, List<SnapshotProduct> Items)> Sections { get; } = [];

    public void OnGet()
    {
        Title = FullTitle(null);
        MetaDescription = Catalog.Settings.SeoDescription ?? Catalog.Settings.Tagline;
        CanonicalPath = "/";
        var used = new HashSet<int>();
        List<SnapshotProduct> Pick(Func<SnapshotProduct, bool> where, int take = 8)
        {
            var items = Catalog.Products.Where(p => !used.Contains(p.Id) && p.StockStatus != "out" && where(p)).Take(take).ToList();
            foreach (var p in items) used.Add(p.Id);
            return items;
        }
        var sale = Pick(p => p.Badges?.Contains("SALE") == true);
        var news = Pick(p => p.Badges?.Contains("NEW") == true);
        var hits = Pick(p => p.Badges?.Contains("HIT") == true);
        if (sale.Count > 0) Sections.Add(("OnSale", sale));
        if (news.Count > 0) Sections.Add(("NewArrivals", news));
        if (hits.Count > 0) Sections.Add(("Featured", hits));
        if (Sections.Count == 0) Sections.Add(("Featured", Pick(p => p.Media is { Count: > 0 }).Concat(Pick(_ => true)).Take(8).ToList()));
    }
}
