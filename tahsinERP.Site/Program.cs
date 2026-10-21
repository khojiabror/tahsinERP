using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.Site;
using tahsinERP.Site.Catalog;
using tahsinERP.Site.Data;
using tahsinERP.Site.Leads;
using tahsinERP.Site.Media;
using tahsinERP.Site.Tenancy;
using tahsinERP.Site.Text;

var isCli = Cli.IsCommand(args);
var builder = WebApplication.CreateBuilder(isCli ? [] : args);
var config = builder.Configuration;
var services = builder.Services;

services.Configure<SiteOptions>(config.GetSection("Site"));
var connectionString = config.GetConnectionString("Site") ?? throw new InvalidOperationException("ConnectionStrings:Site sozlanmagan.");
services.AddDbContext<SiteDbContext>(o => o.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3)));
services.AddSingleton(TimeProvider.System);
services.AddSingleton<CatalogStore>();
services.AddSingleton<SiteErpClient>();
services.AddSingleton<MediaCache>();
services.AddSingleton<CatalogSyncWorker>();
services.AddScoped<SiteContext>();
services.AddScoped<LeadService>();
services.AddScoped<tahsinERP.Site.Admin.SiteTenantAdmin>();
services.AddHttpClient(SiteErpClient.HttpClientName, (sp, c) =>
        c.Timeout = TimeSpan.FromSeconds(Math.Clamp(sp.GetRequiredService<IOptions<SiteOptions>>().Value.ErpTimeoutSeconds, 5, 120)))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
if (!isCli)
{
    services.AddHostedService(sp => sp.GetRequiredService<CatalogSyncWorker>());
    services.AddHostedService<LeadWorker>();
}
services.AddOutputCache(o => o.AddPolicy("page", new TenantPagePolicy()));
services.AddRazorPages(o =>
{
    o.Conventions.AddPageRoute("/Catalog", "{lang:regex(^(uz|ru)$)}/catalog");
    o.Conventions.AddPageRoute("/Catalog", "{lang:regex(^(uz|ru)$)}/search");
});
services.Configure<ForwardedHeadersOptions>(o => o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost);
services.AddResponseCompression(o => o.EnableForHttps = true);
// kirill/o'zbek harflari HTML'da &#x..; bo'lib ketmasin (sahifa yengil, matn o'qiladigan)
services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(o =>
    o.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All));

var app = builder.Build();
if (app.Services.GetRequiredService<IOptions<SiteOptions>>().Value.MasterKey is not { Length: >= 32 })
    throw new InvalidOperationException("Site:MasterKey (Site__MasterKey) kamida 32 belgi bo'lishi kerak.");
await SchemaInitializer.EnsureAsync(connectionString);
if (isCli) return await Cli.RunAsync(app.Services, args);

app.UseForwardedHeaders();
app.UseResponseCompression();
app.UseStatusCodePagesWithReExecute("/_status/{0}");
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["X-Frame-Options"] = "SAMEORIGIN";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = c => c.Context.Response.Headers.CacheControl = "public, max-age=604800"
});
app.MapGet("/health", () => Results.Ok(new { ok = true }));
// Caddy on_demand_tls "ask": sertifikat faqat bizga ma'lum domenlarga (standart subdomen yoki ERP'da tasdiqlangan domen)
app.MapGet("/_tls/allowed", (string? domain, CatalogStore store) =>
    !string.IsNullOrWhiteSpace(domain) && store.TenantForHost(domain) != null ? Results.Ok() : Results.NotFound());
tahsinERP.Site.Admin.AdminEndpoints.Map(app);   // tenant middleware /admin/api ni o'tkazib yuboradi
app.UseMiddleware<TenantMiddleware>();
app.Use(async (ctx, next) =>
{
    var site = ctx.RequestServices.GetRequiredService<SiteContext>();
    if (site.Ready) ctx.Response.Headers.ContentSecurityPolicy = Csp(site);
    await next();
});
app.UseOutputCache();

app.MapGet("/", (SiteContext site, HttpRequest req) =>
{
    var lang = req.Headers.AcceptLanguage.ToString().StartsWith("ru", StringComparison.OrdinalIgnoreCase) && site.Catalog.SupportsLanguage("ru")
        ? "ru" : site.Catalog.DefaultLanguage;
    return Results.Redirect("/" + lang + "/");
});

