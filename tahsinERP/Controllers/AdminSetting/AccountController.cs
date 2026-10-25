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
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.ViewModels;
using tahsinERP.Filters;
using tahsinERP.I18n;

namespace tahsinERP.Controllers
{
    // Faqat Login/Logout anonim; qolgan amallar (Settings, rasm) login talab qiladi
    // Session'ga yozadi (vaqtinchalik parol bayrog'i) — boshqa controller'lar ReadOnly (CORE-P05)
    [System.Web.Mvc.SessionState(System.Web.SessionState.SessionStateBehavior.Required)]
    public class AccountController : Controller
    {
        /// <summary>
        /// Harakatsizlik chegarasi (daqiqa): shuncha vaqt so'rov bo'lmasa seans tugaydi. Chipta sliding — har so'rovda yangilanadi
        /// (FormsAuthentication chipta davri bo'yicha uzaytiradi, web.config'dagi timeout'dan qat'i nazar). Brauzerdagi taymer
        /// (assets/js/session-idle.js) ham shu qiymat bilan foydalanuvchini chiqaradi. "Meni saqlab qolish" — cookie brauzer
        /// yopilganda ham qoladi, lekin harakatsizlik chegarasi o'sha.
        /// </summary>
        public const int IdleTimeoutMinutes = 30;

