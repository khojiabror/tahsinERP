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
        [Required(ErrorMessage = "Sanani tanlang")]
        [Display(Name = "Sana")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }
        
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

    /// <summary>
    /// Details sahifasi uchun: BOM/Norm sarf va chiqim hujjatlari.
    /// </summary>
    public class ProductProductionDetailsViewModel
    {
        public PRODUCT_PRODUCTION Production { get; set; }
        /// <summary>BOM/Norm bo'yicha hisoblangan sarf qatorlari (material, talab, chiqindi, jami).</summary>
        public List<BomConsumptionLineViewModel> BomConsumptionLines { get; set; }
        /// <summary>Ushbu ishlab chiqarishga bog'liq chiqim hujjatlari (BOM chiqimi).</summary>
        public List<ProductionExpenseLinkViewModel> ExpenseDocuments { get; set; }

        public ProductProductionDetailsViewModel()
        {
            BomConsumptionLines = new List<BomConsumptionLineViewModel>();
            ExpenseDocuments = new List<ProductionExpenseLinkViewModel>();
        }
    }

    public class BomConsumptionLineViewModel
    {
        public int MaterialID { get; set; }
        public string MaterialPNo { get; set; }
        public string MaterialName { get; set; }
        public double RequiredQtyPieces { get; set; }
        public double RequiredQtyMeters { get; set; }
        public double WasteQty { get; set; }
        public double TotalConsumption { get; set; }
        public string UnitName { get; set; }
        public bool TotalConsumptionIsInKg { get; set; }
    }

    public class ProductionExpenseLinkViewModel
    {
        public int ExpenseID { get; set; }
        public string DocNo { get; set; }
        public DateTime IssueDateTime { get; set; }
        public double Amount { get; set; }
        public double TotalPrice { get; set; }
        public int ProductCount { get; set; }
    }
}

