namespace tahsinERP.ViewModels
{
    public class ForwarderDashboardStatViewModel
    {
        public int? ForwarderID { get; set; }
        public string ForwarderName { get; set; }
        public int TripCount { get; set; }
        public double TotalCost { get; set; }
        public double TotalTonnage { get; set; }
        public double TotalPaid { get; set; }
    }
}
