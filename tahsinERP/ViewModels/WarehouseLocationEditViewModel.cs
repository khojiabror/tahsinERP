using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels
{
    public class WarehouseLocationEditViewModel
    {
        public int ID { get; set; }

        [Required]
        [Display(Name = "Ombor")]
        public int WarehouseID { get; set; }

        [Required]
        [Display(Name = "Joy kodi (DLOC)")]
        public string LocationCode { get; set; }

        [Display(Name = "Nomi")]
        public string LocationName { get; set; }

        [Display(Name = "Min sig‘im")]
        public decimal MinCapacity { get; set; }

        [Display(Name = "Max sig‘im")]
        public decimal MaxCapacity { get; set; }

        [Display(Name = "Lot aralashtirish")]
        public bool AllowMixedLots { get; set; }

        [Display(Name = "Mahsulot aralashtirish")]
        public bool AllowMixedProducts { get; set; }

        [Display(Name = "Faol")]
        public bool IsActive { get; set; } = true;
    }
}
