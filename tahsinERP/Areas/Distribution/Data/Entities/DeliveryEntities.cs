using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tahsinERP.Areas.Distribution.Data.Entities
{
    /// <summary>dst.VEHICLES — yetkazish mashinasi.</summary>
    [Table("VEHICLES")]
    public class Vehicle : IAuditable
    {
        [Key] public int ID { get; set; }
        [Required, StringLength(20)] public string PlateNo { get; set; }
        [StringLength(100)] public string Model { get; set; }
        public decimal CapacityKg { get; set; }
        public decimal? CapacityM3 { get; set; }
        [StringLength(100)] public string DriverName { get; set; }
        [StringLength(25)] public string DriverPhone { get; set; }
        public int? ExpeditorUserID { get; set; }
        public int? WarehouseID { get; set; }
        public bool IsActive { get; set; } = true;
        [StringLength(300)] public string Note { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    /// <summary>Reys holati (TRIPS.TripStatus). Raqamlar bazada saqlanadi.</summary>
    public enum TripStatus : byte
    {
        Planned = 0,     // rejalandi: jo'natishlar biriktiriladi, tartib o'zgaradi
        InTransit = 1,   // yo'lda: ekspeditor yetkazmoqda
        Closed = 2,      // yopildi: qaytgan tovar omborga, farq dalolatnomasi
        Cancelled = 9
    }

    /// <summary>Reysdagi to'xtash natijasi (TRIP_STOPS.StopStatus).</summary>
    public enum StopStatus : byte
    {
        Pending = 0,
        Delivered = 1,
        Partial = 2,
        Refused = 3
    }

    /// <summary>
    /// dst.TRIPS — reys. Hujjat holati: Draft (rejalandi/yo'lda), Posted (yopildi), Cancelled (faqat rejalangan).
    /// </summary>
    [Table("TRIPS")]
    public class Trip : DstDocument
    {
        public byte TripStatus { get; set; }
        public int VehicleID { get; set; }
        public int ExpeditorUserID { get; set; }
        public int WarehouseID { get; set; }
        public decimal WeightKg { get; set; }
        public decimal VolumeM3 { get; set; }
        public decimal Total { get; set; }
        public bool RouteOptimized { get; set; }
        [Column(TypeName = "datetime2")] public DateTime? StartedAt { get; set; }
        [Column(TypeName = "datetime2")] public DateTime? FinishedAt { get; set; }
        public decimal ShortageCost { get; set; }
        [StringLength(500)] public string Note { get; set; }
        [Timestamp] public byte[] RowVer { get; set; }

        [NotMapped] public TripStatus State => (TripStatus)TripStatus;
    }

    [Table("TRIP_STOPS")]
    public class TripStop : IAuditable
    {
        [Key] public int ID { get; set; }
        public int TripID { get; set; }
        public int ShipmentID { get; set; }
        public int OrderID { get; set; }
        public int CustomerID { get; set; }
        public int Seq { get; set; }
        public byte StopStatus { get; set; }
        public decimal Amount { get; set; }
        public decimal? DeliveredAmount { get; set; }
        public decimal WeightKg { get; set; }
        public decimal VolumeM3 { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public int? DeliveredBy { get; set; }
        [StringLength(200)] public string PhotoKey { get; set; }
        [StringLength(200)] public string SignatureKey { get; set; }
        public int? ReasonID { get; set; }
        [StringLength(300)] public string Note { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public int? ReturnID { get; set; }
        public decimal ShortageAmount { get; set; }

        public virtual ICollection<TripStopLine> Lines { get; set; } = new List<TripStopLine>();

        [NotMapped] public StopStatus State => (StopStatus)StopStatus;
        [NotMapped] public string AuditKey => ID.ToString();
    }

    [Table("TRIP_STOP_LINES")]
    public class TripStopLine : IAuditable
    {
        [Key] public int ID { get; set; }
        public int StopID { get; set; }
        public int ShipmentLineID { get; set; }
        public int ProductID { get; set; }
        public decimal QtyBase { get; set; }
        public decimal Amount { get; set; }
        public decimal? QtyDelivered { get; set; }
        public decimal? QtyReturned { get; set; }

        [ForeignKey(nameof(StopID))] public virtual TripStop Stop { get; set; }

        /// <summary>Yetkazilmagan (mijozga berilmagan) miqdor.</summary>
        [NotMapped] public decimal QtyUndelivered => QtyDelivered.HasValue ? QtyBase - QtyDelivered.Value : 0;
        [NotMapped] public string AuditKey => ID.ToString();
    }

    /// <summary>Reys xarajati turlari (TRIP_EXPENSES.Kind).</summary>
    public static class TripExpenseKinds
    {
        public const string Fuel = "FUEL";
        public const string Parking = "PARKING";
        public const string Repair = "REPAIR";
        public const string Meal = "MEAL";
        public const string Other = "OTHER";

        public static readonly string[] All = { Fuel, Parking, Repair, Meal, Other };
    }

    [Table("TRIP_EXPENSES")]
    public class TripExpense : IAuditable
    {
        public const byte Pending = 0;
        public const byte Posted = 1;
        public const byte Cancelled = 2;

        [Key] public int ID { get; set; }
        public int TripID { get; set; }
        public int? CategoryID { get; set; }
        [Required, StringLength(10)] public string Kind { get; set; } = TripExpenseKinds.Other;
        public decimal Amount { get; set; }
        public int? FinAccountID { get; set; }
        public byte Status { get; set; }
        public int? FinTransactionID { get; set; }
        [StringLength(300)] public string Note { get; set; }
        [StringLength(200)] public string PhotoKey { get; set; }
        [Required, StringLength(10)] public string Source { get; set; } = AuditActor.Web;
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? PostedBy { get; set; }
        public DateTime? PostedAt { get; set; }
        [NotMapped] public string AuditKey => ID.ToString();
    }

    /// <summary>dst.VISIT_PLANS — agentning aniq kunga tashrif rejasi (tartib bilan).</summary>
    [Table("VISIT_PLANS")]
    public class VisitPlan
    {
        [Key] public int ID { get; set; }
        public int AgentUserID { get; set; }
        [Column(TypeName = "date")] public DateTime PlanDate { get; set; }
        public int CustomerID { get; set; }
        public int Seq { get; set; }
        [StringLength(200)] public string Note { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>dst.KPI_SCHEMES — agent (ekspeditor) KPI sxemasi.</summary>
    [Table("KPI_SCHEMES")]
    public class KpiScheme : IAuditable
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public int AgentUserID { get; set; }
        public int? EmployeeID { get; set; }
        public decimal SalesPct { get; set; }
        public decimal MonthlyPlan { get; set; }
        public decimal PlanBonus { get; set; }
        public decimal OverPlanPct { get; set; }
        public decimal MinCoveragePct { get; set; }
        public decimal CoveragePenalty { get; set; }
        public decimal OverduePenaltyPct { get; set; }
        public decimal ShortagePenaltyPct { get; set; }
        public bool IsActive { get; set; } = true;
        public int? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        [NotMapped] public string AuditKey => AgentUserID.ToString();
    }

    /// <summary>dst.KPI_RESULTS — oy bo'yicha hisoblangan KPI (Status 1 — HR ish haqi qatoriga o'tkazilgan).</summary>
    [Table("KPI_RESULTS")]
    public class KpiResult : IAuditable
    {
        public const byte Calculated = 0;
        public const byte Applied = 1;

        [Key] public int ID { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int AgentUserID { get; set; }
        public int? EmployeeID { get; set; }
        public decimal NetSales { get; set; }
        public decimal PlanAmount { get; set; }
        public decimal? PlanPct { get; set; }
        public decimal? CoveragePct { get; set; }
        public decimal OverdueDebt { get; set; }
        public decimal ShortageCost { get; set; }
        public decimal Commission { get; set; }
        public decimal Bonus { get; set; }
        public decimal Penalty { get; set; }
        public decimal ManualBonus { get; set; }
        public decimal ManualPenalty { get; set; }
        [StringLength(300)] public string Note { get; set; }
        public byte Status { get; set; }
        public int? PayrollLineID { get; set; }
        public decimal? AppliedBonus { get; set; }
        public decimal? AppliedPenalty { get; set; }
        public int CalculatedBy { get; set; }
        public DateTime CalculatedAt { get; set; }
        public int? AppliedBy { get; set; }
        public DateTime? AppliedAt { get; set; }

        /// <summary>Ish haqiga mukofot sifatida: % savdo + bonus + qo'lda bonus.</summary>
        [NotMapped] public decimal TotalBonus => Commission + Bonus + ManualBonus;
        [NotMapped] public decimal TotalPenalty => Penalty + ManualPenalty;
        [NotMapped] public string AuditKey => ID.ToString();
    }
}
