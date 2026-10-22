using System.ComponentModel.DataAnnotations;
using System.Web;

namespace tahsinERP.ViewModels
{
    public class InventoryAdjustmentViewModel
    {
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Wh_Warehouse")]
        public int WarehouseId { get; set; }

        [Display(Name = "Wh_ExcelFile")]
        public HttpPostedFileBase ExcelFile { get; set; }

        [Display(Name = "Confirm")]
        public bool Confirmed { get; set; }
    }
}
