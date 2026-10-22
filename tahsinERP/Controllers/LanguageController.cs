using System;
using System.Web.Mvc;
using tahsinERP.I18n;
using tahsinERP.Localization;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Interfeys tilini almashtirish (sarlavhadagi tanlagich, login sahifasi). Tanlov cookie'ga va —
    /// kirgan foydalanuvchi uchun — dst.USER_SETTINGS'ga yoziladi (boshqa qurilmada va botda ham shu til).
    /// GET: faqat o'z tilini o'zgartiradi, boshqa holatga ta'sir qilmaydi; qaytish manzili faqat lokal.
    /// </summary>
    [AllowAnonymous]
    public class LanguageController : Controller
    {
        public ActionResult Set(string lang, string returnUrl)
        {
            var language = Languages.TryNormalize(lang);
            if (language != null)
            {
                var cookie = UiLanguage.Cookie(language);
                cookie.Secure = Request.IsSecureConnection;
                Response.Cookies.Add(cookie);

                if (Request.IsAuthenticated)
                {
                    try
                    {
                        var userId = RoleHelper.GetUserId(User.Identity.Name);
                        if (userId.HasValue)
                            UiLanguage.SaveForUser(userId.Value, language);
                    }
                    catch (Exception ex)
                    {
                        // cookie baribir ishlaydi; saqlanmagani logga
                        System.Diagnostics.Trace.TraceError("Language/Set: " + ex);
                    }
                }
            }
            return Redirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/"));
        }
    }
}
