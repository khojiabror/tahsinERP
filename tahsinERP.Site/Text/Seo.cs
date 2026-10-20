using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.OutputCaching;
using tahsinERP.Site.Catalog;
using tahsinERP.Site.Media;
using tahsinERP.Site.Tenancy;

namespace tahsinERP.Site.Text;

/// <summary>schema.org JSON-LD va kanonik manzillar.</summary>
public static class Seo
{
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = false };

    /// <summary>&lt;script type="application/ld+json"&gt; — "&lt;/" xavfsiz kodlanadi.</summary>
    public static IHtmlContent Script(JsonNode node) =>
        new HtmlString("<script type=\"application/ld+json\">" + node.ToJsonString(Opts).Replace("</", "<\\/") + "</script>");

    public static string Origin(HttpRequest req) => req.Scheme + "://" + req.Host.Value;

    public static JsonNode Organization(SiteContext s, string origin)
    {
        var settings = s.Catalog.Settings;
        var org = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Organization",
            ["name"] = s.SiteName,
            ["url"] = origin + "/"
        };
        if (settings.LogoMediaId is int logo && s.Catalog.Media.TryGetValue(logo, out var m)) org["logo"] = origin + MediaCache.Url(m, 400);
        if (settings.Phone != null) org["telephone"] = settings.Phone;
        if (settings.Email != null) org["email"] = settings.Email;
        if (settings.Address != null) org["address"] = settings.Address;
        var same = new JsonArray();
        foreach (var u in new[] { Social.Telegram(settings.Telegram), Social.Instagram(settings.Instagram), Social.Facebook(settings.Facebook) })
            if (u != null) same.Add(u);
        if (same.Count > 0) org["sameAs"] = same;
        return org;
    }

    public static JsonNode WebSite(SiteContext s, string origin) => new JsonObject
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "WebSite",
        ["name"] = s.SiteName,
        ["url"] = origin + "/" + s.Lang + "/",
        ["potentialAction"] = new JsonObject
        {
            ["@type"] = "SearchAction",
            ["target"] = origin + "/" + s.Lang + "/search?q={search_term_string}",
            ["query-input"] = "required name=search_term_string"
        }
    };

    public static JsonNode Product(SiteContext s, SnapshotProduct p, string origin)
    {
        var node = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = CatalogIndex.Name(p, s.Lang),
            ["sku"] = p.Sku,
            ["url"] = origin + s.Url("/p/" + p.Slug)
        };
        var desc = SafeText.Plain(CatalogIndex.Description(p, s.Lang), 500);
        if (desc.Length > 0) node["description"] = desc;
        var images = new JsonArray();
        foreach (var pm in p.Media ?? [])
            if (s.Catalog.Media.TryGetValue(pm.Id, out var m)) images.Add(origin + MediaCache.Url(m, 1200));
        if (images.Count > 0) node["image"] = images;
        if (p.BrandId is int b && s.Catalog.Brands.TryGetValue(b, out var brand)) node["brand"] = new JsonObject { ["@type"] = "Brand", ["name"] = brand.Name };
        var gtin = p.Barcodes?.FirstOrDefault(x => x.Length is 8 or 12 or 13 or 14 && x.All(char.IsDigit));
        if (gtin != null) node["gtin"] = gtin;
        if (s.ShowPrices && p.Price.HasValue)
        {
            node["offers"] = new JsonObject
            {
                ["@type"] = "Offer",
                ["price"] = p.Price.Value,
                ["priceCurrency"] = "UZS",
                ["availability"] = p.StockStatus == "out" ? "https://schema.org/BackOrder" : "https://schema.org/InStock",
                ["url"] = origin + s.Url("/p/" + p.Slug)
            };
        }
        return node;
    }

    public static JsonNode Breadcrumbs(IEnumerable<(string Name, string Url)> items, string origin)
    {
        var list = new JsonArray();
        int i = 1;
        foreach (var (name, url) in items)
            list.Add(new JsonObject { ["@type"] = "ListItem", ["position"] = i++, ["name"] = name, ["item"] = origin + url });
        return new JsonObject { ["@context"] = "https://schema.org", ["@type"] = "BreadcrumbList", ["itemListElement"] = list };
    }
}

public static class Social
{
    private static string? Handle(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().TrimStart('@');

    public static string? Telegram(string? v) => Link(v, "https://t.me/");
    public static string? Instagram(string? v) => Link(v, "https://instagram.com/");
    public static string? Facebook(string? v) => Link(v, "https://facebook.com/");

    private static string? Link(string? v, string prefix)
    {
        var h = Handle(v);
        if (h == null) return null;
        if (h.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return h;
        return h.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-') ? prefix + h : null;
    }
}

/// <summary>
/// Sahifa keshi: GET, tenant bo'yicha teg (yangi nusxa kelganda tozalanadi), host + yo'l + so'rov bo'yicha, 60 soniya.
/// Savat va POST keshlanmaydi.
/// </summary>
public sealed class TenantPagePolicy : IOutputCachePolicy
{
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken ct)
    {
        var http = context.HttpContext;
        var site = http.RequestServices.GetRequiredService<SiteContext>();
        bool cacheable = HttpMethods.IsGet(http.Request.Method) && site.Ready && !http.Request.Path.Value!.EndsWith("/cart", StringComparison.Ordinal)
                         && !http.Request.Query.ContainsKey("handler");
        context.EnableOutputCaching = true;
        context.AllowCacheLookup = cacheable;
        context.AllowCacheStorage = cacheable;
        context.AllowLocking = true;
        context.ResponseExpirationTimeSpan = TimeSpan.FromSeconds(60);
        context.CacheVaryByRules.VaryByHost = true;
        context.CacheVaryByRules.QueryKeys = "*";
        context.CacheVaryByRules.HeaderNames = "X-Partial";
        if (site.Tenant != null) context.Tags.Add("tenant:" + site.Tenant.Slug);
        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken ct) => ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken ct)
    {
        if (context.HttpContext.Response.StatusCode != StatusCodes.Status200OK) context.AllowCacheStorage = false;
        return ValueTask.CompletedTask;
    }
}
