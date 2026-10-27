using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace tahsinERP.Provisioning.Contracts;

// Avto-yangilash kontrakti (Docs/Deploy/03_AUTO_UPDATE.md). CI relizni yig'adi: har ilova alohida ZIP, manifest.json (SHA-256)
// va manifest.sig (ECDSA P-256 imzo). Panel, agent va Updater shu ta'riflardan foydalanadi; imzo tekshiruvi — ReleaseVerifier.

/// <summary>Reliz ichidagi ilovalar. ERP — har firma alohida IIS instance'iga o'rnatiladi; qolganlari server bo'yicha bitta.</summary>
public static class ReleaseApps
{
    public const string Erp = "erp";
    public const string Panel = "panel";
    public const string Bot = "bot";
    public const string Site = "site";
    public const string Agent = "agent";
    public const string Updater = "updater";
    public static readonly string[] All = [Erp, Panel, Bot, Site, Agent, Updater];
    /// <summary>Paneldan bitta tugma bilan o'rnatiladiganlar (Updater o'zini yangilamaydi — qo'lda, Install-Updater.ps1).</summary>
    public static readonly string[] ServerApps = [Panel, Bot, Site, Agent];

    public static string Title(string app) => app switch
    {
        Erp => "ERP", Panel => "Boshqaruv paneli", Bot => "Telegram bot", Site => "Sayt-katalog", Agent => "Provisioning agenti",
        Updater => "Updater", _ => app,
    };
}

public sealed class ReleaseManifest
{
    public int Format { get; set; } = 1;
    /// <summary>Reliz versiyasi: yyyy.M.d.N (System.Version bilan solishtiriladi).</summary>
    public string Version { get; set; } = "";
    public string? Commit { get; set; }
    public DateTime BuiltAtUtc { get; set; }
    /// <summary>Oldingi reliz tegi (o'zgarishlar shundan beri hisoblangan).</summary>
    public string? PreviousTag { get; set; }
    public List<ReleaseArtifact> Artifacts { get; set; } = [];

    public ReleaseArtifact? Find(string app) => Artifacts.FirstOrDefault(a => string.Equals(a.App, app, StringComparison.OrdinalIgnoreCase));
}

public sealed class ReleaseArtifact
{
    public string App { get; set; } = "";
    /// <summary>Reliz asset nomi, masalan erp.zip.</summary>
    public string File { get; set; } = "";
    public long Size { get; set; }
    /// <summary>SHA-256, kichik hex.</summary>
    public string Sha256 { get; set; } = "";
    /// <summary>Oldingi relizdan beri shu ilova manbasi o'zgarganmi (CI git diff bo'yicha).</summary>
    public bool Changed { get; set; } = true;
}

/// <summary>manifest.sig tarkibi.</summary>
public sealed class ReleaseSignature
{
    public string KeyId { get; set; } = "";
    public string Alg { get; set; } = "ES256";
    /// <summary>manifest.json baytlarining imzosi, IEEE P1363 (r||s), Base64.</summary>
    public string Sig { get; set; } = "";
}

public sealed class ReleaseVerificationException(string message) : Exception(message);

/// <summary>Ishonchli reliz kalitlari. Private kalit faqat GitHub secret'da (RELEASE_SIGNING_KEY); bu yerda faqat public.</summary>
public static class ReleaseKeys
{
    public static readonly IReadOnlyDictionary<string, string> Trusted = new Dictionary<string, string>
    {
        ["r1"] = """
            -----BEGIN PUBLIC KEY-----
            MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEnVNTbh0Jbhr1MZUUM56Wrr+tx7VZ
            cb84p890kCMT0Db90WnVP5xdjpIyDRBl02Rrv2rzqrlNiGd3Krbq2Vh8GA==
            -----END PUBLIC KEY-----
            """,
    };
}

