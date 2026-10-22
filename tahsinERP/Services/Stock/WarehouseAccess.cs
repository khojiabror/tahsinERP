using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using tahsinERP.Models;
using tahsinERP.I18n;

namespace tahsinERP.Services.Stock
{
    /// <summary>
    /// Ombor modulining umumiy yordamchilari (avval Incomes/Expenses/Inventory controller'larida nusxalangan —
    /// audit "Duplicates map"): foydalanuvchiga biriktirilgan omborlar, kirimlarni "faqat ko'rish" holati,
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
        /// Kirimlarni faqat ko'radi (yaratish/tahrirlash/o'chirish yo'q): "Incomes" moduli bo'yicha o'zgartirish huquqi yo'q.
        /// Avval rol NOMIDA "MES/Production/Sales/CRM" bo'lsa deb aniqlanardi — nom faqat ko'rsatish uchun, MES/Sotuv
        /// xodimlarining cheklovi endi Role → Huquqlar sahifasida (Incomes: o'zgartirish o'chirilgan) belgilanadi.
        /// </summary>
        public static bool IsIncomesReadOnly(string userName)
        {
            return !RoleHelper.IsChangePermitted(userName, "Incomes");
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
            var message = L.T("Wh_ErrorWithMessage", ex.Message);
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
                message += " | " + L.T("Wh_InnerErrorWithMessage", inner.Message);
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
