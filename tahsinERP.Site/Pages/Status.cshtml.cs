using Microsoft.AspNetCore.Mvc;
using tahsinERP.Site.Tenancy;

namespace tahsinERP.Site.Pages;

/// <summary>Xato sahifasi (UseStatusCodePagesWithReExecute): to'g'ridan-to'g'ri ochilsa — 404.</summary>
public sealed class StatusModel(SiteContext site) : SitePageModel(site)
{
    public int Code { get; private set; }

    public IActionResult OnGet(int code)
    {
        if (HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodeReExecuteFeature>() == null) return NotFound();
        Code = code;
        Title = FullTitle(T("NotFoundTitle"));
        NoIndex = true;
        return Page();
    }
}
