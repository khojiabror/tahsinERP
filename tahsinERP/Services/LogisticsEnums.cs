using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace tahsinERP.Services
{
    /// <summary>
    /// Logistika kodlari (LOG-M02): avval 0/1/2/3 sehrli sonlar va 3 xil SelectList view'larda takrorlangan edi.
    /// Bazadagi qiymatlar o'zgarmaydi (byte ustunlar).
    /// </summary>
    public static class LogisticsEnums
    {
        public static readonly IReadOnlyDictionary<byte, string> Directions = new Dictionary<byte, string>
        {
            { 0, "Kiruvchi" }, { 1, "Chiquvchi" }, { 2, "Ichki" }
        };

        public static readonly IReadOnlyDictionary<byte, string> FreightBases = new Dictionary<byte, string>
        {
            { 0, "Mashina uchun" }, { 1, "Ton uchun" }, { 2, "KM uchun" }, { 3, "Belgilangan narx" }
        };

        public static readonly IReadOnlyDictionary<byte, string> PayerTypes = new Dictionary<byte, string>
        {
            { 0, "Bizning kompaniya" }, { 1, "Mijoz" }, { 2, "Taminotchi" }, { 3, "Bo'lib" }
        };

        public static SelectList DirectionList(object selected = null) => ToSelectList(Directions, selected);

        public static SelectList FreightBasisList(object selected = null) => ToSelectList(FreightBases, selected);

        public static SelectList PayerTypeList(object selected = null) => ToSelectList(PayerTypes, selected);

        public static string DirectionText(byte? value) => Text(Directions, value);

        public static string FreightBasisText(byte? value) => Text(FreightBases, value);

        private static string Text(IReadOnlyDictionary<byte, string> map, byte? value)
        {
            string text;
            return value.HasValue && map.TryGetValue(value.Value, out text) ? text : "";
        }

        private static SelectList ToSelectList(IReadOnlyDictionary<byte, string> map, object selected)
        {
            return new SelectList(map.Select(x => new { Value = x.Key, Text = x.Value }), "Value", "Text", selected);
        }
    }
}
