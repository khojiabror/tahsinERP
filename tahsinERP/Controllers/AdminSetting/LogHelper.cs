using System;
using System.Data.Entity.Infrastructure;
using System.Web;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    public static class LogHelper
    {
        /// <summary>
        /// Foydalanuvchi amalini mijoz IP manzili bilan bazaga yozadi.
        /// </summary>
        public static void LogToDatabase(string userEmail, string controllerName, string actionName, string details = null, HttpContextBase httpContext = null)
        {
            try
            {
                // Always try ga get HttpContext - this is critical uchun IP va MAC
                HttpContextBase contextToUse = httpContext;
                if (contextToUse == null && HttpContext.Current != null)
                {
                    contextToUse = new HttpContextWrapper(HttpContext.Current);
                }

                // Olish client IP address dan HTTP request
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
                    
                // Olingan IP manzilini tekshiruv uchun yozib qo'yamiz
                System.Diagnostics.Debug.WriteLine($"LogHelper: IP manzili olindi: {clientIp}");
                }
                catch (Exception ipEx)
                {
                    // IP olishda xatolik bo'lsa, standart qiymat ishlatiladi
                    System.Diagnostics.Debug.WriteLine($"LogHelper: IP olishda xatolik: {ipEx.Message}");
                    clientIp = "127.0.0.1";
                }

                // MAC address - use standart value (device ID functionality disabled)
                string macAddress = "00:00:00:00:00:00";

                // Olish UserID - handle errors gracefully
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
                    // Foydalanuvchi topilmasa, log yozishni davom ettirish uchun 0 qiymati ishlatiladi
                    userId = 0;
                }

                // Ensure we have valid non-null values
                clientIp = clientIp ?? "127.0.0.1";
                macAddress = macAddress ?? "00:00:00:00:00:00";
                controllerName = controllerName ?? "Unknown";
                actionName = actionName ?? "Unknown";

                // faqat log agar we have valid ma'lumot (userId > 0 yoki at least userEmail)
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
                            
                            // Tekshiruv: yozuv muvaffaqiyatli saqlanganini tasdiqlash
                            if (saved > 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"LogHelper: Log muvaffaqiyatli saqlandi. Foydalanuvchi: {userEmail}, IP: {clientIp}, MAC: {macAddress}");
                            }
                        }
                        catch (RetryLimitExceededException retryEx)
                        {
                            // Ma'lumotlar bazasi qayta urinish limiti oshib ketdi
                            System.Diagnostics.Debug.WriteLine($"LogHelper: RetryLimitExceededException - {retryEx.Message}");
                        }
                        catch (System.Data.Entity.Validation.DbEntityValidationException valEx)
                        {
                            // Validatsiya xatosi tafsilotlarini yozish
                            var errorMessages = new System.Text.StringBuilder();
                            foreach (var validationError in valEx.EntityValidationErrors)
                            {
                                foreach (var error in validationError.ValidationErrors)
                                {
                                    errorMessages.AppendLine($"Maydon: {error.PropertyName}, Xatolik: {error.ErrorMessage}");
                                }
                            }
                            System.Diagnostics.Debug.WriteLine($"LogHelper validatsiya xatosi: {errorMessages}");
                        }
                        catch (Exception dbEx)
                        {
                            // Ma'lumotlar bazasi xatosini qayd etamiz, lekin exception tashlamaymiz
                            System.Diagnostics.Debug.WriteLine($"LogHelper ma'lumotlar bazasi xatosi: {dbEx.Message}");
                            if (dbEx.InnerException != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Ichki xatolik: {dbEx.InnerException.Message}");
                            }
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"LogHelper: Log yozuvi o'tkazib yuborildi - userId ({userId}) yoki userEmail ({userEmail}) noto'g'ri.");
                }
            }
            catch (Exception ex)
            {
                // Kutilmagan xatoni qayd etamiz, lekin exception tashlamaymiz - loglash ilovani ga'xtatmasligi kerak
                System.Diagnostics.Debug.WriteLine($"LogHelper kutilmagan xatolik: {ex.Message}");
            }
        }
    }
}
