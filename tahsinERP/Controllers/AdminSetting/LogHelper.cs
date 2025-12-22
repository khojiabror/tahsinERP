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
            try
            {
                // Always try to get HttpContext - this is critical for IP and MAC
                HttpContextBase contextToUse = httpContext;
                if (contextToUse == null && HttpContext.Current != null)
                {
                    contextToUse = new HttpContextWrapper(HttpContext.Current);
                }

                // Get client IP address from HTTP request
                string clientIp = "127.0.0.1";
                try
                {
                    if (contextToUse != null && contextToUse.Request != null)
                    {
                        clientIp = NetworkHelper.GetClientIpAddress(contextToUse);
                    }
                    else if (HttpContext.Current != null && HttpContext.Current.Request != null)
                    {
                        clientIp = NetworkHelper.GetClientIpAddress(new HttpContextWrapper(HttpContext.Current));
                    }
                    
                    // Log what we got
                    System.Diagnostics.Debug.WriteLine($"LogHelper: Got IP address: {clientIp}");
                }
                catch (Exception ipEx)
                {
                    // If IP retrieval fails, use default
                    System.Diagnostics.Debug.WriteLine($"LogHelper: Error getting IP: {ipEx.Message}");
                    clientIp = "127.0.0.1";
                }

                // Get device identifier from session or cookie (set by client-side JavaScript)
                string macAddress = "00:00:00:00:00:00";
                try
                {
                    // Try multiple ways to get device ID
                    string deviceId = null;
                    
                    // First try: Use context session
                    if (contextToUse != null && contextToUse.Session != null)
                    {
                        object sessionDeviceId = contextToUse.Session["DeviceId"];
                        if (sessionDeviceId != null && !string.IsNullOrEmpty(sessionDeviceId.ToString()))
                        {
                            deviceId = sessionDeviceId.ToString();
                        }
                    }
                    
                    // Second try: Use HttpContext.Current session
                    if (string.IsNullOrEmpty(deviceId) && HttpContext.Current != null && HttpContext.Current.Session != null)
                    {
                        object sessionDeviceId = HttpContext.Current.Session["DeviceId"];
                        if (sessionDeviceId != null && !string.IsNullOrEmpty(sessionDeviceId.ToString()))
                        {
                            deviceId = sessionDeviceId.ToString();
                        }
                    }
                    
                    // Third try: Get from cookie (backup method)
                    if (string.IsNullOrEmpty(deviceId))
                    {
                        HttpContextBase requestContext = contextToUse;
                        if (requestContext == null && HttpContext.Current != null)
                        {
                            requestContext = new HttpContextWrapper(HttpContext.Current);
                        }
                        
                        if (requestContext != null && requestContext.Request != null && requestContext.Request.Cookies != null)
                        {
                            HttpCookie deviceIdCookie = requestContext.Request.Cookies["DeviceId"];
                            if (deviceIdCookie != null && !string.IsNullOrEmpty(deviceIdCookie.Value))
                            {
                                deviceId = deviceIdCookie.Value;
                                // Also store in session for future use
                                if (requestContext.Session != null)
                                {
                                    requestContext.Session["DeviceId"] = deviceId;
                                }
                            }
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(deviceId))
                    {
                        macAddress = deviceId;
                        System.Diagnostics.Debug.WriteLine($"LogHelper: Got MAC address from session/cookie: {macAddress}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("LogHelper: Device ID not found in session or cookie");
                    }
                }
                catch (Exception sessionEx)
                {
                    // If session access fails, use default
                    System.Diagnostics.Debug.WriteLine($"LogHelper: Error getting device ID: {sessionEx.Message}");
                    macAddress = "00:00:00:00:00:00";
                }

                // Get UserID - handle errors gracefully
                int userId = 0;
                try
                {
                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        userId = UserHelper.GetUserId(userEmail);
                    }
                }
                catch
                {
                    // If user not found, try to continue with 0 or skip logging
                    // For now, we'll use 0 if user not found
                    userId = 0;
                }

                // Ensure we have valid non-null values
                clientIp = clientIp ?? "127.0.0.1";
                macAddress = macAddress ?? "00:00:00:00:00:00";
                controllerName = controllerName ?? "Unknown";
                actionName = actionName ?? "Unknown";

                // Only log if we have valid data (userId > 0 or at least userEmail)
                if (userId > 0 || !string.IsNullOrEmpty(userEmail))
                {
                    using (var db = new DBTHSNEntities())
                    {
                        try
                        {
                            USERLOG log = new USERLOG
                            {
                                ControllerName = controllerName,
                                ActionName = actionName,
                                DateTime = DateTime.Now,
                                UserID = userId,
                                IP = clientIp,
                                MacAddr = macAddress
                            };

                            db.USERLOGS.Add(log);
                            int saved = db.SaveChanges();
                            
                            // Debug: verify save was successful
                            if (saved > 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"LogHelper: Successfully saved log for user {userEmail}, IP: {clientIp}, MAC: {macAddress}");
                            }
                        }
                        catch (RetryLimitExceededException retryEx)
                        {
                            // Database retry limit exceeded - log to console
                            System.Diagnostics.Debug.WriteLine($"LogHelper: RetryLimitExceededException - {retryEx.Message}");
                        }
                        catch (System.Data.Entity.Validation.DbEntityValidationException valEx)
                        {
                            // Validation error - log details
                            var errorMessages = new System.Text.StringBuilder();
                            foreach (var validationError in valEx.EntityValidationErrors)
                            {
                                foreach (var error in validationError.ValidationErrors)
                                {
                                    errorMessages.AppendLine($"Property: {error.PropertyName}, Error: {error.ErrorMessage}");
                                }
                            }
                            System.Diagnostics.Debug.WriteLine($"LogHelper validation error: {errorMessages.ToString()}");
                        }
                        catch (Exception dbEx)
                        {
                            // Log database error but don't throw
                            System.Diagnostics.Debug.WriteLine($"LogHelper database error: {dbEx.Message}");
                            if (dbEx.InnerException != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Inner exception: {dbEx.InnerException.Message}");
                            }
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"LogHelper: Skipping log - invalid userId ({userId}) and userEmail ({userEmail})");
                }
            }
            catch (Exception ex)
            {
                // Log any unexpected errors but don't throw - logging should never break the application
                System.Diagnostics.Debug.WriteLine($"LogHelper unexpected error: {ex.Message}");
            }
        }
    }
}
