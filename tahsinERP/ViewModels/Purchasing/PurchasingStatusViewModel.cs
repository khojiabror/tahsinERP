using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace tahsinERP.ViewModels.Purchasing
{
    public class PurchasingStatusViewModel
    {
        public string ItemName { get; set; }
        public bool? Status { get; set; } = false;
        public double? OrderAmount { get; set; }
        public int OrderID { get; set; }
        public string OrderedByDept { get; set; }
        //public string OrderCodeEDO { get; set; }
        //public DateTime OrderedDate { get; set; }
        //contract
        public double? ContractAmount { get; set; }
        public string Supplier { get; set; }
        public double ContractAmountStatus { get; set; }
        //payment
        public double? PaymentAmount { get; set; }
        public double PaymentAmountStatus { get; set; } = 0;
        //warehouse ..
        public double? IncomeAmount { get; set; }
        public double IncomeAmountStatus { get; set; } = 0;
    }

}