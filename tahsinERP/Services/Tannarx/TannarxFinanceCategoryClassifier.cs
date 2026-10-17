using System.Collections.Generic;
using System.Linq;

namespace tahsinERP.Services.Tannarx
{
    internal enum TannarxFinanceBucket
    {
        None = 0,
        Material = 1,
        TaxBank = 2,
        Other = 3
    }

    /// <summary>
    /// FIN_CATEGORIES ni fakt tannarx guruhlariga ajratadi (nom bo'yicha, qat'iy qoidalar).
    /// - faqat xarajat kategoriyalari (Type = 2), o'chirilmaganlar;
    /// - "kredit", "o'tkazma", "naqd", "transfer" so'zi bor kategoriya hech qaysi guruhga kirmaydi
    ///   (bank krediti, hisoblar orasidagi o'tkazma, naqd yechish xarajat emas);
    /// - har kategoriya faqat BITTA guruhga tushadi (avval "Boshqa soliq" ham soliq, ham boshqa bo'lib ikki marta sanalardi);
    /// - o'z nomi bo'yicha aniqlanmagan bola kategoriya ota kategoriyasining guruhini oladi.
    /// </summary>
    internal static class TannarxFinanceCategoryClassifier
    {
        internal const byte ExpenseCategoryType = 2;

        private static readonly string[] ExcludeKeywords = { "kredit", "o'tkazma", "otkazma", "naqd", "transfer" };
        private static readonly string[] TaxBankKeywords = { "soliq", "solig", "bank" };

        internal class CategoryRow
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public byte Type { get; set; }
            public int? ParentID { get; set; }
        }

        private enum OwnClass
        {
            Unknown,
            Excluded,
            Material,
            TaxBank,
            Other
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

        private static OwnClass ClassifyOwn(string name)
        {
            var n = Normalize(name);
            if (n.Length == 0)
                return OwnClass.Unknown;
            if (ExcludeKeywords.Any(k => n.Contains(k)))
                return OwnClass.Excluded;
            // "Material tashuvi" — bu transport, material narxi emas (u F_WAYBILLS orqali alohida hisoblanadi).
            if (n.Contains("material") && !n.Contains("tash"))
                return OwnClass.Material;
            if (TaxBankKeywords.Any(k => n.Contains(k)))
                return OwnClass.TaxBank;
            if (n.Contains("boshqa"))
                return OwnClass.Other;
            return OwnClass.Unknown;
        }

        /// <summary>CategoryID → guruh (faqat None bo'lmaganlar qaytadi).</summary>
        internal static Dictionary<int, TannarxFinanceBucket> Classify(IList<CategoryRow> categories)
        {
            var byId = categories.ToDictionary(c => c.ID);
            var own = categories.ToDictionary(c => c.ID, c => ClassifyOwn(c.Name));
            var result = new Dictionary<int, TannarxFinanceBucket>();

            foreach (var cat in categories)
            {
                if (cat.Type != ExpenseCategoryType)
                    continue;

                var resolved = OwnClass.Unknown;
                var current = cat;
                for (var depth = 0; current != null && depth < 10; depth++)
                {
                    var cls = own[current.ID];
                    if (cls != OwnClass.Unknown)
                    {
                        resolved = cls;
                        break;
                    }
                    current = current.ParentID.HasValue && byId.TryGetValue(current.ParentID.Value, out var parent)
                        ? parent
                        : null;
                }

                var bucket = ToBucket(resolved);
                if (bucket != TannarxFinanceBucket.None)
                    result[cat.ID] = bucket;
            }

            return result;
        }

        private static TannarxFinanceBucket ToBucket(OwnClass cls)
        {
            switch (cls)
            {
                case OwnClass.Material: return TannarxFinanceBucket.Material;
                case OwnClass.TaxBank: return TannarxFinanceBucket.TaxBank;
                case OwnClass.Other: return TannarxFinanceBucket.Other;
                default: return TannarxFinanceBucket.None;
            }
        }
    }
}
