using System;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels.Tannarx
{
    public class TannarxEditViewModel : ITannarxCostFields
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Mes_Tan_Val_ProductRequired")]
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public string ProductType { get; set; }
        public string Dimensions { get; set; }
        
        [Required(ErrorMessage = "Mes_Tan_Val_YearRequired")]
        [Range(2020, 2100, ErrorMessage = "Mes_Tan_Val_YearInvalid")]
        public int Year { get; set; }
        
        [Required(ErrorMessage = "Mes_Tan_Val_MonthRequired")]
        [Range(1, 12, ErrorMessage = "Mes_Tan_Val_MonthRange")]
        public int Month { get; set; }
        public string MonthString { get; set; }
        
        // Material costs
        [Display(Name = "Mes_Tan_MaterialPrice")]
        [Required(ErrorMessage = "Mes_Tan_Val_MaterialPriceRequired")]
        [Range(0, double.MaxValue, ErrorMessage = "Mes_Tan_Val_MaterialPriceNegative")]
        public decimal MaterialPrice { get; set; }
        
        [Display(Name = "Mes_Tan_MaterialTransport")]
        public decimal MaterialTransportCost { get; set; }
        
        // Labor and expenses
        [Display(Name = "Mes_Tan_LaborA")]
        [Required(ErrorMessage = "Mes_Tan_Val_LaborRequired")]
        [Range(0, double.MaxValue, ErrorMessage = "Mes_Tan_Val_LaborNegative")]
        public decimal LaborCost { get; set; }
        
        [Display(Name = "Mes_Tan_TaxBankB")]
        public decimal TaxAndBank { get; set; }
        
        [Display(Name = "Mes_Tan_AdminC")]
        public decimal AdministrativeExpenses { get; set; }
        
        [Display(Name = "Mes_Tan_OtherD")]
        public decimal OtherExpenses { get; set; }
        
        [Display(Name = "Mes_Tan_ProfitE")]
        public decimal Profit { get; set; }
        
        // Percentage fields
        [Display(Name = "Mes_Tan_TaxBankBPct")]
        [Range(0, 100, ErrorMessage = "Mes_Tan_Val_PercentRange")]
        public decimal? TaxAndBankPercent { get; set; }
        
        [Display(Name = "Mes_Tan_AdminCPct")]
        [Range(0, 100, ErrorMessage = "Mes_Tan_Val_PercentRange")]
        public decimal? AdministrativeExpensesPercent { get; set; }
        
        [Display(Name = "Mes_Tan_OtherDPct")]
        [Range(0, 100, ErrorMessage = "Mes_Tan_Val_PercentRange")]
        public decimal? OtherExpensesPercent { get; set; }
        
        [Display(Name = "Mes_Tan_ProfitEPct")]
        [Range(0, 100, ErrorMessage = "Mes_Tan_Val_PercentRange")]
        public decimal? ProfitPercent { get; set; }
        
        // EXW Prices
        [Display(Name = "Mes_Tan_SalesExwNoVat")]
        public decimal SalesPriceEXWWithoutVAT { get; set; }
        
        [Display(Name = "Mes_Tan_SalesExwVat")]
        public decimal SalesPriceEXWWithVAT { get; set; }
        
        [Display(Name = "Mes_Tan_SetExwVat")]
        [Range(0, double.MaxValue, ErrorMessage = "Mes_Tan_Val_PriceNegative")]
        public decimal? SetPriceEXWWithVAT { get; set; }
        
        // Finished product transportation
        [Display(Name = "Mes_Tan_FinishedTransportPrice")]
        [Range(0, double.MaxValue, ErrorMessage = "Mes_Tan_Val_TransportNegative")]
        public decimal? FinishedProductTransportCost { get; set; }
        
        // CIP Prices
        [Display(Name = "Mes_Tan_SalesCipNoVat")]
        public decimal SalesPriceCIPWithoutVAT { get; set; }
        
        [Display(Name = "Mes_Tan_SalesCipVat")]
        public decimal SalesPriceCIPWithVAT { get; set; }
        
        [Display(Name = "Mes_Tan_SetCipVat")]
        [Range(0, double.MaxValue, ErrorMessage = "Mes_Tan_Val_PriceNegative")]
        public decimal? SetPriceCIPWithVAT { get; set; }
    }
}
