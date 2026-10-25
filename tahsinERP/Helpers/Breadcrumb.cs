using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// Breadcrumb'ning oraliq bo'limi: foydalanuvchi maqsad sahifani ko'ra olsa (ruxsat + paket) — havola,
    /// aks holda oddiy matn. Shu bilan breadcrumb hech qachon yopiq sahifaga olib bormaydi.
    /// module — menyu boshqa modul huquqi bilan ko'rsatadigan sahifalar uchun (masalan SRMDashboard → Supplier).
    /// </summary>
    public static class Breadcrumb
    {
        public static IHtmlString Link(this HtmlHelper html, string text, string controller, string action = "Index", string area = "", string module = null)
        {
            var user = html.ViewContext.HttpContext.User;
            var name = user != null && user.Identity != null ? user.Identity.Name : null;
            // Menyu bilan bir xil: modul (controller) darajasidagi ko'rish huquqi; bosh sahifa hammaga ochiq
            var permitted = controller == "Home" || (!string.IsNullOrEmpty(name) && RoleHelper.IsViewPermitted(name, module ?? controller));
            if (!permitted)
                return new HtmlString(HttpUtility.HtmlEncode(text));

            var url = new UrlHelper(html.ViewContext.RequestContext).Action(action, controller, new { area });
            return new HtmlString("<a href=\"" + HttpUtility.HtmlAttributeEncode(url) + "\">" + HttpUtility.HtmlEncode(text) + "</a>");
        }
    }
}
