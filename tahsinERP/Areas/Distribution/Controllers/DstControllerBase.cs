using System;
using System.Data.Entity.Infrastructure;
using System.Threading;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Controllers;
using tahsinERP.Filters;
using tahsinERP.Models;

namespace tahsinERP.Areas.Distribution.Controllers
{
    /// <summary>
    /// Distributsiya controllerlari uchun asos: server tomonidagi ruxsat ([Permission], controller/action
    /// bo'yicha), foydalanuvchi tili (uz/ru), maxsus huquqlar va umumiy yordamchilar.
    /// </summary>
    [Permission]
    public abstract class DstControllerBase : BaseController
    {
        // Model binding OnActionExecuting'dan OLDIN bo'ladi — madaniyat shu yerda qo'yiladi
        protected override IAsyncResult BeginExecuteCore(AsyncCallback callback, object state)
        {
            Thread.CurrentThread.CurrentCulture = DstLocalization.FormCulture;
            return base.BeginExecuteCore(callback, state);
        }

        protected override void ExecuteCore()
        {
            Thread.CurrentThread.CurrentCulture = DstLocalization.FormCulture;
            base.ExecuteCore();
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var userId = RoleHelper.GetUserId(CurrentUserName);
            if (userId.HasValue)
                Thread.CurrentThread.CurrentUICulture = DstLocalization.CultureFor(DstLocalization.LanguageOf(userId.Value));

            ViewBag.DstCanSeeCost = DstPermissions.Has(CurrentUserName, DstPermissions.SeeCost);
            ViewBag.DstCanChange = RoleHelper.IsPermittedForAction(CurrentUserName,
                filterContext.ActionDescriptor.ControllerDescriptor.ControllerName, "Index", true);
            base.OnActionExecuting(filterContext);
        }

        protected bool CanSeeCost => DstPermissions.Has(CurrentUserName, DstPermissions.SeeCost);

        protected bool IsRussian => Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ru";

        /// <summary>Joriy foydalanuvchi nomidan yozadigan distributsiya konteksti.</summary>
        protected DistributionDbContext NewDb()
        {
            return new DistributionDbContext { Actor = new AuditActor(RoleHelper.GetUserId(CurrentUserName), AuditActor.Web) };
        }

        protected DistributionUnitOfWork NewUnitOfWork()
        {
            return new DistributionUnitOfWork(new AuditActor(RoleHelper.GetUserId(CurrentUserName), AuditActor.Web));
        }

        /// <summary>Ikki tilli nom: ruscha bo'lsa va to'ldirilgan bo'lsa — ruscha.</summary>
        protected string Localized(string uz, string ru)
        {
            return IsRussian && !string.IsNullOrWhiteSpace(ru) ? ru : uz;
        }

        /// <summary>AJAX amal natijasi: biznes xatosi foydalanuvchiga, qolgani — umumiy xabar (logga).</summary>
        protected JsonResult DstJson(Func<object> action)
        {
            try
            {
                var data = action();
                return Json(new { success = true, data });
            }
            catch (DstValidationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                return Json(new { success = false, message = DstLocalization.T("Error_Duplicate") });
            }
            catch (Exception ex)
            {
                return JsonError(ex, DstLocalization.T("Error_SaveFailed"));
            }
        }

        protected static bool IsUniqueViolation(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is System.Data.SqlClient.SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                    return true;
            }
            return false;
        }
    }
}
