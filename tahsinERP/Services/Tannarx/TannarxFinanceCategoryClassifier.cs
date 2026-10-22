using System.Collections.Generic;
using System.Linq;
using tahsinERP.I18n;
using tahsinERP.Services.Common;

namespace tahsinERP.Services.Tannarx
{
    internal enum TannarxFinanceBucket
    {
        /// <summary>Tannarxga kirmaydi (sababi Reason da).</summary>
        Excluded = 0,
        Material = 1,
        TaxBank = 2,
        /// <summary>Tahliliy guruhi "Boshqa" (OTHER) kategoriya.</summary>
        Other = 3,
        /// <summary>Guruhi tannarx uchun aniqlanmagan, boshqa joyda hisoblanmaydigan xarajat — "Boshqa" ga qo'shiladi.</summary>
        OtherAuto = 4
    }

    internal class TannarxFinanceClass
    {
        public TannarxFinanceBucket Bucket { get; set; }
        public string Reason { get; set; }
        public bool IsManual { get; set; }
    }

    /// <summary>
    /// FIN_CATEGORIES (faqat xarajat, Type = 2) ni fakt tannarx guruhlariga ajratadi — kategoriyaning tahliliy guruhi
    /// (FIN_CATEGORIES.AnalyticsGroup, migratsiya 007) bo'yicha; NOM bo'yicha emas (nom faqat ko'rsatish uchun).
    /// Tamoyil: oydagi BARCHA xarajat tannarxga tushadi, faqat boshqa joyda allaqachon hisoblanadigan yoki xarajat
    /// bo'lmagan pul harakati chiqariladi:
    /// - FINANCING (kredit / o'tkazma / naqd yechish / qarz) — xarajat emas;
    /// - DIVIDEND (dividend, aksiyador) — foydani taqsimlash;
    /// - VAT (QQS) — tannarx emas (qaytariladigan soliq);
    /// - CAPEX (asosiy vosita / uskuna / qurilish / investitsiya) — kapital xarajat;
    /// - PURCHASE (xarid / ta'minotchi) — material PATH0/1/2 (ombor, PI) orqali;
    /// - LOGISTICS (transport) — yo'l varaqalari (F_WAYBILLS) orqali;
    /// - PAYROLL (oylik / ish haqi) — HR ish haqi (ishbay va soatbay) orqali.
    /// MATERIAL -> Material (PATH3), TAX/BANK -> soliq va bank, OTHER -> Boshqa. Har kategoriya faqat BITTA guruhga tushadi;
    /// tannarx uchun guruhi aniqlanmagan (bo'sh, INTEREST, DEPRECIATION) bola kategoriya ota kategoriyasining guruhini oladi;
    /// hech qayerga tushmaganlari OtherAuto ("Boshqa").
    /// </summary>
    internal static class TannarxFinanceCategoryClassifier
    {
        internal const byte ExpenseCategoryType = 2;

        /// <summary>Tannarxdan chiqariladigan guruhlar va sababi (resx kaliti; ko'rsatishda joriy tilga o'giriladi).</summary>
        private static readonly Dictionary<string, string> ExclusionReasons = new Dictionary<string, string>
        {
            [SystemCodes.FinAnalyticsGroup.Financing] = "Mes_Tan_Excl_Financing",
            [SystemCodes.FinAnalyticsGroup.Dividend] = "Mes_Tan_Excl_Dividend",
            [SystemCodes.FinAnalyticsGroup.Vat] = "Mes_Tan_Excl_Vat",
            [SystemCodes.FinAnalyticsGroup.Capex] = "Mes_Tan_Excl_Capex",
            [SystemCodes.FinAnalyticsGroup.Logistics] = "Mes_Tan_Excl_Logistics",
            [SystemCodes.FinAnalyticsGroup.Payroll] = "Mes_Tan_Excl_Payroll",
            [SystemCodes.FinAnalyticsGroup.Purchase] = "Mes_Tan_Excl_Purchase"
        };

        internal class CategoryRow
        {
            public int ID { get; set; }
            /// <summary>Faqat ko'rsatish uchun.</summary>
            public string Name { get; set; }
            public byte Type { get; set; }
            public int? ParentID { get; set; }
            /// <summary>FIN_CATEGORIES.AnalyticsGroup — sinf shundan aniqlanadi.</summary>
            public string AnalyticsGroup { get; set; }
        }

