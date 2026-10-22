using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.I18n;

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

        // Value — resurs kaliti (ko'rsatishda L.T orqali tarjima qilinadi)
        private static readonly KeyValuePair<string, string>[] SourceDocTypeNames =
        {
            new KeyValuePair<string, string>(Manual, "Fin_Manual"),
            new KeyValuePair<string, string>(RecurringObligationPayment, "Fin_Src_RecurringPayment"),
            new KeyValuePair<string, string>(PurchaseInvoicePayment, "Fin_Src_SupplierPayment"),
            new KeyValuePair<string, string>("SalesInvoice", "Fin_Src_SalesInvoice"),
            new KeyValuePair<string, string>("PurchaseInvoice", "Fin_Src_PurchaseInvoice"),
            new KeyValuePair<string, string>("IncomeDoc", "Fin_Src_IncomeDoc"),
            new KeyValuePair<string, string>("ExpenseDoc", "Fin_Src_ExpenseDoc"),
            new KeyValuePair<string, string>("WaybillPayment", "Fin_Src_WaybillPayment"),
            new KeyValuePair<string, string>("ShareholderContribution", "Fin_Src_ShareholderContribution")
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
                new { Value = (byte)1, Text = L.T("Fin_Inflow") },
                new { Value = (byte)2, Text = L.T("Fin_Expense") }
            }, "Value", "Text", selected);
        }

        public static SelectList CounterpartyTypes(byte? selected)
        {
            return new SelectList(new[]
            {
                new { Value = (byte?)null, Text = L.T("Fin_NotSelected") },
                new { Value = (byte?)1, Text = L.T("Fin_Customer") },
                new { Value = (byte?)2, Text = L.T("Fin_Supplier") },
                new { Value = (byte?)3, Text = L.T("Fin_Shareholder") },
                new { Value = (byte?)4, Text = L.T("Fin_CpForwarder") },
                new { Value = (byte?)6, Text = L.T("Fin_Employee") },
                new { Value = (byte?)5, Text = L.T("Fin_Other") }
            }, "Value", "Text", selected);
        }

        /// <summary>Yaratish formasi: faqat qo'lda tanlanadigan turlar.</summary>
        public static SelectList ManualSourceDocTypeList(string selected)
        {
            var items = SourceDocTypeNames
                .Where(p => ManualSourceDocTypes.Contains(p.Key))
                .Select(p => new { Value = p.Key, Text = L.T(p.Value) })
                .ToList();
            return new SelectList(items, "Value", "Text", string.IsNullOrWhiteSpace(selected) ? Manual : selected);
        }

        /// <summary>Barcha ma'lum turlar (filtr va ko'rish); noma'lum joriy qiymat "(maxsus)" sifatida qo'shiladi.</summary>
        public static SelectList SourceDocTypeList(string current)
        {
            var items = SourceDocTypeNames.Select(p => new { Value = p.Key, Text = L.T(p.Value) }).ToList();
            var selected = string.IsNullOrWhiteSpace(current) ? Manual : current.Trim();
            if (!SourceDocTypeNames.Any(p => string.Equals(p.Key, selected, StringComparison.OrdinalIgnoreCase)))
            {
                items.Insert(0, new { Value = selected, Text = selected + " (" + L.T("Fin_Custom") + ")" });
            }
            return new SelectList(items, "Value", "Text", selected);
        }

        /// <summary>
        /// Foydalanuvchi qo'lda kiritgan tranzaksiya: Manual, ta'minotchiga to'lov (xarid invoysiga bog'langan) yoki
        /// doimiy harajat to'lovi — tahrirlash va o'chirish mumkin. Hujjatdan avtomatik yaratilganlari (SalesInvoice,
        /// PurchaseInvoice, WaybillPayment, ShareholderContribution ...) faqat manba hujjat orqali o'zgaradi.
        /// Avval faqat Manual edi — qo'lda kiritilgan ta'minotchi va doimiy harajat to'lovini tuzatib bo'lmasdi.
        /// </summary>
        public static bool IsUserEditable(string sourceDocType)
        {
            return string.IsNullOrWhiteSpace(sourceDocType) ||
                   ManualSourceDocTypes.Contains(sourceDocType.Trim(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
