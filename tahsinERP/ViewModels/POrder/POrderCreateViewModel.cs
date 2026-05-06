using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels.POrder
{
    public class POrderCreateViewModel
    {
        public string OrderNo { get; set; }
        
        [Required(ErrorMessage = "Sana kiritilishi shart")]
        [Display(Name = "Sana")]
        public DateTime IssuedDate { get; set; } = DateTime.Now;
        
        [Required(ErrorMessage = "Korxona tanlanishi shart")]
        [Display(Name = "Korxona")]
        public int? CompanyID { get; set; }
        
        [Display(Name = "Ta'minotchi")]
        public int? SupplierID { get; set; }
        
        [Display(Name = "Shartnoma")]
        public int? ContractID { get; set; }
        
        [Required(ErrorMessage = "Valyuta tanlanishi shart")]
        [Display(Name = "Valyuta")]
        public int? CurrencyID { get; set; }
        
        [Display(Name = "Izoh")]
        public string Description { get; set; }
        
        [Display(Name = "Sotuv buyurtmalari")]
        public int[] SelectedSalesOrderIds { get; set; }

        [Display(Name = "Talabnoma")]
        public int? RequisitionID { get; set; }
        
        public List<POrderProductViewModel> OrderProducts { get; set; }
        
        public POrderCreateViewModel()
        {
            OrderProducts = new List<POrderProductViewModel>();
            SelectedSalesOrderIds = new int[0];
        }
    }
    
    public class POrderProductViewModel
    {
        public int? ID { get; set; }
        
        [Required(ErrorMessage = "Mahsulot tanlanishi shart")]
        [Display(Name = "Mahsulot")]
        public int ProductID { get; set; }
        
        [Display(Name = "Mahsulot kodi")]
        public string ProductPNo { get; set; }
        
        [Display(Name = "Mahsulot nomi")]
        public string ProductName { get; set; }
        
        [Required(ErrorMessage = "Narx kiritilishi shart")]
        [Display(Name = "Narx")]
        public double Price { get; set; }
        
        [Required(ErrorMessage = "Miqdor kiritilishi shart")]
        [Display(Name = "Miqdor")]
        public double Amount { get; set; }
        
        [Display(Name = "Birlik")]
        public int? UnitID { get; set; }
        
        [Display(Name = "Jami")]
        public double TotalPrice { get; set; }
        
        [Display(Name = "MOQ")]
        public double? MOQ { get; set; }
        
        // Conversion information
        [Display(Name = "Hisob tushuntirishi")]
        public string CalculationExplanation { get; set; }

        /// <summary> DB Priority (max 50) uchun qisqa qator — generatsiyadan. </summary>
        public string CalculationSummary { get; set; }
        
        [Display(Name = "Konvertatsiya qilingan")]
        public bool IsConverted { get; set; }
        
        [Display(Name = "Original miqdor")]
        public decimal? OriginalQuantity { get; set; }
        
        [Display(Name = "Original birlik")]
        public int? OriginalUnitID { get; set; }
    }
    
    public class POrderEditViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Buyurtma raqami kiritilishi shart")]
        [Display(Name = "Buyurtma raqami")]
        public string OrderNo { get; set; }
        
        [Required(ErrorMessage = "Sana kiritilishi shart")]
        [Display(Name = "Sana")]
        public DateTime IssuedDate { get; set; }
        
        [Required(ErrorMessage = "Korxona tanlanishi shart")]
        [Display(Name = "Korxona")]
        public int? CompanyID { get; set; }
        
        [Display(Name = "Ta'minotchi")]
        public int? SupplierID { get; set; }
        
        [Display(Name = "Shartnoma")]
        public int? ContractID { get; set; }
        
        [Required(ErrorMessage = "Valyuta tanlanishi shart")]
        [Display(Name = "Valyuta")]
        public int? CurrencyID { get; set; }
        
        [Display(Name = "Izoh")]
        public string Description { get; set; }
        
        public List<POrderProductViewModel> OrderProducts { get; set; }
        
        public POrderEditViewModel()
        {
            OrderProducts = new List<POrderProductViewModel>();
        }
    }
}
