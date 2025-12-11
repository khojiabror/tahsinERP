using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    public class ProductPlanDailyEditViewModel
    {
        public int PlanID { get; set; }
        public List<DailyPlanItem> DailyPlans { get; set; }
        
        public ProductPlanDailyEditViewModel()
        {
            DailyPlans = new List<DailyPlanItem>();
        }
    }
    
    public class DailyPlanItem
    {
        public int ID { get; set; }
        public DateTime Day { get; set; }
        public double DayShift { get; set; }
        public double NightShift { get; set; }
        public double Total { get; set; }
        public double? ActualDayShift { get; set; }
        public double? ActualNightShift { get; set; }
        public double? ActualTotal { get; set; }
    }
    
    public class ProductPlanDetailsViewModel
    {
        public PRODUCTPLAN Plan { get; set; }
        public List<DailyPlanItem> DailyPlans { get; set; }
        public List<ChartDataPoint> PlanVsActualChart { get; set; }
        public List<SORDER_DELIVERY_SCHEDULE> LinkedDeliverySchedules { get; set; }
    }
    
    public class ChartDataPoint
    {
        public DateTime Date { get; set; }
        public double Planned { get; set; }
        public double Actual { get; set; }
    }
}

