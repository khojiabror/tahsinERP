using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    public class ProductProductionViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Mahsulotni tanlang")]
        [Display(Name = "Mahsulot")]
        public int ProductID { get; set; }
        
        [Required(ErrorMessage = "Sexni tanlang")]
        [Display(Name = "Sex")]
        public int ShopID { get; set; }
        
        [Required(ErrorMessage = "Sanani kiriting")]
        [Display(Name = "Sana")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }
        
        [Display(Name = "Kunduzgi smena")]
        [Range(0, double.MaxValue, ErrorMessage = "Miqdor 0 yoki undan katta bo'lishi kerak")]
        public double DayShift { get; set; }
        
        [Display(Name = "Tungi smena")]
        [Range(0, double.MaxValue, ErrorMessage = "Miqdor 0 yoki undan katta bo'lishi kerak")]
        public double NightShift { get; set; }
        
        [Display(Name = "Jami")]
        public double Total { get; set; }
        
        [Display(Name = "Izoh")]
        public string Comment { get; set; }
        
        // Navigation properties
        public PRODUCT Product { get; set; }
        public SHOP Shop { get; set; }
    }
    
    public class ProductProductionDailyEntryViewModel
    {
        [Required(ErrorMessage = "Reja sanasini tanlang")]
        [Display(Name = "Reja sanasi")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Fakt ishlab chiqarish sanasini tanlang")]
        [Display(Name = "Fakt ishlab chiqarish sanasi")]
        [DataType(DataType.Date)]
        public DateTime ProductionDate { get; set; }
        
        [Required(ErrorMessage = "Sexni tanlang")]
        [Display(Name = "Sex")]
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
        public double? PlannedDayShift { get; set; }
        public double? PlannedNightShift { get; set; }
        public double? PlannedTotal { get; set; }
        public double DayShift { get; set; }
        public double NightShift { get; set; }
        public double Total { get; set; }
        public string Comment { get; set; }
        public bool IsPlanned { get; set; }
    }
}

