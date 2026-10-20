using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using tahsinERP.Areas.Distribution.Data.Entities;

namespace tahsinERP.Areas.Site.Data
{
    // `site` sxemasi (Database/Migrations/021_SITE.sql). DistributionDbContext ichida: sayt narx turlari,
    // birliklar va qoldiqlarni distributsiya jadvallaridan oladi — bitta kontekst, bitta audit.

    public static class SiteSettingKeys
    {
        public const string Enabled = "Enabled";
        public const string Name = "Name";
        public const string Tagline = "Tagline";
        public const string LogoMediaId = "LogoMediaId";
        public const string PrimaryColor = "PrimaryColor";
        public const string Font = "Font";
        public const string Radius = "Radius";
        public const string Languages = "Languages";
        public const string Phone = "Phone";
        public const string Phone2 = "Phone2";
        public const string Email = "Email";
        public const string Address = "Address";
        public const string WorkingHours = "WorkingHours";
        public const string MapLat = "MapLat";
        public const string MapLng = "MapLng";
        public const string Telegram = "Telegram";
        public const string Instagram = "Instagram";
        public const string Facebook = "Facebook";
        public const string PriceMode = "PriceMode";
        public const string PriceTypeId = "PriceTypeId";
        public const string StockMode = "StockMode";
        public const string LowStockQty = "LowStockQty";
        public const string LeadsEnabled = "LeadsEnabled";
        public const string SeoTitle = "SeoTitle";
        public const string SeoDescription = "SeoDescription";
        public const string AnalyticsId = "AnalyticsId";
        public const string MetrikaId = "MetrikaId";
    }

    public static class PriceModes
    {
        public const string Show = "SHOW";
        public const string Ask = "ASK";
        public const string Hidden = "HIDDEN";
        public static readonly string[] All = { Show, Ask, Hidden };
    }

    public static class StockModes
    {
        public const string Exact = "EXACT";
        public const string Status = "STATUS";
        public const string Hidden = "HIDDEN";
        public static readonly string[] All = { Exact, Status, Hidden };
    }

    public static class ProductBadges
    {
        public const string New = "NEW";
        public const string Sale = "SALE";
        public const string Hit = "HIT";
        public static readonly string[] All = { New, Sale, Hit };
    }

    public enum DomainStatus : byte { Pending = 0, Active = 1, Rejected = 2 }

    public enum LeadStatus : byte { New = 0, Seen = 1, Ordered = 2, Rejected = 3 }

    [Table("SETTINGS", Schema = "site")]
    public class SiteSetting : IAuditable
    {
        [Key, StringLength(50), Column("Key")] public string Key { get; set; }
        [Column("Value")] public string Value { get; set; }
        public DateTime UpdatedAt { get; set; }
        [NotMapped] public string AuditKey => Key;
    }