        [AllowAnonymous]
        public ActionResult Login()
        {
            // agar already authenticated, redirect ga home
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(UserViewModel user)
        {
            if (string.IsNullOrWhiteSpace(user.UName))
            {
                ModelState.AddModelError("UName", L.T("Core_EnterUserName"));
                return View();
            }

            if (string.IsNullOrWhiteSpace(user.Password))
            {
                ModelState.AddModelError("Password", L.T("Core_EnterPassword"));
                return View();
            }

            string clientIp = Request.UserHostAddress;
            TimeSpan lockRemaining;
            if (LoginThrottle.Default.IsLockedOut(user.UName, clientIp, out lockRemaining))
            {
                ModelState.AddModelError("", L.T("Core_TooManyAttempts", Math.Ceiling(lockRemaining.TotalMinutes)));
                return View();
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                // Foydalanuvchi nomi bo'yicha: baza collation'i CI (Cyrillic_General_CI_AS), ToLower() indeksni o'chirib qo'yardi (CORE-P20)
                var loginName = user.UName.Trim();
                var getUser = db.USERS.FirstOrDefault(u => u.Uname == loginName && u.IsDeleted != true);

                if (Services.LicenseSync.PanelDeveloperLogin.ShouldAskPanel(getUser))
                {
                    // Firmada bunday foydalanuvchi yo'q (yoki u panel dasturchisining nusxasi) — boshqaruv panelidagi
                    // ERP dasturchisimi? Parol panelda tekshiriladi, muvaffaqiyatli bo'lsa DEVELOPER roli bilan nusxa.
                    var dev = Services.LicenseSync.PanelDeveloperLogin.Verify(loginName, user.Password);
                    if (dev.Status == Services.LicenseSync.LicensePanelClient.DevLoginStatus.Ok)
                    {
                        getUser = Services.LicenseSync.PanelDeveloperLogin.EnsureLocalUser(db, getUser, loginName, dev.FullName);
                    }
                    else
                    {
                        bool nowLocked = LoginThrottle.Default.RegisterFailure(user.UName, clientIp);
                        if (dev.Status == Services.LicenseSync.LicensePanelClient.DevLoginStatus.Locked)
                            ModelState.AddModelError("", L.T("Core_TooManyAttempts", 15));
                        else if (getUser == null)
                            ModelState.AddModelError("", L.T("Core_UserNotExists"));
                        else if (dev.Status == Services.LicenseSync.LicensePanelClient.DevLoginStatus.Unavailable)
                            ModelState.AddModelError("", L.T("Core_DevPanelUnavailable"));
                        else
                            ModelState.AddModelError("", nowLocked
                                ? L.T("Core_WrongPasswordLocked", LoginThrottle.Default.MaxFailures, LoginThrottle.Default.LockoutDuration.TotalMinutes.ToString("0"))
                                : L.T("Core_WrongPassword"));
                        return View();
                    }
                    LoginThrottle.Default.Reset(user.UName, clientIp);
                }
                else
                {
                    if (!getUser.IsActive)
                    {
                        ModelState.AddModelError("", L.T("Core_UserInactive"));
                        return View();
                    }

                    // Tekshirish password
                    bool needsRehash;
                    if (!PasswordHasher.Verify(user.Password, getUser.Password, getUser.HashCode, out needsRehash))
                    {
                        bool nowLocked = LoginThrottle.Default.RegisterFailure(user.UName, clientIp);
                        ModelState.AddModelError("", nowLocked
                            ? L.T("Core_WrongPasswordLocked", LoginThrottle.Default.MaxFailures, LoginThrottle.Default.LockoutDuration.TotalMinutes.ToString("0"))
                            : L.T("Core_WrongPassword"));
                        return View();
                    }
                    LoginThrottle.Default.Reset(user.UName, clientIp);

                    // Eski (SHA1+MD5) hash — yangi PBKDF2 formatiga o'tkazamiz
                    if (needsRehash)
                    {
                        string newSalt;
                        getUser.Password = PasswordHasher.Hash(user.Password, out newSalt);
                        getUser.HashCode = newSalt;
                        db.SaveChanges();
                    }
                }

                // ==== Litsenziya tekshiruvi ====
                // Admin/Developer litsenziyasiz ham kira oladi (birinchi kalitni
                // faollashtirish uchun). Company muddati tugagan bo'lsa, global
                // filter ularni License sahifasiga yo'naltiradi.
                bool canManageLicense = RoleHelper.CanManageLicense(getUser.Uname);
                Services.LicenseService.LicenseStatus licStatus = null;
                try
                {
                    licStatus = Services.LicenseService.GetStatus(db);
                }
                catch (Exception ex)
                {
                    // Sabab logga, foydalanuvchiga umumiy xabar. Admin baribir kiradi — License sahifasida ko'radi.
                    System.Diagnostics.Trace.TraceError("[License] Login paytida litsenziya tekshiruvi xatosi: " + ex);
                    if (!canManageLicense)
                    {
                        ModelState.AddModelError("", L.T("Core_LicenseCheckError"));
                        return View();
                    }
                }

                if (!canManageLicense)
                {
                    // Grace (faqat o'qish) davrida kirish mumkin — yozishni global filter bloklaydi
                    if (licStatus.Blocked)
                    {
                        ModelState.AddModelError("", licStatus.Message);
                        return View();
                    }

                    // Seat muddati ham soat muhri bilan himoyalangan vaqt bo'yicha tekshiriladi
                    var userCheck = Services.LicenseService.CheckUserLicense(db, getUser.ID, licStatus.EffectiveNow);
                    if (!userCheck.Ok)
                    {
                        ModelState.AddModelError("", userCheck.Message);
                        return View();
                    }
                }

                // Aniqlash session timeout based on "Keep me logged in"
                int sessionTimeout = IdleTimeoutMinutes;
                var expirationTime = DateTime.Now.AddMinutes(sessionTimeout);

                // Yaratish authentication ticket bilan username as identity
                var authTicket = new FormsAuthenticationTicket(
                    1,                          // version
                    getUser.Uname,              // username (identity name)
                    DateTime.Now,               // issue date
                    expirationTime,             // expiration
                    user.KeepMeSigned,          // persistent
                    // user ma'lumot (FullName) + ko'p-tenant rejimida tenant belgisi
                    tahsinERP.Configuration.Tenancy.TenantHttp.BindUserData(getUser.FullName ?? getUser.Uname)
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

                // Belgilash user image cookie
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

                // Log user entry immediately (IP will be logged, MAC might be 00:00:00:00:00:00 agar not in session yet)
                SetUserEntry(getUser.ID);

                // Vaqtinchalik parol (masalan yangi korxona admini) — avval parolni almashtirish
                if (getUser.MustChangePassword)
                {
                    Session[tahsinERP.Filters.MustChangePasswordAttribute.SessionKey] = true;
                    TempData["MustChangePassword"] = L.T("Core_TempPasswordLogin");
                    return RedirectToAction("Settings");
                }

                // Litsenziya bloklangan bo'lsa admin to'g'ridan-to'g'ri License sahifasiga (kalit kiritish uchun)
                if (canManageLicense && (licStatus == null || licStatus.Blocked))
                    return RedirectToAction("Index", "License");

                return RedirectToAction("Index", "Home");
            }
        }

        private void SetUserEntry(int userID)
        {
            try
            {
                // Olish client IP address dan HTTP request
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
                
                // MAC address - use standart value (device ID functionality disabled)
                string macAddress = "00:00:00:00:00:00";

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
                        // Ma'lumotlar bazasi xatoligini qayd etamiz, lekin login jarayonini to'xtatmaymiz
                        System.Diagnostics.Debug.WriteLine($"SetUserEntry bazaga yozishda xatolik: {dbEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Xatolikni qayd etamiz, lekin login jarayonini to'xtatmaymiz
                System.Diagnostics.Debug.WriteLine($"SetUserEntry xatoligi: {ex.Message}");
            }
        }

        /// <summary>Brauzer taymeri: foydalanuvchi sahifada ishlayapti (yozish, aylantirish), lekin so'rov yubormayapti — seans uzaytiriladi.</summary>
        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0)]
        public ActionResult KeepAlive()
        {
            return new HttpStatusCodeResult(204);
        }

        [AllowAnonymous]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();

            // Clear barcha cookies
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

        public ActionResult Settings()
        {
            // Faqat o'z profili: URL'dagi userName parametri e'tiborga olinmaydi
            string currentUserName = User.Identity.Name;

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
            // Foydalanuvchi faqat o'zini tahrirlaydi — formadagi UName'ga ishonilmaydi
            string currentUserName = User.Identity.Name;
            if (string.IsNullOrEmpty(currentUserName))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);
            }
            uvm.UName = currentUserName;

