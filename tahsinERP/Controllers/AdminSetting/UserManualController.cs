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
        // GET: UserManual
        public ActionResult Index()
        {
            // Check if user is authenticated
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            // Check basic view permission (everyone with system access can view manual)
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, "Layout"))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            var model = UserManualProvider.GetManual();
            return View(model);
        }

        // GET: UserManual/GetChapter
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
                return Json(new { error = "Chapter not found" }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { 
                id = chapter.Id,
                title = chapter.Title,
                content = chapter.ContentHtml 
            }, JsonRequestBehavior.AllowGet);
        }
    }
}
