using System;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels
{
    public class ReconciliationAdjustmentEditViewModel
    {
        public string ReconciliationType { get; set; }
        public int CounterpartyId { get; set; }
        /// <summary>P_/S_/F_CONTRACTS.ID</summary>
        public int ContractId { get; set; }
        public string ContractNo { get; set; }
        public string CounterpartyName { get; set; }

        /// <summary>Joriy yil uchun yozuv mavjud bo'lsa — tahrirlash.</summary>
        public int EntryYear { get; set; }

        [Required(ErrorMessage = "Crm_RecValDateRequired")]
        [Display(Name = "Crm_RecCutoffDate")]
        public DateTime CutoffDate { get; set; }

        [Display(Name = "Crm_RecDebitOurs")]
        [Range(typeof(decimal), "0", "9999999999999", ErrorMessage = "Crm_RecValPositive")]
        public decimal Party1Debit { get; set; }

        [Display(Name = "Crm_RecCreditOurs")]
        [Range(typeof(decimal), "0", "9999999999999", ErrorMessage = "Crm_RecValPositive")]
        public decimal Party1Credit { get; set; }

        [Display(Name = "Comment")]
        [StringLength(500)]
        public string Note { get; set; }

        public bool AdjustmentAlreadyExistsForYear { get; set; }
    }
}
