namespace tahsinERP.ViewModels
{
    public class LanePerformanceViewModel
    {
        public string FromLocation { get; set; }
        public string ToLocation { get; set; }
        public int TripCount { get; set; }
        public decimal TotalTonnage { get; set; }
        public decimal TotalCBM { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalDistance { get; set; }
        public decimal AvgCostPerTon { get; set; }
        public decimal AvgCostPerKm { get; set; }
        public string MainForwarder { get; set; }
    }
}
