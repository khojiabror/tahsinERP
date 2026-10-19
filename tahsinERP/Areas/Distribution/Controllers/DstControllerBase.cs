using System.Threading;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Controllers;
using tahsinERP.Filters;
using tahsinERP.Models;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Distributsiya controllerlari uchun asos: server tomonidagi ruxsat ([Permission], controller/action
    /// bo'yicha), foydalanuvchi tili (uz/ru) va maxsus huquqlar.
    /// </summary>
    [Permission]
    public abstract class DstControllerBase : BaseController
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var userId = RoleHelper.GetUserId(CurrentUserName);
            if (userId.HasValue)
                Thread.CurrentThread.CurrentUICulture = DstLocalization.CultureFor(DstLocalization.LanguageOf(userId.Value));

            ViewBag.DstCanSeeCost = DstPermissions.Has(CurrentUserName, DstPermissions.SeeCost);
            base.OnActionExecuting(filterContext);
        }

        protected bool CanSeeCost => DstPermissions.Has(CurrentUserName, DstPermissions.SeeCost);
    }
}
