using System.Collections.Generic;
using System;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels
{
    public class SContractViewModel
    {
        public int ID { get; set; }
        public string ContractNo { get; set; }
        public int CustomerID { get; set; }
        public DateTime IssuedDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Incoterms { get; set; }
        public string PaymentTerms { get; set; }
        /// <summary>To'lov muddati (kun) — invoysning DueDate'i shundan hisoblanadi.</summary>
        [Range(0, 3650, ErrorMessage = "Crm_CtrPaymentDaysRange")]
        public int? PaymentDays { get; set; }
        public int CurrencyID { get; set; }
        public decimal Amount { get; set; }
        public List<SContractProductViewModel> ProductList { get; set; }
    }

    public class SContractProductViewModel
    {
        public int ID { get; set; }
        public int SContractID { get; set; }
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public decimal PiecePrice { get; set; }
        public int UnitID { get; set; }
        public decimal Amount { get; set; }
        public decimal VatPercent { get; set; }
        public ProductViewModel PRODUCT { get; set; }
    }

    public class ProductViewModel
    {
        public int ID { get; set; }
        public string PNo { get; set; }
        public string Name { get; set; }
    }
}
