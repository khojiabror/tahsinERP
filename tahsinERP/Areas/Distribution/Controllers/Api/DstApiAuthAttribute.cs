using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Threading;
using System.Web.Mvc;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Configuration.Tenancy;
using tahsinERP.Integration;
using tahsinERP.Services;

namespace tahsinERP.Areas.Distribution.Controllers.Api
{
    /// <summary>
    /// Servisdan-servisga autentifikatsiya (bot → ERP): <see cref="DstApiSignature"/> imzosi tenant kaliti bilan.
    /// Kalit manbai: muhit o'zgaruvchisi TAHSINERP_BOT_KEY yoki appSettings "DstBotKey" (sozlanmagan bo'lsa API o'chiq — 503).
    /// X-Acting-User berilsa — o'sha faol ERP foydalanuvchisi nomidan (HttpContext.User), barcha ruxsat/scope qoidalari bilan.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class DstApiAuthAttribute : FilterAttribute, IAuthorizationFilter
    {
        public const string EnvKey = "TAHSINERP_BOT_KEY";
        public const string ActingUserIdItem = "DstApi.ActingUserId";

        /// <summary>Qo'shimcha xizmat/modul talab qilinsa (masalan "Website") — paketda bo'lmasa 403 Edition.</summary>
        public string RequiredModule { get; set; }

        public static string MasterKey()
        {
            var key = Environment.GetEnvironmentVariable(EnvKey);
            return string.IsNullOrWhiteSpace(key) ? ConfigurationManager.AppSettings["DstBotKey"] : key;
        }

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            var http = filterContext.HttpContext;
            http.Response.SuppressFormsAuthenticationRedirect = true;   // 401 login sahifasiga 302 bo'lib ketmasin
            http.Response.TrySkipIisCustomErrors = true;                // va IIS HTML xato sahifasiga almashmasin
            var master = MasterKey();
            if (string.IsNullOrWhiteSpace(master) || master.Length < 32)
            {
                filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, "Bot API sozlanmagan");
                return;
            }

            byte[] body;
            var input = http.Request.InputStream;
            using (var ms = new MemoryStream())
            {
                input.Position = 0;
                input.CopyTo(ms);
                body = ms.ToArray();
                input.Position = 0;   // model binding uchun qayta o'qiladi
            }

            var tenant = TenantContext.CurrentOrNull?.Slug ?? DstApiSignature.DefaultTenant;
            var actingHeader = http.Request.Headers[DstApiSignature.ActingUserHeader];
            bool ok = DstApiSignature.Verify(DstApiSignature.TenantKey(master, tenant),
                http.Request.Headers[DstApiSignature.TimestampHeader], http.Request.Headers[DstApiSignature.SignatureHeader],
                http.Request.HttpMethod, http.Request.Url.PathAndQuery, actingHeader, body, DateTimeOffset.UtcNow);
            if (!ok)
            {
                filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Unauthorized, "Imzo noto'g'ri");
                return;
            }

            // [AllowAnonymous] tufayli global LicenseCheck o'tkazib yuboradi — bot ham pullik mahsulot qismi, shu yerda tekshiriladi
            var license = LicenseService.GetCachedStatus();
            bool isRead = string.Equals(http.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase);
            if (!license.FullAccess && !(license.ReadOnly && isRead))
            {
                http.Response.TrySkipIisCustomErrors = true;
                http.Response.AddHeader("X-License-Message", Uri.EscapeDataString(license.Message ?? ""));
                filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Forbidden, "License");
                return;
            }
            if (!string.IsNullOrEmpty(RequiredModule) && Enum.TryParse(RequiredModule, out tahsinERP.Services.Editions.ProductModule module)
                && !tahsinERP.Services.Editions.EditionModules.IsModuleAllowed(tahsinERP.Services.Editions.EditionGate.CurrentEdition, module))
            {
                filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Forbidden, "Edition");
                return;
            }
            var actionModule = filterContext.ActionDescriptor.GetCustomAttributes(typeof(DstApiModuleAttribute), true)
                .OfType<DstApiModuleAttribute>().FirstOrDefault();
            if (actionModule != null
                && !tahsinERP.Services.Editions.EditionModules.IsModuleAllowed(tahsinERP.Services.Editions.EditionGate.CurrentEdition, actionModule.Module))
            {
                filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Forbidden, "Edition");
                return;
            }

            if (!string.IsNullOrEmpty(actingHeader))
            {
                if (!int.TryParse(actingHeader, out var userId))
                {
                    filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Forbidden, "Foydalanuvchi noto'g'ri");
                    return;
                }
                string uname;
                using (var db = new DistributionDbContext())
                    uname = db.Users.Where(u => u.ID == userId && u.IsActive && u.IsDeleted != true).Select(u => u.Uname).FirstOrDefault();
                if (uname == null)
                {
                    filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Forbidden, "Foydalanuvchi faol emas");
                    return;
                }
                var principal = new GenericPrincipal(new GenericIdentity(uname, "DstApi"), new string[0]);
                http.User = principal;
                Thread.CurrentPrincipal = principal;
                http.Items[ActingUserIdItem] = userId;
            }
        }
    }
}
