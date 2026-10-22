using System;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels
{
    public class NormLineViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Mes_Norm_SelectMaterial")]
        [Display(Name = "Mes_Norm_Material")]
        public int MaterialID { get; set; }
        
        [Display(Name = "Mes_MaterialCode")]
        public string MaterialCode { get; set; }
        
        [Display(Name = "Mes_MaterialName")]
        public string MaterialName { get; set; }
        
        [Display(Name = "Mes_Norm_MaterialType")]
        public string MaterialType { get; set; }
        
        [Display(Name = "Mes_Norm_LengthMm")]
        public double? Length { get; set; }
        
        [Display(Name = "Mes_Norm_WidthMm")]
        public double? Width { get; set; }
        
        [Display(Name = "Mes_Norm_ThicknessMm")]
        public double? Height { get; set; }
        
        [Display(Name = "Mes_Norm_StandardLengthMm")]
        public double? StandardLengthMm { get; set; }
        
        [Display(Name = "Mes_Norm_CalcMm")]
        public double? CalculatedLengthMm { get; set; }
        
        [Display(Name = "Mes_Norm_CalcM")]
        public double? CalculatedLengthM { get; set; }
        
        [Display(Name = "Mes_Norm_QuantityPcs")]
        [Range(0, int.MaxValue, ErrorMessage = "Mes_Norm_Val_QtyNonNegative")]
        public int Quantity { get; set; } = 1;
        
        [Display(Name = "Mes_Norm_PiecesPerBar")]
        [Range(1, int.MaxValue, ErrorMessage = "Mes_Norm_Val_PiecesMin1")]
        public int PiecesPerBar { get; set; } = 1;
        
        [Display(Name = "Mes_Norm_ConsumptionPerProduct")]
        public decimal ConsumptionPerProduct { get; set; }
        
        [Display(Name = "Mes_Norm_WastePerProduct")]
        public decimal WastePerProduct { get; set; }

        public bool IsWasteManual { get; set; }
        
        [Display(Name = "Unit")]
        public int? UnitID { get; set; }

        [Display(Name = "Price")]
        public decimal? UnitPrice { get; set; }
        
        [Display(Name = "Currency")]
        public string Currency { get; set; }
        
        [Display(Name = "Mes_Norm_Norma")]
        public decimal Norma { get; set; }
        
        [Display(Name = "Mes_Norm_Waste")]
        public decimal Chiqindi { get; set; }
        
        [Display(Name = "Mes_Norm_TotalCostPerProduct")]
        public decimal TotalCostPerProduct { get; set; }
        
        [Display(Name = "Mes_Month")]
        public string Month { get; set; }

        // Kesish usuli — foydalanuvchi kiritadigan erkin matn (qaysi yog'ochdan qaysi o'lchamda qanchadan kesish).
        // NORM_INPUT_LINES.Comment ustuniga saqlanadi.
        [Display(Name = "Mes_Norm_CuttingMethod")]
        public string CuttingMethod { get; set; }

        public bool IsSelected { get; set; } = true;

        // Optimizatsiya natijasi: bu qator boshqa (uzunroq) kesimning chiqindisidan olingan bo'lsa true.
        public bool UsesWaste { get; set; }

        // Optimizatsiya izohi (qaysi qator chiqindisidan, qancha olingani va h.k.).
        public string OptimizationNote { get; set; }
    }
}

