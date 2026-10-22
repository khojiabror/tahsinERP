using System.Globalization;
using System.IO.Compression;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.Updater;

public sealed record ExecutionResult(UpdateOutcome Outcome, string? Error, string? InstalledVersion);

/// <summary>
/// Bitta yangilash so'rovini bajaradi: imzo/hash → zaxira → to'xtatish → nusxa → (migratsiya) → ishga tushirish → health;
/// o'zgarish boshlangandan keyingi har qanday xatoda avtomatik rollback (fayllar + kerak bo'lsa baza .bak'dan).
/// Pool environmentVariables va ilova sirlariga tegilmaydi; ERP Web.config'dagi machineKey/appSettings saqlanadi.
/// </summary>
public sealed class UpdateExecutor(UpdaterOptions o, ISystemOps sys, StateStore store, ILogger<UpdateExecutor> log)
{
    /// <summary>split-erp: umumiy saytdan ko'chirilgan instance (versiyasi noma'lum).</summary>
    public const string LegacyVersion = "legacy";

    /// <summary>Testlarda almashtiriladi; prod'da faqat kodga joylangan kalitlar.</summary>
    public IReadOnlyDictionary<string, string> TrustedKeys { get; init; } = ReleaseKeys.Trusted;

    private sealed class RolledBackException(string message) : Exception(message);

