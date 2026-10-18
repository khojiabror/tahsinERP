using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.ProvisioningAgent;

/// <summary>
/// Bitta job'ni bajaradi. Har qadam idempotent: qayta urinishda bajarilgan qadamlar holati tekshirilib o'tkazib yuboriladi
/// (baza bor → tekshiriladi; tenants.json o'zgarmagan bo'lsa yozilmaydi; arxiv bor → qayta zaxira qilinmaydi).
/// Xato turlari: <see cref="TransientJobException"/> → Retry, <see cref="InvalidJobException"/> → Failed.
/// </summary>
public sealed class JobExecutor(AgentOptions options, SqlOps sql, ScriptRunner scripts, IBackupStore backups, ILogger<JobExecutor> logger)
{
    // 03_DB_PROVISIONING.md, 3-bo'lim
    private static readonly int[] RetryExitCodes = [3, 6, 8];

    private sealed class JobLog(ILogger logger, long jobId)
    {
        private readonly StringBuilder _sb = new();
        public void Info(string msg) => Write("INFO", msg);
        public void Warn(string msg) => Write("WARN", msg);
        public void Raw(string text) { if (!string.IsNullOrWhiteSpace(text)) _sb.Append(text.TrimEnd()).Append('\n'); }
        private void Write(string level, string msg)
        {
            _sb.Append(CultureInfo.InvariantCulture, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {msg}\n");
            logger.LogInformation("Job {JobId}: {Message}", jobId, msg);
        }
        public override string ToString() => _sb.ToString();
    }

    public TenantsFile Tenants() => new(options.ResolveTenantsFile(), options.Paths.BaseDomain);

    public async Task<JobCompletion> ExecuteAsync(JobLease lease, CancellationToken ct)
    {
        var log = new JobLog(logger, lease.JobId);
        var p = lease.Payload;
        var c = new JobCompletion { LeaseToken = lease.LeaseToken };
        var secrets = new[] { lease.Secrets?.AdminPassword, lease.Secrets?.RegToken, options.Demo.UserPassword }.Where(s => !string.IsNullOrEmpty(s)).Cast<string>().ToList();
        log.Info($"{lease.Type} {p.Slug} ({p.DbName}), urinish {lease.Attempt}/{lease.MaxAttempts}; SQL: {options.Sql.Server}" +
                 (p.SqlInstance != null && !string.Equals(p.SqlInstance, options.Sql.Server, StringComparison.OrdinalIgnoreCase) ? $" (panelda: {p.SqlInstance})" : ""));
        try
        {
            TenantsFile.ValidateSlug(p.Slug);
            TenantsFile.ValidateDatabase(p.DbName);
            switch (lease.Type)
            {
                case JobKind.Create: await CreateAsync(lease, c, log, ct); break;
                case JobKind.Reset: await ResetAsync(lease, c, log, ct); break;
                case JobKind.Suspend:
                case JobKind.Resume: await SetEnabledAsync(lease.Type == JobKind.Resume, p, c, log, ct); break;
                case JobKind.Archive: await ArchiveAsync(p, c, log, ct); break;
                case JobKind.Delete: await DeleteAsync(p, c, log, ct); break;
                default: throw new InvalidJobException($"Noma'lum job turi: {lease.Type}");
            }
            c.Outcome = JobOutcome.Succeeded;
            log.Info("TAYYOR");
        }
        catch (TransientJobException ex)
        {
            c.Outcome = JobOutcome.Retry;
            c.Error = ex.Message;
            log.Warn("vaqtinchalik xato, qayta uriniladi: " + ex.Message);
        }
        catch (SqlException ex)
        {
            c.Outcome = JobOutcome.Failed;
            c.Error = "SQL xatosi: " + ex.Message;
            log.Warn(c.Error);
        }
        catch (IOException ex)
        {
            c.Outcome = JobOutcome.Retry;
            c.Error = "Fayl xatosi: " + ex.Message;
            log.Warn(c.Error);
        }
        catch (Exception ex) when (ex is InvalidJobException or UnauthorizedAccessException or InvalidOperationException)
        {
            c.Outcome = JobOutcome.Failed;
            c.Error = ex.Message;
            log.Warn("xato: " + ex.Message);
        }
        c.Log = ScriptRunner.Redact(log.ToString(), secrets);
        c.Error = c.Error is null ? null : ScriptRunner.Redact(c.Error, secrets);
        return c;
    }

