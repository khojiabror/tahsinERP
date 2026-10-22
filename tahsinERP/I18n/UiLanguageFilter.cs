using System.Web.Mvc;

namespace tahsinERP.I18n
{
    /// <summary>
    /// Global filtr (FilterConfig'da birinchi): har so'rovda tilni aniqlab joriy oqimga qo'yadi.
    /// Avtorizatsiya bosqichida ishlaydi — model binding va view'lardan oldin.
    /// </summary>
    public sealed class UiLanguageFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            UiLanguage.Apply(UiLanguage.Resolve(filterContext.HttpContext));
        }
    }
}
