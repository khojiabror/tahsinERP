using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels
{
    public class WarehouseLocationEditViewModel
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Wh_Warehouse")]
        public int WarehouseID { get; set; }

        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Wh_Loc_CodeDloc")]
        public string LocationCode { get; set; }

        [Display(Name = "Name")]
        public string LocationName { get; set; }

        [Display(Name = "Wh_Loc_MinCapacity")]
        public decimal MinCapacity { get; set; }

        [Display(Name = "Wh_Loc_MaxCapacity")]
        public decimal MaxCapacity { get; set; }

        [Display(Name = "Wh_Loc_MixLots")]
        public bool AllowMixedLots { get; set; }

        [Display(Name = "Wh_Loc_MixProducts")]
        public bool AllowMixedProducts { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }
}
