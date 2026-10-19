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
        
        [Required(ErrorMessage = "Sana kiritilishi shart")]
        [Display(Name = "Sana")]
        public DateTime IssueDateTime { get; set; } = DateTime.Now;
        
        [Required(ErrorMessage = "Yuboruvchi ombor tanlanishi shart")]
        [Display(Name = "Yuboruvchi ombor")]
        public int SenderWHID { get; set; }
        
        [Display(Name = "Qabul qiluvchi ombor")]
        public int? ReceiverWhID { get; set; }
        
        [Required(ErrorMessage = "Valyuta tanlanishi shart")]
        [Display(Name = "Valyuta")]
        public int CurrencyID { get; set; }
        
        [Display(Name = "Izoh")]
        public string Description { get; set; }
        
        [DataType(DataType.Upload)]
        [Display(Name = "Hujjat fayli")]
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
        
        [Required(ErrorMessage = "Hujjat raqami kiritilishi shart")]
        [Display(Name = "Hujjat raqami")]
        public string DocNo { get; set; }
        
        [Required(ErrorMessage = "Sana kiritilishi shart")]
        [Display(Name = "Sana")]
        public DateTime IssueDateTime { get; set; }
        
        [Required(ErrorMessage = "Yuboruvchi ombor tanlanishi shart")]
        [Display(Name = "Yuboruvchi ombor")]
        public int SenderWHID { get; set; }
        
        [Display(Name = "Qabul qiluvchi ombor")]
        public int? ReceiverWhID { get; set; }
        
        [Required(ErrorMessage = "Valyuta tanlanishi shart")]
        [Display(Name = "Valyuta")]
        public int CurrencyID { get; set; }
        
        [Display(Name = "Izoh")]
        public string Description { get; set; }
        
        [DataType(DataType.Upload)]
        [Display(Name = "Hujjat fayli")]
        public HttpPostedFileBase ExpenseFile { get; set; }
        
        public List<ExpenseProductViewModel> ExpenseProducts { get; set; }
        
        // For display
        public string SenderWHName { get; set; }
        public string ReceiverWHName { get; set; }
        public bool SendStatus { get; set; }
        
        [Display(Name = "Jami miqdor")]
        public decimal? Amount { get; set; }
        
        [Display(Name = "Jami summa")]
        public decimal? TotalPrice { get; set; }
        
        public ExpenseEditViewModel()
        {
            ExpenseProducts = new List<ExpenseProductViewModel>();
        }
    }

    public class ExpenseProductViewModel
    {
        public int? ID { get; set; }
        
        [Required(ErrorMessage = "Mahsulot tanlanishi shart")]
        [Display(Name = "Mahsulot")]
        public int ProductID { get; set; }
        
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        
        [Required(ErrorMessage = "Miqdor kiritilishi shart")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Miqdor 0 dan katta bo'lishi kerak")]
        [Display(Name = "Miqdor")]
        public decimal Amount { get; set; }
        
        [Required(ErrorMessage = "Birlik tanlanishi shart")]
        [Display(Name = "Birlik")]
        public int UnitID { get; set; }
        
        public string UnitName { get; set; }
        
        [Display(Name = "Narx (dona)")]
        public decimal PiecePrice { get; set; }
        
        [Display(Name = "Jami summa")]
        public decimal TotalPrice { get; set; }
        
        [Display(Name = "Izoh")]
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
