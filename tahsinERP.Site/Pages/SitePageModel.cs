using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using tahsinERP.Site.Catalog;
using tahsinERP.Site.Tenancy;

namespace tahsinERP.Site.Pages;

/// <summary>
/// Barcha ochiq sahifalar uchun asos: til marshrutdan (sayt tillaridan biri bo'lmasa — 404),
/// sayt o'chiq bo'lsa — "tez orada" (503, Retry-After), sahifa sarlavhasi va meta ma'lumotlari.
/// </summary>
public abstract class SitePageModel(SiteContext site) : PageModel
{
    public SiteContext Site { get; } = site;
    public CatalogIndex Catalog => Site.Catalog;
    public string Lang => Site.Lang;

    public string Title { get; protected set; } = "";
    public string? MetaDescription { get; protected set; }
    public string? CanonicalPath { get; protected set; }   // tilsiz yo'l: "/p/sement" — hreflang uchun
    public string? OgImage { get; protected set; }
    public bool NoIndex { get; protected set; }

    public string T(string key, params object?[] args) => Site.T(key, args);

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var lang = RouteData.Values["lang"] as string;
        if (lang != null && !Catalog.SupportsLanguage(lang))
        {
            context.Result = NotFound();
            return;
        }
        if (!Catalog.Settings.Enabled)
        {
            Response.Headers.RetryAfter = "3600";
            context.Result = new PartialViewResult { ViewName = "_Soon", StatusCode = StatusCodes.Status503ServiceUnavailable, ViewData = ViewData };
            return;
        }
        await next();
    }

    protected string FullTitle(string? title) =>
        string.IsNullOrEmpty(title) ? (Catalog.Settings.SeoTitle ?? Site.SiteName) : title + " — " + Site.SiteName;
}
