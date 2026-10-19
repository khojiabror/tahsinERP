using System.Web.Mvc;

namespace tahsinERP.Areas.Distribution
{
    /// <summary>
    /// Distributsiya moduli (Docs/Distribution/00_PLAN.md). URL: /Distribution/{controller}/{action}/{id}.
    /// Controller nomlari "Dst" bilan boshlanadi — PERMISSIONMODULES.Controller va EditionModules
    /// xaritasi area'siz controller nomi bo'yicha ishlaydi, mavjud controllerlar bilan to'qnashmasin.
    /// </summary>
    public class DistributionAreaRegistration : AreaRegistration
    {
        public override string AreaName => "Distribution";

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "Distribution_default",
                "Distribution/{controller}/{action}/{id}",
                new { controller = "DstHome", action = "Index", id = UrlParameter.Optional },
                new[] { "tahsinERP.Areas.Distribution.Controllers", "tahsinERP.Areas.Distribution.Controllers.Api" });
        }
    }
}
