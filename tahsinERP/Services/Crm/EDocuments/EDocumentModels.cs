using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace tahsinERP.Services.Crm.EDocuments
{
    /// <summary>SALES_SF_HEADERS / SALES_TTNK_HEADERS.IntegrationStatusCode qiymatlari.</summary>
    public static class EDocumentStatus
    {
        /// <summary>Faqat tizim ichida (standart; bazadagi DEFAULT ham shu).</summary>
        public const string Local = "LOCAL";
        /// <summary>Provayderga yuborildi, javob kutilmoqda.</summary>
        public const string Sent = "SENT";
        /// <summary>Kontragent/soliq tomonidan qabul qilindi (imzolandi).</summary>
        public const string Accepted = "ACCEPTED";
        public const string Rejected = "REJECTED";
        public const string Error = "ERROR";

        public static bool IsLocal(string status)
        {
            return string.IsNullOrEmpty(status) || status == Local;
        }

        /// <summary>Qayta yuborib bo'lmaydigan holatlar (faqat holatni yangilash mumkin).</summary>
        public static bool IsDelivered(string status)
        {
            return status == Sent || status == Accepted;
        }

        public static string Title(string status)
        {
            switch (status)
            {
                case Sent: return "Yuborilgan";
                case Accepted: return "Qabul qilingan";
                case Rejected: return "Rad etilgan";
                case Error: return "Xato";
                default: return "Ichki (tizimda)";
            }
        }

        public static string Badge(string status)
        {
            switch (status)
            {
                case Sent: return "info";
                case Accepted: return "success";
                case Rejected:
                case Error: return "danger";
                default: return "secondary";
            }
        }
    }

    public enum EDocumentKind
    {
        /// <summary>Счёт-фактура (SALES_SF_HEADERS).</summary>
        Invoice,
        /// <summary>TTN / ТТН (SALES_TTNK_HEADERS).</summary>
        Waybill
    }

    /// <summary>
    /// Tomon rekvizitlari. Maydon nomlari O'zbekiston e-faktura (roaming) formatiga yaqin; aniq
    /// provayder (Didox, Faktura.uz) talablari provayder klassidagi xaritalashda moslashtiriladi.
    /// </summary>
    public class EDocumentParty
    {
        public string Tin { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Account { get; set; }
        public string BankId { get; set; }
        public string VatRegCode { get; set; }
        public string Director { get; set; }
    }

    public class EInvoiceItem
    {
        public int OrdNo { get; set; }
        public string Name { get; set; }
        /// <summary>MXIK (IKPU) kodi — soliq e-faktura uchun majburiy. Hozir PRODUCTS'da yo'q.</summary>
        public string CatalogCode { get; set; }
        public string CatalogName { get; set; }
        /// <summary>Qadoq/o'lchov birligi kodi (soliq klassifikatori).</summary>
        public string PackageCode { get; set; }
        public string MeasureName { get; set; }
        public string HsCode { get; set; }
        public decimal Count { get; set; }
        public decimal Price { get; set; }
        public decimal DeliverySum { get; set; }
        public decimal VatRate { get; set; }
        public decimal VatSum { get; set; }
        public decimal DeliverySumWithVat { get; set; }
    }

    /// <summary>Счёт-фактура — provayderdan mustaqil model.</summary>
    [XmlRoot("EInvoice")]
    public class EInvoiceDocument
    {
        public Guid Uid { get; set; }
        public string Number { get; set; }
        public DateTime Date { get; set; }
        public string ContractNo { get; set; }
        public DateTime? ContractDate { get; set; }
        public string Currency { get; set; }
        public EDocumentParty Seller { get; set; }
        public EDocumentParty Buyer { get; set; }
        public List<EInvoiceItem> Items { get; set; } = new List<EInvoiceItem>();
        public decimal TotalSum { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalWithVat { get; set; }
    }

    public class EWaybillItem
    {
        public int OrdNo { get; set; }
        public string Name { get; set; }
        public string CatalogCode { get; set; }
        public string HsCode { get; set; }
        public string MeasureName { get; set; }
        public decimal Count { get; set; }
        public decimal? Price { get; set; }
        public decimal? Sum { get; set; }
        public decimal? WeightNet { get; set; }
        public decimal? WeightGross { get; set; }
        public int? PlaceCount { get; set; }
        public string PackageType { get; set; }
    }

    /// <summary>TTN — provayderdan mustaqil model.</summary>
    [XmlRoot("EWaybill")]
    public class EWaybillDocument
    {
        public Guid Uid { get; set; }
        public string Number { get; set; }
        public DateTime Date { get; set; }
        public string ContractNo { get; set; }
        public DateTime? ContractDate { get; set; }
        public string InvoiceNo { get; set; }
        public EDocumentParty Shipper { get; set; }
        public EDocumentParty Consignee { get; set; }
        public EDocumentParty Carrier { get; set; }
        public string ResponsiblePerson { get; set; }
        public string ResponsiblePersonPinfl { get; set; }
        public string DriverName { get; set; }
        public string DriverPinfl { get; set; }
        public string TruckNo { get; set; }
        public string TruckModel { get; set; }
        public string TrailerNo { get; set; }
        public string TrailerModel { get; set; }
        public string DepartureAddress { get; set; }
        public string DepartureRegion { get; set; }
        public string ArrivalAddress { get; set; }
        public string ArrivalRegion { get; set; }
        public decimal? DistanceKm { get; set; }
        public decimal? WeightGrossT { get; set; }
        public decimal? WeightNetT { get; set; }
        public string Currency { get; set; }
        public decimal DeliveryCost { get; set; }
        public decimal VatAmount { get; set; }
        public decimal? TotalDeliveryCost { get; set; }
        public List<EWaybillItem> Items { get; set; } = new List<EWaybillItem>();
    }

    /// <summary>Provayder javobi.</summary>
    public class EDocumentResult
    {
        public bool Success { get; set; }
        /// <summary>Provayderdagi hujjat identifikatori (ExternalSFKey / ExternalDocKey).</summary>
        public string ExternalId { get; set; }
        /// <summary><see cref="EDocumentStatus"/> qiymati.</summary>
        public string Status { get; set; }
        public string Message { get; set; }

        public static EDocumentResult Fail(string message)
        {
            return new EDocumentResult { Success = false, Status = EDocumentStatus.Error, Message = message };
        }
    }

    /// <summary>SF/TTNK Details sahifasidagi "Elektron hujjat" paneli.</summary>
    public class EDocumentPanelViewModel
    {
        public EDocumentKind Kind { get; set; }
        public long DocumentID { get; set; }
        public string ControllerName { get; set; }
        public string Status { get; set; }
        public string ProviderCode { get; set; }
        public string ExternalSystemCode { get; set; }
        public string ExternalKey { get; set; }
        public DateTime? LastSyncDT { get; set; }
        public string LastMessage { get; set; }
        public string ActiveProviderCode { get; set; }
        public string ActiveProviderTitle { get; set; }
        public bool ActiveProviderIsExternal { get; set; }
        public bool ActiveProviderConfigured { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public bool CanChange { get; set; }
    }
}
