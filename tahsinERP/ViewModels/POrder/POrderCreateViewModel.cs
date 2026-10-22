using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace tahsinERP.ViewModels.POrder
{
    public class POrderCreateViewModel
    {
        public string OrderNo { get; set; }
        
        [Required(ErrorMessage = "Srm_Val_DateRequired")]
        [Display(Name = "Date")]
        public DateTime IssuedDate { get; set; } = DateTime.Now;
        
        [Required(ErrorMessage = "Srm_Val_CompanyRequired")]
        [Display(Name = "Menu_Company")]
        public int? CompanyID { get; set; }
        
        [Display(Name = "Srm_Supplier")]
        public int? SupplierID { get; set; }
        
        [Display(Name = "Srm_Contract")]
        public int? ContractID { get; set; }
        
        [Required(ErrorMessage = "Srm_Val_CurrencyRequired")]
        [Display(Name = "Currency")]
        public int? CurrencyID { get; set; }
        
        [Display(Name = "Comment")]
        public string Description { get; set; }

        /// <summary> QQS stavkasi (%). Saqlashda Amount = net summa + QQS qilib yoziladi. </summary>
        [Display(Name = "Srm_VatPercentShort")]
        public decimal VatRate { get; set; }

        [Display(Name = "Srm_SalesOrders")]
        public int[] SelectedSalesOrderIds { get; set; }

        [Display(Name = "Srm_RequisitionDoc")]
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
        
        [Required(ErrorMessage = "Srm_Val_ProductRequired")]
        [Display(Name = "Srm_Product")]
        public int ProductID { get; set; }
        
        [Display(Name = "Srm_ProductCode")]
        public string ProductPNo { get; set; }
        
        [Display(Name = "Srm_ProductName")]
        public string ProductName { get; set; }
        
        [Required(ErrorMessage = "Srm_Val_PriceRequired")]
        [Display(Name = "Price")]
        public decimal Price { get; set; }
        
        [Required(ErrorMessage = "Srm_Val_QtyRequired")]
        [Display(Name = "Quantity")]
        public decimal Amount { get; set; }
        
        [Display(Name = "Unit")]
        public int? UnitID { get; set; }
        
        [Display(Name = "Total")]
        public decimal TotalPrice { get; set; }
        
        [Display(Name = "Srm_Moq")]
        public decimal? MOQ { get; set; }
        
        // Conversion information
        [Display(Name = "Srm_CalcExplanation")]
        public string CalculationExplanation { get; set; }

        /// <summary> DB Priority (max 50) uchun qisqa qator — generatsiyadan. </summary>
        public string CalculationSummary { get; set; }
        
    }
    
    public class POrderEditViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Srm_Val_OrderNoRequired")]
        [Display(Name = "Srm_OrderNo")]
        public string OrderNo { get; set; }
        
        [Required(ErrorMessage = "Srm_Val_DateRequired")]
        [Display(Name = "Date")]
        public DateTime IssuedDate { get; set; }
        
        [Required(ErrorMessage = "Srm_Val_CompanyRequired")]
        [Display(Name = "Menu_Company")]
        public int? CompanyID { get; set; }
        
        [Display(Name = "Srm_Supplier")]
        public int? SupplierID { get; set; }
        
        [Display(Name = "Srm_Contract")]
        public int? ContractID { get; set; }
        
        [Required(ErrorMessage = "Srm_Val_CurrencyRequired")]
        [Display(Name = "Currency")]
        public int? CurrencyID { get; set; }
        
        [Display(Name = "Comment")]
        public string Description { get; set; }

        /// <summary> QQS stavkasi (%). Saqlashda Amount = net summa + QQS qilib yoziladi. </summary>
        [Display(Name = "Srm_VatPercentShort")]
        public decimal VatRate { get; set; }

        public List<POrderProductViewModel> OrderProducts { get; set; }

        public POrderEditViewModel()
        {
            OrderProducts = new List<POrderProductViewModel>();
        }
    }
}
