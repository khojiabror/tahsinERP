using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tahsinERP.Areas.Distribution.Data.Entities
{
    /// <summary>
    /// dst.D_ORDERS. <see cref="DstDocument.Status"/>: yangi buyurtma — Draft (o'chirish mumkin), tasdiqlangandan
    /// keyin — Posted, bekor — Cancelled. Batafsil holat — <see cref="OrderStatus"/> (Services.Orders.OrderStatus).
    /// </summary>
    [Table("D_ORDERS")]
    public class DstOrder : DstDocument
    {
        public byte OrderStatus { get; set; }
        public int CustomerID { get; set; }
        public int? AgentUserID { get; set; }
        public int? WarehouseID { get; set; }
        public int? PriceTypeID { get; set; }
        [Column(TypeName = "date")] public DateTime? DeliveryDate { get; set; }
        public bool IsPickup { get; set; }
        [StringLength(500)] public string Comment { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountPct { get; set; }
        public decimal DiscountSum { get; set; }
        public decimal Total { get; set; }
        public decimal ShippedTotal { get; set; }
        public decimal WeightKg { get; set; }
        public decimal VolumeM3 { get; set; }
        [Required, StringLength(10)] public string Source { get; set; } = AuditActor.Web;
        public Guid? ClientRequestId { get; set; }
        public int? CreditOverrideBy { get; set; }
        [Column(TypeName = "datetime2")] public DateTime? CreditOverrideAt { get; set; }
        [StringLength(300)] public string CreditOverrideNote { get; set; }
        public int? ConfirmedBy { get; set; }
        [Column(TypeName = "datetime2")] public DateTime? ConfirmedAt { get; set; }
        [Column(TypeName = "datetime2")] public DateTime? DeliveredAt { get; set; }
        [StringLength(200)] public string SignatureKey { get; set; }

        [Timestamp] public byte[] RowVer { get; set; }

        public virtual ICollection<DstOrderLine> Lines { get; set; } = new List<DstOrderLine>();
    }

    [Table("D_ORDER_LINES")]
    public class DstOrderLine : IAuditable
    {
        [Key] public int ID { get; set; }
        public int OrderID { get; set; }
        public int LineNo { get; set; }
        public int ProductID { get; set; }
        public int? ProductUnitID { get; set; }
        public int UnitID { get; set; }
        public decimal Factor { get; set; }
        public decimal Qty { get; set; }
        public decimal QtyBase { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? MinUnitPrice { get; set; }
        public byte PriceSource { get; set; }
        public decimal DiscountPct { get; set; }
        public decimal DiscountSum { get; set; }
        public decimal Amount { get; set; }
        public decimal QtyShippedBase { get; set; }

        [ForeignKey(nameof(OrderID))] public virtual DstOrder Order { get; set; }

        [NotMapped] public decimal QtyRemainingBase => QtyBase - QtyShippedBase;
        [NotMapped] public string AuditKey => ID.ToString();
    }

    [Table("D_ORDER_STATUS_LOG")]
    public class DstOrderStatusLog
    {
        [Key] public long ID { get; set; }
        public int OrderID { get; set; }
        public byte? FromStatus { get; set; }
        public byte ToStatus { get; set; }
        public int? UserID { get; set; }
        public DateTime At { get; set; }
        [StringLength(500)] public string Note { get; set; }
    }

    [Table("STOCK_RESERVATIONS")]
    public class StockReservation : IAuditable
    {
        [Key] public int ID { get; set; }
        public int WarehouseID { get; set; }
        public int ProductID { get; set; }
        public int OrderLineID { get; set; }
        public decimal QtyBase { get; set; }
        public DateTime CreatedAt { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    [Table("D_SHIPMENTS")]
    public class DstShipment : DstDocument
    {
        public int OrderID { get; set; }
        public int WarehouseID { get; set; }
        public int? ExpenseID { get; set; }
        public decimal Total { get; set; }
        public decimal? CostTotal { get; set; }
        public bool IsPickup { get; set; }

        public virtual ICollection<DstShipmentLine> Lines { get; set; } = new List<DstShipmentLine>();
    }

    [Table("D_SHIPMENT_LINES")]
    public class DstShipmentLine : IAuditable
    {
        [Key] public int ID { get; set; }
        public int ShipmentID { get; set; }
        public int OrderLineID { get; set; }
        public int ProductID { get; set; }
        public decimal QtyBase { get; set; }
        public decimal Amount { get; set; }
        public decimal? CostAmount { get; set; }

        [ForeignKey(nameof(ShipmentID))] public virtual DstShipment Shipment { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    /// <summary>dst.CUSTOMER_LEDGER.DocType qiymatlari.</summary>
    public static class LedgerDocTypes
    {
        public const string Shipment = "SHIPMENT";
        public const string Payment = "PAYMENT";
        public const string Return = "RETURN";
        public const string Adjustment = "ADJUSTMENT";
        public const string Opening = "OPENING";

        /// <summary>Teskari yozuv turi: asl tur + "_REV", DocID — o'sha hujjat (masalan SHIPMENT_REV).</summary>
        public const string ReversalSuffix = "_REV";
        public static string ReversalOf(string docType) => docType + ReversalSuffix;
    }

    /// <summary>dst.CUSTOMER_LEDGER — mijoz qarzining yagona manbai (yozuvlar o'zgartirilmaydi, teskari yozuv bilan tuzatiladi).</summary>
    [Table("CUSTOMER_LEDGER")]
    public class CustomerLedgerEntry : IAuditable
    {
        [Key] public long ID { get; set; }
        public int CustomerID { get; set; }
        public DateTime EntryDate { get; set; }
        [Required, StringLength(20)] public string DocType { get; set; }
        public int? DocID { get; set; }
        public int? OrderID { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        [Column(TypeName = "date")] public DateTime? DueDate { get; set; }
        public int? AgentUserID { get; set; }
        [StringLength(300)] public string Note { get; set; }
        public int? CreatedBy { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    [Table("SETTINGS")]
    public class DstSetting
    {
        public const string MinOrderAmount = "MinOrderAmount";
        public const string VisitRadiusM = "VisitRadiusM";
        public const string AgentCashAccountId = "AgentCashAccountId";
        public const string BotUsername = "BotUsername";
        public const string LargeOrderAmount = "LargeOrderAmount";

        [Key, StringLength(50), DatabaseGenerated(DatabaseGeneratedOption.None)] public string Key { get; set; }
        [StringLength(400)] public string Value { get; set; }
    }
}
