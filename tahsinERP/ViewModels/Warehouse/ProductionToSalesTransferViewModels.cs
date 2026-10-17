using System;

namespace tahsinERP.ViewModels
{
    // ProductionToSalesTransfer modellari (SRM-M02: avval controller faylida e'lon qilingan edi).
    public class ProductionToSalesTransferViewModel
    {
        public DateTime TransferDate { get; set; }
        public string TransferDocNo { get; set; }
        public int ProductionWHID { get; set; }
        public int SalesWHID { get; set; }
        public string Description { get; set; }
    }

    public class TransferLineViewModel
    {
        public int ProductID { get; set; }
        public double Quantity { get; set; }
        public int? IncomeID { get; set; }
        /// <summary>ProdID:123:7|ProdID:124:3</summary>
        public string LotAllocations { get; set; }
    }
}
