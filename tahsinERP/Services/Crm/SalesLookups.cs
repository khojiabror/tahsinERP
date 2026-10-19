using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services.Common;

namespace tahsinERP.Services.Crm
{
    /// <summary>
    /// CRM formalaridagi Mijoz → Shartnoma → Buyurtma kaskad ro'yxatlari (D5, D11, D12, CRM-P04).
    /// Mijozlar <see cref="ReferenceDataCache"/>'dan; shartnoma/buyurtma — faqat ID va raqam proyeksiyasi.
    /// </summary>
    public static class SalesLookups
    {
        public static SelectList Customers(object selected = null)
        {
            return ReferenceDataCache.SelectList(ReferenceDataCache.Customers, selected);
        }

        /// <summary>"Tanlang" bandi bilan mijozlar (qiymati <paramref name="emptyValue"/>).</summary>
        public static SelectList CustomersWithPlaceholder(int? selected, string emptyValue = "", string emptyText = "Tanlang")
        {
            var items = new List<SelectListItem> { new SelectListItem { Value = emptyValue, Text = emptyText } };
            items.AddRange(ReferenceDataCache.Get(ReferenceDataCache.Customers)
                .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.Name }));
            var selectedValue = selected.HasValue && selected.Value > 0 ? selected.Value.ToString() : emptyValue;
            return new SelectList(items, "Value", "Text", selectedValue);
        }

        /// <summary>Mijozning faol shartnomalari ("Tanlang" bilan).</summary>
        public static SelectList ContractsFor(DBTHSNEntities db, int? customerId, int? selected, string emptyValue = "", string emptyText = "Tanlang")
        {
            var items = new List<SelectListItem> { new SelectListItem { Value = emptyValue, Text = emptyText } };
            if (customerId.HasValue && customerId.Value > 0)
            {
                items.AddRange(db.S_CONTRACTS.AsNoTracking()
                    .Where(c => c.CustomerID == customerId.Value && (c.IsDeleted == false || c.IsDeleted == null))
                    .OrderBy(c => c.ContractNo)
                    .Select(c => new { c.ID, c.ContractNo })
                    .ToList()
                    .Select(c => new SelectListItem { Value = c.ID.ToString(), Text = c.ContractNo }));
            }
            var selectedValue = selected.HasValue && selected.Value > 0 ? selected.Value.ToString() : emptyValue;
            return new SelectList(items, "Value", "Text", selectedValue);
        }

        /// <summary>Shartnomaning faol buyurtmalari ("Tanlang" bilan).</summary>
        public static SelectList OrdersFor(DBTHSNEntities db, int? contractId, int? selected)
        {
            var items = new List<SelectListItem> { new SelectListItem { Value = "", Text = "Tanlang" } };
            if (contractId.HasValue && contractId.Value > 0)
            {
                items.AddRange(db.S_ORDERS.AsNoTracking()
                    .Where(o => o.ContractID == contractId.Value && (o.IsDeleted == false || o.IsDeleted == null))
                    .OrderByDescending(o => o.IssuedDate)
                    .Select(o => new { o.ID, o.OrderNo })
                    .ToList()
                    .Select(o => new SelectListItem { Value = o.ID.ToString(), Text = o.OrderNo }));
            }
            return new SelectList(items, "Value", "Text", selected?.ToString() ?? "");
        }

        /// <summary>Valyutalar (qiymat = CURRENCIES.ID, matn = qisqa kod, masalan "UZS").</summary>
        public static SelectList Currencies(int? selectedId)
        {
            return new SelectList(ReferenceDataCache.Get(ReferenceDataCache.Currencies), "ID", "Name", selectedId);
        }

        /// <summary>
        /// Valyuta qisqa kodi (keshdan). SALES_INVOICES.CurrencyCode va S_ORDERS.Currency ustunlari
        /// olib tashlandi (CRM-M02) — yagona manba CurrencyID.
        /// </summary>
        public static string CurrencyCode(int? currencyId)
        {
            return ReferenceDataCache.NameOf(ReferenceDataCache.Currencies, currencyId) ?? "";
        }

        /// <summary>
        /// AJAX: mijozning shartnomalari + har birining o'rtacha QQS %
        /// (SalesInvoices va SOrder'dagi bir xil ikki nusxa o'rniga — D5).
        /// </summary>
        public static object ContractsByCustomer(DBTHSNEntities db, int customerId)
        {
            var contracts = db.S_CONTRACTS.AsNoTracking()
                .Where(c => c.CustomerID == customerId && (c.IsDeleted == false || c.IsDeleted == null))
                .OrderBy(c => c.ContractNo)
                .Select(c => new { c.ID, c.ContractNo })
                .ToList();

            var contractIds = contracts.Select(c => c.ID).ToList();
            var lines = db.S_CONTRACT_PRODUCTS.AsNoTracking()
                .Where(p => contractIds.Contains(p.ContractID))
                .Select(p => new { p.ContractID, p.PiecePrice, p.Amount, p.vatamount })
                .ToList()
                .Select(p => new SContractVatHelper.ContractProductVatLine
                {
                    ContractID = p.ContractID,
                    LineBaseAmount = SContractVatHelper.GetLineBaseAmount(p.PiecePrice, p.Amount),
                    VATAmount = (p.vatamount)
                });

            var vatByContract = SContractVatHelper.BuildContractVatRates(lines);

            return contracts.Select(c => new
            {
                c.ID,
                c.ContractNo,
                VatRate = vatByContract.ContainsKey(c.ID) ? vatByContract[c.ID] : 0m
            }).ToList();
        }
    }
}
