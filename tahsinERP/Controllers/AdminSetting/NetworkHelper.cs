using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Web;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Mijoz IP manzilini aniqlash.
    ///
    /// Avvalgi versiya mijoz yuborgan X-Forwarded-For, X-Real-IP, CF-Connecting-IP va h.k.
    /// header'larga so'zsiz ishonardi (IP'ni soxtalashtirish mumkin edi), barcha header'larni
    /// IPv4 qidirib aylanib chiqardi va har chaqiruvda DNS so'rovi yuborardi.
    ///
    /// Endi: REMOTE_ADDR (Request.UserHostAddress). X-Forwarded-For faqat so'rov web.config'dagi
    /// <c>appSettings["TrustedProxies"]</c> (vergul bilan ajratilgan IP'lar) ro'yxatidagi proksidan
    /// kelganda hisobga olinadi.
    /// </summary>
    public static class NetworkHelper
    {
        private static readonly Lazy<string[]> TrustedProxies = new Lazy<string[]>(() =>
            (ConfigurationManager.AppSettings["TrustedProxies"] ?? "")
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToArray());

        // Server IP'si o'zgarmaydi — bir marta aniqlanadi (avval har log yozuvida DNS so'rovi edi)
        private static readonly Lazy<string> ServerIp = new Lazy<string>(() =>
        {
            try
            {
                return Dns.GetHostEntry(Dns.GetHostName()).AddressList
                    .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString()
                    ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        });

        /// <summary>
        /// Server IP manzili (keshlangan).
        /// </summary>
        public static string GetIpAddress()
        {
            return ServerIp.Value;
        }

        /// <summary>
        /// Mijozning IP manzili.
        /// </summary>
        public static string GetClientIpAddress(HttpRequestBase request)
        {
            if (request == null)
                return "127.0.0.1";

            string remote = request.UserHostAddress;
            if (string.IsNullOrEmpty(remote))
                return "127.0.0.1";

            if (TrustedProxies.Value.Length > 0 && TrustedProxies.Value.Contains(remote))
            {
                // Eng chapdagi manzil — asl mijoz
                var forwarded = request.Headers["X-Forwarded-For"];
                if (!string.IsNullOrWhiteSpace(forwarded))
                {
                    var first = forwarded.Split(',')[0].Trim();
                    IPAddress parsed;
                    if (IPAddress.TryParse(first, out parsed))
                        return parsed.ToString();
                }
            }

            return remote == "::1" ? "127.0.0.1" : remote;
        }

        /// <summary>
        /// Mijozning IP manzili (HttpContext orqali).
        /// </summary>
        public static string GetClientIpAddress(HttpContextBase httpContext)
        {
            if (httpContext?.Request == null)
                return "127.0.0.1";

            return GetClientIpAddress(httpContext.Request);
        }

        /// <summary>
        /// Mijoz MAC manzilini server tomonidan olib bo'lmaydi — standart qiymat qaytaradi.
        /// </summary>
        public static string GetMacAddress(string ipAddress)
        {
            return "00:00:00:00:00:00";
        }
    }
}
