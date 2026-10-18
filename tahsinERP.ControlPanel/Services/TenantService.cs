using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;

namespace tahsinERP.ControlPanel.Services;

/// <summary>Tenant qoidalari. Slug/DbName qoidalari ERP TenantRegistry (02_MULTI_TENANT.md) bilan bir xil.</summary>
public sealed partial class TenantService(ControlDbContext db, AuditLogger audit, Clock clock, SubscriptionService subscriptions,
    IOptions<PanelOptions> options)
{
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")]
    private static partial Regex SlugRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_\-\.]{1,128}$")]
    private static partial Regex DatabaseRegex();

    /// <summary>ERP TenantRegistry.ReservedSlugs — panel subdomenlari.</summary>
    public static readonly string[] ReservedSlugs =
        ["admin", "my", "pay", "www", "api", "app", "mail", "smtp", "static", "cdn", "assets", "status", "panel", "erp"];

    public static string? ValidateSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || !SlugRegex().IsMatch(slug))
            return "Slug: faqat kichik lotin harflari, raqamlar va '-', 1–63 belgi, '-' bilan boshlanmasin/tugamasin.";
        if (ReservedSlugs.Contains(slug))
            return $"'{slug}' band (panel subdomeni uchun ajratilgan).";
        return null;
    }

    public static string? ValidateDbName(string? dbName) =>
        string.IsNullOrEmpty(dbName) || DatabaseRegex().IsMatch(dbName) ? null : "Baza nomi: faqat A-Z a-z 0-9 _ - . (1–128).";

    public static string? ValidateInn(string? inn) =>
        string.IsNullOrEmpty(inn) || ((inn.Length == 9 || inn.Length == 14) && inn.All(char.IsAsciiDigit))
            ? null : "STIR 9 raqam (yuridik shaxs) yoki JShShIR 14 raqam bo'lsin.";

    public static string DefaultDbName(string slug) => "DB_" + slug.Replace('-', '_');

    private static object Snapshot(Tenant t) => new
    {
        t.Slug, t.LegalName, t.Inn, t.ContactName, t.Phone, t.Email, t.TelegramChatId, t.Address,
        t.Deployment, t.Status, t.CompanyId, t.DbName, t.ServerId, t.InstallationId, t.Notes
    };

    private async Task ValidateAsync(Tenant t, CancellationToken ct)
    {
        t.Slug = (t.Slug ?? "").Trim().ToLowerInvariant();
        t.LegalName = (t.LegalName ?? "").Trim();
        t.Inn = string.IsNullOrWhiteSpace(t.Inn) ? null : t.Inn.Trim();
        t.DbName = string.IsNullOrWhiteSpace(t.DbName) ? null : t.DbName.Trim();
        string? error = ValidateSlug(t.Slug) ?? ValidateDbName(t.DbName) ?? ValidateInn(t.Inn);
        if (error is null && t.LegalName.Length == 0) error = "Yuridik nom majburiy.";
        if (error is null && t.CompanyId <= 0) error = "CompanyID musbat bo'lsin.";
        if (error is null && await db.Tenants.AnyAsync(x => x.Slug == t.Slug && x.Id != t.Id, ct)) error = $"'{t.Slug}' slug band.";
        if (error is null && t.DbName != null && await db.Tenants.AnyAsync(x => x.DbName == t.DbName && x.Id != t.Id, ct)) error = $"'{t.DbName}' baza nomi band.";
        if (error is null && t.ServerId != null)
        {
            var server = await db.Servers.FirstOrDefaultAsync(s => s.Id == t.ServerId, ct);
            if (server is null || !server.IsActive) error = "Server topilmadi yoki faol emas.";
            else
            {
                int used = await db.Tenants.CountAsync(x => x.ServerId == server.Id && x.Id != t.Id && x.Status != TenantStatus.Archived, ct);
                if (used >= server.MaxTenants) error = $"'{server.Name}' serveri to'lgan ({used}/{server.MaxTenants}).";
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
        await ValidateAsync(t, ct);
        audit.Add("tenant.update", "Tenant", t.Id, before, Snapshot(t));
        await db.SaveChangesAsync(ct);
    }

    public object TakeSnapshot(Tenant t) => Snapshot(t);

    public async Task ChangeStatusAsync(Tenant t, TenantStatus status, string? reason, CancellationToken ct = default)
    {
        if (t.Status == status) return;
        if (status == TenantStatus.Archived && string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Arxivlash sababi majburiy.");
        if (status == TenantStatus.Archived && t.Deployment == DeploymentKind.SaaS && t.ProvisionedAtUtc != null)
            throw new DomainException("Bazasi yaratilgan SaaS tenant faqat «Arxivlash» amali (provisioning) orqali arxivlanadi.");
        if (t.Status == TenantStatus.Archived && t.Deployment == DeploymentKind.SaaS && t.ProvisionedAtUtc != null)
            throw new DomainException("Arxivlangan tenant bazasi o'chirilgan — holatni qo'lda qaytarib bo'lmaydi.");
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
            throw new DomainException("Arxivdagi tenant uchun token chiqarilmaydi.");
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
