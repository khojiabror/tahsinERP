using System;
using System.Configuration;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using tahsinERP.Models;
using tahsinERP.ViewModels;

namespace tahsinERP.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        [DllImport("Iphlpapi.dll")]
        private static extern int SendARP(int dest, int host, ref long mac, ref int length);

        [DllImport("Ws2_32.dll")]
        private static extern int inet_addr(string ip);

        // Session timeout constants (in minutes)
        private const int DEFAULT_SESSION_TIMEOUT = 15;
        private const int KEEP_LOGGED_IN_TIMEOUT = 60; // 1 hour

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Login()
        {
            // If already authenticated, redirect to home
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(UserViewModel user)
        {
            if (string.IsNullOrWhiteSpace(user.UName))
            {
                ModelState.AddModelError("UName", "Foydalanuvchi nomini kiriting");
                return View();
            }

            if (string.IsNullOrWhiteSpace(user.Password))
            {
                ModelState.AddModelError("Password", "Kalit so'zni kiriting");
                return View();
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Find user by username (case-insensitive)
                var getUser = db.USERS.FirstOrDefault(u => 
                    u.Uname.ToLower() == user.UName.ToLower() && 
                    u.IsDeleted != true);

                if (getUser == null)
                {
                    ModelState.AddModelError("", "Bunday foydalanuvchi mavjud emas!");
                    return View();
                }

                if (!getUser.IsActive)
                {
                    ModelState.AddModelError("", "Foydalanuvchi faol emas. Administrator bilan bog'laning.");
                    return View();
                }

                // Validate password
                var hashCode = getUser.HashCode;
                var encodingPasswordString = Helper.EncodePassword(user.Password, hashCode);

                if (getUser.Password != encodingPasswordString)
                {
                    ModelState.AddModelError("", "Kalit so'zi noto'g'ri!");
                    return View();
                }

                // Determine session timeout based on "Keep me logged in"
                int sessionTimeout = user.KeepMeSigned ? KEEP_LOGGED_IN_TIMEOUT : DEFAULT_SESSION_TIMEOUT;
                var expirationTime = DateTime.Now.AddMinutes(sessionTimeout);

                // Create authentication ticket with username as identity
                var authTicket = new FormsAuthenticationTicket(
                    1,                          // version
                    getUser.Uname,              // username (identity name)
                    DateTime.Now,               // issue date
                    expirationTime,             // expiration
                    user.KeepMeSigned,          // persistent
                    getUser.FullName ?? getUser.Uname  // user data (FullName)
                );

                string encryptedTicket = FormsAuthentication.Encrypt(authTicket);

                var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket)
                {
                    HttpOnly = true,
                    Secure = FormsAuthentication.RequireSSL,
                    Path = FormsAuthentication.FormsCookiePath,
                    Domain = FormsAuthentication.CookieDomain,
                    Expires = user.KeepMeSigned ? expirationTime : DateTime.MinValue
                };
                Response.Cookies.Set(cookie);

                // Set user image cookie
                var image = db.USERIMAGES.FirstOrDefault(ui => ui.UserID == getUser.ID);
                if (image != null)
                {
                    var userImageCookie = new HttpCookie("UserImageId", image.ID.ToString())
                    {
                        HttpOnly = true,
                        Secure = FormsAuthentication.RequireSSL,
                        Path = "/",
                        Expires = user.KeepMeSigned ? expirationTime : DateTime.MinValue
                    };
                    Response.Cookies.Set(userImageCookie);
                }

                // Log user entry immediately (IP will be logged, MAC might be 00:00:00:00:00:00 if not in session yet)
                SetUserEntry(getUser.ID);

                return RedirectToAction("Index", "Home");
            }
        }

        private void SetUserEntry(int userID)
        {
            try
            {
                // Get client IP address from HTTP request
                string clientIp = "127.0.0.1";
                try
                {
                    if (Request != null)
                    {
                        clientIp = NetworkHelper.GetClientIpAddress(Request);
                        System.Diagnostics.Debug.WriteLine($"SetUserEntry: Got IP address: {clientIp}");
                    }
                }
                catch (Exception ipEx)
                {
                    System.Diagnostics.Debug.WriteLine($"SetUserEntry: Error getting IP: {ipEx.Message}");
                    clientIp = "127.0.0.1";
                }
                
                // Get device identifier from session or cookie (set by client-side JavaScript)
                string macAddress = "00:00:00:00:00:00";
                try
                {
                    // Try multiple ways to get device ID
                    string deviceId = null;
                    
                    // First try: Use Session property directly (Controller has this)
                    if (Session != null)
                    {
                        object sessionDeviceId = Session["DeviceId"];
                        if (sessionDeviceId != null && !string.IsNullOrEmpty(sessionDeviceId.ToString()))
                        {
                            deviceId = sessionDeviceId.ToString();
                        }
                    }
                    
                    // Second try: Use HttpContext property (Controller has this as HttpContextBase)
                    if (string.IsNullOrEmpty(deviceId) && HttpContext != null && HttpContext.Session != null)
                    {
                        object sessionDeviceId = HttpContext.Session["DeviceId"];
                        if (sessionDeviceId != null && !string.IsNullOrEmpty(sessionDeviceId.ToString()))
                        {
                            deviceId = sessionDeviceId.ToString();
                        }
                    }
                    
                    // Third try: Use System.Web.HttpContext.Current (static property)
                    if (string.IsNullOrEmpty(deviceId) && System.Web.HttpContext.Current != null && System.Web.HttpContext.Current.Session != null)
                    {
                        object sessionDeviceId = System.Web.HttpContext.Current.Session["DeviceId"];
                        if (sessionDeviceId != null && !string.IsNullOrEmpty(sessionDeviceId.ToString()))
                        {
                            deviceId = sessionDeviceId.ToString();
                        }
                    }
                    
                    // Fourth try: Get from cookie (backup method)
                    if (string.IsNullOrEmpty(deviceId) && Request != null && Request.Cookies != null)
                    {
                        HttpCookie deviceIdCookie = Request.Cookies["DeviceId"];
                        if (deviceIdCookie != null && !string.IsNullOrEmpty(deviceIdCookie.Value))
                        {
                            deviceId = deviceIdCookie.Value;
                            // Also store in session for future use
                            if (Session != null)
                            {
                                Session["DeviceId"] = deviceId;
                            }
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(deviceId))
                    {
                        macAddress = deviceId;
                        System.Diagnostics.Debug.WriteLine($"SetUserEntry: Got MAC address from session/cookie: {macAddress}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("SetUserEntry: Device ID not found in session or cookie");
                    }
                }
                catch (Exception sessionEx)
                {
                    System.Diagnostics.Debug.WriteLine($"SetUserEntry: Error getting device ID: {sessionEx.Message}");
                    macAddress = "00:00:00:00:00:00";
                }

                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    try
                    {
                        var userEntry = new USER_ENTRIES
                        {
                            UserID = userID,
                            DateTime = DateTime.Now,
                            IP = clientIp ?? "127.0.0.1",
                            MAC = macAddress ?? "00:00:00:00:00:00"
                        };

                        db.USER_ENTRIES.Add(userEntry);
                        db.SaveChanges();
                    }
                    catch (Exception dbEx)
                    {
                        // Log database error but don't fail login
                        System.Diagnostics.Debug.WriteLine($"SetUserEntry database error: {dbEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail login
                System.Diagnostics.Debug.WriteLine($"SetUserEntry error: {ex.Message}");
            }
        }

        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();

            // Clear all cookies
            if (Request.Cookies[FormsAuthentication.FormsCookieName] != null)
            {
                var cookie = new HttpCookie(FormsAuthentication.FormsCookieName)
                {
                    Expires = DateTime.Now.AddDays(-1)
                };
                Response.Cookies.Add(cookie);
            }

            if (Request.Cookies["UserImageId"] != null)
            {
                var imageCookie = new HttpCookie("UserImageId")
                {
                    Expires = DateTime.Now.AddDays(-1)
                };
                Response.Cookies.Add(imageCookie);
            }

            return RedirectToAction("Login");
        }

        public ActionResult GetUserImage(int id)
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var image = db.USERIMAGES.FirstOrDefault(ui => ui.ID == id);
                if (image != null)
                {
                    return File(image.Image, "image/png");
                }
                return HttpNotFound();
            }
        }

        public ActionResult Settings(string userName = null)
        {
            // Use current user if no username provided
            string currentUserName = userName ?? User.Identity.Name;

            if (string.IsNullOrEmpty(currentUserName))
            {
                return RedirectToAction("Login");
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var currentUser = db.USERS.FirstOrDefault(u => u.Uname == currentUserName);
                if (currentUser == null)
                {
                    return HttpNotFound();
                }

                var userViewModel = new UserViewModel
                {
                    ID = currentUser.ID,
                    UName = currentUser.Uname,
                    Email = currentUser.Email,
                    FullName = currentUser.FullName,
                    IsActive = currentUser.IsActive
                };

                var userimage = db.USERIMAGES.FirstOrDefault(ui => ui.UserID == currentUser.ID);
                if (userimage != null)
                {
                    ViewBag.Base64String = "data:image/png;base64," + Convert.ToBase64String(userimage.Image);
                }

                return View(userViewModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveSettings(UserViewModel uvm)
        {
            if (string.IsNullOrEmpty(uvm.UName))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            int userPhotoMaxLength = Convert.ToInt32(ConfigurationManager.AppSettings["photoMaxSize"] ?? "2097152");

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var userToUpdate = db.USERS.FirstOrDefault(u => u.Uname == uvm.UName);
                if (userToUpdate == null)
                {
                    return HttpNotFound();
                }

                // Update password if provided
                if (!string.IsNullOrEmpty(uvm.Password))
                {
                    var keyNew = Helper.GeneratePassword(10);
                    var password = Helper.EncodePassword(uvm.Password, keyNew);
                    userToUpdate.HashCode = keyNew;
                    userToUpdate.Password = password;
                }

                // Update other fields
                if (!string.IsNullOrEmpty(uvm.FullName))
                    userToUpdate.FullName = uvm.FullName;
                if (!string.IsNullOrEmpty(uvm.Email))
                    userToUpdate.Email = uvm.Email;

                // Handle user image
                var uImage = db.USERIMAGES.FirstOrDefault(ui => ui.UserID == userToUpdate.ID);

                try
                {
                    if (Request.Files["userPhotoUpload"] != null && Request.Files["userPhotoUpload"].ContentLength > 0)
                    {
                        if (Request.Files["userPhotoUpload"].InputStream.Length > userPhotoMaxLength)
                        {
                            ModelState.AddModelError("", "Suratni yuklab bo'lmadi, u 2 MB dan ortiq.");
                            return View("Settings", uvm);
                        }

                        byte[] avatar = new byte[Request.Files["userPhotoUpload"].InputStream.Length];
                        Request.Files["userPhotoUpload"].InputStream.Read(avatar, 0, avatar.Length);

                        if (uImage == null)
                        {
                            var uImageNew = new USERIMAGE
                            {
                                UserID = userToUpdate.ID,
                                Image = avatar
                            };
                            db.USERIMAGES.Add(uImageNew);
                        }
                        else
                        {
                            uImage.Image = avatar;
                            db.Entry(uImage).State = System.Data.Entity.EntityState.Modified;
                        }
                    }

                    db.SaveChanges();

                    LogHelper.LogToDatabase(User.Identity.Name, "AccountController", 
                        $"{User.Identity.Name} foydalanuvchi ma'lumotlarini saqladi", null, HttpContext);

                    return RedirectToAction("Index", "Home");
                }
                catch (RetryLimitExceededException)
                {
                    ModelState.AddModelError("", "O'zgarishlarni saqlab bo'lmadi. Qayta urinib ko'ring.");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Xatolik yuz berdi: {ex.Message}");
                }

                return View("Settings", uvm);
            }
        }

        /// <summary>
        /// Receives device identifier from client-side JavaScript and stores it in session and cookie
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public JsonResult SetDeviceId(string deviceId)
        {
            try
            {
                if (string.IsNullOrEmpty(deviceId))
                {
                    return Json(new { success = false, message = "Device ID is required" });
                }

                // Store device ID in session for use in logging
                if (Session != null)
                {
                    Session["DeviceId"] = deviceId;
                }
                
                // Also try to store in HttpContext.Session as backup
                if (HttpContext != null && HttpContext.Session != null)
                {
                    HttpContext.Session["DeviceId"] = deviceId;
                }
                
                // Also try to store in System.Web.HttpContext.Current.Session as backup
                if (System.Web.HttpContext.Current != null && System.Web.HttpContext.Current.Session != null)
                {
                    System.Web.HttpContext.Current.Session["DeviceId"] = deviceId;
                }
                
                // Also store in cookie as backup (persists across requests)
                var deviceIdCookie = new HttpCookie("DeviceId", deviceId)
                {
                    HttpOnly = false, // Allow JavaScript to read it
                    Expires = DateTime.Now.AddYears(1), // Long expiration
                    Path = "/"
                };
                Response.Cookies.Set(deviceIdCookie);

                System.Diagnostics.Debug.WriteLine($"SetDeviceId: Successfully stored device ID in session and cookie: {deviceId}");
                
                return Json(new { success = true, deviceId = deviceId });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetDeviceId: Error storing device ID: {ex.Message}");
                return Json(new { success = false, message = $"Error storing device ID: {ex.Message}" });
            }
        }
    }
}
