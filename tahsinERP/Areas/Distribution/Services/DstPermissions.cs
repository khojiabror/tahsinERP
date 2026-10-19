using tahsinERP.Models;

namespace tahsinERP.Areas.Distribution.Services
{
    /// <summary>
    /// Maxsus (maydon/amal darajasidagi) huquqlar — PERMISSIONMODULES.Module (Controller = 'Dst'), migratsiya 012.
    /// Rol → Huquqlar sahifasida boshqa modullar kabi beriladi.
    /// </summary>
    public static class DstPermissions
    {
        /// <summary>Xarid narxi, tannarx va foydani ko'rish.</summary>
        public const string SeeCost = "DstSeeCost";

        /// <summary>Qarz limitidan oshgan / muddati o'tgan qarzli mijoz buyurtmasini tasdiqlash.</summary>
        public const string OverrideCredit = "DstOverrideCredit";

        /// <summary>Rol chegirma limitidan yuqori chegirma berish.</summary>
        public const string DiscountUnlimited = "DstDiscountUnlimited";

        public static bool Has(string userName, string permission)
        {
            if (string.IsNullOrEmpty(userName)) return false;
            return RoleHelper.IsAdminOrDeveloper(userName) || RoleHelper.IsViewPermitted(userName, permission);
        }
    }
}
