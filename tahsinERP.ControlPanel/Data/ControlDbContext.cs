using Microsoft.EntityFrameworkCore;

namespace tahsinERP.ControlPanel.Data;

public class ControlDbContext(DbContextOptions<ControlDbContext> options) : DbContext(options)
{
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Server> Servers => Set<Server>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();
    public DbSet<InvoiceSequence> InvoiceSequences => Set<InvoiceSequence>();
    public DbSet<ApiNonce> ApiNonces => Set<ApiNonce>();
    public DbSet<ProvisioningJob> ProvisioningJobs => Set<ProvisioningJob>();
    public DbSet<UsageState> UsageStates => Set<UsageState>();
    public DbSet<UsageUser> UsageUsers => Set<UsageUser>();
    public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();
    public DbSet<UsageDailyModule> UsageDailyModules => Set<UsageDailyModule>();
    public DbSet<UsageDailyUser> UsageDailyUsers => Set<UsageDailyUser>();
    public DbSet<UsageDailyDoc> UsageDailyDocs => Set<UsageDailyDoc>();
    public DbSet<PortalUser> PortalUsers => Set<PortalUser>();
    public DbSet<PortalInvite> PortalInvites => Set<PortalInvite>();
    public DbSet<PortalOtp> PortalOtps => Set<PortalOtp>();
    public DbSet<PortalRequest> PortalRequests => Set<PortalRequest>();
    public DbSet<BankImport> BankImports => Set<BankImport>();
    public DbSet<BankImportLine> BankImportLines => Set<BankImportLine>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<StaffUser>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.PasswordHash).HasMaxLength(200);
            e.Property(x => x.TotpSecretProtected).HasMaxLength(500);
            e.Property(x => x.SecurityStamp).HasMaxLength(64);
        });

        b.Entity<Plan>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Modules).HasMaxLength(500);
            e.Property(x => x.VatRatePercent).HasPrecision(5, 2);
            e.HasData(
                new Plan { Id = 1, Code = PlanCodes.Start, Name = "Start", MaxUsers = 5, Modules = "Srm,Crm,Tasks", VatRatePercent = 12m, SortOrder = 1 },
                new Plan { Id = 2, Code = PlanCodes.Biznes, Name = "Biznes", MaxUsers = 20, Modules = "Srm,Crm,Tasks,Mes,Engineering,Finance", VatRatePercent = 12m, SortOrder = 2 },
                new Plan { Id = 3, Code = PlanCodes.Korp, Name = "Korporativ", MaxUsers = 50, Modules = "Srm,Crm,Tasks,Mes,Engineering,Finance,Hr,Logistics,Docs,Distribution", VatRatePercent = 12m, SortOrder = 3 },
                new Plan { Id = 4, Code = PlanCodes.Trial, Name = "Sinov (14 kun)", MaxUsers = 5, Modules = "Srm,Crm,Tasks,Mes,Engineering,Finance,Hr,Logistics,Docs,Distribution", VatRatePercent = 12m, TrialDays = 14, PriceMonthTiyin = 0, PriceYearTiyin = 0, SortOrder = 4 },
                // Qo'shimcha xizmat: narx ochiq savol (Tariflar sahifasida belgilanadi), 02_WEBSITE_CATALOG.md 9-bo'lim.
                new Plan { Id = 5, Code = PlanCodes.Site, Name = "Sayt-katalog", MaxUsers = 0, Modules = "Website", VatRatePercent = 12m, IsAddOn = true, SortOrder = 10 },
                // Distributsiya paketi (Distribution/00_PLAN.md 1, 4-faza): BIZNES bilan bir pog'ona (20 foydalanuvchi), MES/BOM'siz.
                // Narx ochiq. Asosiy zanjirdan tashqari — tarif so'rovi turi modul to'plami bo'yicha aniqlanadi (PortalRequestService.Classify).
                new Plan { Id = 6, Code = PlanCodes.Dist, Name = "Distributsiya", MaxUsers = 20, Modules = "Srm,Crm,Tasks,Finance,Distribution", VatRatePercent = 12m, SortOrder = 5 });
        });

        b.Entity<Server>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Host).HasMaxLength(253);
            e.Property(x => x.Region).HasMaxLength(50);
            e.Property(x => x.SqlInstance).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.AgentSecretProtected).HasMaxLength(500);
            e.Property(x => x.AgentVersion).HasMaxLength(50);
        });

        b.Entity<Tenant>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.DbName).IsUnique().HasFilter("[DbName] IS NOT NULL");
            e.HasIndex(x => x.InstallationId).IsUnique().HasFilter("[InstallationId] IS NOT NULL");
            e.HasIndex(x => x.RegTokenHash).IsUnique().HasFilter("[RegTokenHash] IS NOT NULL");
            e.Property(x => x.Slug).HasMaxLength(63);
            e.Property(x => x.LegalName).HasMaxLength(300);
            e.Property(x => x.Inn).HasMaxLength(14);
            e.Property(x => x.ContactName).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.TelegramChatId).HasMaxLength(50);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.DbName).HasMaxLength(128);
            e.Property(x => x.InstallationId).HasMaxLength(36);
            e.Property(x => x.RegTokenHash).HasMaxLength(64);
            e.Property(x => x.ApiSecretProtected).HasMaxLength(500);
            e.Property(x => x.LastVersion).HasMaxLength(50);
            e.Property(x => x.LastHost).HasMaxLength(253);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.ArchiveLocation).HasMaxLength(500);
            e.Property(x => x.OnlineCheck).HasDefaultValue(OnlineCheckMode.Optional).HasSentinel((OnlineCheckMode)0);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Server).WithMany().HasForeignKey(x => x.ServerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Subscription>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasOne(x => x.Tenant).WithMany(t => t.Subscriptions).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        b.Entity<Invoice>(e =>
        {
            e.HasIndex(x => x.Number).IsUnique().HasFilter("[Number] IS NOT NULL");
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.Property(x => x.Number).HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.Property(x => x.DidoxId).HasMaxLength(100);
            e.Property(x => x.DidoxStatus).HasMaxLength(100);
            e.Property(x => x.DidoxError).HasMaxLength(1000);
            e.HasIndex(x => x.DidoxId).HasFilter("[DidoxId] IS NOT NULL");
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Ignore(x => x.RemainingTiyin);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Payments).WithOne(p => p.Invoice!).HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<InvoiceLine>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.VatRatePercent).HasPrecision(5, 2);
        });

        b.Entity<Payment>(e =>
        {
            e.Property(x => x.Reference).HasMaxLength(100);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.Property(x => x.ReverseReason).HasMaxLength(1000);
            e.HasIndex(x => x.PaidAt);
        });

        b.Entity<License>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.IssuedAtUtc });
            e.HasIndex(x => x.InstallationId);
            e.Property(x => x.InstallationId).HasMaxLength(36);
            e.Property(x => x.KeyId).HasMaxLength(20);
            e.Property(x => x.LicenseKey).HasMaxLength(2000);
            e.Property(x => x.ETag).HasMaxLength(64);
            e.Property(x => x.Edition).HasMaxLength(20);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.RevokeReason).HasMaxLength(1000);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Notification>(e =>
        {
            e.HasIndex(x => x.DedupKey).IsUnique();
            e.Property(x => x.Template).HasMaxLength(50);
            e.Property(x => x.DedupKey).HasMaxLength(200);
            e.Property(x => x.Recipient).HasMaxLength(100);
            e.Property(x => x.Error).HasMaxLength(1000);
        });

        b.Entity<AuditEntry>(e =>
        {
            e.ToTable("AuditLog");
            e.HasIndex(x => x.AtUtc);
            e.HasIndex(x => new { x.Entity, x.EntityId });
            e.HasIndex(x => x.PortalUserId).HasFilter("[PortalUserId] IS NOT NULL");
            e.Property(x => x.Action).HasMaxLength(100);
            e.Property(x => x.Entity).HasMaxLength(50);
            e.Property(x => x.EntityId).HasMaxLength(50);
            e.Property(x => x.Ip).HasMaxLength(64);
        });

        b.Entity<ApiNonce>(e =>
        {
            e.HasIndex(x => new { x.InstallationId, x.Nonce }).IsUnique();
            e.HasIndex(x => x.ExpiresUtc);
            e.Property(x => x.InstallationId).HasMaxLength(36);
            e.Property(x => x.Nonce).HasMaxLength(32);
        });

        b.Entity<ProvisioningJob>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Id });
            e.HasIndex(x => new { x.Status, x.NotBeforeUtc });
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
            e.Property(x => x.Payload).HasMaxLength(4000);
            e.Property(x => x.SecretsProtected).HasMaxLength(2000);
            e.Property(x => x.IdempotencyKey).HasMaxLength(100);
            e.Property(x => x.LeaseToken).HasMaxLength(64);
            e.Property(x => x.Result).HasMaxLength(4000);
            e.Property(x => x.Error).HasMaxLength(2000);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Ignore(x => x.IsActive);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<UsageState>(e =>
        {
            e.HasKey(x => x.TenantId);
            e.Property(x => x.TenantId).ValueGeneratedNever();
            e.Property(x => x.Error).HasMaxLength(1000);
            e.HasOne<Tenant>().WithOne().HasForeignKey<UsageState>(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UsageUser>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
            e.Property(x => x.Login).HasMaxLength(50);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UsageEvent>(e =>
        {
            // Demo reset'dan keyin ID'lar qaytadan boshlanadi — vaqt bilan birga noyob.
            e.HasIndex(x => new { x.TenantId, x.Kind, x.SourceId, x.AtUtc }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.AtUtc });
            e.HasIndex(x => x.AtUtc);
            e.Property(x => x.Module).HasMaxLength(50);
            e.Property(x => x.Controller).HasMaxLength(50);
            e.Property(x => x.Action).HasMaxLength(300);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UsageDailyModule>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Date, x.Module, x.Controller, x.ActionType }).IsUnique();
            e.HasIndex(x => new { x.Date, x.Module });
            e.Property(x => x.Module).HasMaxLength(20);
            e.Property(x => x.Controller).HasMaxLength(50);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UsageDailyUser>(e =>
        {
            e.HasKey(x => new { x.TenantId, x.Date, x.UserId });
            e.HasIndex(x => x.Date);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UsageDailyDoc>(e =>
        {
            e.HasKey(x => new { x.TenantId, x.Date, x.DocType });
            e.HasIndex(x => x.Date);
            e.Property(x => x.DocType).HasMaxLength(30);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PortalUser>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
            e.HasIndex(x => x.Phone).IsUnique().HasFilter("[Phone] IS NOT NULL");
            e.HasIndex(x => x.TelegramLinkHash).IsUnique().HasFilter("[TelegramLinkHash] IS NOT NULL");
            e.HasIndex(x => x.TenantId);
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.PasswordHash).HasMaxLength(200);
            e.Property(x => x.TelegramChatId).HasMaxLength(50);
            e.Property(x => x.TelegramLinkHash).HasMaxLength(64);
            e.Property(x => x.OtpChannel).HasMaxLength(20);
            e.Property(x => x.SecurityStamp).HasMaxLength(64);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PortalInvite>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.PortalUserId);
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.HasOne(x => x.PortalUser).WithMany().HasForeignKey(x => x.PortalUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PortalOtp>(e =>
        {
            e.HasIndex(x => new { x.PortalUserId, x.CreatedAtUtc });
            e.Property(x => x.Salt).HasMaxLength(32);
            e.Property(x => x.CodeHash).HasMaxLength(64);
            e.Property(x => x.Channel).HasMaxLength(20);
            e.Property(x => x.Ip).HasMaxLength(64);
            e.HasOne<PortalUser>().WithMany().HasForeignKey(x => x.PortalUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PortalRequest>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => new { x.Status, x.Id });
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.Property(x => x.DecisionReason).HasMaxLength(1000);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PortalUser).WithMany().HasForeignKey(x => x.PortalUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Subscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Subscription>().WithMany().HasForeignKey(x => x.CurrentSubscriptionId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<BankImport>(e =>
        {
            e.HasIndex(x => x.UploadedAtUtc);
            e.Property(x => x.FileName).HasMaxLength(260);
            e.Property(x => x.FileHash).HasMaxLength(64);
            e.Property(x => x.Account).HasMaxLength(200);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.ImportId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<BankImportLine>(e =>
        {
            e.HasIndex(x => new { x.ImportId, x.LineNo }).IsUnique();
            e.HasIndex(x => x.DedupKey);
            // Idempotentlik: bitta bank tranzaksiyasi faqat bir marta qo'llanadi (parallel xodimlar ham).
            e.HasIndex(x => x.AppliedKey).IsUnique().HasFilter("[AppliedKey] IS NOT NULL");
            e.Property(x => x.DocNumber).HasMaxLength(50);
            e.Property(x => x.PayerName).HasMaxLength(300);
            e.Property(x => x.PayerInn).HasMaxLength(14);
            e.Property(x => x.PayerAccount).HasMaxLength(34);
            e.Property(x => x.Purpose).HasMaxLength(1000);
            e.Property(x => x.DedupKey).HasMaxLength(64);
            e.Property(x => x.AppliedKey).HasMaxLength(64);
            e.Property(x => x.MatchNote).HasMaxLength(300);
            e.Property(x => x.Error).HasMaxLength(1000);
            e.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<InvoiceSequence>(e =>
        {
            e.HasKey(x => x.Year);
            e.Property(x => x.Year).ValueGeneratedNever();
        });
    }
}
