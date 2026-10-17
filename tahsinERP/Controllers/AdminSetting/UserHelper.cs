using System;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    public static class UserHelper
    {
        /// <summary>
        /// Gets user ID by username (User.Identity.Name)
        /// </summary>
        public static int GetUserId(string username)
        {
            if (string.IsNullOrEmpty(username))
                throw new ArgumentException("Username cannot be null or empty");

            // RoleHelper snapshot keshidan — har chaqiruvda yangi DbContext ochilmaydi
            var id = RoleHelper.GetUserId(username);
            if (id.HasValue)
                return id.Value;

            throw new Exception($"Foydalanuvchi topilmadi: '{username}'");
        }

        /// <summary>
        /// Gets user by username
        /// </summary>
        public static USER GetUser(string username)
        {
            if (string.IsNullOrEmpty(username))
                return null;

            using (var db = new DBTHSNEntities())
            {
                return db.USERS.FirstOrDefault(u => 
                    u.IsDeleted != true && 
                    u.IsActive && 
                    u.Uname == username);
            }
        }

        /// <summary>
        /// Gets full name by username yoki userID
        /// </summary>
        public static string GetFullName(string username = null, int? userID = null)
        {
            if (string.IsNullOrEmpty(username) && userID == null)
                throw new ArgumentException("Username va UserID ham bir vaqtda NULL bo'lishi mumkin emas");

            // Ro'yxatlarda har qator uchun chaqiriladi — 10 daqiqalik kesh (avval har safar yangi DbContext)
            string cacheKey = userID.HasValue ? "UserFullNameById_" + userID.Value : "UserFullNameByName_" + username;
            var cached = System.Web.HttpRuntime.Cache.Get(cacheKey) as string;
            if (cached != null)
                return cached;

            using (var db = new DBTHSNEntities())
            {
                USER user = null;

                if (userID.HasValue)
                {
                    user = db.USERS.FirstOrDefault(u => u.IsDeleted != true && u.ID == userID.Value);
                }
                else if (!string.IsNullOrEmpty(username))
                {
                    user = db.USERS.FirstOrDefault(u => u.IsDeleted != true && u.Uname == username);
                }

                if (user != null)
                {
                    var name = user.FullName ?? user.Uname;
                    System.Web.HttpRuntime.Cache.Insert(cacheKey, name, null, DateTime.Now.AddMinutes(10), System.Web.Caching.Cache.NoSlidingExpiration);
                    return name;
                }

                throw new Exception("Foydalanuvchi topilmadi");
            }
        }
    }
}
