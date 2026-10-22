using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    public class ProductPlanViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Mes_Val_SelectProduct")]
        [Display(Name = "Mes_Product")]
        public int ProductID { get; set; }
        
        [Required(ErrorMessage = "Mes_Plan_Val_EnterPlanQty")]
        [Display(Name = "Mes_Plan_PlanQty")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Mes_Plan_Val_QtyPositive")]
        public double TotalQty { get; set; }
        
        [Required(ErrorMessage = "Mes_Plan_Val_EnterStartDate")]
        [Display(Name = "Mes_Plan_StartDate")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }
        
        [Required(ErrorMessage = "Mes_Plan_Val_EnterEndDate")]
        [Display(Name = "Mes_Plan_EndDate")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }
        
        [Display(Name = "Mes_Plan_IsDeleted")]
        public bool IsDeleted { get; set; }

        [Display(Name = "Mes_Plan_SalesOrder")]
        public int? SalesOrderID { get; set; }
        public string SalesOrderNo { get; set; }
        
        // Navigation properties
        public PRODUCT Product { get; set; }
        public List<ProductPlanDailyViewModel> DailyPlans { get; set; }
        public List<SORDER_DELIVERY_SCHEDULE> LinkedDeliverySchedules { get; set; }
        
        public ProductPlanViewModel()
        {
            DailyPlans = new List<ProductPlanDailyViewModel>();
            LinkedDeliverySchedules = new List<SORDER_DELIVERY_SCHEDULE>();
            StartDate = DateTime.Now;
            EndDate = DateTime.Now.AddDays(30);
        }
    }
    
    public class ProductPlanDailyViewModel
    {
        public int ID { get; set; }
        public int PlanID { get; set; }
        
        [Required]
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime Day { get; set; }
        
        [Display(Name = "Mes_DayShift")]
        [Range(0, double.MaxValue, ErrorMessage = "Mes_Val_QtyNonNegative")]
        public double DayShift { get; set; }
        
        [Display(Name = "Mes_NightShift")]
        [Range(0, double.MaxValue, ErrorMessage = "Mes_Val_QtyNonNegative")]
        public double NightShift { get; set; }
        
        [Display(Name = "Total")]
        public double Total { get; set; }
        
        // Actual production data for comparison
        public double? ActualDayShift { get; set; }
        public double? ActualNightShift { get; set; }
        public double? ActualTotal { get; set; }
    }
    
    public class ProductPlanListViewModel
    {
        public List<ProductPlanViewModel> Plans { get; set; }
        public int? ProductID { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        
        public ProductPlanListViewModel()
        {
            Plans = new List<ProductPlanViewModel>();
        }
    }

    public class ProductPlanGridEntryViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public List<ProductPlanGridRowViewModel> Rows { get; set; }

        public ProductPlanGridEntryViewModel()
        {
            Rows = new List<ProductPlanGridRowViewModel>();
            Year = DateTime.Now.Year;
            Month = DateTime.Now.Month;
        }
    }

    public class ProductPlanGridRowViewModel
    {
        public int? PlanID { get; set; }
        public int? ProductID { get; set; }
        public double? TotalQty { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? SalesOrderID { get; set; }
    }
}

