using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace tahsinERP.ViewModels
{
    public class WrhsIncomeEditViewModel
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
       // public string Description { get; set; } = "";// Optional description
        public DateTime IssueDateTime { get; set; } // Set in controller
        public int SenderWHID { get; set; } // Sender Warehouse ID, defaults to null
        public bool RecieveStatus { get; set; }
        public int? SupplierID { get; set; }
        public List<WhrsIncomePartEdit> Parts { get; set; }
        public string WHName { get; set; }
        public WrhsIncomeEditViewModel()
        {
            Parts = new List<WhrsIncomePartEdit>();
        }
    }
    public class WhrsIncomePartEdit
    {
        public int ID { get; set; }
        public string PartName { get; set; }
        public string PartNo { get; set; }
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