    public async Task<ExecutionResult> ExecuteAsync(InboxRequest req, string jobDir, JobReporter rep, CancellationToken ct)
    {
        try
        {
            string? version = req.Action switch
            {
                UpdateAction.InstallApp => await InstallAppAsync(req, jobDir, rep, ct),
                UpdateAction.InstallTenant => await InstallTenantAsync(req, jobDir, rep, ct),
                UpdateAction.CreateInstance => await CreateInstanceAsync(req, jobDir, rep, ct),
                UpdateAction.RollbackApp => await RollbackAppAsync(req, rep, ct),
                UpdateAction.RollbackTenant => await RollbackTenantAsync(req, rep, ct),
                UpdateAction.MigrateTenant => await MigrateTenantAsync(req, rep, ct),
                _ => throw new UpdateRejectedException($"Noma'lum amal: {req.Action}"),
            };
            rep.Line("TAYYOR" + (version is null ? "" : ": " + version));
            return new ExecutionResult(UpdateOutcome.Succeeded, null, version);
        }
        catch (UpdateRejectedException ex)
        {
            rep.Line("RAD ETILDI (hech narsa o'zgarmadi): " + ex.Message);
            return new ExecutionResult(UpdateOutcome.Failed, ex.Message, null);
        }
        catch (ReleaseVerificationException ex)
        {
            rep.Line("IMZO/HASH XATOSI (hech narsa o'zgarmadi): " + ex.Message);
            return new ExecutionResult(UpdateOutcome.Failed, ex.Message, null);
        }
        catch (RolledBackException ex)
        {
            return new ExecutionResult(UpdateOutcome.RolledBack, ex.Message, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Job #{JobId}", req.JobId);
            rep.Line("XATO: " + ex.Message);
            return new ExecutionResult(UpdateOutcome.Failed, ex.Message, null);
        }
    }

    // ------------------------------------------------------------------ Server ilovalari

    private async Task<string> InstallAppAsync(InboxRequest req, string jobDir, JobReporter rep, CancellationToken ct)
    {
        string app = req.App.ToLowerInvariant();
        if (!ReleaseApps.ServerApps.Contains(app)) throw new UpdateRejectedException($"{app} bu yerdan o'rnatilmaydi.");
        var profile = o.App(app);
        string version = RequireVersion(req);
        rep.Stage("Imzo va hash tekshiruvi");
        string zip = VerifyPackage(jobDir, app, version, rep);
        string? current = store.Load().Apps.GetValueOrDefault(app)?.Version;
        if (!ReleaseVerifier.IsNewer(version, current))
            throw new UpdateRejectedException($"O'rnatilgan versiya {current} — {version} undan yangi emas (orqaga qaytish faqat Rollback bilan).");
        string releaseDir = Extract(app, version, zip, rep);
        var target = TargetOf(profile);

        rep.Stage("Zaxira");
        BackupEntry? backup = null;
        if (Directory.Exists(profile.Path))
        {
            backup = await BackupFilesAsync(app, profile.Path!, current, null, rep, ct);
            RememberBackup(s => s.Apps, app, backup, null);
        }
        else rep.Line($"{profile.Path} yo'q — birinchi o'rnatish, zaxira olinmaydi");

        rep.Stage("To'xtatish: " + target);
        await sys.StopAsync(target, ct);
        try
        {
            rep.Stage("Fayllar");
            await sys.CopyAsync(releaseDir, profile.Path!, false, profile.Exclude, null, ct);
            await RunCliAsync(profile, rep, ct);
            rep.Stage("Ishga tushirish");
            await sys.StartAsync(target, ct);
            rep.Stage("Health");
            if (!await WaitHealthyAsync(profile.HealthUrl, target, s => s == 200, rep, ct))
                throw new InvalidOperationException("Health tekshiruvi o'tmadi.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RollbackAfterFailureAsync(ex, rep, ct, async () =>
            {
                if (backup is null) throw new InvalidOperationException("Zaxira yo'q — rollback qilib bo'lmaydi.");
                await sys.StopAsync(target, ct);
                await RestoreAppFilesAsync(backup, profile, ct);
                await sys.StartAsync(target, ct);
                await WaitHealthyAsync(profile.HealthUrl, target, s => s < 500, rep, ct);
            });
        }
        store.Update(s =>
        {
            var t = s.Apps.TryGetValue(app, out var x) ? x : s.Apps[app] = new TargetState();
            t.PreviousVersion = t.Version;
            t.Version = version;
            t.InstalledAtUtc = DateTime.UtcNow;
            Prune(t);
        });
        return version;
    }

    private async Task<string?> RollbackAppAsync(InboxRequest req, JobReporter rep, CancellationToken ct)
    {
        string app = req.App.ToLowerInvariant();
        if (!ReleaseApps.ServerApps.Contains(app)) throw new UpdateRejectedException($"{app} bu yerdan qaytarilmaydi.");
        var profile = o.App(app);
        var state = store.Load().Apps.GetValueOrDefault(app);
        var backup = state?.Backups.LastOrDefault() ?? throw new UpdateRejectedException("Zaxira yo'q — qaytarib bo'lmaydi.");
        if (!Directory.Exists(backup.Path)) throw new UpdateRejectedException($"Zaxira papkasi yo'q: {backup.Path}");
        var target = TargetOf(profile);
        rep.Stage($"Qaytarish: {state!.Version} → {backup.FromVersion ?? "?"}");
        await sys.StopAsync(target, ct);
        await RestoreAppFilesAsync(backup, profile, ct);
        await sys.StartAsync(target, ct);
        rep.Stage("Health");
        if (!await WaitHealthyAsync(profile.HealthUrl, target, s => s < 500, rep, ct))
            rep.Line("OGOHLANTIRISH: health o'tmadi — qo'lda tekshiring.");
        ConsumeBackup(s => s.Apps, app, backup);
        return backup.FromVersion;
    }

    private async Task RunCliAsync(AppProfile profile, JobReporter rep, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(profile.Cli) || string.IsNullOrEmpty(profile.CliDll)) return;
        rep.Stage($"Buyruq: {profile.Cli}");
        var env = sys.PoolEnv(profile.Name!);
        var r = await sys.RunAsync(o.DotnetExe, [Path.Combine(profile.Path!, profile.CliDll), profile.Cli], env, profile.Path, ct);
        foreach (var line in Redact(r.Output, env.Values).Split('\n', StringSplitOptions.RemoveEmptyEntries).TakeLast(40)) rep.Line("  " + line.TrimEnd());
        if (r.ExitCode != 0) throw new InvalidOperationException($"{profile.Cli} xato bilan tugadi (kod {r.ExitCode}).");
    }

