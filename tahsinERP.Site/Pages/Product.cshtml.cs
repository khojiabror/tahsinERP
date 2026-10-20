using Microsoft.AspNetCore.Mvc;
using tahsinERP.Site.Catalog;
using tahsinERP.Site.Media;
using tahsinERP.Site.Tenancy;
using tahsinERP.Site.Text;

namespace tahsinERP.Site.Pages;

public sealed class ProductModel(SiteContext site) : SitePageModel(site)
{
    public SnapshotProduct Product { get; private set; } = null!;
    public SnapshotCategory? Category { get; private set; }
    public List<SnapshotProduct> Related { get; private set; } = [];

    public IActionResult OnGet(string slug)
    {
        var p = Catalog.ProductBySlug(slug);
        if (p == null) return NotFound();
        Product = p;
        Category = Catalog.CategoryById(p.CategoryId);
        Related = Catalog.Related(p);
        var name = CatalogIndex.Name(p, Lang);
        Title = FullTitle(p.SeoTitle ?? name);
        var plain = SafeText.Plain(CatalogIndex.Description(p, Lang));
        MetaDescription = p.SeoDescription ?? (plain.Length > 0 ? plain : name + " — " + p.Sku);
        CanonicalPath = "/p/" + p.Slug;
        if (p.Media?.FirstOrDefault() is { } first && Catalog.Media.TryGetValue(first.Id, out var m)) OgImage = MediaCache.Url(m, 1200);
        return Page();
    }
}
