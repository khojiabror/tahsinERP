using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace tahsinERP.ViewModels
{
    public class PurchasingContractViewModel
    {
        public int ID { get; set; }
        public string ContractNo { get; set; }
        public System.DateTime IssueDT { get; set; }
        public int CompanyID { get; set; }
        public string Supplier { get; set; }
        public string Currency { get; set; }
        public double Amount { get; set; }
        public string Incoterms { get; set; }
        public string PaymentTerms { get; set; }
        public System.DateTime DueDT { get; set; }
        public string Comment { get; set; }
        public int OrderID { get; set; }
        [DataType(DataType.Upload)]
        public HttpPostedFileBase File { get; set; }
        public List<PurchasingContractItem> Parts { get; set; }
        public PurchasingContractViewModel()
        {
            Parts = new List<PurchasingContractItem>();
        }
    }
    public class PurchasingContractItem
    {
        public int ID { get; set; }
        public int ContractID { get; set; }
        public int ItemID { get; set; }
        public double Price { get; set; }
        public double Amount { get; set; }
        public double TotalPrice { get; set; }
        public double MOQ { get; set; }
        public int UnitID { get; set; }
    }
}