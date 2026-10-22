using System.Text.Json;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.Updater;

/// <summary>
/// Inbox\&lt;jobId&gt;\request.json ni kuzatadi va so'rovlarni ketma-ket bajaradi. Natija — Status\&lt;jobId&gt;.json;
/// bajarilgan so'rov papkasi Done\ ga ko'chiriladi (paket faylisiz). Bir vaqtda faqat bitta yangilash.
/// </summary>
public sealed class InboxWorker(UpdaterOptions options, UpdateExecutor executor, StateStore store, ILogger<InboxWorker> log) : BackgroundService
{
    public static readonly string Version = typeof(InboxWorker).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Updater {Version}: inbox {Inbox}, domen {Domain}", Version, options.Paths.Inbox, options.Domain);
        store.Update(s => s.UpdaterVersion = Version);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessPendingAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { log.LogError(ex, "Inbox aylanishi xatosi"); }
            try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.PollSeconds)), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task<int> ProcessPendingAsync(CancellationToken ct)
    {
        if (!Directory.Exists(options.Paths.Inbox)) return 0;
        int done = 0;
        foreach (var dir in Directory.GetDirectories(options.Paths.Inbox).OrderBy(d => d, StringComparer.Ordinal))
        {
            string reqPath = Path.Combine(dir, "request.json");
            if (!File.Exists(reqPath)) continue; // agent hali yozmoqda
            if (!long.TryParse(Path.GetFileName(dir), out long jobId)) { MoveToDone(dir); continue; }
            string statusPath = Path.Combine(options.Paths.Status, jobId + ".json");
            var rep = new JobReporter(statusPath, jobId, log);
            InboxRequest? req;
            try { req = JsonSerializer.Deserialize<InboxRequest>(File.ReadAllText(reqPath), StateStore.Json); }
            catch (JsonException) { req = null; }
            if (req is null || req.JobId != jobId)
            {
                rep.Line("request.json noto'g'ri");
                rep.Finish(UpdateOutcome.Failed, "request.json noto'g'ri", null);
            }
            else if (File.Exists(statusPath))
            {
                // Updater bajarish o'rtasida to'xtagan (server qayta yuklangan) — yarim holat ustiga qayta o'rnatilmaydi
                rep.Line("Updater bajarish o'rtasida to'xtagan — job qayta ishga tushirilmaydi, holatni qo'lda tekshiring (Rollback mumkin).");
                rep.Finish(UpdateOutcome.Failed, "Updater bajarish o'rtasida to'xtagan", null);
            }
            else
            {
                rep.Stage($"Boshlandi: {req.Action} {req.App} {req.Slug} {req.Version}");
                var r = await executor.ExecuteAsync(req, dir, rep, ct);
                rep.Finish(r.Outcome, r.Error, r.InstalledVersion);
            }
            MoveToDone(dir);
            done++;
        }
        return done;
    }

    private void MoveToDone(string dir)
    {
        try
        {
            foreach (var zip in Directory.GetFiles(dir, "*.zip")) File.Delete(zip); // reliz Releases\ da ochilgan
            string doneRoot = Path.Combine(options.Paths.UpdatesDir, "Done");
            Directory.CreateDirectory(doneRoot);
            string target = Path.Combine(doneRoot, Path.GetFileName(dir));
            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.Move(dir, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.LogWarning(ex, "{Dir} Done'ga ko'chirilmadi", dir);
            try { Directory.Delete(dir, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }
}
