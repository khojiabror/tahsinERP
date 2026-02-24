namespace tahsinERP.ViewModels
{
    public class WaybillPaymentLinkedWaybillViewModel
    {
        public int WaybillID { get; set; }
        public string WaybillNo { get; set; }
        public double Amount { get; set; }
        public double Paid { get; set; }
        public double Outstanding { get; set; }
        public double Allocated { get; set; }
        public string Currency { get; set; }
    }
}
