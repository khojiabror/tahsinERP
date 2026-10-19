using System.Net;
using System.Text.Json;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.ProvisioningAgent;

/// <summary>
/// Demo tenant kechki reset rejalashtiruvchisi: TimeZone bo'yicha ResetHour..ResetHour+ResetWindowHours oralig'ida, kuniga bir marta
/// panelga Reset job so'rovi (kalit = sana — panel takrorlamaydi). Oxirgi sana StateDir\agent-state.json da.
/// </summary>
public sealed class DemoScheduler(AgentOptions options, TimeProvider time, ILogger<DemoScheduler> log)
{
    private sealed class State { public string? LastDemoResetDate { get; set; } }

    private string StatePath => Path.Combine(options.Paths.StateDir, "agent-state.json");

    /// <summary>Hozir so'rov yuborish kerakmi — kerak bo'lsa sana kaliti, aks holda null.</summary>
    public string? DueKey()
    {
        if (!options.Demo.Enabled || string.IsNullOrWhiteSpace(options.Demo.Slug)) return null;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(options.Demo.TimeZone);
        var local = TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone);
        int h = local.Hour;
        if (h < options.Demo.ResetHour || h >= options.Demo.ResetHour + Math.Max(1, options.Demo.ResetWindowHours)) return null;
        string key = local.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return ReadState().LastDemoResetDate == key ? null : key;
    }

    public void MarkDone(string key)
    {
        Directory.CreateDirectory(options.Paths.StateDir);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(new State { LastDemoResetDate = key }));
    }

    private State ReadState()
    {
        try { return File.Exists(StatePath) ? JsonSerializer.Deserialize<State>(File.ReadAllText(StatePath)) ?? new State() : new State(); }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            log.LogWarning(ex, "agent-state.json o'qilmadi");
            return new State();
        }
    }
}

