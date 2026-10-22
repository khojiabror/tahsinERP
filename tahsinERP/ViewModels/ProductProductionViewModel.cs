using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    public class ProductProductionViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Mes_Val_SelectProduct")]
        [Display(Name = "Mes_Product")]
        public int ProductID { get; set; }
        
        [Required(ErrorMessage = "Mes_Prod_Val_SelectShop")]
        [Display(Name = "Mes_Prod_Shop")]
        public int ShopID { get; set; }
        
        [Required(ErrorMessage = "Mes_Prod_Val_EnterDate")]
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }
        
        [Display(Name = "Mes_DayShift")]
        [Range(0, double.MaxValue, ErrorMessage = "Mes_Val_QtyNonNegative")]
        public decimal DayShift { get; set; }
        
        [Display(Name = "Mes_NightShift")]
        [Range(0, double.MaxValue, ErrorMessage = "Mes_Val_QtyNonNegative")]
        public decimal NightShift { get; set; }
        
        [Display(Name = "Total")]
        public decimal Total { get; set; }
        
        [Display(Name = "Comment")]
        public string Comment { get; set; }
        
        // Navigation properties
        public PRODUCT Product { get; set; }
        public SHOP Shop { get; set; }
    }
    
    public class ProductProductionDailyEntryViewModel
    {
        [Required(ErrorMessage = "Mes_Prod_Val_SelectPlanDate")]
        [Display(Name = "Mes_Prod_PlanDate")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Mes_Prod_Val_SelectProductionDate")]
        [Display(Name = "Mes_FactProductionDate")]
        [DataType(DataType.Date)]
        public DateTime ProductionDate { get; set; }
        
        [Required(ErrorMessage = "Mes_Prod_Val_SelectShop")]
        [Display(Name = "Mes_Prod_Shop")]
        public int ShopID { get; set; }
        
        public List<ProductionEntryItem> ProductionItems { get; set; }
        
        public ProductProductionDailyEntryViewModel()
        {
            ProductionItems = new List<ProductionEntryItem>();
        }
    }
    
    public class ProductionEntryItem
    {
        public int? ID { get; set; }
        public int ProductID { get; set; }
        public string ProductNo { get; set; }
        public string ProductName { get; set; }
        public decimal? PlannedDayShift { get; set; }
        public decimal? PlannedNightShift { get; set; }
        public decimal? PlannedTotal { get; set; }
        public decimal DayShift { get; set; }
        public decimal NightShift { get; set; }
        public decimal Total { get; set; }
        public string Comment { get; set; }
        public bool IsPlanned { get; set; }
    }
}

