using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tahsinERP.Areas.Distribution.Data.Entities
{
    // 3-bosqich: ombor kengaytmasi (migratsiya 025_DST_Warehouse).

    /// <summary>Qo'shimcha xarajatni kirim qatorlariga taqsimlash asosi.</summary>
    public enum LandedCostBasis : byte
    {
        Value = 1,
        Quantity = 2,
        Weight = 3
    }

    public static class LandedCostKinds
    {
        public const string Freight = "FREIGHT";
        public const string Customs = "CUSTOMS";
        public const string Other = "OTHER";

        public static readonly string[] All = { Freight, Customs, Other };
    }

    /// <summary>dst.LANDED_COSTS — kirimga qo'shimcha xarajat (yo'l, bojxona...): tannarxga taqsimlanadi.</summary>
    [Table("LANDED_COSTS")]
    public class LandedCost : DstDocument
    {
        public LandedCostBasis Basis { get; set; }
        [StringLength(300)] public string Note { get; set; }
        public decimal TotalAmount { get; set; }
        /// <summary>Taqsimlangan xarajatning allaqachon chiqarilgan tovarga tushgan qismi (o'tgan chiqimlar qayta baholanmaydi).</summary>
        public decimal ConsumedAmount { get; set; }

        public virtual ICollection<LandedCostIncome> Incomes { get; set; } = new List<LandedCostIncome>();
        public virtual ICollection<LandedCostItem> Items { get; set; } = new List<LandedCostItem>();
        public virtual ICollection<LandedCostLine> Lines { get; set; } = new List<LandedCostLine>();
    }

    [Table("LANDED_COST_INCOMES")]
    public class LandedCostIncome : IAuditable
    {
        [Key] public int ID { get; set; }
        public int LandedCostID { get; set; }
        public int IncomeID { get; set; }
        [ForeignKey(nameof(LandedCostID))] public virtual LandedCost LandedCost { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    [Table("LANDED_COST_ITEMS")]
    public class LandedCostItem : IAuditable
    {
        [Key] public int ID { get; set; }
        public int LandedCostID { get; set; }
        [Required, StringLength(10)] public string Kind { get; set; }
        [StringLength(200)] public string Description { get; set; }
        public decimal Amount { get; set; }
        [ForeignKey(nameof(LandedCostID))] public virtual LandedCost LandedCost { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    [Table("LANDED_COST_LINES")]
    public class LandedCostLine : IAuditable
    {
        [Key] public int ID { get; set; }
        public int LandedCostID { get; set; }
        public int IncomeID { get; set; }
        public int IncomeProductID { get; set; }
        public int ProductID { get; set; }
        public decimal Qty { get; set; }
        public decimal BaseValue { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal Allocated { get; set; }
        public decimal UnitAdd { get; set; }
        public decimal ConsumedQty { get; set; }
        [ForeignKey(nameof(LandedCostID))] public virtual LandedCost LandedCost { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    /// <summary>dst.WRITE_OFFS — hisobdan chiqarish: qoralama → tasdiqqa yuborildi → rahbar tasdig'i (o'tkazildi) yoki rad (bekor).</summary>
    [Table("WRITE_OFFS")]
    public class WriteOff : DstDocument
    {
        public int WarehouseID { get; set; }
        public int? ReasonID { get; set; }
        [StringLength(300)] public string Note { get; set; }
        [StringLength(200)] public string PhotoKey { get; set; }
        public int? SubmittedBy { get; set; }
        [Column(TypeName = "datetime2")] public DateTime? SubmittedAt { get; set; }
        public int? ExpenseID { get; set; }
        public decimal CostTotal { get; set; }

        [NotMapped] public bool IsSubmitted => Status == DocumentStatus.Draft && SubmittedAt.HasValue;

        public virtual ICollection<WriteOffLine> Lines { get; set; } = new List<WriteOffLine>();
    }

    [Table("WRITE_OFF_LINES")]
    public class WriteOffLine : IAuditable
    {
        [Key] public int ID { get; set; }
        public int WriteOffID { get; set; }
        public int ProductID { get; set; }
        public decimal QtyBase { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal? CostAmount { get; set; }
        [StringLength(200)] public string Note { get; set; }
        [ForeignKey(nameof(WriteOffID))] public virtual WriteOff WriteOff { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    /// <summary>dst.REGRADES — pereso'rtitsa: bir tovar chiqim, boshqasi kirim, tannarx saqlanadi.</summary>
    [Table("REGRADES")]
    public class Regrade : DstDocument
    {
        public int WarehouseID { get; set; }
        public int? ReasonID { get; set; }
        [StringLength(300)] public string Note { get; set; }
        public int? ExpenseID { get; set; }
        public int? IncomeID { get; set; }
        public decimal CostTotal { get; set; }

        public virtual ICollection<RegradeLine> Lines { get; set; } = new List<RegradeLine>();
    }

    [Table("REGRADE_LINES")]
    public class RegradeLine : IAuditable
    {
        [Key] public int ID { get; set; }
        public int RegradeID { get; set; }
        public int FromProductID { get; set; }
        public int ToProductID { get; set; }
        public decimal FromQtyBase { get; set; }
        public decimal ToQtyBase { get; set; }
        public decimal? CostAmount { get; set; }
        [ForeignKey(nameof(RegradeID))] public virtual Regrade Regrade { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    public static class TransferResolutions
    {
        /// <summary>Kamomad — yo'qotish (jo'natuvchidan chiqib ketgan, hech kimga kirim emas).</summary>
        public const string Loss = "LOSS";
        /// <summary>Kamomad jo'natuvchi omborga qaytariladi (jo'natilmay qolgan).</summary>
        public const string Return = "RETURN";

        public static readonly string[] All = { Loss, Return };
    }

    /// <summary>dst.TRANSFER_ACTS — ombordan omborga ko'chirmani haqiqiy miqdor bilan qabul qilish va farq dalolatnomasi.</summary>
    [Table("TRANSFER_ACTS")]
    public class TransferAct : DstDocument
    {
        public int IncomeID { get; set; }
        public int? ExpenseID { get; set; }
        public int FromWarehouseID { get; set; }
        public int ToWarehouseID { get; set; }
        [StringLength(300)] public string Note { get; set; }
        public decimal ShortageCost { get; set; }
        public decimal LossCost { get; set; }
        public decimal SurplusCost { get; set; }
        public int? ReturnIncomeID { get; set; }

        public virtual ICollection<TransferActLine> Lines { get; set; } = new List<TransferActLine>();
    }

    [Table("TRANSFER_ACT_LINES")]
    public class TransferActLine : IAuditable
    {
        [Key] public int ID { get; set; }
        public int ActID { get; set; }
        public int IncomeProductID { get; set; }
        public int ProductID { get; set; }
        public int UnitID { get; set; }
        public decimal SentQty { get; set; }
        public decimal ReceivedQty { get; set; }
        public decimal UnitCost { get; set; }
        [StringLength(10)] public string Resolution { get; set; }
        [StringLength(200)] public string Note { get; set; }
        [ForeignKey(nameof(ActID))] public virtual TransferAct Act { get; set; }
        [NotMapped] public decimal Diff => ReceivedQty - SentQty;
        [NotMapped] public string AuditKey => ID.ToString();
    }

    public static class PromotionKinds
    {
        /// <summary>Chegirma: tovar/kategoriya/hamma, qatordagi miqdor MinQtyBase dan kam bo'lmasa (0 — muddatli chegirma).</summary>
        public const string Discount = "DISCOUNT";
        /// <summary>Sovg'a: har BuyQtyBase uchun FreeQtyBase (masalan 10+1).</summary>
        public const string Free = "FREE";

        public static readonly string[] All = { Discount, Free };
    }

    /// <summary>dst.PROMOTIONS — aksiya. Buyurtmada avtomatik qo'llanadi (OrderService.SaveDraft → PromotionService).</summary>
    [Table("PROMOTIONS")]
    public class Promotion : IAuditable
    {
        [Key] public int ID { get; set; }
        [Required, StringLength(150)] public string Name { get; set; }
        [Required, StringLength(10)] public string Kind { get; set; }
        [Column(TypeName = "date")] public DateTime DateFrom { get; set; }
        [Column(TypeName = "date")] public DateTime? DateTo { get; set; }
        public bool IsActive { get; set; } = true;
        public int? ProductID { get; set; }
        public int? CategoryID { get; set; }
        public int? PriceTypeID { get; set; }
        public decimal MinQtyBase { get; set; }
        public decimal? DiscountPct { get; set; }
        public decimal? BuyQtyBase { get; set; }
        public decimal? FreeQtyBase { get; set; }
        public int? FreeProductID { get; set; }
        public bool Repeatable { get; set; } = true;
        [StringLength(300)] public string Note { get; set; }
        public int? CreatedBy { get; set; }
        [Column(TypeName = "datetime2")] public DateTime CreatedAt { get; set; }
        [Column(TypeName = "datetime2")] public DateTime? UpdatedAt { get; set; }

        public bool IsValidOn(DateTime day) => IsActive && DateFrom <= day.Date && (DateTo == null || DateTo >= day.Date);

        [NotMapped] public string AuditKey => ID.ToString();
    }
}