        /// <summary>O'z tahliliy guruhi bo'yicha sinf; null — tannarx uchun aniqlanmadi (ota kategoriyaga qaraladi).</summary>
        private static TannarxFinanceClass ClassifyOwn(string analyticsGroup)
        {
            if (string.IsNullOrEmpty(analyticsGroup))
                return null;
            if (ExclusionReasons.TryGetValue(analyticsGroup, out var reasonKey))
                return new TannarxFinanceClass { Bucket = TannarxFinanceBucket.Excluded, Reason = L.T(reasonKey) };
            switch (analyticsGroup)
            {
                case SystemCodes.FinAnalyticsGroup.Material:
                    return new TannarxFinanceClass { Bucket = TannarxFinanceBucket.Material, Reason = L.T("Mes_Materials") };
                case SystemCodes.FinAnalyticsGroup.Tax:
                case SystemCodes.FinAnalyticsGroup.Bank:
                    return new TannarxFinanceClass { Bucket = TannarxFinanceBucket.TaxBank, Reason = L.T("Mes_Tan_Reason_TaxBank") };
                case SystemCodes.FinAnalyticsGroup.Other:
                    return new TannarxFinanceClass { Bucket = TannarxFinanceBucket.Other, Reason = L.T("Mes_Tan_Reason_Other") };
                default:
                    return null; // INTEREST, DEPRECIATION — tannarx uchun alohida guruh yo'q
            }
        }

        internal static string BucketTitle(TannarxFinanceBucket bucket)
        {
            switch (bucket)
            {
                case TannarxFinanceBucket.Material: return L.T("Mes_Tan_Bucket_Material");
                case TannarxFinanceBucket.TaxBank: return L.T("Mes_Tan_Bucket_TaxBank");
                case TannarxFinanceBucket.Other: return L.T("Mes_Tan_Bucket_Other");
                case TannarxFinanceBucket.OtherAuto: return L.T("Mes_Tan_Bucket_OtherAuto");
                default: return L.T("Mes_Tan_Bucket_Exclude");
            }
        }

        /// <summary>
        /// Har xarajat kategoriyasi (Type = 2) → sinf. Qo'lda belgilangan guruh (TANNARX_FIN_CATEGORY_MAP) tahliliy guruhdan ustun;
        /// kategoriyaning o'zida hech narsa bo'lmasa eng yaqin ota kategoriyaning belgisi yoki tahliliy guruhi olinadi.
        /// </summary>
        internal static Dictionary<int, TannarxFinanceClass> Classify(IList<CategoryRow> categories, IDictionary<int, TannarxFinanceBucket> manual = null)
        {
            var byId = categories.ToDictionary(c => c.ID);
            var own = categories.ToDictionary(c => c.ID, c =>
            {
                if (manual != null && manual.TryGetValue(c.ID, out var bucket))
                {
                    return new TannarxFinanceClass
                    {
                        Bucket = bucket,
                        Reason = bucket == TannarxFinanceBucket.Excluded ? L.T("Mes_Tan_Reason_ManualExcluded") : L.T("Mes_Tan_Reason_Manual"),
                        IsManual = true
                    };
                }
                return ClassifyOwn(c.AnalyticsGroup);
            });
            var result = new Dictionary<int, TannarxFinanceClass>();

            foreach (var cat in categories)
            {
                if (cat.Type != ExpenseCategoryType)
                    continue;

                TannarxFinanceClass resolved = null;
                var current = cat;
                for (var depth = 0; current != null && depth < 10; depth++)
                {
                    resolved = own[current.ID];
                    if (resolved != null)
                        break;
                    current = current.ParentID.HasValue && byId.TryGetValue(current.ParentID.Value, out var parent)
                        ? parent
                        : null;
                }

                result[cat.ID] = resolved ?? new TannarxFinanceClass
                {
                    Bucket = TannarxFinanceBucket.OtherAuto,
                    Reason = L.T("Mes_Tan_Reason_Undefined")
                };
            }

            return result;
        }
    }
}
