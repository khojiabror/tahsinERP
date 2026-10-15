using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace tahsinERP.ViewModels.PInvoice
{
    public class PInvoiceCreateViewModel
    {
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
        public List<PInvoicePartViewModel> Parts { get; set; }
        public PInvoiceCreateViewModel()
        {
            Parts = new List<PInvoicePartViewModel>();
        }
    }
    public class PInvoicePartViewModel
    {
        [Required]
        public int PartID { get; set; }
        [Required]
        public double Quantity { get; set; }
        [Required]
        public int UnitID { get; set; }
        [Required]
        public double Price { get; set; }
        public double TotalPrice => Price * Quantity;
        
        // For display purposes
        public string ProductPNo { get; set; }
        public string ProductName { get; set; }
        public bool RequiresCutting { get; set; }
        public decimal? QuantityCBM { get; set; } // Calculated CBM for cutting products
    }
}