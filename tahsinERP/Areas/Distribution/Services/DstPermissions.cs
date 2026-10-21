using tahsinERP.Models;

namespace tahsinERP.Areas.Distribution.Services
{
    /// <summary>
    /// Maxsus (maydon/amal darajasidagi) huquqlar — PERMISSIONMODULES.Module (Controller = 'Dst'), migratsiya 013.
    /// Rol → Huquqlar sahifasida boshqa modullar kabi beriladi, lekin <b>Change</b> ustuni bilan: yangi rolga barcha
    /// modullarda View avtomatik beriladi (RoleController), shuning uchun View bilan tekshirilsa maxsus huquq ham
    /// beixtiyor berilib qolardi (05_FINDINGS K1-1). Mavjud View egalariga 027 Change ham qo'ydi.
    /// </summary>
    public static class DstPermissions
    {
        /// <summary>Xarid narxi, tannarx va foydani ko'rish.</summary>
        public const string SeeCost = "DstSeeCost";

        /// <summary>Qarz limitidan oshgan / muddati o'tgan qarzli mijoz buyurtmasini tasdiqlash.</summary>
        public const string OverrideCredit = "DstOverrideCredit";

        /// <summary>Rol chegirma limitidan yuqori chegirma berish.</summary>
        public const string DiscountUnlimited = "DstDiscountUnlimited";

        /// <summary>Hisobdan chiqarishni tasdiqlash / rad etish (025).</summary>
        public const string WriteOffApprove = "DstWriteOffApprove";

        public static bool Has(string userName, string permission)
        {
            if (string.IsNullOrEmpty(userName)) return false;
            if (RoleHelper.IsAdminOrDeveloper(userName)) return true;
            return Grants(false, tahsinERP.Services.Editions.EditionGate.IsControllerAllowed(permission),
                RoleHelper.IsChangePermitted(userName, permission));
        }

        /// <summary>Qaror qoidasi: Admin/Developer — doim; boshqalar — paketda bor va Change belgilangan (View yetmaydi).</summary>
        public static bool Grants(bool adminOrDeveloper, bool inEdition, bool changePermit) =>
            adminOrDeveloper || (inEdition && changePermit);
    }
}
