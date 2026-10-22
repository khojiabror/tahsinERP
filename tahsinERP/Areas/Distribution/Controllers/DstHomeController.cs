using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>Distributsiya modulining bosh sahifasi va foydalanuvchi tilini tanlash.</summary>
    public class DstHomeController : DstControllerBase
    {
        public ActionResult Index()
        {
            return View();
        }

        /// <summary>Interfeys tilini almashtirish (o'z sozlamasi — alohida huquq talab qilinmaydi).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Filters.SkipPermission]
        public ActionResult SetLanguage(string language)
        {
            tahsinERP.I18n.UiLanguage.SaveForUser(CurrentUserId, language);
            Response.Cookies.Add(tahsinERP.I18n.UiLanguage.Cookie(language));
            return RedirectToAction("Index");
        }
    }
}
