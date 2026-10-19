using System;
using System.Web.Mvc;
using System.Web.Routing;
using tahsinERP.Services.Editions;

namespace tahsinERP.Filters
{
    /// <summary>
    /// Paketga (Edition) kirmagan modul controllerlarini "Paketingizda yo'q — tarifni oshiring"
    /// sahifasiga (License/NotInPlan) yo'naltiradi. AJAX — 403 + X-Edition-Message.
    /// Xarita: <see cref="EditionModules"/>. Edition bo'sh / v1 kalit — hech narsa yopilmaydi.
    /// Menyu bandlari RoleHelper.IsViewPermitted ichidagi EditionGate tekshiruvi bilan yashiriladi.
    /// </summary>
    public class EditionCheckAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var user = filterContext.HttpContext.User;
            if (user == null || !user.Identity.IsAuthenticated)
                return; // Authorize filtri hal qiladi

            string controller = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            if (EditionGate.IsControllerAllowed(controller))
                return;

            var module = EditionModules.ModuleOf(controller);
            string message = "\"" + EditionModules.DisplayName(module) + "\" moduli paketingizda yo'q — tarifni " +
                             EditionModules.EditionDisplayName(EditionModules.CheapestEditionWith(module)) + " paketiga oshiring.";

            if (filterContext.HttpContext.Request.IsAjaxRequest())
            {
                filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
                filterContext.HttpContext.Response.AddHeader("X-Edition-Message", Uri.EscapeDataString(message));
                filterContext.Result = new HttpStatusCodeResult(403, "Edition");
                return;
            }

            filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary(new
            {
                area = "",   // Area (masalan /Distribution) ichidan ham ildizdagi sahifaga
                controller = "License",
                action = "NotInPlan",
                module = module.ToString()
            }));
        }
    }
}
