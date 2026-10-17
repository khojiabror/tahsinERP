namespace tahsinERP.ViewModels
{
    /// <summary>Yuk tashuvchi statistikasi — Dashboard va ForwarderComparison uchun yagona model (LOG-D04).</summary>
    public class ForwarderComparisonViewModel
    {
        public int? ForwarderID { get; set; }
        public string ForwarderName { get; set; }
        public int TripCount { get; set; }
        public double TotalCost { get; set; }
        public double TotalTonnage { get; set; }
        public double TotalDistance { get; set; }
        public double AvgCostPerTon { get; set; }
        public double AvgCostPerKm { get; set; }
        public int OnTimeDeliveries { get; set; }
        public int TotalDeliveries { get; set; }
        public double OTIFPercentage { get; set; }

        /// <summary>Dashboard uchun: to'langan summa (WaybillLedgerService).</summary>
        public double TotalPaid { get; set; }
    }
}
