using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.ControlPanel.Services;

/// <summary>Ishonchli reliz kalitlari (testlarda almashtiriladi).</summary>
public sealed class ReleaseTrust
{
    public IReadOnlyDictionary<string, string> Keys { get; init; } = ReleaseKeys.Trusted;
}

/// <summary>
/// Avto-yangilash (Docs/Deploy/03_AUTO_UPDATE.md): GitHub relizlarini keshlash (imzo tekshiruvi bilan), yangilash navbati
/// (UpdateJobs), agent API'si uchun lease/progress/complete va server inventari. Bajarishni serverdagi Updater qiladi.
/// </summary>
public sealed class UpdatesService(ControlDbContext db, IReleaseSource source, ReleaseTrust trust, AuditLogger audit, Clock clock,
    IOptions<UpdatesOptions> options, ILogger<UpdatesService> log)
{
    private UpdatesOptions O => options.Value;

    // ------------------------------------------------------------------ Relizlar

    /// <summary>GitHub'dan yangi relizlarni o'qib keshlaydi. Qaytaradi: yangi qo'shilganlar soni.</summary>
    public async Task<int> RefreshAsync(CancellationToken ct)
    {
        if (!source.IsConfigured) throw new DomainException("GitHub token sozlanmagan — relizlar o'qilmaydi (ControlPanel__Updates__GitHubToken).");
        var listing = await source.ListAsync(ct);
        var known = (await db.AppReleases.Select(r => r.Tag).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        int added = 0;
        foreach (var rel in listing.Where(r => !r.Draft && (O.IncludePrerelease || !r.Prerelease) && !known.Contains(r.Tag)))
        {
            var manifestAsset = rel.Assets.FirstOrDefault(a => a.Name == "manifest.json");
            var sigAsset = rel.Assets.FirstOrDefault(a => a.Name == "manifest.sig");
            if (manifestAsset is null || sigAsset is null || manifestAsset.Size > 256 * 1024 || sigAsset.Size > 4096) continue;
            byte[] manifest = await ReadSmallAsync(manifestAsset.Id, ct);
            string sig = Encoding.UTF8.GetString(await ReadSmallAsync(sigAsset.Id, ct));
            var row = new AppRelease
            {
                Tag = rel.Tag, Notes = Trim(rel.Body, 20_000), Prerelease = rel.Prerelease, PublishedAtUtc = rel.PublishedAtUtc ?? clock.UtcNow,
                ManifestBase64 = Convert.ToBase64String(manifest), Signature = sig, FetchedAtUtc = clock.UtcNow,
                AssetsJson = JsonSerializer.Serialize(rel.Assets.ToDictionary(a => a.Name, a => a.Id)),
            };
            try
            {
                var m = ReleaseVerifier.Verify(manifest, sig, trust.Keys);
                row.Version = m.Version;
                row.Commit = m.Commit;
                row.SignatureValid = true;
                row.Apps = string.Join(",", m.Artifacts.Select(a => a.App + (a.Changed ? "*" : "")));
                // Manifestdagi har artefakt relizda bo'lsin
                var missing = m.Artifacts.Where(a => rel.Assets.All(x => x.Name != a.File)).Select(a => a.File).ToList();
                if (missing.Count > 0) { row.SignatureValid = false; row.VerifyError = "Relizda fayl yo'q: " + string.Join(", ", missing); }
            }
            catch (ReleaseVerificationException ex)
            {
                row.Version = rel.Tag.TrimStart('v');
                row.SignatureValid = false;
                row.VerifyError = ex.Message;
                log.LogWarning("Reliz {Tag} imzosi yaroqsiz: {Message}", rel.Tag, ex.Message);
            }
            if (await db.AppReleases.AnyAsync(r => r.Version == row.Version, ct)) continue;
            db.AppReleases.Add(row);
            added++;
        }
        if (added > 0)
        {
            audit.Add("update.releases-fetched", "AppRelease", null, null, new { Added = added });
            await db.SaveChangesAsync(ct);
        }
        return added;
    }

    private async Task<byte[]> ReadSmallAsync(long assetId, CancellationToken ct)
    {
        await using var s = await source.OpenAssetAsync(assetId, ct);
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    /// <summary>Imzosi to'g'ri relizlar, yangisi birinchi.</summary>
    public async Task<List<AppRelease>> ValidReleasesAsync(CancellationToken ct = default) =>
        (await db.AppReleases.AsNoTracking().Where(r => r.SignatureValid).ToListAsync(ct))
            .OrderByDescending(r => ReleaseVerifier.ParseVersion(r.Version)).ToList();

    public static bool Contains(AppRelease r, string app) =>
        r.Apps.Split(',', StringSplitOptions.RemoveEmptyEntries).Any(a => a.TrimEnd('*').Equals(app, StringComparison.OrdinalIgnoreCase));

    public static bool ChangedIn(AppRelease r, string app) =>
        r.Apps.Split(',', StringSplitOptions.RemoveEmptyEntries).Any(a => a.Equals(app + "*", StringComparison.OrdinalIgnoreCase));

    /// <summary>installed'dan yangi va shu ilovani o'z ichiga olgan relizlar (yangisi birinchi).</summary>
    public static List<AppRelease> NewerThan(IEnumerable<AppRelease> valid, string app, string? installed) =>
        valid.Where(r => Contains(r, app) && ReleaseVerifier.IsNewer(r.Version, installed)).ToList();

    /// <summary>Taklif qilinadigan versiya: oraliqda ilova haqiqatan o'zgargan bo'lsa — eng yangisi, aks holda null.</summary>
    public static AppRelease? Offer(IEnumerable<AppRelease> valid, string app, string? installed)
    {
        var newer = NewerThan(valid, app, installed);
        return newer.Any(r => ChangedIn(r, app)) ? newer[0] : null;
    }

    // ------------------------------------------------------------------ Inventar

    public async Task<(UpdateInventory? Inventory, DateTime? ReportedAtUtc)> InventoryAsync(int serverId, CancellationToken ct = default)
    {
        var row = await db.ServerInventories.AsNoTracking().FirstOrDefaultAsync(x => x.ServerId == serverId, ct);
        if (row is null) return (null, null);
        try { return (JsonSerializer.Deserialize<UpdateInventory>(row.Json, AgentProtocol.Json), row.ReportedAtUtc); }
        catch (JsonException) { return (null, row.ReportedAtUtc); }
    }

    public async Task SaveInventoryAsync(Server server, UpdateInventory inv, CancellationToken ct)
    {
        var row = await db.ServerInventories.FirstOrDefaultAsync(x => x.ServerId == server.Id, ct);
        if (row is null) db.ServerInventories.Add(row = new ServerInventory { ServerId = server.Id });
        row.Json = JsonSerializer.Serialize(inv, AgentProtocol.Json);
        row.UpdaterVersion = inv.UpdaterVersion is { Length: <= 32 } v ? v : null;
        row.ReportedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ Navbat (xodim)

    public async Task<UpdateJob> EnqueueAsync(int serverId, UpdateAction action, string app, int? tenantId, string? version, bool restoreDatabase,
        CancellationToken ct = default)
    {
        app = app.ToLowerInvariant();
        var server = await db.Servers.FirstOrDefaultAsync(s => s.Id == serverId, ct) ?? throw new DomainException("Server topilmadi.");
        if (server.AgentSecretProtected is null) throw new DomainException("Bu serverda agent ulanmagan.");
        bool erp = action is UpdateAction.InstallTenant or UpdateAction.CreateInstance or UpdateAction.RollbackTenant or UpdateAction.MigrateTenant;
        if (erp) app = ReleaseApps.Erp;
        else if (!ReleaseApps.ServerApps.Contains(app)) throw new DomainException($"Noma'lum ilova: {app}");

        Tenant? tenant = null;
        if (erp)
        {
            tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct) ?? throw new DomainException("Tashkilot topilmadi.");
            if (tenant.ServerId != serverId) throw new DomainException("Tashkilot boshqa serverda.");
            if (tenant.DbName is null) throw new DomainException("Tashkilot bazasi hali yaratilmagan (provisioning).");
        }
        var (inv, _) = await InventoryAsync(serverId, ct);
        string? from = erp ? inv?.Instances.FirstOrDefault(i => i.Slug == tenant!.Slug)?.Version : inv?.Apps.FirstOrDefault(a => a.App == app)?.Version;
        bool hasInstance = erp && inv?.Instances.Any(i => i.Slug == tenant!.Slug) == true;
        switch (action)
        {
            case UpdateAction.InstallApp or UpdateAction.InstallTenant or UpdateAction.CreateInstance:
            {
                var rel = await db.AppReleases.AsNoTracking().FirstOrDefaultAsync(r => r.Version == version, ct)
                    ?? throw new DomainException($"Reliz {version} topilmadi.");
                if (!rel.SignatureValid) throw new DomainException($"Reliz {version} imzosi yaroqsiz — o'rnatilmaydi.");
                if (!Contains(rel, app)) throw new DomainException($"Reliz {version} da {app} yo'q.");
                if (action == UpdateAction.CreateInstance && hasInstance) throw new DomainException("Instance allaqachon bor.");
                if (action == UpdateAction.InstallTenant && !hasInstance) throw new DomainException("Instance yo'q — avval «Instance yaratish».");
                if (action != UpdateAction.CreateInstance && !ReleaseVerifier.IsNewer(version, from))
                    throw new DomainException($"O'rnatilgan {from} — {version} undan yangi emas (orqaga — Rollback).");
                break;
            }
            case UpdateAction.RollbackApp or UpdateAction.RollbackTenant or UpdateAction.MigrateTenant:
                version = null;
                if (erp && !hasInstance) throw new DomainException("Instance yo'q.");
                break;
            default:
                throw new DomainException("Noma'lum amal.");
        }
        string target = erp ? "erp:" + tenant!.Slug : app;
        var active = await db.UpdateJobs.Where(j => j.ServerId == serverId && (j.Status == UpdateJobStatus.Pending || j.Status == UpdateJobStatus.Running))
            .Select(j => new { j.App, j.Slug }).ToListAsync(ct);
        if (active.Any(j => (j.App == ReleaseApps.Erp ? "erp:" + j.Slug : j.App) == target))
            throw new DomainException("Shu ilova/firma uchun yangilash allaqachon navbatda.");

        var job = new UpdateJob
        {
            ServerId = serverId, Action = action, App = app, TenantId = tenant?.Id, Slug = tenant?.Slug, DbName = tenant?.DbName, Version = version,
            FromVersion = from, RestoreDatabase = action == UpdateAction.RollbackTenant && restoreDatabase, Status = UpdateJobStatus.Pending,
            CreatedByStaffId = audit.CurrentStaffId, CreatedAtUtc = clock.UtcNow,
        };
        db.UpdateJobs.Add(job);
        await db.SaveChangesAsync(ct);
        audit.Add("update.enqueue", "UpdateJob", job.Id, null, new { job.Action, job.App, job.Slug, job.Version, job.FromVersion, job.RestoreDatabase });
        await db.SaveChangesAsync(ct);
        return job;
    }

    public async Task CancelAsync(UpdateJob job, CancellationToken ct = default)
    {
        if (job.Status != UpdateJobStatus.Pending) throw new DomainException("Faqat navbatdagi (boshlanmagan) job bekor qilinadi.");
        job.Status = UpdateJobStatus.Cancelled;
        job.FinishedAtUtc = clock.UtcNow;
        audit.Add("update.cancel", "UpdateJob", job.Id);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Lease muddati o'tgan Running job'lar — agent bilan aloqa uzilgan (natija noma'lum).</summary>
    public async Task<int> ExpireStaleAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var stale = await db.UpdateJobs.Where(j => j.Status == UpdateJobStatus.Running && j.LeaseUntilUtc < now).ToListAsync(ct);
        foreach (var j in stale)
        {
            j.Status = UpdateJobStatus.Failed;
            j.Error = "Agentdan xabar kelmadi (lease muddati o'tdi) — serverda holatni tekshiring.";
            j.FinishedAtUtc = now;
        }
        if (stale.Count > 0) await db.SaveChangesAsync(ct);
        return stale.Count;
    }

    // ------------------------------------------------------------------ Agent API

    public async Task<UpdateJobLease?> LeaseAsync(Server server, CancellationToken ct)
    {
        await ExpireStaleAsync(ct);
        // Bir serverda bir vaqtda bitta yangilash
        if (await db.UpdateJobs.AnyAsync(j => j.ServerId == server.Id && j.Status == UpdateJobStatus.Running, ct)) return null;
        var job = await db.UpdateJobs.Where(j => j.ServerId == server.Id && j.Status == UpdateJobStatus.Pending).OrderBy(j => j.Id).FirstOrDefaultAsync(ct);
        if (job is null) return null;
        job.Status = UpdateJobStatus.Running;
        job.LeaseToken = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        job.LeaseUntilUtc = clock.UtcNow.AddMinutes(O.LeaseMinutes);
        job.StartedAtUtc = clock.UtcNow;
        job.Stage = "Agent oldi";
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return null; }
        return new UpdateJobLease
        {
            JobId = job.Id, LeaseToken = job.LeaseToken, Action = job.Action, App = job.App, Slug = job.Slug, DbName = job.DbName,
            Version = job.Version, RestoreDatabase = job.RestoreDatabase, LeaseUntilUtc = job.LeaseUntilUtc.Value,
        };
    }

    private async Task<UpdateJob?> LeasedAsync(Server server, long id, string token, CancellationToken ct)
    {
        var job = await db.UpdateJobs.FirstOrDefaultAsync(j => j.Id == id && j.ServerId == server.Id, ct);
        if (job is null || job.Status != UpdateJobStatus.Running || job.LeaseToken is null
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(job.LeaseToken), Encoding.ASCII.GetBytes(token ?? "")))
            return null;
        return job;
    }

    public async Task<ProvisioningService.CompleteStatus> ProgressAsync(Server server, long id, UpdateProgress p, CancellationToken ct)
    {
        var job = await LeasedAsync(server, id, p.LeaseToken, ct);
        if (job is null) return await NotFoundOrLostAsync(server, id, ct);
        if (p.Stage != null) job.Stage = Trim(p.Stage, 200);
        if (p.Log != null) job.Log = Trim(p.Log, 200_000, tail: true)!;
        job.LeaseUntilUtc = clock.UtcNow.AddMinutes(O.LeaseMinutes);
        await db.SaveChangesAsync(ct);
        return ProvisioningService.CompleteStatus.Ok;
    }

    public async Task<ProvisioningService.CompleteStatus> CompleteAsync(Server server, long id, UpdateCompletion c, CancellationToken ct)
    {
        var job = await LeasedAsync(server, id, c.LeaseToken, ct);
        if (job is null) return await NotFoundOrLostAsync(server, id, ct);
        job.Status = c.Outcome switch
        {
            UpdateOutcome.Succeeded => UpdateJobStatus.Succeeded,
            UpdateOutcome.RolledBack => UpdateJobStatus.RolledBack,
            _ => UpdateJobStatus.Failed,
        };
        if (c.Log != null) job.Log = Trim(c.Log, 200_000, tail: true)!;
        job.Error = Trim(c.Error, 2000);
        job.InstalledVersion = c.InstalledVersion is { Length: <= 32 } v ? v : null;
        job.Stage = job.Status switch
        {
            UpdateJobStatus.Succeeded => "Tayyor", UpdateJobStatus.RolledBack => "Rollback qilindi", _ => "Xato",
        };
        job.FinishedAtUtc = clock.UtcNow;
        job.LeaseToken = null;
        audit.Add("update.complete", "UpdateJob", job.Id, null, new { job.Action, job.App, job.Slug, job.Version, job.Status, job.Error });
        await db.SaveChangesAsync(ct);
        return ProvisioningService.CompleteStatus.Ok;
    }

    private async Task<ProvisioningService.CompleteStatus> NotFoundOrLostAsync(Server server, long id, CancellationToken ct) =>
        await db.UpdateJobs.AnyAsync(j => j.Id == id && j.ServerId == server.Id, ct)
            ? ProvisioningService.CompleteStatus.LeaseLost : ProvisioningService.CompleteStatus.NotFound;

    public async Task<ReleaseManifestResponse?> ManifestForJobAsync(Server server, long id, CancellationToken ct)
    {
        var job = await db.UpdateJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id && j.ServerId == server.Id && j.Status == UpdateJobStatus.Running, ct);
        if (job?.Version is null) return null;
        var rel = await db.AppReleases.AsNoTracking().FirstOrDefaultAsync(r => r.Version == job.Version && r.SignatureValid, ct);
        return rel is null ? null : new ReleaseManifestResponse { Version = rel.Version, Manifest = rel.ManifestBase64, Signature = rel.Signature };
    }

    /// <summary>Job relizining artefakti (faqat shu job ilovasiniki) — GitHub'dan oqim.</summary>
    public async Task<Stream?> OpenArtifactForJobAsync(Server server, long id, string file, CancellationToken ct)
    {
        var job = await db.UpdateJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id && j.ServerId == server.Id && j.Status == UpdateJobStatus.Running, ct);
        if (job?.Version is null) return null;
        var rel = await db.AppReleases.AsNoTracking().FirstOrDefaultAsync(r => r.Version == job.Version && r.SignatureValid, ct);
        if (rel is null) return null;
        var m = ReleaseVerifier.Verify(Convert.FromBase64String(rel.ManifestBase64), rel.Signature, trust.Keys);
        var art = m.Find(job.App);
        if (art is null || art.File != file) return null;
        var assets = JsonSerializer.Deserialize<Dictionary<string, long>>(rel.AssetsJson) ?? [];
        if (!assets.TryGetValue(file, out long assetId)) return null;
        return await source.OpenAssetAsync(assetId, ct);
    }

    private static string? Trim(string? s, int max, bool tail = false) =>
        s is null || s.Length <= max ? s : tail ? "…" + s[^(max - 1)..] : s[..max];
}

/// <summary>Relizlarni fon rejimida tekshiradi (RefreshMinutes) va osilib qolgan job'larni yopadi.</summary>
public sealed class ReleaseRefreshHostedService(IServiceScopeFactory scopes, IOptions<UpdatesOptions> options, ILogger<ReleaseRefreshHostedService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.RefreshMinutes <= 0) return;
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); } catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<UpdatesService>();
                await svc.ExpireStaleAsync(stoppingToken);
                if (scope.ServiceProvider.GetRequiredService<IReleaseSource>().IsConfigured)
                {
                    int n = await svc.RefreshAsync(stoppingToken);
                    if (n > 0) log.LogInformation("Yangi relizlar: {Count}", n);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning("Relizlarni tekshirish xatosi: {Message}", ex.Message);
            }
            try { await Task.Delay(TimeSpan.FromMinutes(options.Value.RefreshMinutes), stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
