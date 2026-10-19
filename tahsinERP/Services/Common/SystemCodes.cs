using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Models;

namespace tahsinERP.Services.Common
{
    /// <summary>
    /// Tizim spravochnik qatorlarining barqaror kodlari (ustun <c>SystemCode</c>, migratsiya 001).
    /// Kod tizim qatorini FAQAT shu kod bo'yicha topadi; <c>Name</c> — faqat ko'rsatish uchun,
    /// foydalanuvchi uni erkin o'zgartiradi. SystemCode bor qatorni UI'da o'chirib bo'lmaydi.
    /// Yangi kod qo'shilsa: shu yerga + Database/Seed/100_Reference.sql + mavjud bazalar uchun NNN migratsiya.
    /// </summary>
    public static class SystemCodes
    {
        /// <summary>FIN_BALANCE_CATEGORIES.SystemCode</summary>
        public static class BalanceCategory
        {
            public const string Cash = "CASH";
            public const string Bank = "BANK";
            public const string Payables = "PAYABLES";
            public const string Receivables = "RECEIVABLES";
            public const string CharterCapital = "CHARTER_CAPITAL";
            public const string PayrollDebt = "PAYROLL_DEBT";
            public const string LogisticsDebt = "LOGISTICS_DEBT";
            public const string RawMaterials = "RAW_MATERIALS";
            public const string FinishedGoods = "FINISHED_GOODS";
            public const string Equipment = "EQUIPMENT";
            public const string Buildings = "BUILDINGS";
            public const string RecurringObligations = "RECURRING_OBLIGATIONS";

            /// <summary>
            /// Subledger kategoriyalari (AR/AP/oylik/logistika/doimiy harajat) dinamik hisoblanadi —
            /// kategoriya darajasidagi "OpeningBalance" snapshot ishlatilmaydi.
            /// </summary>
            private static readonly HashSet<string> Subledger = new HashSet<string>(StringComparer.Ordinal)
            {
                Payables, Receivables, PayrollDebt, LogisticsDebt, RecurringObligations
            };

            /// <summary>Type=2 bo'lib qolgan bo'lsa ham balansning Aktiv ustunida ko'rinadiganlar.</summary>
            private static readonly HashSet<string> AssetColumn = new HashSet<string>(StringComparer.Ordinal)
            {
                Cash, Bank, Receivables, RawMaterials, FinishedGoods, Equipment, Buildings
            };

            public static bool IsSubledger(FIN_BALANCE_CATEGORIES c) => c?.SystemCode != null && Subledger.Contains(c.SystemCode);
            public static bool IsAssetColumn(FIN_BALANCE_CATEGORIES c) => c?.SystemCode != null && AssetColumn.Contains(c.SystemCode);
            public static bool Is(FIN_BALANCE_CATEGORIES c, string code) => c != null && string.Equals(c.SystemCode, code, StringComparison.Ordinal);
        }

        /// <summary>FIN_CATEGORIES.SystemCode</summary>
        public static class FinCategory
        {
            /// <summary>Sotuv daromadi (Type=1) — sotuv invoysi posting'i.</summary>
            public const string Sales = "SALES";
            /// <summary>Xarid xarajati (Type=2) — xarid invoysi posting'i.</summary>
            public const string Purchase = "PURCHASE";
            /// <summary>Oylik / ish haqi (Type=2).</summary>
            public const string Payroll = "PAYROLL";
            /// <summary>Aksiyador hissalari (Type=1).</summary>
            public const string ShareholderContribution = "SHAREHOLDER_CONTRIBUTION";
            /// <summary>Xomashyo tashuvi (Type=2) — yo'l varaqasi to'lovi, xarid yuki.</summary>
            public const string MaterialTransport = "MATERIAL_TRANSPORT";
            /// <summary>Tayyor mahsulot tashuvi (Type=2) — yo'l varaqasi to'lovi, sotuv yuki.</summary>
            public const string FinishedGoodsTransport = "FG_TRANSPORT";
        }

        /// <summary>PRODUCT_CATEGORIES.SystemCode</summary>
        public static class ProductCategory
        {
            /// <summary>Jihoz — xomashyo bahosiga kirmaydi.</summary>
            public const string Equipment = "EQUIPMENT";
        }

        /// <summary>UNITS.SystemCode</summary>
        public static class Unit
        {
            public const string Pieces = "PCS";
            public const string Kilogram = "KG";
            public const string Tonne = "TONNE";
            public const string Gram = "GRAM";
            public const string Meter = "METER";
            public const string SquareMeter = "M2";
            public const string CubicMeter = "M3";
            public const string Liter = "LITER";
            public const string Millimeter = "MM";
            public const string Centimeter = "CM";
            public const string Pair = "PAIR";
        }

        /// <summary>Qator tizimnikimi (o'chirish taqiqlangan).</summary>
        public static bool IsSystem(string systemCode) => !string.IsNullOrEmpty(systemCode);

        public const string DeleteForbiddenMessage =
            "Bu tizim yozuvi (hisobotlar unga bog'langan) — uni o'chirib bo'lmaydi. Nomini o'zgartirishingiz mumkin.";
    }

    /// <summary>Tizim qatorlarini SystemCode bo'yicha topish (nom yoki qattiq ID bo'yicha emas).</summary>
    public static class SystemRefs
    {
        public static FIN_BALANCE_CATEGORIES BalanceCategory(DBTHSNEntities db, string code)
        {
            return db.FIN_BALANCE_CATEGORIES.FirstOrDefault(c => c.SystemCode == code && c.IsActive);
        }

        public static FIN_CATEGORIES FinCategory(DBTHSNEntities db, string code)
        {
            return db.FIN_CATEGORIES.FirstOrDefault(c => c.SystemCode == code && c.IsActive);
        }

        public static int? FinCategoryId(DBTHSNEntities db, string code)
        {
            return db.FIN_CATEGORIES.Where(c => c.SystemCode == code && c.IsActive).Select(c => (int?)c.ID).FirstOrDefault();
        }

        public static int? UnitId(DBTHSNEntities db, string code)
        {
            return db.UNITS.Where(u => u.SystemCode == code && (u.IsDeleted == null || u.IsDeleted == false))
                .Select(u => (int?)u.ID).FirstOrDefault();
        }

        public static int? ProductCategoryId(DBTHSNEntities db, string code)
        {
            return db.PRODUCT_CATEGORIES.Where(c => c.SystemCode == code).Select(c => (int?)c.ID).FirstOrDefault();
        }
    }
}
