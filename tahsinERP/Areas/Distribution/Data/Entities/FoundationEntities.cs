using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tahsinERP.Areas.Distribution.Data.Entities
{
    /// <summary>
    /// Jurnalga yoziladigan entity: o'zgarishlari <see cref="AuditTrailEntry"/> ga tushadi.
    /// <see cref="AuditKey"/> — qatorning barqaror kaliti (odatda ID).
    /// </summary>
    public interface IAuditable
    {
        string AuditKey { get; }
    }

    /// <summary>dst.AUDIT_TRAIL — kim, qachon, nimani (eski → yangi) o'zgartirdi.</summary>
    [Table("AUDIT_TRAIL")]
    public class AuditTrailEntry
    {
        public const string Insert = "I";
        public const string Update = "U";
        public const string Delete = "D";

        [Key]
        public long ID { get; set; }

        public Guid CorrelationId { get; set; }

        [Required, StringLength(100)]
        public string EntityName { get; set; }

        [Required, StringLength(100)]
        public string EntityKey { get; set; }

        [Required, StringLength(1), Column(TypeName = "char")]
        public string Action { get; set; }

        [StringLength(100)]
        public string FieldName { get; set; }

        public string OldValue { get; set; }

        public string NewValue { get; set; }

        public int? UserID { get; set; }

        [Required, StringLength(10)]
        public string Source { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime ChangedAt { get; set; }
    }

    /// <summary>dst.USER_SCOPES — foydalanuvchi ko'radigan ombor/hudud/jamoa/mijozlar.</summary>
    [Table("USER_SCOPES")]
    public class UserScope : IAuditable
    {
        [Key]
        public int ID { get; set; }

        public int UserID { get; set; }

        [Required, StringLength(20)]
        public string ScopeType { get; set; }

        public int ScopeID { get; set; }

        [NotMapped]
        public string AuditKey => ID.ToString();
    }

    /// <summary>dst.USER_SETTINGS — interfeys tili va shaxsiy sozlamalar.</summary>
    [Table("USER_SETTINGS")]
    public class UserSetting : IAuditable
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int UserID { get; set; }

        [Required, StringLength(5)]
        public string Language { get; set; }

        [NotMapped]
        public string AuditKey => UserID.ToString();
    }
}