            int userPhotoMaxLength = Convert.ToInt32(ConfigurationManager.AppSettings["photoMaxSize"] ?? "2097152");

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var userToUpdate = db.USERS.FirstOrDefault(u => u.Uname == currentUserName);
                if (userToUpdate == null)
                {
                    return HttpNotFound();
                }

                // Har qanday o'zgartirish uchun amaldagi parol talab qilinadi
                bool ignoredRehash;
                if (string.IsNullOrEmpty(uvm.CurrentPassword) ||
                    !PasswordHasher.Verify(uvm.CurrentPassword, userToUpdate.Password, userToUpdate.HashCode, out ignoredRehash))
                {
                    ModelState.AddModelError("", L.T("Core_CurrentPasswordWrong"));
                    return View("Settings", uvm);
                }

                // Vaqtinchalik parol bilan kirgan bo'lsa — yangi parol majburiy va eskisidan farq qilishi kerak
                if (userToUpdate.MustChangePassword &&
                    (string.IsNullOrEmpty(uvm.Password) || uvm.Password == uvm.CurrentPassword))
                {
                    ModelState.AddModelError("", L.T("Core_ChangeTempPassword"));
                    return View("Settings", uvm);
                }

                // Yangilash password agar provided
                if (!string.IsNullOrEmpty(uvm.Password))
                {
                    if (uvm.Password != uvm.ConfirmPassword)
                    {
                        ModelState.AddModelError("", L.T("Core_PasswordMismatch"));
                        return View("Settings", uvm);
                    }
                    string keyNew;
                    userToUpdate.Password = PasswordHasher.Hash(uvm.Password, out keyNew);
                    userToUpdate.HashCode = keyNew;
                    userToUpdate.MustChangePassword = false;
                    Session.Remove(tahsinERP.Filters.MustChangePasswordAttribute.SessionKey);
                }

                // Yangilash other fields
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
                            ModelState.AddModelError("", L.T("Core_PhotoTooLarge"));
                            return View("Settings", uvm);
                        }

                        byte[] avatar = UploadHelper.ReadAll(Request.Files["userPhotoUpload"]);

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
                    ModelState.AddModelError("", L.T("Core_SaveRetryFailed"));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", L.T("Core_ErrorWithMessage", ex.Message));
                }

                return View("Settings", uvm);
            }
        }
    }
}