public static class ReleaseVerifier
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Sha256Hex(Stream s) => Convert.ToHexStringLower(SHA256.HashData(s));

    public static string Sha256File(string path)
    {
        using var f = File.OpenRead(path);
        return Sha256Hex(f);
    }

    public static ReleaseSignature Sign(byte[] manifest, string keyId, string privateKeyPem)
    {
        using var ec = ECDsa.Create();
        ec.ImportFromPem(privateKeyPem);
        return new ReleaseSignature { KeyId = keyId, Sig = Convert.ToBase64String(ec.SignData(manifest, HashAlgorithmName.SHA256)) };
    }

    /// <summary>Imzoni ishonchli kalit bilan tekshiradi va manifestni qaytaradi. Har qanday nomuvofiqlik — istisno.</summary>
    public static ReleaseManifest Verify(byte[] manifest, string signatureJson, IReadOnlyDictionary<string, string>? trusted = null)
    {
        trusted ??= ReleaseKeys.Trusted;
        ReleaseSignature? sig;
        try { sig = JsonSerializer.Deserialize<ReleaseSignature>(signatureJson, Json); }
        catch (JsonException) { throw new ReleaseVerificationException("manifest.sig o'qilmadi."); }
        if (sig is null || sig.Alg != "ES256" || !trusted.TryGetValue(sig.KeyId, out var pem))
            throw new ReleaseVerificationException($"Noma'lum imzo kaliti: {sig?.KeyId}.");
        byte[] raw;
        try { raw = Convert.FromBase64String(sig.Sig); }
        catch (FormatException) { throw new ReleaseVerificationException("Imzo Base64 emas."); }
        using var ec = ECDsa.Create();
        ec.ImportFromPem(pem);
        if (!ec.VerifyData(manifest, raw, HashAlgorithmName.SHA256))
            throw new ReleaseVerificationException("Imzo mos kelmadi — paket o'rnatilmaydi.");
        ReleaseManifest? m;
        try { m = JsonSerializer.Deserialize<ReleaseManifest>(manifest, Json); }
        catch (JsonException) { throw new ReleaseVerificationException("manifest.json o'qilmadi."); }
        if (m is null || m.Format != 1 || ParseVersion(m.Version) is null)
            throw new ReleaseVerificationException("Manifest formati noto'g'ri.");
        return m;
    }

    /// <summary>Artefakt fayli manifestdagi hajm va SHA-256 ga mosligini tekshiradi.</summary>
    public static void VerifyArtifact(ReleaseArtifact artifact, string path)
    {
        var fi = new FileInfo(path);
        if (!fi.Exists) throw new ReleaseVerificationException($"Fayl topilmadi: {artifact.File}.");
        if (fi.Length != artifact.Size) throw new ReleaseVerificationException($"{artifact.File}: hajm mos emas.");
        if (!string.Equals(Sha256File(path), artifact.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new ReleaseVerificationException($"{artifact.File}: SHA-256 mos emas.");
    }

    public static Version? ParseVersion(string? v) =>
        System.Version.TryParse(v?.Trim().TrimStart('v'), out var r) ? r : null;

    /// <summary>a &gt; b. Noma'lum (null/eski "legacy") versiya har qanday versiyadan kichik.</summary>
    public static bool IsNewer(string? a, string? b)
    {
        var va = ParseVersion(a);
        if (va is null) return false;
        var vb = ParseVersion(b);
        return vb is null || va > vb;
    }

    public static string NewVersion(DateTime utc, int run) =>
        string.Create(CultureInfo.InvariantCulture, $"{utc:yyyy}.{utc.Month}.{utc.Day}.{run}");

    public static byte[] Serialize(ReleaseManifest m) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(m, Json));
}

// ---------------- Panel ↔ agent: yangilash job'lari ----------------

public enum UpdateAction
{
    /// <summary>Server ilovasini (panel/bot/site/agent) relizdan o'rnatish.</summary>
    InstallApp = 1,
    /// <summary>Firma ERP instance'ini relizdan yangilash (+ shu firma bazasi migratsiyasi).</summary>
    InstallTenant = 2,
    /// <summary>Firma uchun yangi ERP instance (sayt + pool + papka) — yo'q bo'lsa yaratadi va relizni o'rnatadi.</summary>
    CreateInstance = 3,
    RollbackApp = 4,
    RollbackTenant = 5,
    /// <summary>Firma bazasiga joriy instance versiyasidagi migratsiyalarni qayta qo'llash.</summary>
    MigrateTenant = 6,
}

public enum UpdateOutcome { Succeeded = 1, Failed = 2, RolledBack = 3 }

public sealed class UpdateJobLease
{
    public long JobId { get; set; }
    public string LeaseToken { get; set; } = "";
    public UpdateAction Action { get; set; }
    public string App { get; set; } = "";
    public string? Slug { get; set; }
    public string? DbName { get; set; }
    /// <summary>O'rnatiladigan reliz (Install*/CreateInstance). Rollback/Migrate — null.</summary>
    public string? Version { get; set; }
    /// <summary>RollbackTenant: bazani ham .bak'dan tiklash (shu vaqtdan keyingi ma'lumot yo'qoladi).</summary>
    public bool RestoreDatabase { get; set; }
    public DateTime LeaseUntilUtc { get; set; }
}

