using System.Net;
using System.Text.Json;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.ProvisioningAgent;

/// <summary>
/// Avto-yangilash (Docs/Deploy/03_AUTO_UPDATE.md): paneldan yangilash job'ini oladi, reliz manifesti/imzosi va artefaktni
/// panel orqali yuklab, Updater inbox'iga qo'yadi (request.json — oxirida), Updater status faylini kuzatib panelga jonli log
/// yuboradi. Agentning o'zi yangilanayotganda Updater uni to'xtatadi — kuzatilayotgan job StateDir\update-pending.json da
/// saqlanadi va qayta ishga tushganda davom ettiriladi. Updater holati (state.json) inventar sifatida panelga yuboriladi.
/// IIS/fayllarga bu yerda tegilmaydi — buni faqat Updater (LocalSystem) qiladi va paketni o'zi qayta tekshiradi.
/// </summary>
public sealed class UpdateRunner(AgentOptions options, SqlOps sql, TimeProvider time, ILogger<UpdateRunner> log)
{
    private sealed class Pending
    {
        public long JobId { get; set; }
        public string LeaseToken { get; set; } = "";
        public DateTime StartedUtc { get; set; }
    }

    private DateTime _nextInventoryUtc = DateTime.MinValue;

    /// <summary>Testlarda almashtiriladi; prod'da kodga joylangan kalitlar (Updater bilan bir xil).</summary>
    public IReadOnlyDictionary<string, string> TrustedKeys { get; init; } = ReleaseKeys.Trusted;

    private string PendingPath => Path.Combine(options.Paths.StateDir, "update-pending.json");
    private string InboxDir(long jobId) => Path.Combine(options.Updates.UpdatesDir, "Inbox", jobId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    private string StatusPath(long jobId) => Path.Combine(options.Updates.UpdatesDir, "Status", jobId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".json");
    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    /// <summary>Bitta aylanish. Job bilan ishlangan bo'lsa true.</summary>
    public async Task<bool> RunOnceAsync(PanelClient panel, CancellationToken ct)
    {
        if (!options.Updates.Enabled) return false;
        if (ReadPending() is { } p)
        {
            await FollowAsync(panel, p, ct);
            _nextInventoryUtc = DateTime.MinValue;
            return true;
        }
        var lease = await panel.UpdateLeaseAsync(ct);
        if (lease is null)
        {
            if (UtcNow >= _nextInventoryUtc) await SendInventoryAsync(panel, ct);
            return false;
        }
        log.LogInformation("Yangilash job #{JobId}: {Action} {App} {Slug} {Version}", lease.JobId, lease.Action, lease.App, lease.Slug, lease.Version);
        var pending = new Pending { JobId = lease.JobId, LeaseToken = lease.LeaseToken, StartedUtc = UtcNow };
        if (!await PrepareAsync(panel, lease, ct)) return true;
        WritePending(pending);
        await FollowAsync(panel, pending, ct);
        _nextInventoryUtc = DateTime.MinValue;
        return true;
    }

    private static bool NeedsPackage(UpdateAction a) => a is UpdateAction.InstallApp or UpdateAction.InstallTenant or UpdateAction.CreateInstance;

    /// <summary>Paketni yuklab inbox'ga qo'yadi. Xato bo'lsa job Failed bilan yopiladi va false.</summary>
    private async Task<bool> PrepareAsync(PanelClient panel, UpdateJobLease lease, CancellationToken ct)
    {
        string inbox = InboxDir(lease.JobId);
        try
        {
            if (Directory.Exists(inbox)) Directory.Delete(inbox, true);
            Directory.CreateDirectory(inbox);
            if (NeedsPackage(lease.Action))
            {
                var mr = await panel.ReleaseManifestAsync(lease.JobId, ct);
                byte[] manifest = Convert.FromBase64String(mr.Manifest);
                // Erta tekshiruv (keraksiz yuklashning oldini olish); hal qiluvchi tekshiruvni Updater qiladi
                var m = ReleaseVerifier.Verify(manifest, mr.Signature, TrustedKeys);
                string app = lease.Action == UpdateAction.InstallApp ? lease.App : ReleaseApps.Erp;
                var art = m.Find(app) ?? throw new ReleaseVerificationException($"Relizda {app} yo'q.");
                if (art.File != Path.GetFileName(art.File)) throw new ReleaseVerificationException("Artefakt nomi yaroqsiz.");
                await File.WriteAllBytesAsync(Path.Combine(inbox, "manifest.json"), manifest, ct);
                await File.WriteAllTextAsync(Path.Combine(inbox, "manifest.sig"), mr.Signature, ct);
                await TryProgressAsync(panel, lease.JobId, lease.LeaseToken, "Yuklab olish", $"{art.File} ({art.Size / 1024 / 1024} MB) yuklanmoqda…", ct);
                string zip = Path.Combine(inbox, art.File);
                await panel.DownloadArtifactAsync(lease.JobId, art.File, zip, ct);
                ReleaseVerifier.VerifyArtifact(art, zip);
            }
            var req = new InboxRequest
            {
                JobId = lease.JobId, Action = lease.Action, App = lease.App, Slug = lease.Slug, DbName = lease.DbName, Version = lease.Version,
                RestoreDatabase = lease.RestoreDatabase,
            };
            string tmp = Path.Combine(inbox, "request.json.tmp");
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(req, UpdaterFiles.Json), ct);
            File.Move(tmp, Path.Combine(inbox, "request.json"), overwrite: true);
            await TryProgressAsync(panel, lease.JobId, lease.LeaseToken, "Updater navbatida", "Paket Updater'ga topshirildi.", ct);
            return true;
        }
        catch (Exception ex) when (ex is ReleaseVerificationException or IOException or UnauthorizedAccessException or HttpRequestException
                                       or PanelClient.PanelException or FormatException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            log.LogWarning("Yangilash job #{JobId} tayyorlanmadi: {Message}", lease.JobId, ex.Message);
            TryDeleteDir(inbox);
            await CompleteWithRetryAsync(panel, lease.JobId, new UpdateCompletion
            {
                LeaseToken = lease.LeaseToken, Outcome = UpdateOutcome.Failed, Error = ex.Message, Log = "Agent: paket tayyorlanmadi — " + ex.Message,
            }, ct);
            return false;
        }
    }

