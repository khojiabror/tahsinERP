using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Globalization;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using tahsinERP.Areas.Distribution.Data.Entities;
using tahsinERP.Areas.Distribution.Services;
using tahsinERP.Models;

namespace tahsinERP.Areas.Distribution.Data
{
    /// <summary>Kim va qayerdan o'zgartirmoqda (jurnal uchun).</summary>
    public sealed class AuditActor
    {
        public const string Web = "web";
        public const string Bot = "bot";
        public const string Api = "api";
        public const string System = "system";

        public AuditActor(int? userId, string source)
        {
            UserId = userId;
            Source = string.IsNullOrWhiteSpace(source) ? Web : source;
        }

        public int? UserId { get; }
        public string Source { get; }

        /// <summary>Joriy HTTP so'rov foydalanuvchisi (bo'lmasa — system).</summary>
        public static AuditActor FromHttpContext()
        {
            string name = HttpContext.Current?.User?.Identity?.IsAuthenticated == true
                ? HttpContext.Current.User.Identity.Name
                : null;
            return name == null ? new AuditActor(null, System) : new AuditActor(RoleHelper.GetUserId(name), Web);
        }
    }

    /// <summary>
    /// Distributsiya moduli konteksti: `dst` sxemasi, EF6 Code-First (migratsiyasiz — sxema
    /// Database/Migrations/NNN_DST_*.sql bilan boshqariladi). EDMX'ga kirmaydi.
    ///
    /// SaveChanges:
    ///  - <see cref="IAuditable"/> entity o'zgarishlarini dst.AUDIT_TRAIL ga yozadi (U — har maydon eski/yangi,
    ///    I/D — butun qator JSON) shu tranzaksiyaning o'zida;
    ///  - <see cref="DstDocument"/>: Draft bo'lmagan hujjatni o'chirish va bekor qilingan hujjatni tahrirlash taqiqlanadi.
    /// Ombor/moliya bilan bitta tranzaksiya kerak bo'lsa — <see cref="DistributionUnitOfWork"/>.
    /// </summary>
    public class DistributionDbContext : DbContext
    {
        public const string Schema = "dst";

        static DistributionDbContext()
        {
            Database.SetInitializer<DistributionDbContext>(null);
        }

        /// <summary>Joriy tenant bazasiga o'z ulanishi bilan.</summary>
        public DistributionDbContext()
            : base(tahsinERP.Configuration.DbConfig.GetSqlConnectionString())
        {
        }

        /// <summary>Mavjud ulanish ustida (<see cref="DistributionUnitOfWork"/> uchun).</summary>
        public DistributionDbContext(DbConnection existingConnection, bool contextOwnsConnection)
            : base(existingConnection, contextOwnsConnection)
        {
        }

        public virtual DbSet<AuditTrailEntry> AuditTrail { get; set; }
        public virtual DbSet<UserScope> UserScopes { get; set; }
        public virtual DbSet<UserSetting> UserSettings { get; set; }

        // ma'lumotnomalar (S1)
        public virtual DbSet<Territory> Territories { get; set; }
        public virtual DbSet<CustomerProfile> CustomerProfiles { get; set; }
        public virtual DbSet<Brand> Brands { get; set; }
        public virtual DbSet<Manufacturer> Manufacturers { get; set; }
        public virtual DbSet<SubCategory> SubCategories { get; set; }
        public virtual DbSet<ProductProfile> ProductProfiles { get; set; }
        public virtual DbSet<ProductUnit> ProductUnits { get; set; }
        public virtual DbSet<ProductBarcode> ProductBarcodes { get; set; }
        public virtual DbSet<PriceType> PriceTypes { get; set; }
        public virtual DbSet<PriceListItem> PriceListItems { get; set; }
        public virtual DbSet<CustomerPrice> CustomerPrices { get; set; }
        public virtual DbSet<SalesTeam> SalesTeams { get; set; }
        public virtual DbSet<SalesAgent> SalesAgents { get; set; }
        public virtual DbSet<DiscountLimit> DiscountLimits { get; set; }
        public virtual DbSet<Reason> Reasons { get; set; }
        public virtual DbSet<WarehouseSetting> WarehouseSettings { get; set; }
        public virtual DbSet<ProductStockLimit> ProductStockLimits { get; set; }

