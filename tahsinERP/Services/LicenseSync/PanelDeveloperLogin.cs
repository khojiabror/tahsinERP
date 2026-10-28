using System;
using System.Linq;
using System.Security.Cryptography;
using tahsinERP.Helpers;
using tahsinERP.Models;
using tahsinERP.Services.Common;

namespace tahsinERP.Services.LicenseSync
{
    /// <summary>
    /// Boshqaruv panelidagi ERP dasturchilari (super-foydalanuvchilar) — o'z login/paroli bilan panelga ulangan
    /// istalgan firma va demo ERP'ga kiradi. Parol shu bazada saqlanmaydi: har kirishda panel tekshiradi
    /// (<see cref="LicenseSyncService.VerifyDeveloper"/>); muvaffaqiyatli bo'lsa firma bazasida
    /// USERS.IsPanelDeveloper = 1 nusxa (DEVELOPER roli bilan) yaratiladi/yangilanadi — ruxsat, log va hujjat
    /// muallifi oddiy foydalanuvchidagidek ishlaydi.
    /// Firmaning o'z foydalanuvchisi (bayroq 0) hech qachon panel orqali kirmaydi — bir xil nom bo'lsa firma ustun.
    /// </summary>
    public static class PanelDeveloperLogin
    {
        /// <summary>Bu foydalanuvchi uchun panelga so'rash kerakmi: lokal yozuv yo'q yoki u panel dasturchisi.</summary>
        public static bool ShouldAskPanel(USER local)
        {
            return local == null || local.IsPanelDeveloper;
        }

        /// <summary>
        /// Panel umuman ulanmagan (on-prem offline: URL yoki RegToken yo'q) — bu holatda noma'lum login oddiy
        /// "foydalanuvchi yo'q" bo'lib qoladi; qolgan Unavailable'lar (tarmoq, ro'yxatdan o'tish xatosi) alohida xabar.
        /// </summary>
        public static bool IsPanelNotConnected(LicensePanelClient.DevLoginResult r)
        {
            return r.Status == LicensePanelClient.DevLoginStatus.Unavailable
                && (r.Error == "panel_disabled" || r.Error == "not_registered");
        }

        /// <summary>Panelda tekshirish (sinxron controller'dan).</summary>
        public static LicensePanelClient.DevLoginResult Verify(string login, string password, string host)
        {
            try
            {
                // Login sahifasi uzoq kutmasin (noma'lum foydalanuvchi nomi ham shu yerdan o'tadi);
                // yangi firmada birinchi marta ro'yxatdan o'tish ham shu muddatga kiradi
                return LicenseSyncService.VerifyDeveloper(login, password, host, TimeSpan.FromSeconds(15));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[PanelDeveloperLogin] " + ex);
                return new LicensePanelClient.DevLoginResult { Status = LicensePanelClient.DevLoginStatus.Unavailable, Error = ex.Message };
            }
        }

        /// <summary>Firma bazasidagi nusxa: yo'q bo'lsa yaratiladi, bor bo'lsa faollashtiriladi va DEVELOPER roli kafolatlanadi.</summary>
        public static USER EnsureLocalUser(DBTHSNEntities db, USER existing, string login, string fullName)
        {
            var developerRole = db.ROLES.FirstOrDefault(r => r.SystemCode == SystemCodes.Role.Developer);
            if (developerRole == null)
                throw new InvalidOperationException("DEVELOPER roli bazada yo'q (ROLES.SystemCode).");

            var user = existing;
            if (user == null)
            {
                string salt;
                user = new USER
                {
                    Uname = login,
                    // Ishlatilmaydigan tasodifiy parol — lokal tekshiruv bu foydalanuvchi uchun chaqirilmaydi
                    Password = PasswordHasher.Hash(Convert.ToBase64String(RandomBytes(24)), out salt),
                    HashCode = salt,
                    IsActive = true,
                    IsDeleted = false,
                    MustChangePassword = false,
                    IsPanelDeveloper = true,
                    CompanyID = db.COMPANIES.OrderBy(c => c.ID).Select(c => (int?)c.ID).FirstOrDefault()
                };
                db.USERS.Add(user);
            }

            user.FullName = Truncate(string.IsNullOrWhiteSpace(fullName) ? login : fullName.Trim(), 50);
            user.IsActive = true;
            user.IsDeleted = false;
            if (!user.ROLES.Any(r => r.ID == developerRole.ID))
                user.ROLES.Add(developerRole);
            db.SaveChanges();

            RoleHelper.ClearUserCache(user.Uname);
            return user;
        }

        private static byte[] RandomBytes(int n)
        {
            var b = new byte[n];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(b);
            return b;
        }

        private static string Truncate(string s, int max) => s.Length > max ? s.Substring(0, max) : s;
    }
}
