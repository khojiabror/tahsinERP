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
        public int ContractID { get; set; }
        public int OrderID { get; set; }
        public int OrderItemID { get; set; }
        public int ContractItemID { get; set; }
        public int PaymentID { get; set; }
        public int IncomeID { get; set; }
        public int IncomeItemID { get; set; }
        public string OrderedByDept { get; set; }
        public double? ContractAmount { get; set; }
        public string Supplier { get; set; }
        public double ContractAmountPercentage { get; set; }
        //payment
        public double? PaymentAmount { get; set; }
        public double PaymentAmountPercentage { get; set; } = 0;
        //warehouse ..
        public double? IncomeAmount { get; set; }
        public double IncomeAmountPercentage { get; set; } = 0;
    }

}