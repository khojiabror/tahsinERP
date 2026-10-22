using System;
using System.Collections.Generic;
using tahsinERP.Models;
using tahsinERP.Services.Common;
using tahsinERP.I18n;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// Balans hisoboti va opening balansda kategoriya qaysi ustunda (Aktiv / Passiv) ko'rinishi kerakligini aniqlash.
    /// Ba'zi bazalarda operatsion aktivlar (debitor, ombor, OS) noto'g'ri Type=2 (Passiv) bo'lib qolgan;
    /// bu yordamchi ularni Aktiv ustuniga qaytaradi.
    /// </summary>
    public static class BalanceCategoryDisplayHelper
    {
        /// <summary>
        /// true = balans hisobotida "Aktivlar" ustunida ko'rsatish; false = "Passivlar va Kapital".
        /// </summary>
        public static bool IsAssetColumn(FIN_BALANCE_CATEGORIES c)
        {
            if (c == null)
            {
                return false;
            }

            if (c.Type == 1)
            {
                return true;
            }

            if (c.Type == 3)
            {
                return false;
            }

            // Type 2: odatda passiv, lekin aktiv tizim kategoriyasi bo'lsa (naqd, bank, debitor, zapas, OS) — aktiv ustuniga
            if (c.Type == 2)
            {
                return SystemCodes.BalanceCategory.IsAssetColumn(c);
            }

            return c.Type == 1;
        }

        /// <summary>
        /// Opening balans / ro'yxatlar uchun qisqa tur nomi.
        /// </summary>
        public static string GetSideLabel(FIN_BALANCE_CATEGORIES c)
        {
            if (c == null)
            {
                return string.Empty;
            }

            if (IsAssetColumn(c))
            {
                return L.T("Fin_Asset");
            }

            return c.Type == 3 ? L.T("Fin_Equity") : L.T("Fin_Liability");
        }
    }
}
