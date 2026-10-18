using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Caching;
using System.Web.Security;

namespace tahsinERP.Models
{
    /// <summary>
    /// Permission va Role boshqaruv yordamchisi.
    ///
    /// Har bir foydalanuvchi uchun bitta "snapshot" keshlanadi: ID, rollar va barcha ruxsatlar
    /// (2 ta so'rov bilan quriladi). Avval har bir (user, modul) juftligi alohida 3 ta so'rov
    /// bilan olinardi — layout'dagi 165 ta tekshiruv login'dan keyin ~210 so'rovga olib kelardi.
    ///
    /// Ruxsatlar o'zgarganda <see cref="ClearAllCache"/> global versiyani oshiradi: eski snapshotlar
    /// keyingi murojaatda qayta quriladi (kesh bo'ylab aylanib chiqish shart emas).
    /// </summary>
    public static class RoleHelper
    {
        // Snapshot sliding muddati — faol foydalanuvchi uchun kesh yashab turadi
        private static readonly TimeSpan SnapshotSliding = TimeSpan.FromMinutes(20);
        // Ism/lavozim keshi (absolute)
        private const int CACHE_DURATION_MINUTES = 10;

        // Ruxsatlar/modullar o'zgarganda oshiriladi
        private static long _permissionsVersion;

        #region Cache Keys
        // T = tenant prefiksi ("" bitta-baza rejimida) — HttpRuntime.Cache barcha tenantlar uchun umumiy,
        // ikki tenantdagi bir xil "admin" huquqlari/modullari aralashib ketmasligi kerak.
        private static string T => tahsinERP.Configuration.Tenancy.TenantContext.CacheKeyPrefix;
        private static string SnapshotKey(string username) => "PermSnap_" + T + username.ToLowerInvariant();
        private static string ModulesKey => "PermModules" + T;
        private static string GetUserFullNameCacheKey(string username) => $"UserFullName_{T}{username}";
        private static string GetUserPositionCacheKey(string username) => $"UserPosition_{T}{username}";
        #endregion

        #region Cache Helper
        private static Cache AppCache => HttpRuntime.Cache;

        private static void SetCache(string key, object value)
        {
            AppCache.Insert(key, value, null, DateTime.Now.AddMinutes(CACHE_DURATION_MINUTES), Cache.NoSlidingExpiration);
        }

        private static object GetCache(string key)
        {
            return AppCache.Get(key);
        }
        #endregion

        #region Public Methods

        /// <summary>
        /// Foydalanuvchining to'liq ismini olish (cache bilan)
        /// </summary>
        public static string GetUserFullName(string username)
        {
            if (string.IsNullOrEmpty(username)) return string.Empty;

            var cacheKey = GetUserFullNameCacheKey(username);
            var cachedName = GetCache(cacheKey) as string;

            if (cachedName != null)
                return cachedName;

            // Auth ticket dan olishga harakat qilish
            try
            {
                var authCookie = HttpContext.Current?.Request.Cookies[FormsAuthentication.FormsCookieName];
                if (authCookie != null)
                {
                    var authTicket = FormsAuthentication.Decrypt(authCookie.Value);
                    var ticketName = authTicket != null ? tahsinERP.Configuration.Tenancy.TenantHttp.StripTenant(authTicket.UserData) : null;
                    if (!string.IsNullOrEmpty(ticketName))
                    {
                        SetCache(cacheKey, ticketName);
                        return ticketName;
                    }
                }
            }
            catch { }

            // DB dan olish
            using (var db = new DBTHSNEntities())
            {
                var user = db.USERS.FirstOrDefault(u => u.Uname == username && u.IsDeleted != true);
                var fullName = user?.FullName ?? username;
                SetCache(cacheKey, fullName);
                return fullName;
            }
        }

