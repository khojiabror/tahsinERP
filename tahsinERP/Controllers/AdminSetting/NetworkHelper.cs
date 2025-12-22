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
                // Get server IP addresses to filter them out
                string[] serverIps = GetServerIpAddresses();
                
                string ipAddress = null;

                // Helper method to safely get server variable
                // This is important for release/publish mode where ServerVariables might not work correctly
                Func<string, string> getServerVar = (key) =>
                {
                    try
                    {
                        if (request.ServerVariables != null)
                        {
                            var allKeys = request.ServerVariables.AllKeys;
                            if (allKeys != null && allKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                            {
                                return request.ServerVariables[key];
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but don't fail
                        System.Diagnostics.Debug.WriteLine($"GetClientIpAddress: Error getting server variable {key}: {ex.Message}");
                    }
                    return null;
                };

                // Priority 1: Check for IP in X-Forwarded-For header (for proxies/load balancers)
                // This is usually the most reliable for getting client IP behind proxies
                string xForwardedFor = null;
                try
                {
                    xForwardedFor = request.Headers["X-Forwarded-For"];
                }
                catch { }
                
                if (string.IsNullOrEmpty(xForwardedFor))
                {
                    xForwardedFor = getServerVar("HTTP_X_FORWARDED_FOR");
                }
                
                if (!string.IsNullOrEmpty(xForwardedFor))
                {
                    // X-Forwarded-For can contain multiple IPs, the first one is usually the original client
                    string[] ips = xForwardedFor.Split(',');
                    foreach (string ip in ips)
                    {
                        string trimmedIp = ip.Trim();
                        if (!string.IsNullOrEmpty(trimmedIp) && 
                            !IsLoopback(trimmedIp) && 
                            !IsServerIp(trimmedIp, serverIps))
                        {
                            ipAddress = trimmedIp;
                            break;
                        }
                    }
                }

                // Priority 2: Check for X-Real-IP header (Nginx, etc.)
                if (string.IsNullOrEmpty(ipAddress))
                {
                    string xRealIp = null;
                    try
                    {
                        xRealIp = request.Headers["X-Real-IP"];
                    }
                    catch { }
                    
                    if (string.IsNullOrEmpty(xRealIp))
                    {
                        xRealIp = getServerVar("HTTP_X_REAL_IP");
                    }
                    if (!string.IsNullOrEmpty(xRealIp))
                    {
                        string trimmedIp = xRealIp.Trim();
                        if (!IsLoopback(trimmedIp) && !IsServerIp(trimmedIp, serverIps))
                        {
                            ipAddress = trimmedIp;
                        }
                    }
                }

                // Priority 3: Check CF-Connecting-IP (Cloudflare)
                if (string.IsNullOrEmpty(ipAddress))
                {
                    string cfIp = request.Headers["CF-Connecting-IP"];
                    if (!string.IsNullOrEmpty(cfIp))
                    {
                        string trimmedIp = cfIp.Trim();
                        if (!IsLoopback(trimmedIp) && !IsServerIp(trimmedIp, serverIps))
                        {
                            ipAddress = trimmedIp;
                        }
                    }
                }

                // Priority 4: Check True-Client-IP (Akamai, Cloudflare Enterprise)
                if (string.IsNullOrEmpty(ipAddress))
                {
                    string trueClientIp = null;
                    try
                    {
                        trueClientIp = request.Headers["True-Client-IP"];
                    }
                    catch { }
                    
                    if (string.IsNullOrEmpty(trueClientIp))
                    {
                        trueClientIp = getServerVar("HTTP_TRUE_CLIENT_IP");
                    }
                    if (!string.IsNullOrEmpty(trueClientIp))
                    {
                        string trimmedIp = trueClientIp.Trim();
                        if (!IsLoopback(trimmedIp) && !IsServerIp(trimmedIp, serverIps))
                        {
                            ipAddress = trimmedIp;
                        }
                    }
                }

                // Priority 5: Check X-Client-IP
                if (string.IsNullOrEmpty(ipAddress))
                {
                    string xClientIp = request.Headers["X-Client-IP"];
                    if (!string.IsNullOrEmpty(xClientIp))
                    {
                        string trimmedIp = xClientIp.Trim();
                        if (!IsLoopback(trimmedIp) && !IsServerIp(trimmedIp, serverIps))
                        {
                            ipAddress = trimmedIp;
                        }
                    }
                }

                // Priority 6: Check REMOTE_ADDR
                // IMPORTANT: REMOTE_ADDR is usually the actual client IP when there's no proxy
                // In production, REMOTE_ADDR should contain the real client IP
                if (string.IsNullOrEmpty(ipAddress))
                {
                    string remoteAddr = getServerVar("REMOTE_ADDR");
                    if (string.IsNullOrEmpty(remoteAddr))
                    {
                        // Try alternative server variable names
                        remoteAddr = getServerVar("REMOTE_HOST");
                    }
                    
                    if (!string.IsNullOrEmpty(remoteAddr))
                    {
                        string trimmedIp = remoteAddr.Trim();
                        
                        // Check if we have proxy headers
                        bool hasProxyHeaders = false;
                        try
                        {
                            hasProxyHeaders = !string.IsNullOrEmpty(request.Headers["X-Forwarded-For"]) ||
                                             !string.IsNullOrEmpty(request.Headers["X-Real-IP"]) ||
                                             !string.IsNullOrEmpty(getServerVar("HTTP_X_FORWARDED_FOR"));
                        }
                        catch { }
                        
                        // If no proxy headers, REMOTE_ADDR is the real client IP (even if it's 127.0.0.1 in dev)
                        if (!hasProxyHeaders)
                        {
                            // No proxy = direct connection, REMOTE_ADDR is the client IP
                            ipAddress = trimmedIp;
                            System.Diagnostics.Debug.WriteLine($"INFO: GetClientIpAddress - Using REMOTE_ADDR as client IP (no proxy headers): {trimmedIp}");
                        }
                        else
                        {
                            // We have proxy headers, so REMOTE_ADDR might be proxy IP
                            // Only use it if it's not loopback and not server IP
                            if (!IsLoopback(trimmedIp))
                            {
                                bool isDefinitelyServerIp = IsServerIp(trimmedIp, serverIps);
                                
                                if (!isDefinitelyServerIp)
                                {
                                    ipAddress = trimmedIp;
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"WARNING: GetClientIpAddress - REMOTE_ADDR appears to be server IP: {trimmedIp}, but proxy headers exist");
                                }
                            }
                        }
                    }
                }

                // Priority 7: Fall back to UserHostAddress
                // UserHostAddress is usually the same as REMOTE_ADDR, but check it anyway
                if (string.IsNullOrEmpty(ipAddress))
                {
                    if (!string.IsNullOrEmpty(request.UserHostAddress))
                    {
                        string userHostIp = request.UserHostAddress.Trim();
                        
                        // Check if we have proxy headers
                        bool hasProxyHeaders = !string.IsNullOrEmpty(request.Headers["X-Forwarded-For"]) ||
                                               !string.IsNullOrEmpty(request.Headers["X-Real-IP"]);
                        
                        if (!hasProxyHeaders)
                        {
                            // No proxy headers - UserHostAddress is the real client IP (even if 127.0.0.1 in dev)
                            ipAddress = userHostIp;
                            System.Diagnostics.Debug.WriteLine($"INFO: GetClientIpAddress - Using UserHostAddress as client IP (no proxy headers): {userHostIp}");
                        }
                        else
                        {
                            // We have proxy headers, so only use if not loopback and not server IP
                            if (!IsLoopback(userHostIp))
                            {
                                bool isDefinitelyServerIp = IsServerIp(userHostIp, serverIps);
                                
                                if (!isDefinitelyServerIp)
                                {
                                    ipAddress = userHostIp;
                                }
                            }
                        }
                    }
                }

                // Last resort: If we still don't have a valid IP, use REMOTE_ADDR
                // This handles edge cases where all checks failed
                if (string.IsNullOrEmpty(ipAddress))
                {
                    string remoteAddr = getServerVar("REMOTE_ADDR");
                    if (string.IsNullOrEmpty(remoteAddr))
                    {
                        remoteAddr = getServerVar("REMOTE_HOST");
                    }
                    
                    if (!string.IsNullOrEmpty(remoteAddr))
                    {
                        ipAddress = remoteAddr.Trim();
                        System.Diagnostics.Debug.WriteLine($"WARNING: GetClientIpAddress - Last resort: Using REMOTE_ADDR: {ipAddress}");
                    }
                }
                
                // Debug: Log what we found (only in debug mode to avoid performance issues in production)
                #if DEBUG
                if (!string.IsNullOrEmpty(ipAddress))
                {
                    System.Diagnostics.Debug.WriteLine($"GetClientIpAddress found: {ipAddress}");
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"  - X-Forwarded-For: {request.Headers["X-Forwarded-For"] ?? "null"}");
                        System.Diagnostics.Debug.WriteLine($"  - X-Real-IP: {request.Headers["X-Real-IP"] ?? "null"}");
                        System.Diagnostics.Debug.WriteLine($"  - REMOTE_ADDR: {getServerVar("REMOTE_ADDR") ?? "null"}");
                        System.Diagnostics.Debug.WriteLine($"  - UserHostAddress: {request.UserHostAddress ?? "null"}");
                    }
                    catch { }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"GetClientIpAddress: No valid IP found, returning 127.0.0.1");
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"  - X-Forwarded-For: {request.Headers["X-Forwarded-For"] ?? "null"}");
                        System.Diagnostics.Debug.WriteLine($"  - X-Real-IP: {request.Headers["X-Real-IP"] ?? "null"}");
                        System.Diagnostics.Debug.WriteLine($"  - REMOTE_ADDR: {getServerVar("REMOTE_ADDR") ?? "null"}");
                        System.Diagnostics.Debug.WriteLine($"  - UserHostAddress: {request.UserHostAddress ?? "null"}");
                    }
                    catch { }
                }
                #endif

                // Prefer IPv4 over IPv6 if we have both
                if (!string.IsNullOrEmpty(ipAddress))
                {
                    // If it's IPv6, try to find IPv4 alternative
                    if (ipAddress.Contains(":") && !ipAddress.StartsWith("::"))
                    {
                        // Try to get IPv4 from other sources
                        string ipv4 = GetIPv4FromRequest(request, serverIps);
                        if (!string.IsNullOrEmpty(ipv4))
                        {
                            return ipv4;
                        }
                    }
                    return ipAddress;
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail
                System.Diagnostics.Debug.WriteLine($"GetClientIpAddress error: {ex.Message}");
            }

            return "127.0.0.1";
        }

        /// <summary>
        /// Gets server IP addresses to filter them out
        /// </summary>
        private static string[] GetServerIpAddresses()
        {
            try
            {
                var serverIps = new System.Collections.Generic.List<string>();
                
                // Get local machine IPs
                IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress ip in hostEntry.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        serverIps.Add(ip.ToString());
                    }
                }
                
                // Add common local IPs
                serverIps.Add("127.0.0.1");
                serverIps.Add("::1");
                serverIps.Add("localhost");
                
                return serverIps.ToArray();
            }
            catch
            {
                return new string[] { "127.0.0.1", "::1", "localhost" };
            }
        }

        /// <summary>
        /// Checks if an IP address is a server IP
        /// </summary>
        private static bool IsServerIp(string ip, string[] serverIps)
        {
            if (string.IsNullOrEmpty(ip) || serverIps == null)
                return false;

            ip = ip.Trim();
            
            // Exact match with server IPs
            foreach (string serverIp in serverIps)
            {
                if (ip.Equals(serverIp, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            
            return false;
        }

        /// <summary>
        /// Checks if an IP address is a private network IP (LAN)
        /// </summary>
        private static bool IsPrivateNetworkIp(string ip)
        {
            if (string.IsNullOrEmpty(ip))
                return false;

            ip = ip.Trim();
            
            // Private network ranges:
            // 10.0.0.0 - 10.255.255.255
            // 172.16.0.0 - 172.31.255.255
            // 192.168.0.0 - 192.168.255.255
            
            if (ip.StartsWith("10.") || 
                ip.StartsWith("192.168.") ||
                (ip.StartsWith("172.") && IsInRange(ip, "172.16.0.0", "172.31.255.255")))
            {
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Checks if an IP is in a given range
        /// </summary>
        private static bool IsInRange(string ip, string startIp, string endIp)
        {
            try
            {
                IPAddress ipAddr = IPAddress.Parse(ip);
                IPAddress startAddr = IPAddress.Parse(startIp);
                IPAddress endAddr = IPAddress.Parse(endIp);
                
                byte[] ipBytes = ipAddr.GetAddressBytes();
                byte[] startBytes = startAddr.GetAddressBytes();
                byte[] endBytes = endAddr.GetAddressBytes();
                
                for (int i = 0; i < 4; i++)
                {
                    if (ipBytes[i] < startBytes[i] || ipBytes[i] > endBytes[i])
                        return false;
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if an IP address is a loopback address
        /// </summary>
        private static bool IsLoopback(string ip)
        {
            if (string.IsNullOrEmpty(ip))
                return true;

            ip = ip.Trim();
            
            // Check for IPv4 loopback
            if (ip == "127.0.0.1" || ip == "localhost" || ip.StartsWith("127."))
                return true;

            // Check for IPv6 loopback
            if (ip == "::1" || ip == "::ffff:127.0.0.1" || ip == "0:0:0:0:0:0:0:1")
                return true;

            return false;
        }

        /// <summary>
        /// Tries to get IPv4 address from request
        /// </summary>
        private static string GetIPv4FromRequest(HttpRequestBase request, string[] serverIps = null)
        {
            try
            {
                if (serverIps == null)
                {
                    serverIps = GetServerIpAddresses();
                }

                // Check all headers for IPv4
                foreach (string headerName in request.Headers.AllKeys)
                {
                    string headerValue = request.Headers[headerName];
                    if (!string.IsNullOrEmpty(headerValue) && headerValue.Contains(".") && !headerValue.Contains(":"))
                    {
                        // Looks like IPv4
                        IPAddress ip;
                        string ipString = headerValue.Split(',')[0].Trim();
                        if (IPAddress.TryParse(ipString, out ip) && 
                            ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                            !IsLoopback(ipString) &&
                            !IsServerIp(ipString, serverIps))
                        {
                            return ip.ToString();
                        }
                    }
                }

                // Check server variables
                foreach (string key in request.ServerVariables.AllKeys)
                {
                    string value = request.ServerVariables[key];
                    if (!string.IsNullOrEmpty(value) && value.Contains(".") && !value.Contains(":"))
                    {
                        IPAddress ip;
                        string ipString = value.Split(',')[0].Trim();
                        if (IPAddress.TryParse(ipString, out ip) && 
                            ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                            !IsLoopback(ipString) &&
                            !IsServerIp(ipString, serverIps))
                        {
                            return ip.ToString();
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
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