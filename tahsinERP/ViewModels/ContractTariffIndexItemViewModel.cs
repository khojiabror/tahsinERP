namespace tahsinERP.ViewModels
{
    public class ContractTariffIndexItemViewModel
    {
        public int ID { get; set; }
        public string ContractNo { get; set; }
        public string TransportType { get; set; }
        public string FromLocation { get; set; }
        public string ToLocation { get; set; }
        public byte FreightBasis { get; set; }
        public double Rate { get; set; }
        public string Currency { get; set; }
        public bool IsActive { get; set; }
    }
}
