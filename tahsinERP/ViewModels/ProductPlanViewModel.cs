using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    public class ProductPlanViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Mahsulotni tanlang")]
        [Display(Name = "Mahsulot")]
        public int ProductID { get; set; }
        
        [Required(ErrorMessage = "Reja miqdorini kiriting")]
        [Display(Name = "Reja miqdori")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Miqdor 0 dan katta bo'lishi kerak")]
        public double TotalQty { get; set; }
        
        [Required(ErrorMessage = "Boshlanish sanasini kiriting")]
        [Display(Name = "Boshlanish sanasi")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }
        
        [Required(ErrorMessage = "Tugash sanasini kiriting")]
        [Display(Name = "Tugash sanasi")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }
        
        [Display(Name = "O'chirilgan")]
        public bool IsDeleted { get; set; }
        
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
        [Display(Name = "Sana")]
        [DataType(DataType.Date)]
        public DateTime Day { get; set; }
        
        [Display(Name = "Kunduzgi smena")]
        [Range(0, double.MaxValue, ErrorMessage = "Miqdor 0 yoki undan katta bo'lishi kerak")]
        public double DayShift { get; set; }
        
        [Display(Name = "Tungi smena")]
        [Range(0, double.MaxValue, ErrorMessage = "Miqdor 0 yoki undan katta bo'lishi kerak")]
        public double NightShift { get; set; }
        
        [Display(Name = "Jami")]
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
}

