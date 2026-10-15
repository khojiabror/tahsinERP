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
            
            // Mijoz IP manzilini olish
            string clientIp = "127.0.0.1";
            HttpContextBase contextToUse = httpContext;
            
            if (contextToUse == null && HttpContext.Current != null)
            {
                contextToUse = new HttpContextWrapper(HttpContext.Current);
            }
            
            if (contextToUse != null && contextToUse.Request != null)
            {
                clientIp = NetworkHelper.GetClientIpAddress(contextToUse);
            }
            
            // MAC manzil - standart qiymat (qurilma ID funksiyasi o'chirilgan)
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

                // Mijoz IP manzilini olish
                string clientIp = "127.0.0.1";
                HttpContextBase contextToUse = httpContext;
                
                if (contextToUse == null && HttpContext.Current != null)
                {
                    contextToUse = new HttpContextWrapper(HttpContext.Current);
                }
                
                if (contextToUse != null && contextToUse.Request != null)
                {
                    clientIp = NetworkHelper.GetClientIpAddress(contextToUse);
                }
                
                // MAC manzil - standart qiymat (qurilma ID funksiyasi o'chirilgan)
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

                LogHelper.LogToDatabase(userEmail, "CookieHelper", "Confirm",
                    "Qurilma tasdiqlash yozuvi qo'shildi.");
            }
        }
    }
}
