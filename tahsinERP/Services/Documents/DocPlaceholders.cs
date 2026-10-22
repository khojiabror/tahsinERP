using System;
using System.Collections.Generic;
using System.Linq;

namespace tahsinERP.Services.Documents
{
    /// <summary>Shablon maydoni turi: oddiy matn, formatlangan blok (HTML) yoki jadval.</summary>
    public enum PlaceholderKind
    {
        Text,
        RichText,
        Table
    }

    public sealed class PlaceholderInfo
    {
        public string Key { get; set; }
        /// <summary>Tavsif resx kaliti; <see cref="Description"/> — joriy tilda.</summary>
        public string DescriptionKey { get; set; }
        public string Description => tahsinERP.I18n.L.T(DescriptionKey);
        public PlaceholderKind Kind { get; set; }

        /// <summary>Shablonda yoziladigan ko'rinish: {{KEY}}.</summary>
        public string Token => "{{" + Key + "}}";
    }

    /// <summary>
    /// Har hujjat turi uchun shablon maydonlari katalogi — generatsiya, shablon yuklashdagi tekshiruv,
    /// "Shablonlar" sahifasidagi yordam va namunaviy shablon shu yerdan olinadi (yagona manba).
    /// Shablonda maydon {{KEY}} ko'rinishida yoziladi (katta-kichik harf farqsiz; eski {KEY} ham qabul qilinadi).
    /// RichText/Table maydoni alohida paragrafda tursa — formatlangan matn/jadval qo'yiladi,
    /// boshqa matn ichida bo'lsa — oddiy matn sifatida.
    /// </summary>
    public static class DocPlaceholders
    {
        public const string LetterType = DocumentService.OfficialLetterType;
        public const string OrderType = DocumentService.CompanyOrderType;
        public const string StandardType = "COMPANY_STANDARD";

        /// <summary>Takrorlanuvchi maydonlar uchun eng ko'p indeks (ITEM_NAME_1..N, ORDER_DECREE_1_CONTENT_1..N).</summary>
        public const int MaxIndexed = 30;

        private static readonly Dictionary<string, string> TypeNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { LetterType, "Rasmiy maktub" },
            { OrderType, "Korxona buyrug'i" },
            { StandardType, "Korxona standarti" }
        };