// /media/{id}-{hash8}-{w}.webp
app.MapGet("/media/{file}", async (string file, SiteContext site, MediaCache media, HttpResponse res, CancellationToken ct) =>
{
    var m = Regex.Match(file, @"^(\d{1,9})-([0-9a-f]{8})-(\d{3,4})\.webp$");
    if (!m.Success) return Results.NotFound();
    var path = await media.GetAsync(site.Tenant!, site.Catalog, int.Parse(m.Groups[1].Value), m.Groups[2].Value, int.Parse(m.Groups[3].Value), ct);
    if (path == null) return Results.NotFound();
    res.Headers.CacheControl = "public, max-age=31536000, immutable";
    return Results.File(path, "image/webp");
});

app.MapGet("/robots.txt", (HttpRequest req, SiteContext site) =>
    Results.Text("User-agent: *\nDisallow: /*/cart\nDisallow: /*?*handler=\nAllow: /\n\nSitemap: " + Seo.Origin(req) + "/sitemap.xml\n", "text/plain"))
    .CacheOutput("page");

app.MapGet("/sitemap.xml", (HttpRequest req, SiteContext site) =>
{
    var origin = Seo.Origin(req);
    var c = site.Catalog;
    var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">\n");
    void Url(string path, DateTime? modified = null)
    {
        foreach (var lang in c.Languages)
        {
            sb.Append("<url><loc>").Append(System.Security.SecurityElement.Escape(origin + "/" + lang + path)).Append("</loc>");
            foreach (var alt in c.Languages)
                sb.Append("<xhtml:link rel=\"alternate\" hreflang=\"").Append(alt).Append("\" href=\"")
                  .Append(System.Security.SecurityElement.Escape(origin + "/" + alt + path)).Append("\"/>");
            if (modified.HasValue) sb.Append("<lastmod>").Append(modified.Value.ToString("yyyy-MM-dd")).Append("</lastmod>");
            sb.Append("</url>\n");
        }
    }
    Url("/");
    Url("/catalog");
    foreach (var cat in c.Categories) Url("/c/" + cat.Slug);
    foreach (var p in c.Products.Take(20000)) Url("/p/" + p.Slug, p.UpdatedAt);
    foreach (var page in c.Snapshot.Pages ?? []) Url("/page/" + page.Slug);
    sb.Append("</urlset>");
    return Results.Text(sb.ToString(), "application/xml", Encoding.UTF8);
}).CacheOutput("page");

// AI qidiruv tizimlari uchun qisqa tavsif (vercel/shop'dagi kabi)
app.MapGet("/llms.txt", (HttpRequest req, SiteContext site) =>
{
    var origin = Seo.Origin(req);
    var c = site.Catalog;
    var lang = c.DefaultLanguage;
    var sb = new StringBuilder().Append("# ").Append(site.SiteName).Append('\n');
    if (c.Settings.Tagline != null) sb.Append("\n> ").Append(c.Settings.Tagline).Append('\n');
    sb.Append("\n## ").Append(T.Get(lang, "Categories")).Append('\n');
    foreach (var cat in c.Categories) sb.Append("- [").Append(CatalogIndex.Name(cat, lang)).Append("](").Append(origin).Append('/').Append(lang).Append("/c/").Append(cat.Slug).Append(")\n");
    sb.Append("\n## ").Append(T.Get(lang, "Contacts")).Append('\n');
    foreach (var v in new[] { c.Settings.Phone, c.Settings.Email, c.Settings.Address }.Where(v => v != null)) sb.Append("- ").Append(v).Append('\n');
    return Results.Text(sb.ToString(), "text/plain", Encoding.UTF8);
}).CacheOutput("page");

app.MapRazorPages().CacheOutput("page");
app.Run();
return 0;

// Sahifa sozlamasiga qarab CSP: faqat yoqilgan analitika va Google Fonts manbalari.
static string Csp(SiteContext site)
{
    var s = site.Catalog.Settings;
    var script = "'self'";
    var connect = "'self'";
    var img = "'self' data:";
    if (!string.IsNullOrEmpty(s.AnalyticsId))
    {
        script += " https://www.googletagmanager.com";
        connect += " https://*.google-analytics.com https://*.analytics.google.com https://www.googletagmanager.com";
        img += " https://*.google-analytics.com https://www.googletagmanager.com";
    }
    if (!string.IsNullOrEmpty(s.MetrikaId))
    {
        script += " https://mc.yandex.ru https://yastatic.net";
        connect += " https://mc.yandex.ru";
        img += " https://mc.yandex.ru";
    }
    return $"default-src 'self'; script-src {script}; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; " +
           $"img-src {img}; connect-src {connect}; frame-ancestors 'self'; base-uri 'self'; form-action 'self'";
}
