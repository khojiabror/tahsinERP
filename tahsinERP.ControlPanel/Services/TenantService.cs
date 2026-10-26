using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.I18n;

namespace tahsinERP.ControlPanel.Services;

/// <summary>Tenant qoidalari. Slug/DbName qoidalari ERP TenantRegistry (02_MULTI_TENANT.md) bilan bir xil.</summary>
public sealed partial class TenantService(ControlDbContext db, AuditLogger audit, Clock clock, SubscriptionService subscriptions,
    IOptions<PanelOptions> options)
{
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")]
    private static partial Regex SlugRegex();

    // New-TenantDb.ps1 chegarasi (03_DB_PROVISIONING.md): 1–100
    [GeneratedRegex(@"^[A-Za-z0-9_\-\.]{1,100}$")]
    private static partial Regex DatabaseRegex();

    /// <summary>ERP TenantRegistry.ReservedSlugs — panel subdomenlari.</summary>
    public static readonly string[] ReservedSlugs =
        ["admin", "my", "pay", "www", "api", "app", "mail", "smtp", "static", "cdn", "assets", "status", "panel", "erp", "bot", "sayt", "sites"];

    public static string? ValidateSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || !SlugRegex().IsMatch(slug))
            return L.T("Pnl_TenSvc_SlugFormat");
        if (ReservedSlugs.Contains(slug))
            return L.T("Pnl_TenSvc_SlugReserved", slug);
        return null;
    }

    public static string? ValidateDbName(string? dbName) =>
        string.IsNullOrEmpty(dbName) || DatabaseRegex().IsMatch(dbName) ? null : L.T("Pnl_TenSvc_DbNameFormat");

    public static string? ValidateInn(string? inn) =>
        string.IsNullOrEmpty(inn) || ((inn.Length == 9 || inn.Length == 14) && inn.All(char.IsAsciiDigit))
            ? null : L.T("Pnl_TenSvc_InnFormat");

    public static string DefaultDbName(string slug) => "DB_" + slug.Replace('-', '_');

    private static object Snapshot(Tenant t) => new
    {
        t.Slug, t.LegalName, t.Inn, t.ContactName, t.Phone, t.Email, t.TelegramChatId, t.Address,
        t.Deployment, t.Status, t.CompanyId, t.DbName, t.ServerId, t.InstallationId, t.Notes, t.OnlineCheck, t.OfflineGraceDays, t.SampleData, t.Modules
    };

    private async Task ValidateAsync(Tenant t, CancellationToken ct)
    {
        t.Slug = (t.Slug ?? "").Trim().ToLowerInvariant();
        t.LegalName = (t.LegalName ?? "").Trim();
        t.Inn = string.IsNullOrWhiteSpace(t.Inn) ? null : t.Inn.Trim();
        t.DbName = string.IsNullOrWhiteSpace(t.DbName) ? null : t.DbName.Trim();
        string? error = ValidateSlug(t.Slug) ?? ValidateDbName(t.DbName) ?? ValidateInn(t.Inn);
        if (error is null && t.LegalName.Length == 0) error = L.T("Pnl_TenSvc_LegalNameRequired");
        if (error is null && t.CompanyId <= 0) error = L.T("Pnl_TenSvc_CompanyIdPositive");
        if (error is null && t.OfflineGraceDays is int g && (g < Licensing.LicenseIssuer.MinOfflineGraceDays || g > Licensing.LicenseIssuer.MaxOfflineGraceDays))
            error = L.T("Pnl_TenSvc_OfflineDaysRange", Licensing.LicenseIssuer.MinOfflineGraceDays, Licensing.LicenseIssuer.MaxOfflineGraceDays);
        if (t.Deployment == DeploymentKind.SaaS) t.OnlineCheck = OnlineCheckMode.Optional; // SaaS — siyosat ma'nosiz
        if (error is null && await db.Tenants.AnyAsync(x => x.Slug == t.Slug && x.Id != t.Id, ct)) error = L.T("Pnl_TenSvc_SlugTaken", t.Slug);
        if (error is null && t.DbName != null && await db.Tenants.AnyAsync(x => x.DbName == t.DbName && x.Id != t.Id, ct)) error = L.T("Pnl_TenSvc_DbNameTaken", t.DbName);
        if (error is null && t.ServerId != null)
        {
            var server = await db.Servers.FirstOrDefaultAsync(s => s.Id == t.ServerId, ct);
            if (server is null || !server.IsActive) error = L.T("Pnl_TenSvc_ServerInactive");
            else
            {
                int used = await db.Tenants.CountAsync(x => x.ServerId == server.Id && x.Id != t.Id && x.Status != TenantStatus.Archived, ct);
                if (used >= server.MaxTenants) error = L.T("Pnl_TenSvc_ServerFull", server.Name, used, server.MaxTenants);
            }
        }
        if (error != null) throw new DomainException(error);
    }

    /// <summary>Yangi tenant. startTrial — TRIAL obunasi (14 kun) bilan; aks holda Suspended, birinchi to'lovgacha.</summary>
    public async Task<Tenant> CreateAsync(Tenant t, bool startTrial, CancellationToken ct = default)
    {
        if (t.Deployment == DeploymentKind.SaaS && string.IsNullOrWhiteSpace(t.DbName) && !string.IsNullOrWhiteSpace(t.Slug))
            t.DbName = DefaultDbName(t.Slug.Trim().ToLowerInvariant());
        await ValidateAsync(t, ct);
        t.Status = startTrial ? TenantStatus.Trial : TenantStatus.Suspended;
        t.CreatedAtUtc = clock.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Tenants.Add(t);
        await db.SaveChangesAsync(ct);
        audit.Add("tenant.create", "Tenant", t.Id, after: Snapshot(t));
        if (startTrial)
            await subscriptions.CreateTrialAsync(t, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return t;
    }

    public async Task UpdateAsync(Tenant t, object before, CancellationToken ct = default)
    {
        if (t.ProvisionedAtUtc != null)
        {
            var e = db.Entry(t);
            if (e.Property(x => x.DbName).IsModified && !Equals(e.Property(x => x.DbName).OriginalValue, t.DbName)
                || e.Property(x => x.ServerId).IsModified && !Equals(e.Property(x => x.ServerId).OriginalValue, t.ServerId)
                || e.Property(x => x.Deployment).IsModified && !Equals(e.Property(x => x.Deployment).OriginalValue, t.Deployment))
                throw new DomainException(L.T("Pnl_TenSvc_ProvisionedLocked"));
        }
        await ValidateAsync(t, ct);
        audit.Add("tenant.update", "Tenant", t.Id, before, Snapshot(t));
        await db.SaveChangesAsync(ct);
    }

    public object TakeSnapshot(Tenant t) => Snapshot(t);

    public async Task ChangeStatusAsync(Tenant t, TenantStatus status, string? reason, CancellationToken ct = default)
    {
        if (t.Status == status) return;
        if (status == TenantStatus.Archived && string.IsNullOrWhiteSpace(reason))
            throw new DomainException(L.T("Pnl_Prov_ArchiveReasonRequired"));
        if (status == TenantStatus.Archived && t.Deployment == DeploymentKind.SaaS && t.ProvisionedAtUtc != null)
            throw new DomainException(L.T("Pnl_TenSvc_ArchiveViaProvisioning"));
        if (t.Status == TenantStatus.Archived && t.Deployment == DeploymentKind.SaaS && t.ProvisionedAtUtc != null)
            throw new DomainException(L.T("Pnl_TenSvc_ArchivedNoRestore"));
        var before = t.Status;
        t.Status = status;
        audit.Add("tenant.status", "Tenant", t.Id, new { Status = before }, new { Status = status, Reason = reason });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Yangi bir martalik reg_token (7 kun). Bazada faqat SHA-256 xeshi saqlanadi; token ekranda bir marta ko'rsatiladi.
    /// Tenant allaqachon bog'langan bo'lsa ham chiqarish mumkin (ERP qayta o'rnatilganda) — yangi register eski bog'lanishni almashtiradi.
    /// </summary>
    public async Task<string> IssueRegTokenAsync(Tenant t, CancellationToken ct = default)
    {
        if (t.Status == TenantStatus.Archived)
            throw new DomainException(L.T("Pnl_TenSvc_ArchivedNoToken"));
        string token = "RT-" + Base32.Encode(RandomNumberGenerator.GetBytes(20));
        t.RegTokenHash = HashToken(token);
        t.RegTokenExpiresUtc = clock.UtcNow.AddDays(options.Value.RegTokenDays);
        t.RegTokenUsedUtc = null;
        audit.Add("tenant.regtoken", "Tenant", t.Id, after: new { t.RegTokenExpiresUtc, AlreadyBound = t.InstallationId != null });
        await db.SaveChangesAsync(ct);
        return token;
    }

    public static string HashToken(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));
}
