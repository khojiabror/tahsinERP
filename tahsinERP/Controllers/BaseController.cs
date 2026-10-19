using System;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Controllerlar uchun umumiy asos. Yangi va refaktor qilinayotgan controllerlar shundan meros oladi.
    ///  - <see cref="CurrentUserName"/>, <see cref="CurrentUserId"/> — keshdan (UserHelper.GetUserId DB'ga borardi);
    ///  - <see cref="CanView"/>/<see cref="CanChange"/> — RoleHelper snapshot'i;
    ///  - <see cref="JsonError"/> — istisnoni logga yozadi, foydalanuvchiga ichki xabarni ko'rsatmaydi;
    ///  - <see cref="NotAuthorized"/> — 544 marta takrorlangan RedirectToAction("Index","ViewNotAuthorized");
    ///  - <see cref="SoftDeleteAsync{T}"/> — Delete POST'dagi Find → IsDeleted → hook → Save → log → catch bloki.
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
            return RedirectToAction("Index", "ViewNotAuthorized", new { area = "" });
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

        private static readonly ConcurrentDictionary<Type, PropertyInfo> IsDeletedProps = new ConcurrentDictionary<Type, PropertyInfo>();

        private static PropertyInfo IsDeletedProperty(Type type)
        {
            return IsDeletedProps.GetOrAdd(type, t =>
            {
                var prop = t.GetProperty("IsDeleted");
                if (prop == null || (prop.PropertyType != typeof(bool) && prop.PropertyType != typeof(bool?)))
                    throw new InvalidOperationException(t.Name + " da IsDeleted (bool/bool?) maydoni yo'q.");
                return prop;
            });
        }

        /// <summary>
        /// Umumiy soft delete: yozuvni topadi (o'chirilmagan bo'lsa), IsDeleted = true qiladi,
        /// <paramref name="afterDelete"/> hook'ini (masalan SoftDeleteReferenceService.After...Async) chaqiradi,
        /// saqlaydi va audit log yozadi. Xatoda ModelState'ga foydalanuvchi xabari qo'shiladi.
        /// <code>
        /// if (await SoftDeleteAsync&lt;FORWARDER&gt;(db, id,
        ///         (ctx, f) =&gt; SoftDeleteReferenceService.AfterForwarderSoftDeletedAsync(ctx, f.ID),
        ///         f =&gt; $"{f.ForwarderName} - yuk tashuvchi o'chirildi."))
        ///     return RedirectToAction("Index");
        /// </code>
        /// </summary>
        /// <returns>true — o'chirildi; false — topilmadi yoki xato (sabab ModelState'da).</returns>
        protected async Task<bool> SoftDeleteAsync<T>(
            DBTHSNEntities db,
            object id,
            Func<DBTHSNEntities, T, Task> afterDelete,
            Func<T, string> logText,
            string notFoundMessage = "Yozuv topilmadi.") where T : class
        {
            var prop = IsDeletedProperty(typeof(T));
            var entity = id == null ? null : await db.Set<T>().FindAsync(id);
            if (entity == null || Equals(prop.GetValue(entity), true))
            {
                ModelState.AddModelError("", notFoundMessage);
                return false;
            }

            string controllerName = RouteData?.Values["controller"] + "Controller";
            try
            {
                prop.SetValue(entity, true);
                if (afterDelete != null)
                    await afterDelete(db, entity);
                await db.SaveChangesAsync();

                LogHelper.LogToDatabase(CurrentUserName, controllerName, "Delete", logText?.Invoke(entity));
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[SoftDelete] {0} {1}: {2}", typeof(T).Name, id, ex);
                LogHelper.LogToDatabase(CurrentUserName, controllerName, "Delete", "Xatolik: " + InnermostMessage(ex));
                ModelState.AddModelError("", "Ma'lumotni o'chirishda xatolik yuz berdi. Qayta urinib ko'ring va muammo davom etsa, tizim administratoriga murojaat qiling.");
                return false;
            }
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
