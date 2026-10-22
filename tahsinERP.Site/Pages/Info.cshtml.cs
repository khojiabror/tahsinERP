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
        Heading = Text.T.Pick(Lang, page.TitleUz, page.TitleRu)!;
        Title = FullTitle(Heading);
        var plain = SafeText.Plain(Text.T.Pick(Lang, page.BodyUz, page.BodyRu));
        MetaDescription = plain.Length > 0 ? plain : null;
        CanonicalPath = "/page/" + page.Slug;
        return Page();
    }
}
