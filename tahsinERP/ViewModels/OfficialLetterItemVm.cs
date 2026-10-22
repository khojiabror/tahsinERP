using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// Maktubdagi bitta mahsulot/narx satri.
    /// EXW va CIP shartlari uchun alohida narx (QQS bilan va QQSsiz) maydonlari.
    /// </summary>
    public class OfficialLetterItemVm
    {
        [Display(Name = "Doc_ProductName")]
        public string ItemName { get; set; }

        [Display(Name = "Doc_Dimension")]
        public string ItemSize { get; set; }

        [Display(Name = "Doc_PriceExw")]
        public decimal? PriceExw { get; set; }

        [Display(Name = "Doc_PriceVatExw")]
        public decimal? PriceVatExw { get; set; }

        [Display(Name = "Doc_PriceCip")]
        public decimal? PriceCip { get; set; }

        [Display(Name = "Doc_PriceVatCip")]
        public decimal? PriceVatCip { get; set; }
    }
}