    private async Task RestoreAppFilesAsync(BackupEntry backup, AppProfile profile, CancellationToken ct) =>
        // .NET ilovalarida ma'lumot papkasi yo'q (ProgramData'da) — to'liq ko'zgu; loglar saqlanadi
        await sys.CopyAsync(backup.Path, profile.Path!, true, null, ["logs"], ct);

    // ------------------------------------------------------------------ ERP firma instance'lari

    private async Task<string> InstallTenantAsync(InboxRequest req, string jobDir, JobReporter rep, CancellationToken ct)
    {
        string slug = RequireSlug(req);
        string version = RequireVersion(req);
        var inst = store.Load().Instances.GetValueOrDefault(slug)
            ?? throw new UpdateRejectedException($"{slug} uchun ERP instance yo'q — avval «Instance yaratish».");
        rep.Stage("Imzo va hash tekshiruvi");
        string zip = VerifyPackage(jobDir, ReleaseApps.Erp, version, rep);
        if (!ReleaseVerifier.IsNewer(version, inst.Version))
            throw new UpdateRejectedException($"{slug}: o'rnatilgan {inst.Version} — {version} undan yangi emas (orqaga — faqat Rollback).");
        string releaseDir = Extract(ReleaseApps.Erp, version, zip, rep);
        string path = o.InstancePath(slug);
        var target = new Target(TargetKind.Pool, o.InstancePool(slug));
        string? db = inst.DbName;

        rep.Stage("Zaxira");
        var backup = await BackupFilesAsync("erp-" + slug, path, inst.Version, db, rep, ct);
        RememberBackup(s => s.Instances, slug, backup, db);

        rep.Stage("To'xtatish: " + target);
        await sys.StopAsync(target, ct);
        bool migrating = false;
        try
        {
            rep.Stage("Fayllar");
            var preserved = WebConfigMerger.Capture(Path.Combine(path, "Web.config"), o.Erp.PreserveAppSettings);
            await sys.CopyAsync(Path.Combine(releaseDir, "app"), path, false, null, null, ct);
            WebConfigMerger.Apply(Path.Combine(path, "Web.config"), preserved);
            rep.Line(preserved.HasMachineKey ? "Web.config: machineKey saqlandi" : "Web.config: machineKey yangi yaratildi");
            if (db != null)
            {
                migrating = true;
                await MigrateAsync(releaseDir, db, rep, ct);
            }
            rep.Stage("Ishga tushirish");
            await sys.StartAsync(target, ct);
            rep.Stage("Health");
            if (!await WaitHealthyAsync(ErpHealthUrl(slug), target, s => s == 200, rep, ct))
                throw new InvalidOperationException("Health tekshiruvi o'tmadi.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RollbackAfterFailureAsync(ex, rep, ct, async () =>
            {
                await sys.StopAsync(target, ct);
                await RestoreErpFilesAsync(backup, path, ct);
                if (migrating && db != null && backup.DbBackupFile != null)
                {
                    rep.Line($"Baza {db} zaxiradan tiklanmoqda");
                    await sys.RestoreDbAsync(db, backup.DbBackupFile, ct);
                }
                await sys.StartAsync(target, ct);
                await WaitHealthyAsync(ErpHealthUrl(slug), target, s => s < 500, rep, ct);
            });
        }
        store.Update(s =>
        {
            var t = s.Instances[slug];
            t.PreviousVersion = t.Version;
            t.Version = version;
            t.InstalledAtUtc = DateTime.UtcNow;
            Prune(t);
        });
        await SyncDatabaseRootAsync(version, releaseDir, rep, ct);
        return version;
    }

