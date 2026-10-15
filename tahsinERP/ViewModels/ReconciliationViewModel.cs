using System;
using System.Collections.Generic;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// Akt-sverki (Mutual settlements reconciliation) uchun view model
    /// O'zbekcha kontent - ikki tomonlama oldi-berdi hisobini aks ettiradi
    /// </summary>
    public class ReconciliationViewModel
    {
        public string Party1Name { get; set; }  // Bizning korxona / filial
        public string Party2Name { get; set; }  // Kontragent (ta'minotchi, xaridor, yuk tashuvchi)
        public DateTime AsOfDate { get; set; }
        public string ReportDate { get; set; }
        public string AgreementInfo { get; set; }
        public string ReconciliationType { get; set; } // Supplier, Customer, Forwarder
        public int CounterpartyId { get; set; }
        /// <summary>Tanlangan shartnoma (P_CONTRACTS / S_CONTRACTS / F_CONTRACTS ID).</summary>
        public int ContractId { get; set; }
        public string ContractNo { get; set; }
        /// <summary>Dropdown: ushbu kontragentning barcha shartnomalari.</summary>
        public List<ReconciliationContractOption> ContractsForParty { get; set; }
        public bool HasSelectedContract => ContractId > 0;
        public List<ReconciliationRowViewModel> Rows { get; set; }
        public decimal Party1DebitTotal { get; set; }
        public decimal Party1CreditTotal { get; set; }
        public decimal Party2DebitTotal { get; set; }
        public decimal Party2CreditTotal { get; set; }
        public decimal FinalBalance { get; set; }  // Ijobiy = kontragent bizga qarzdor, manfiy = biz kontragentga qarzdor
    }

    public class ReconciliationContractOption
    {
        public int Id { get; set; }
        public string ContractNo { get; set; }
    }

    public class ReconciliationRowViewModel
    {
        public string Content { get; set; }
        public DateTime? Date { get; set; }
        public string DateStr => Date?.ToString("dd.MM.yyyy") ?? "";
        public decimal Party1Debit { get; set; }
        public decimal Party1Credit { get; set; }
        public decimal Party2Debit { get; set; }
        public decimal Party2Credit { get; set; }
        public bool IsSubheader { get; set; }
        public bool IsBalanceRow { get; set; }
        public bool IsAdjustmentRow { get; set; }
    }
}
