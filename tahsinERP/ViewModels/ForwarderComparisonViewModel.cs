namespace tahsinERP.ViewModels
{
    /// <summary>Yuk tashuvchi statistikasi — Dashboard va ForwarderComparison uchun yagona model (LOG-D04).</summary>
    public class ForwarderComparisonViewModel
    {
        public int? ForwarderID { get; set; }
        public string ForwarderName { get; set; }
        public int TripCount { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalTonnage { get; set; }
        public decimal TotalDistance { get; set; }
        public decimal AvgCostPerTon { get; set; }
        public decimal AvgCostPerKm { get; set; }
        public int OnTimeDeliveries { get; set; }
        public int TotalDeliveries { get; set; }
        public double OTIFPercentage { get; set; }

        /// <summary>Dashboard uchun: to'langan summa (WaybillLedgerService).</summary>
        public decimal TotalPaid { get; set; }
    }
}
