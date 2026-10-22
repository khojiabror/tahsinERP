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

        /// <summary>
        /// FIN_CATEGORIES.AnalyticsGroup (migratsiya 007) — tahliliy guruh; UNIQUE EMAS, foydalanuvchi kategoriya
        /// formasida tanlaydi. EBITDA (ValuationService) va fakt tannarx (TannarxFinanceCategoryClassifier) shu guruhga qaraydi.
        /// </summary>
        public static class FinAnalyticsGroup
        {
            /// <summary>Material (fakt tannarx PATH3 manbasi).</summary>
            public const string Material = "MATERIAL";
            /// <summary>Moliyaviy harakat (kredit, o'tkazma, naqd yechish, qarz) — xarajat emas.</summary>
            public const string Financing = "FINANCING";
            /// <summary>Foydani taqsimlash (dividend, aksiyador) — operatsion xarajat emas.</summary>
            public const string Dividend = "DIVIDEND";
            /// <summary>QQS — qaytariladigan soliq (EBITDA'da soliq sifatida qo'shib qo'yiladi).</summary>
            public const string Vat = "VAT";
            /// <summary>Kapital xarajat (asosiy vosita, uskuna, qurilish, investitsiya).</summary>
            public const string Capex = "CAPEX";
            /// <summary>Transport / logistika — yo'l varaqalari orqali hisoblanadi.</summary>
            public const string Logistics = "LOGISTICS";
            /// <summary>Ish haqi — HR ish haqi orqali hisoblanadi.</summary>
            public const string Payroll = "PAYROLL";
            /// <summary>Xarid — material ombor/PI orqali hisoblanadi.</summary>
            public const string Purchase = "PURCHASE";
            /// <summary>Soliq (QQS'dan boshqa).</summary>
            public const string Tax = "TAX";
            /// <summary>Bank xizmatlari.</summary>
            public const string Bank = "BANK";
            /// <summary>Boshqa xarajatlar.</summary>
            public const string Other = "OTHER";
            /// <summary>Kredit foizi.</summary>
            public const string Interest = "INTEREST";
            /// <summary>Amortizatsiya.</summary>
            public const string Depreciation = "DEPRECIATION";

            /// <summary>Kod → resx kaliti (tanlov ro'yxati tartibida).</summary>
            private static readonly KeyValuePair<string, string>[] DisplayKeys =
            {
                new KeyValuePair<string, string>(Material, "Core_FinGroup_Material"),
                new KeyValuePair<string, string>(Payroll, "Core_FinGroup_Payroll"),
                new KeyValuePair<string, string>(Purchase, "Core_FinGroup_Purchase"),
                new KeyValuePair<string, string>(Logistics, "Core_FinGroup_Logistics"),
                new KeyValuePair<string, string>(Tax, "Core_FinGroup_Tax"),
                new KeyValuePair<string, string>(Vat, "Core_FinGroup_Vat"),
                new KeyValuePair<string, string>(Bank, "Core_FinGroup_Bank"),
                new KeyValuePair<string, string>(Interest, "Core_FinGroup_Interest"),
                new KeyValuePair<string, string>(Depreciation, "Core_FinGroup_Depreciation"),
                new KeyValuePair<string, string>(Capex, "Core_FinGroup_Capex"),
                new KeyValuePair<string, string>(Dividend, "Core_FinGroup_Dividend"),
                new KeyValuePair<string, string>(Financing, "Core_FinGroup_Financing"),
                new KeyValuePair<string, string>(Other, "Core_FinGroup_Other"),
            };

            /// <summary>Kod → UI nomi joriy tilda (tanlov ro'yxati tartibida).</summary>
            public static IReadOnlyList<KeyValuePair<string, string>> All =>
                DisplayKeys.Select(x => new KeyValuePair<string, string>(x.Key, tahsinERP.I18n.L.T(x.Value))).ToList();

            public static bool IsValid(string code) => code != null && All.Any(x => x.Key == code);
        }

        /// <summary>PRODUCT_CATEGORIES.SystemCode</summary>
        public static class ProductCategory
        {
            /// <summary>Jihoz — xomashyo bahosiga kirmaydi.</summary>
            public const string Equipment = "EQUIPMENT";
        }

        /// <summary>PRODUCT_CATEGORIES.MaterialKind (migratsiya 007; UNIQUE emas, kategoriya formasida tanlanadi).</summary>
        public static class MaterialKind
        {
            /// <summary>Yog'och: narx brus uzunligi bo'yicha metrga aylantiriladi, norma/chiqindi jami faqat shulardan.</summary>
            public const string Wood = "WOOD";

            public static readonly IReadOnlyList<KeyValuePair<string, string>> All = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(Wood, "Yog'och"),
            };

            public static bool IsValid(string code) => code != null && All.Any(x => x.Key == code);
        }

        /// <summary>ROLES.SystemCode (migratsiya 007).</summary>
        public static class Role
        {
            /// <summary>Korxona administratori.</summary>
            public const string Admin = "ADMIN";
            /// <summary>Yashirin ma'muriy rol (faqat Developer ko'radi/o'zgartiradi).</summary>
            public const string Developer = "DEVELOPER";

            public static bool IsDeveloper(ROLE r) => r != null && string.Equals(r.SystemCode, Developer, StringComparison.Ordinal);
            public static bool IsAdminOrDeveloper(ROLE r) => r != null && (r.SystemCode == Admin || r.SystemCode == Developer);
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

        /// <summary>Foydalanuvchiga ko'rsatiladigan xabar — joriy tilda.</summary>
        public static string DeleteForbiddenMessage =>
            tahsinERP.I18n.L.T("Core_SystemRecordDeleteForbidden");
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

        public static ROLE Role(DBTHSNEntities db, string code)
        {
            return db.ROLES.FirstOrDefault(r => r.SystemCode == code && r.IsDeleted != true);
        }

        public static int? ProductCategoryId(DBTHSNEntities db, string code)
        {
            return db.PRODUCT_CATEGORIES.Where(c => c.SystemCode == code).Select(c => (int?)c.ID).FirstOrDefault();
        }
    }
}
