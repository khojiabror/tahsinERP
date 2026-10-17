using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;

namespace tahsinERP.Services.Crm
{
    /// <summary>
    /// Formadagi takrorlanuvchi qatorlar ("Prefix[i].Field") — CRM-D14. Model binder indekslarda
    /// bo'shliq bo'lsa keyingi qatorlarni tashlab yuboradi (CRM-B08), shuning uchun CRM formalari
    /// kalitlar bo'yicha o'qiladi; SOrder va SContract'dagi uchta nusxa shu yerda birlashtirildi.
    /// </summary>
    public static class FormRows
    {
        private static readonly Regex IndexPattern = new Regex(@"^(?<prefix>[^\[]+)\[(?<i>\d+)\]\.(?<field>.+)$", RegexOptions.Compiled);

        /// <summary><paramref name="prefix"/>[i].<paramref name="field"/> kaliti bor indekslar, o'sish tartibida.</summary>
        public static List<int> Indexes(NameValueCollection form, string prefix, string field)
        {
            return form.AllKeys
                .Where(k => k != null)
                .Select(k => IndexPattern.Match(k))
                .Where(m => m.Success && m.Groups["prefix"].Value == prefix && m.Groups["field"].Value == field)
                .Select(m => int.Parse(m.Groups["i"].Value))
                .Distinct()
                .OrderBy(i => i)
                .ToList();
        }

        public static string Value(NameValueCollection form, string prefix, int index, string field)
        {
            return form[$"{prefix}[{index}].{field}"];
        }

        /// <summary>Qatordagi butun son; bo'sh yoki noto'g'ri bo'lsa 0.</summary>
        public static int Int(NameValueCollection form, string prefix, int index, string field)
        {
            int value;
            return int.TryParse(Value(form, prefix, index, field), out value) ? value : 0;
        }

        public static long Long(NameValueCollection form, string prefix, int index, string field)
        {
            long value;
            return long.TryParse(Value(form, prefix, index, field), out value) ? value : 0;
        }
    }
}
