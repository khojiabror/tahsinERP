using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    public class QualityInspectionViewModel
    {
        public long ID { get; set; }
        
        [Display(Name = "Mes_Order")]
        public int? SOrderID { get; set; }
        
        [Display(Name = "Mes_Qi_DeliverySchedule")]
        public long? DeliveryScheduleID { get; set; }
        
        [Display(Name = "Mes_Qi_DailyPlan")]
        public int? ProductPlanDailyID { get; set; }
        
        [Display(Name = "Mes_Qi_Production")]
        public int? ProductionID { get; set; }
        
        [Required(ErrorMessage = "Mes_Val_SelectProduct")]
        [Display(Name = "Mes_Product")]
        public int ProductID { get; set; }
        
        [Required(ErrorMessage = "Mes_Qi_Val_EnterInspected")]
        [Display(Name = "Mes_Qi_QtyInspected")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Mes_Qi_Val_QtyPositive")]
        public decimal QtyInspected { get; set; }
        
        [Required(ErrorMessage = "Mes_Qi_Val_EnterAccepted")]
        [Display(Name = "Mes_Qi_QtyAccepted")]
        [Range(0, double.MaxValue, ErrorMessage = "Mes_Val_QtyNonNegative")]
        public decimal QtyAccepted { get; set; }
        
        [Required(ErrorMessage = "Mes_Qi_Val_EnterRejected")]
        [Display(Name = "Mes_Qi_QtyRejected")]
        [Range(0, double.MaxValue, ErrorMessage = "Mes_Val_QtyNonNegative")]
        public decimal QtyRejected { get; set; }
        
        [Display(Name = "Mes_Dt_Name")]
        public int? DefectTypeID { get; set; }
        
        [Display(Name = "Status")]
        public string StatusCode { get; set; }
        
        [Display(Name = "Mes_Qi_Notes")]
        public string Notes { get; set; }
        
        [Display(Name = "Mes_Qi_InspectionDate")]
        [DataType(DataType.Date)]
        public DateTime InspectionDate { get; set; }
        
        [Display(Name = "Mes_Qi_ApprovalDate")]
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

    public class QualityInspectionDailyViewModel
    {
        [Display(Name = "Mes_Qi_InspectionDate")]
        [DataType(DataType.Date)]
        public DateTime InspectionDate { get; set; }

        public List<QualityInspectionDailyItemViewModel> Items { get; set; }

        /// <summary>
        /// Tanlangan kalendar kunida (filtrdan keyin) nechta ProductProduction borligini bildiradi.
        /// </summary>
        public int ProductionRowCountForDay { get; set; }
    }

    public class QualityInspectionDailyItemViewModel
    {
        public int ProductionID { get; set; }
        public int ProductID { get; set; }
        public string ProductNo { get; set; }
        public string ProductName { get; set; }

        public decimal ProducedQty { get; set; }
        public decimal AlreadyInspectedQty { get; set; }
        public decimal RemainingQty { get; set; }

        [Display(Name = "Mes_Qi_QtyAccepted")]
        [Range(0, double.MaxValue, ErrorMessage = "Mes_Val_QtyNonNegative")]
        public decimal QtyAccepted { get; set; }

        [Display(Name = "Mes_Qi_QtyRejected")]
        [Range(0, double.MaxValue, ErrorMessage = "Mes_Val_QtyNonNegative")]
        public decimal QtyRejected { get; set; }

        [Display(Name = "Mes_Dt_Name")]
        public int? DefectTypeID { get; set; }

        [Display(Name = "Mes_Qi_Notes")]
        public string Notes { get; set; }
    }
}

