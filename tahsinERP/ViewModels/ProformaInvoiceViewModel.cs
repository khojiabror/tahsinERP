using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    public class ProformaInvoiceViewModel
    {
        public int ID { get; set; }
        public string PrInvoiceNo { get; set; }
        public int InvoiceID { get; set; }
        public int SupplierID { get; set; }
        public double Amount { get; set; }
        public System.DateTime InvoiceDate { get; set; }
        public int CurrencyID { get; set; }
        [DataType(DataType.Upload)]
        public HttpPostedFileBase File { get; set; } // File upload
        public virtual COMPANy COMPANy { get; set; }
        public virtual P_INVOICES P_INVOICES { get; set; }
        public virtual SUPPLIER SUPPLIER { get; set; }
    }
}