    private async Task<string> CreateInstanceAsync(InboxRequest req, string jobDir, JobReporter rep, CancellationToken ct)
    {
        string slug = RequireSlug(req);
        string version = RequireVersion(req);
        if (req.DbName is { } dbn && !UpdaterOptions.IsValidDb(dbn)) throw new UpdateRejectedException($"Baza nomi yaroqsiz: {dbn}");
        string? db = req.DbName;
        string path = o.InstancePath(slug);
        if (store.Load().Instances.ContainsKey(slug)) throw new UpdateRejectedException($"{slug} instance'i allaqachon bor.");
        if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
            throw new UpdateRejectedException($"{path} bo'sh emas — avval qo'lda tekshiring.");
        bool legacy = version == LegacyVersion;
        string source;
        string? releaseDir = null;
        if (legacy)
        {
            if (!Directory.Exists(o.Erp.SharedPath)) throw new UpdateRejectedException($"Umumiy ERP papkasi yo'q: {o.Erp.SharedPath}");
            source = o.Erp.SharedPath;
        }
        else
        {
            rep.Stage("Imzo va hash tekshiruvi");
            string zip = VerifyPackage(jobDir, ReleaseApps.Erp, version, rep);
            releaseDir = Extract(ReleaseApps.Erp, version, zip, rep);
            source = Path.Combine(releaseDir, "app");
        }
        string pool = o.InstancePool(slug);
        var target = new Target(TargetKind.Pool, pool);
        bool siteCreated = false;
        string? dbBackup = null;
        bool migrated = false;
        try
        {
            rep.Stage("Fayllar → " + path);
            Directory.CreateDirectory(path);
            // App_Data\DstFiles — firmalar yuklagan rasmlar: faqat shu firmaniki
            await sys.CopyAsync(source, path, false, null, ["DstFiles"], ct);
            string dst = Path.Combine(o.Erp.SharedPath, "App_Data", "DstFiles", slug);
            if (Directory.Exists(dst)) await sys.CopyAsync(dst, Path.Combine(path, "App_Data", "DstFiles", slug), false, null, null, ct);
            var preserved = WebConfigMerger.Capture(Path.Combine(o.Erp.SharedPath, "Web.config"), o.Erp.PreserveAppSettings);
            WebConfigMerger.Apply(Path.Combine(path, "Web.config"), preserved);
            rep.Line(preserved.HasMachineKey ? "Web.config: machineKey umumiy saytdan (kirgan foydalanuvchilar chiqib ketmaydi)" : "Web.config: yangi machineKey");

            // Pool avval: IIS APPPOOL\<pool> hisobi pool yaratilgandan keyingina mavjud (SQL login undan oldin xato beradi).
            // Pool'ga sayt hali bog'lanmagan — trafik umumiy saytda qoladi.
            var spec = new ErpSiteSpec(pool, path, o.InstanceHost(slug), o.Erp.SharedSite, o.Erp.IdleTimeoutMinutes, o.Paths.DataDir);
            rep.Stage("IIS: pool " + pool);
            siteCreated = true; // rollback: sayt/pool o'chiriladi
            await sys.EnsureErpPoolAsync(spec, ct);

            if (db != null)
            {
                rep.Stage("SQL huquqi: IIS APPPOOL\\" + pool);
                await sys.GrantDbAccessAsync(pool, db, ct);
                if (releaseDir != null)
                {
                    dbBackup = DbBackupPath(db);
                    rep.Line($"Baza zaxirasi: {dbBackup}");
                    await sys.BackupDbAsync(db, dbBackup, ct);
                    migrated = true;
                    await MigrateAsync(releaseDir, db, rep, ct);
                }
            }

            // Sayt yaratilishi bilan https://<slug>.<domen> umumiy saytdan shu instance'ga o'tadi — eng oxirida
            rep.Stage("IIS: sayt va pool " + pool);
            await sys.EnsureErpSiteAsync(spec, ct);
            await sys.StartAsync(target, ct);
            rep.Stage("Health");
            if (!await WaitHealthyAsync(ErpHealthUrl(slug), target, s => legacy ? s < 500 : s == 200, rep, ct))
                throw new InvalidOperationException("Health tekshiruvi o'tmadi.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RollbackAfterFailureAsync(ex, rep, ct, async () =>
            {
                if (siteCreated)
                {
                    rep.Line("Sayt va pool o'chirilmoqda — firma umumiy saytda qoladi");
                    await sys.RemoveErpSiteAsync(pool, ct);
                }
                if (migrated && db != null && dbBackup != null)
                {
                    rep.Line($"Baza {db} zaxiradan tiklanmoqda");
                    await sys.RestoreDbAsync(db, dbBackup, ct);
                }
                TryDelete(path);
            });
        }
        store.Update(s => s.Instances[slug] = new TargetState { Version = version, DbName = db, InstalledAtUtc = DateTime.UtcNow });
        if (releaseDir != null) await SyncDatabaseRootAsync(version, releaseDir, rep, ct);
        return version;
    }

