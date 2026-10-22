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
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters); // HandleError, Authorize, LicenseCheck, MustChangePassword, EditionCheck
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            ControllerBuilder.Current.SetControllerFactory(new TahsinControllerFactory()); // ReadOnly sessiya + TempData keshda (CORE-P05)
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            // Ko'p tillilik: [Display]/validatsiya xabarlari resx kaliti bo'lsa joriy tilda (I18n/LocalizedModelProviders.cs);
            // model binder va raqam/sana xabarlari — App_GlobalResources/MvcMessages*.resx
            ModelMetadataProviders.Current = new tahsinERP.I18n.LocalizedMetadataProvider();
            var dataAnnotations = System.Linq.Enumerable.FirstOrDefault(System.Linq.Enumerable.OfType<DataAnnotationsModelValidatorProvider>(ModelValidatorProviders.Providers));
            if (dataAnnotations != null) ModelValidatorProviders.Providers.Remove(dataAnnotations);
            ModelValidatorProviders.Providers.Add(new tahsinERP.I18n.LocalizedValidatorProvider());
            ClientDataTypeModelValidatorProvider.ResourceClassKey = "MvcMessages";
            DefaultModelBinder.ResourceClassKey = "MvcMessages";
            tahsinERP.Services.LicenseSync.LicenseSyncService.Start(); // panel URL bo'sh bo'lsa (on-prem offline) hech narsa qilmaydi
        }

        // Har so'rovda yangi CultureInfo yaratilmaydi — keshlangan, faqat o'qiladigan nusxa
        private static readonly System.Globalization.CultureInfo AppCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");

        protected void Application_BeginRequest()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = AppCulture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = AppCulture;
            tahsinERP.Configuration.Tenancy.TenantHttp.BeginRequest(Context); // host → tenant bazasi (reestr bo'sh bo'lsa hech narsa qilmaydi)
        }
        // MVC action'dan tashqaridagi xatolar (controller/action topilmadi, 400, route xatosi) uchun xato sahifasi.
        // Action ichidagi xatolarni global HandleErrorAttribute ushlaydi.
        protected void Application_Error()
        {
            var ex = Server.GetLastError();
            int code = (ex as System.Web.HttpException)?.GetHttpCode() ?? 500;
            if (code != 404 && code != 400)
            {
                code = 500;
                System.Diagnostics.Trace.TraceError("Unhandled: " + ex);
            }

            Server.ClearError();
            Response.Clear();
            Response.StatusCode = code;
            Response.TrySkipIisCustomErrors = true;

            var routeData = new RouteData();
            routeData.Values["controller"] = "Error";
            routeData.Values["action"] = code == 404 ? "NotFound" : "Error";
            try
            {
                IController controller = new ErrorController();
                controller.Execute(new RequestContext(new System.Web.HttpContextWrapper(Context), routeData));
            }
            catch
            {
                // Xato sahifasining o'zi ishlamasa — oddiy matn (cheksiz aylanishsiz)
                Response.Clear();
                Response.ContentType = "text/plain";
                Response.Write(code == 404 ? tahsinERP.I18n.L.T("Error_NotFound") : tahsinERP.I18n.L.T("Error_Generic"));
            }
        }

        protected void Application_PostAuthenticateRequest()
        {
            tahsinERP.Configuration.Tenancy.TenantHttp.PostAuthenticateRequest(Context); // cookie boshqa tenant'niki bo'lsa — anonim
        }
    }
}
