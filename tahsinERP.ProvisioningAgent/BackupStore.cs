using System.Security.Cryptography;

namespace tahsinERP.ProvisioningAgent;

/// <summary>
/// Arxiv zaxiralari ombori. Hozir faqat <see cref="LocalBackupStore"/> (lokal papka). S3 uchun keyinroq S3BackupStore —
/// xuddi shu interfeys (AWS SDK chaqiruvi bu loyihada YO'Q). Manzil formati implementatsiyaga bog'liq ("file:" / "s3:").
/// </summary>
public interface IBackupStore
{
    /// <summary>Lokal .bak faylni omborga ko'chiradi (manba o'chiriladi), manzilini qaytaradi.</summary>
    Task<string> StoreAsync(string localBak, string slug, CancellationToken ct);
    /// <summary>Tenantning eng so'nggi arxivi (Archive qayta urinishida idempotentlik uchun) yoki null.</summary>
    Task<string?> FindLatestAsync(string slug, CancellationToken ct);
    /// <summary>Tenantning barcha arxivlari (Delete). Qaytaradi: o'chirilgan fayllar soni.</summary>
    Task<int> DeleteAllAsync(string slug, CancellationToken ct);
}

public sealed class LocalBackupStore(AgentOptions options) : IBackupStore
{
    private const string Scheme = "file:";

    private string Root => Path.GetFullPath(options.Paths.ArchiveDir);

    private string SlugDir(string slug)
    {
        TenantsFile.ValidateSlug(slug); // yo'l ichiga begona belgi tushmasin
        return Path.Combine(Root, slug);
    }

    public async Task<string> StoreAsync(string localBak, string slug, CancellationToken ct)
    {
        string dir = SlugDir(slug);
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, Path.GetFileName(localBak));
        if (!string.Equals(Path.GetFullPath(localBak), target, StringComparison.OrdinalIgnoreCase))
            File.Move(localBak, target, overwrite: true);
        string hash;
        await using (var fs = File.OpenRead(target))
            hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(fs, ct));
        await File.WriteAllTextAsync(target + ".sha256", hash + "  " + Path.GetFileName(target) + Environment.NewLine, ct);
        return Scheme + target;
    }

    public Task<string?> FindLatestAsync(string slug, CancellationToken ct)
    {
        string dir = SlugDir(slug);
        if (!Directory.Exists(dir)) return Task.FromResult<string?>(null);
        var latest = new DirectoryInfo(dir).GetFiles("*.bak").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
        return Task.FromResult(latest is null ? null : Scheme + latest.FullName);
    }

    public Task<int> DeleteAllAsync(string slug, CancellationToken ct)
    {
        string dir = SlugDir(slug);
        if (!Directory.Exists(dir)) return Task.FromResult(0);
        int n = Directory.GetFiles(dir, "*.bak").Length;
        Directory.Delete(dir, recursive: true);
        return Task.FromResult(n);
    }
}
