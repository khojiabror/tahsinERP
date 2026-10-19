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
            int userId = CurrentUserId;
            using (var db = new DistributionDbContext())
            {
                var setting = db.UserSettings.Find(userId);
                if (setting == null)
                    db.UserSettings.Add(new UserSetting { UserID = userId, Language = DstLocalization.Normalize(language) });
                else
                    setting.Language = DstLocalization.Normalize(language);
                db.SaveChanges();
            }
            DstLocalization.ForgetLanguage(userId);
            return RedirectToAction("Index");
        }
    }
}
