using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    // ViewModel for Warehouse Income
    public class WrhsIncomeViewModel
    {
        public int ID { get; set; }
        [Required(ErrorMessage = "Document number is required.")]
        public string DocNo { get; set; }
        public int WHID { get; set; } // Warehouse ID, defaults to null
        [Required(ErrorMessage = "Invoice ID is required.")]
        public int InvoiceID { get; set; }
        [DataType(DataType.Upload)]
        public HttpPostedFileBase File { get; set; } // File upload
        [Required(ErrorMessage = "Amount is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Amount must be a positive number.")]
        public double Amount { get; set; }
        [Required(ErrorMessage = "Currency is required.")]
        public int CurrencyID { get; set; }
        [DataType(DataType.Text)]
        public string Description { get; set; } // Optional description
        public DateTime IssueDateTime { get; set; } // Set in controller
        public int SenderWHID { get; set; } // Sender Warehouse ID, defaults to null
        public bool RecieveStatus { get; set; } 
        public int? SupplierID { get; set; }
        public List<WhrsIncomePart> Parts { get; set; }
        public string WHName { get; set; }
        public WrhsIncomeViewModel()
        {
            Parts = new List<WhrsIncomePart>();
        }
    }

    // ViewModel for parts in Warehouse Income
    public class WhrsIncomePart
    {
        public int ID { get; set; }
        public PRODUCT Part { get; set; }
        public int PartID { get; set; }
        [Required(ErrorMessage = "Unit ID is required.")]
        public int UnitID { get; set; }
        [Required(ErrorMessage = "Amount is required.")]
        public double Amount { get; set; }
        [Required]
        public double PiecePrice { get; set; }
        public string Comment { get; set; } // Optional comment
    }
}
