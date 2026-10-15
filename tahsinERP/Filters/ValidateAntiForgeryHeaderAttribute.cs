using System;
using System.Web.Helpers;
using System.Web.Mvc;

namespace tahsinERP.Filters
{
    /// <summary>
    /// CSRF tekshiruvi: standart [ValidateAntiForgeryToken] kabi, lekin token'ni
    /// "RequestVerificationToken" HTTP header'idan ham qabul qiladi.
    ///
    /// Header bor bo'lsa — cookie + header juftligi tekshiriladi (AJAX, JSON body);
    /// bo'lmasa — oddiy forma maydoni (__RequestVerificationToken) tekshiriladi.
    /// Header'ni Scripts/csrf.js barcha jQuery POST so'rovlariga avtomatik qo'shadi.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ValidateAntiForgeryHeaderAttribute : FilterAttribute, IAuthorizationFilter
    {
        public const string HeaderName = "RequestVerificationToken";

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext == null) throw new ArgumentNullException(nameof(filterContext));

            var request = filterContext.HttpContext.Request;
            string headerToken = request.Headers[HeaderName];
            if (!string.IsNullOrEmpty(headerToken))
            {
                var cookie = request.Cookies[AntiForgeryConfig.CookieName];
                AntiForgery.Validate(cookie != null ? cookie.Value : null, headerToken);
            }
            else
            {
                AntiForgery.Validate();
            }
        }
    }
}
