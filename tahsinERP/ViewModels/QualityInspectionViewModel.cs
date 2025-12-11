using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    public class QualityInspectionViewModel
    {
        public long ID { get; set; }
        
        [Display(Name = "Buyurtma")]
        public int? SOrderID { get; set; }
        
        [Display(Name = "Yetkazib berish jadvali")]
        public long? DeliveryScheduleID { get; set; }
        
        [Display(Name = "Kunlik reja")]
        public int? ProductPlanDailyID { get; set; }
        
        [Display(Name = "Ishlab chiqarish")]
        public int? ProductionID { get; set; }
        
        [Required(ErrorMessage = "Mahsulotni tanlang")]
        [Display(Name = "Mahsulot")]
        public int ProductID { get; set; }
        
        [Required(ErrorMessage = "Tekshirilgan miqdorni kiriting")]
        [Display(Name = "Tekshirilgan miqdor")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Miqdor 0 dan katta bo'lishi kerak")]
        public decimal QtyInspected { get; set; }
        
        [Required(ErrorMessage = "Qabul qilingan miqdorni kiriting")]
        [Display(Name = "Qabul qilingan miqdor")]
        [Range(0, double.MaxValue, ErrorMessage = "Miqdor 0 yoki undan katta bo'lishi kerak")]
        public decimal QtyAccepted { get; set; }
        
        [Required(ErrorMessage = "Rad etilgan miqdorni kiriting")]
        [Display(Name = "Rad etilgan miqdor")]
        [Range(0, double.MaxValue, ErrorMessage = "Miqdor 0 yoki undan katta bo'lishi kerak")]
        public decimal QtyRejected { get; set; }
        
        [Display(Name = "Nuqson turi")]
        public int? DefectTypeID { get; set; }
        
        [Display(Name = "Holat")]
        public string StatusCode { get; set; }
        
        [Display(Name = "Izohlar")]
        public string Notes { get; set; }
        
        [Display(Name = "Tekshiruv sanasi")]
        [DataType(DataType.Date)]
        public DateTime InspectionDate { get; set; }
        
        [Display(Name = "Tasdiqlash sanasi")]
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate { get; set; }
        
        // Navigation properties for display
        public PRODUCT Product { get; set; }
        public DEFECT_TYPES DefectType { get; set; }
        public S_ORDERS SOrder { get; set; }
        public SORDER_DELIVERY_SCHEDULE DeliverySchedule { get; set; }
        public PRODUCTPLANS_DAILY ProductPlanDaily { get; set; }
        public PRODUCT_PRODUCTION Production { get; set; }
    }
    
    public class QualityInspectionListViewModel
    {
        public List<QualityInspectionViewModel> Inspections { get; set; }
        public string SearchTerm { get; set; }
        public int? ProductID { get; set; }
        public string StatusCode { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}

