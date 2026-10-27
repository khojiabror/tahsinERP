using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Foydalanuvchi qo'llanmasi — Word qo'llanmalardan yaratilgan modul sahifalari (UserManualProvider)
    /// </summary>
    public class UserManualController : Controller
    {
        // Olish so'rovi: UserManual?m=03-muhandislik-tannarx
        public ActionResult Index(string m)
        {
            // Tekshirish agar user is authenticated
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            // Tekshirish basic view permission (everyone bilan system access can view manual)
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Layout"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var lang = UserManualProvider.ManualLanguage(I18n.L.Lang);
            var modules = UserManualProvider.GetModules(lang);
            var current = modules.FirstOrDefault(x => x.Id == m) ?? modules.FirstOrDefault();
            return View(new UserManualPageViewModel
            {
                Modules = modules,
                Current = current,
                Language = lang,
                ContentHtml = current == null ? null
                    : UserManualProvider.GetContent(lang, current.Id, Url.Action("Img", new { id = "" }) + "/")
            });
        }

        // Olish so'rovi: UserManual/Img/<xesh> — rasm nomi kontent xeshi, shuning uchun brauzer uni muddatsiz keshlaydi
        [HttpGet]
        public ActionResult Img(string id)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return new HttpStatusCodeResult(401);
            }

            var path = UserManualProvider.ImagePath(id);
            if (path == null)
            {
                return HttpNotFound();
            }

            Response.Cache.SetCacheability(HttpCacheability.Private);
            Response.Cache.SetMaxAge(TimeSpan.FromDays(365));
            return File(path, "image/webp");
        }
    }
}