    private async Task<string?> RollbackTenantAsync(InboxRequest req, JobReporter rep, CancellationToken ct)
    {
        string slug = RequireSlug(req);
        var inst = store.Load().Instances.GetValueOrDefault(slug) ?? throw new UpdateRejectedException($"{slug} instance'i yo'q.");
        var backup = inst.Backups.LastOrDefault() ?? throw new UpdateRejectedException("Zaxira yo'q — qaytarib bo'lmaydi.");
        if (!Directory.Exists(backup.Path)) throw new UpdateRejectedException($"Zaxira papkasi yo'q: {backup.Path}");
        if (req.RestoreDatabase && (backup.DbBackupFile is null || !File.Exists(backup.DbBackupFile)))
            throw new UpdateRejectedException("Baza zaxirasi yo'q — faqat fayllarni qaytarish mumkin.");
        string path = o.InstancePath(slug);
        var target = new Target(TargetKind.Pool, o.InstancePool(slug));
        rep.Stage($"Qaytarish: {inst.Version} → {backup.FromVersion ?? "?"}");
        await sys.StopAsync(target, ct);
        await RestoreErpFilesAsync(backup, path, ct);
        if (req.RestoreDatabase)
        {
            rep.Line($"Baza {inst.DbName} {backup.CreatedAtUtc:yyyy-MM-dd HH:mm} UTC holatiga tiklanmoqda (keyingi ma'lumotlar yo'qoladi)");
            await sys.RestoreDbAsync(inst.DbName!, backup.DbBackupFile!, ct);
        }
        await sys.StartAsync(target, ct);
        rep.Stage("Health");
        if (!await WaitHealthyAsync(ErpHealthUrl(slug), target, s => s < 500, rep, ct))
            rep.Line("OGOHLANTIRISH: health o'tmadi — qo'lda tekshiring.");
        ConsumeBackup(s => s.Instances, slug, backup);
        return backup.FromVersion;
    }

