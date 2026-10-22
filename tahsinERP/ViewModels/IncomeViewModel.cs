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
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Date")]
        public DateTime IssueDateTime { get; set; } = DateTime.Now;
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Wh_ReceiverWarehouse")]
        public int ReceivingWHID { get; set; }
        
        [Display(Name = "Wh_SenderWarehouse")]
        public int? SenderWHID { get; set; }
        
        [Display(Name = "Wh_Invoice")]
        public int? InvoiceID { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Currency")]
        public int CurrencyID { get; set; }
        
        [Display(Name = "Comment")]
        public string Description { get; set; }

        [Display(Name = "Wh_LocationDloc")]
        public int? LocationID { get; set; }
        
        [DataType(DataType.Upload)]
        [Display(Name = "Wh_Pts_DocFile")]
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
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Wh_DocNo")]
        public string DocNo { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Date")]
        public DateTime IssueDateTime { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Wh_ReceiverWarehouse")]
        public int ReceivingWHID { get; set; }
        
        [Display(Name = "Wh_SenderWarehouse")]
        public int? SenderWHID { get; set; }
        
        [Display(Name = "Wh_Invoice")]
        public int? InvoiceID { get; set; }
        
        [Required(ErrorMessage = "Val_Required")]
        [Display(Name = "Currency")]
        public int CurrencyID { get; set; }
        
        [Display(Name = "Comment")]
        public string Description { get; set; }
        
        [DataType(DataType.Upload)]
        [Display(Name = "Wh_Pts_DocFile")]
        public HttpPostedFileBase IncomeFile { get; set; }
        
        public List<IncomeProductViewModel> IncomeProducts { get; set; }
        
        // For display
        public string WarehouseName { get; set; }
        public string InvoiceNo { get; set; }
        public bool RecieveStatus { get; set; }
        
        [Display(Name = "Wh_TotalQty")]
        public decimal? Amount { get; set; }
        
        [Display(Name = "Wh_TotalAmount")]
        public decimal? TotalPrice { get; set; }
        
        public IncomeEditViewModel()
        {
            IncomeProducts = new List<IncomeProductViewModel>();
        }
    }

    public class IncomeProductViewModel
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
        public int? UnitID { get; set; }
        
        public string UnitName { get; set; }
        
        [Display(Name = "Wh_PricePerUnit")]
        public decimal? PiecePrice { get; set; }

        /// <summary>QQS foizi (0–100). 0 = QQS yo‘q.</summary>
        [Display(Name = "Wh_VatPct")]
        [Range(0, 100, ErrorMessage = "Wh_Val_VatRange")]
        public decimal VATRate { get; set; }
        
        [Display(Name = "Wh_TotalAmount")]
        public decimal? TotalPrice { get; set; }
        
        [Display(Name = "Comment")]
        public string Comment { get; set; }

        [Display(Name = "Wh_LocationDloc")]
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
        public decimal? Amount { get; set; }
        public decimal? TotalPrice { get; set; }
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
