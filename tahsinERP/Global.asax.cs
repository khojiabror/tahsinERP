using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using tahsinERP.Controllers;

namespace tahsinERP
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters); // Global filtrlarga LogActionFilter ni qo'shish
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            GlobalFilters.Filters.Add(new AuthorizeAttribute());
        }
        protected void Application_BeginRequest()
        {
            System.Globalization.CultureInfo culture = new System.Globalization.CultureInfo("en-US");
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
        }

        protected void Application_Error()
        {
            var exception = Server.GetLastError();
            if (exception == null)
            {
                return;
            }

            var httpException = exception as HttpException;
            var statusCode = httpException?.GetHttpCode() ?? 500;

            Server.ClearError();
            Response.TrySkipIisCustomErrors = true;

            if (statusCode == 404)
            {
                Response.Redirect("~/Error/NotFound");
                return;
            }

            Response.Redirect("~/Error/Error");
        }

    }
}
