using System.Data.Entity;
using tahsinERP.Areas.Distribution.Data.Entities;

namespace tahsinERP.Areas.Distribution.Data
{
    /// <summary>2-bosqich: yetkazish va nazorat (026_DST_Delivery) — alohida fayl (parallel ishlar bilan konflikt bo'lmasin).</summary>
    public partial class DistributionDbContext
    {
        public virtual DbSet<Vehicle> Vehicles { get; set; }
        public virtual DbSet<Trip> Trips { get; set; }
        public virtual DbSet<TripStop> TripStops { get; set; }
        public virtual DbSet<TripStopLine> TripStopLines { get; set; }
        public virtual DbSet<TripExpense> TripExpenses { get; set; }
        public virtual DbSet<VisitPlan> VisitPlans { get; set; }
        public virtual DbSet<KpiScheme> KpiSchemes { get; set; }
        public virtual DbSet<KpiResult> KpiResults { get; set; }

        partial void OnModelCreatingDelivery(DbModelBuilder modelBuilder)
        {
            var v = modelBuilder.Entity<Vehicle>();
            v.Property(e => e.CapacityKg).HasPrecision(18, 2);
            v.Property(e => e.CapacityM3).HasPrecision(18, 3);
            v.Property(e => e.DriverPhone).IsUnicode(false);

            var t = modelBuilder.Entity<Trip>();
            t.Property(e => e.WeightKg).HasPrecision(18, 3);
            t.Property(e => e.VolumeM3).HasPrecision(18, 4);
            t.Property(e => e.Total).HasPrecision(19, 2);
            t.Property(e => e.ShortageCost).HasPrecision(19, 2);

            var s = modelBuilder.Entity<TripStop>();
            s.Property(e => e.Amount).HasPrecision(19, 2);
            s.Property(e => e.DeliveredAmount).HasPrecision(19, 2);
            s.Property(e => e.WeightKg).HasPrecision(18, 3);
            s.Property(e => e.VolumeM3).HasPrecision(18, 4);
            s.Property(e => e.Lat).HasPrecision(9, 6);
            s.Property(e => e.Lng).HasPrecision(9, 6);
            s.Property(e => e.ShortageAmount).HasPrecision(19, 2);
            s.Property(e => e.PhotoKey).IsUnicode(false);
            s.Property(e => e.SignatureKey).IsUnicode(false);
            s.Property(e => e.DeliveredAt).HasColumnType("datetime2");

            var sl = modelBuilder.Entity<TripStopLine>();
            sl.Property(e => e.QtyBase).HasPrecision(18, 4);
            sl.Property(e => e.Amount).HasPrecision(19, 2);
            sl.Property(e => e.QtyDelivered).HasPrecision(18, 4);
            sl.Property(e => e.QtyReturned).HasPrecision(18, 4);

            var x = modelBuilder.Entity<TripExpense>();
            x.Property(e => e.Amount).HasPrecision(19, 2);
            x.Property(e => e.Kind).IsUnicode(false);
            x.Property(e => e.PhotoKey).IsUnicode(false);
            x.Property(e => e.Source).IsUnicode(false);

            var k = modelBuilder.Entity<KpiScheme>();
            k.Property(e => e.SalesPct).HasPrecision(5, 2);
            k.Property(e => e.MonthlyPlan).HasPrecision(19, 2);
            k.Property(e => e.PlanBonus).HasPrecision(19, 2);
            k.Property(e => e.OverPlanPct).HasPrecision(5, 2);
            k.Property(e => e.MinCoveragePct).HasPrecision(5, 2);
            k.Property(e => e.CoveragePenalty).HasPrecision(19, 2);
            k.Property(e => e.OverduePenaltyPct).HasPrecision(5, 2);
            k.Property(e => e.ShortagePenaltyPct).HasPrecision(5, 2);

            var r = modelBuilder.Entity<KpiResult>();
            r.Property(e => e.NetSales).HasPrecision(19, 2);
            r.Property(e => e.PlanAmount).HasPrecision(19, 2);
            r.Property(e => e.PlanPct).HasPrecision(7, 2);
            r.Property(e => e.CoveragePct).HasPrecision(7, 2);
            r.Property(e => e.OverdueDebt).HasPrecision(19, 2);
            r.Property(e => e.ShortageCost).HasPrecision(19, 2);
            r.Property(e => e.Commission).HasPrecision(19, 2);
            r.Property(e => e.Bonus).HasPrecision(19, 2);
            r.Property(e => e.Penalty).HasPrecision(19, 2);
            r.Property(e => e.ManualBonus).HasPrecision(19, 2);
            r.Property(e => e.ManualPenalty).HasPrecision(19, 2);
            r.Property(e => e.AppliedBonus).HasPrecision(19, 2);
            r.Property(e => e.AppliedPenalty).HasPrecision(19, 2);
        }
    }
}
