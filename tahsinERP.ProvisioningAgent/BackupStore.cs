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
    /// <summary>
    /// Restore: arxiv manzilidan RESTORE uchun lokal .bak yo'li. Manzil shu ombor ichida bo'lishi va SHA-256 (yonidagi .sha256) mos
    /// kelishi shart — aks holda InvalidDataException.
    /// </summary>
    Task<string> FetchAsync(string location, CancellationToken ct);
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

    public async Task<string> FetchAsync(string location, CancellationToken ct)
    {
        if (!location.StartsWith(Scheme, StringComparison.Ordinal)) throw new InvalidDataException("Arxiv manzili lokal emas: " + location);
        string path = Path.GetFullPath(location[Scheme.Length..]);
        if (!path.StartsWith(Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Arxiv manzili arxiv papkasidan tashqarida: " + path);
        if (!File.Exists(path)) throw new InvalidDataException("Arxiv fayli topilmadi: " + path);
        string shaFile = path + ".sha256";
        if (File.Exists(shaFile))
        {
            string expected = (await File.ReadAllTextAsync(shaFile, ct)).Split(' ', 2)[0].Trim();
            string actual;
            await using (var fs = File.OpenRead(path))
                actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(fs, ct));
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Arxiv fayli buzilgan (SHA-256 mos emas): " + path);
        }
        return path;
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
