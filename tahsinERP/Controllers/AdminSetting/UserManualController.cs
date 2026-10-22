using System.Linq;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// User Manual controller - provides user manual documentation
    /// </summary>
    public class UserManualController : Controller
    {
        // Olish so'rovi: UserManual
        public ActionResult Index()
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

            var model = UserManualProvider.GetManual();
            return View(model);
        }

        // Olish so'rovi: UserManual/GetChapter
        [HttpGet]
        public ActionResult GetChapter(string slug)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { error = "Not authenticated" }, JsonRequestBehavior.AllowGet);
            }

            var manual = UserManualProvider.GetManual();
            var chapter = manual.Chapters.FirstOrDefault(c => c.Id == slug);

            if (chapter == null)
            {
                return Json(new { error = tahsinERP.I18n.L.T("Core_SectionNotFound") }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { 
                id = chapter.Id,
                title = chapter.Title,
                content = chapter.ContentHtml 
            }, JsonRequestBehavior.AllowGet);
        }
    }
}
