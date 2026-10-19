using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.ControlPanel.Services;

public class ProvisioningOptions
{
    /// <summary>Tenant URL'i: https://&lt;slug&gt;.&lt;BaseDomain&gt;. Domen hali tanlanmagan — sozlamada.</summary>
    public string BaseDomain { get; set; } = "";
    /// <summary>Kechasi shablondan qayta tiklanadigan demo tenant slug'i.</summary>
    public string DemoSlug { get; set; } = "demo";
    /// <summary>Agent job'ni shuncha daqiqa band qiladi (uzun amallarda /extend bilan uzaytiradi).</summary>
    public int LeaseMinutes { get; set; } = 30;
    public int MaxAttempts { get; set; } = 5;
    public string DefaultAdminLogin { get; set; } = "admin";
}

/// <summary>
/// ProvisioningJobs navbati (05_PROVISIONING.md). Qoidalar:
/// - bir tenantda bir vaqtda bitta faol (Pending/Running) job; xuddi shu turdagi takroriy so'rov — mavjud job qaytadi (idempotent);
/// - har amal AuditLog'ga amal bilan bir SaveChanges'da yoziladi;
/// - maxfiy parametrlar (reg_token, vaqtinchalik parol) faqat shifrlangan holda, job muvaffaqiyatli tugagach/bekor qilingach o'chiriladi.
/// </summary>
public sealed class ProvisioningService(ControlDbContext db, AuditLogger audit, Clock clock, TenantService tenants, ISecretProtector protector,
    ITelegramSender telegram, IOptions<ProvisioningOptions> options, ILogger<ProvisioningService> log)
{
    private ProvisioningOptions O => options.Value;
    private const int MaxLogChars = 200_000;

    public sealed record EnqueueResult(ProvisioningJob Job, bool Created, string? TempPassword);

    public static bool DesiredEnabled(TenantStatus s) => s is TenantStatus.Trial or TenantStatus.Active or TenantStatus.Grace;

    public string? TenantUrl(Tenant t) => string.IsNullOrWhiteSpace(O.BaseDomain) ? null : $"https://{t.Slug}.{O.BaseDomain.Trim().TrimStart('.')}";

    public bool IsDemo(Tenant t) => string.Equals(t.Slug, O.DemoSlug, StringComparison.OrdinalIgnoreCase);

    /// <summary>12 belgili vaqtinchalik parol (o'xshash belgilarsiz) — New-TenantDb.ps1 bilan bir xil alifbo.</summary>
    public static string NewTempPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        return string.Create(12, 0, (span, _) =>
        {
            for (int i = 0; i < span.Length; i++) span[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        });
    }

    /// <summary>Bo'sh joyi eng ko'p faol server (00_PLAN.md 7.1). Yo'q bo'lsa — null.</summary>
    public async Task<Server?> PickServerAsync(CancellationToken ct = default)
    {
        var servers = await db.Servers.Where(s => s.IsActive).ToListAsync(ct);
        var used = await db.Tenants.Where(t => t.ServerId != null && t.Status != TenantStatus.Archived)
            .GroupBy(t => t.ServerId!.Value).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C, ct);
        return servers.Select(s => (s, free: s.MaxTenants - used.GetValueOrDefault(s.Id)))
            .Where(x => x.free > 0).OrderByDescending(x => x.free).ThenBy(x => x.s.Id).Select(x => x.s).FirstOrDefault();
    }

    /// <summary>Job qo'shadi. Xodim amali — staffId (null — tizim/agent). Biznes qoidasi buzilsa DomainException.</summary>
    public async Task<EnqueueResult> EnqueueAsync(Tenant t, ProvisioningJobType type, string? reason = null, string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        if (idempotencyKey != null)
        {
            var byKey = await db.ProvisioningJobs.FirstOrDefaultAsync(j => j.IdempotencyKey == idempotencyKey, ct);
            if (byKey != null) return new EnqueueResult(byKey, false, null);
        }
        var active = await db.ProvisioningJobs.FirstOrDefaultAsync(j => j.TenantId == t.Id
            && (j.Status == ProvisioningJobStatus.Pending || j.Status == ProvisioningJobStatus.Running), ct);
        if (active != null)
        {
            if (active.Type == type) return new EnqueueResult(active, false, null);
            throw new DomainException($"Tenantda boshqa amal bajarilmoqda: {active.Type} (#{active.Id}). Tugashini kuting yoki bekor qiling.");
        }

        if (t.Deployment != DeploymentKind.SaaS) throw new DomainException("Provisioning faqat SaaS tenantlar uchun.");
        if (t.DeletedAtUtc != null) throw new DomainException("Tenant o'chirilgan.");
        if (string.IsNullOrWhiteSpace(t.DbName)) throw new DomainException("Baza nomi (DbName) berilmagan.");
        bool provisioned = t.ProvisionedAtUtc != null;
        string? tempPassword = null;
        var secrets = new JobSecrets();
        switch (type)
        {
            case ProvisioningJobType.Create:
                if (provisioned) throw new DomainException("Baza allaqachon yaratilgan.");
                if (t.Status == TenantStatus.Archived) throw new DomainException("Arxivdagi tenant uchun baza yaratilmaydi.");
                if (t.ServerId is null)
                {
                    var server = await PickServerAsync(ct) ?? throw new DomainException("Bo'sh joyi bor faol server yo'q (Serverlar bo'limi).");
                    t.ServerId = server.Id;
                    audit.Add("tenant.server", "Tenant", t.Id, null, new { ServerId = server.Id, Reason = "Provisioning: avtomatik tanlandi" });
                }
                tempPassword = NewTempPassword();
                secrets.AdminPassword = tempPassword;
                secrets.RegToken = await tenants.IssueRegTokenAsync(t, ct);
                break;
            case ProvisioningJobType.Reset:
                if (!IsDemo(t)) throw new DomainException($"Qayta tiklash faqat demo tenant ('{O.DemoSlug}') uchun.");
                if (t.Status == TenantStatus.Archived) throw new DomainException("Arxivdagi tenant.");
                // Shablondan tiklangan bazada InstallationId yo'q — ERP yangisini yaratib, shu token bilan qayta bog'lanadi.
                secrets.RegToken = await tenants.IssueRegTokenAsync(t, ct);
                break;
            case ProvisioningJobType.Suspend:
            case ProvisioningJobType.Resume:
                if (!provisioned) throw new DomainException("Baza hali yaratilmagan.");
                if (t.Status == TenantStatus.Archived) throw new DomainException("Arxivdagi tenant.");
                break;
            case ProvisioningJobType.Archive:
                if (!provisioned) throw new DomainException("Baza hali yaratilmagan — arxivlash uchun holatni o'zgartiring.");
                if (t.Status == TenantStatus.Archived) throw new DomainException("Allaqachon arxivda.");
                if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Arxivlash sababi majburiy.");
                break;
            case ProvisioningJobType.Delete:
                if (t.Status != TenantStatus.Archived) throw new DomainException("O'chirish faqat arxivlangan tenant uchun.");
                if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("O'chirish sababi majburiy.");
                break;
        }
        if (t.ServerId is null) throw new DomainException("Tenant serverga biriktirilmagan.");

        var payload = new JobPayload
        {
            Slug = t.Slug, DbName = t.DbName!, CompanyId = t.CompanyId, CompanyName = t.LegalName,
            AdminLogin = O.DefaultAdminLogin, AdminFullName = "Administrator", AdminEmail = t.Email,
            Demo = IsDemo(t), Reason = reason?.Trim(),
        };
        var job = new ProvisioningJob
        {
            TenantId = t.Id, Type = type, Status = ProvisioningJobStatus.Pending,
            Payload = JsonSerializer.Serialize(payload, AgentProtocol.Json),
            SecretsProtected = secrets.RegToken is null && secrets.AdminPassword is null ? null
                : protector.Protect(JsonSerializer.Serialize(secrets, AgentProtocol.Json), SecretPurposes.JobSecrets),
            IdempotencyKey = idempotencyKey, MaxAttempts = Math.Max(1, O.MaxAttempts), NotBeforeUtc = clock.UtcNow,
            CreatedByStaffId = audit.CurrentStaffId, CreatedAtUtc = clock.UtcNow,
        };
        db.ProvisioningJobs.Add(job);
        await db.SaveChangesAsync(ct);
        audit.Add("provisioning.enqueue", "Tenant", t.Id, after: new { JobId = job.Id, Type = type, Reason = reason, idempotencyKey });
        await db.SaveChangesAsync(ct);
        return new EnqueueResult(job, true, tempPassword);
    }

    /// <summary>Xodim: tenantni to'xtatish (holat Suspended + Suspend job, agar baza yaratilgan bo'lsa).</summary>
    public async Task<ProvisioningJob?> SuspendAsync(Tenant t, string? reason, CancellationToken ct = default)
    {
        if (t.Status == TenantStatus.Archived) throw new DomainException("Arxivdagi tenant.");
        await tenants.ChangeStatusAsync(t, TenantStatus.Suspended, reason, ct);
        return t.ProvisionedAtUtc != null && t.Deployment == DeploymentKind.SaaS
            ? (await EnqueueAsync(t, ProvisioningJobType.Suspend, reason, ct: ct)).Job : null;
    }

    /// <summary>Xodim: qayta yoqish. Holat faol obunadan olinadi; obuna bo'lmasa — xato (kunlik tekshiruv baribir yana to'xtatardi).</summary>
    public async Task<ProvisioningJob?> ResumeAsync(Tenant t, string? reason, CancellationToken ct = default)
    {
        if (t.Status == TenantStatus.Archived) throw new DomainException("Arxivdagi tenant.");
        var today = clock.Today;
        var sub = await db.Subscriptions.Where(s => s.TenantId == t.Id && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.EndDate).FirstOrDefaultAsync(ct);
        if (sub is null || sub.EndDate < today)
            throw new DomainException("Faol obuna yo'q — avval to'lovni belgilang yoki obunani uzaytiring.");
        var target = sub.Period == BillingPeriod.Trial ? TenantStatus.Trial : TenantStatus.Active;
        await tenants.ChangeStatusAsync(t, target, reason ?? "Qayta yoqildi", ct);
        return t.ProvisionedAtUtc != null && t.Deployment == DeploymentKind.SaaS
            ? (await EnqueueAsync(t, ProvisioningJobType.Resume, reason, ct: ct)).Job : null;
    }

    /// <summary>
    /// Panel holati (Suspended ↔ faol) bilan tenants.json dagi oxirgi holat farq qilsa — Suspend/Resume job (tizim nomidan).
    /// Agent har so'rovida chaqiriladi: to'lov, kunlik tekshiruv yoki qo'lda o'zgarish qayerda bo'lmasin, holat yaqinlashadi.
    /// </summary>
    public async Task<int> ReconcileAsync(int serverId, CancellationToken ct = default)
    {
        var candidates = await db.Tenants.Where(t => t.ServerId == serverId && t.Deployment == DeploymentKind.SaaS
                && t.ProvisionedAtUtc != null && t.DeletedAtUtc == null && t.Status != TenantStatus.Archived
                && !db.ProvisioningJobs.Any(j => j.TenantId == t.Id && (j.Status == ProvisioningJobStatus.Pending || j.Status == ProvisioningJobStatus.Running)))
            .ToListAsync(ct);
        int n = 0;
        foreach (var t in candidates)
        {
            bool desired = DesiredEnabled(t.Status);
            if (t.RegistryEnabled == desired) continue;
            await EnqueueAsync(t, desired ? ProvisioningJobType.Resume : ProvisioningJobType.Suspend, $"Holat: {t.Status}", ct: ct);
            n++;
        }
        return n;
    }

    /// <summary>Agent uchun keyingi job (eng eskisi). Muddati o'tgan Running job qayta beriladi.</summary>
    public async Task<JobLease?> LeaseAsync(Server server, CancellationToken ct = default)
    {
        await ReconcileAsync(server.Id, ct);
        for (int guard = 0; guard < 20; guard++)
        {
            DateTime now = clock.UtcNow;
            var job = await db.ProvisioningJobs.Include(j => j.Tenant)
                .Where(j => j.Tenant!.ServerId == server.Id
                            && ((j.Status == ProvisioningJobStatus.Pending && j.NotBeforeUtc <= now)
                                || (j.Status == ProvisioningJobStatus.Running && j.LeaseUntilUtc < now)))
                .OrderBy(j => j.Id).FirstOrDefaultAsync(ct);
            if (job is null) return null;

            if (job.Status == ProvisioningJobStatus.Running)
                AppendLog(job, $"{now:yyyy-MM-dd HH:mm:ss} [WARN] lease muddati o'tdi (agent javob bermadi) — qayta beriladi");
            if (job.Attempts >= job.MaxAttempts)
            {
                FinishFailed(job, "Urinishlar soni tugadi (agent javob bermadi).", null);
                await SaveIgnoringConflictAsync(ct);
                continue;
            }
            job.Attempts++;
            job.Status = ProvisioningJobStatus.Running;
            job.LeaseToken = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
            job.LeaseUntilUtc = now.AddMinutes(Math.Max(1, O.LeaseMinutes));
            job.LeasedByServerId = server.Id;
            job.StartedAtUtc ??= now;
            if (!await SaveIgnoringConflictAsync(ct)) continue; // boshqa agent/instans oldin oldi

            var payload = JsonSerializer.Deserialize<JobPayload>(job.Payload, AgentProtocol.Json) ?? new JobPayload();
            var t = job.Tenant!;
            payload.Enabled = DesiredEnabled(t.Status);
            payload.SqlInstance = server.SqlInstance;
            JobSecrets? secrets = job.SecretsProtected is null ? null
                : JsonSerializer.Deserialize<JobSecrets>(protector.Unprotect(job.SecretsProtected, SecretPurposes.JobSecrets), AgentProtocol.Json);
            return new JobLease
            {
                JobId = job.Id, LeaseToken = job.LeaseToken, Type = (JobKind)job.Type, Attempt = job.Attempts, MaxAttempts = job.MaxAttempts,
                LeaseUntilUtc = job.LeaseUntilUtc.Value, Payload = payload, Secrets = secrets,
            };
        }
        return null;
    }

    public enum CompleteStatus { Ok, NotFound, LeaseLost }

    public async Task<CompleteStatus> ExtendAsync(Server server, long jobId, string leaseToken, CancellationToken ct = default)
    {
        var job = await db.ProvisioningJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null) return CompleteStatus.NotFound;
        if (job.Status != ProvisioningJobStatus.Running || job.LeaseToken != leaseToken || job.LeasedByServerId != server.Id)
            return CompleteStatus.LeaseLost;
        job.LeaseUntilUtc = clock.UtcNow.AddMinutes(Math.Max(1, O.LeaseMinutes));
        return await SaveIgnoringConflictAsync(ct) ? CompleteStatus.Ok : CompleteStatus.LeaseLost;
    }

    /// <summary>Agent natijasi. Muvaffaqiyatda tenant holati yangilanadi; Create'da xush kelibsiz xabari tenant Telegram chatiga.</summary>
    public async Task<CompleteStatus> CompleteAsync(Server server, long jobId, JobCompletion c, CancellationToken ct = default)
    {
        var job = await db.ProvisioningJobs.Include(j => j.Tenant).FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null) return CompleteStatus.NotFound;
        if (job.Status != ProvisioningJobStatus.Running || job.LeaseToken != c.LeaseToken || job.LeasedByServerId != server.Id)
            return CompleteStatus.LeaseLost;

        DateTime now = clock.UtcNow;
        var t = job.Tenant!;
        AppendLog(job, $"--- urinish {job.Attempts}/{job.MaxAttempts}: {c.Outcome} ---");
        if (!string.IsNullOrEmpty(c.Log)) AppendLog(job, c.Log);
        job.Result = Truncate(c.Result, 4000);
        job.ExitCode = c.ExitCode;
        job.LeaseToken = null;
        job.LeaseUntilUtc = null;
        string? welcomePassword = null;

        if (c.Outcome == JobOutcome.Succeeded)
        {
            if (job.Type == ProvisioningJobType.Create && job.SecretsProtected != null)
                welcomePassword = JsonSerializer.Deserialize<JobSecrets>(protector.Unprotect(job.SecretsProtected, SecretPurposes.JobSecrets), AgentProtocol.Json)?.AdminPassword;
            job.Status = ProvisioningJobStatus.Succeeded;
            job.Error = null;
            job.FinishedAtUtc = now;
            job.SecretsProtected = null;
            var before = new { t.Status, t.ProvisionedAtUtc, t.RegistryEnabled, t.ArchiveLocation };
            switch (job.Type)
            {
                case ProvisioningJobType.Create:
                    t.ProvisionedAtUtc = now;
                    t.RegistryEnabled = c.RegistryEnabled ?? DesiredEnabled(t.Status);
                    break;
                case ProvisioningJobType.Reset:
                    t.RegistryEnabled = c.RegistryEnabled ?? true;
                    t.InstallationId = null; // yangi baza — ERP qayta register qiladi
                    break;
                case ProvisioningJobType.Suspend:
                    t.RegistryEnabled = false;
                    break;
                case ProvisioningJobType.Resume:
                    t.RegistryEnabled = true;
                    break;
                case ProvisioningJobType.Archive:
                    t.Status = TenantStatus.Archived;
                    t.RegistryEnabled = c.RegistryEnabled ?? false;
                    t.ArchiveLocation = Truncate(c.ArchiveLocation, 500);
                    break;
                case ProvisioningJobType.Delete:
                    t.DeletedAtUtc = now;
                    t.RegistryEnabled = null;
                    t.ArchiveLocation = null;
                    break;
            }
            audit.Add("provisioning.succeeded", "Tenant", t.Id, before,
                new { JobId = job.Id, job.Type, t.Status, t.ProvisionedAtUtc, t.RegistryEnabled, t.ArchiveLocation, t.DeletedAtUtc }, staffIdOverride: null);
        }
        else if (c.Outcome == JobOutcome.Retry && job.Attempts < job.MaxAttempts)
        {
            int delay = c.RetryAfterSeconds is > 0 ? c.RetryAfterSeconds.Value : (int)Math.Min(3600, 60 * Math.Pow(2, job.Attempts - 1));
            job.Status = ProvisioningJobStatus.Pending;
            job.NotBeforeUtc = now.AddSeconds(delay);
            job.Error = Truncate(c.Error, 2000);
            audit.Add("provisioning.retry", "Tenant", t.Id, null, new { JobId = job.Id, job.Type, job.Attempts, c.ExitCode, job.Error, RetryAtUtc = job.NotBeforeUtc }, staffIdOverride: null);
        }
        else
        {
            FinishFailed(job, c.Error ?? "Noma'lum xato", c.ExitCode);
            if (c.RegistryEnabled.HasValue) t.RegistryEnabled = c.RegistryEnabled;
        }
        try { await db.SaveChangesAsync(ct); }
        catch (Exception ex) when (BillingService.IsConcurrencyConflict(ex)) { return CompleteStatus.LeaseLost; }

        if (welcomePassword != null)
            await SendWelcomeAsync(t, job, welcomePassword, ct);
        return CompleteStatus.Ok;
    }

    private void FinishFailed(ProvisioningJob job, string error, int? exitCode)
    {
        job.Status = ProvisioningJobStatus.Failed;
        job.Error = Truncate(error, 2000);
        job.ExitCode = exitCode ?? job.ExitCode;
        job.FinishedAtUtc = clock.UtcNow;
        job.LeaseToken = null;
        job.LeaseUntilUtc = null;
        // Maxfiy qiymatlar qayta urinish (Retry) uchun qoladi: Create'da xodimga ko'rsatilgan parol o'zgarmasligi kerak.
        audit.Add("provisioning.failed", "Tenant", job.TenantId, null, new { JobId = job.Id, job.Type, job.Attempts, job.ExitCode, job.Error }, staffIdOverride: null);
    }

    /// <summary>Xodim: Failed job'ni qayta navbatga qo'yish (urinishlar hisobi yangidan).</summary>
    public async Task RetryAsync(ProvisioningJob job, CancellationToken ct = default)
    {
        if (job.Status != ProvisioningJobStatus.Failed) throw new DomainException("Faqat xato bilan tugagan job qayta ishga tushiriladi.");
        if (await db.ProvisioningJobs.AnyAsync(j => j.TenantId == job.TenantId && j.Id != job.Id
                && (j.Status == ProvisioningJobStatus.Pending || j.Status == ProvisioningJobStatus.Running), ct))
            throw new DomainException("Tenantda boshqa faol job bor.");
        if (job.Type == ProvisioningJobType.Create && await db.Tenants.AnyAsync(t => t.Id == job.TenantId && t.ProvisionedAtUtc != null, ct))
            throw new DomainException("Baza allaqachon yaratilgan.");
        job.Status = ProvisioningJobStatus.Pending;
        job.Attempts = 0;
        job.NotBeforeUtc = clock.UtcNow;
        job.FinishedAtUtc = null;
        AppendLog(job, $"--- {clock.UtcNow:yyyy-MM-dd HH:mm:ss} xodim qayta navbatga qo'ydi ---");
        audit.Add("provisioning.requeue", "Tenant", job.TenantId, null, new { JobId = job.Id, job.Type });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Xodim: hali boshlanmagan (Pending) yoki xato bergan job'ni bekor qilish. Running bekor qilinmaydi — yarim holat qolmasin.</summary>
    public async Task CancelAsync(ProvisioningJob job, CancellationToken ct = default)
    {
        if (job.Status is not (ProvisioningJobStatus.Pending or ProvisioningJobStatus.Failed))
            throw new DomainException("Faqat navbatdagi yoki xato bergan job bekor qilinadi.");
        var before = job.Status;
        job.Status = ProvisioningJobStatus.Cancelled;
        job.SecretsProtected = null;
        job.FinishedAtUtc = clock.UtcNow;
        audit.Add("provisioning.cancel", "Tenant", job.TenantId, new { Status = before }, new { JobId = job.Id, job.Type, job.Status });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Agent rejalashtiruvchisi: demo tenant kechki Reset'i (kalit bo'yicha bir marta).</summary>
    public async Task<ScheduleResponse> ScheduleAsync(Server server, ScheduleRequest r, CancellationToken ct = default)
    {
        if (r.Type != JobKind.Reset) throw new DomainException("Faqat Reset rejalashtiriladi.");
        if (string.IsNullOrWhiteSpace(r.Key) || r.Key.Length > 40) throw new DomainException("Kalit yaroqsiz.");
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.Slug == r.Slug, ct) ?? throw new DomainException("Tenant topilmadi.");
        if (t.ServerId != server.Id) throw new DomainException("Tenant bu serverda emas.");
        var res = await EnqueueAsync(t, ProvisioningJobType.Reset, "Kechki demo reset", $"demo-reset:{t.Slug}:{r.Key}", ct);
        return new ScheduleResponse { JobId = res.Job.Id, Created = res.Created };
    }

    private async Task SendWelcomeAsync(Tenant t, ProvisioningJob job, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(t.TelegramChatId)) return;
        var payload = JsonSerializer.Deserialize<JobPayload>(job.Payload, AgentProtocol.Json);
        string url = TenantUrl(t) ?? $"(manzil: {t.Slug}.<domen>)";
        string text = $"tahsinERP: «{t.LegalName}» uchun tizim tayyor.\nManzil: {url}\nLogin: {payload?.AdminLogin}\n" +
                      $"Vaqtinchalik parol: {password}\nBirinchi kirishda parolni almashtirish so'raladi.";
        // Notifications'ga parolsiz matn yoziladi.
        var n = new Notification
        {
            TenantId = t.Id, Template = "welcome", DedupKey = $"{t.Id}|welcome|job{job.Id}|{t.TelegramChatId}", Recipient = t.TelegramChatId,
            Message = text.Replace(password, "********"), CreatedAtUtc = clock.UtcNow,
        };
        if (!telegram.IsConfigured)
        {
            n.Channel = NotificationChannel.Log;
            n.Status = NotificationStatus.Logged;
        }
        else
        {
            n.Channel = NotificationChannel.Telegram;
            try { await telegram.SendAsync(t.TelegramChatId, text, ct); n.Status = NotificationStatus.Sent; }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                n.Status = NotificationStatus.Failed;
                n.Error = Truncate(ex.Message, 1000);
                log.LogWarning(ex, "Xush kelibsiz xabari yuborilmadi ({Slug})", t.Slug);
            }
        }
        db.Notifications.Add(n);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) { log.LogWarning(ex, "Xush kelibsiz xabari logga yozilmadi"); }
    }

    private async Task<bool> SaveIgnoringConflictAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException)
        {
            foreach (var e in db.ChangeTracker.Entries().ToList()) e.State = EntityState.Detached;
            return false;
        }
    }

    private static void AppendLog(ProvisioningJob job, string text)
    {
        string s = job.Log.Length == 0 ? text : job.Log + "\n" + text;
        job.Log = s.Length > MaxLogChars ? "…(qisqartirildi)\n" + s[^MaxLogChars..] : s;
    }

    private static string? Truncate(string? s, int max) => s is null ? null : s.Length > max ? s[..max] : s;
}
