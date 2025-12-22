using System;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels.Tannarx
{
    public class TannarxEditViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Mahsulot tanlanishi shart")]
        public int ProductID { get; set; }
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public string ProductType { get; set; }
        public string Dimensions { get; set; }
        
        [Required(ErrorMessage = "Yil tanlanishi shart")]
        [Range(2020, 2100, ErrorMessage = "Yil noto'g'ri")]
        public int Year { get; set; }
        
        [Required(ErrorMessage = "Oy tanlanishi shart")]
        [Range(1, 12, ErrorMessage = "Oy 1-12 orasida bo'lishi kerak")]
        public int Month { get; set; }
        public string MonthString { get; set; }
        
        // Material costs
        [Display(Name = "Material narxi")]
        [Required(ErrorMessage = "Material narxi kiritilishi shart")]
        [Range(0, double.MaxValue, ErrorMessage = "Material narxi manfiy bo'lishi mumkin emas")]
        public decimal MaterialPrice { get; set; }
        
        [Display(Name = "Material tashuvi narxi")]
        public decimal MaterialTransportCost { get; set; }
        
        // Labor and expenses
        [Display(Name = "A - Ish haqi (Faqat ishchilar)")]
        [Required(ErrorMessage = "Ish haqi kiritilishi shart")]
        [Range(0, double.MaxValue, ErrorMessage = "Ish haqi manfiy bo'lishi mumkin emas")]
        public decimal LaborCost { get; set; }
        
        [Display(Name = "B - Soliq va bank")]
        public decimal TaxAndBank { get; set; }
        
        [Display(Name = "C - Adminstrativ harajatlar")]
        public decimal AdministrativeExpenses { get; set; }
        
        [Display(Name = "D - Boshqa xarajatlar")]
        public decimal OtherExpenses { get; set; }
        
        [Display(Name = "E - Foyda")]
        public decimal Profit { get; set; }
        
        // Percentage fields
        [Display(Name = "B - Soliq va bank (%)")]
        [Range(0, 100, ErrorMessage = "Foiz 0-100 orasida bo'lishi kerak")]
        public decimal? TaxAndBankPercent { get; set; }
        
        [Display(Name = "C - Adminstrativ harajatlar (%)")]
        [Range(0, 100, ErrorMessage = "Foiz 0-100 orasida bo'lishi kerak")]
        public decimal? AdministrativeExpensesPercent { get; set; }
        
        [Display(Name = "D - Boshqa xarajatlar (%)")]
        [Range(0, 100, ErrorMessage = "Foiz 0-100 orasida bo'lishi kerak")]
        public decimal? OtherExpensesPercent { get; set; }
        
        [Display(Name = "E - Foyda (%)")]
        [Range(0, 100, ErrorMessage = "Foiz 0-100 orasida bo'lishi kerak")]
        public decimal? ProfitPercent { get; set; }
        
        // EXW Prices
        [Display(Name = "Sotuv narxi EXW QQS siz")]
        public decimal SalesPriceEXWWithoutVAT { get; set; }
        
        [Display(Name = "Sotuv narxi QQS bilan EXW")]
        public decimal SalesPriceEXWWithVAT { get; set; }
        
        [Display(Name = "Belgilangan narxi QQS bilan EXW")]
        [Range(0, double.MaxValue, ErrorMessage = "Narx manfiy bo'lishi mumkin emas")]
        public decimal? SetPriceEXWWithVAT { get; set; }
        
        // Finished product transportation
        [Display(Name = "Tayyor maxsulot tashuvi narxi")]
        [Range(0, double.MaxValue, ErrorMessage = "Tashuv narxi manfiy bo'lishi mumkin emas")]
        public decimal? FinishedProductTransportCost { get; set; }
        
        // CIP Prices
        [Display(Name = "Sotuv narxi CIP QQS siz")]
        public decimal SalesPriceCIPWithoutVAT { get; set; }
        
        [Display(Name = "Sotuv narxi QQS bilan CIP")]
        public decimal SalesPriceCIPWithVAT { get; set; }
        
        [Display(Name = "Belgilangan QQS bilan CIP")]
        [Range(0, double.MaxValue, ErrorMessage = "Narx manfiy bo'lishi mumkin emas")]
        public decimal? SetPriceCIPWithVAT { get; set; }
    }
}
