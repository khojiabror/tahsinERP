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
            tahsinERP.Services.LicenseSync.LicenseSyncService.Start(); // panel URL bo'sh bo'lsa (on-prem offline) hech narsa qilmaydi
        }
        protected void Application_BeginRequest()
        {
            System.Globalization.CultureInfo culture = new System.Globalization.CultureInfo("en-US");
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
            tahsinERP.Configuration.Tenancy.TenantHttp.BeginRequest(Context); // host → tenant bazasi (reestr bo'sh bo'lsa hech narsa qilmaydi)
        }
        protected void Application_PostAuthenticateRequest()
        {
            tahsinERP.Configuration.Tenancy.TenantHttp.PostAuthenticateRequest(Context); // cookie boshqa tenant'niki bo'lsa — anonim
        }
    }
}