        // buyurtmalar (S2)
        public virtual DbSet<DstOrder> Orders { get; set; }
        public virtual DbSet<DstOrderLine> OrderLines { get; set; }
        public virtual DbSet<DstOrderStatusLog> OrderStatusLog { get; set; }
        public virtual DbSet<StockReservation> StockReservations { get; set; }
        public virtual DbSet<DstShipment> Shipments { get; set; }
        public virtual DbSet<DstShipmentLine> ShipmentLines { get; set; }
        public virtual DbSet<CustomerLedgerEntry> CustomerLedger { get; set; }
        public virtual DbSet<DstSetting> Settings { get; set; }

        // pul va qaytarish (S3)
        public virtual DbSet<DstPayment> Payments { get; set; }
        public virtual DbSet<CashHandover> CashHandovers { get; set; }
        public virtual DbSet<DstReturn> Returns { get; set; }
        public virtual DbSet<DstReturnLine> ReturnLines { get; set; }

        // bot va agent ish kuni (S4)
        public virtual DbSet<TelegramLinkCode> TelegramLinkCodes { get; set; }
        public virtual DbSet<TelegramAccount> TelegramAccounts { get; set; }
        public virtual DbSet<DstNotification> Notifications { get; set; }
        public virtual DbSet<AgentDay> AgentDays { get; set; }
        public virtual DbSet<Visit> Visits { get; set; }

        // sayt-katalog (021_SITE, Areas/Site)
        public virtual DbSet<Site.Data.SiteSetting> SiteSettings { get; set; }
        public virtual DbSet<Site.Data.SiteDomain> SiteDomains { get; set; }
        public virtual DbSet<Site.Data.SiteMedia> SiteMedia { get; set; }
        public virtual DbSet<Site.Data.SiteCategoryContent> SiteCategories { get; set; }
        public virtual DbSet<Site.Data.SiteProductContent> SiteProducts { get; set; }
        public virtual DbSet<Site.Data.SiteProductAttribute> SiteProductAttributes { get; set; }
        public virtual DbSet<Site.Data.SiteProductMedia> SiteProductMedia { get; set; }
        public virtual DbSet<Site.Data.SitePage> SitePages { get; set; }
        public virtual DbSet<Site.Data.SiteBanner> SiteBanners { get; set; }
        public virtual DbSet<Site.Data.SiteLead> SiteLeads { get; set; }

        // dbo jadvallari — faqat o'qish (join uchun)
        public virtual DbSet<FinAccountRef> FinAccounts { get; set; }
        public virtual DbSet<StockRef> Stocks { get; set; }
        public virtual DbSet<CustomerRef> Customers { get; set; }
        public virtual DbSet<ProductRef> Products { get; set; }
        public virtual DbSet<UnitRef> Units { get; set; }
        public virtual DbSet<CategoryRef> Categories { get; set; }
        public virtual DbSet<UserRef> Users { get; set; }
        public virtual DbSet<WarehouseRef> Warehouses { get; set; }
        public virtual DbSet<RoleRef> Roles { get; set; }

        private AuditActor _actor;

        /// <summary>Jurnal uchun foydalanuvchi; berilmasa joriy HTTP so'rovdan olinadi.</summary>
        public AuditActor Actor
        {
            get { return _actor ?? (_actor = AuditActor.FromHttpContext()); }
            set { _actor = value; }
        }

        /// <summary>Test va servislar vaqtni boshqarishi uchun.</summary>
        public Func<DateTime> Clock { get; set; } = () => DateTime.Now;

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(Schema);

            // EF6 sukut bo'yicha DateTime -> datetime, decimal -> (18,2): ustunlar aniq turlari bilan
            modelBuilder.Properties<DateTime>()
                .Where(p => !p.IsDefined(typeof(System.ComponentModel.DataAnnotations.Schema.ColumnAttribute), true))
                .Configure(c => c.HasColumnType("datetime2"));

            // varchar ustunlar (nvarchar parametr indeksni buzmasin)
            modelBuilder.Properties<string>()
                .Where(p => typeof(IReadOnlyRef).IsAssignableFrom(p.DeclaringType) && p.DeclaringType != typeof(CategoryRef))
                .Configure(c => c.IsUnicode(false));

            var audit = modelBuilder.Entity<AuditTrailEntry>();
            audit.Property(e => e.Action).IsFixedLength().IsUnicode(false);
            audit.Property(e => e.EntityName).IsUnicode(false);
            audit.Property(e => e.EntityKey).IsUnicode(false);
            audit.Property(e => e.FieldName).IsUnicode(false);
            audit.Property(e => e.Source).IsUnicode(false);
            modelBuilder.Entity<UserScope>().Property(e => e.ScopeType).IsUnicode(false);
            modelBuilder.Entity<UserSetting>().Property(e => e.Language).IsUnicode(false);

