using System;
using System.Collections.Generic;

namespace tahsinERP.ViewModels.Tannarx
{
    public class TannarxAnalyticsReportViewModel
    {
        public int? Year { get; set; }
        public int? Month { get; set; }
        public int? ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        
        // Planned values
        public decimal PlannedMaterialPrice { get; set; }
        public decimal PlannedMaterialTransportCost { get; set; }
        public decimal PlannedLaborCost { get; set; }
        public decimal PlannedTaxAndBank { get; set; }
        public decimal PlannedAdministrativeExpenses { get; set; }
        public decimal PlannedOtherExpenses { get; set; }
        public decimal PlannedProfit { get; set; }
        public decimal PlannedEXWWithoutVAT { get; set; }
        public decimal PlannedEXWWithVAT { get; set; }
        public decimal PlannedSetPriceEXWWithVAT { get; set; }
        public decimal PlannedFinishedProductTransportCost { get; set; }
        public decimal PlannedCIPWithoutVAT { get; set; }
        public decimal PlannedCIPWithVAT { get; set; }
        public decimal PlannedSetPriceCIPWithVAT { get; set; }
        
        // Actual values
        public decimal ActualMaterialPrice { get; set; }
        public decimal ActualMaterialTransportCost { get; set; }
        public decimal ActualLaborCost { get; set; }
        public decimal ActualTaxAndBank { get; set; }
        public decimal ActualAdministrativeExpenses { get; set; }
        public decimal ActualOtherExpenses { get; set; }
        public decimal ActualProfit { get; set; }
        public decimal ActualEXWWithoutVAT { get; set; }
        public decimal ActualEXWWithVAT { get; set; }
        public decimal ActualCIPWithoutVAT { get; set; }
        public decimal ActualCIPWithVAT { get; set; }
        public decimal ActualFinishedProductTransportCost { get; set; }
        
        // Variances
        public decimal MaterialPriceVariance { get; set; }
        public decimal MaterialPriceVariancePercent { get; set; }
        public decimal MaterialTransportVariance { get; set; }
        public decimal MaterialTransportVariancePercent { get; set; }
        public decimal LaborCostVariance { get; set; }
        public decimal LaborCostVariancePercent { get; set; }
        public decimal AdminExpensesVariance { get; set; }
        public decimal AdminExpensesVariancePercent { get; set; }
        public decimal EXWVariance { get; set; }
        public decimal EXWVariancePercent { get; set; }
        public decimal CIPVariance { get; set; }
        public decimal CIPVariancePercent { get; set; }
        public decimal FinishedProductTransportVariance { get; set; }
        public decimal FinishedProductTransportVariancePercent { get; set; }
        
        // Additional info
        public int? ActualProductionQuantity { get; set; }
        public DateTime? LastUpdated { get; set; }
        public string ActualMaterialCalculationNote { get; set; }
    }
    
    public class TannarxAnalyticsReportListViewModel
    {
        public int? Year { get; set; }
        public int? Month { get; set; }
        public List<TannarxAnalyticsReportViewModel> Reports { get; set; }
        public TannarxAnalyticsSummaryViewModel Summary { get; set; }
    }
    
    public class TannarxAnalyticsSummaryViewModel
    {
        public int TotalProducts { get; set; }
        public int ProductsWithActualData { get; set; }
        public decimal TotalPlannedCost { get; set; }
        public decimal TotalActualCost { get; set; }
        public decimal TotalVariance { get; set; }
        public decimal TotalVariancePercent { get; set; }
        public decimal AverageVariancePercent { get; set; }
    }
}
