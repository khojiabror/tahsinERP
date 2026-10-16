using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Caching;
using System.Web.Security;

namespace tahsinERP.Models
{
    /// <summary>
    /// Permission va Role boshqaruv yordamchisi
    /// Caching bilan ishlaydi - har safar DB ga bormaslik uchun
    /// </summary>
    public static class RoleHelper
    {
        // Cache davomiyligi (daqiqada)
        private const int CACHE_DURATION_MINUTES = 10;

        #region Cache Keys
        private static string GetUserRolesCacheKey(string username) => $"UserRoles_{username}";
        private static string GetUserFullNameCacheKey(string username) => $"UserFullName_{username}";
        private static string GetUserPositionCacheKey(string username) => $"UserPosition_{username}";
        private static string GetPermissionCacheKey(string username, string module) => $"Permission_{username}_{module}";
        private static string GetPermissionCacheKey(string username, string controller, string action) => $"Permission_{username}_{controller}_{action}";
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

        private static void RemoveCache(string key)
        {
            AppCache.Remove(key);
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
                    if (authTicket != null && !string.IsNullOrEmpty(authTicket.UserData))
                    {
                        SetCache(cacheKey, authTicket.UserData);
                        return authTicket.UserData;
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
        /// Foydalanuvchi rollarini olish (cache bilan)
        /// </summary>
        public static string[] GetUserRoles(string username)
        {
            if (string.IsNullOrEmpty(username))
                return new string[0];

            var cacheKey = GetUserRolesCacheKey(username);
            var cachedRoles = GetCache(cacheKey) as string[];
            
            if (cachedRoles != null)
                return cachedRoles;

            try
            {
                using (var db = new DBTHSNEntities())
                {
                    var user = db.USERS
                        .Include("ROLES")
                        .FirstOrDefault(u => u.Uname == username && u.IsDeleted != true && u.IsActive);

                    if (user?.ROLES != null && user.ROLES.Any())
                    {
                        var roles = user.ROLES.Select(r => r.RName).ToArray();
                        SetCache(cacheKey, roles);
                        return roles;
                    }
                }
            }
            catch { }

            return new string[0];
        }

        /// <summary>
        /// Foydalanuvchi ma'lum bir rolga egami (nomi bo'yicha, katta-kichik harf farqsiz).
        /// </summary>
        public static bool IsInRole(string username, string roleName)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(roleName))
                return false;
            var roles = GetUserRoles(username);
            return Array.Exists(roles, r => string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Developer roli egasimi (yashirin ma'muriy imtiyozlar uchun).</summary>
        public static bool IsDeveloper(string username)
        {
            return IsInRole(username, "Developer");
        }

        /// <summary>Litsenziyani boshqara oladigan rol (Admin yoki Developer).</summary>
        public static bool CanManageLicense(string username)
        {
            return IsInRole(username, "Admin") || IsInRole(username, "Developer");
        }

        /// <summary>
        /// Ko'rish huquqi borligini tekshirish (Module nomi bo'yicha)
        /// </summary>
        public static bool IsViewPermitted(string username, string moduleName)
        {
            if (string.IsNullOrEmpty(username)) return false;

            var cacheKey = GetPermissionCacheKey(username, moduleName);
            var cached = GetCache(cacheKey) as PermissionCache;
            
            if (cached != null)
                return cached.ViewPermit;

            var permission = GetPermissionFromDb(username, moduleName, null, null, null);
            // Cache'ga saqlashdan oldin ikkala property ham to'g'ri o'rnatilgan bo'lishi kerak
            SetCache(cacheKey, permission);
            return permission.ViewPermit;
        }

        /// <summary>
        /// Ko'rish huquqi borligini tekshirish (Controller/Action bo'yicha)
        /// </summary>
        public static bool IsViewPermitted(string username, string controller, string action)
        {
            if (string.IsNullOrEmpty(username)) return false;

            var cacheKey = GetPermissionCacheKey(username, controller, action);
            var cached = GetCache(cacheKey) as PermissionCache;
            
            if (cached != null)
                return cached.ViewPermit;

            var permission = GetPermissionFromDb(username, null, controller, action, null);
            // Cache'ga saqlashdan oldin ikkala property ham to'g'ri o'rnatilgan bo'lishi kerak
            SetCache(cacheKey, permission);
            return permission.ViewPermit;
        }

        /// <summary>
        /// Ko'rish huquqi borligini tekshirish (Controller/Action/Parameter bo'yicha)
        /// </summary>
        public static bool IsViewPermitted(string username, string controller, string action, string parameter)
        {
            if (string.IsNullOrEmpty(username)) return false;

            var cacheKey = $"Permission_{username}_{controller}_{action}_{parameter}";
            var cached = GetCache(cacheKey) as PermissionCache;
            
            if (cached != null)
                return cached.ViewPermit;

            var permission = GetPermissionFromDb(username, null, controller, action, parameter);
            // Cache'ga saqlashdan oldin ikkala property ham to'g'ri o'rnatilgan bo'lishi kerak
            SetCache(cacheKey, permission);
            return permission.ViewPermit;
        }

        /// <summary>
        /// O'zgartirish huquqi borligini tekshirish (Module nomi bo'yicha)
        /// </summary>
        public static bool IsChangePermitted(string username, string moduleName)
        {
            if (string.IsNullOrEmpty(username)) return false;

            var cacheKey = GetPermissionCacheKey(username, moduleName);
            var cached = GetCache(cacheKey) as PermissionCache;
            
            if (cached != null)
                return cached.ChangePermit;

            var permission = GetPermissionFromDb(username, moduleName, null, null, null);
            // Cache'ga saqlashdan oldin ikkala property ham to'g'ri o'rnatilgan bo'lishi kerak
            SetCache(cacheKey, permission);
            return permission.ChangePermit;
        }

        /// <summary>
        /// O'zgartirish huquqi borligini tekshirish (Controller/Action bo'yicha)
        /// </summary>
        public static bool IsChangePermitted(string username, string controller, string action)
        {
            if (string.IsNullOrEmpty(username)) return false;

            var cacheKey = GetPermissionCacheKey(username, controller, action);
            var cached = GetCache(cacheKey) as PermissionCache;
            
            if (cached != null)
                return cached.ChangePermit;

            var permission = GetPermissionFromDb(username, null, controller, action, null);
            // Cache'ga saqlashdan oldin ikkala property ham to'g'ri o'rnatilgan bo'lishi kerak
            SetCache(cacheKey, permission);
            return permission.ChangePermit;
        }

        /// <summary>
        /// O'zgartirish huquqi borligini tekshirish (Controller/Action/Parameter bo'yicha)
        /// </summary>
        public static bool IsChangePermitted(string username, string controller, string action, string parameter)
        {
            if (string.IsNullOrEmpty(username)) return false;

            var cacheKey = $"Permission_{username}_{controller}_{action}_{parameter}";
            var cached = GetCache(cacheKey) as PermissionCache;
            
            if (cached != null)
                return cached.ChangePermit;

            var permission = GetPermissionFromDb(username, null, controller, action, parameter);
            // Cache'ga saqlashdan oldin ikkala property ham to'g'ri o'rnatilgan bo'lishi kerak
            SetCache(cacheKey, permission);
            return permission.ChangePermit;
        }

        /// <summary>
        /// Foydalanuvchi uchun cache ni tozalash (permission o'zgarganda chaqiriladi)
        /// </summary>
        public static void ClearUserCache(string username)
        {
            if (string.IsNullOrEmpty(username)) return;

            var cache = HttpRuntime.Cache;
            var keysToRemove = new List<string>();
            
            var enumerator = cache.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var key = enumerator.Key.ToString();
                if (key.Contains(username))
                    keysToRemove.Add(key);
            }

            foreach (var key in keysToRemove)
            {
                cache.Remove(key);
            }
        }

        /// <summary>
        /// Barcha cache ni tozalash
        /// </summary>
        public static void ClearAllCache()
        {
            var cache = HttpRuntime.Cache;
            var keysToRemove = new List<string>();
            
            var enumerator = cache.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var key = enumerator.Key.ToString();
                if (key.StartsWith("Permission_") || key.StartsWith("UserRoles_") || key.StartsWith("UserFullName_"))
                    keysToRemove.Add(key);
            }

            foreach (var key in keysToRemove)
            {
                cache.Remove(key);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// DB dan permission olish
        /// </summary>
        private static PermissionCache GetPermissionFromDb(string username, string moduleName, string controller, string action, string parameter)
        {
            var result = new PermissionCache
            {
                ViewPermit = false,
                ChangePermit = false
            };

            try
            {
                using (var db = new DBTHSNEntities())
                {
                    // Foydalanuvchi rolini olish
                    var user = db.USERS
                        .Include("ROLES")
                        .FirstOrDefault(u => u.Uname == username && u.IsDeleted != true && u.IsActive);

                    if (user?.ROLES == null || !user.ROLES.Any())
                        return result;

                    var roleId = user.ROLES.First().ID;

                    // Permission moduleni topish
                    PERMISSIONMODULE module = null;

                    if (!string.IsNullOrEmpty(moduleName))
                    {
                        module = db.PERMISSIONMODULES.FirstOrDefault(pm => pm.Module == moduleName);
                    }
                    else if (!string.IsNullOrEmpty(controller) && !string.IsNullOrEmpty(action))
                    {
                        if (!string.IsNullOrEmpty(parameter))
                        {
                            module = db.PERMISSIONMODULES.FirstOrDefault(pm =>
                                pm.Controller == controller &&
                                pm.Action == action &&
                                pm.Parameter == parameter);
                        }
                        else
                        {
                            module = db.PERMISSIONMODULES.FirstOrDefault(pm =>
                                pm.Controller == controller &&
                                pm.Action == action &&
                                (pm.Parameter == null || pm.Parameter == ""));
                        }
                    }

                    if (module == null)
                        return result;

                    // Permission ni olish
                    var permission = db.PERMISSIONS.FirstOrDefault(p =>
                        p.RoleID == roleId &&
                        p.PermissionModuleID == module.ID);

                    if (permission != null)
                    {
                        // Explicitly set both properties to ensure they are set correctly
                        // PERMISSION modelida ViewPermit va ChangePermit bool (not nullable), shuning uchun ?? false kerak emas
                        result.ViewPermit = permission.ViewPermit;
                        result.ChangePermit = permission.ChangePermit;
                    }
                }
            }
            catch { }

            return result;
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// Permission cache uchun oddiy class
        /// </summary>
        [Serializable]
        private class PermissionCache
        {
            public bool ViewPermit { get; set; }
            public bool ChangePermit { get; set; }
            
            public PermissionCache()
            {
                ViewPermit = false;
                ChangePermit = false;
            }
        }

        #endregion
    }
}