    // ---------------- Create ----------------
    private async Task CreateAsync(JobLease lease, JobCompletion c, JobLog log, CancellationToken ct)
    {
        var p = lease.Payload;
        string password = lease.Secrets?.AdminPassword ?? throw new InvalidJobException("Admin paroli berilmagan.");
        string regToken = lease.Secrets?.RegToken ?? throw new InvalidJobException("reg_token berilmagan.");

        if (await sql.DatabaseExistsAsync(p.DbName, ct))
            await AdoptExistingAsync(p, password, log, ct);
        else
        {
            var args = new List<string> { "-Server", options.Sql.Server, "-DbName", p.DbName, "-AdminLogin", p.AdminLogin,
                "-AdminFullName", string.IsNullOrWhiteSpace(p.AdminFullName) ? "Administrator" : p.AdminFullName! };
            if (!string.IsNullOrWhiteSpace(p.CompanyName)) { args.Add("-CompanyName"); args.Add(p.CompanyName!); }
            if (!string.IsNullOrWhiteSpace(p.AdminEmail)) { args.Add("-AdminEmail"); args.Add(p.AdminEmail!); }
            if (!string.IsNullOrWhiteSpace(options.Sql.User)) { args.Add("-SqlUser"); args.Add(options.Sql.User!); }
            log.Info("New-TenantDb.ps1 ishga tushirildi");
            var r = await scripts.RunAsync("New-TenantDb.ps1", args, new Dictionary<string, string?> { ["TAHSINERP_ADMIN_PASSWORD"] = password }, ct);
            log.Raw(r.Log);
            c.ExitCode = r.ExitCode;
            c.Result = r.Result?.ToJsonString();
            string err = (string?)r.Result?["error"] ?? $"chiqish kodi {r.ExitCode}";
            if (r.ExitCode == 7)
            {
                log.Warn("baza shu orada paydo bo'ldi (kod 7) — tekshirilmoqda");
                await AdoptExistingAsync(p, password, log, ct);
            }
            else if (RetryExitCodes.Contains(r.ExitCode)) throw new TransientJobException($"New-TenantDb kod {r.ExitCode}: {err}");
            else if (r.ExitCode != 0) throw new InvalidJobException($"New-TenantDb kod {r.ExitCode}: {err}");
        }

        log.Info(await sql.WriteRegTokenAsync(p.DbName, regToken, ct));
        bool changed = Tenants().Upsert(p.Slug, p.DbName, p.CompanyId, p.Enabled);
        log.Info($"tenants.json: {p.Slug} → {p.DbName}, enabled={p.Enabled.ToString().ToLowerInvariant()}" + (changed ? "" : " (o'zgarishsiz)"));
        c.RegistryEnabled = p.Enabled;
        log.Info(await sql.ProbeAsync(p.DbName, ct));
    }

    /// <summary>Baza allaqachon bor: oldingi urinish yaratgan tahsinERP bazasi bo'lsa davom etamiz, begona bo'lsa — to'xtaymiz.</summary>
    private async Task AdoptExistingAsync(JobPayload p, string password, JobLog log, CancellationToken ct)
    {
        var info = await sql.InspectAsync(p.DbName, p.AdminLogin, ct);
        if (!info.IsTahsin || !info.AdminExists)
            throw new InvalidJobException($"'{p.DbName}' bazasi allaqachon mavjud va bu job yaratgan tahsinERP bazasiga o'xshamaydi (seed/admin yo'q) — operator tekshirsin.");
        if (info.AdminMustChange && await sql.ResetTempPasswordAsync(p.DbName, p.AdminLogin, password, ct))
            log.Info("baza oldingi urinishda yaratilgan — admin vaqtinchalik paroli yangilandi (idempotent)");
        else
            log.Info("baza oldingi urinishda yaratilgan; admin parolini allaqachon almashtirgan — tegilmadi");
    }

    // ---------------- Reset (demo) ----------------
    private async Task ResetAsync(JobLease lease, JobCompletion c, JobLog log, CancellationToken ct)
    {
        var p = lease.Payload;
        if (!p.Demo || !string.Equals(p.Slug, options.Demo.Slug, StringComparison.OrdinalIgnoreCase))
            throw new InvalidJobException($"Reset faqat demo tenant ('{options.Demo.Slug}') uchun.");
        if (string.IsNullOrEmpty(options.Demo.UserPassword))
            throw new InvalidJobException("Demo:UserPassword sozlanmagan (agent env TAHSIN_AGENT_Demo__UserPassword).");
        string template = options.ResolveDemoTemplate();
        if (!File.Exists(template))
            await BuildDemoTemplateAsync(template, log, ct);

        log.Info($"RESTORE {p.DbName} ← {template}");
        await sql.RestoreAsync(p.DbName, template, ct);
        log.Info("shablondan keyingi migratsiyalar (Invoke-DbMigrations.ps1)");
        var args = new List<string> { "-Server", options.Sql.Server, "-Database", p.DbName };
        if (!string.IsNullOrWhiteSpace(options.Sql.User)) { args.Add("-SqlUser"); args.Add(options.Sql.User!); }
        var r = await scripts.RunAsync("Invoke-DbMigrations.ps1", args, new Dictionary<string, string?>(), ct);
        log.Raw(r.Log);
        c.ExitCode = r.ExitCode;
        if (RetryExitCodes.Contains(r.ExitCode)) throw new TransientJobException($"Invoke-DbMigrations kod {r.ExitCode}");
        if (r.ExitCode != 0) throw new InvalidJobException($"Invoke-DbMigrations kod {r.ExitCode}");

        await sql.EnsureDemoUserAsync(p.DbName, options.Demo.UserLogin, options.Demo.UserPassword, ct);
        log.Info($"demo foydalanuvchi: {options.Demo.UserLogin}");
        if (lease.Secrets?.RegToken is { Length: > 0 } token) log.Info(await sql.WriteRegTokenAsync(p.DbName, token, ct));
        Tenants().Upsert(p.Slug, p.DbName, p.CompanyId, true);
        c.RegistryEnabled = true;
        log.Info(await sql.ProbeAsync(p.DbName, ct));
    }

