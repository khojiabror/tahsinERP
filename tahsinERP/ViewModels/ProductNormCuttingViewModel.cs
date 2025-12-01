using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels
{
    public class ProductNormCuttingViewModel
    {
        public int ProductID { get; set; }
        
        [Display(Name = "Mahsulot raqami")]
        public string ProductCode { get; set; }
        
        [Display(Name = "Mahsulot nomi")]
        public string ProductName { get; set; }
        
        [Display(Name = "Oy")]
        [Required(ErrorMessage = "Oyni tanlang")]
        public string Period { get; set; }
        
        [Display(Name = "Versiya")]
        public string Version { get; set; }
        
        [Display(Name = "Versiya ID")]
        public int? NormVersionID { get; set; }
        
        [Display(Name = "Samarali sanadan")]
        public DateTime? EffectiveFrom { get; set; }
        
        [Display(Name = "Samarali sanagacha")]
        public DateTime? EffectiveTo { get; set; }
        
        [Display(Name = "Izoh")]
        public string Comment { get; set; }
        
        public List<NormCuttingLineViewModel> CuttingLines { get; set; } = new List<NormCuttingLineViewModel>();
        
        // Aggregated totals per material
        public Dictionary<int, MaterialSummaryViewModel> MaterialSummaries { get; set; } = new Dictionary<int, MaterialSummaryViewModel>();
        
        // Overall totals
        [Display(Name = "Umumiy norma")]
        public decimal TotalNorm { get; set; }
        
        [Display(Name = "Umumiy chiqindi")]
        public decimal TotalWaste { get; set; }
        
        [Display(Name = "Umumiy summa")]
        public decimal TotalCost { get; set; }
        
        // Optimization suggestion
        public OptimizationSuggestionViewModel OptimizationSuggestion { get; set; }
        
        public bool HasOptimizationSuggestion { get; set; }
    }
    
    public class MaterialSummaryViewModel
    {
        public int MaterialID { get; set; }
        public string MaterialName { get; set; }
        public decimal TotalConsumption { get; set; }
        public decimal TotalWaste { get; set; }
        public decimal TotalCost { get; set; }
        public List<int> SelectedLineIDs { get; set; } = new List<int>();
    }
    
    public class OptimizationSuggestionViewModel
    {
        public Dictionary<int, MaterialSummaryViewModel> SuggestedMaterials { get; set; } = new Dictionary<int, MaterialSummaryViewModel>();
        public decimal SuggestedTotalNorm { get; set; }
        public decimal SuggestedTotalWaste { get; set; }
        public decimal SuggestedTotalCost { get; set; }
        public string Message { get; set; }
    }
}

