using System;
using System.Web.Mvc;
using System.Web.Routing;
using tahsinERP.Services;

namespace tahsinERP.Filters
{
    /// <summary>
    /// Har bir so'rovda KOMPANIYA litsenziyasini tekshiradi (to'lov darvozasi).
    /// Natija LicenseService'da 5 daqiqa keshlanadi — har so'rovda DB'ga bormaydi.
    ///
    ///   To'liq yaroqli          — o'tkazadi.
    ///   Grace (tugaganiga ≤3 kun) — faqat o'qish: GET/HEAD o'tadi, yozish so'rovlari bloklanadi.
    ///   Aks holda               — License/Blocked sahifasiga yo'naltiradi (admin u yerdan yangi kalit kiritadi).
    ///
    /// User-level (seat) tekshiruvi login vaqtida amalga oshiriladi.
    /// </summary>
    public class LicenseCheckAttribute : ActionFilterAttribute
    {
        // Litsenziya tekshiruvidan ozod controllerlar (aks holda bloklovdan chiqib bo'lmaydi)
        private static readonly string[] ExemptControllers =
        {
            "Account", "License", "Error", "ViewNotAuthorized", "ComingSoon"
        };

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // AllowAnonymous bo'lsa — o'tkazamiz
            bool allowAnon =
                filterContext.ActionDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true) ||
                filterContext.ActionDescriptor.ControllerDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true);
            if (allowAnon)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            string controller = filterContext.RouteData.Values["controller"] as string ?? "";
            if (Array.Exists(ExemptControllers, c => string.Equals(c, controller, StringComparison.OrdinalIgnoreCase)))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            // Autentifikatsiya qilinmagan bo'lsa — Authorize filtri hal qiladi
            if (filterContext.HttpContext.User == null || !filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            // Xatoliklar LicenseService ichida logga yoziladi, foydalanuvchiga umumiy xabar qaytadi
            var status = LicenseService.GetCachedStatus();

            if (status.FullAccess)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var request = filterContext.HttpContext.Request;
            bool isReadRequest =
                string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase);

            if (status.ReadOnly && isReadRequest)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            string message = status.ReadOnly
                ? status.Message + " Ma'lumotni o'zgartirish vaqtincha mumkin emas."
                : status.Message;

            // AJAX so'rovlarga redirect emas — 403 va matnli xabar
            if (request.IsAjaxRequest())
            {
                filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
                filterContext.Result = new HttpStatusCodeResult(403, "License");
                filterContext.HttpContext.Response.AddHeader("X-License-Message", Uri.EscapeDataString(message ?? ""));
                return;
            }

            // Bloklangan sahifaga yo'naltiramiz
            filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary(new
            {
                area = "",   // Area (masalan /Distribution) ichidan ham ildizdagi sahifaga
                controller = "License",
                action = "Blocked"
            }));

            filterContext.Controller.TempData["LicenseMessage"] = message;
        }
    }
}
