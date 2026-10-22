using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.I18n;

namespace tahsinERP.Services
{
    /// <summary>
    /// Logistika kodlari (LOG-M02): avval 0/1/2/3 sehrli sonlar va 3 xil SelectList view'larda takrorlangan edi.
    /// Bazadagi qiymatlar o'zgarmaydi (byte ustunlar).
    /// </summary>
    public static class LogisticsEnums
    {
        // Qiymatlar — resx kalitlari (ko'rsatishda L.T orqali joriy tilga o'giriladi)
        public static readonly IReadOnlyDictionary<byte, string> Directions = new Dictionary<byte, string>
        {
            { 0, "Log_Dir_Inbound" }, { 1, "Log_Dir_Outbound" }, { 2, "Log_Dir_Internal" }
        };

        public static readonly IReadOnlyDictionary<byte, string> FreightBases = new Dictionary<byte, string>
        {
            { 0, "Log_Basis_PerTruck" }, { 1, "Log_Basis_PerTon" }, { 2, "Log_Basis_PerKm" }, { 3, "Log_Basis_Flat" }
        };

        public static readonly IReadOnlyDictionary<byte, string> PayerTypes = new Dictionary<byte, string>
        {
            { 0, "Log_Payer_OurCompany" }, { 1, "Log_Payer_Customer" }, { 2, "Log_Payer_Supplier" }, { 3, "Log_Payer_Shared" }
        };

        public static SelectList DirectionList(object selected = null) => ToSelectList(Directions, selected);

        public static SelectList FreightBasisList(object selected = null) => ToSelectList(FreightBases, selected);

        public static SelectList PayerTypeList(object selected = null) => ToSelectList(PayerTypes, selected);

        public static string DirectionText(byte? value) => Text(Directions, value);

        public static string FreightBasisText(byte? value) => Text(FreightBases, value);

        /// <summary>Excel shabloni uchun: import alias'lari o'zbekcha, shuning uchun har doim o'zbek tilida.</summary>
        public static string FreightBasisImportText(byte? value)
        {
            string key;
            return value.HasValue && FreightBases.TryGetValue(value.Value, out key) ? L.TFor("uz", key) : "";
        }

        // Excel import matnlari: o'zbekcha nom yoki inglizcha kod (avval WaybillController'da edi, LOG-M01)
        private static readonly IReadOnlyDictionary<string, byte> DirectionAliases = new Dictionary<string, byte>
        {
            { "kirim", 0 }, { "kiruvchi", 0 }, { "inbound", 0 },
            { "chiqim", 1 }, { "chiquvchi", 1 }, { "outbound", 1 },
            { "ichki", 2 }, { "internal", 2 }
        };

        private static readonly IReadOnlyDictionary<string, byte> FreightBasisAliases = new Dictionary<string, byte>
        {
            { "mashina uchun", 0 }, { "pertruck", 0 },
            { "ton uchun", 1 }, { "perton", 1 },
            { "km uchun", 2 }, { "perkm", 2 },
            { "belgilangan narx", 3 }, { "flat", 3 }
        };

        private static readonly IReadOnlyDictionary<string, byte> PayerTypeAliases = new Dictionary<string, byte>
        {
            { "bizning kompaniya", 0 }, { "ourcompany", 0 },
            { "mijoz", 1 }, { "customer", 1 },
            { "taminotchi", 2 }, { "supplier", 2 },
            { "bo'lib", 3 }, { "shared", 3 }
        };

        public static byte? ParseDirection(string text) => Parse(DirectionAliases, text);

        public static byte? ParseFreightBasis(string text) => Parse(FreightBasisAliases, text);

        public static byte? ParsePayerType(string text) => Parse(PayerTypeAliases, text);

        private static byte? Parse(IReadOnlyDictionary<string, byte> aliases, string text)
        {
            byte value;
            return text != null && aliases.TryGetValue(text.Trim().ToLowerInvariant(), out value) ? value : (byte?)null;
        }

        private static string Text(IReadOnlyDictionary<byte, string> map, byte? value)
        {
            string text;
            return value.HasValue && map.TryGetValue(value.Value, out text) ? L.T(text) : "";
        }

        private static SelectList ToSelectList(IReadOnlyDictionary<byte, string> map, object selected)
        {
            return new SelectList(map.Select(x => new { Value = x.Key, Text = L.T(x.Value) }), "Value", "Text", selected);
        }
    }
}
