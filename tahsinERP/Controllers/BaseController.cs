using System;
using System.Net;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Controllerlar uchun umumiy asos. Yangi va refaktor qilinayotgan controllerlar shundan meros oladi.
    ///  - <see cref="CurrentUserName"/>, <see cref="CurrentUserId"/> — keshdan (UserHelper.GetUserId DB'ga borardi);
    ///  - <see cref="CanView"/>/<see cref="CanChange"/> — RoleHelper snapshot'i;
    ///  - <see cref="JsonError"/> — istisnoni logga yozadi, foydalanuvchiga ichki xabarni ko'rsatmaydi;
    ///  - <see cref="NotAuthorized"/> — 544 marta takrorlangan RedirectToAction("Index","ViewNotAuthorized").
    /// Server tomonidagi ruxsat uchun controller/action'ga <see cref="Filters.PermissionAttribute"/> qo'ying.
    /// </summary>
    public abstract class BaseController : Controller
    {
        protected string CurrentUserName => User?.Identity?.Name;

        /// <summary>Joriy foydalanuvchi ID'si (topilmasa istisno).</summary>
        protected int CurrentUserId
        {
            get
            {
                var id = RoleHelper.GetUserId(CurrentUserName);
                if (!id.HasValue)
                    throw new InvalidOperationException("Joriy foydalanuvchi topilmadi: " + CurrentUserName);
                return id.Value;
            }
        }

        protected bool CanView(string module) => RoleHelper.IsViewPermitted(CurrentUserName, module);

        protected bool CanChange(string module) => RoleHelper.IsChangePermitted(CurrentUserName, module);

        protected ActionResult NotAuthorized()
        {
            if (Request.IsAjaxRequest())
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "Ruxsat yo'q");
            return RedirectToAction("Index", "ViewNotAuthorized");
        }

        /// <summary>
        /// Xatoni Trace'ga yozib, foydalanuvchiga umumiy xabar qaytaradi
        /// (avval 30+ joyda ex.Message — SQL/ichki tafsilotlar — to'g'ridan-to'g'ri ko'rsatilardi).
        /// </summary>
        protected JsonResult JsonError(Exception ex, string userMessage = null)
        {
            string errorId = Guid.NewGuid().ToString("N").Substring(0, 8);
            System.Diagnostics.Trace.TraceError("[{0}] {1}/{2} ({3}): {4}",
                errorId,
                RouteData?.Values["controller"],
                RouteData?.Values["action"],
                CurrentUserName,
                ex);
            return Json(new
            {
                success = false,
                message = (userMessage ?? "Xatolik yuz berdi.") + " (kod: " + errorId + ")"
            }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>Ichki istisnolar zanjiridagi eng chuqur xabar (13+ joyda qo'lda yozilgan tsikl).</summary>
        protected static string InnermostMessage(Exception ex)
        {
            while (ex?.InnerException != null)
                ex = ex.InnerException;
            return ex?.Message;
        }
    }
}