    private async Task FollowAsync(PanelClient panel, Pending p, CancellationToken ct)
    {
        string statusPath = StatusPath(p.JobId);
        string? lastLog = null;
        DateTime lastSent = DateTime.MinValue;
        while (true)
        {
            var st = ReadStatus(statusPath);
            var elapsed = UtcNow - p.StartedUtc;
            if (st is null && elapsed > TimeSpan.FromSeconds(Math.Max(10, options.Updates.UpdaterStartTimeoutSeconds))
                && File.Exists(Path.Combine(InboxDir(p.JobId), "request.json")))
            {
                TryDeleteDir(InboxDir(p.JobId));
                await FinishAsync(panel, p, new UpdateCompletion
                {
                    LeaseToken = p.LeaseToken, Outcome = UpdateOutcome.Failed,
                    Error = "Updater javob bermadi — xizmat o'rnatilmagan yoki to'xtagan (Install-Updater.ps1).",
                }, statusPath, ct);
                return;
            }
            if (st is { Final: true })
            {
                await FinishAsync(panel, p, new UpdateCompletion
                {
                    LeaseToken = p.LeaseToken, Outcome = st.Outcome ?? UpdateOutcome.Failed, Error = st.Error, Log = st.Log,
                    InstalledVersion = st.InstalledVersion,
                }, statusPath, ct);
                return;
            }
            if (elapsed > TimeSpan.FromMinutes(Math.Max(5, options.Updates.JobTimeoutMinutes)))
            {
                await FinishAsync(panel, p, new UpdateCompletion
                {
                    LeaseToken = p.LeaseToken, Outcome = UpdateOutcome.Failed, Log = st?.Log,
                    Error = $"Updater {options.Updates.JobTimeoutMinutes} daqiqada tugatmadi — serverda holatni tekshiring.",
                }, statusPath, ct);
                return;
            }
            if (st != null && (st.Log != lastLog || UtcNow - lastSent > TimeSpan.FromMinutes(1)))
            {
                if (await TryProgressAsync(panel, p.JobId, p.LeaseToken, st.Stage, st.Log, ct))
                {
                    lastLog = st.Log;
                    lastSent = UtcNow;
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
        }
    }

    /// <summary>Natijani yuboradi. Panel vaqtincha yo'q bo'lsa (masalan o'zi yangilanmoqda) — pending saqlanadi, keyingi aylanishda qayta.</summary>
    private async Task FinishAsync(PanelClient panel, Pending p, UpdateCompletion c, string statusPath, CancellationToken ct)
    {
        await CompleteWithRetryAsync(panel, p.JobId, c, ct);
        ClearPending();
        try { File.Delete(statusPath); } catch (IOException) { }
        log.LogInformation("Yangilash job #{JobId}: {Outcome} {Error}", p.JobId, c.Outcome, c.Error);
    }

    /// <summary>Panel API tanasi ≤ 512 KB (JSON'da apostrof ' bo'lib 6 baravar o'sadi) — log'ning oxirgi qismi yuboriladi.</summary>
    private static string? Tail(string? log) => log is { Length: > 60_000 } ? "…\n" + log[^60_000..] : log;

    private async Task CompleteWithRetryAsync(PanelClient panel, long jobId, UpdateCompletion c, CancellationToken ct)
    {
        c.Log = Tail(c.Log);
        for (int i = 1; ; i++)
        {
            try
            {
                await panel.UpdateCompleteAsync(jobId, c, ct);
                return;
            }
            catch (PanelClient.PanelException ex) when (ex.Status is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
            {
                log.LogWarning("Yangilash job #{JobId}: panel natijani qabul qilmadi ({Message})", jobId, ex.Message);
                return;
            }
            catch (Exception ex) when (i < 20 && ex is HttpRequestException or PanelClient.PanelException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                log.LogWarning("Yangilash natijasi yuborilmadi ({Attempt}/20): {Message}", i, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, 3 * i)), ct);
            }
        }
    }

    private async Task<bool> TryProgressAsync(PanelClient panel, long jobId, string token, string? stage, string? logText, CancellationToken ct)
    {
        try
        {
            await panel.UpdateProgressAsync(jobId, new UpdateProgress { LeaseToken = token, Stage = stage, Log = Tail(logText) }, ct);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or PanelClient.PanelException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            log.LogDebug("Progress yuborilmadi: {Message}", ex.Message); // panel o'zi yangilanayotgan bo'lishi mumkin
            return false;
        }
    }

    public async Task SendInventoryAsync(PanelClient panel, CancellationToken ct)
    {
        _nextInventoryUtc = UtcNow.AddMinutes(Math.Max(1, options.Updates.InventoryMinutes));
        var inv = await CollectInventoryAsync(ct);
        try { await panel.InventoryAsync(inv, ct); }
        catch (PanelClient.PanelException ex) when (ex.Status == HttpStatusCode.NotFound)
        {
            log.LogDebug("Panel inventarni qabul qilmaydi (eski panel)");
        }
    }

    public async Task<UpdateInventory> CollectInventoryAsync(CancellationToken ct)
    {
        UpdaterState state;
        try
        {
            state = File.Exists(options.Updates.UpdaterStateFile)
                ? JsonSerializer.Deserialize<UpdaterState>(await File.ReadAllTextAsync(options.Updates.UpdaterStateFile, ct), UpdaterFiles.Json) ?? new UpdaterState()
                : new UpdaterState();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            log.LogWarning("Updater state.json o'qilmadi: {Message}", ex.Message);
            state = new UpdaterState();
        }
        var schema = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var db in state.Instances.Values.Select(i => i.DbName).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
            schema[db] = await sql.SchemaVersionAsync(db, ct);
        var inv = UpdaterFiles.ToInventory(state, state.UpdaterVersion, db => schema.GetValueOrDefault(db));
        inv.Backup = ReadBackupReport(options.Updates.BackupStatusFile);
        return inv;
    }

    /// <summary>Zaxira natijasi fayli (PowerShell yozadi). Yo'q yoki o'qilmasa — null.</summary>
    public static BackupReport? ReadBackupReport(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<BackupReport>(File.ReadAllText(path).TrimStart('﻿'), AgentProtocol.Json);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    private static JobStatus? ReadStatus(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<JobStatus>(File.ReadAllText(path), UpdaterFiles.Json) : null; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    private Pending? ReadPending()
    {
        try { return File.Exists(PendingPath) ? JsonSerializer.Deserialize<Pending>(File.ReadAllText(PendingPath), UpdaterFiles.Json) : null; }
        catch (Exception ex) when (ex is IOException or JsonException) { return null; }
    }

    private void WritePending(Pending p)
    {
        Directory.CreateDirectory(options.Paths.StateDir);
        File.WriteAllText(PendingPath, JsonSerializer.Serialize(p, UpdaterFiles.Json));
    }

    private void ClearPending()
    {
        try { File.Delete(PendingPath); } catch (IOException) { }
    }

    private static void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
