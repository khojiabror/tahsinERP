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
            modelBuilder.Entity<AuditTrailEntry>().Property(e => e.Action).IsFixedLength().IsUnicode(false);
            modelBuilder.Entity<AuditTrailEntry>().Property(e => e.EntityName).IsUnicode(false);
            modelBuilder.Entity<AuditTrailEntry>().Property(e => e.EntityKey).IsUnicode(false);
            modelBuilder.Entity<AuditTrailEntry>().Property(e => e.FieldName).IsUnicode(false);
            modelBuilder.Entity<AuditTrailEntry>().Property(e => e.Source).IsUnicode(false);
            modelBuilder.Entity<UserScope>().Property(e => e.ScopeType).IsUnicode(false);
            modelBuilder.Entity<UserSetting>().Property(e => e.Language).IsUnicode(false);
            base.OnModelCreating(modelBuilder);
        }

        public override int SaveChanges()
        {
            ChangeTracker.DetectChanges();
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
