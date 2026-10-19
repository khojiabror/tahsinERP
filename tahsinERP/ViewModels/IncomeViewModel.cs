using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// Income ViewModel - Yangi business logic bo'yicha
    /// Income Supplierdan P_INVOICE asosida bo'ladi
    /// MES va Sales xodimlari faqat ko'radi, Create/Edit qila olmaydi
    /// </summary>
    public class IncomeCreateViewModel
    {
        public string DocNo { get; set; }
        
        [Required(ErrorMessage = "Sana kiritilishi shart")]
        [Display(Name = "Sana")]
        public DateTime IssueDateTime { get; set; } = DateTime.Now;
        
        [Required(ErrorMessage = "Qabul qiluvchi ombor tanlanishi shart")]
        [Display(Name = "Qabul qiluvchi ombor")]
        public int ReceivingWHID { get; set; }
        
        [Display(Name = "Yuboruvchi ombor")]
        public int? SenderWHID { get; set; }
        
        [Display(Name = "Invoys")]
        public int? InvoiceID { get; set; }
        
        [Required(ErrorMessage = "Valyuta tanlanishi shart")]
        [Display(Name = "Valyuta")]
        public int CurrencyID { get; set; }
        
        [Display(Name = "Izoh")]
        public string Description { get; set; }

        [Display(Name = "Joylashuv (DLOC)")]
        public int? LocationID { get; set; }
        
        [DataType(DataType.Upload)]
        [Display(Name = "Hujjat fayli")]
        public HttpPostedFileBase IncomeFile { get; set; }
        
        public List<IncomeProductViewModel> IncomeProducts { get; set; }
        
        // For display
        public string WarehouseName { get; set; }
        public string InvoiceNo { get; set; }
        public string SupplierName { get; set; }
        
        public IncomeCreateViewModel()
        {
            IncomeProducts = new List<IncomeProductViewModel>();
        }
    }

    public class IncomeEditViewModel
    {
        public int ID { get; set; }
        
        [Required(ErrorMessage = "Hujjat raqami kiritilishi shart")]
        [Display(Name = "Hujjat raqami")]
        public string DocNo { get; set; }
        
        [Required(ErrorMessage = "Sana kiritilishi shart")]
        [Display(Name = "Sana")]
        public DateTime IssueDateTime { get; set; }
        
        [Required(ErrorMessage = "Qabul qiluvchi ombor tanlanishi shart")]
        [Display(Name = "Qabul qiluvchi ombor")]
        public int ReceivingWHID { get; set; }
        
        [Display(Name = "Yuboruvchi ombor")]
        public int? SenderWHID { get; set; }
        
        [Display(Name = "Invoys")]
        public int? InvoiceID { get; set; }
        
        [Required(ErrorMessage = "Valyuta tanlanishi shart")]
        [Display(Name = "Valyuta")]
        public int CurrencyID { get; set; }
        
        [Display(Name = "Izoh")]
        public string Description { get; set; }
        
        [DataType(DataType.Upload)]
        [Display(Name = "Hujjat fayli")]
        public HttpPostedFileBase IncomeFile { get; set; }
        
        public List<IncomeProductViewModel> IncomeProducts { get; set; }
        
        // For display
        public string WarehouseName { get; set; }
        public string InvoiceNo { get; set; }
        public bool RecieveStatus { get; set; }
        
        [Display(Name = "Jami miqdor")]
        public double? Amount { get; set; }
        
        [Display(Name = "Jami summa")]
        public double? TotalPrice { get; set; }
        
        public IncomeEditViewModel()
        {
            IncomeProducts = new List<IncomeProductViewModel>();
        }
    }

    public class IncomeProductViewModel
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
        public double Amount { get; set; }
        
        [Required(ErrorMessage = "Birlik tanlanishi shart")]
        [Display(Name = "Birlik")]
        public int? UnitID { get; set; }
        
        public string UnitName { get; set; }
        
        [Display(Name = "Narx (dona)")]
        public double? PiecePrice { get; set; }

        /// <summary>QQS foizi (0–100). 0 = QQS yo‘q.</summary>
        [Display(Name = "QQS %")]
        [Range(0, 100, ErrorMessage = "QQS 0 dan 100 gacha")]
        public double VATRate { get; set; }
        
        [Display(Name = "Jami summa")]
        public double? TotalPrice { get; set; }
        
        [Display(Name = "Izoh")]
        public string Comment { get; set; }

        [Display(Name = "Joylashuv (DLOC)")]
        public int? LocationID { get; set; }
        
        // For cutting materials
        public bool RequiresCutting { get; set; }
    }

    public class IncomeDetailsViewModel
    {
        public int ID { get; set; }
        public string DocNo { get; set; }
        public DateTime IssueDateTime { get; set; }
        public int ReceivingWHID { get; set; }
        public string ReceivingWHName { get; set; }
        public int? SenderWHID { get; set; }
        public string SenderWHName { get; set; }
        public int? InvoiceID { get; set; }
        public string InvoiceNo { get; set; }
        public string SupplierName { get; set; }
        public int CurrencyID { get; set; }
        public string CurrencyName { get; set; }
        public double? Amount { get; set; }
        public double? TotalPrice { get; set; }
        public string Description { get; set; }
        public bool RecieveStatus { get; set; }
        public bool IsDeleted { get; set; }
        public List<IncomeProductViewModel> IncomeProducts { get; set; }
        public bool IsAutoLinked { get; set; } // Auto-linked from Expense
        public string LinkedExpenseDocNo { get; set; }
        
        public IncomeDetailsViewModel()
        {
            IncomeProducts = new List<IncomeProductViewModel>();
        }
    }
}