            var cp = modelBuilder.Entity<CustomerProfile>();
            cp.Property(e => e.INN).IsUnicode(false);
            cp.Property(e => e.Phone2).IsUnicode(false);
            cp.Property(e => e.PhotoKey).IsUnicode(false);
            cp.Property(e => e.PointType).IsUnicode(false);
            cp.Property(e => e.Category).IsUnicode(false).IsFixedLength();
            cp.Property(e => e.CreatedVia).IsUnicode(false);
            cp.Property(e => e.Lat).HasPrecision(9, 6);
            cp.Property(e => e.Lng).HasPrecision(9, 6);
            cp.Property(e => e.CreditLimit).HasPrecision(19, 2);

            var pp = modelBuilder.Entity<ProductProfile>();
            pp.Property(e => e.BaseWeightKg).HasPrecision(18, 4);
            pp.Property(e => e.BaseVolumeM3).HasPrecision(18, 6);

            var pu = modelBuilder.Entity<ProductUnit>();
            pu.Property(e => e.Factor).HasPrecision(18, 6);
            pu.Property(e => e.WeightKg).HasPrecision(18, 4);
            pu.Property(e => e.VolumeM3).HasPrecision(18, 6);

            modelBuilder.Entity<ProductBarcode>().Property(e => e.Barcode).IsUnicode(false);

            modelBuilder.Entity<Site.Data.SiteSetting>().Property(e => e.Key).IsUnicode(false);
            modelBuilder.Entity<Site.Data.SiteDomain>().Property(e => e.Host).IsUnicode(false);
            var sm = modelBuilder.Entity<Site.Data.SiteMedia>();
            sm.Property(e => e.ContentType).IsUnicode(false);
            sm.Property(e => e.Hash).IsUnicode(false).IsFixedLength();
            modelBuilder.Entity<Site.Data.SiteCategoryContent>().Property(e => e.Slug).IsUnicode(false);
            modelBuilder.Entity<Site.Data.SiteProductContent>().Property(e => e.Slug).IsUnicode(false);
            modelBuilder.Entity<Site.Data.SiteProductContent>().Property(e => e.Badges).IsUnicode(false);
            modelBuilder.Entity<Site.Data.SitePage>().Property(e => e.Slug).IsUnicode(false);
            var lead = modelBuilder.Entity<Site.Data.SiteLead>();
            lead.Property(e => e.Phone).IsUnicode(false);
            lead.Property(e => e.INN).IsUnicode(false);
            lead.Property(e => e.Language).IsUnicode(false);
            lead.Property(e => e.Host).IsUnicode(false);
            modelBuilder.Entity<PriceListItem>().Property(e => e.Price).HasPrecision(19, 2);
            modelBuilder.Entity<PriceListItem>().Property(e => e.MinPrice).HasPrecision(19, 2);
            modelBuilder.Entity<CustomerPrice>().Property(e => e.Price).HasPrecision(19, 2);
            modelBuilder.Entity<SalesAgent>().Property(e => e.Phone).IsUnicode(false);
            modelBuilder.Entity<DiscountLimit>().Property(e => e.MaxPercent).HasPrecision(5, 2);
            modelBuilder.Entity<Reason>().Property(e => e.Type).IsUnicode(false);
            modelBuilder.Entity<ProductStockLimit>().Property(e => e.MinQty).HasPrecision(18, 4);

            var o = modelBuilder.Entity<DstOrder>();
            o.Property(e => e.Subtotal).HasPrecision(19, 2);
            o.Property(e => e.DiscountPct).HasPrecision(5, 2);
            o.Property(e => e.DiscountSum).HasPrecision(19, 2);
            o.Property(e => e.Total).HasPrecision(19, 2);
            o.Property(e => e.ShippedTotal).HasPrecision(19, 2);
            o.Property(e => e.WeightKg).HasPrecision(18, 3);
            o.Property(e => e.VolumeM3).HasPrecision(18, 4);
            o.Property(e => e.Source).IsUnicode(false);

            var ol = modelBuilder.Entity<DstOrderLine>();
            ol.Property(e => e.Factor).HasPrecision(18, 6);
            ol.Property(e => e.Qty).HasPrecision(18, 4);
            ol.Property(e => e.QtyBase).HasPrecision(18, 4);
            ol.Property(e => e.UnitPrice).HasPrecision(19, 2);
            ol.Property(e => e.MinUnitPrice).HasPrecision(19, 2);
            ol.Property(e => e.DiscountPct).HasPrecision(5, 2);
            ol.Property(e => e.DiscountSum).HasPrecision(19, 2);
            ol.Property(e => e.Amount).HasPrecision(19, 2);
            ol.Property(e => e.QtyShippedBase).HasPrecision(18, 4);

