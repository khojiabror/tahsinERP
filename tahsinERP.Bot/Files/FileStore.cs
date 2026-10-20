using Microsoft.Extensions.Options;

namespace tahsinERP.Bot.Files;

/// <summary>Fotosurat/imzo ombori. Kalit: "{tenant}/{yyyy}/{MM}/{guid}.{ext}" — ERP'ga faqat kalit yoziladi.</summary>
public interface IFileStore
{
    Task<string> SaveAsync(string tenantSlug, byte[] content, string extension, CancellationToken ct);
    Task<byte[]?> ReadAsync(string key, CancellationToken ct);
}

/// <summary>Lokal disk (AWS'da — EBS/EFS papkasi; keyin S3 bilan almashtiriladi, kalit formati o'zgarmaydi).</summary>
public sealed class LocalFileStore : IFileStore
{
    public const int MaxBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> Allowed = ["jpg", "png"];
    private readonly string _root;
    private readonly TimeProvider _clock;

    public LocalFileStore(IOptions<BotOptions> options, IHostEnvironment env, TimeProvider clock)
    {
        var root = options.Value.FilesRoot;
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(env.ContentRootPath, root)));
        _clock = clock;
    }

    internal LocalFileStore(string root, TimeProvider clock)
    {
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));   // oxiridagi "\" olib tashlanadi (PathOf solishtiruvi uchun)
        _clock = clock;
    }

    public async Task<string> SaveAsync(string tenantSlug, byte[] content, string extension, CancellationToken ct)
    {
        extension = extension.TrimStart('.').ToLowerInvariant();
        if (!Allowed.Contains(extension)) throw new ArgumentException("Fayl turi ruxsat etilmagan.", nameof(extension));
        if (content.Length == 0 || content.Length > MaxBytes) throw new ArgumentException("Fayl hajmi noto'g'ri.", nameof(content));
        if (!IsSafeSegment(tenantSlug)) throw new ArgumentException("Tenant nomi noto'g'ri.", nameof(tenantSlug));
        var now = _clock.GetUtcNow();
        var key = $"{tenantSlug}/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}.{extension}";
        var path = PathOf(key)!;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, ct);
        return key;
    }

    public async Task<byte[]?> ReadAsync(string key, CancellationToken ct)
    {
        var path = PathOf(key);
        return path != null && File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
    }

    /// <summary>Kalit -> to'liq yo'l; ildizdan tashqariga chiqadigan kalit — null.</summary>
    internal string? PathOf(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Contains("..") || key.Contains('\\') || Path.IsPathRooted(key)) return null;
        var full = Path.GetFullPath(Path.Combine(_root, key));
        return full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? full : null;
    }

    private static bool IsSafeSegment(string s) => s.Length is > 0 and <= 50 && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
