using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    public class PInvoiceViewModel
    {
        public int ID { get; set; }
        [Required]
        public string InvoiceNo { get; set; }
        [Required]
        public int OrderID { get; set; }
        [Required(ErrorMessage = "Siz tanlagan Taminotchi va Buyurtma ta'minotchisi bir xil bo'lishi shart!")]
        public int SupplierID { get; set; }
        public DateTime InvoiceDate { get; set; }
        [Required]
        public int CurrencyID { get; set; }
        public int CompanyID { get; set; }
        [DataType(DataType.Upload)]
        public HttpPostedFileBase File { get; set; }
        public bool IsDeleted { get; set; }
        public List<PInvoicePartViewModel> Parts { get; set; }
        public PInvoiceViewModel()
        {
            Parts = new List<PInvoicePartViewModel>();
        }
    }
    public class PInvoicePartViewModel
    {
        public int ID { get; set; }
        [Required]
        public int InvoiceID { get; set; }
        public PART Part { get; set; }
        [Required]
        public int PartID { get; set; }
        [Required]
        public double Quantity { get; set; }
        [Required]
        public int UnitID { get; set; }

        public double Price { get; set; }

        public double TotalPrice { get; set; }
    }
}