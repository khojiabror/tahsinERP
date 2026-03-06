using System.Web.Mvc;
using System.Web.Routing;

namespace tahsinERP
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "ProductionTransferLots",
                url: "ProductionToSalesTransfer/GetAvailableProductionLots",
                defaults: new { controller = "ProductionToSalesTransfer", action = "GetAvailableProductionLots" },
                namespaces: new[] { "tahsinERP.Controllers.SRM" }
            );
            routes.MapRoute(
                name: "ProductionTransferStock",
                url: "ProductionToSalesTransfer/GetProductionStock",
                defaults: new { controller = "ProductionToSalesTransfer", action = "GetProductionStock" },
                namespaces: new[] { "tahsinERP.Controllers.SRM" }
            );
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
            routes.MapRoute(
                name: "DeletePart",
                url: "PContract/DeletePart/{id}",
                defaults: new { controller = "PContract", action = "DeletePart" }
            );
            routes.MapRoute(
                name: "PurchasingPayment",
                url: "Purchasing/{controller}/{action}/{id}",
                defaults: new { controller = "PurchasingPayment", action = "Index", id = UrlParameter.Optional },
                namespaces: new[] { "tahsinERP.Controllers.Purchasing" } // Namespace qo'shish
            );

        }
    }
}
