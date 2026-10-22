using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels
{
    public class ProductNormIndexViewModel
    {
        public int ProductID { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public int NormCount { get; set; }
    }

    public class ProductNormViewModel
    {
        public int ProductID { get; set; }
        
        [Display(Name = "Mes_ProductNo")]
        public string ProductCode { get; set; }
        
        [Display(Name = "Mes_ProductName")]
        public string ProductName { get; set; }
        
        [Display(Name = "Mes_Month")]
        [Required(ErrorMessage = "Mes_Norm_SelectMonth")]
        public string Period { get; set; }
        
        [Display(Name = "Mes_Norm_Version")]
        public string Version { get; set; }
        
        [Display(Name = "Mes_Norm_VersionId")]
        public int? NormVersionID { get; set; }
        
        [Display(Name = "Mes_Norm_EffectiveFrom")]
        public DateTime? EffectiveFrom { get; set; }
        
        [Display(Name = "Mes_Norm_EffectiveTo")]
        public DateTime? EffectiveTo { get; set; }
        
        [Display(Name = "Comment")]
        public string Comment { get; set; }
        
        public List<NormLineViewModel> CuttingLines { get; set; } = new List<NormLineViewModel>();
        
        // Aggregated totals per material
        public Dictionary<int, MaterialSummaryViewModel> MaterialSummaries { get; set; } = new Dictionary<int, MaterialSummaryViewModel>();
        
        // Overall totals
        [Display(Name = "Mes_Norm_TotalNorm")]
        public decimal TotalNorm { get; set; }
        
        [Display(Name = "Mes_Norm_TotalWaste")]
        public decimal TotalWaste { get; set; }
        
        [Display(Name = "Mes_Norm_TotalAmount")]
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
    
    public class NormVersionItemViewModel
    {
        public int NormVersionID { get; set; }
        public string Period { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string Comment { get; set; }
        public int LineCount { get; set; }
        public decimal TotalSarfSumma { get; set; }
        public decimal TotalChiqindiSumma { get; set; }
        public decimal UmumiySumma { get; set; }
    }
    
    public class ProductNormDetailsViewModel
    {
        public int ProductID { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public List<NormVersionItemViewModel> NormVersions { get; set; } = new List<NormVersionItemViewModel>();
    }
}

