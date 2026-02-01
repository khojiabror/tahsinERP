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

        [Required(ErrorMessage = "Waybill raqami majburiy")]
        [Display(Name = "Waybill raqami")]
        public string WaybillNo { get; set; }

        [Required(ErrorMessage = "Sana majburiy")]
        [Display(Name = "Sana")]
        [DataType(DataType.Date)]
        public DateTime? IssueDate { get; set; }

        [Display(Name = "Yo'nalish")]
        public byte? Direction { get; set; } // 0=Inbound, 1=Outbound, 2=Internal

        [Display(Name = "Forwarder")]
        public int? ForwarderID { get; set; }
        public List<SelectListItem> Forwarders { get; set; }

        [Required(ErrorMessage = "Shartnoma majburiy")]
        [Display(Name = "Shartnoma")]
        public int? ContractID { get; set; }
        public List<SelectListItem> Contracts { get; set; }

        [Required(ErrorMessage = "Transport turi majburiy")]
        [Display(Name = "Transport turi")]
        public int? TransportTypeID { get; set; }
        public List<SelectListItem> TransportTypes { get; set; }

        [Display(Name = "Qayerdan")]
        public string FromLocation { get; set; }

        [Display(Name = "Qayerga")]
        public string ToLocation { get; set; }

        [Display(Name = "Masofa (km)")]
        public double? DistanceKm { get; set; }

        // Cargo information
        [Display(Name = "Brut vazn (ton)")]
        public double? GrWeight { get; set; }

        [Display(Name = "Net vazn (ton)")]
        public double? NetWeight { get; set; }

        [Display(Name = "Hajm (CBM)")]
        public double? CBM { get; set; }

        [Display(Name = "Palletlar soni")]
        public int? PalletsCount { get; set; }

        // Cost information
        [Display(Name = "Tarif asosi")]
        public byte? FreightBasis { get; set; } // 0=PerTruck, 1=PerTon, 2=PerKm, 3=Flat

        [Display(Name = "Narx")]
        public double? Rate { get; set; }

        [Display(Name = "Jami summa")]
        public double? Amount { get; set; }

        [Display(Name = "Valyuta")]
        public int? CurrencyID { get; set; }
        public List<SelectListItem> Currencies { get; set; }

        [Display(Name = "To'lovchi")]
        public byte? PayerType { get; set; } // 0=OurCompany, 1=Customer, 2=Supplier, 3=Shared

        [Display(Name = "To'lovchi ID")]
        public int? PayerID { get; set; }

        // Links to business documents
        [Display(Name = "Xarid invoysi")]
        public int? InvoiceID { get; set; }
        public List<SelectListItem> PurchaseInvoices { get; set; }

        [Display(Name = "Sotuv invoysi")]
        public long? SalesInvoiceID { get; set; }
        public List<SelectListItem> SalesInvoices { get; set; }

        [Display(Name = "Sotuv buyurtmasi")]
        public int? SalesOrderID { get; set; }
        public List<SelectListItem> SalesOrders { get; set; }

        [Display(Name = "Xarid buyurtmasi")]
        public int? PurchaseOrderID { get; set; }
        public List<SelectListItem> PurchaseOrders { get; set; }

        [Display(Name = "TTNK")]
        public long? TTNKID { get; set; }
        public List<SelectListItem> TTNKs { get; set; }

        // Vehicle and driver
        [Display(Name = "Yuk mashinasi raqami")]
        public string TruckNo { get; set; }

        [Display(Name = "Traler raqami")]
        public string TrailerNo { get; set; }

        [Display(Name = "Yuk mashinasi modeli")]
        public string TruckModel { get; set; }

        [Display(Name = "Traler modeli")]
        public string TrailerModel { get; set; }

        [Display(Name = "Haydovchi ismi")]
        public string DriverName { get; set; }

        [Display(Name = "Haydovchi PINFL")]
        public string DriverPINFL { get; set; }

        // Dates for OTIF tracking
        [Display(Name = "Rejalashtirilgan yetkazish sanasi")]
        [DataType(DataType.Date)]
        public DateTime? PlannedDeliveryDate { get; set; }

        [Display(Name = "Haqiqiy yetkazish sanasi")]
        [DataType(DataType.Date)]
        public DateTime? ActualDeliveryDate { get; set; }

        [Display(Name = "Izoh")]
        public string Description { get; set; }

        [Display(Name = "Qo'lda tuzatish sababi")]
        public string ManualCorrectionReason { get; set; }

        // Helper properties for UI
        public bool IsCalculated { get; set; }
        public string CalculatedAmountDisplay { get; set; }
    }
}
