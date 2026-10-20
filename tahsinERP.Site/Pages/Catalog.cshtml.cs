using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using tahsinERP.Site.Catalog;
using tahsinERP.Site.Tenancy;
using tahsinERP.Site.Text;

namespace tahsinERP.Site.Pages;

/// <summary>
/// Katalog: /{lang}/catalog (hammasi), /{lang}/c/{slug} (kategoriya), /{lang}/search?q= (qidiruv).
/// Filtrlar oddiy GET parametrlari (b=brend, a.{xususiyat}=qiymat, stock=1, sort, page, view) — har holat o'z URL'iga ega.
/// X-Partial sarlavhasi bilan — faqat natijalar bloki (JS sahifani qayta yuklamasdan almashtiradi).
/// </summary>
public sealed class CatalogModel(SiteContext site) : SitePageModel(site)
{
    public SnapshotCategory? Category { get; private set; }
    public string? Query { get; private set; }
    public CatalogIndex.Result Result { get; private set; } = null!;
    public string Sort { get; private set; } = "default";
    public string View { get; private set; } = "grid";
    public bool InStock { get; private set; }
    public bool IsSearch { get; private set; }
    public string BasePath { get; private set; } = "/catalog";

    public static readonly string[] Sorts = ["default", "price-asc", "price-desc", "name", "new"];

    public IActionResult OnGet(string? slug)
    {
        IsSearch = Request.Path.Value?.EndsWith("/search", StringComparison.Ordinal) == true;
        if (slug != null)
        {
            Category = Catalog.CategoryBySlug(slug);
            if (Category == null) return NotFound();
            BasePath = "/c/" + Category.Slug;
        }
        else if (IsSearch) BasePath = "/search";

        Query = Request.Query["q"].FirstOrDefault()?.Trim();
        if (Query?.Length > 100) Query = Query[..100];
        Sort = Sorts.Contains(Request.Query["sort"].FirstOrDefault()) ? Request.Query["sort"].First()! : "default";
        if (Sort.StartsWith("price") && !Site.ShowPrices) Sort = "default";
        View = Request.Query["view"].FirstOrDefault() == "list" ? "list" : "grid";
        InStock = Request.Query["stock"].FirstOrDefault() == "1";
        int page = int.TryParse(Request.Query["page"].FirstOrDefault(), out var pg) ? pg : 1;
        var brands = Request.Query["b"].Select(v => int.TryParse(v, out var b) ? b : (int?)null).Where(b => b.HasValue).Select(b => b!.Value).ToList();
        var attrs = Request.Query.Where(kv => kv.Key.StartsWith("a.", StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key[2..], kv => (IReadOnlyCollection<string>)kv.Value.Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).ToList());

        Result = Catalog.Search(new CatalogIndex.Query(Query, Category?.Id, brands, attrs, InStock, Sort, page, View == "list" ? 48 : 24), Lang);

        if (Category != null)
        {
            var name = CatalogIndex.Name(Category, Lang);
            Title = FullTitle(Category.SeoTitle ?? name);
            var plain = SafeText.Plain(CatalogIndex.Description(Category, Lang));
            MetaDescription = Category.SeoDescription ?? (plain.Length > 0 ? plain : null);
            if (Category.MediaId is int mid && Catalog.Media.TryGetValue(mid, out var m)) OgImage = Media.MediaCache.Url(m, 1200);
        }
        else Title = FullTitle(IsSearch ? (string.IsNullOrEmpty(Query) ? T("Search") : T("SearchResults", Query)) : T("AllProducts"));

        // filtrlangan, saralangan va qidiruv holatlari indekslanmaydi (dublikat sahifalar); sahifalash — kanonik ?page=
        bool filtered = brands.Count > 0 || attrs.Count > 0 || InStock || Sort != "default" || View != "grid";
        NoIndex = IsSearch || filtered;
        CanonicalPath = IsSearch ? null : BasePath + (Result.Page > 1 ? "?page=" + Result.Page.ToString(CultureInfo.InvariantCulture) : "");

        if (Request.Headers["X-Partial"] == "1") return Partial("_Results", this);
        return Page();
    }

    /// <summary>Joriy so'rov parametrlarini o'zgartirib URL quradi (filtr va sahifalash havolalari).</summary>
    public string Link(Action<Dictionary<string, List<string>>> change, bool resetPage = true)
    {
        var q = Request.Query.ToDictionary(kv => kv.Key, kv => kv.Value.Where(v => v != null).Select(v => v!).ToList());
        if (resetPage) q.Remove("page");
        change(q);
        var qs = string.Join("&", q.Where(kv => kv.Value.Count > 0).SelectMany(kv => kv.Value.Select(v => Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(v))));
        return Site.Url(BasePath) + (qs.Length > 0 ? "?" + qs : "");
    }

    public string Toggle(string key, string value) => Link(q =>
    {
        if (!q.TryGetValue(key, out var list)) q[key] = list = [];
        if (!list.Remove(value)) list.Add(value);
    });

    public string PageLink(int page) => Link(q => { if (page > 1) q["page"] = [page.ToString(CultureInfo.InvariantCulture)]; else q.Remove("page"); }, resetPage: false);
}
