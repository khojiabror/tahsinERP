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
    public DbSet<AppRelease> AppReleases => Set<AppRelease>();
    public DbSet<UpdateJob> UpdateJobs => Set<UpdateJob>();
    public DbSet<ServerInventory> ServerInventories => Set<ServerInventory>();
    public DbSet<ServerHealthDay> ServerHealthDays => Set<ServerHealthDay>();
    public DbSet<DemoRequest> DemoRequests => Set<DemoRequest>();
    public DbSet<ErpDeveloper> ErpDevelopers => Set<ErpDeveloper>();
    public DbSet<TenantSiteDomain> SiteDomains => Set<TenantSiteDomain>();
    public DbSet<EconomicsSettings> EconomicsSettings => Set<EconomicsSettings>();
    public DbSet<PlannedCost> PlannedCosts => Set<PlannedCost>();
    public DbSet<ModuleWeight> ModuleWeights => Set<ModuleWeight>();
    public DbSet<PlanEconomics> PlanEconomics => Set<PlanEconomics>();
    public DbSet<Competitor> Competitors => Set<Competitor>();
    public DbSet<Expense> Expenses => Set<Expense>();

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

        b.Entity<ErpDeveloper>(e =>
        {
            e.HasIndex(x => x.Login).IsUnique();
            e.Property(x => x.Login).HasMaxLength(30);
            e.Property(x => x.FullName).HasMaxLength(50);
            e.Property(x => x.PasswordHash).HasMaxLength(200);
            e.Property(x => x.LastTenant).HasMaxLength(100);
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
            e.Property(x => x.Modules).HasMaxLength(500);
            e.Property(x => x.OnlineCheck).HasDefaultValue(OnlineCheckMode.Optional).HasSentinel((OnlineCheckMode)0);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Server).WithMany().HasForeignKey(x => x.ServerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Subscription>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasOne(x => x.Tenant).WithMany(t => t.Subscriptions).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Modules).HasMaxLength(500);
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

        b.Entity<AppRelease>(e =>
        {
            e.HasIndex(x => x.Version).IsUnique();
            e.Property(x => x.Version).HasMaxLength(32);
            e.Property(x => x.Tag).HasMaxLength(64);
            e.Property(x => x.Commit).HasMaxLength(64);
            e.Property(x => x.Signature).HasMaxLength(1000);
            e.Property(x => x.VerifyError).HasMaxLength(500);
            e.Property(x => x.AssetsJson).HasMaxLength(2000);
            e.Property(x => x.Apps).HasMaxLength(200);
        });

        b.Entity<UpdateJob>(e =>
        {
            e.HasIndex(x => new { x.ServerId, x.Status });
            e.Property(x => x.App).HasMaxLength(20);
            e.Property(x => x.Slug).HasMaxLength(63);
            e.Property(x => x.DbName).HasMaxLength(128);
            e.Property(x => x.Version).HasMaxLength(32);
            e.Property(x => x.FromVersion).HasMaxLength(32);
            e.Property(x => x.InstalledVersion).HasMaxLength(32);
            e.Property(x => x.LeaseToken).HasMaxLength(64);
            e.Property(x => x.Stage).HasMaxLength(200);
            e.Property(x => x.Error).HasMaxLength(2000);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Ignore(x => x.IsActive);
            e.HasOne(x => x.Server).WithMany().HasForeignKey(x => x.ServerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ServerInventory>(e =>
        {
            e.HasKey(x => x.ServerId);
            e.Property(x => x.ServerId).ValueGeneratedNever();
            e.HasOne<Server>().WithMany().HasForeignKey(x => x.ServerId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.UpdaterVersion).HasMaxLength(32);
        });

        b.Entity<ServerHealthDay>(e =>
        {
            e.HasIndex(x => new { x.ServerId, x.Day }).IsUnique();
            e.HasOne<Server>().WithMany().HasForeignKey(x => x.ServerId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.DisksJson).HasMaxLength(4000);
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

        b.Entity<TenantSiteDomain>(e =>
        {
            e.ToTable("SiteDomains");
            e.HasIndex(x => x.TenantId).IsUnique();
            e.HasIndex(x => x.Host).IsUnique();
            e.Property(x => x.Host).HasMaxLength(253);
            e.Property(x => x.PendingRemoval).HasMaxLength(1000);
            e.Property(x => x.DnsResult).HasMaxLength(500);
            e.Property(x => x.Error).HasMaxLength(2000);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DemoRequest>(e =>
        {
            e.HasIndex(x => new { x.Status, x.Id });
            e.HasIndex(x => x.Phone);
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.Company).HasMaxLength(300);
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Industry).HasMaxLength(30);
            e.Property(x => x.CompanySize).HasMaxLength(20);
            e.Property(x => x.Modules).HasMaxLength(200);
            e.Property(x => x.Plan).HasMaxLength(20);
            e.Property(x => x.Message).HasMaxLength(2000);
            e.Property(x => x.Lang).HasMaxLength(5);
            e.Property(x => x.Ip).HasMaxLength(64);
            e.Property(x => x.UserAgent).HasMaxLength(400);
            e.Property(x => x.StaffNotifyError).HasMaxLength(500);
            e.Property(x => x.StaffNote).HasMaxLength(1000);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<InvoiceSequence>(e =>
        {
            e.HasKey(x => x.Year);
            e.Property(x => x.Year).ValueGeneratedNever();
        });

        ConfigureEconomics(b);
    }

    /// <summary>
    /// Iqtisodiyot jadvallari va boshlang'ich reja (tahsinERP_Tannarx_Narx.xlsx, 2026-10): so'm = $ × 12 700.
    /// Raqamlar keyin «Iqtisodiyot → Sozlamalar»da o'zgartiriladi.
    /// </summary>
    private static void ConfigureEconomics(ModelBuilder b)
    {
        const long Som = 100; // tiyin
        b.Entity<EconomicsSettings>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.UsdRate).HasPrecision(18, 2);
            e.Property(x => x.PayrollTaxPercent).HasPrecision(5, 2);
            e.Property(x => x.TurnoverTaxPercent).HasPrecision(5, 2);
            e.Property(x => x.BankFeePercent).HasPrecision(5, 2);
            e.Property(x => x.ProfitTaxPercent).HasPrecision(5, 2);
            e.HasData(new EconomicsSettings
            {
                Id = 1, UsdRate = 12700m, PayrollTaxPercent = 12m,
                ServerPriceTiyin = 508_000 * Som, FirmsPerServer = 8, MinServers = 1,
                PerFirmTiyin = 12_700 * Som, PlatformTiyin = 25_400 * Som,
                SupportSalaryTiyin = 6_350_000 * Som, FirmsPerSupport = 15, MinSupport = 1,
                TurnoverTaxPercent = 0m, BankFeePercent = 1m, ProfitTaxPercent = 15m
            });
        });

        b.Entity<PlannedCost>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasData(
                new PlannedCost { Id = 1, Kind = PlannedCostKind.Staff, Name = "Asoschi / rahbar", Count = 1, AmountTiyin = 0, Category = CostCategory.Admin, SortOrder = 1 },
                new PlannedCost { Id = 2, Kind = PlannedCostKind.Staff, Name = "Dasturchi (senior)", Count = 1, AmountTiyin = 0, Category = CostCategory.Development, SortOrder = 2 },
                new PlannedCost { Id = 3, Kind = PlannedCostKind.Staff, Name = "Dasturchi (junior)", Count = 1, AmountTiyin = 0, Category = CostCategory.Development, SortOrder = 3 },
                new PlannedCost { Id = 4, Kind = PlannedCostKind.Staff, Name = "Sotuv menejeri", Count = 1, AmountTiyin = 6_350_000 * Som, Category = CostCategory.Sales, SortOrder = 4 },
                new PlannedCost { Id = 5, Kind = PlannedCostKind.Other, Name = "AI vositalar (Claude va b.)", AmountTiyin = 1_270_000 * Som, Category = CostCategory.Development, SortOrder = 10 },
                new PlannedCost { Id = 6, Kind = PlannedCostKind.Other, Name = "Marketing (reklama, ko'rgazma)", AmountTiyin = 3_810_000 * Som, Category = CostCategory.Sales, SortOrder = 11 },
                new PlannedCost { Id = 7, Kind = PlannedCostKind.Other, Name = "Internet, telefon, aloqa", AmountTiyin = 381_000 * Som, Category = CostCategory.Admin, SortOrder = 12 },
                new PlannedCost { Id = 8, Kind = PlannedCostKind.Other, Name = "Buxgalter / yuridik xizmat", AmountTiyin = 1_270_000 * Som, Category = CostCategory.Admin, SortOrder = 13 });
        });

        b.Entity<ModuleWeight>(e =>
        {
            e.HasKey(x => x.Module);
            e.Property(x => x.Module).HasMaxLength(30);
            e.HasData(
                new ModuleWeight { Module = "Core", Weight = 1 }, new ModuleWeight { Module = "Srm", Weight = 2 },
                new ModuleWeight { Module = "Crm", Weight = 2 }, new ModuleWeight { Module = "Tasks", Weight = 1 },
                new ModuleWeight { Module = "Mes", Weight = 3 }, new ModuleWeight { Module = "Engineering", Weight = 2 },
                new ModuleWeight { Module = "Finance", Weight = 2 }, new ModuleWeight { Module = "Hr", Weight = 2 },
                new ModuleWeight { Module = "Logistics", Weight = 2 }, new ModuleWeight { Module = "Docs", Weight = 1 },
                new ModuleWeight { Module = "Distribution", Weight = 3 });
        });

        b.Entity<PlanEconomics>(e =>
        {
            e.HasKey(x => x.PlanId);
            e.Property(x => x.PlanId).ValueGeneratedNever();
            e.Property(x => x.MarkupPercent).HasPrecision(6, 2);
            e.Property(x => x.TargetGrossMarginPercent).HasPrecision(5, 2);
            e.HasOne(x => x.Plan).WithOne().HasForeignKey<PlanEconomics>(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
            e.HasData(
                new PlanEconomics { PlanId = 1, PlannedTenants = 10, MarkupPercent = 40m, TargetGrossMarginPercent = 45m },
                new PlanEconomics { PlanId = 6, PlannedTenants = 5, MarkupPercent = 40m, TargetGrossMarginPercent = 60m },
                new PlanEconomics { PlanId = 2, PlannedTenants = 3, MarkupPercent = 40m, TargetGrossMarginPercent = 60m },
                new PlanEconomics { PlanId = 3, PlannedTenants = 2, MarkupPercent = 40m, TargetGrossMarginPercent = 60m });
        });

        b.Entity<Competitor>(e =>
        {
            e.Property(x => x.Company).HasMaxLength(100);
            e.Property(x => x.Tariff).HasMaxLength(100);
            e.Property(x => x.PriceMonthUsd).HasPrecision(18, 2);
            e.Property(x => x.MatchPlanCodes).HasMaxLength(200);
            e.Property(x => x.Features).HasMaxLength(200);
            e.Property(x => x.Note).HasMaxLength(500);
            const string all = "Warehouse,Sales,Finance,Production,Hr,~Distribution,Pos";
            // SAVDO paketi (feature/commerce-module) — kod hali main'da bo'lmasa ham satr sifatida bog'lanadi.
            const string Savdo = "SAVDO";
            int i = 0;
            Competitor C(string co, string tf, long? som, decimal? usd, int? users, string? plan, bool inc, string f, string note) =>
                new() { Id = ++i, Company = co, Tariff = tf, PriceMonthTiyin = som * Som, PriceMonthUsd = usd, Users = users, MatchPlanCodes = plan, Include = inc, Features = f, Note = note, SortOrder = i };
            e.HasData(
                C("Bito ERP", "Start", 290_000, null, 3, PlanCodes.Start, true, "Warehouse,Sales,~Finance,~Bot,Pos", "bito.uz/tarif (2026-10): +1 user 90 000 so'm, 3 filial, 14 kun bepul"),
                C("Bito ERP", "Business", 490_000, null, 5, PlanCodes.Dist + "," + Savdo, true, "Warehouse,Sales,Finance,~Production,Distribution,Bot,Pos", "bito.uz/tarif: distributsiya, mijoz boti, cash-flow, oddiy ishlab chiqarish"),
                C("Bito ERP", "Pro", 990_000, null, 10, PlanCodes.Biznes, true, "Warehouse,Sales,Finance,Production,Distribution,Bot,Pos", "bito.uz/tarif: pro ishlab chiqarish, shaxsiy menejer"),
                C("Linko", "Start", 150_000, null, null, PlanCodes.Start, true, "Warehouse,Sales,~Finance,Pos", "linko.uz/pos: do'kon/POS, foydalanuvchi soni ko'rsatilmagan"),
                C("Linko", "Standard", 250_000, null, null, PlanCodes.Start, true, "Warehouse,Sales,Finance,~Hr,Pos", "linko.uz/pos: 58/73 funksiya, aksiya 150 000"),
                C("Linko", "Pro", 500_000, null, null, PlanCodes.Start + "," + Savdo, true, "Warehouse,Sales,Finance,~Hr,Pos", "linko.uz/pos: barcha 73 funksiya"),
                C("Hippo", "Ombor Base", 247_200, null, null, PlanCodes.Start, true, "Warehouse,Sales,Pos", "hippo.uz/pos: 1 ombor, CRM + kassa"),
                C("Hippo", "Ombor Medium", 412_000, null, null, PlanCodes.Start + "," + Savdo, true, "Warehouse,Sales,Pos", "hippo.uz/pos: 3 ombor"),
                C("Hippo", "Ombor Pro", 618_000, null, null, PlanCodes.Start, true, "Warehouse,Sales,Pos", "hippo.uz/pos: 7 ombor. Hippo ERP narxi — so'rov bo'yicha"),
                C("MoySklad", "Bazaviy", 156_800, null, null, PlanCodes.Start, true, "Warehouse,Sales,~Finance,Pos", "moysklad.uz (2026-05): 2 yur. shaxs, 0.5 GB"),
                C("MoySklad", "Prof", 459_200, null, null, PlanCodes.Biznes + "," + Savdo, true, "Warehouse,Sales,Finance,~Production,Pos", "moysklad.uz: 10 yur. shaxs, oddiy ishlab chiqarish"),
                C("Smartup", "So'rov bo'yicha", null, null, null, PlanCodes.Dist + "," + Savdo, false, "Warehouse,Sales,Finance,Distribution,Bot", "smartup24.com: narx ochiq emas"),
                C("Sales Doctor", "Obuna", null, 15m, 1, PlanCodes.Dist, true, "Warehouse,Sales,~Finance,Distribution,Bot", "CEO LinkedIn: $15/oy (1 agent uchunligini aniqlashtiring)"),
                C("1C", "UNF, 1 o'rin", 133_722, null, 1, PlanCodes.Start, true, "Warehouse,Sales,Finance,~Production,~Hr,~Pos", "4 814 000 so'm bir martalik ÷ 36 oy (1cba.uz); joriy qilish alohida"),
                C("1C", "UNF, 5 user", 245_972, null, 5, PlanCodes.Biznes, true, "Warehouse,Sales,Finance,~Production,~Hr,~Pos", "8 855 000 so'm ÷ 36 oy (1cba.uz)"),
                C("1C", "Savdoni boshqarish, 1 o'rin", 167_167, null, 1, PlanCodes.Dist + "," + Savdo, true, "Warehouse,Sales,~Finance,~Distribution,~Pos", "6 018 000 so'm ÷ 36 oy (1cba.uz)"),
                C("Odoo", "Standard (Online)", null, 8.95m, 1, PlanCodes.Korp, true, all, "1-yil aksiya $7.25; hosting kiritilgan, joriy qilish hamkor orqali"),
                C("Odoo", "Custom", null, 13.60m, 1, PlanCodes.Korp, true, all, "1-yil aksiya $10.90; Studio, API, o'z serveri"),
                C("SAP Business One", "Starter", null, 38m, 1, PlanCodes.Start, false, "Warehouse,Sales,Finance", "Hamkorlar: ~$38/user (≤5 user); xalqaro mo'ljal"),
                C("SAP Business One", "Professional", null, 95m, 1, PlanCodes.Korp, false, "Warehouse,Sales,Finance,Production,~Hr", "Hamkorlar: $95–185/user, joriy qilish $15 000+"));
        });

        b.Entity<Expense>(e =>
        {
            e.HasIndex(x => x.Date);
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.AmountOriginal).HasPrecision(18, 2);
            e.Property(x => x.Rate).HasPrecision(18, 4);
            e.Property(x => x.Reference).HasMaxLength(200);
        });
    }
}
