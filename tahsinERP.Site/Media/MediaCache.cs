using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using SkiaSharp;
using tahsinERP.Site.Catalog;
using tahsinERP.Site.Data;

namespace tahsinERP.Site.Media;

/// <summary>
/// Rasmlar: asl nusxa ERP'dan bir marta olinadi ({DataRoot}/{tenant}/orig/{id}_{hash}), so'ralgan o'lchamdagi WebP
/// tayyorlanib diskda saqlanadi ({id}_{hash8}_{w}.webp). URL'da hash bor — brauzer/CDN 1 yil keshlaydi.
/// Ruxsat etilgan kengliklar cheklangan (diskni to'ldirib yuborish hujumi bo'lmasin).
/// </summary>
public sealed class MediaCache(SiteErpClient erp, IOptions<SiteOptions> options, IHostEnvironment env, ILogger<MediaCache> log)
{
    public static readonly int[] Widths = [200, 400, 600, 900, 1200, 1600];
    private const int Quality = 80;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    private string Root
    {
        get
        {
            var r = options.Value.DataRoot;
            return Path.GetFullPath(Path.IsPathRooted(r) ? r : Path.Combine(env.ContentRootPath, r));
        }
    }

    public static int SnapWidth(int requested) => Widths.FirstOrDefault(w => w >= requested, Widths[^1]);

    public static string Url(SnapshotMedia m, int width) => $"/media/{m.Id}-{m.Hash[..8]}-{SnapWidth(width)}.webp";

    /// <summary>srcset qatori: "/media/..-400.webp 400w, ...".</summary>
    public static string SrcSet(SnapshotMedia m, params int[] widths) =>
        string.Join(", ", widths.Where(w => w <= Math.Max(m.Width, Widths[0])).DefaultIfEmpty(Widths[0]).Select(w => Url(m, w) + " " + w + "w"));

    /// <summary>WebP fayl yo'li (bo'lmasa tayyorlaydi). Media nusxada bo'lmasa — null.</summary>
    public async Task<string?> GetAsync(SiteTenant tenant, CatalogIndex index, int id, string hash8, int width, CancellationToken ct)
    {
        if (!index.Media.TryGetValue(id, out var m) || !m.Hash.StartsWith(hash8, StringComparison.Ordinal) || !Widths.Contains(width)) return null;
        var dir = Path.Combine(Root, Safe(tenant.Slug));
        var target = Path.Combine(dir, $"{id}_{m.Hash[..8]}_{width}.webp");
        if (File.Exists(target)) return target;

        var gate = _locks.GetOrAdd(target, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (File.Exists(target)) return target;
            var original = await OriginalAsync(tenant, dir, m, ct);
            var webp = Resize(original, width);
            Directory.CreateDirectory(dir);
            var tmp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(tmp, webp, ct);
            File.Move(tmp, target, true);
            return target;
        }
        finally
        {
            gate.Release();
            _locks.TryRemove(target, out _);
        }
    }

    private async Task<byte[]> OriginalAsync(SiteTenant tenant, string dir, SnapshotMedia m, CancellationToken ct)
    {
        var origDir = Path.Combine(dir, "orig");
        var path = Path.Combine(origDir, $"{m.Id}_{m.Hash}");
        if (File.Exists(path)) return await File.ReadAllBytesAsync(path, ct);
        var (bytes, _) = await erp.MediaAsync(tenant, m.Id, ct);
        Directory.CreateDirectory(origDir);
        await File.WriteAllBytesAsync(path, bytes, ct);
        return bytes;
    }

    /// <summary>Kenglik bo'yicha kichraytirish (kattalashtirmaydi) va WebP.</summary>
    internal static byte[] Resize(byte[] original, int width)
    {
        using var src = SKBitmap.Decode(original) ?? throw new InvalidDataException("Rasmni o'qib bo'lmadi");
        var w = Math.Min(width, src.Width);
        var h = Math.Max(1, (int)Math.Round(src.Height * (double)w / src.Width));
        using var resized = w == src.Width ? src.Copy() : src.Resize(new SKImageInfo(w, h), new SKSamplingOptions(SKCubicResampler.Mitchell));
        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Webp, Quality);
        return data.ToArray();
    }

    /// <summary>Yangi nusxadan keyin: har tovarning asosiy rasmi (kartochka o'lchami) va bannerlar.</summary>
    public async Task PrefetchAsync(SiteTenant tenant, CatalogIndex index, CancellationToken ct)
    {
        var ids = index.Products.Select(p => p.Media?.FirstOrDefault()?.Id).Where(i => i.HasValue).Select(i => i!.Value)
            .Concat((index.Snapshot.Banners ?? []).Select(b => b.MediaId)).Distinct().ToList();
        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) return;
            if (!index.Media.TryGetValue(id, out var m)) continue;
            try
            {
                await GetAsync(tenant, index, id, m.Hash[..8], 400, ct);
            }
            catch (Exception ex)
            {
                log.LogDebug("Rasm {Id} ({Tenant}): {Error}", id, tenant.Slug, ex.Message);
            }
        }
    }

    private static string Safe(string s) => new(s.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
}
