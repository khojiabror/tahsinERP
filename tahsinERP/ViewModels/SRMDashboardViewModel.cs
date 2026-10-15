namespace tahsinERP.ViewModels
{
    public class SRMSpendBySupplierItem
    {
        public int SupplierID { get; set; }
        public string SupplierName { get; set; }
        public int InvoiceCount { get; set; }
        public decimal TotalSpend { get; set; }
    }

    public class SRMPayablesBySupplierItem
    {
        public int SupplierID { get; set; }
        public string SupplierName { get; set; }
        public decimal Outstanding { get; set; }
    }
}