    /// <summary>Demo shabloni: New-TenantDb -Demo -NoAdmin -BackupTo (Database/Demo/*.sql), so'ng vaqtinchalik baza o'chiriladi.</summary>
    public Task BuildDemoTemplateAsync(string template, CancellationToken ct) => BuildDemoTemplateAsync(template, new JobLog(logger, 0), ct);

    private async Task BuildDemoTemplateAsync(string template, JobLog log, CancellationToken ct)
    {
        string buildDb = "DB_DEMO_TEMPLATE_BUILD";
        log.Info($"demo shabloni yo'q — yaratilmoqda: {template}");
        Directory.CreateDirectory(Path.GetDirectoryName(template)!);
        await sql.DropAsync(buildDb, ct);
        var args = new List<string> { "-Server", options.Sql.Server, "-DbName", buildDb, "-Demo", "-NoAdmin", "-RecoveryModel", "SIMPLE", "-BackupTo", template };
        if (!string.IsNullOrWhiteSpace(options.Sql.User)) { args.Add("-SqlUser"); args.Add(options.Sql.User!); }
        var r = await scripts.RunAsync("New-TenantDb.ps1", args, new Dictionary<string, string?>(), ct);
        log.Raw(r.Log);
        try
        {
            if (RetryExitCodes.Contains(r.ExitCode)) throw new TransientJobException($"Demo shablon: New-TenantDb kod {r.ExitCode}");
            if (r.ExitCode != 0) throw new InvalidJobException($"Demo shablon: New-TenantDb kod {r.ExitCode}: {(string?)r.Result?["error"]}");
        }
        finally { await sql.DropAsync(buildDb, ct); }
        log.Info("demo shabloni tayyor");
    }

    // ---------------- Suspend / Resume ----------------
    private Task SetEnabledAsync(bool enabled, JobPayload p, JobCompletion c, JobLog log, CancellationToken ct)
    {
        bool changed = Tenants().Upsert(p.Slug, p.DbName, p.CompanyId, enabled);
        log.Info($"tenants.json: {p.Slug} enabled={enabled.ToString().ToLowerInvariant()}" + (changed ? "" : " (allaqachon shunday)"));
        c.RegistryEnabled = enabled;
        return Task.CompletedTask;
    }

    // ---------------- Archive ----------------
    private async Task ArchiveAsync(JobPayload p, JobCompletion c, JobLog log, CancellationToken ct)
    {
        Tenants().Upsert(p.Slug, p.DbName, p.CompanyId, false);
        c.RegistryEnabled = false;
        log.Info("tenants.json: enabled=false (kirish yopildi)");
        if (await sql.DatabaseExistsAsync(p.DbName, ct))
        {
            Directory.CreateDirectory(options.Paths.BackupDir);
            string bak = Path.Combine(options.Paths.BackupDir, $"{p.DbName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak");
            log.Info($"BACKUP {p.DbName} → {bak} (COPY_ONLY, CHECKSUM) + VERIFYONLY");
            await sql.BackupAsync(p.DbName, bak, ct);
            c.ArchiveLocation = await backups.StoreAsync(bak, p.Slug, ct);
            log.Info($"arxiv: {c.ArchiveLocation}");
            await sql.DropAsync(p.DbName, ct);
            log.Info($"baza o'chirildi: {p.DbName}");
        }
        else
        {
            c.ArchiveLocation = await backups.FindLatestAsync(p.Slug, ct)
                ?? throw new InvalidJobException($"'{p.DbName}' bazasi ham, arxivi ham topilmadi — operator tekshirsin.");
            log.Info($"baza allaqachon yo'q, arxiv bor (oldingi urinish): {c.ArchiveLocation}");
        }
    }

    // ---------------- Delete ----------------
    private async Task DeleteAsync(JobPayload p, JobCompletion c, JobLog log, CancellationToken ct)
    {
        if (await sql.DatabaseExistsAsync(p.DbName, ct))
        {
            await sql.DropAsync(p.DbName, ct);
            log.Info($"baza o'chirildi: {p.DbName}");
        }
        int n = await backups.DeleteAllAsync(p.Slug, ct);
        log.Info($"arxiv fayllari o'chirildi: {n}");
        bool removed = Tenants().Remove(p.Slug);
        log.Info("tenants.json: " + (removed ? "yozuv o'chirildi" : "yozuv yo'q edi"));
        c.RegistryEnabled = null;
    }
}
