using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// Maktubdagi bitta mahsulot/narx satri.
    /// EXW va CIP shartlari uchun alohida narx (QQS bilan va QQSsiz) maydonlari.
    /// </summary>
    public class OfficialLetterItemVm
    {
        [Display(Name = "Mahsulot nomi")]
        public string ItemName { get; set; }

        [Display(Name = "O'lchami")]
        public string ItemSize { get; set; }

        [Display(Name = "Narx EXW")]
        public decimal? PriceExw { get; set; }

        [Display(Name = "Narx QQS bilan EXW")]
        public decimal? PriceVatExw { get; set; }

        [Display(Name = "Narx CIP")]
        public decimal? PriceCip { get; set; }

        [Display(Name = "Narx QQS bilan CIP")]
        public decimal? PriceVatCip { get; set; }
    }
}