    private async Task<string?> MigrateTenantAsync(InboxRequest req, JobReporter rep, CancellationToken ct)
    {
        string slug = RequireSlug(req);
        var inst = store.Load().Instances.GetValueOrDefault(slug) ?? throw new UpdateRejectedException($"{slug} instance'i yo'q.");
        if (inst.DbName is null) throw new UpdateRejectedException("Instance bazasi noma'lum.");
        string releaseDir = Path.Combine(o.Paths.ReleasesDir, ReleaseApps.Erp, inst.Version ?? "");
        if (inst.Version == LegacyVersion || !File.Exists(Path.Combine(releaseDir, ".complete")))
            throw new UpdateRejectedException($"{inst.Version} relizi serverda yo'q — avval relizni o'rnating.");
        var target = new Target(TargetKind.Pool, o.InstancePool(slug));
        string bak = DbBackupPath(inst.DbName);
        rep.Stage("Baza zaxirasi");
        await sys.BackupDbAsync(inst.DbName, bak, ct);
        await sys.StopAsync(target, ct);
        try
        {
            await MigrateAsync(releaseDir, inst.DbName, rep, ct);
            await sys.StartAsync(target, ct);
            if (!await WaitHealthyAsync(ErpHealthUrl(slug), target, s => s == 200, rep, ct))
                throw new InvalidOperationException("Health tekshiruvi o'tmadi.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RollbackAfterFailureAsync(ex, rep, ct, async () =>
            {
                await sys.StopAsync(target, ct);
                await sys.RestoreDbAsync(inst.DbName, bak, ct);
                await sys.StartAsync(target, ct);
            });
        }
        TryDeleteFile(bak);
        return inst.Version;
    }

