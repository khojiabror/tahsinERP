using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tahsinERP.Areas.Distribution.Data.Entities
{
    /// <summary>dst.TERRITORIES — viloyat (1) → tuman (2) → hudud (3).</summary>
    [Table("TERRITORIES")]
    public class Territory : IAuditable
    {
        public const byte Region = 1;
        public const byte District = 2;
        public const byte Area = 3;

        [Key]
        public int ID { get; set; }
        public int? ParentID { get; set; }
        public byte Level { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; }

        [StringLength(100)]
        public string NameRu { get; set; }

        public bool IsActive { get; set; } = true;

        [NotMapped] public string AuditKey => ID.ToString();
    }

    /// <summary>Savdo nuqtasi turlari (CUSTOMER_PROFILES.PointType).</summary>
    public static class PointTypes
    {
        public const string Shop = "SHOP";            // xo'jalik mollari do'koni
        public const string Market = "MARKET";        // qurilish bozori
        public const string Brigade = "BRIGADE";      // qurilish brigadasi
        public const string Foreman = "FOREMAN";      // prorab
        public const string Private = "PRIVATE";      // xususiy mijoz

        public static readonly string[] All = { Shop, Market, Brigade, Foreman, Private };
    }

    /// <summary>Tashrif kunlari bitmask'i: Du=1, Se=2, Ch=4, Pa=8, Ju=16, Sh=32, Ya=64.</summary>
    public static class VisitDays
    {
        public static byte For(DayOfWeek day) => (byte)(1 << (((int)day + 6) % 7));

        public static bool Includes(byte mask, DayOfWeek day) => (mask & For(day)) != 0;
    }

    /// <summary>dst.CUSTOMER_PROFILES — dbo.CUSTOMERS ning distributsiya maydonlari (1:1).</summary>
    [Table("CUSTOMER_PROFILES")]
    public class CustomerProfile : IAuditable
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int CustomerID { get; set; }

        [StringLength(14)] public string INN { get; set; }
        [StringLength(25)] public string Phone2 { get; set; }
        [StringLength(100)] public string OwnerName { get; set; }
        [StringLength(200)] public string Landmark { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        [StringLength(200)] public string PhotoKey { get; set; }

        [Required, StringLength(20)]
        public string PointType { get; set; } = PointTypes.Shop;

        [StringLength(1)] public string Category { get; set; }
        public int? TerritoryID { get; set; }
        public int? AgentUserID { get; set; }
        public int? PriceTypeID { get; set; }
        public byte VisitDays { get; set; }
        public decimal CreditLimit { get; set; }
        public int PaymentDays { get; set; }
        public bool IsApproved { get; set; } = true;
        public int? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        [Required, StringLength(10)]
        public string CreatedVia { get; set; } = AuditActor.Web;

        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        [NotMapped] public string AuditKey => CustomerID.ToString();
    }

    [Table("BRANDS")]
    public class Brand : IAuditable
    {
        [Key] public int ID { get; set; }
        [Required, StringLength(100)] public string Name { get; set; }
        public bool IsActive { get; set; } = true;
        [NotMapped] public string AuditKey => ID.ToString();
    }

    [Table("MANUFACTURERS")]
    public class Manufacturer : IAuditable
    {
        [Key] public int ID { get; set; }
        [Required, StringLength(100)] public string Name { get; set; }
        [StringLength(50)] public string Country { get; set; }
        public bool IsActive { get; set; } = true;
        [NotMapped] public string AuditKey => ID.ToString();
    }

    [Table("SUB_CATEGORIES")]
    public class SubCategory : IAuditable
    {
        [Key] public int ID { get; set; }
        public int CategoryID { get; set; }
        [Required, StringLength(100)] public string Name { get; set; }
        [StringLength(100)] public string NameRu { get; set; }
        public bool IsActive { get; set; } = true;
        [NotMapped] public string AuditKey => ID.ToString();
    }

    /// <summary>dst.PRODUCT_PROFILES — dbo.PRODUCTS ning distributsiya maydonlari (1:1).</summary>
    [Table("PRODUCT_PROFILES")]
    public class ProductProfile : IAuditable
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ProductID { get; set; }
        [StringLength(150)] public string NameRu { get; set; }
        public int? BrandID { get; set; }
        public int? ManufacturerID { get; set; }
        public int? SubCategoryID { get; set; }
        public bool IsSaleActive { get; set; } = true;
        public bool IsBulky { get; set; }
        public bool IsWeighted { get; set; }
        public decimal? BaseWeightKg { get; set; }
        public decimal? BaseVolumeM3 { get; set; }
        [NotMapped] public string AuditKey => ProductID.ToString();
    }

    /// <summary>dst.PRODUCT_UNITS — 1 shu birlik = Factor asosiy birlik (PRODUCTS.UnitID).</summary>
    [Table("PRODUCT_UNITS")]
    public class ProductUnit : IAuditable
    {
        [Key] public int ID { get; set; }
        public int ProductID { get; set; }
        public int UnitID { get; set; }
        public decimal Factor { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? VolumeM3 { get; set; }
        public bool IsDefaultSale { get; set; }
        public bool IsActive { get; set; } = true;
        [NotMapped] public string AuditKey => ID.ToString();
    }

    [Table("PRODUCT_BARCODES")]
    public class ProductBarcode : IAuditable
    {
        [Key] public int ID { get; set; }
        [Required, StringLength(50)] public string Barcode { get; set; }
        public int ProductID { get; set; }
        public int? ProductUnitID { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    [Table("PRICE_TYPES")]
    public class PriceType : IAuditable
    {
        [Key] public int ID { get; set; }
        [Required, StringLength(50)] public string Name { get; set; }
        [StringLength(50)] public string NameRu { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    /// <summary>dst.PRICE_LIST_ITEMS — narx o'zgarishi = yangi qator (tarix ValidFrom bo'yicha).</summary>
    [Table("PRICE_LIST_ITEMS")]
    public class PriceListItem : IAuditable
    {
        [Key] public int ID { get; set; }
        public int PriceTypeID { get; set; }
        public int ProductID { get; set; }
        public decimal Price { get; set; }
        public decimal? MinPrice { get; set; }
        public DateTime ValidFrom { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    [Table("CUSTOMER_PRICES")]
    public class CustomerPrice : IAuditable
    {
        [Key] public int ID { get; set; }
        public int CustomerID { get; set; }
        public int ProductID { get; set; }
        public decimal Price { get; set; }
        [Column(TypeName = "date")] public DateTime ValidFrom { get; set; }
        [Column(TypeName = "date")] public DateTime? ValidTo { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    [Table("SALES_TEAMS")]
    public class SalesTeam : IAuditable
    {
        [Key] public int ID { get; set; }
        [Required, StringLength(100)] public string Name { get; set; }
        public int? SupervisorUserID { get; set; }
        public bool IsActive { get; set; } = true;
        [NotMapped] public string AuditKey => ID.ToString();
    }

    /// <summary>dst.SALES_AGENTS — agent yoki ekspeditor (ERP foydalanuvchisi).</summary>
    [Table("SALES_AGENTS")]
    public class SalesAgent : IAuditable
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int UserID { get; set; }
        public int? TeamID { get; set; }
        public int? WarehouseID { get; set; }
        [StringLength(25)] public string Phone { get; set; }
        public bool IsExpeditor { get; set; }
        public bool IsActive { get; set; } = true;
        [NotMapped] public string AuditKey => UserID.ToString();
    }

    [Table("DISCOUNT_LIMITS")]
    public class DiscountLimit : IAuditable
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int RoleID { get; set; }
        public decimal MaxPercent { get; set; }
        [NotMapped] public string AuditKey => RoleID.ToString();
    }

    /// <summary>dst.REASONS.Type qiymatlari.</summary>
    public static class ReasonTypes
    {
        public const string Return = "RETURN";
        public const string NoOrder = "NO_ORDER";
        public const string Cancel = "CANCEL";
        public const string WriteOff = "WRITE_OFF";

        public static readonly string[] All = { Return, NoOrder, Cancel, WriteOff };
    }

    [Table("REASONS")]
    public class Reason : IAuditable
    {
        [Key] public int ID { get; set; }
        [Required, StringLength(20)] public string Type { get; set; }
        [Required, StringLength(100)] public string Name { get; set; }
        [StringLength(100)] public string NameRu { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    [Table("WAREHOUSE_SETTINGS")]
    public class WarehouseSetting : IAuditable
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int WarehouseID { get; set; }
        public bool IsSalesPoint { get; set; } = true;
        public bool AllowNegative { get; set; }
        [StringLength(200)] public string DeliveryZone { get; set; }
        [NotMapped] public string AuditKey => WarehouseID.ToString();
    }

    [Table("PRODUCT_STOCK_LIMITS")]
    public class ProductStockLimit : IAuditable
    {
        [Key, Column(Order = 0), DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int WarehouseID { get; set; }

        [Key, Column(Order = 1), DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ProductID { get; set; }

        public decimal MinQty { get; set; }
        [NotMapped] public string AuditKey => WarehouseID + ":" + ProductID;
    }
}
