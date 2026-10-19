using System;
using System.ComponentModel;
using System.Globalization;
using System.Web.Mvc;

namespace tahsinERP.Areas.Distribution.Services
{
    /// <summary>
    /// Ilova madaniyati uz-UZ (o'nlik ajratkich ","), brauzer &lt;input type=number&gt; esa "." yuboradi —
    /// standart binder 41.311081 ni rad etadi. Distributsiya modellarida decimal maydonlar ikkala ko'rinishda
    /// qabul qilinadi ([ModelBinder(typeof(DstModelBinder))] bilan; global binderlarga tegilmaydi).
    /// </summary>
    public class DstModelBinder : DefaultModelBinder
    {
        protected override object GetPropertyValue(ControllerContext controllerContext, ModelBindingContext bindingContext,
            PropertyDescriptor propertyDescriptor, IModelBinder propertyBinder)
        {
            var type = propertyDescriptor.PropertyType;
            if (type == typeof(decimal) || type == typeof(decimal?))
            {
                var raw = bindingContext.ValueProvider.GetValue(bindingContext.ModelName)?.AttemptedValue;
                if (string.IsNullOrWhiteSpace(raw))
                    return type == typeof(decimal) ? (object)0m : null;
                if (TryParseDecimal(raw, out var value))
                    return value;
                bindingContext.ModelState.AddModelError(bindingContext.ModelName, DstLocalization.T("Error_NumberFormat"));
                return type == typeof(decimal) ? (object)0m : null;
            }
            return base.GetPropertyValue(controllerContext, bindingContext, propertyDescriptor, propertyBinder);
        }

        /// <summary>"1 234,5", "1234.5", "1,234.50" ko'rinishlarini qabul qiladi.</summary>
        public static bool TryParseDecimal(string raw, out decimal value)
        {
            var s = raw.Trim().Replace(" ", "").Replace(" ", "").Replace(" ", "");
            int lastDot = s.LastIndexOf('.'), lastComma = s.LastIndexOf(',');
            if (lastDot >= 0 && lastComma >= 0)
            {
                // oxirgisi — o'nlik ajratkich, boshqasi — minglik
                s = lastDot > lastComma ? s.Replace(",", "") : s.Replace(".", "").Replace(',', '.');
            }
            else if (lastComma >= 0)
            {
                s = s.Replace(',', '.');
            }
            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }
    }
}
