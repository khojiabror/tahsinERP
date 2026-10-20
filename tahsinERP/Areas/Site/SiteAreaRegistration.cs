using System.Web.Mvc;

namespace tahsinERP.Areas.Site
{
    /// <summary>
    /// Sayt-katalog admin paneli (Docs/Distribution/02_WEBSITE_CATALOG.md). URL: /Site/{controller}/{action}/{id}.
    /// Controllerlar "Site" bilan boshlanadi (PERMISSIONMODULES va EditionModules area'siz nom bilan ishlaydi).
    /// Ochiq saytning o'zi bu yerda emas — alohida tahsinERP.Site servisi (SiteApi orqali o'qiydi).
    /// </summary>
    public class SiteAreaRegistration : AreaRegistration
    {
        public override string AreaName => "Site";

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "Site_default",
                "Site/{controller}/{action}/{id}",
                new { controller = "SiteHome", action = "Index", id = UrlParameter.Optional },
                new[] { "tahsinERP.Areas.Site.Controllers", "tahsinERP.Areas.Site.Controllers.Api" });
        }
    }
}
