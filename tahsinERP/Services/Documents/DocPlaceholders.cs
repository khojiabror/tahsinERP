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
        public string Description { get; set; }
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
            P("DIRECTOR_NAME", "Direktor F.I.Sh. (kompaniya sozlamasi)"),
            P("TODAY", "Bugungi sana (dd.MM.yyyy)")
        };

        private static readonly PlaceholderInfo[] Letter =
        {
            P("LETTER_NO", "Maktub raqami"),
            P("LETTER_DATE", "Maktub sanasi (dd.MM.yyyy)"),
            P("COMPANY_NAME", "Qabul qiluvchi kompaniya"),
            P("CONTACT_NAME", "Qabul qiluvchi shaxs"),
            P("CONTACT_POSITION", "Qabul qiluvchi lavozimi"),
            P("RECIPIENT_BLOCK", "Kompaniya, lavozim va shaxs — har biri yangi qatorda"),
            P("GREETING_LINE", "Murojaat (masalan: Hurmatli ...!)"),
            P("TOPIC", "Mavzu"),
            P("LETTER_BODY", "Maktub matni (formatlash bilan)", PlaceholderKind.RichText),
            P("ITEMS_TABLE", "Mahsulot va narxlar jadvali", PlaceholderKind.Table),
            P("ITEMS_BLOCK", "Mahsulotlar ro'yxati (oddiy matn)"),
            P("OUR_REF_NO", "Bizning havola raqami"),
            P("THEIR_REF_NO", "Ularning havola raqami"),
            P("CHANNEL", "Yuborish usuli"),
            P("ITEM_NAME_1", "1-mahsulot nomi (2, 3, ... ham bor)"),
            P("ITEM_DIM_1", "1-mahsulot o'lchami"),
            P("ITEM_PRICE_EXW_1", "1-mahsulot EXW narxi"),
            P("ITEM_PRICE_VAT_EXW_1", "1-mahsulot QQS bilan EXW narxi"),
            P("ITEM_PRICE_CIP_1", "1-mahsulot CIP narxi"),
            P("ITEM_PRICE_VAT_CIP_1", "1-mahsulot QQS bilan CIP narxi")
        };

        private static readonly PlaceholderInfo[] Order =
        {
            P("ORDER_NO", "Buyruq raqami"),
            P("ORDER_DATE", "Buyruq sanasi (dd.MM.yyyy)"),
            P("ORDER_TOPIC", "Buyruq mavzusi (sarlavha)"),
            P("ORDER_TYPE", "Buyruq turi"),
            P("ORG_UNIT_NAME", "Bo'lim"),
            P("ORDER_BODY", "Buyruq matni (formatlash bilan)", PlaceholderKind.RichText),
            P("ORDER_DECREE_1_CONTENT_1", "Matnning 1-bandi (2, 3, ... ham bor; raqamsiz)")
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

        private static PlaceholderInfo P(string key, string description, PlaceholderKind kind = PlaceholderKind.Text)
        {
            return new PlaceholderInfo { Key = key, Description = description, Kind = kind };
        }

        public static IEnumerable<string> DocTypes => TypeNames.Keys;

        public static string TypeName(string docTypeCode)
        {
            string name;
            return docTypeCode != null && TypeNames.TryGetValue(docTypeCode, out name) ? name : docTypeCode;
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