        /// <summary>
        /// Foydalanuvchining xodim sifatidagi lavozimini olish (cache bilan).
        /// Tizimdagi roli emas, HR_EMPLOYEES dagi lavozimi (HR_POSITIONS.Name) qaytariladi.
        /// Bog'lanish USERS.FullName == HR_EMPLOYEES.FullName bo'yicha amalga oshiriladi.
        /// </summary>
        public static string GetUserPosition(string username)
        {
            if (string.IsNullOrEmpty(username))
                return string.Empty;

            var cacheKey = GetUserPositionCacheKey(username);
            var cached = GetCache(cacheKey) as string;
            if (cached != null)
                return cached;

            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var user = db.USERS.FirstOrDefault(u => u.Uname == username && u.IsDeleted != true);
                    if (user != null && !string.IsNullOrEmpty(user.FullName))
                    {
                        var position = db.HR_EMPLOYEES
                            .Where(e => e.FullName == user.FullName
                                        && e.IsDeleted != true
                                        && e.IsActive
                                        && e.PositionID != null)
                            .Select(e => e.HR_POSITIONS.Name)
                            .FirstOrDefault();

                        if (!string.IsNullOrEmpty(position))
                        {
                            SetCache(cacheKey, position);
                            return position;
                        }
                    }
                }
            }
            catch { }

