using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using tahsinERP.Models;

namespace tahsinERP.Services.Stock
{
    /// <summary>
    /// Ombor modulining umumiy yordamchilari (avval Incomes/Expenses/Inventory controller'larida nusxalangan —
    /// audit "Duplicates map"): foydalanuvchiga biriktirilgan omborlar, MES/Sotuv "faqat ko'rish" roli,
    /// standart valyuta, xato matni va yuklangan fayl.
    /// </summary>
    public static class WarehouseAccess
    {
        /// <summary>Foydalanuvchi MRP (mas'ul) bo'lgan faol omborlar.</summary>
        public static Task<List<int>> AssignedWarehouseIdsAsync(DBTHSNEntities db, string userName)
        {
            var userId = tahsinERP.Controllers.UserHelper.GetUserId(userName);
            return db.WAREHOUSES
                .Where(w => w.IsDeleted != true && w.MRP == userId)
                .OrderBy(w => w.ID)
                .Select(w => w.ID)
                .ToListAsync();
        }

        /// <summary>
        /// MES/ishlab chiqarish yoki Sotuv/CRM roli — ombor hujjatlarini faqat ko'radi (yaratish/tahrirlash yo'q).
        /// Rol nomi bo'yicha aniqlanadi; rollarda tizim kodi paydo bo'lganda faqat shu yerni o'zgartirish kifoya.
        /// </summary>
        public static bool IsMesOrSalesReadOnly(string userName)
        {
            var roles = RoleHelper.GetUserRoles(userName);
            if (roles == null)
                return false;
            return roles.Any(r => r != null && (r.Contains("MES") || r.Contains("Production") || r.Contains("Sales") || r.Contains("CRM")));
        }

        /// <summary>Standart valyuta: UZS, bo'lmasa birinchi valyuta.</summary>
        public static int DefaultCurrencyId(DBTHSNEntities db)
        {
            var c = db.CURRENCIES.AsNoTracking().FirstOrDefault(x => x.Shortname != null && x.Shortname.Trim().ToUpper() == "UZS")
                ?? db.CURRENCIES.AsNoTracking().OrderBy(x => x.ID).FirstOrDefault();
            return c != null ? c.ID : 1;
        }

        /// <summary>"Xatolik: ... | Ichki xatolik: ..." — ichki istisnolar bilan.</summary>
        public static string ErrorMessage(Exception ex)
        {
            var message = $"Xatolik: {ex.Message}";
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
                message += $" | Ichki xatolik: {inner.Message}";
            return message;
        }

        /// <summary>Yuklangan fayl baytlari; fayl bo'lmasa null.</summary>
        public static byte[] ReadUpload(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength <= 0)
                return null;
            using (var reader = new BinaryReader(file.InputStream))
                return reader.ReadBytes(file.ContentLength);
        }
    }
}
