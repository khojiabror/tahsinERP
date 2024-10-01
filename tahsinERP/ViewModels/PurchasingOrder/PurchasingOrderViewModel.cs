using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace tahsinERP.ViewModels.PurchasingOrder
{
    public class PurchasingOrderViewModel
    {
        public int ID { get; set; }
        public string OrderCode { get; set; }
        public string OrderCodeEDO { get; set; }
        public System.DateTime CreatedDT { get; set; }
        public int CreatedByUserID { get; set; }
        public Nullable<System.DateTime> ApprovedDTEDO { get; set; }
        public string Comment { get; set; }
        public string Status { get; set; }
        public int BuyerUserID { get; set; }
        [DataType(DataType.Upload)]
        public HttpPostedFileBase File { get; set; }
        public List<PurchasingOrderPart> Parts { get; set; }    
        public PurchasingOrderViewModel()
        {
            Parts = new List<PurchasingOrderPart>();
        }
    }
    public class PurchasingOrderPart
    {
        public int ItemID { get; set; }
        public double Price { get; set; }
        public double Amount { get; set; }
        public string Currency { get; set; }
    }
}