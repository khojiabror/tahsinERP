using System.Collections.Generic;
using System.Linq;

namespace tahsinERP.Services.Tannarx
{
    internal enum TannarxFinanceBucket
    {
        /// <summary>Tannarxga kirmaydi (sababi Reason da).</summary>
        Excluded = 0,
        Material = 1,
        TaxBank = 2,
        /// <summary>Nomida "boshqa" bor kategoriya.</summary>
        Other = 3,
        /// <summary>Nomi bo'yicha aniqlanmagan, boshqa joyda hisoblanmaydigan xarajat — "Boshqa" ga qo'shiladi.</summary>
        OtherAuto = 4
    }

    internal class TannarxFinanceClass
    {
        public TannarxFinanceBucket Bucket { get; set; }
        public string Reason { get; set; }
        public bool IsManual { get; set; }
    }

    /// <summary>
    /// FIN_CATEGORIES (faqat xarajat, Type = 2) ni fakt tannarx guruhlariga ajratadi. Tamoyil: oydagi BARCHA xarajat
    /// tannarxga tushadi, faqat boshqa joyda allaqachon hisoblanadigan yoki xarajat bo'lmagan pul harakati chiqariladi:
    /// - kredit / o'tkazma / naqd yechish / qarz — xarajat emas;
    /// - dividend, aksiyador — foydani taqsimlash;
    /// - QQS — tannarx emas (qaytariladigan soliq);
    /// - asosiy vosita / uskuna / jihoz / qurilish / investitsiya — kapital xarajat;
    /// - xarid / ta'minotchi — material PATH0/1/2 (ombor, PI) orqali;
    /// - transport (tash, logist, yuk) — yo'l varaqalari (F_WAYBILLS) orqali;
    /// - oylik / ish haqi — HR ish haqi (ishbay va soatbay) orqali.
    /// Har kategoriya faqat BITTA guruhga tushadi; o'z nomi bo'yicha aniqlanmagan bola kategoriya ota kategoriyasining
    /// guruhini oladi; hech qayerga tushmaganlari OtherAuto ("Boshqa").
    /// </summary>
    internal static class TannarxFinanceCategoryClassifier
    {
        internal const byte ExpenseCategoryType = 2;

        private static readonly (string[] Keywords, string Reason)[] ExclusionRules =
        {
            (new[] { "kredit", "o'tkazma", "otkazma", "naqd", "transfer", "qarz", "hisob raqam", "hisobraqam" }, "Moliyaviy harakat (kredit/o'tkazma/naqd/qarz)"),
            (new[] { "divid", "aksiyador" }, "Foydani taqsimlash (dividend/aksiyador)"),
            (new[] { "qqs", "nds", "vat" }, "QQS — tannarx emas"),
            (new[] { "asosiy vosita", "uskuna", "jihoz", "jixoz", "qurilish", "investitsiya", "kapital" }, "Kapital xarajat (asosiy vosita/uskuna/qurilish)"),
            (new[] { "tash", "transport", "logist", "yuk" }, "Transport — yo'l varaqalari orqali hisoblanadi"),
            (new[] { "oylik", "ish haqi", "ish xaqi", "maosh", "salary", "avans" }, "Ish haqi — HR ish haqi orqali hisoblanadi"),
            (new[] { "xarid", "ta'minotchi", "taminotchi", "supplier" }, "Xarid — material ombor/PI orqali hisoblanadi")
        };

        private static readonly string[] TaxBankKeywords = { "soliq", "solig", "bank" };

        internal class CategoryRow
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public byte Type { get; set; }
            public int? ParentID { get; set; }
        }

        internal static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            return name.Trim().ToLowerInvariant()
                .Replace('ʻ', '\'')   // ʻ
                .Replace('ʼ', '\'')   // ʼ
                .Replace('‘', '\'')   // ‘
                .Replace('’', '\'')   // ’
                .Replace('`', '\'');
        }

        /// <summary>O'z nomi bo'yicha sinf; null — aniqlanmadi (ota kategoriyaga qaraladi).</summary>
        private static TannarxFinanceClass ClassifyOwn(string name)
        {
            var n = Normalize(name);
            if (n.Length == 0)
                return null;
            // "Material" birinchi: "Material xaridi" — material (Finance PATH3 manbasi), lekin "Material tashuvi" — transport.
            if (n.Contains("material") && !n.Contains("tash"))
                return new TannarxFinanceClass { Bucket = TannarxFinanceBucket.Material, Reason = "Material" };
            foreach (var rule in ExclusionRules)
            {
                if (rule.Keywords.Any(k => n.Contains(k)))
                    return new TannarxFinanceClass { Bucket = TannarxFinanceBucket.Excluded, Reason = rule.Reason };
            }
            if (TaxBankKeywords.Any(k => n.Contains(k)))
                return new TannarxFinanceClass { Bucket = TannarxFinanceBucket.TaxBank, Reason = "Soliq va bank" };
            if (n.Contains("boshqa"))
                return new TannarxFinanceClass { Bucket = TannarxFinanceBucket.Other, Reason = "Boshqa" };
            return null;
        }

        internal static string BucketTitle(TannarxFinanceBucket bucket)
        {
            switch (bucket)
            {
                case TannarxFinanceBucket.Material: return "Material (PATH3)";
                case TannarxFinanceBucket.TaxBank: return "B — Soliq va bank";
                case TannarxFinanceBucket.Other: return "D — Boshqa";
                case TannarxFinanceBucket.OtherAuto: return "D — Boshqa (avtomatik)";
                default: return "Chiqarilsin";
            }
        }

        /// <summary>
        /// Har xarajat kategoriyasi (Type = 2) → sinf. Qo'lda belgilangan guruh (TANNARX_FIN_CATEGORY_MAP) nom qoidasidan ustun;
        /// kategoriyaning o'zida hech narsa bo'lmasa eng yaqin ota kategoriyaning belgisi yoki nom qoidasi olinadi.
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
                        Reason = bucket == TannarxFinanceBucket.Excluded ? "Qo'lda: tannarxga kirmaydi" : "Qo'lda belgilangan",
                        IsManual = true
                    };
                }
                return ClassifyOwn(c.Name);
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
                    Reason = "Aniqlanmagan xarajat → Boshqa"
                };
            }

            return result;
        }
    }
}
