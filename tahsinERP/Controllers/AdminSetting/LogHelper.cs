using System;
using System.Web;
using tahsinERP.Models;
using tahsinERP.Services;

namespace tahsinERP.Controllers
{
    public static class LogHelper
    {
        /// <summary>
        /// Foydalanuvchi amalini mijoz IP manzili bilan USERLOGS'ga yozadi.
        /// IP va foydalanuvchi ID so'rov vaqtida olinadi (keshdan), bazaga yozish esa
        /// <see cref="AuditLogQueue"/> orqali fonda, paketlab bajariladi.
        /// </summary>
        public static void LogToDatabase(string userEmail, string controllerName, string actionName, string details = null, HttpContextBase httpContext = null)
        {
            try
            {
                if (string.IsNullOrEmpty(userEmail))
                    return;

                HttpContextBase contextToUse = httpContext;
                if (contextToUse == null && HttpContext.Current != null)
                {
                    contextToUse = new HttpContextWrapper(HttpContext.Current);
                }

                string clientIp = "127.0.0.1";
                try
                {
                    clientIp = NetworkHelper.GetClientIpAddress(contextToUse) ?? "127.0.0.1";
                }
                catch
                {
                    // IP olinmasa standart qiymat
                }

                // Noma'lum foydalanuvchi (FK buzilishi butun paketni yiqitmasligi uchun) — yozilmaydi
                int? userId = RoleHelper.GetUserId(userEmail);
                if (!userId.HasValue)
                    return;

                AuditLogQueue.Instance.Enqueue(new USERLOG
                {
                    ControllerName = controllerName ?? "Unknown",
                    ActionName = actionName ?? "Unknown",
                    DateTime = DateTime.Now,
                    UserID = userId.Value,
                    IP = clientIp,
                    MacAddr = "00:00:00:00:00:00"
                });
            }
            catch (Exception ex)
            {
                // Loglash ilovani to'xtatmasligi kerak
                System.Diagnostics.Trace.TraceError("[LogHelper] " + ex.Message);
            }
        }
    }
}
