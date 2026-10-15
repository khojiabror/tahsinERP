using System;

namespace tahsinERP.ViewModels
{
    public class WaybillIndexItemViewModel
    {
        public int ID { get; set; }
        public string WaybillNo { get; set; }
        public DateTime? IssueDate { get; set; }
        public byte? Direction { get; set; }
        public string ForwarderName { get; set; }
        public string ContractNo { get; set; }
        public string TransportType { get; set; }
        public string FromLocation { get; set; }
        public string ToLocation { get; set; }
        public string TruckNo { get; set; }
        public string DriverName { get; set; }
        public double? DistanceKm { get; set; }
        public double? GrWeight { get; set; }
        public double? CBM { get; set; }
        public double? Amount { get; set; }
        public string Currency { get; set; }
        public double TotalPaid { get; set; }
        public double Outstanding { get; set; }
        public string PaymentStatus { get; set; }
        public string CargoTypeText { get; set; }
        public string LinkedDoc { get; set; }
    }
}
