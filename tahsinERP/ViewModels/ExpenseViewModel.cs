using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// Expense ViewModel - Yangi business logic bo'yicha
    /// SRM xodimlari Expense Create qilganda MES/Sales uchun avtomatik Income yaratiladi
    /// </summary>
    public class ExpenseCreateViewModel
    {
        public string DocNo { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Date")]
        public DateTime IssueDateTime { get; set; } = DateTime.Now;
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Wh_SenderWarehouse")]
        public int SenderWHID { get; set; }
        
        [Display(Name = "Wh_ReceiverWarehouse")]
        public int? ReceiverWhID { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Currency")]
        public int CurrencyID { get; set; }
        
        [Display(Name = "Comment")]
        public string Description { get; set; }
        
        [DataType(DataType.Upload)]
        [Display(Name = "Wh_Pts_DocFile")]
        public HttpPostedFileBase ExpenseFile { get; set; }
        
        public List<ExpenseProductViewModel> ExpenseProducts { get; set; }
        
        // For display
        public string SenderWHName { get; set; }
        public string ReceiverWHName { get; set; }
        
        public ExpenseCreateViewModel()
        {
            ExpenseProducts = new List<ExpenseProductViewModel>();
        }
    }

    public class ExpenseEditViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Wh_DocNo")]
        public string DocNo { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Date")]
        public DateTime IssueDateTime { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Wh_SenderWarehouse")]
        public int SenderWHID { get; set; }
        
        [Display(Name = "Wh_ReceiverWarehouse")]
        public int? ReceiverWhID { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Currency")]
        public int CurrencyID { get; set; }
        
        [Display(Name = "Comment")]
        public string Description { get; set; }
        
        [DataType(DataType.Upload)]
        [Display(Name = "Wh_Pts_DocFile")]
        public HttpPostedFileBase ExpenseFile { get; set; }
        
        public List<ExpenseProductViewModel> ExpenseProducts { get; set; }
        
        // For display
        public string SenderWHName { get; set; }
        public string ReceiverWHName { get; set; }
        public bool SendStatus { get; set; }
        
        [Display(Name = "Wh_TotalQty")]
        public decimal? Amount { get; set; }
        
        [Display(Name = "Wh_TotalAmount")]
        public decimal? TotalPrice { get; set; }
        
        public ExpenseEditViewModel()
        {
            ExpenseProducts = new List<ExpenseProductViewModel>();
        }
    }

    public class ExpenseProductViewModel
    {
        public int? ID { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Wh_Product")]
        public int ProductID { get; set; }
        
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Wh_Val_QtyPositive")]
        [Display(Name = "Quantity")]
        public decimal Amount { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Unit")]
        public int UnitID { get; set; }
        
        public string UnitName { get; set; }
        
        [Display(Name = "Wh_PricePerUnit")]
        public decimal PiecePrice { get; set; }
        
        [Display(Name = "Wh_TotalAmount")]
        public decimal TotalPrice { get; set; }
        
        [Display(Name = "Comment")]
        public string Comment { get; set; }
        
        // FIFO/Lot tracking: qaysi Income (kirim) dan chiqim - dropdown orqali tanlanadi
        public int? IncomeID { get; set; }
        
        // For stock availability check
        public decimal AvailableStock { get; set; }
    }

    public class ExpenseDetailsViewModel
    {
        public int ID { get; set; }
        public string DocNo { get; set; }
        public DateTime IssueDateTime { get; set; }
        public int SenderWHID { get; set; }
        public string SenderWHName { get; set; }
        public int? ReceiverWhID { get; set; }
        public string ReceiverWHName { get; set; }
        public int CurrencyID { get; set; }
        public string CurrencyName { get; set; }
        public decimal? Amount { get; set; }
        public decimal? TotalPrice { get; set; }
        public string Description { get; set; }
        public bool SendStatus { get; set; }
        public bool IsDeleted { get; set; }
        public List<ExpenseProductViewModel> ExpenseProducts { get; set; }
        public bool HasLinkedIncome { get; set; } // Auto-created Income exists
        public string LinkedIncomeDocNo { get; set; }
        
        public ExpenseDetailsViewModel()
        {
            ExpenseProducts = new List<ExpenseProductViewModel>();
        }
    }
}
