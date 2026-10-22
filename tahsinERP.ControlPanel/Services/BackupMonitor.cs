using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.ControlPanel.Services;

public enum BackupHealth { Unknown, Ok, Stale, Failed }

/// <summary>
/// Serverlardagi kunlik zaxiraning holati (Docs/Deploy/04_BACKUP.md): agent inventar bilan Backup-Databases.ps1 natijasini yuboradi.
/// Zaxira kuniga bir marta — StaleHours dan eski bo'lsa (task ishlamagan) ogohlantiriladi.
/// </summary>
public sealed class BackupMonitor(ControlDbContext db, Clock clock)
{
    public const int StaleHours = 26;

    public sealed record Row(int ServerId, string ServerName, BackupReport? Report, BackupHealth Health);

    public static BackupHealth HealthOf(BackupReport? r, DateTime nowUtc) =>
        r is null ? BackupHealth.Unknown
        : !r.Ok ? BackupHealth.Failed
        : r.FinishedAtUtc is not { } at || nowUtc - DateTime.SpecifyKind(at, DateTimeKind.Utc) > TimeSpan.FromHours(StaleHours) ? BackupHealth.Stale
        : BackupHealth.Ok;

    /// <summary>Faol serverlar va ularning oxirgi zaxirasi.</summary>
    public async Task<List<Row>> AllAsync(CancellationToken ct = default)
    {
        var servers = await db.Servers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).Select(s => new { s.Id, s.Name }).ToListAsync(ct);
        var inv = await db.ServerInventories.AsNoTracking().ToDictionaryAsync(i => i.ServerId, i => i.Json, ct);
        return servers.Select(s =>
        {
            BackupReport? r = null;
            if (inv.TryGetValue(s.Id, out var json))
                try { r = JsonSerializer.Deserialize<UpdateInventory>(json, AgentProtocol.Json)?.Backup; } catch (JsonException) { }
            return new Row(s.Id, s.Name, r, HealthOf(r, clock.UtcNow));
        }).ToList();
    }

    /// <summary>Bosh sahifa uchun: xato bergan yoki eskirgan zaxiralar (hali hisobot bermaganlar emas).</summary>
    public async Task<List<Row>> ProblemsAsync(CancellationToken ct = default) =>
        (await AllAsync(ct)).Where(r => r.Health is BackupHealth.Failed or BackupHealth.Stale).ToList();
}
