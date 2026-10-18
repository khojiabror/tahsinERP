using System;
using System.Web.Mvc;
using System.Web.Routing;

namespace tahsinERP.Filters
{
    /// <summary>
    /// Vaqtinchalik parol bilan kirgan foydalanuvchi (USERS.MustChangePassword = 1) parolni almashtirmaguncha
    /// faqat Account sahifalarini ochadi — qolgan so'rovlar Account/Settings ga yo'naltiriladi.
    /// Bayroq login vaqtida session'ga yoziladi (<see cref="SessionKey"/>), har so'rovda bazaga borilmaydi;
    /// session yo'qolsa keyingi login yana talab qiladi (bayroq bazada parol almashtirilguncha turadi).
    /// </summary>
    public sealed class MustChangePasswordAttribute : ActionFilterAttribute
    {
        public const string SessionKey = "MustChangePassword";

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var http = filterContext.HttpContext;
            if (http.Session == null || !(http.Session[SessionKey] is bool must) || !must)
                return;
            if (http.User == null || !http.User.Identity.IsAuthenticated)
                return;

            string controller = filterContext.RouteData.Values["controller"] as string ?? "";
            if (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(controller, "Error", StringComparison.OrdinalIgnoreCase))
                return;

            if (http.Request.IsAjaxRequest())
            {
                http.Response.TrySkipIisCustomErrors = true;
                filterContext.Result = new HttpStatusCodeResult(403, "MustChangePassword");
                return;
            }
            filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary { { "controller", "Account" }, { "action", "Settings" } });
        }
    }
}
