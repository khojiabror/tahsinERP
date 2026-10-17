using System;
using System.Net;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Filters
{
    /// <summary>
    /// Server tomonidagi ruxsat tekshiruvi (har action ichidagi copy-paste
    /// <c>if (!RoleHelper.Is...Permitted(...)) return RedirectToAction(...)</c> o'rniga).
    ///
    /// Controller yoki action'ga qo'yiladi; action'dagisi controller'dagisini bekor qiladi.
    ///  - <see cref="Module"/> berilsa — PERMISSIONMODULES.Module bo'yicha tekshiriladi.
    ///  - Aks holda — (Controller, Action) yozuvi bo'yicha; yozuv bo'lmasa, shu controller'ning
    ///    istalgan yozuvidagi ruxsat (AJAX yordamchi amallar uchun).
    ///  - GET/HEAD — ko'rish huquqi; boshqa metodlar va Create/Edit/Delete... GET sahifalari —
    ///    o'zgartirish huquqi. <see cref="Change"/> = true har doim o'zgartirish talab qiladi.
    ///
    /// Ruxsat bo'lmasa: AJAX/JSON so'rovga 403, oddiy so'rovga ViewNotAuthorized sahifasi.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class PermissionAttribute : FilterAttribute, IAuthorizationFilter
    {
        private static readonly string[] ChangeActionPrefixes =
        {
            "Create", "Edit", "Delete", "Approve", "Reject", "Upload", "Import", "Save", "Update", "Remove", "Cancel", "Close", "Receive"
        };

        private static readonly string[] ReadOnlyPostPrefixes =
        {
            "Get", "Search", "Load", "Find", "Check", "Calculate", "Preview", "Export"
        };

        public PermissionAttribute()
        {
        }

        public PermissionAttribute(string module)
        {
            Module = module;
        }

        /// <summary>PERMISSIONMODULES.Module nomi (ixtiyoriy).</summary>
        public string Module { get; set; }

        /// <summary>Boshqa controller nomi bo'yicha tekshirish (masalan, API controller sahifa ruxsatidan foydalanadi).</summary>
        public string Controller { get; set; }

        /// <summary>Har doim o'zgartirish huquqini talab qilish.</summary>
        public bool Change { get; set; }

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext.ActionDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true) ||
                filterContext.ActionDescriptor.IsDefined(typeof(SkipPermissionAttribute), true))
            {
                return;
            }

            var user = filterContext.HttpContext.User;
            if (user == null || !user.Identity.IsAuthenticated)
            {
                // Global [Authorize] login sahifasiga yo'naltiradi
                return;
            }

            string username = user.Identity.Name;
            string action = filterContext.ActionDescriptor.ActionName;
            string controller = Controller ?? filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            bool needChange = Change || RequiresChange(filterContext.HttpContext.Request.HttpMethod, action);

            bool allowed;
            if (!string.IsNullOrEmpty(Module))
            {
                allowed = needChange
                    ? RoleHelper.IsChangePermitted(username, Module)
                    : RoleHelper.IsViewPermitted(username, Module);
            }
            else
            {
                allowed = RoleHelper.IsPermittedForAction(username, controller, action, needChange);
            }

            if (allowed) return;

            var request = filterContext.HttpContext.Request;
            bool wantsJson = request.IsAjaxRequest() ||
                             (request.AcceptTypes != null && Array.Exists(request.AcceptTypes, t => t != null && t.Contains("application/json")));
            if (wantsJson)
            {
                filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Forbidden, "Ruxsat yo'q");
            }
            else
            {
                filterContext.Result = new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary
                {
                    { "controller", "ViewNotAuthorized" },
                    { "action", "Index" }
                });
            }
        }

        private static bool RequiresChange(string httpMethod, string action)
        {
            if (!string.Equals(httpMethod, "GET", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(httpMethod, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                // POST orqali chaqiriladigan o'qish amallari (qidiruv, hisob-kitob ko'rinishi) — ko'rish huquqi yetarli
                foreach (var prefix in ReadOnlyPostPrefixes)
                {
                    if (action.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                return true;
            }

            foreach (var prefix in ChangeActionPrefixes)
            {
                if (action.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }

    /// <summary>Controller darajasidagi <see cref="PermissionAttribute"/> dan ozod qilish.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class SkipPermissionAttribute : Attribute
    {
    }
}
