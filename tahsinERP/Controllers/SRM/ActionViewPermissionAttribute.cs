using System.Web.Mvc;
using System.Web.Routing;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// SRM-D16: view boshidagi <c>@if (RoleHelper.Is...Permitted(controller, action)) ... else Response.Redirect</c>
    /// o'ramining o'rnini bosadi. Sahifa (ViewResult) qaytarilganda (Controller, Action) yozuvi bo'yicha
    /// qat'iy tekshiradi — controller yozuviga qaytmaydi (<see cref="Filters.PermissionAttribute"/>dan farqli),
    /// shuning uchun avvalgi view tekshiruvi bilan xatti-harakat bir xil. Redirect/JSON natijalarga tegmaydi.
    /// </summary>
    public sealed class ActionViewPermissionAttribute : ActionFilterAttribute
    {
        /// <param name="change">true — o'zgartirish huquqi (Create/Edit/Delete sahifalari), false — ko'rish.</param>
        public ActionViewPermissionAttribute(bool change)
        {
            Change = change;
        }

        public bool Change { get; }

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            if (filterContext.Exception != null || !(filterContext.Result is ViewResult))
            {
                return;
            }

            string username = filterContext.HttpContext.User?.Identity?.Name;
            string controller = filterContext.RouteData.Values["controller"]?.ToString();
            string action = filterContext.RouteData.Values["action"]?.ToString();

            bool allowed = username != null && (Change
                ? RoleHelper.IsChangePermitted(username, controller, action)
                : RoleHelper.IsViewPermitted(username, controller, action));
            if (!allowed)
            {
                filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary
                {
                    { "controller", "ViewNotAuthorized" },
                    { "action", "Index" }
                });
            }
        }
    }
}
