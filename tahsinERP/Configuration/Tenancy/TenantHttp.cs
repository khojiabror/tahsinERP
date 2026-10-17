using System;
using System.Security.Principal;
using System.Web;
using System.Web.Security;

namespace tahsinERP.Configuration.Tenancy
{
    /// <summary>
    /// So'rov darajasidagi tenant ishlari (Global.asax chaqiradi):
    ///   BeginRequest            — host bo'yicha tenant'ni aniqlash; topilmasa 404 / reestr buzilgan bo'lsa 503.
    ///   PostAuthenticateRequest — auth cookie shu tenant uchun chiqarilganini tekshirish.
    ///
    /// Barcha tenantlar bitta machineKey'ga ega, shuning uchun firma1'dagi "admin" cookie'sini
    /// firma2 host'iga qo'yib, u yerdagi "admin" bo'lib kirish mumkin bo'lardi. Buning oldini olish
    /// uchun ticket.UserData oxiriga tenant kaliti qo'shiladi (<see cref="BindUserData"/>).
    /// </summary>
    public static class TenantHttp
    {
        private const string TenantMarker = "\u001Ftenant=";

        public static void BeginRequest(HttpContext context)
        {
            var registry = TenantRegistry.Current;
            if (!registry.IsActive)
                return; // bitta-baza rejimi — hech narsa o'zgarmaydi

            if (registry.IsBroken)
            {
                Reject(context, 503, "Tizim sozlamasi xato (tenant reestri). Administrator bilan bog'laning.");
                return;
            }

            var tenant = registry.ResolveHost(context.Request.Url.Host);
            if (tenant == null)
            {
                Reject(context, 404, "Tashkilot topilmadi.");
                return;
            }
            TenantContext.SetForRequest(context, tenant);
        }

        public static void PostAuthenticateRequest(HttpContext context)
        {
            if (!TenantRegistry.Current.IsActive)
                return;

            var identity = context.User != null ? context.User.Identity as FormsIdentity : null;
            if (identity == null || !identity.IsAuthenticated)
                return;

            var tenant = TenantContext.CurrentOrNull;
            string ticketTenant = TenantOf(identity.Ticket.UserData);
            if (tenant == null || !string.Equals(ticketTenant, tenant.Key, StringComparison.Ordinal))
            {
                // Boshqa tenant (yoki tenant'siz eski) cookie — anonim deb hisoblaymiz, login sahifasiga tushadi
                context.User = new GenericPrincipal(new GenericIdentity(string.Empty), new string[0]);
            }
        }

        /// <summary>Login'da ticket.UserData ga joriy tenant'ni qo'shadi (bitta-baza rejimida o'zgarmaydi).</summary>
        public static string BindUserData(string userData)
        {
            var tenant = TenantContext.CurrentOrNull;
            return tenant == null ? userData : (userData ?? string.Empty) + TenantMarker + tenant.Key;
        }

        /// <summary>UserData'dan tenant belgisini olib tashlaydi (to'liq ism kabi asl qiymat).</summary>
        public static string StripTenant(string userData)
        {
            if (userData == null) return null;
            int idx = userData.IndexOf(TenantMarker, StringComparison.Ordinal);
            return idx < 0 ? userData : userData.Substring(0, idx);
        }

        /// <summary>UserData'dagi tenant kaliti yoki null.</summary>
        public static string TenantOf(string userData)
        {
            if (userData == null) return null;
            int idx = userData.IndexOf(TenantMarker, StringComparison.Ordinal);
            return idx < 0 ? null : userData.Substring(idx + TenantMarker.Length);
        }

        private static void Reject(HttpContext context, int status, string message)
        {
            var response = context.Response;
            response.Clear();
            response.StatusCode = status;
            response.TrySkipIisCustomErrors = true;
            response.ContentType = "text/plain; charset=utf-8";
            response.Write(message);
            context.ApplicationInstance.CompleteRequest();
        }
    }
}
