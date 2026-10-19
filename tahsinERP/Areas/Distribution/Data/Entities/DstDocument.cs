using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tahsinERP.Areas.Distribution.Data.Entities
{
    /// <summary>Hujjat holati: Draft → Posted → Cancelled. Posted hujjat o'chirilmaydi, faqat sabab bilan bekor qilinadi.</summary>
    public enum DocumentStatus : byte
    {
        Draft = 0,
        Posted = 1,
        Cancelled = 2
    }

    /// <summary>
    /// Barcha distributsiya hujjatlarining umumiy maydonlari. Holatni faqat
    /// <see cref="Services.DocumentLifecycle"/> o'zgartiradi; o'chirish va bekor qilingan
    /// hujjatni tahrirlashni <see cref="DistributionDbContext"/> SaveChanges'da to'xtatadi.
    /// </summary>
    public abstract class DstDocument : IAuditable
    {
        [Key]
        public int ID { get; set; }

        [Required, StringLength(30)]
        public string DocNo { get; set; }

        [Column(TypeName = "date")]
        public DateTime DocDate { get; set; }

        public DocumentStatus Status { get; set; }

        public int CreatedBy { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; }

        public int? PostedBy { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? PostedAt { get; set; }

        public int? CancelledBy { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? CancelledAt { get; set; }

        [StringLength(500)]
        public string CancelReason { get; set; }

        [NotMapped]
        public string AuditKey => ID.ToString();
    }
}
