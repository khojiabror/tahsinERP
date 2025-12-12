using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    public static class CookieHelper
    {
        public static bool IsConfirmed(string userEmail, HttpContextBase httpContext = null)
        {
            var UserID = UserHelper.GetUserId(userEmail);
            
            // Get client IP address
            string clientIp = "127.0.0.1";
            if (httpContext != null && httpContext.Request != null)
            {
                clientIp = NetworkHelper.GetClientIpAddress(httpContext);
            }
            else if (HttpContext.Current != null)
            {
                clientIp = NetworkHelper.GetClientIpAddress(new HttpContextWrapper(HttpContext.Current));
            }
            
            // MAC address cannot be retrieved from client in web applications
            string macAddress = "00:00:00:00:00:00";

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var confirmation = db.COOKIES_CONFIRMATION.Where(c => c.UserID == UserID && c.MacAddress == macAddress && c.IPAddress == clientIp).FirstOrDefault();

                if (confirmation == null)
                {
                    return false;
                }
                else
                    return true;
            }
        }

        public static void Confirm(string userEmail, HttpContextBase httpContext = null)
        {
            using (DBTHSNEntities dB = new DBTHSNEntities())
            {
                var UserID = UserHelper.GetUserId(userEmail);

                // Get client IP address
                string clientIp = "127.0.0.1";
                if (httpContext != null && httpContext.Request != null)
                {
                    clientIp = NetworkHelper.GetClientIpAddress(httpContext);
                }
                else if (HttpContext.Current != null)
                {
                    clientIp = NetworkHelper.GetClientIpAddress(new HttpContextWrapper(HttpContext.Current));
                }
                
                // MAC address cannot be retrieved from client in web applications
                string macAddress = "00:00:00:00:00:00";

                var cookieConfirm = new COOKIES_CONFIRMATION
                {
                    UserID = UserID,
                    IssuedDT = DateTime.Now,
                    MacAddress = macAddress,
                    IPAddress = clientIp,
                    Confirmation = true
                };

                dB.COOKIES_CONFIRMATION.Add(cookieConfirm);
                dB.SaveChanges();
            }
        }
    }
}
