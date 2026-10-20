using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tahsinERP.Areas.Distribution.Data.Entities
{
    public static class InventoryKinds
    {
        /// <summary>Boshlang'ich: o'tkazilganda qoldiq sanalganga tenglanadi.</summary>
        public const string Opening = "OPENING";
        /// <summary>Nazorat: taqqoslash (pilot qabul mezoni); farqni qoldiqqa yozish ixtiyoriy.</summary>
        public const string Control = "CONTROL";

        public static readonly string[] All = { Opening, Control };
    }

    /// <summary>dst.INVENTORY_COUNTS — ombordagi sanoq.</summary>
    [Table("INVENTORY_COUNTS")]
    public class InventoryCount : DstDocument
    {
        public int WarehouseID { get; set; }
        [Required, StringLength(10)] public string Kind { get; set; }
        public bool Applied { get; set; }
        public int? AdjustmentID { get; set; }
        [StringLength(300)] public string Note { get; set; }

        public virtual ICollection<InventoryCountRow> Lines { get; set; } = new List<InventoryCountRow>();
    }

    [Table("INVENTORY_COUNT_LINES")]
    public class InventoryCountRow : IAuditable
    {
        [Key] public int ID { get; set; }
        public int CountID { get; set; }
        public int ProductID { get; set; }
        public decimal? CountedQty { get; set; }
        public decimal? SystemQty { get; set; }
        public decimal? UnitCost { get; set; }
        [StringLength(200)] public string Note { get; set; }

        [ForeignKey(nameof(CountID))] public virtual InventoryCount Count { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    public enum IssueStatus : byte
    {
        New = 0,
        InProgress = 1,
        Testing = 2,
        Closed = 3
    }

    /// <summary>dst.ISSUES — pilot xatolar jurnali. Severity: 1 = K1 (ishni to'xtatadi / pul yoki qoldiq noto'g'ri), 2 = K2, 3 = K3.</summary>
    [Table("ISSUES")]
    public class Issue : IAuditable
    {
        [Key] public int ID { get; set; }
        [Required, StringLength(20)] public string IssueNo { get; set; }
        [Column(TypeName = "datetime2")] public DateTime CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        [Required, StringLength(10)] public string Source { get; set; } = AuditActor.Web;
        [StringLength(150)] public string Place { get; set; }
        [StringLength(100)] public string DocRef { get; set; }
        [Required, StringLength(2000)] public string Description { get; set; }
        [StringLength(1000)] public string Expected { get; set; }
        public byte? Severity { get; set; }
        public IssueStatus Status { get; set; }
        [StringLength(200)] public string PhotoKey { get; set; }
        [StringLength(1000)] public string Resolution { get; set; }
        [Column(TypeName = "datetime2")] public DateTime? UpdatedAt { get; set; }
        public int? ClosedBy { get; set; }
        [Column(TypeName = "datetime2")] public DateTime? ClosedAt { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }
}
