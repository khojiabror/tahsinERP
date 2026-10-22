using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.ViewModels
{
    /// <summary>
    /// ViewModel for creating/editing waybills
    /// </summary>
    public class WaybillCreateViewModel
    {
        public int? ID { get; set; }

        [Display(Name = "Log_WaybillNo")]
        public string WaybillNo { get; set; }

        [Required(ErrorMessage = "Log_Val_DateRequired")]
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime? IssueDate { get; set; }

        [Display(Name = "Log_Direction")]
        public byte? Direction { get; set; } // 0=Inbound, 1=Outbound, 2=Internal

        [Display(Name = "Log_Forwarder")]
        public int? ForwarderID { get; set; }
        public List<SelectListItem> Forwarders { get; set; }

        [Display(Name = "Log_Contract")]
        public int? ContractID { get; set; }
        public List<SelectListItem> Contracts { get; set; }

        [Display(Name = "Log_TransportType")]
        public int? TransportTypeID { get; set; }
        public List<SelectListItem> TransportTypes { get; set; }

        [Display(Name = "Log_Tariff")]
        public int? TariffID { get; set; }
        public List<SelectListItem> Tariffs { get; set; }

        /// <summary>Edit ochilgandagi tarif: o'zgarmasa stavka qayta yozilmaydi (LOG-B05).</summary>
        public int? OriginalTariffID { get; set; }

        [Display(Name = "Log_From")]
        public string FromLocation { get; set; }

        [Display(Name = "Log_To")]
        public string ToLocation { get; set; }

        [Display(Name = "Log_DistanceKm")]
        public decimal? DistanceKm { get; set; }

        // Cargo information
        [Display(Name = "Log_GrossWeightTon")]
        public decimal? GrWeight { get; set; }

        [Display(Name = "Log_NetWeightTon")]
        public decimal? NetWeight { get; set; }

        [Display(Name = "Log_VolumeCbm")]
        public decimal? CBM { get; set; }

        [Display(Name = "Log_PalletsCount")]
        public int? PalletsCount { get; set; }

        // Cost information
        [Display(Name = "Log_TariffBasis")]
        public byte? FreightBasis { get; set; } // 0=PerTruck, 1=PerTon, 2=PerKm, 3=Flat

        [Display(Name = "Price")]
        public decimal? Rate { get; set; }

        [Display(Name = "Log_TotalAmount")]
        public decimal? Amount { get; set; }

        [Display(Name = "Currency")]
        public int? CurrencyID { get; set; }
        public List<SelectListItem> Currencies { get; set; }

        [Display(Name = "Log_Payer")]
        public byte? PayerType { get; set; } // 0=OurCompany, 1=Customer, 2=Supplier, 3=Shared

        [Display(Name = "Log_PayerId")]
        public int? PayerID { get; set; }

        // Links to business documents
        [Display(Name = "Log_PurchaseInvoice")]
        public int? InvoiceID { get; set; }
        public List<SelectListItem> PurchaseInvoices { get; set; }

        [Display(Name = "Log_SalesInvoice")]
        public long? SalesInvoiceID { get; set; }
        public List<SelectListItem> SalesInvoices { get; set; }

        [Display(Name = "Log_SalesOrder")]
        public int? SalesOrderID { get; set; }
        public List<SelectListItem> SalesOrders { get; set; }

        [Display(Name = "Log_PurchaseOrder")]
        public int? PurchaseOrderID { get; set; }
        public List<SelectListItem> PurchaseOrders { get; set; }

        [Display(Name = "Log_Ttnk")]
        public long? TTNKID { get; set; }
        public List<SelectListItem> TTNKs { get; set; }

        // Vehicle and driver
        [Display(Name = "Log_TruckNumber")]
        public string TruckNo { get; set; }

        [Display(Name = "Log_TrailerNo")]
        public string TrailerNo { get; set; }

        [Display(Name = "Log_TruckModelFull")]
        public string TruckModel { get; set; }

        [Display(Name = "Log_TrailerModel")]
        public string TrailerModel { get; set; }

        [Display(Name = "Log_DriverName")]
        public string DriverName { get; set; }

        [Display(Name = "Log_DriverPinfl")]
        public string DriverPINFL { get; set; }

        // Dates for OTIF tracking
        [Display(Name = "Log_PlannedDeliveryDate")]
        [DataType(DataType.Date)]
        public DateTime? PlannedDeliveryDate { get; set; }

        [Display(Name = "Log_ActualDeliveryDate")]
        [DataType(DataType.Date)]
        public DateTime? ActualDeliveryDate { get; set; }

        [Display(Name = "Comment")]
        public string Description { get; set; }

        [Display(Name = "Log_ManualCorrectionReason")]
        public string ManualCorrectionReason { get; set; }

        // Helper properties for UI
        public bool IsCalculated { get; set; }
        public string CalculatedAmountDisplay { get; set; }
    }
}