            modelBuilder.Entity<StockReservation>().Property(e => e.QtyBase).HasPrecision(18, 4);
            modelBuilder.Entity<DstShipment>().Property(e => e.Total).HasPrecision(19, 2);
            modelBuilder.Entity<DstShipment>().Property(e => e.CostTotal).HasPrecision(19, 2);
            var sl = modelBuilder.Entity<DstShipmentLine>();
            sl.Property(e => e.QtyBase).HasPrecision(18, 4);
            sl.Property(e => e.Amount).HasPrecision(19, 2);
            sl.Property(e => e.CostAmount).HasPrecision(19, 2);
            var cl = modelBuilder.Entity<CustomerLedgerEntry>();
            cl.Property(e => e.Debit).HasPrecision(19, 2);
            cl.Property(e => e.Credit).HasPrecision(19, 2);
            cl.Property(e => e.DocType).IsUnicode(false);
            modelBuilder.Entity<DstSetting>().Property(e => e.Key).IsUnicode(false);
            modelBuilder.Entity<StockRef>().Property(e => e.Amount).HasPrecision(18, 6);

            var pay = modelBuilder.Entity<DstPayment>();
            pay.Property(e => e.Amount).HasPrecision(19, 2);
            pay.Property(e => e.Method).IsUnicode(false);
            pay.Property(e => e.Source).IsUnicode(false);
            var ho = modelBuilder.Entity<CashHandover>();
            ho.Property(e => e.AmountDeclared).HasPrecision(19, 2);
            ho.Property(e => e.AmountReceived).HasPrecision(19, 2);
            var ret = modelBuilder.Entity<DstReturn>();
            ret.Property(e => e.Total).HasPrecision(19, 2);
            ret.Property(e => e.CostTotal).HasPrecision(19, 2);
            ret.Property(e => e.PhotoKey).IsUnicode(false);
            ret.Property(e => e.Source).IsUnicode(false);
            modelBuilder.Entity<TelegramLinkCode>().Property(e => e.Code).IsUnicode(false);
            modelBuilder.Entity<TelegramLinkCode>().Property(e => e.Role).IsUnicode(false);
            modelBuilder.Entity<TelegramAccount>().Property(e => e.Role).IsUnicode(false);
            modelBuilder.Entity<DstNotification>().Property(e => e.Type).IsUnicode(false);
            modelBuilder.Entity<AgentDay>().Property(e => e.StartLat).HasPrecision(9, 6);
            modelBuilder.Entity<AgentDay>().Property(e => e.StartLng).HasPrecision(9, 6);
            modelBuilder.Entity<AgentDay>().Property(e => e.EndLat).HasPrecision(9, 6);
            modelBuilder.Entity<AgentDay>().Property(e => e.EndLng).HasPrecision(9, 6);
            modelBuilder.Entity<AgentDay>().Property(e => e.SelfieKey).IsUnicode(false);
            var vis = modelBuilder.Entity<Visit>();
            vis.Property(e => e.Lat).HasPrecision(9, 6);
            vis.Property(e => e.Lng).HasPrecision(9, 6);
            vis.Property(e => e.Result).IsUnicode(false);
            vis.Property(e => e.PhotoKey).IsUnicode(false);
            modelBuilder.Entity<DstOrder>().Property(e => e.SignatureKey).IsUnicode(false);

            var rl = modelBuilder.Entity<DstReturnLine>();
            rl.Property(e => e.QtyBase).HasPrecision(18, 4);
            rl.Property(e => e.UnitPrice).HasPrecision(19, 4);
            rl.Property(e => e.Amount).HasPrecision(19, 2);
            rl.Property(e => e.UnitCost).HasPrecision(19, 6);

            base.OnModelCreating(modelBuilder);
        }

