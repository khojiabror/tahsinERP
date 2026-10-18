using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;

namespace tahsinERP.ControlPanel.Services;

/// <summary>Agentga beriladigan ish (snake_case JSON). regToken faqat CreateDb'da va faqat shu javobda.</summary>
public record AgentJob(int Id, string Type, int Attempt, int? TenantId, string? Slug, string? DbName, string? CompanyName,
    string? AdminEmail, string? RegToken);

/// <summary>Agent hisoboti: skriptning RESULT JSON'i, chiqish kodi, log oxiri, vaqtinchalik parol (alohida maydonda).</summary>
public record AgentReport(bool Ok, int ExitCode, string? Error, JsonObject? Result, string? Log, string? TempPassword);

/// <summary>
/// 2-bosqich provisioning (00_PLAN.md 7, 03_DB_PROVISIONING.md). Panel faqat navbat va reestrni yuritadi; bazani ERP
/// serveridagi agent New-TenantDb.ps1 / Invoke-DbMigrations.ps1 bilan yaratadi. Har amal AuditLog'da.
/// </summary>
public sealed class ProvisioningService(ControlDbContext db, AuditLogger audit, Clock clock, TenantService tenants,
    ISecretProtector protector, IOptions<PanelOptions> options, ILogger<ProvisioningService> log)
{
    public const int MaxAttempts = 5;
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan SecretKeep = TimeSpan.FromDays(7);
    public const int MaxLogChars = 30_000;
    private const string SecretPurpose = "provisioning-temp-password";

    /// <summary>03_DB_PROVISIONING.md 3: 3 — SQL'ga ulanib bo'lmadi, 6 — applock. Keyinroq qayta urinish mumkin.</summary>
    public static bool IsRetryable(int exitCode) => exitCode is 3 or 6;

    public static string HashToken(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));

    // ---------- Agent tokeni ----------

    public async Task<string> IssueAgentTokenAsync(Server server, CancellationToken ct = default)
    {
        string token = "AG-" + Base32.Encode(RandomNumberGenerator.GetBytes(30));
        server.AgentTokenHash = HashToken(token);
        server.AgentTokenIssuedUtc = clock.UtcNow;
        audit.Add("server.agent-token", "Server", server.Id, after: new { server.AgentTokenIssuedUtc });
        await db.SaveChangesAsync(ct);
        return token;
    }

    public async Task<Server?> AuthenticateAgentAsync(string? bearer, string? version, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bearer) || !bearer.StartsWith("AG-", StringComparison.Ordinal))
            return null;
        string hash = HashToken(bearer);
        var server = await db.Servers.FirstOrDefaultAsync(s => s.AgentTokenHash == hash && s.IsActive, ct);
        if (server is null)
            return null;
        DateTime now = clock.UtcNow;
        if (server.AgentLastSeenUtc is null || now - server.AgentLastSeenUtc > TimeSpan.FromMinutes(1) || server.AgentVersion != version)
        {
            server.AgentLastSeenUtc = now;
            server.AgentVersion = version is { Length: > 50 } ? version[..50] : version;
            await db.SaveChangesAsync(ct);
        }
        return server;
    }

    // ---------- Navbatga qo'yish (xodim) ----------

    private Task<bool> HasOpenJobAsync(int? tenantId, int serverId, ProvisioningJobType type, CancellationToken ct) =>
        db.ProvisioningJobs.AnyAsync(j => j.ServerId == serverId && j.Type == type && (tenantId == null || j.TenantId == tenantId)
                                          && (j.Status == ProvisioningJobStatus.Pending || j.Status == ProvisioningJobStatus.Running), ct);

    public async Task<ProvisioningJob> EnqueueCreateDbAsync(Tenant tenant, int staffId, CancellationToken ct = default)
    {
        if (tenant.Deployment != DeploymentKind.SaaS) throw new DomainException("Baza faqat SaaS tenant uchun yaratiladi (on-prem — mijoz serverida).");
        if (tenant.Status == TenantStatus.Archived) throw new DomainException("Arxivdagi tenant.");
        if (tenant.ServerId is null) throw new DomainException("Avval serverni tanlang (Tahrirlash).");
        if (string.IsNullOrWhiteSpace(tenant.DbName)) throw new DomainException("Baza nomi bo'sh.");
        if (tenant.ProvisionedAtUtc is not null) throw new DomainException("Baza allaqachon yaratilgan.");
        if (tenant.InstallationId is not null) throw new DomainException("Tenant ERP'ga allaqachon bog'langan — yangi baza kerak bo'lsa, avval qo'lda hal qiling.");
        var server = await db.Servers.FirstAsync(s => s.Id == tenant.ServerId, ct);
        if (!server.IsActive) throw new DomainException("Server faol emas.");
        if (server.AgentTokenHash is null) throw new DomainException("Bu serverda agent ulanmagan (Serverlar → agent tokeni).");
        if (await HasOpenJobAsync(tenant.Id, server.Id, ProvisioningJobType.CreateDb, ct)) throw new DomainException("Bu tenant uchun ish allaqachon navbatda.");

        var job = new ProvisioningJob
        {
            ServerId = server.Id, TenantId = tenant.Id, Type = ProvisioningJobType.CreateDb, Status = ProvisioningJobStatus.Pending,
            CreatedByStaffId = staffId, CreatedAtUtc = clock.UtcNow,
        };
        db.ProvisioningJobs.Add(job);
        await db.SaveChangesAsync(ct);
        audit.Add("provisioning.enqueue", "ProvisioningJob", job.Id, after: new { job.Type, tenant.Slug, tenant.DbName, Server = server.Name }, staffIdOverride: staffId);
        await db.SaveChangesAsync(ct);
        return job;
    }

    public async Task<ProvisioningJob> EnqueueMigrateAllAsync(Server server, int staffId, CancellationToken ct = default)
    {
        if (server.AgentTokenHash is null) throw new DomainException("Bu serverda agent ulanmagan.");
        if (await HasOpenJobAsync(null, server.Id, ProvisioningJobType.MigrateAll, ct)) throw new DomainException("Migratsiya ishi allaqachon navbatda.");
        var job = new ProvisioningJob
        {
            ServerId = server.Id, Type = ProvisioningJobType.MigrateAll, Status = ProvisioningJobStatus.Pending,
            CreatedByStaffId = staffId, CreatedAtUtc = clock.UtcNow,
        };
        db.ProvisioningJobs.Add(job);
        await db.SaveChangesAsync(ct);
        audit.Add("provisioning.enqueue", "ProvisioningJob", job.Id, after: new { job.Type, Server = server.Name }, staffIdOverride: staffId);
        await db.SaveChangesAsync(ct);
        return job;
    }

    public async Task CancelAsync(ProvisioningJob job, int staffId, CancellationToken ct = default)
    {
        if (job.Status != ProvisioningJobStatus.Pending) throw new DomainException("Faqat navbatdagi (boshlanmagan) ish bekor qilinadi.");
        job.Status = ProvisioningJobStatus.Cancelled;
        job.FinishedAtUtc = clock.UtcNow;
        audit.Add("provisioning.cancel", "ProvisioningJob", job.Id, staffIdOverride: staffId);
        await db.SaveChangesAsync(ct);
    }

    // ---------- Agent: ish olish va natija ----------

    /// <summary>
    /// Shu server uchun navbatdagi ish (eng eskisi; lease muddati o'tgan Running ham qayta beriladi). CreateDb uchun yangi
    /// reg_token shu yerda chiqariladi — token faqat agent ishni haqiqatan boshlaganda yaratiladi va 7 kun amal qiladi.
    /// </summary>
    public async Task<AgentJob?> LeaseAsync(Server server, CancellationToken ct = default)
    {
        DateTime now = clock.UtcNow;
        var job = await db.ProvisioningJobs.Include(j => j.Tenant)
            .Where(j => j.ServerId == server.Id && j.Attempts < MaxAttempts
                        && (j.Status == ProvisioningJobStatus.Pending || (j.Status == ProvisioningJobStatus.Running && j.LeaseUntilUtc < now)))
            .OrderBy(j => j.Id).FirstOrDefaultAsync(ct);
        if (job is null)
            return null;

        job.Status = ProvisioningJobStatus.Running;
        job.Attempts++;
        job.LeaseUntilUtc = now.Add(Lease);
        job.StartedAtUtc ??= now;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return null; } // boshqa agent instansi oldi

        string? regToken = null;
        var t = job.Tenant;
        if (job.Type == ProvisioningJobType.CreateDb && t is not null)
            regToken = await tenants.IssueRegTokenAsync(t, ct);
        audit.Add("provisioning.lease", "ProvisioningJob", job.Id, after: new { job.Attempts, Server = server.Name });
        await db.SaveChangesAsync(ct);
        return new AgentJob(job.Id, job.Type.ToString(), job.Attempts, t?.Id, t?.Slug, t?.DbName, t?.LegalName, t?.Email, regToken);
    }

    public async Task<ProvisioningJob> CompleteAsync(Server server, int jobId, AgentReport report, CancellationToken ct = default)
    {
        var job = await db.ProvisioningJobs.Include(j => j.Tenant).FirstOrDefaultAsync(j => j.Id == jobId && j.ServerId == server.Id, ct)
            ?? throw new DomainException("Ish topilmadi.");
        if (job.Status != ProvisioningJobStatus.Running)
            throw new DomainException("Ish bajarilish holatida emas (" + job.Status + ").");

        DateTime now = clock.UtcNow;
        job.ExitCode = report.ExitCode;
        job.Error = Truncate(report.Error, 2000);
        job.Log = TailLog(report.Log);
        job.Result = SanitizeResult(report.Result);
        job.LeaseUntilUtc = null;

        if (report.Ok && report.ExitCode == 0)
        {
            job.Status = ProvisioningJobStatus.Succeeded;
            job.FinishedAtUtc = now;
            if (job.Type == ProvisioningJobType.CreateDb && job.Tenant is { } t)
            {
                t.ProvisionedAtUtc = now;
                if (!string.IsNullOrEmpty(report.TempPassword))
                    job.SecretProtected = protector.Protect(report.TempPassword, SecretPurpose);
                audit.Add("tenant.provisioned", "Tenant", t.Id, after: new { t.DbName, Server = server.Name, JobId = job.Id });
            }
        }
        else if (IsRetryable(report.ExitCode) && job.Attempts < MaxAttempts)
        {
            job.Status = ProvisioningJobStatus.Pending; // keyingi so'rovda qayta beriladi
        }
        else
        {
            job.Status = ProvisioningJobStatus.Failed;
            job.FinishedAtUtc = now;
        }
        audit.Add("provisioning.complete", "ProvisioningJob", job.Id,
            after: new { job.Type, job.Status, job.ExitCode, job.Error, job.Attempts, Tenant = job.Tenant?.Slug, Server = server.Name });
        await db.SaveChangesAsync(ct);
        log.LogInformation("Provisioning #{Job} {Type}: {Status} (kod {Code})", job.Id, job.Type, job.Status, job.ExitCode);
        return job;
    }

    /// <summary>Yangi admin vaqtinchalik parolini BIR MARTA ko'rsatadi va o'chiradi (audit bilan).</summary>
    public async Task<string> RevealTempPasswordAsync(ProvisioningJob job, int staffId, CancellationToken ct = default)
    {
        if (job.SecretProtected is null) throw new DomainException("Parol yo'q (allaqachon ko'rsatilgan yoki 7 kun o'tgan).");
        string password = protector.Unprotect(job.SecretProtected, SecretPurpose);
        job.SecretProtected = null;
        audit.Add("provisioning.reveal-password", "ProvisioningJob", job.Id, staffIdOverride: staffId);
        await db.SaveChangesAsync(ct);
        return password;
    }

    public async Task<int> PurgeOldSecretsAsync(CancellationToken ct = default)
    {
        DateTime cutoff = clock.UtcNow - SecretKeep;
        var old = await db.ProvisioningJobs.Where(j => j.SecretProtected != null && j.FinishedAtUtc < cutoff).ToListAsync(ct);
        foreach (var j in old) j.SecretProtected = null;
        await db.SaveChangesAsync(ct);
        return old.Count;
    }

    // ---------- Reestr (tenants.json) ----------

    /// <summary>
    /// Shu server uchun tenants.json (02_MULTI_TENANT.md formati): SaaS, bazasi yaratilgan, arxivda emas.
    /// Suspended → enabled=false (kirish yopiq, ma'lumot saqlanadi). JSON aniqlanishi barqaror — ETag = SHA-256.
    /// </summary>
    public async Task<(string Json, string ETag)> BuildRegistryAsync(Server server, CancellationToken ct = default)
    {
        var rows = await db.Tenants.AsNoTracking()
            .Where(t => t.ServerId == server.Id && t.Deployment == DeploymentKind.SaaS && t.ProvisionedAtUtc != null
                        && t.DbName != null && t.Status != TenantStatus.Archived)
            .OrderBy(t => t.Slug)
            .Select(t => new { t.Slug, t.DbName, t.CompanyId, t.Status })
            .ToListAsync(ct);
        var root = new JsonObject();
        if (!string.IsNullOrWhiteSpace(options.Value.BaseDomain))
            root["baseDomain"] = options.Value.BaseDomain.Trim().ToLowerInvariant();
        var list = new JsonArray();
        foreach (var r in rows)
            list.Add(new JsonObject
            {
                ["slug"] = r.Slug,
                ["database"] = r.DbName,
                ["companyId"] = r.CompanyId,
                ["enabled"] = r.Status != TenantStatus.Suspended,
            });
        root["tenants"] = list;
        string json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        return (json, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))));
    }

    // ---------- yordamchilar ----------

    public static string? SanitizeResult(JsonObject? result)
    {
        if (result is null) return null;
        var copy = JsonNode.Parse(result.ToJsonString())!.AsObject();
        copy.Remove("tempPassword");
        return Truncate(copy.ToJsonString(), 4000);
    }

    /// <summary>Log oxiri; RESULT qatoridagi tempPassword maskalanadi (03_DB_PROVISIONING.md 4).</summary>
    public static string? TailLog(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        text = System.Text.RegularExpressions.Regex.Replace(text, "\"tempPassword\"\\s*:\\s*\"[^\"]*\"", "\"tempPassword\":\"***\"");
        return text.Length <= MaxLogChars ? text : "…" + text[^MaxLogChars..];
    }

    private static string? Truncate(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}