            // Lavozim topilmasa, bo'sh qiymatni keshlaymiz (DB ga qayta-qayta bormaslik uchun)
            SetCache(cacheKey, string.Empty);
            return string.Empty;
        }

        /// <summary>
        /// Foydalanuvchi ID si (o'chirilmagan foydalanuvchi; topilmasa null). Snapshot'dan — DB'ga bormaydi.
        /// </summary>
        public static int? GetUserId(string username)
        {
            var snap = GetSnapshot(username);
            return snap?.UserId;
        }

        /// <summary>
        /// Foydalanuvchi rollarini olish (cache bilan). Faol bo'lmagan foydalanuvchi uchun bo'sh.
        /// </summary>
        public static string[] GetUserRoles(string username)
        {
            var snap = GetSnapshot(username);
            return snap != null && snap.IsActive ? snap.Roles : new string[0];
        }

        /// <summary>
        /// Foydalanuvchida shu tizim kodli rol bormi (ROLES.SystemCode — rol NOMI emas, nom faqat ko'rsatish uchun).
        /// Kodlar: <see cref="tahsinERP.Services.Common.SystemCodes.Role"/>.
        /// </summary>
        public static bool HasRoleCode(string username, string roleCode)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(roleCode))
                return false;
            var snap = GetSnapshot(username);
            return snap != null && snap.IsActive && Array.IndexOf(snap.RoleCodes, roleCode) >= 0;
        }

        /// <summary>Developer roli egasimi (yashirin ma'muriy imtiyozlar uchun).</summary>
        public static bool IsDeveloper(string username)
        {
            return HasRoleCode(username, tahsinERP.Services.Common.SystemCodes.Role.Developer);
        }

        /// <summary>Admin yoki Developer roli (modullarda 13+ joyda qo'lda yozilgan tekshiruv).</summary>
        public static bool IsAdminOrDeveloper(string username)
        {
            return HasRoleCode(username, tahsinERP.Services.Common.SystemCodes.Role.Admin) || IsDeveloper(username);
        }

        /// <summary>Litsenziyani boshqara oladigan rol (Admin yoki Developer).</summary>
        public static bool CanManageLicense(string username)
        {
            return IsAdminOrDeveloper(username);
        }

        /// <summary>Ko'rish huquqi (Module nomi bo'yicha)</summary>
        public static bool IsViewPermitted(string username, string moduleName)
        {
            if (!tahsinERP.Services.Editions.EditionGate.IsControllerAllowed(moduleName)) return false; // paketda yo'q — menyu yashiriladi
            return Lookup(username, ModuleKey(moduleName)).View;
        }

        /// <summary>Ko'rish huquqi (Controller/Action bo'yicha)</summary>
        public static bool IsViewPermitted(string username, string controller, string action)
        {
            if (!tahsinERP.Services.Editions.EditionGate.IsControllerAllowed(controller)) return false; // paketda yo'q — menyu yashiriladi
            return Lookup(username, ActionKey(controller, action, null)).View;
        }

        /// <summary>Ko'rish huquqi (Controller/Action/Parameter bo'yicha)</summary>
        public static bool IsViewPermitted(string username, string controller, string action, string parameter)
        {
            if (!tahsinERP.Services.Editions.EditionGate.IsControllerAllowed(controller)) return false; // paketda yo'q — menyu yashiriladi
            return Lookup(username, ActionKey(controller, action, parameter)).View;
        }

        /// <summary>O'zgartirish huquqi (Module nomi bo'yicha)</summary>
        public static bool IsChangePermitted(string username, string moduleName)
        {
            return Lookup(username, ModuleKey(moduleName)).Change;
        }

        /// <summary>O'zgartirish huquqi (Controller/Action bo'yicha)</summary>
        public static bool IsChangePermitted(string username, string controller, string action)
        {
            return Lookup(username, ActionKey(controller, action, null)).Change;
        }

        /// <summary>O'zgartirish huquqi (Controller/Action/Parameter bo'yicha)</summary>
        public static bool IsChangePermitted(string username, string controller, string action, string parameter)
        {
            return Lookup(username, ActionKey(controller, action, parameter)).Change;
        }

        /// <summary>
        /// Action-darajadagi ruxsat; agar shu action uchun PERMISSIONMODULES yozuvi bo'lmasa,
        /// controller'ning istalgan yozuvidagi ruxsat olinadi (AJAX yordamchi amallar uchun).
        /// <see cref="tahsinERP.Filters.PermissionAttribute"/> ishlatadi.
        /// </summary>
        public static bool IsPermittedForAction(string username, string controller, string action, bool change)
        {
            var snap = GetSnapshot(username);
            if (snap == null || !snap.IsActive) return false;

            PermissionEntry entry;
            if (!snap.Entries.TryGetValue(ActionKey(controller, action, null), out entry) &&
                !snap.Entries.TryGetValue(ControllerKey(controller), out entry))
            {
                return false;
            }
            return change ? entry.Change : entry.View;
        }

        /// <summary>
        /// Foydalanuvchi uchun cache ni tozalash (rol/faollik o'zgarganda chaqiriladi)
        /// </summary>
        public static void ClearUserCache(string username)
        {
            if (string.IsNullOrEmpty(username)) return;

            AppCache.Remove(SnapshotKey(username));
            AppCache.Remove(GetUserFullNameCacheKey(username));
            AppCache.Remove(GetUserPositionCacheKey(username));
        }

        /// <summary>
        /// Barcha ruxsat keshini bekor qilish (rol/modul ruxsatlari o'zgarganda).
        /// Versiya oshiriladi — har bir foydalanuvchi snapshot'i keyingi murojaatda qayta quriladi.
        /// </summary>
        public static void ClearAllCache()
        {
            Interlocked.Increment(ref _permissionsVersion);
            AppCache.Remove(ModulesKey);
        }

        #endregion

        #region Snapshot

        private static PermissionEntry Lookup(string username, string key)
        {
            var snap = GetSnapshot(username);
            if (snap == null || !snap.IsActive || key == null) return PermissionEntry.None;

            PermissionEntry entry;
            return snap.Entries.TryGetValue(key, out entry) ? entry : PermissionEntry.None;
        }

        private static string ModuleKey(string module)
        {
            return string.IsNullOrEmpty(module) ? null : "M|" + module;
        }

        private static string ActionKey(string controller, string action, string parameter)
        {
            if (string.IsNullOrEmpty(controller) || string.IsNullOrEmpty(action)) return null;
            return "A|" + controller + "|" + action + "|" + (parameter ?? "");
        }

        private static string ControllerKey(string controller)
        {
            return string.IsNullOrEmpty(controller) ? null : "C|" + controller;
        }

        private static UserSnapshot GetSnapshot(string username)
        {
            if (string.IsNullOrEmpty(username)) return null;

            var key = SnapshotKey(username);
            long version = Interlocked.Read(ref _permissionsVersion);
            var snap = AppCache.Get(key) as UserSnapshot;
            if (snap != null && snap.Version == version)
                return snap;

            snap = BuildSnapshot(username, version);
            if (snap != null)
            {
                AppCache.Insert(key, snap, null, Cache.NoAbsoluteExpiration, SnapshotSliding);
            }
            return snap;
        }

        private static UserSnapshot BuildSnapshot(string username, long version)
        {
            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var user = db.USERS
                        .Where(u => u.Uname == username && u.IsDeleted != true)
                        .Select(u => new
                        {
                            u.ID,
                            u.IsActive,
                            Roles = u.ROLES.Select(r => new { r.ID, r.RName, r.SystemCode })
                        })
                        .FirstOrDefault();

                    if (user == null)
                        return null;

                    var roleIds = user.Roles.Select(r => r.ID).ToList();
                    var snap = new UserSnapshot
                    {
                        UserId = user.ID,
                        IsActive = user.IsActive,
                        Roles = user.Roles.Select(r => r.RName).ToArray(),
                        RoleCodes = user.Roles.Where(r => r.SystemCode != null).Select(r => r.SystemCode).ToArray(),
                        Version = version,
                        Entries = new Dictionary<string, PermissionEntry>(StringComparer.OrdinalIgnoreCase)
                    };

                    if (!user.IsActive || roleIds.Count == 0)
                        return snap;

                    // Bir nechta rol bo'lsa — eng keng huquq (avval faqat birinchi rol olinardi)
                    var grants = db.PERMISSIONS
                        .Where(p => roleIds.Contains(p.RoleID))
                        .GroupBy(p => p.PermissionModuleID)
                        .Select(g => new
                        {
                            ModuleID = g.Key,
                            View = g.Any(p => p.ViewPermit),
                            Change = g.Any(p => p.ChangePermit)
                        })
                        .ToList()
                        .ToDictionary(g => g.ModuleID, g => new PermissionEntry(g.View, g.Change));

                    // Modul nomi yoki controller/action dublikat bo'lsa — eng kichik ID (oldingi FirstOrDefault xatti-harakatiga yaqin)
                    foreach (var m in GetModules(db))
                    {
                        PermissionEntry grant;
                        if (!grants.TryGetValue(m.ID, out grant))
                            grant = PermissionEntry.None;

                        var moduleKey = ModuleKey(m.Module);
                        if (moduleKey != null && !snap.Entries.ContainsKey(moduleKey))
                            snap.Entries[moduleKey] = grant;

                        var actionKey = ActionKey(m.Controller, m.Action, string.IsNullOrEmpty(m.Parameter) ? null : m.Parameter);
                        if (actionKey != null && !snap.Entries.ContainsKey(actionKey))
                            snap.Entries[actionKey] = grant;

                        // Controller bo'yicha yig'ma (yozuvi yo'q AJAX amallar uchun)
                        var ctrlKey = ControllerKey(m.Controller);
                        if (ctrlKey != null)
                        {
                            PermissionEntry existing;
                            snap.Entries[ctrlKey] = snap.Entries.TryGetValue(ctrlKey, out existing)
                                ? new PermissionEntry(existing.View || grant.View, existing.Change || grant.Change)
                                : grant;
                        }
                    }

                    return snap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[RoleHelper] Snapshot qurishda xato: " + ex);
                return null;
            }
        }

        private static List<ModuleRow> GetModules(DBTHSNEntities db)
        {
            var cached = AppCache.Get(ModulesKey) as List<ModuleRow>;
            if (cached != null) return cached;

            var modules = db.PERMISSIONMODULES
                .OrderBy(m => m.ID)
                .Select(m => new ModuleRow
                {
                    ID = m.ID,
                    Module = m.Module,
                    Controller = m.Controller,
                    Action = m.Action,
                    Parameter = m.Parameter
                })
                .ToList();

            AppCache.Insert(ModulesKey, modules, null, DateTime.Now.AddMinutes(30), Cache.NoSlidingExpiration);
            return modules;
        }

        #endregion

        #region Helper Classes

        private sealed class ModuleRow
        {
            public int ID { get; set; }
            public string Module { get; set; }
            public string Controller { get; set; }
            public string Action { get; set; }
            public string Parameter { get; set; }
        }

        private struct PermissionEntry
        {
            public static readonly PermissionEntry None = new PermissionEntry(false, false);

            public PermissionEntry(bool view, bool change)
            {
                View = view;
                Change = change;
            }

            public bool View { get; }
            public bool Change { get; }
        }

        private sealed class UserSnapshot
        {
            public int UserId { get; set; }
            public bool IsActive { get; set; }
            public string[] Roles { get; set; }
            /// <summary>ROLES.SystemCode (ADMIN, DEVELOPER) — huquq tekshiruvi faqat shu bo'yicha.</summary>
            public string[] RoleCodes { get; set; }
            public long Version { get; set; }
            public Dictionary<string, PermissionEntry> Entries { get; set; }
        }

        #endregion
    }
}
