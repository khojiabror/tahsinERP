namespace tahsinERP.ViewModels.Sales
{
    /// <summary>Sotuv invoysi qatori (Create/Edit formasi).</summary>
    public class SalesInvoiceLineViewModel
    {
        public int ProductID { get; set; }
        public string Description { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineAmount { get; set; }
        public decimal VATRate { get; set; }
        public decimal VATAmount { get; set; }
        public decimal TotalWithVAT { get; set; }
    }

    /// <summary>Hisob-faktura (SF) qatori.</summary>
    public class SalesSFLineViewModel
    {
        public long? InvoiceLineID { get; set; }
        public int ProductID { get; set; }
        public string Description { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineAmount { get; set; }
        public decimal VATRate { get; set; }
        public decimal VATAmount { get; set; }
        public decimal TotalWithVAT { get; set; }
    }

    /// <summary>TTNK qatori.</summary>
    public class SalesTTNKLineViewModel
    {
        public int? ProductID { get; set; }
        public long? InvoiceLineID { get; set; }
        public string HSCode { get; set; }
        public string Description { get; set; }
        public string UnitOfMeasure { get; set; }
        public decimal Qty { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? LineAmount { get; set; }
        public decimal? WeightNetT { get; set; }
        public decimal? WeightGrossT { get; set; }
        public int? PlaceCount { get; set; }
        public string PackageType { get; set; }
        public string Remark { get; set; }
    }
}
