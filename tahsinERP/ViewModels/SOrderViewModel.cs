using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;


namespace tahsinERP.ViewModels
{
    public class SOrderViewModel
    {
        [Required(ErrorMessage = "Buyurtma raqami majburiy")]
        [Display(Name = "Buyurtma raqami")]
        public string OrderNo { get; set; }

        [Required(ErrorMessage = "Sana majburiy")]
        [Display(Name = "Buyurtma berilgan sanasi")]
        [DataType(DataType.Date)]
        public DateTime IssuedDate { get; set; }

        [Required(ErrorMessage = "Haridor majburiy")]
        [Display(Name = "Haridor")]
        public int CustomerID { get; set; }

        [Display(Name = "Shartnoma raqami")]
        public int ContractID { get; set; }

        [Display(Name = "Valyuta")]
        public int CurrencyID { get; set; }

        [Display(Name = "Qisqacha tavsifi")]
        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        [Display(Name = "Maxsulotlar")]
        public List<SOrderProduct> Products { get; set; }
        public SOrderViewModel()
        {
            Products = new List<SOrderProduct>();
        }
    }

    public class SOrderProduct
    {
        [Required(ErrorMessage = "Maxsulot majburiy")]
        [Display(Name = "Maxsulot")]
        public int ProductID { get; set; }

        [Required(ErrorMessage = "Narx majburiy")]
        [Display(Name = "Birlik narxi")]
        [Range(0, double.MaxValue, ErrorMessage = "Narx 0 dan katta bo'lishi kerak")]
        public float Price { get; set; }

        [Required(ErrorMessage = "Miqdor majburiy")]
        [Display(Name = "Miqdori")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Miqdor 0 dan katta bo'lishi kerak")]
        public float Amount { get; set; }

        [Display(Name = "MOQ")]
        [Range(0, double.MaxValue, ErrorMessage = "MOQ 0 dan katta yoki teng bo'lishi kerak")]
        public float MOQ { get; set; }

        [Required(ErrorMessage = "O'lchov birligi majburiy")]
        [Display(Name = "O'lchov birligi")]
        public int UnitID { get; set; }
    }
}