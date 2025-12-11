using System;
using System.Collections.Generic;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    public class MESDashboardViewModel
    {
        // Today's KPIs
        public double TodayPlanned { get; set; }
        public double TodayActual { get; set; }
        public double TodayPlanVsActualPercent { get; set; }
        
        // Month's KPIs
        public double MonthPlanned { get; set; }
        public double MonthActual { get; set; }
        public double MonthPlanVsActualPercent { get; set; }
        
        // Charts data
        public List<DailyPlanVsActualData> DailyPlanVsActual { get; set; }
        public List<ProductionByShopData> ProductionByShop { get; set; }
        
        // Quality summary
        public int TotalInspections { get; set; }
        public int ApprovedInspections { get; set; }
        public int RejectedInspections { get; set; }
        public double TotalAcceptedQty { get; set; }
        public double TotalRejectedQty { get; set; }
        
        // Bottlenecks
        public List<OrderBottleneck> OrderBottlenecks { get; set; }
        
        public MESDashboardViewModel()
        {
            DailyPlanVsActual = new List<DailyPlanVsActualData>();
            ProductionByShop = new List<ProductionByShopData>();
            OrderBottlenecks = new List<OrderBottleneck>();
        }
    }
    
    public class DailyPlanVsActualData
    {
        public DateTime Date { get; set; }
        public double Planned { get; set; }
        public double Actual { get; set; }
    }
    
    public class ProductionByShopData
    {
        public int ShopID { get; set; }
        public string ShopName { get; set; }
        public double TotalProduction { get; set; }
    }
    
    public class OrderBottleneck
    {
        public int OrderID { get; set; }
        public string OrderNo { get; set; }
        public int ProductID { get; set; }
        public string ProductNo { get; set; }
        public double OrderedQty { get; set; }
        public double ProducedQty { get; set; }
        public double InspectedQty { get; set; }
        public double InvoicedQty { get; set; }
        public double RemainingQty { get; set; }
    }
}

