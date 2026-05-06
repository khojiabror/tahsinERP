using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    public class PurchaseOrderTrackingIndexItemViewModel
    {
        public P_ORDERS POrder { get; set; }
        public OrderTrackingStatusViewModel TrackingStatus { get; set; }
    }

    public class OrderTrackingStatusViewModel
    {
        public int OrderID { get; set; }
        public string OrderNo { get; set; }
        public bool OrderCreated { get; set; }
        public System.DateTime? OrderCreatedDate { get; set; }
        public string OrderStatus { get; set; } // green/red

        public bool ContractCreated { get; set; }
        public string ContractNo { get; set; }
        public System.DateTime? ContractDate { get; set; }
        public string ContractStatus { get; set; } // green/red

        public bool InvoiceCreated { get; set; }
        public string InvoiceNo { get; set; }
        public System.DateTime? InvoiceDate { get; set; }
        public string InvoiceStatus { get; set; } // green/red

        public bool WarehouseReceived { get; set; }
        public System.DateTime? WarehouseReceiptDate { get; set; }
        public string WarehouseStatus { get; set; } // green/red
    }
}
