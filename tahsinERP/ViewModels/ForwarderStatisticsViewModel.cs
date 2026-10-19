using System;
using System.Collections.Generic;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// Forwarder statistics and analytics ViewModel
    /// </summary>
    public class ForwarderStatisticsViewModel
    {
        public FORWARDER Forwarder { get; set; }
        
        // Trip statistics
        public int TotalTrips { get; set; }
        public int InboundTrips { get; set; }
        public int OutboundTrips { get; set; }
        public int InternalTrips { get; set; }
        
        // Cargo statistics
        public decimal TotalTonnage { get; set; }
        public decimal TotalCBM { get; set; }
        public decimal TotalNetWeight { get; set; }
        
        // Cost statistics
        public decimal TotalCost { get; set; }
        public decimal UnpaidAmount { get; set; }
        public decimal PaidAmount { get; set; }
        
        // Performance metrics
        public decimal AverageCostPerKm { get; set; }
        public decimal AverageCostPerTon { get; set; }
        public decimal AverageCostPerTrip { get; set; }
        public decimal AverageCostPerCBM { get; set; }
        
        // OTIF (On Time In Full) performance
        public int OnTimeDeliveries { get; set; }
        public int TotalDeliveries { get; set; }
        public int LateDeliveries { get; set; }
        public double OTIFPercentage { get; set; }
        
        // Related data
        public List<F_CONTRACTS> ActiveContracts { get; set; }
        public List<F_WAYBILLS> RecentWaybills { get; set; }
        public List<F_WAYBILL_PAYMENTS> RecentPayments { get; set; }
        
        // Lane performance (top routes)
        public List<LanePerformanceViewModel> TopLanes { get; set; }
    }
}
