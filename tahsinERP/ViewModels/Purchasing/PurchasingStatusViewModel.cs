using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace tahsinERP.ViewModels.Purchasing
{
    public class PurchasingStatusViewModel
    {
        public int ItemID { get; set; }
        public string ItemName { get; set; }
        public bool? Status { get; set; } = false;
        public double? OrderAmount { get; set; }
        public double? ContractAmount { get; set; }
        public double? PaymentAmount { get; set; }
        public string Supplier { get; set; }
        public string Currency { get; set; }
        public string OrderCode { get; set; }
        public string OrderedByDept { get; set; }
        public double? ItemAmount { get; set; }
        public bool? IsPayment { get; set; } = false;
    }

}