        /// <summary>Hujjat turini qaysi controller ruxsati boshqaradi.</summary>
        private static readonly Dictionary<string, string> OwnerControllers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { LetterType, "OfficialLetters" },
            { OrderType, "CompanyOrders" },
            { StandardType, "STDCompanyStandards" }
        };

        private static readonly PlaceholderInfo[] Common =
        {
            P("DIRECTOR_NAME", "Doc_PhDirectorName"),
            P("TODAY", "Doc_PhToday")
        };

        private static readonly PlaceholderInfo[] Letter =
        {
            P("LETTER_NO", "Doc_PhLetterNo"),
            P("LETTER_DATE", "Doc_PhLetterDate"),
            P("COMPANY_NAME", "Doc_PhCompanyName"),
            P("CONTACT_NAME", "Doc_PhContactName"),
            P("CONTACT_POSITION", "Doc_PhContactPosition"),
            P("RECIPIENT_BLOCK", "Doc_PhRecipientBlock"),
            P("GREETING_LINE", "Doc_PhGreetingLine"),
            P("TOPIC", "Doc_PhTopic"),
            P("LETTER_BODY", "Doc_PhLetterBody", PlaceholderKind.RichText),
            P("ITEMS_TABLE", "Doc_PhItemsTable", PlaceholderKind.Table),
            P("ITEMS_BLOCK", "Doc_PhItemsBlock"),
            P("OUR_REF_NO", "Doc_PhOurRefNo"),
            P("THEIR_REF_NO", "Doc_PhTheirRefNo"),
            P("CHANNEL", "Doc_PhChannel"),
            P("ITEM_NAME_1", "Doc_PhItemName1"),
            P("ITEM_DIM_1", "Doc_PhItemDim1"),
            P("ITEM_PRICE_EXW_1", "Doc_PhItemPriceExw1"),
            P("ITEM_PRICE_VAT_EXW_1", "Doc_PhItemPriceVatExw1"),
            P("ITEM_PRICE_CIP_1", "Doc_PhItemPriceCip1"),
            P("ITEM_PRICE_VAT_CIP_1", "Doc_PhItemPriceVatCip1")
        };

        private static readonly PlaceholderInfo[] Order =
        {
            P("ORDER_NO", "Doc_PhOrderNo"),
            P("ORDER_DATE", "Doc_PhOrderDate"),
            P("ORDER_TOPIC", "Doc_PhOrderTopic"),
            P("ORDER_TYPE", "Doc_PhOrderType"),
            P("ORG_UNIT_NAME", "Doc_PhOrgUnitName"),
            P("ORDER_BODY", "Doc_PhOrderBody", PlaceholderKind.RichText),
            P("ORDER_DECREE_1_CONTENT_1", "Doc_PhOrderDecree1Content1")
        };

        /// <summary>Eski shablonlardagi nomlar (avval DocumentTemplateService ichida qattiq yozilgan edi).</summary>
        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Letter.No", "LETTER_NO" },
            { "Letter.Date", "LETTER_DATE" },
            // Mavjud OFFICIAL_LETTER_DEFAULT shablonida: "Xurmatli {{Receipent.Receiver.Name}}!" va alohida
            // qutilarda kompaniya/lavozim — avval hammasiga butun murojaat/qabul qiluvchi bloki qo'yilib, takrorlanardi
            { "Receipent.Receiver.Name", "CONTACT_NAME" },
            { "Receipent.CompanyName", "COMPANY_NAME" },
            { "Receipent.Company.Receiver.Position", "CONTACT_POSITION" },
            { "Content", "LETTER_BODY" },
            { "ORDER NO", "ORDER_NO" },
            { "ITEM_PRICE_1", "ITEM_PRICE_EXW_1" },
            { "ITEM_PRICE_VAT_1", "ITEM_PRICE_VAT_EXW_1" }
        };

        private static PlaceholderInfo P(string key, string descriptionKey, PlaceholderKind kind = PlaceholderKind.Text)
        {
            return new PlaceholderInfo { Key = key, DescriptionKey = descriptionKey, Kind = kind };
        }

        public static IEnumerable<string> DocTypes => TypeNames.Keys;

        public static string TypeName(string docTypeCode)
        {
            string name;
            return docTypeCode != null && TypeNames.TryGetValue(docTypeCode, out name) ? name : docTypeCode;
        }

        /// <summary>Turi nomi joriy tilda (faqat ko'rsatish; <see cref="TypeName"/> DOC_TYPES'ga yoziladi).</summary>
        public static string TypeDisplayName(string docTypeCode)
        {
            if (string.Equals(docTypeCode, LetterType, StringComparison.OrdinalIgnoreCase)) return tahsinERP.I18n.L.T("Doc_TypeLetter");
            if (string.Equals(docTypeCode, OrderType, StringComparison.OrdinalIgnoreCase)) return tahsinERP.I18n.L.T("Doc_TypeOrder");
            if (string.Equals(docTypeCode, StandardType, StringComparison.OrdinalIgnoreCase)) return tahsinERP.I18n.L.T("Doc_TypeStandard");
            return docTypeCode;
        }

        public static bool IsKnownType(string docTypeCode) => docTypeCode != null && TypeNames.ContainsKey(docTypeCode);

        public static string OwnerController(string docTypeCode)
        {
            string c;
            return docTypeCode != null && OwnerControllers.TryGetValue(docTypeCode, out c) ? c : null;
        }

        /// <summary>Yordam sahifasi uchun ro'yxat (STD shablonida maydon yo'q — u muharrirga yuklanadi).</summary>
        public static IReadOnlyList<PlaceholderInfo> For(string docTypeCode)
        {
            if (string.Equals(docTypeCode, LetterType, StringComparison.OrdinalIgnoreCase))
                return Letter.Concat(Common).ToList();
            if (string.Equals(docTypeCode, OrderType, StringComparison.OrdinalIgnoreCase))
                return Order.Concat(Common).ToList();
            return new List<PlaceholderInfo>();
        }

        /// <summary>Shablondagi nomni katalog kalitiga keltiradi (alias, indeksli maydonlar); noma'lum bo'lsa null.</summary>
        public static string Resolve(string docTypeCode, string rawKey)
        {
            if (string.IsNullOrWhiteSpace(rawKey))
                return null;

            string key = rawKey.Trim();
            string alias;
            if (Aliases.TryGetValue(key, out alias))
            {
                key = alias;
            }
            else
            {
                // ITEM_PRICE_3 -> ITEM_PRICE_EXW_3 (eski nom, istalgan indeks)
                var m = System.Text.RegularExpressions.Regex.Match(key, @"^ITEM_PRICE(_VAT)?_(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (m.Success)
                    key = "ITEM_PRICE" + m.Groups[1].Value + "_EXW_" + m.Groups[2].Value;
            }

            key = key.ToUpperInvariant();
            var known = For(docTypeCode);
            if (known.Any(p => p.Key == key))
                return key;

            // Indeksli maydonlar: katalogda _1 bilan ko'rsatilgan, istalgan raqam qabul qilinadi
            var idx = System.Text.RegularExpressions.Regex.Match(key, @"^(.*_)(\d+)$");
            if (idx.Success && known.Any(p => p.Key == idx.Groups[1].Value + "1"))
                return key;

            return null;
        }

        public static PlaceholderKind KindOf(string docTypeCode, string key)
        {
            var p = For(docTypeCode).FirstOrDefault(x => x.Key == key);
            return p?.Kind ?? PlaceholderKind.Text;
        }
    }
}
