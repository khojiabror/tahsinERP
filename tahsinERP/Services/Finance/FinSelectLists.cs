using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace tahsinERP.Services.Finance
{
    /// <summary>
    /// Finance formalaridagi takrorlangan SelectList'lar (FIN-D13, FIN-D14):
    /// yil/oy, yo'nalish, kontragent turi va manba hujjat turi.
    /// </summary>
    public static class FinSelectLists
    {
        public const string Manual = "Manual";
        public const string PurchaseInvoicePayment = "PurchaseInvoicePayment";
        public const string RecurringObligationPayment = "RecurringObligationPayment";

        /// <summary>Qo'lda kiritiladigan tranzaksiyada tanlash mumkin bo'lgan manba turlari.</summary>
        public static readonly string[] ManualSourceDocTypes = { Manual, RecurringObligationPayment, PurchaseInvoicePayment };

        private static readonly KeyValuePair<string, string>[] SourceDocTypeNames =
        {
            new KeyValuePair<string, string>(Manual, "Qo'lda"),
            new KeyValuePair<string, string>(RecurringObligationPayment, "Doimiy harajat to'lovi (ijara, buxgalteriya va h.k.)"),
            new KeyValuePair<string, string>(PurchaseInvoicePayment, "Ta'minotchiga to'lov (xarid invoysiga bog'lash)"),
            new KeyValuePair<string, string>("SalesInvoice", "Sotuv invoysi"),
            new KeyValuePair<string, string>("PurchaseInvoice", "Xarid invoysi"),
            new KeyValuePair<string, string>("IncomeDoc", "Ombor kirimi"),
            new KeyValuePair<string, string>("ExpenseDoc", "Ombor chiqimi"),
            new KeyValuePair<string, string>("WaybillPayment", "Yo'l varaqasi to'lovi"),
            new KeyValuePair<string, string>("ShareholderContribution", "Aksiyador hissasi")
        };

        /// <summary>Joriy yil ± 5.</summary>
        public static SelectList Years(int? selected)
        {
            return new SelectList(Enumerable.Range(DateTime.Now.Year - 5, 11).ToList(), selected);
        }

        public static SelectList Months(int? selected)
        {
            var months = Enumerable.Range(1, 12)
                .Select(m => new { Value = m, Text = new DateTime(2000, m, 1).ToString("MMMM") })
                .ToList();
            return new SelectList(months, "Value", "Text", selected);
        }

        public static SelectList Directions(byte? selected)
        {
            return new SelectList(new[]
            {
                new { Value = (byte)1, Text = "Kirim" },
                new { Value = (byte)2, Text = "Chiqim" }
            }, "Value", "Text", selected);
        }

        public static SelectList CounterpartyTypes(byte? selected)
        {
            return new SelectList(new[]
            {
                new { Value = (byte?)null, Text = "Tanlanmagan" },
                new { Value = (byte?)1, Text = "Mijoz" },
                new { Value = (byte?)2, Text = "Ta'minotchi" },
                new { Value = (byte?)3, Text = "Aksiyador" },
                new { Value = (byte?)4, Text = "Forwarder" },
                new { Value = (byte?)6, Text = "Xodim" },
                new { Value = (byte?)5, Text = "Boshqa" }
            }, "Value", "Text", selected);
        }

        /// <summary>Yaratish formasi: faqat qo'lda tanlanadigan turlar.</summary>
        public static SelectList ManualSourceDocTypeList(string selected)
        {
            var items = SourceDocTypeNames
                .Where(p => ManualSourceDocTypes.Contains(p.Key))
                .Select(p => new { Value = p.Key, Text = p.Value })
                .ToList();
            return new SelectList(items, "Value", "Text", string.IsNullOrWhiteSpace(selected) ? Manual : selected);
        }

        /// <summary>Barcha ma'lum turlar (filtr va ko'rish); noma'lum joriy qiymat "(maxsus)" sifatida qo'shiladi.</summary>
        public static SelectList SourceDocTypeList(string current)
        {
            var items = SourceDocTypeNames.Select(p => new { Value = p.Key, Text = p.Value }).ToList();
            var selected = string.IsNullOrWhiteSpace(current) ? Manual : current.Trim();
            if (!SourceDocTypeNames.Any(p => string.Equals(p.Key, selected, StringComparison.OrdinalIgnoreCase)))
            {
                items.Insert(0, new { Value = selected, Text = selected + " (maxsus)" });
            }
            return new SelectList(items, "Value", "Text", selected);
        }

        public static bool IsManual(string sourceDocType)
        {
            return string.IsNullOrWhiteSpace(sourceDocType) ||
                   string.Equals(sourceDocType.Trim(), Manual, StringComparison.OrdinalIgnoreCase);
        }
    }
}
