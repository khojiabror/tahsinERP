using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tahsinERP.Areas.Distribution.Data.Entities
{
    /// <summary>Telegram'dagi rollar (bog'lash kodida beriladi).</summary>
    public static class BotRoles
    {
        public const string Agent = "AGENT";
        public const string Expeditor = "EXPEDITOR";
        public const string Manager = "MANAGER";
        public const string Storekeeper = "STOREKEEPER";
        public const string Customer = "CUSTOMER";

        public static readonly string[] Staff = { Agent, Expeditor, Manager, Storekeeper };
    }

    [Table("TELEGRAM_LINK_CODES")]
    public class TelegramLinkCode
    {
        [Key, StringLength(16), DatabaseGenerated(DatabaseGeneratedOption.None)] public string Code { get; set; }
        public int? UserID { get; set; }
        public int? CustomerID { get; set; }
        [Required, StringLength(20)] public string Role { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public long? TelegramUserId { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    [Table("TELEGRAM_ACCOUNTS")]
    public class TelegramAccount : IAuditable
    {
        [Key] public int ID { get; set; }
        public long TelegramUserId { get; set; }
        public long ChatId { get; set; }
        public int? UserID { get; set; }
        public int? CustomerID { get; set; }
        [Required, StringLength(20)] public string Role { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime LinkedAt { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    [Table("NOTIFICATIONS")]
    public class DstNotification
    {
        [Key] public long ID { get; set; }
        [Required, StringLength(30)] public string Type { get; set; }
        public int? UserID { get; set; }
        public int? CustomerID { get; set; }
        [Required, StringLength(2000)] public string Text { get; set; }
        public string Payload { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public int Attempts { get; set; }
    }

    [Table("AGENT_DAYS")]
    public class AgentDay
    {
        [Key] public int ID { get; set; }
        public int UserID { get; set; }
        [Column(TypeName = "date")] public DateTime WorkDate { get; set; }
        public DateTime StartAt { get; set; }
        public decimal? StartLat { get; set; }
        public decimal? StartLng { get; set; }
        [StringLength(200)] public string SelfieKey { get; set; }
        public DateTime? EndAt { get; set; }
        public decimal? EndLat { get; set; }
        public decimal? EndLng { get; set; }
    }

    public static class VisitResults
    {
        public const string Order = "ORDER";
        public const string Payment = "PAYMENT";
        public const string NoOrder = "NO_ORDER";
    }

    [Table("VISITS")]
    public class Visit : IAuditable
    {
        [Key] public int ID { get; set; }
        public int UserID { get; set; }
        public int CustomerID { get; set; }
        [Column(TypeName = "date")] public DateTime VisitDate { get; set; }
        public DateTime CheckInAt { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public int? AccuracyM { get; set; }
        public int? DistanceM { get; set; }
        public bool IsLive { get; set; }
        public bool IsPlanned { get; set; }
        [StringLength(10)] public string Result { get; set; }
        public int? NoOrderReasonID { get; set; }
        public int? OrderID { get; set; }
        public int? PaymentID { get; set; }
        [StringLength(200)] public string PhotoKey { get; set; }
        [StringLength(300)] public string Note { get; set; }
        public DateTime? CheckOutAt { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }
}