public sealed class UpdateProgress
{
    public string LeaseToken { get; set; } = "";
    public string? Stage { get; set; }
    public string? Log { get; set; }
}

public sealed class UpdateCompletion
{
    public string LeaseToken { get; set; } = "";
    public UpdateOutcome Outcome { get; set; }
    public string? Log { get; set; }
    public string? Error { get; set; }
    public string? InstalledVersion { get; set; }
}

/// <summary>Updater holati (state.json) — agent panelga har necha daqiqada yuboradi.</summary>
public sealed class UpdateInventory
{
    public string? UpdaterVersion { get; set; }
    public DateTime CollectedAtUtc { get; set; }
    public List<InstalledApp> Apps { get; set; } = [];
    public List<ErpInstanceInfo> Instances { get; set; } = [];
    /// <summary>Kunlik zaxiraning oxirgi natijasi (Backup-Databases.ps1 → Updates\Status\backup.json); sozlanmagan bo'lsa null.</summary>
    public BackupReport? Backup { get; set; }
    /// <summary>Server resurslari (RAM, disklar, CPU) — agent har inventarda o'lchaydi; eski agent yubormaydi (null).</summary>
    public ServerHealth? Health { get; set; }
}

/// <summary>Server resurslarining joriy holati. CPU — oldingi inventardan beri o'rtacha band foizi (birinchi o'lchovda null).</summary>
public sealed class ServerHealth
{
    public string? MachineName { get; set; }
    public string? Os { get; set; }
    public int CpuCount { get; set; }
    public double? CpuPercent { get; set; }
    public long RamTotalBytes { get; set; }
    public long RamAvailableBytes { get; set; }
    public long UptimeSeconds { get; set; }
    public List<DiskHealth> Disks { get; set; } = [];

    [JsonIgnore] public long RamUsedBytes => Math.Max(0, RamTotalBytes - RamAvailableBytes);
    [JsonIgnore] public double RamPercent => RamTotalBytes > 0 ? 100.0 * RamUsedBytes / RamTotalBytes : 0;
}

public sealed class DiskHealth
{
    /// <summary>Disk nomi, masalan "C:\".</summary>
    public string Name { get; set; } = "";
    public string? Label { get; set; }
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }

    [JsonIgnore] public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);
    [JsonIgnore] public double UsedPercent => TotalBytes > 0 ? 100.0 * UsedBytes / TotalBytes : 0;
}

/// <summary>Kunlik zaxira natijasi (Docs/Deploy/04_BACKUP.md). Sirsiz: faqat holat, fayl nomlari va xato matnlari.</summary>
public sealed class BackupReport
{
    public bool Ok { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    /// <summary>Bucket'dagi yoki serverdagi papka.</summary>
    public string? Folder { get; set; }
    /// <summary>Bucket nomi; bo'sh — faqat lokal zaxira.</summary>
    public string? Bucket { get; set; }
    public List<string> Files { get; set; } = [];
    public long? TotalBytes { get; set; }
    public List<string> Errors { get; set; } = [];
}

public sealed class InstalledApp
{
    public string App { get; set; } = "";
    public string? Version { get; set; }
    public string? PreviousVersion { get; set; }
    public DateTime? InstalledAtUtc { get; set; }
    /// <summary>Rollback mumkin (lokal zaxira bor).</summary>
    public bool CanRollback { get; set; }
}

public sealed class ErpInstanceInfo
{
    public string Slug { get; set; } = "";
    public string? DbName { get; set; }
    public string? Version { get; set; }
    public string? PreviousVersion { get; set; }
    public DateTime? InstalledAtUtc { get; set; }
    /// <summary>Bazaning oxirgi qo'llangan migratsiyasi (SCHEMA_VERSIONS), masalan "027_..." .</summary>
    public string? SchemaVersion { get; set; }
    public string? PoolState { get; set; }
    public bool CanRollback { get; set; }
    /// <summary>Oxirgi zaxirada baza .bak ham bor (RestoreDatabase mumkin).</summary>
    public bool HasDatabaseBackup { get; set; }
}

/// <summary>Agent → panel: reliz manifesti va imzosi (Updater qayta tekshiradi).</summary>
public sealed class ReleaseManifestResponse
{
    public string Version { get; set; } = "";
    /// <summary>manifest.json baytlari, Base64 (imzo aynan shu baytlarga).</summary>
    public string Manifest { get; set; } = "";
    public string Signature { get; set; } = "";
}
