using System;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels
{
    public class NormCuttingLineViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Materialni tanlang")]
        [Display(Name = "Material")]
        public int MaterialID { get; set; }
        
        [Display(Name = "Material kodi")]
        public string MaterialCode { get; set; }
        
        [Display(Name = "Material nomi")]
        public string MaterialName { get; set; }
        
        [Display(Name = "Material turi")]
        public string MaterialType { get; set; }
        
        [Display(Name = "Uzunlik (mm)")]
        public double? Length { get; set; }
        
        [Display(Name = "Kenglik (mm)")]
        public double? Width { get; set; }
        
        [Display(Name = "Qalinlik (mm)")]
        public double? Height { get; set; }
        
        [Display(Name = "Standart uzunlik (mm)")]
        public double? StandardLengthMm { get; set; }
        
        [Display(Name = "Hisob (mm)")]
        public double? CalculatedLengthMm { get; set; }
        
        [Display(Name = "Hisob (m)")]
        public double? CalculatedLengthM { get; set; }
        
        [Display(Name = "Miqdori (dona)")]
        [Range(1, int.MaxValue, ErrorMessage = "Miqdor 1 dan katta bo'lishi kerak")]
        public int Quantity { get; set; } = 1;
        
        [Display(Name = "Bitta bar/sheet dan kesilgan dona")]
        [Range(1, int.MaxValue, ErrorMessage = "Dona 1 dan katta bo'lishi kerak")]
        public int PiecesPerBar { get; set; } = 1;
        
        [Display(Name = "Bitta mahsulot uchun sarflanish (m)")]
        public decimal ConsumptionPerProduct { get; set; }
        
        [Display(Name = "Bitta mahsulot uchun chiqindi (m)")]
        public decimal WastePerProduct { get; set; }
        
        [Display(Name = "Narx")]
        public decimal? UnitPrice { get; set; }
        
        [Display(Name = "Valyuta")]
        public string Currency { get; set; }
        
        [Display(Name = "Norma")]
        public decimal Norma { get; set; }
        
        [Display(Name = "Chiqindi")]
        public decimal Chiqindi { get; set; }
        
        [Display(Name = "Bitta mahsulot uchun umumiy narx")]
        public decimal TotalCostPerProduct { get; set; }
        
        [Display(Name = "Oy")]
        public string Month { get; set; }
        
        public bool IsSelected { get; set; } = true;
    }
}