    private async Task MigrateAsync(string releaseDir, string db, JobReporter rep, CancellationToken ct)
    {
        if (!UpdaterOptions.IsValidDb(db)) throw new UpdateRejectedException($"Baza nomi yaroqsiz: {db}");
        string script = Path.Combine(releaseDir, "database", "tools", "Invoke-DbMigrations.ps1");
        if (!File.Exists(script)) throw new InvalidOperationException("Relizda database\\tools\\Invoke-DbMigrations.ps1 yo'q.");
        rep.Stage("Migratsiya: " + db);
        var r = await sys.RunAsync(o.PowerShellExe,
            // -Seed: yangi ruxsat modullari seed'da keladi (idempotent) — 01_DEPLOY_GUIDE.md 6-bo'lim
            ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, "-Server", o.SqlServer, "-Database", db, "-Seed"], null, null, ct);
        foreach (var line in r.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).TakeLast(60)) rep.Line("  " + line.TrimEnd());
        if (r.ExitCode != 0) throw new InvalidOperationException($"Migratsiya xatosi (kod {r.ExitCode}).");
    }

    private async Task RestoreErpFilesAsync(BackupEntry backup, string path, CancellationToken ct)
    {
        // App_Data (yuklangan fayllar) o'chirilmasin — /E; bin esa aynan zaxiradagidek (yangi DLL'lar qolmasin)
        await sys.CopyAsync(backup.Path, path, false, null, null, ct);
        string bin = Path.Combine(backup.Path, "bin");
        if (Directory.Exists(bin)) await sys.CopyAsync(bin, Path.Combine(path, "bin"), true, null, null, ct);
    }

    /// <summary>Agentning Database papkasi (yangi firma bazalari) eng yangi o'rnatilgan ERP versiyasiga tenglashtiriladi.</summary>
    private async Task SyncDatabaseRootAsync(string version, string releaseDir, JobReporter rep, CancellationToken ct)
    {
        var max = store.Load().Instances.Values.Select(i => ReleaseVerifier.ParseVersion(i.Version)).Where(v => v != null).Max();
        if (max is null || ReleaseVerifier.ParseVersion(version) < max) return;
        string src = Path.Combine(releaseDir, "database");
        if (!Directory.Exists(src)) return;
        await sys.CopyAsync(src, o.Paths.DatabaseRoot, true, null, null, ct);
        rep.Line($"Database papkasi {version} ga yangilandi ({o.Paths.DatabaseRoot})");
    }

    private string ErpHealthUrl(string slug) => $"https://{o.InstanceHost(slug)}{o.Erp.HealthPath}";

    // ------------------------------------------------------------------ Umumiy

    private async Task RollbackAfterFailureAsync(Exception error, JobReporter rep, CancellationToken ct, Func<Task> rollback)
    {
        rep.Line("XATO: " + error.Message);
        rep.Stage("Avtomatik rollback");
        try
        {
            await rollback();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Rollback xatosi");
            rep.Line("ROLLBACK HAM XATO: " + ex.Message + " — qo'lda aralashuv kerak!");
            throw new InvalidOperationException($"{error.Message}; rollback xatosi: {ex.Message}");
        }
        rep.Line("Rollback bajarildi — oldingi holat tiklandi.");
        throw new RolledBackException(error.Message);
    }

    /// <summary>manifest.json + manifest.sig + &lt;app&gt;.zip ni tekshiradi; ZIP yo'lini qaytaradi.</summary>
    public string VerifyPackage(string jobDir, string app, string version, JobReporter rep)
    {
        string mPath = Path.Combine(jobDir, "manifest.json"), sPath = Path.Combine(jobDir, "manifest.sig");
        if (!File.Exists(mPath) || !File.Exists(sPath)) throw new ReleaseVerificationException("manifest.json yoki manifest.sig yo'q.");
        var m = ReleaseVerifier.Verify(File.ReadAllBytes(mPath), File.ReadAllText(sPath), TrustedKeys);
        if (m.Version != version) throw new ReleaseVerificationException($"Manifest versiyasi {m.Version}, so'ralgan {version}.");
        var art = m.Find(app) ?? throw new ReleaseVerificationException($"Relizda {app} yo'q.");
        if (art.File != Path.GetFileName(art.File) || !art.File.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new ReleaseVerificationException($"Artefakt nomi yaroqsiz: {art.File}");
        string zip = Path.Combine(jobDir, art.File);
        ReleaseVerifier.VerifyArtifact(art, zip);
        rep.Line($"Imzo OK ({m.Version}, commit {m.Commit?[..Math.Min(8, m.Commit.Length)]}), {art.File} SHA-256 OK");
        return zip;
    }

    /// <summary>Tekshirilgan ZIP'ni Releases\&lt;app&gt;\&lt;versiya&gt; ga ochadi (bor bo'lsa qayta ishlatadi).</summary>
    private string Extract(string app, string version, string zip, JobReporter rep)
    {
        string dir = Path.Combine(o.Paths.ReleasesDir, app, version);
        if (File.Exists(Path.Combine(dir, ".complete")))
        {
            rep.Line($"Reliz allaqachon ochilgan: {dir}");
            return dir;
        }
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        string tmp = dir + ".tmp-" + Guid.NewGuid().ToString("N")[..8];
        ZipFile.ExtractToDirectory(zip, tmp); // .NET zip-slip'dan himoyalangan (yo'l tashqariga chiqsa — istisno)
        if (app == ReleaseApps.Erp && !File.Exists(Path.Combine(tmp, "app", "bin", "tahsinERP.dll")))
        {
            Directory.Delete(tmp, true);
            throw new ReleaseVerificationException("ERP paketi tuzilishi noto'g'ri (app\\bin\\tahsinERP.dll yo'q).");
        }
        Directory.Move(tmp, dir);
        File.WriteAllText(Path.Combine(dir, ".complete"), DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        rep.Line($"Ochildi: {dir}");
        return dir;
    }

    private async Task<BackupEntry> BackupFilesAsync(string key, string path, string? fromVersion, string? db, JobReporter rep, CancellationToken ct)
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var b = new BackupEntry
        {
            Path = Path.Combine(o.Paths.BackupRoot, $"{key}-{stamp}"), FromVersion = fromVersion, CreatedAtUtc = DateTime.UtcNow,
        };
        // DstFiles (yuklangan rasmlar) reliz bilan almashtirilmaydi — zaxiraga olinmaydi
        await sys.CopyAsync(path, b.Path, false, null, ["DstFiles", "logs"], ct);
        rep.Line($"Fayllar zaxirasi: {b.Path}");
        if (db != null)
        {
            b.DbBackupFile = DbBackupPath(db);
            await sys.BackupDbAsync(db, b.DbBackupFile, ct);
            rep.Line($"Baza zaxirasi: {b.DbBackupFile}");
        }
        return b;
    }

    private string DbBackupPath(string db) =>
        Path.Combine(o.Paths.DbBackupDir, $"update-{db}-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.bak");

    private void RememberBackup(Func<UpdaterState, Dictionary<string, TargetState>> pick, string key, BackupEntry b, string? db) =>
        store.Update(s =>
        {
            var d = pick(s);
            if (!d.TryGetValue(key, out var t)) d[key] = t = new TargetState { DbName = db };
            t.Backups.Add(b);
        });

    private void ConsumeBackup(Func<UpdaterState, Dictionary<string, TargetState>> pick, string key, BackupEntry b) =>
        store.Update(s =>
        {
            var t = pick(s)[key];
            t.PreviousVersion = t.Version;
            t.Version = b.FromVersion;
            t.InstalledAtUtc = DateTime.UtcNow;
            t.Backups.RemoveAll(x => x.Path == b.Path);
            TryDelete(b.Path);
            if (b.DbBackupFile != null) TryDeleteFile(b.DbBackupFile);
        });

    private void Prune(TargetState t)
    {
        while (t.Backups.Count > Math.Max(1, o.KeepBackups))
        {
            var old = t.Backups[0];
            t.Backups.RemoveAt(0);
            TryDelete(old.Path);
            if (old.DbBackupFile != null) TryDeleteFile(old.DbBackupFile);
        }
    }

    private async Task<bool> WaitHealthyAsync(string? url, Target target, Func<int, bool> ok, JobReporter rep, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(5, o.HealthTimeoutSeconds));
        int? last = null;
        while (true)
        {
            if (url is null)
            {
                string? st = sys.State(target);
                if (st is "Running" or "Started") { rep.Line($"{target}: {st}"); return true; }
                last = null;
            }
            else
            {
                last = await sys.HttpStatusAsync(url, ct);
                if (last is int code && ok(code)) { rep.Line($"{url} → {code}"); return true; }
            }
            if (DateTime.UtcNow >= deadline)
            {
                rep.Line($"{url ?? target.ToString()} → {(last?.ToString(CultureInfo.InvariantCulture) ?? "javob yo'q")} ({o.HealthTimeoutSeconds} s)");
                return false;
            }
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
        }
    }

    private static Target TargetOf(AppProfile p) => new(p.IsService ? TargetKind.Service : TargetKind.Pool, p.Name!);

    private static string RequireVersion(InboxRequest r) =>
        r.Version is { } v && (v == LegacyVersion || ReleaseVerifier.ParseVersion(v) != null) ? v : throw new UpdateRejectedException("Versiya noto'g'ri.");

    private static string RequireSlug(InboxRequest r) =>
        UpdaterOptions.IsValidSlug(r.Slug) ? r.Slug! : throw new UpdateRejectedException($"Slug yaroqsiz: {r.Slug}");

    /// <summary>Pool env qiymatlarini (va ulanish satri qismlarini) hamda Password=/Pwd= qiymatlarini log'dan yashiradi.</summary>
    public static string Redact(string text, IEnumerable<string> secrets)
    {
        var parts = new List<string>();
        foreach (var s in secrets)
        {
            parts.Add(s);
            foreach (var seg in s.Split(';', StringSplitOptions.RemoveEmptyEntries))
                if (seg.IndexOf('=') is int i and > 0) parts.Add(seg[(i + 1)..].Trim());
        }
        foreach (var s in parts.Where(p => p.Length >= 6).Distinct().OrderByDescending(p => p.Length))
            text = text.Replace(s, "********", StringComparison.Ordinal);
        return PasswordRe.Replace(text, "$1********");
    }

    private static readonly System.Text.RegularExpressions.Regex PasswordRe =
        new(@"((?:password|pwd)\s*=\s*)[^;\s""']+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static void TryDeleteFile(string f)
    {
        try { File.Delete(f); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
