using System;
using System.Collections.Generic;

namespace tahsinERP.ViewModels.Tannarx
{
    public class TannarxAnalyticsDashboardViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthString { get; set; }
        
        // Summary metrics
        public int TotalProducts { get; set; }
        public int ProductsWithActualData { get; set; }
        
        // Total costs comparison
        public decimal TotalPlannedMaterialPrice { get; set; }
        public decimal TotalActualMaterialPrice { get; set; }
        public decimal MaterialPriceVariance { get; set; }
        public decimal MaterialPriceVariancePercent { get; set; }
        
        public decimal TotalPlannedLaborCost { get; set; }
        public decimal TotalActualLaborCost { get; set; }
        public decimal LaborCostVariance { get; set; }
        public decimal LaborCostVariancePercent { get; set; }
        
        public decimal TotalPlannedEXWWithVAT { get; set; }
        public decimal TotalActualEXWWithVAT { get; set; }
        public decimal EXWVariance { get; set; }
        public decimal EXWVariancePercent { get; set; }
        
        public decimal TotalPlannedCIPWithVAT { get; set; }
        public decimal TotalActualCIPWithVAT { get; set; }
        public decimal CIPVariance { get; set; }
        public decimal CIPVariancePercent { get; set; }
        
        // Top products by variance
        public List<TannarxProductVarianceViewModel> TopVarianceProducts { get; set; }
        
        // Monthly trend data
        public List<TannarxMonthlyTrendViewModel> MonthlyTrends { get; set; }
    }
    
    public class TannarxProductVarianceViewModel
    {
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public decimal PlannedPrice { get; set; }
        public decimal ActualPrice { get; set; }
        public decimal Variance { get; set; }
        public decimal VariancePercent { get; set; }
    }
    
    public class TannarxMonthlyTrendViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthString { get; set; }
        public decimal PlannedTotal { get; set; }
        public decimal ActualTotal { get; set; }
        public decimal Variance { get; set; }
    }
}
