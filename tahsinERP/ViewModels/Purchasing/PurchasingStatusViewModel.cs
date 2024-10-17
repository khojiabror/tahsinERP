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
        public int Status { get; set; }
        public double OrderAmount { get; set; }
        public double ContractAmount { get; set; }
        public double PaymentAmount { get; set; }
        public double IncomeAmount { get; set; }
    }
}