namespace tahsinERP.ViewModels
{
    public class LaneDashboardStatViewModel
    {
        public string FromLocation { get; set; }
        public string ToLocation { get; set; }
        public int TripCount { get; set; }
        public double TotalCost { get; set; }
        public double TotalTonnage { get; set; }
        public double TotalDistance { get; set; }
        public double AvgCostPerTon { get; set; }
        public double AvgCostPerKm { get; set; }
    }
}