        public override int SaveChanges()
        {
            ChangeTracker.DetectChanges();
            GuardReadOnlyRefs();
            GuardDocuments();

            var actor = Actor;
            var now = Clock();
            var correlation = Guid.NewGuid();
            var added = new List<DbEntityEntry>();
            var trail = new List<AuditTrailEntry>();

            foreach (var entry in ChangeTracker.Entries().Where(e => e.Entity is IAuditable).ToList())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        added.Add(entry);
                        break;
                    case EntityState.Modified:
                        trail.AddRange(DescribeUpdate(entry, actor, now, correlation));
                        break;
                    case EntityState.Deleted:
                        trail.Add(NewTrail(entry, AuditTrailEntry.Delete, null, Snapshot(entry.OriginalValues), null, actor, now, correlation));
                        break;
                }
            }

            if (added.Count == 0)
            {
                AuditTrail.AddRange(trail);
                return base.SaveChanges();
            }

            // Yangi qatorlarning kaliti (IDENTITY) faqat saqlangandan keyin ma'lum — ikki bosqich, bitta tranzaksiyada
            DbContextTransaction own = Database.CurrentTransaction == null ? Database.BeginTransaction() : null;
            try
            {
                AuditTrail.AddRange(trail);
                int result = base.SaveChanges();

                AuditTrail.AddRange(added.Select(e =>
                    NewTrail(e, AuditTrailEntry.Insert, null, null, Snapshot(e.CurrentValues), actor, now, correlation)));
                base.SaveChanges();

                own?.Commit();
                return result;
            }
            catch
            {
                own?.Rollback();
                throw;
            }
            finally
            {
                own?.Dispose();
            }
        }

        private void GuardReadOnlyRefs()
        {
            var changed = ChangeTracker.Entries<IReadOnlyRef>()
                .FirstOrDefault(e => e.State != EntityState.Unchanged && e.State != EntityState.Detached);
            if (changed != null)
                throw new InvalidOperationException(
                    "dbo jadvali (" + changed.Entity.GetType().Name + ") distributsiya kontekstidan o'zgartirilmaydi — DBTHSNEntities orqali yozing.");
        }

        private void GuardDocuments()
        {
            foreach (var entry in ChangeTracker.Entries<DstDocument>())
            {
                if (entry.State == EntityState.Deleted &&
                    entry.Property(d => d.Status).OriginalValue != DocumentStatus.Draft)
                {
                    throw new DstValidationException(
                        DstLocalization.T("Error_PostedCannotBeDeleted") + " (" + entry.Entity.DocNo + ")");
                }

                if (entry.State == EntityState.Modified &&
                    entry.Property(d => d.Status).OriginalValue == DocumentStatus.Cancelled)
                {
                    throw new DstValidationException("Bekor qilingan hujjatni o'zgartirib bo'lmaydi (" + entry.Entity.DocNo + ").");
                }
            }
        }

        private static IEnumerable<AuditTrailEntry> DescribeUpdate(DbEntityEntry entry, AuditActor actor, DateTime now, Guid correlation)
        {
            foreach (var name in entry.CurrentValues.PropertyNames)
            {
                var property = entry.Property(name);
                if (!property.IsModified) continue;

                string oldValue = Format(entry.OriginalValues[name]);
                string newValue = Format(entry.CurrentValues[name]);
                if (string.Equals(oldValue, newValue, StringComparison.Ordinal)) continue;

                yield return NewTrail(entry, AuditTrailEntry.Update, name, oldValue, newValue, actor, now, correlation);
            }
        }

        private static AuditTrailEntry NewTrail(DbEntityEntry entry, string action, string field, string oldValue,
            string newValue, AuditActor actor, DateTime now, Guid correlation)
        {
            return new AuditTrailEntry
            {
                CorrelationId = correlation,
                EntityName = ObjectContextTypeName(entry.Entity),
                EntityKey = ((IAuditable)entry.Entity).AuditKey ?? "",
                Action = action,
                FieldName = field,
                OldValue = oldValue,
                NewValue = newValue,
                UserID = actor.UserId,
                Source = actor.Source,
                ChangedAt = now
            };
        }

        // EF proxy turi o'rniga asl entity nomi
        private static string ObjectContextTypeName(object entity)
        {
            var type = entity.GetType();
            if (type.Namespace == "System.Data.Entity.DynamicProxies" && type.BaseType != null)
                type = type.BaseType;
            return type.Name;
        }

        private static string Snapshot(DbPropertyValues values)
        {
            var dict = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var name in values.PropertyNames)
                dict[name] = Format(values[name]);
            return JsonConvert.SerializeObject(dict);
        }

        /// <summary>Qiymatni madaniyatdan mustaqil matnga (jurnalda bir xil ko'rinishi uchun).</summary>
        public static string Format(object value)
        {
            switch (value)
            {
                case null:
                    return null;
                case DateTime dt:
                    return dt.ToString(dt.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                case decimal d:
                    return d.ToString(CultureInfo.InvariantCulture);
                case double dbl:
                    return dbl.ToString("R", CultureInfo.InvariantCulture);
                case Enum e:
                    return e.ToString();
                case IFormattable f:
                    return f.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return value.ToString();
            }
        }
    }
}
