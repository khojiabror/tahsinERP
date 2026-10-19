using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tahsinERP.Areas.Distribution.Data.Entities
{
    /// <summary>To'lov usullari (D_PAYMENTS.Method). Naqd puldan boshqasi darhol kompaniya hisobiga tushadi.</summary>
    public static class PaymentMethods
    {
        public const string Cash = "CASH";
        public const string Card = "CARD";
        public const string Click = "CLICK";
        public const string Payme = "PAYME";
        public const string Uzum = "UZUM";
        public const string Transfer = "TRANSFER";

        public static readonly string[] All = { Cash, Card, Click, Payme, Uzum, Transfer };

        /// <summary>Faqat naqd pul agent qo'lida qolishi mumkin; qolganlari uchun hisob majburiy.</summary>
        public static bool CanStayWithAgent(string method) => method == Cash;
    }

    /// <summary>dst.D_PAYMENTS — mijoz to'lovi.</summary>
    [Table("D_PAYMENTS")]
    public class DstPayment : DstDocument
    {
        public int CustomerID { get; set; }
        public int? OrderID { get; set; }
        public decimal Amount { get; set; }
        [Required, StringLength(10)] public string Method { get; set; }
        public int ReceivedByUserID { get; set; }
        public int? FinAccountID { get; set; }
        public int? HandoverID { get; set; }
        public int? FinTransactionID { get; set; }
        [StringLength(300)] public string Note { get; set; }
        [Required, StringLength(10)] public string Source { get; set; } = AuditActor.Web;
        public Guid? ClientRequestId { get; set; }

        /// <summary>Agent qo'lida (kassaga hali tushmagan).</summary>
        [NotMapped] public bool IsOnHand => FinAccountID == null && Status == DocumentStatus.Posted;
    }

    /// <summary>dst.CASH_HANDOVERS — agent naqdini kassaga topshirish (Draft — kutmoqda, Posted — qabul, Cancelled — rad).</summary>
    [Table("CASH_HANDOVERS")]
    public class CashHandover : DstDocument
    {
        public int AgentUserID { get; set; }
        public int FinAccountID { get; set; }
        public decimal AmountDeclared { get; set; }
        public decimal? AmountReceived { get; set; }
        [StringLength(300)] public string Note { get; set; }

        [NotMapped] public decimal? Difference => AmountReceived.HasValue ? AmountReceived - AmountDeclared : null;
    }

    /// <summary>dst.D_RETURNS — mijozdan qaytarish.</summary>
    [Table("D_RETURNS")]
    public class DstReturn : DstDocument
    {
        public int CustomerID { get; set; }
        public int? OrderID { get; set; }
        public int WarehouseID { get; set; }
        public int? ReasonID { get; set; }
        public bool IsDefect { get; set; }
        public int? IncomeID { get; set; }
        public decimal Total { get; set; }
        public decimal? CostTotal { get; set; }
        [StringLength(200)] public string PhotoKey { get; set; }
        [StringLength(300)] public string Note { get; set; }
        [Required, StringLength(10)] public string Source { get; set; } = AuditActor.Web;

        public virtual ICollection<DstReturnLine> Lines { get; set; } = new List<DstReturnLine>();
    }

    [Table("D_RETURN_LINES")]
    public class DstReturnLine : IAuditable
    {
        [Key] public int ID { get; set; }
        public int ReturnID { get; set; }
        public int ProductID { get; set; }
        public int? ShipmentLineID { get; set; }
        public decimal QtyBase { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public decimal UnitCost { get; set; }

        [ForeignKey(nameof(ReturnID))] public virtual DstReturn Return { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    /// <summary>dbo.FIN_ACCOUNTS — kassa / bank hisoblari (faqat o'qish).</summary>
    [Table("FIN_ACCOUNTS", Schema = "dbo")]
    public class FinAccountRef : IReadOnlyRef
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ID { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public int CurrencyID { get; set; }
        public bool IsCash { get; set; }
        public bool IsBank { get; set; }
        public bool IsActive { get; set; }
        public bool? IsDeleted { get; set; }
    }
}
