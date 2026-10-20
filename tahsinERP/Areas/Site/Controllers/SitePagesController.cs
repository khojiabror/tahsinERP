using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Areas.Site.Data;
using tahsinERP.Areas.Site.Services;
using tahsinERP.Filters;

namespace tahsinERP.Areas.Site.Controllers
{
    /// <summary>Matnli sahifalar (Biz haqimizda, Yetkazib berish...) va bosh sahifa bannerlari.</summary>
    public class SitePagesController : SiteControllerBase
    {
        public ActionResult Index()
        {
            using (var db = NewDb())
            {
                ViewBag.Pages = db.SitePages.OrderBy(p => p.SortOrder).ThenBy(p => p.TitleUz).ToList();
                ViewBag.Banners = db.SiteBanners.OrderBy(b => b.SortOrder).ThenBy(b => b.ID).ToList();
                return View();
            }
        }

        public ActionResult Edit(int? id)
        {
            using (var db = NewDb())
            {
                SitePage page = id.HasValue ? db.SitePages.Find(id.Value) : new SitePage { IsPublished = true, ShowInMenu = true };
                if (page == null) return HttpNotFound();
                return View(page);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        [ValidateInput(false)]   // matn saytda HTML sifatida emas, xavfsiz formatlangan matn sifatida chiqadi
        public JsonResult SavePage(int? id, string slug, string titleUz, string titleRu, string bodyUz, string bodyRu,
            bool isPublished, bool showInMenu, int sortOrder)
        {
            return DstJson(() =>
            {
                var title = SiteText.Clean(titleUz, 150) ?? throw new DstValidationException(DstLocalization.T("Error_NameRequired"));
                using (var db = NewDb())
                {
                    var page = id.HasValue ? db.SitePages.Find(id.Value) ?? throw NotFound() : db.SitePages.Add(new SitePage());
                    var newSlug = NullIfEmpty(slug)?.ToLowerInvariant() ?? SiteText.Unique(SiteText.Slugify(title), s => db.SitePages.Any(p => p.Slug == s), "page");
                    if (!SiteText.IsValidSlug(newSlug)) throw new DstValidationException(DstLocalization.T("Site_SlugFormat"));
                    if (db.SitePages.Any(p => p.Slug == newSlug && p.ID != page.ID)) throw new DstValidationException(DstLocalization.T("Site_SlugTaken"));
                    page.Slug = newSlug;
                    page.TitleUz = title;
                    page.TitleRu = SiteText.Clean(titleRu, 150);
                    page.BodyUz = SiteText.Clean(bodyUz, 50000);
                    page.BodyRu = SiteText.Clean(bodyRu, 50000);
                    page.IsPublished = isPublished;
                    page.ShowInMenu = showInMenu;
                    page.SortOrder = sortOrder;
                    page.UpdatedAt = Now;
                    db.SaveChanges();
                    Published();
                    return new { id = page.ID };
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult DeletePage(int id)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    db.SitePages.Remove(db.SitePages.Find(id) ?? throw NotFound());
                    db.SaveChanges();
                    Published();
                    return null;
                }
            });
        }

        // ---------- bannerlar ----------

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult SaveBanner(int? id, HttpPostedFileBase file, string titleUz, string titleRu, string subtitleUz, string subtitleRu,
            string link, DateTime? activeFrom, DateTime? activeTo, bool isActive, int sortOrder)
        {
            return DstJson(() =>
            {
                if (activeFrom.HasValue && activeTo.HasValue && activeTo < activeFrom) throw new DstValidationException(DstLocalization.T("Error_NumberFormat"));
                var url = NullIfEmpty(link);
                if (url != null && !(url.StartsWith("/") && !url.StartsWith("//") || url.StartsWith("https://") || url.StartsWith("http://")))
                    throw new DstValidationException(DstLocalization.T("Site_LinkFormat"));
                using (var db = NewDb())
                {
                    var banner = id.HasValue ? db.SiteBanners.Find(id.Value) ?? throw NotFound() : null;
                    int? oldMedia = banner?.MediaID;
                    if (file != null && file.ContentLength > 0)
                    {
                        var media = SiteMediaService.Save(db, ReadUpload(file), CurrentUserId, Now);
                        if (banner == null) banner = db.SiteBanners.Add(new SiteBanner { MediaID = media.ID });
                        else banner.MediaID = media.ID;
                    }
                    if (banner == null) throw new DstValidationException(DstLocalization.T("Site_ImageRequired"));
                    banner.TitleUz = SiteText.Clean(titleUz, 150);
                    banner.TitleRu = SiteText.Clean(titleRu, 150);
                    banner.SubtitleUz = SiteText.Clean(subtitleUz, 300);
                    banner.SubtitleRu = SiteText.Clean(subtitleRu, 300);
                    banner.Link = SiteText.Clean(url, 300);
                    banner.ActiveFrom = activeFrom?.Date;
                    banner.ActiveTo = activeTo?.Date;
                    banner.IsActive = isActive;
                    banner.SortOrder = sortOrder;
                    banner.UpdatedAt = Now;
                    db.SaveChanges();
                    if (oldMedia.HasValue && oldMedia != banner.MediaID) { SiteMediaService.DeleteIfUnused(db, oldMedia.Value); db.SaveChanges(); }
                    Published();
                    return new { id = banner.ID };
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryHeader]
        public JsonResult DeleteBanner(int id)
        {
            return DstJson(() =>
            {
                using (var db = NewDb())
                {
                    var b = db.SiteBanners.Find(id) ?? throw NotFound();
                    int media = b.MediaID;
                    db.SiteBanners.Remove(b);
                    db.SaveChanges();
                    SiteMediaService.DeleteIfUnused(db, media);
                    db.SaveChanges();
                    Published();
                    return null;
                }
            });
        }
    }
}
