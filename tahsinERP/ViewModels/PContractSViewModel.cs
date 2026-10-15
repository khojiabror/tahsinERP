using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    public class PContractSViewModel
    {
        public int ID { get; set; }
        [Required]
        public string ContractNo { get; set; }
        [DataType(DataType.Upload)]
        public HttpPostedFileBase File { get; set; }
        [Required]
        public DateTime IssuedDate { get; set; }
        [Required]
        public DateTime DueDate { get; set; }
        [Required]
        public int SupplierID { get; set; }
        [Required]
        public string Incoterms { get; set; }
        [Required]
        public string PaymentTerms { get; set; }
        [Required]
        public int CurrencyID { get; set; }
        public double Amount { get; set; }
        [Required]
        public string IDN { get; set; }
        public List<PContractSPart> Parts { get; set; }
        public PContractSViewModel()
        {
            Parts = new List<PContractSPart>();
        }
    }
    // Part ViewModel
    public class PContractSPart
    {
        public string Marka { get; set; }
        public string Standart { get; set; }
        public string Coating { get; set; }
        public float Thickness { get; set; }
        public float Width { get; set; }
        public int UnitID { get; set; }
        [Required]
        public double Price { get; set; }
        [Required]
        public double Quantity { get; set; }
        public double TotalPrice { get; set; }
        public int HsCodeID { get; set; }
    }
}