    [Table("DOMAINS", Schema = "site")]
    public class SiteDomain : IAuditable
    {
        [Key] public int ID { get; set; }
        [Required, StringLength(253)] public string Host { get; set; }
        public bool IsPrimary { get; set; }
        public byte Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? VerifiedAt { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    /// <summary>Rasm (bazada). Audit qilinmaydi — ikkilik ma'lumot jurnalga yozilmasin.</summary>
    [Table("MEDIA", Schema = "site")]
    public class SiteMedia
    {
        [Key] public int ID { get; set; }
        [Required] public byte[] Content { get; set; }
        [Required, StringLength(50)] public string ContentType { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Bytes { get; set; }
        [Required, StringLength(64)] public string Hash { get; set; }
        [StringLength(200)] public string AltUz { get; set; }
        [StringLength(200)] public string AltRu { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    [Table("CATEGORY_CONTENT", Schema = "site")]
    public class SiteCategoryContent : IAuditable
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public int CategoryID { get; set; }
        public bool IsPublished { get; set; }
        [Required, StringLength(120)] public string Slug { get; set; }
        [StringLength(150)] public string NameUz { get; set; }
        [StringLength(150)] public string NameRu { get; set; }
        [StringLength(2000)] public string DescriptionUz { get; set; }
        [StringLength(2000)] public string DescriptionRu { get; set; }
        public int? MediaID { get; set; }
        public int SortOrder { get; set; }
        [StringLength(150)] public string SeoTitle { get; set; }
        [StringLength(300)] public string SeoDescription { get; set; }
        public DateTime UpdatedAt { get; set; }
        [NotMapped] public string AuditKey => CategoryID.ToString();
    }

    [Table("PRODUCT_CONTENT", Schema = "site")]
    public class SiteProductContent : IAuditable
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public int ProductID { get; set; }
        public bool IsPublished { get; set; }
        [Required, StringLength(160)] public string Slug { get; set; }
        [StringLength(200)] public string TitleUz { get; set; }
        [StringLength(200)] public string TitleRu { get; set; }
        public string DescriptionUz { get; set; }
        public string DescriptionRu { get; set; }
        [StringLength(50)] public string Badges { get; set; }
        public int SortOrder { get; set; }
        [StringLength(150)] public string SeoTitle { get; set; }
        [StringLength(300)] public string SeoDescription { get; set; }
        public DateTime UpdatedAt { get; set; }
        [NotMapped] public string AuditKey => ProductID.ToString();
    }

    [Table("PRODUCT_ATTRIBUTES", Schema = "site")]
    public class SiteProductAttribute : IAuditable
    {
        [Key] public int ID { get; set; }
        public int ProductID { get; set; }
        [Required, StringLength(100)] public string NameUz { get; set; }
        [StringLength(100)] public string NameRu { get; set; }
        [Required, StringLength(200)] public string ValueUz { get; set; }
        [StringLength(200)] public string ValueRu { get; set; }
        public int SortOrder { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    [Table("PRODUCT_MEDIA", Schema = "site")]
    public class SiteProductMedia : IAuditable
    {
        [Key, Column(Order = 0), DatabaseGenerated(DatabaseGeneratedOption.None)] public int ProductID { get; set; }
        [Key, Column(Order = 1), DatabaseGenerated(DatabaseGeneratedOption.None)] public int MediaID { get; set; }
        public int SortOrder { get; set; }
        [NotMapped] public string AuditKey => ProductID + "/" + MediaID;
    }

    [Table("PAGES", Schema = "site")]
    public class SitePage : IAuditable
    {
        [Key] public int ID { get; set; }
        [Required, StringLength(120)] public string Slug { get; set; }
        [Required, StringLength(150)] public string TitleUz { get; set; }
        [StringLength(150)] public string TitleRu { get; set; }
        public string BodyUz { get; set; }
        public string BodyRu { get; set; }
        public bool IsPublished { get; set; }
        public bool ShowInMenu { get; set; }
        public int SortOrder { get; set; }
        public DateTime UpdatedAt { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    [Table("BANNERS", Schema = "site")]
    public class SiteBanner : IAuditable
    {
        [Key] public int ID { get; set; }
        public int MediaID { get; set; }
        [StringLength(150)] public string TitleUz { get; set; }
        [StringLength(150)] public string TitleRu { get; set; }
        [StringLength(300)] public string SubtitleUz { get; set; }
        [StringLength(300)] public string SubtitleRu { get; set; }
        [StringLength(300)] public string Link { get; set; }
        [Column(TypeName = "date")] public DateTime? ActiveFrom { get; set; }
        [Column(TypeName = "date")] public DateTime? ActiveTo { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public DateTime UpdatedAt { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    [Table("LEADS", Schema = "site")]
    public class SiteLead : IAuditable
    {
        [Key] public int ID { get; set; }
        public Guid ClientRequestId { get; set; }
        public DateTime CreatedAt { get; set; }
        [Required, StringLength(100)] public string Name { get; set; }
        [Required, StringLength(20)] public string Phone { get; set; }
        [StringLength(200)] public string CompanyName { get; set; }
        [StringLength(20)] public string INN { get; set; }
        [StringLength(1000)] public string Message { get; set; }
        public string Items { get; set; }
        [Required, StringLength(2)] public string Language { get; set; }
        [StringLength(253)] public string Host { get; set; }
        public byte Status { get; set; }
        public int? CustomerID { get; set; }
        public int? OrderID { get; set; }
        public int? ProcessedBy { get; set; }
        public DateTime? ProcessedAt { get; set; }
        [StringLength(300)] public string RejectReason { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }
}
