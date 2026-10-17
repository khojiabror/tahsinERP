using System;
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
            // X-Frame-Options web.config'dagi customHeaders orqali beriladi (dublikat bo'lmasin)
            System.Web.Helpers.AntiForgeryConfig.SuppressXFrameOptionsHeader = true;
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters); // Global filtrlarga LogActionFilter ni qo'shish
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            GlobalFilters.Filters.Add(new AuthorizeAttribute());
            GlobalFilters.Filters.Add(new tahsinERP.Filters.LicenseCheckAttribute()); // Litsenziya (company muddat) tekshiruvi
        }

        // Har so'rovda yangi CultureInfo yaratilmaydi — keshlangan, faqat o'qiladigan nusxa
        private static readonly System.Globalization.CultureInfo AppCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");

        protected void Application_BeginRequest()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = AppCulture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = AppCulture;
        }
    }
}
