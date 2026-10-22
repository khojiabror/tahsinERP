using System.Text.Json;
using System.Text.Json.Serialization;

namespace tahsinERP.Provisioning.Contracts;

// Agent ↔ Updater fayl almashuvi (bir serverda): Updates\Inbox\<jobId>\ (agent yozadi), Updates\Status\<jobId>.json va
// Updater\state.json (Updater yozadi). Docs/Deploy/03_AUTO_UPDATE.md.

/// <summary>Updater holati (state.json): o'rnatilgan versiyalar va zaxiralar. Faqat Updater yozadi; agent o'qib panelga yuboradi.</summary>
public sealed class UpdaterState
{
    /// <summary>Updater ishga tushganda yozadi (panelda "Updater o'rnatilganmi" belgisi).</summary>
    public string? UpdaterVersion { get; set; }
    public Dictionary<string, TargetState> Apps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, TargetState> Instances { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TargetState
{
    public string? Version { get; set; }
    public string? PreviousVersion { get; set; }
    public string? DbName { get; set; }
    public DateTime? InstalledAtUtc { get; set; }
    /// <summary>Eng yangisi oxirida.</summary>
    public List<BackupEntry> Backups { get; set; } = [];
}

public sealed class BackupEntry
{
    public string Path { get; set; } = "";
    /// <summary>Zaxira olingan paytdagi (o'rnatishdan oldingi) versiya.</summary>
    public string? FromVersion { get; set; }
    public string? DbBackupFile { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Agent inbox'ga yozadigan so'rov (Inbox\&lt;jobId&gt;\request.json — oxirgi yoziladi, atomik).</summary>
public sealed class InboxRequest
{
    public long JobId { get; set; }
    public UpdateAction Action { get; set; }
    public string App { get; set; } = "";
    public string? Slug { get; set; }
    public string? DbName { get; set; }
    public string? Version { get; set; }
    public bool RestoreDatabase { get; set; }
}

/// <summary>Updater → agent: Status\&lt;jobId&gt;.json.</summary>
public sealed class JobStatus
{
    public long JobId { get; set; }
    public string? Stage { get; set; }
    public string Log { get; set; } = "";
    public bool Final { get; set; }
    public UpdateOutcome? Outcome { get; set; }
    public string? Error { get; set; }
    public string? InstalledVersion { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public static class UpdaterFiles
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Agent → panel inventari (zaxira yo'llari yuborilmaydi).</summary>
    public static UpdateInventory ToInventory(UpdaterState s, string? updaterVersion, Func<string, string?>? schemaVersion = null,
        Func<string, string?>? poolState = null) => new()
    {
        UpdaterVersion = updaterVersion,
        CollectedAtUtc = DateTime.UtcNow,
        Apps = s.Apps.Select(kv => new InstalledApp
        {
            App = kv.Key, Version = kv.Value.Version, PreviousVersion = kv.Value.PreviousVersion, InstalledAtUtc = kv.Value.InstalledAtUtc,
            CanRollback = kv.Value.Backups.Count > 0,
        }).OrderBy(a => a.App).ToList(),
        Instances = s.Instances.Select(kv => new ErpInstanceInfo
        {
            Slug = kv.Key, DbName = kv.Value.DbName, Version = kv.Value.Version, PreviousVersion = kv.Value.PreviousVersion,
            InstalledAtUtc = kv.Value.InstalledAtUtc, CanRollback = kv.Value.Backups.Count > 0,
            HasDatabaseBackup = kv.Value.Backups.LastOrDefault()?.DbBackupFile is { } f && File.Exists(f),
            SchemaVersion = kv.Value.DbName is { } db ? schemaVersion?.Invoke(db) : null,
            PoolState = poolState?.Invoke(kv.Key),
        }).OrderBy(i => i.Slug).ToList(),
    };
}
