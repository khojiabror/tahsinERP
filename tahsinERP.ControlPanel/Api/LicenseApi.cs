using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Licensing;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;
using ErpLicense = tahsinERP.Licensing.LicenseHelper;

namespace tahsinERP.ControlPanel.Api;

/// <summary>
/// ERP ↔ panel License API v1. Kontrakt: Docs/ControlPanel/01_LICENSE_API.md — o'zgartirilsa ERP
/// (Services/LicenseSync/LicensePanelClient.cs) bilan birga o'zgartiring; testlar o'sha klient faylini ishlatadi.
/// </summary>
public static class LicenseApi
{
    public const string HeaderTimestamp = "X-Tahsin-Timestamp";
    public const string HeaderNonce = "X-Tahsin-Nonce";
    public const string HeaderSignature = "X-Tahsin-Signature";
    public const int MaxClockSkewSeconds = 300;
    public static readonly TimeSpan NonceWindow = TimeSpan.FromMinutes(10);
    public const string RateLimitPolicy = "license-api";

    public sealed class RegisterRequest
    {
        [JsonPropertyName("reg_token")] public string? RegToken { get; set; }
        [JsonPropertyName("installation_id")] public string? InstallationId { get; set; }
        [JsonPropertyName("company_id")] public int? CompanyId { get; set; }
        [JsonPropertyName("version")] public string? Version { get; set; }
        [JsonPropertyName("host")] public string? Host { get; set; }
    }

    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/license").RequireRateLimiting(RateLimitPolicy).DisableAntiforgery()
            .AllowAnonymous(); // xodim cookie'si emas — register: reg_token, pull: HMAC imzo
        group.MapPost("/register", RegisterAsync);
        group.MapGet("/{installationId}", PullAsync);
    }

    private static IResult Error(int status, string error, string? message = null) =>
        Results.Json(message is null ? new Dictionary<string, string> { ["error"] = error }
            : new Dictionary<string, string> { ["error"] = error, ["message"] = message }, statusCode: status);

    private static int PollHours(PanelOptions o) => Math.Clamp(o.PollHours, 1, 24);

    /// <summary>On-prem onlayn tekshiruv siyosati (08_STAGE4.md 4): "required" faqat On-prem + Required; qolgani "optional".</summary>
    internal static string OnlineCheck(Tenant t) => LicenseIssuer.IsRollingKey(t) ? "required" : "optional";

    internal static async Task<IResult> RegisterAsync(HttpRequest http, ControlDbContext db, ISecretProtector protector, LicenseIssuer issuer,
        AuditLogger audit, Clock clock, IOptions<PanelOptions> options, ILoggerFactory loggers, CancellationToken ct)
    {
        var log = loggers.CreateLogger("LicenseApi");
        RegisterRequest? body;
        try { body = await http.ReadFromJsonAsync<RegisterRequest>(ct); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or BadHttpRequestException)
        { return Error(400, "bad_request", "JSON tanasi o'qilmadi."); }

        if (body is null || string.IsNullOrWhiteSpace(body.RegToken) || string.IsNullOrWhiteSpace(body.InstallationId))
            return Error(400, "bad_request", "reg_token va installation_id majburiy.");
        string? inst = ErpLicense.NormalizeInstallationId(body.InstallationId);
        if (inst is null)
            return Error(400, "bad_request", "installation_id GUID emas.");
        if (body.CompanyId is <= 0)
            return Error(400, "bad_request", "company_id musbat bo'lsin.");

        string hash = TenantService.HashToken(body.RegToken);
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.RegTokenHash == hash, ct);
        DateTime now = clock.UtcNow;
        if (tenant is null || tenant.Status == TenantStatus.Archived)
            return Error(404, "token_not_found");

        string secret;
        if (tenant.RegTokenUsedUtc is not null)
        {
            // Javob yo'qolgan bo'lsa — o'sha token + o'sha o'rnatma bilan qayta so'rov idempotent.
            if (tenant.InstallationId != inst || tenant.ApiSecretProtected is null)
                return Error(409, "token_used");
            secret = protector.Unprotect(tenant.ApiSecretProtected, SecretPurposes.TenantApiSecret);
        }
        else
        {
            if (tenant.RegTokenExpiresUtc is null || tenant.RegTokenExpiresUtc < now)
                return Error(404, "token_not_found");
            if (await db.Tenants.AnyAsync(t => t.InstallationId == inst && t.Id != tenant.Id, ct))
                return Error(409, "token_used", "Bu installation_id boshqa tenantga bog'langan.");

            var before = new { tenant.InstallationId, tenant.CompanyId };
            secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            tenant.InstallationId = inst;
            if (body.CompanyId is int companyId)
                tenant.CompanyId = companyId; // kalitdagi CompanyID ERP kutganiga teng bo'lishi shart
            tenant.ApiSecretProtected = protector.Protect(secret, SecretPurposes.TenantApiSecret);
            tenant.RegTokenUsedUtc = now;
            tenant.RegisteredAtUtc = now;
            tenant.LastSeenUtc = now;
            tenant.LastVersion = Trim(body.Version, 50);
            tenant.LastHost = Trim(body.Host, 253);
            audit.Add("tenant.register", "Tenant", tenant.Id, before,
                new { tenant.InstallationId, tenant.CompanyId, tenant.LastVersion, tenant.LastHost });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (BillingService.IsConcurrencyConflict(ex))
            {
                log.LogWarning(ex, "Register: parallel so'rov (tenant {Tenant})", tenant.Slug);
                return Error(409, "token_used");
            }
        }

        string? key = null;
        try
        {
            var license = await issuer.EnsureForTenantAsync(tenant, ct) ?? await issuer.GetCurrentAsync(tenant, ct);
            key = license?.LicenseKey;
        }
        catch (Exception ex) when (ex is DomainException or CryptographicException or InvalidOperationException or NotSupportedException)
        {
            // Bog'lanish saqlandi; kalit keyingi pull'da yetib boradi.
            log.LogError(ex, "Register: kalit chiqmadi (tenant {Tenant})", tenant.Slug);
        }

        return Results.Json(new Dictionary<string, object?>
        {
            ["tenant_secret"] = secret,
            ["license_key"] = key,
            ["poll_hours"] = PollHours(options.Value),
            ["online_check"] = OnlineCheck(tenant),
            ["offline_grace_days"] = LicenseIssuer.OfflineGraceDays(tenant, options.Value),
        });
    }

    internal static async Task<IResult> PullAsync(string installationId, HttpRequest http, ControlDbContext db, ISecretProtector protector,
        LicenseIssuer issuer, Clock clock, IOptions<PanelOptions> options, ILoggerFactory loggers, CancellationToken ct)
    {
        string? inst = ErpLicense.NormalizeInstallationId(installationId);
        if (inst is null)
            return Error(404, "unknown_installation");

        string? ts = http.Headers[HeaderTimestamp].FirstOrDefault();
        string? nonce = http.Headers[HeaderNonce].FirstOrDefault();
        string? signature = http.Headers[HeaderSignature].FirstOrDefault();
        string? etag = ParseIfNoneMatch(http.Headers.IfNoneMatch.FirstOrDefault());

        if (!long.TryParse(ts, NumberStyles.None, CultureInfo.InvariantCulture, out long tsValue)
            || Math.Abs(clock.Now.ToUnixTimeSeconds() - tsValue) > MaxClockSkewSeconds)
            return Error(401, "clock_skew");
        if (nonce is null || nonce.Length != 32 || !nonce.All(Uri.IsHexDigit) || string.IsNullOrEmpty(signature))
            return Error(401, "bad_signature");

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.InstallationId == inst, ct);
        if (tenant is null || tenant.Status == TenantStatus.Archived || tenant.ApiSecretProtected is null)
            return Error(404, "unknown_installation");

        byte[] secret = Convert.FromBase64String(protector.Unprotect(tenant.ApiSecretProtected, SecretPurposes.TenantApiSecret));
        // Imzo yo'l AYNAN so'rovdagidek (ERP katta harfli GUID yuboradi).
        string path = http.PathBase.Add(http.Path).Value ?? "";
        string stringToSign = "GET\n" + path + "\n" + ts + "\n" + nonce + "\n" + (etag ?? "");
        byte[] expected = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(stringToSign));
        byte[] given;
        try { given = Convert.FromBase64String(signature); }
        catch (FormatException) { return Error(401, "bad_signature"); }
        if (!CryptographicOperations.FixedTimeEquals(expected, given))
            return Error(401, "bad_signature");

        // Nonce faqat imzo to'g'ri bo'lganda band qilinadi (begona odam nonce'larni "yeb" qo'ya olmaydi).
        // Noyob indeks (InstallationId, Nonce) — bir nechta panel instansida ham bitta nonce bir marta.
        DateTime now = clock.UtcNow;
        db.ApiNonces.Add(new ApiNonce { InstallationId = inst, Nonce = nonce.ToLowerInvariant(), ExpiresUtc = now.Add(NonceWindow) });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            return Error(401, "replay");
        }

        if (tenant.LastSeenUtc is null || now - tenant.LastSeenUtc > TimeSpan.FromMinutes(5))
        {
            tenant.LastSeenUtc = now;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { /* faqat statistika — muhim emas */ }
        }

        // Aylanma kalit (on-prem, onlayn tekshiruv majburiy): yangilash vaqti kelgan bo'lsa shu pull'da yangisi chiqadi.
        if (LicenseIssuer.IsRollingKey(tenant))
        {
            try { await issuer.EnsureForTenantAsync(tenant, ct); }
            catch (Exception ex) when (ex is DomainException or CryptographicException or InvalidOperationException or NotSupportedException)
            {
                loggers.CreateLogger("LicenseApi").LogError(ex, "Pull: aylanma kalit chiqmadi (tenant {Tenant})", tenant.Slug);
            }
        }

        var license = await issuer.GetCurrentAsync(tenant, ct);
        if (license is null)
        {
            bool revoked = await db.Licenses.AnyAsync(l => l.TenantId == tenant.Id && l.InstallationId == inst
                                                           && l.RevokedAtUtc != null && l.SupersededById == null, ct);
            return revoked ? Error(410, "revoked") : Error(404, "no_license");
        }

        http.HttpContext.Response.Headers.ETag = "\"" + license.ETag + "\"";
        if (etag != null && string.Equals(etag, license.ETag, StringComparison.OrdinalIgnoreCase))
            return Results.StatusCode(304);

        return Results.Json(new Dictionary<string, object?>
        {
            ["license_key"] = license.LicenseKey,
            ["etag"] = license.ETag,
            ["poll_hours"] = PollHours(options.Value),
            ["online_check"] = OnlineCheck(tenant),
            ["offline_grace_days"] = LicenseIssuer.OfflineGraceDays(tenant, options.Value),
        });
    }

    private static string? ParseIfNoneMatch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string v = value.Trim();
        if (v.StartsWith("W/", StringComparison.Ordinal)) v = v[2..];
        return v.Trim('"');
    }

    private static string? Trim(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : (s.Length > max ? s[..max] : s.Trim());
}