/// <summary>Asosiy sikl: demo rejalashtirish → lease → bajarish (lease uzaytirib turiladi) → natija.</summary>
public sealed class AgentWorker(IServiceProvider services, AgentOptions options, ILogger<AgentWorker> log) : BackgroundService
{
    private DateTime _nextUsageUtc = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Provisioning agenti {Version}: panel {Url}, server #{ServerId}, SQL {Sql}, tenants.json {File}",
            PanelClient.Version, options.Panel.Url, options.Panel.ServerId, options.Sql.Server, options.ResolveTenantsFile());
        while (!stoppingToken.IsCancellationRequested)
        {
            bool worked = false;
            try { worked = await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or PanelClient.PanelException)
            {
                log.LogWarning("Panel bilan aloqa xatosi: {Message}", ex.Message);
            }
            if (!worked)
                try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(2, options.Panel.PollSeconds)), stoppingToken); }
                catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Bitta aylanish. Job bajarilgan bo'lsa true (darhol keyingisini so'raymiz).</summary>
    public async Task<bool> RunOnceAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var panel = scope.ServiceProvider.GetRequiredService<PanelClient>();
        var scheduler = scope.ServiceProvider.GetRequiredService<DemoScheduler>();
        var executor = scope.ServiceProvider.GetRequiredService<JobExecutor>();

        if (scheduler.DueKey() is { } key)
        {
            try
            {
                var s = await panel.ScheduleAsync(new ScheduleRequest { Type = JobKind.Reset, Slug = options.Demo.Slug, Key = key }, ct);
                log.LogInformation("Demo reset rejalashtirildi: job #{JobId} ({State})", s.JobId, s.Created ? "yangi" : "mavjud");
                scheduler.MarkDone(key);
            }
            catch (PanelClient.PanelException ex) when (ex.Status == (HttpStatusCode)422)
            {
                log.LogWarning("Demo reset rad etildi (bugun qayta so'ralmaydi): {Message}", ex.Message);
                scheduler.MarkDone(key);
            }
        }

        var lease = await panel.LeaseAsync(ct);
        if (lease is null)
        {
            if (options.Usage.Enabled && DateTime.UtcNow >= _nextUsageUtc)
            {
                _nextUsageUtc = DateTime.UtcNow.AddMinutes(Math.Max(1, options.Usage.CheckMinutes));
                await CollectUsageAsync(panel, scope.ServiceProvider.GetRequiredService<UsageCollector>(), ct);
            }
            return false;
        }
        log.LogInformation("Job #{JobId}: {Type} {Slug} (urinish {Attempt})", lease.JobId, lease.Type, lease.Payload.Slug, lease.Attempt);

        using var extendCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var extender = ExtendLoopAsync(panel, lease, extendCts.Token);
        JobCompletion completion;
        try { completion = await executor.ExecuteAsync(lease, ct); }
        finally
        {
            extendCts.Cancel();
            try { await extender; } catch (OperationCanceledException) { }
        }
        WriteLocalLog(lease, completion);

        for (int i = 1; ; i++)
        {
            try
            {
                await panel.CompleteAsync(lease.JobId, completion, ct);
                log.LogInformation("Job #{JobId}: {Outcome} {Error}", lease.JobId, completion.Outcome, completion.Error);
                break;
            }
            catch (PanelClient.PanelException ex) when (ex.Status is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
            {
                log.LogWarning("Job #{JobId}: panel natijani qabul qilmadi ({Message}) — lease yo'qolgan, panel qayta beradi", lease.JobId, ex.Message);
                break;
            }
            catch (Exception ex) when (i < 5 && ex is HttpRequestException or TaskCanceledException or PanelClient.PanelException)
            {
                log.LogWarning("Natija yuborilmadi ({Attempt}/5): {Message}", i, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5 * i), ct);
            }
        }
        return true;
    }

    /// <summary>Panel bergan tenantlar faolligini yig'ib yuboradi. Qaytaradi: yuborilgan hisobotlar soni.</summary>
    public async Task<int> CollectUsageAsync(PanelClient panel, UsageCollector collector, CancellationToken ct)
    {
        var targets = await panel.UsageTargetsAsync(ct);
        int sent = 0;
        foreach (var t in targets.Targets)
        {
            var report = await collector.CollectAsync(t, ct);
            try
            {
                await panel.UsageAsync(report, ct);
                sent++;
                if (report.Error != null) log.LogWarning("Faollik {Slug}: {Error}", t.Slug, report.Error);
                if (report.More) _nextUsageUtc = DateTime.UtcNow; // davomi darhol
            }
            catch (PanelClient.PanelException ex) when (ex.Status == (HttpStatusCode)422)
            {
                log.LogWarning("Faollik {Slug} rad etildi: {Message}", t.Slug, ex.Message);
            }
        }
        return sent;
    }

    private async Task ExtendLoopAsync(PanelClient panel, JobLease lease, CancellationToken ct)
    {
        var period = TimeSpan.FromTicks(Math.Max(TimeSpan.FromSeconds(30).Ticks, (lease.LeaseUntilUtc - DateTime.UtcNow).Ticks / 3));
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(period, ct);
            try { await panel.ExtendAsync(lease.JobId, lease.LeaseToken, ct); }
            catch (Exception ex) when (ex is HttpRequestException or PanelClient.PanelException)
            {
                log.LogWarning("Lease uzaytirilmadi: {Message}", ex.Message);
            }
        }
    }

    private void WriteLocalLog(JobLease lease, JobCompletion c)
    {
        try
        {
            string dir = Path.Combine(options.Paths.StateDir, "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, $"job-{lease.JobId}.log"),
                $"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} {lease.Type} {lease.Payload.Slug} urinish {lease.Attempt}: {c.Outcome} {c.Error}\n{c.Log}\n");
        }
        catch (IOException ex) { log.LogWarning(ex, "Lokal job logi yozilmadi"); }
    }
}
