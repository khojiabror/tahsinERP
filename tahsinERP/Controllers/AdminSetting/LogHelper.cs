using System;
using System.Data.Entity.Infrastructure;
using System.Web;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    public static class LogHelper
    {
        /// <summary>
        /// Logs user action to database with client IP address
        /// </summary>
        public static void LogToDatabase(string userEmail, string controllerName, string actionName, string details = null, HttpContextBase httpContext = null)
        {
            using (var db = new DBTHSNEntities())
            {
                try
                {
                    // Get client IP address from HTTP request
                    string clientIp = "127.0.0.1";
                    if (httpContext != null && httpContext.Request != null)
                    {
                        clientIp = NetworkHelper.GetClientIpAddress(httpContext);
                    }
                    else if (HttpContext.Current != null)
                    {
                        // Fallback to current HttpContext if available
                        clientIp = NetworkHelper.GetClientIpAddress(new HttpContextWrapper(HttpContext.Current));
                    }

                    // MAC address cannot be retrieved from client in web applications
                    // Return default value
                    string macAddress = "00:00:00:00:00:00";

                    USERLOG log = new USERLOG
                    {
                        ControllerName = controllerName,
                        ActionName = actionName,
                        DateTime = DateTime.Now,
                        UserID = UserHelper.GetUserId(userEmail),
                        IP = clientIp,
                        MacAddr = macAddress
                    };

                    db.USERLOGS.Add(log);
                    db.SaveChanges(); // Save changes to the database
                }
                catch (RetryLimitExceededException)
                {
                    Console.WriteLine("Error in LogHelper");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error: {ex.Message}");
                }
            }
        }
    }
}
