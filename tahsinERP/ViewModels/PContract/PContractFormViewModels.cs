using System;
using System.Collections.Generic;

namespace tahsinERP.ViewModels.PContract
{
    // Shartnoma Create/Edit formalari (avval PContractsController.cs ichida edi — SRM-M01)
    public class PContractCreateViewModel
    {
        public string ContractNo { get; set; }
        public DateTime IssuedDate { get; set; } = DateTime.Now;
        public int? CompanyID { get; set; }
        public int SupplierID { get; set; }
        public int CurrencyID { get; set; }
        public decimal? Amount { get; set; }
        public string Incoterms { get; set; }
        public string PaymentTerms { get; set; }
        public DateTime DueDate { get; set; }
        public string IDN { get; set; }
        public List<PContractProductViewModel> ContractProducts { get; set; }

        public PContractCreateViewModel()
        {
            ContractProducts = new List<PContractProductViewModel>();
        }
    }

    public class PContractEditViewModel
    {
        public int ID { get; set; }
        public string ContractNo { get; set; }
        public DateTime IssuedDate { get; set; }
        public int? CompanyID { get; set; }
        public int SupplierID { get; set; }
        public int CurrencyID { get; set; }
        public decimal? Amount { get; set; }
        public string Incoterms { get; set; }
        public string PaymentTerms { get; set; }
        public DateTime DueDate { get; set; }
        public string IDN { get; set; }
        public List<PContractProductViewModel> ContractProducts { get; set; }

        public PContractEditViewModel()
        {
            ContractProducts = new List<PContractProductViewModel>();
        }
    }

    public class PContractProductViewModel
    {
        public int? ID { get; set; }
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public decimal Amount { get; set; }
        public int? UnitID { get; set; }
        public decimal? MOQ { get; set; }
        public int? HSCodeID { get; set; }
        public decimal VatPercent { get; set; }

        // uchun display: indicates agar product requires cutting (uses CBM)
        public bool RequiresCutting { get; set; }
    }
}
