using Microsoft.AspNetCore.Mvc;
using tahsinERP.Site.Catalog;
using tahsinERP.Site.Tenancy;
using tahsinERP.Site.Text;

namespace tahsinERP.Site.Pages;

/// <summary>Matnli sahifa: /{lang}/page/{slug} (Biz haqimizda, Yetkazib berish...).</summary>
public sealed class InfoModel(SiteContext site) : SitePageModel(site)
{
    public SnapshotPage PageContent { get; private set; } = null!;
    public string Heading { get; private set; } = "";

    public IActionResult OnGet(string slug)
    {
        var page = Catalog.Snapshot.Pages?.FirstOrDefault(p => p.Slug == slug);
        if (page == null) return NotFound();
        PageContent = page;
        Heading = Lang == "ru" ? page.TitleRu ?? page.TitleUz : page.TitleUz;
        Title = FullTitle(Heading);
        var plain = SafeText.Plain(Lang == "ru" ? page.BodyRu ?? page.BodyUz : page.BodyUz);
        MetaDescription = plain.Length > 0 ? plain : null;
        CanonicalPath = "/page/" + page.Slug;
        return Page();
    }
}
