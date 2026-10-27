using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.I18n;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.ControlPanel.Services;

public enum HealthLevel { Ok, Warn, Bad }

/// <summary>
/// Server resurslari: joriy holat (agentning oxirgi inventari) va kunlik jurnal (ServerHealthDays). Agent inventarni har
/// ~5 daqiqada yuboradi — <see cref="RecordAsync"/> shu kunning qatorini yangilaydi (o'rtacha, maksimum, qayta yuklashlar).
/// </summary>
public sealed class ServerHealthLog(ControlDbContext db, Clock clock)
{
    /// <summary>Shundan eski inventar — agent ishlamayapti (holat eskirgan).</summary>
    public const int StaleMinutes = 15;
    public const int KeepDays = 400;
    public const double WarnPercent = 85, BadPercent = 95;

    public sealed record Current(int ServerId, ServerHealth? Health, DateTime? ReportedAtUtc, bool Stale);

    public static HealthLevel LevelOf(double percent) =>
        percent >= BadPercent ? HealthLevel.Bad : percent >= WarnPercent ? HealthLevel.Warn : HealthLevel.Ok;

    public async Task RecordAsync(int serverId, ServerHealth h, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var day = DateOnly.FromDateTime(clock.ToLocal(now));
        var prev = await db.ServerHealthDays.Where(x => x.ServerId == serverId).OrderByDescending(x => x.Day).FirstOrDefaultAsync(ct);
        var row = prev?.Day == day ? prev : null;
        if (row is null)
        {
            db.ServerHealthDays.Add(row = new ServerHealthDay { ServerId = serverId, Day = day, FirstAtUtc = now });
            var cutoff = day.AddDays(-KeepDays);
            await db.ServerHealthDays.Where(x => x.ServerId == serverId && x.Day < cutoff).ExecuteDeleteAsync(ct);
        }
        if (prev is not null && h.UptimeSeconds < prev.UptimeSeconds) row.Reboots++;

        long used = h.RamUsedBytes;
        row.RamUsedAvgBytes = (long)((row.RamUsedAvgBytes * (double)row.Samples + used) / (row.Samples + 1));
        row.RamUsedMaxBytes = Math.Max(row.RamUsedMaxBytes, used);
        row.Samples++;
        if (h.CpuPercent is { } cpu)
        {
            row.CpuAvgPercent = Math.Round(((row.CpuAvgPercent ?? 0) * row.CpuSamples + cpu) / (row.CpuSamples + 1), 1);
            row.CpuMaxPercent = Math.Max(row.CpuMaxPercent ?? 0, cpu);
            row.CpuSamples++;
        }
        row.RamTotalBytes = h.RamTotalBytes;
        row.DisksJson = JsonSerializer.Serialize(h.Disks.Take(20), AgentProtocol.Json);
        row.UptimeSeconds = h.UptimeSeconds;
        row.LastAtUtc = now;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Barcha serverlarning joriy holati (inventari yo'qlari ham — Health null).</summary>
    public async Task<Dictionary<int, Current>> CurrentAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var rows = await db.ServerInventories.AsNoTracking().Select(i => new { i.ServerId, i.Json, i.ReportedAtUtc }).ToListAsync(ct);
        return rows.ToDictionary(r => r.ServerId, r =>
        {
            ServerHealth? h = null;
            try { h = JsonSerializer.Deserialize<UpdateInventory>(r.Json, AgentProtocol.Json)?.Health; } catch (JsonException) { }
            return new Current(r.ServerId, h, r.ReportedAtUtc, now - r.ReportedAtUtc > TimeSpan.FromMinutes(StaleMinutes));
        });
    }

    /// <summary>Oxirgi <paramref name="days"/> kunlik jurnal, server bo'yicha (yangi kun birinchi).</summary>
    public async Task<Dictionary<int, List<ServerHealthDay>>> HistoryAsync(int days, CancellationToken ct = default)
    {
        var from = clock.Today.AddDays(-(days - 1));
        var rows = await db.ServerHealthDays.AsNoTracking().Where(x => x.Day >= from).OrderByDescending(x => x.Day).ToListAsync(ct);
        return rows.GroupBy(x => x.ServerId).ToDictionary(g => g.Key, g => g.ToList());
    }

    public static List<DiskHealth> Disks(ServerHealthDay d)
    {
        try { return JsonSerializer.Deserialize<List<DiskHealth>>(d.DisksJson, AgentProtocol.Json) ?? []; }
        catch (JsonException) { return []; }
    }

    public static string Bytes(long b) =>
        b >= 1L << 40 ? $"{b / (double)(1L << 40):0.##} TB"
        : b >= 1L << 30 ? $"{b / (double)(1L << 30):0.#} GB"
        : $"{b / (double)(1L << 20):0} MB";

    public static string Uptime(long seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalDays >= 1 ? L.T("Pnl_Hlt_UptimeDays", (int)t.TotalDays, t.Hours) : L.T("Pnl_Hlt_UptimeHours", t.Hours, t.Minutes);
    }
}
