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

            using (var db = new DBTHSNEntities())
            {
                var user = db.USERS.FirstOrDefault(u => 
                    u.IsDeleted != true && 
                    u.Uname == username);

                if (user != null)
                    return user.ID;

                throw new Exception($"Foydalanuvchi topilmadi: '{username}'");
            }
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
                    return user.FullName ?? user.Uname;

                throw new Exception("Foydalanuvchi topilmadi");
            }
        }
    }
}
