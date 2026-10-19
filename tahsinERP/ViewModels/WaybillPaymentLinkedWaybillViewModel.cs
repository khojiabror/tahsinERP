namespace tahsinERP.ViewModels
{
    public class WaybillPaymentLinkedWaybillViewModel
    {
        public int WaybillID { get; set; }
        public string WaybillNo { get; set; }
        public decimal Amount { get; set; }
        public decimal Paid { get; set; }
        public decimal Outstanding { get; set; }
        public decimal Allocated { get; set; }
        public string Currency { get; set; }
    }
}
