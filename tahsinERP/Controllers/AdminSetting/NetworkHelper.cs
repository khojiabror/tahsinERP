using System;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Web;

namespace tahsinERP.Controllers
{
    public static class NetworkHelper
    {
        [DllImport("Iphlpapi.dll")]
        private static extern int SendARP(int dest, int host, ref long mac, ref int length);

        [DllImport("Ws2_32.dll")]
        private static extern int inet_addr(string ip);

        /// <summary>
        /// Gets the server's IP address (legacy method, kept for backward compatibility)
        /// </summary>
        public static string GetIpAddress()
        {
            string ip = string.Empty;
            IPHostEntry ipEntry = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress[] addr = ipEntry.AddressList;

            // Assuming IPv4 address is needed
            ip = addr.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString();

            return ip;
        }

        /// <summary>
        /// Gets the client's IP address from HTTP request
        /// </summary>
        public static string GetClientIpAddress(HttpRequestBase request)
        {
            if (request == null)
                return "127.0.0.1";

            try
            {
                // Check for IP in X-Forwarded-For header (for proxies/load balancers)
                string xForwardedFor = request.Headers["X-Forwarded-For"];
                if (!string.IsNullOrEmpty(xForwardedFor))
                {
                    // X-Forwarded-For can contain multiple IPs, take the first one
                    string[] ips = xForwardedFor.Split(',');
                    if (ips.Length > 0)
                    {
                        string ip = ips[0].Trim();
                        if (!string.IsNullOrEmpty(ip))
                            return ip;
                    }
                }

                // Check for X-Real-IP header
                string xRealIp = request.Headers["X-Real-IP"];
                if (!string.IsNullOrEmpty(xRealIp))
                {
                    return xRealIp.Trim();
                }

                // Fall back to UserHostAddress
                if (!string.IsNullOrEmpty(request.UserHostAddress))
                {
                    return request.UserHostAddress;
                }

                // Last resort: try to get from server variables
                string ipAddress = request.ServerVariables["HTTP_X_FORWARDED_FOR"];
                if (string.IsNullOrEmpty(ipAddress))
                {
                    ipAddress = request.ServerVariables["REMOTE_ADDR"];
                }

                if (!string.IsNullOrEmpty(ipAddress))
                {
                    return ipAddress.Split(',')[0].Trim();
                }
            }
            catch
            {
                // If anything fails, return default
            }

            return "127.0.0.1";
        }

        /// <summary>
        /// Gets the client's IP address from HttpContext
        /// </summary>
        public static string GetClientIpAddress(HttpContextBase httpContext)
        {
            if (httpContext?.Request == null)
                return "127.0.0.1";

            return GetClientIpAddress(httpContext.Request);
        }

        /// <summary>
        /// Gets MAC address - Note: Cannot get client MAC from server-side code in web applications
        /// This method will return a default value since client MAC is not accessible
        /// </summary>
        public static string GetMacAddress(string ipAddress)
        {
            // For web applications, we cannot get the client's MAC address from server-side code
            // MAC addresses are layer 2 and not transmitted over the internet
            // Return default value instead of throwing error
            return "00:00:00:00:00:00";
        }

        /// <summary>
        /// Legacy method - attempts to get MAC address but will return default on failure
        /// </summary>
        [Obsolete("Use GetMacAddress(string) instead. Client MAC cannot be retrieved from server-side code.")]
        public static string GetMacAddressLegacy(string ipAddress)
        {
            try
            {
                int ldest = inet_addr(ipAddress);
                long macInfo = 0;
                int len = 6;
                int result = SendARP(ldest, 0, ref macInfo, ref len);

                if (result != 0)
                {
                    return "00:00:00:00:00:00";
                }

                string macSrc = macInfo.ToString("X");
                while (macSrc.Length < 12)
                {
                    macSrc = macSrc.Insert(0, "0");
                }

                string macAddress = string.Join(":", Enumerable.Range(0, 12).Where(x => x % 2 == 0).Select(x => macSrc.Substring(x, 2)));
                return macAddress;
            }
            catch
            {
                return "00:00:00:00:00:00";
            }
        }
    }

}