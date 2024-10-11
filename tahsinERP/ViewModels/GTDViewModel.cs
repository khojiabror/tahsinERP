using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    public class GTDViewModel
    {
        public int ID { get; set; }
        public string GTDNo { get; set; }
        public double Sum { get; set; }
        [DataType(DataType.Upload)]
        public HttpPostedFileBase File { get; set; }
        public bool IsDeleted { get; set; }
        public int InvoiceID { get; set; }
        public P_INVOICES P_INVOICES { get; set; }
    }
}