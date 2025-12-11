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
        public double TotalTonnage { get; set; }
        public double TotalCBM { get; set; }
        public double TotalNetWeight { get; set; }
        
        // Cost statistics
        public double TotalCost { get; set; }
        public double UnpaidAmount { get; set; }
        public double PaidAmount { get; set; }
        
        // Performance metrics
        public double AverageCostPerKm { get; set; }
        public double AverageCostPerTon { get; set; }
        public double AverageCostPerTrip { get; set; }
        public double AverageCostPerCBM { get; set; }
        
        // OTIF (On Time In Full) performance
        public int OnTimeDeliveries { get; set; }
        public int LateDeliveries { get; set; }
        public double OTIFPercentage { get; set; }
        
        // Related data
        public List<F_CONTRACTS> ActiveContracts { get; set; }
        public List<F_WAYBILLS> RecentWaybills { get; set; }
        public List<F_WAYBILL_PAYMENTS> RecentPayments { get; set; }
        
        // Lane performance (top routes)
        public List<LanePerformance> TopLanes { get; set; }
    }
    
    /// <summary>
    /// Lane performance metrics
    /// </summary>
    public class LanePerformance
    {
        public string FromLocation { get; set; }
        public string ToLocation { get; set; }
        public int TripCount { get; set; }
        public double TotalTonnage { get; set; }
        public double TotalCost { get; set; }
        public double AverageCostPerTon { get; set; }
        public double AverageCostPerKm { get; set; }
    }
}
