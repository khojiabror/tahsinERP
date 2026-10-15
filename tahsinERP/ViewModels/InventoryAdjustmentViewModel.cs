using System.ComponentModel.DataAnnotations;
using System.Web;

namespace tahsinERP.ViewModels
{
    public class InventoryAdjustmentViewModel
    {
        [Required(ErrorMessage = "Ombor tanlanishi shart")]
        [Display(Name = "Ombor")]
        public int WarehouseId { get; set; }

        [Display(Name = "Excel fayl")]
        public HttpPostedFileBase ExcelFile { get; set; }

        [Display(Name = "Tasdiqlash")]
        public bool Confirmed { get; set; }
    }
}
