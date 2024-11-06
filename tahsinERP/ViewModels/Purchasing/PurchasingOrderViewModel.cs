using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using tahsinERP.Models;

namespace tahsinERP.ViewModels.PurchasingOrder
{
    public class PurchasingOrderViewModel
    {
        public int ID { get; set; }
        public string OrderCode { get; set; }
        public string OrderCodeEDO { get; set; }
        public System.DateTime CreatedDT { get; set; }
        public int CreatedByUserID { get; set; }
        public System.DateTime ApprovedDTEDO { get; set; }
        public string Comment { get; set; }
        public int CurrencyID { get; set; }
        public string OrderedByDept { get; set; }
        public string OrderedByUser { get; set; }
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
        public int ID { get; set; }
        public PURCHASING_ITEMS Item { get; set; }
        public int ItemID { get; set; }
        public double Price { get; set; }
        public double Amount { get; set; }
        public double TotalPrice { get; set; }
    }
}