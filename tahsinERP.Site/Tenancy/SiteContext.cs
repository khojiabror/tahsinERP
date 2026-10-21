using Microsoft.Extensions.Options;
using tahsinERP.Site.Catalog;
using tahsinERP.Site.Data;

namespace tahsinERP.Site.Tenancy;

/// <summary>Joriy so'rovning tenanti, katalog indeksi va tili (Razor sahifalarida @inject SiteContext).</summary>
public sealed class SiteContext
{
    public SiteTenant? Tenant { get; set; }
    public CatalogIndex? Index { get; set; }
    public string Lang { get; set; } = "uz";
    public string Host { get; set; } = "";

    public bool Ready => Tenant != null && Index != null;
    public CatalogIndex Catalog => Index ?? throw new InvalidOperationException("Katalog yo'q");
    public string T(string key, params object?[] args) => Text.T.Get(Lang, key, args);
    public string OtherLang => Catalog.Languages.FirstOrDefault(l => l != Lang) ?? Lang;
    public bool ShowPrices => Index?.Settings.PriceMode == "SHOW";
    public string SiteName => Index?.Settings.Name ?? Tenant?.Slug ?? "";

    public string Url(string path) => "/" + Lang + (path.StartsWith('/') ? path : "/" + path);
}

/// <summary>
/// Host → tenant. Topilmasa 404. Development'da (AllowTenantQuery) localhost'da ?tenant=slug yoki cookie bilan.
/// </summary>
public sealed class TenantMiddleware(RequestDelegate next, CatalogStore store, IOptions<SiteOptions> options)
{
    public const string DevCookie = "dev-tenant";

    public async Task InvokeAsync(HttpContext http, SiteContext site)
    {
        var path = http.Request.Path;
        if (path.StartsWithSegments("/health") || path.StartsWithSegments("/_tls") || path.StartsWithSegments("/css") || path.StartsWithSegments("/js") || path.StartsWithSegments("/favicon.ico")
            || Admin.AdminEndpoints.IsAdminPath(path))
        {
            await next(http);
            return;
        }
        var host = http.Request.Host.Host;
        string? slug = store.TenantForHost(host);
        if (slug == null && options.Value.AllowTenantQuery && (host == "localhost" || host == "127.0.0.1"))
        {
            slug = http.Request.Query["tenant"].FirstOrDefault();
            if (!string.IsNullOrEmpty(slug)) http.Response.Cookies.Append(DevCookie, slug, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
            else slug = http.Request.Cookies[DevCookie];
        }
        var tenant = slug == null ? null : store.Tenants.FirstOrDefault(t => string.Equals(t.Slug, slug, StringComparison.OrdinalIgnoreCase));
        if (tenant == null)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            http.Response.ContentType = "text/plain; charset=utf-8";
            await http.Response.WriteAsync("Sayt topilmadi / Сайт не найден");
            return;
        }
        site.Tenant = tenant;
        site.Index = store.Get(tenant.Slug);
        site.Host = host;
        if (site.Index == null)
        {
            http.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            http.Response.Headers.RetryAfter = "60";
            http.Response.ContentType = "text/plain; charset=utf-8";
            await http.Response.WriteAsync("Katalog yuklanmoqda / Каталог загружается");
            return;
        }
        var first = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        site.Lang = first is "uz" or "ru" && site.Index.SupportsLanguage(first) ? first : site.Index.DefaultLanguage;
        await next(http);
    }
}
