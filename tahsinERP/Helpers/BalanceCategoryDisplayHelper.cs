using System;
using System.Collections.Generic;
using tahsinERP.Models;

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
        /// Balans varag'ining chap ustuni — aktivlar (Naqd, bank, debitor, zapas, OS).
        /// </summary>
        private static readonly HashSet<string> AssetColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Naqd pul",
            "Bank hisobi",
            "Xaridorlardan haqlarimiz",
            "Xomashyo",
            "Tayyor maxsulot",
            "Jihozlar",
            "Bino"
        };

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

            // Type 2: odatda passiv, lekin nomi aniq aktiv bo'lsa — aktiv ustuniga
            if (c.Type == 2)
            {
                var n = (c.Name ?? string.Empty).Trim();
                return AssetColumnNames.Contains(n);
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
                return "Aktiv";
            }

            return c.Type == 3 ? "Kapital" : "Passiv";
        }
    }
}
