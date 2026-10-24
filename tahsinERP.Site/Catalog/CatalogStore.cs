using System.Collections.Concurrent;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.Site.Data;

namespace tahsinERP.Site.Catalog;

/// <summary>
/// Tenantlar va ularning joriy katalog indeksi (xotirada). Domen → tenant xaritasi ham shu yerda:
/// standart subdomen {slug}.{BaseDomain}, panel ulagan o'z domeni (SiteTenant.CustomDomain, ustun) va nusxadagi ERP domenlari.
/// </summary>
public sealed class CatalogStore(IServiceScopeFactory scopes, IOptions<SiteOptions> options, ILogger<CatalogStore> log)
{
    private readonly ConcurrentDictionary<string, CatalogIndex> _indexes = new(StringComparer.OrdinalIgnoreCase);
    private volatile Dictionary<string, string> _hosts = new(StringComparer.OrdinalIgnoreCase);
    private volatile IReadOnlyList<SiteTenant> _tenants = [];

    public IReadOnlyList<SiteTenant> Tenants => _tenants;

    public CatalogIndex? Get(string tenant) => _indexes.GetValueOrDefault(tenant);

    /// <summary>Host → tenant slug (topilmasa null).</summary>
    public string? TenantForHost(string host)
    {
        host = host.ToLowerInvariant().TrimEnd('.');
        if (_hosts.TryGetValue(host, out var t)) return t;
        if (host.StartsWith("www.") && _hosts.TryGetValue(host[4..], out t)) return t;
        var baseDomain = options.Value.BaseDomain.Trim().TrimStart('.').ToLowerInvariant();
        if (baseDomain.Length > 0 && host.EndsWith("." + baseDomain))
        {
            var slug = host[..^(baseDomain.Length + 1)];
            if (!slug.Contains('.') && _tenants.Any(x => string.Equals(x.Slug, slug, StringComparison.OrdinalIgnoreCase))) return slug;
        }
        return null;
    }

    /// <summary>Tenant uchun asosiy ochiq manzil (kanonik URL'lar, sitemap uchun).</summary>
    public string? PrimaryHost(string tenant)
    {
        var own = _tenants.FirstOrDefault(t => string.Equals(t.Slug, tenant, StringComparison.OrdinalIgnoreCase));
        if (own is { CustomDomain: not null, RedirectToDomain: true }) return own.CustomDomain;
        var idx = Get(tenant);
        var primary = idx?.Snapshot.Domains?.FirstOrDefault(d => d.IsPrimary)?.Host ?? idx?.Snapshot.Domains?.FirstOrDefault()?.Host;
        if (primary != null) return primary;
        var baseDomain = options.Value.BaseDomain.Trim().TrimStart('.');
        return baseDomain.Length > 0 ? tenant.ToLowerInvariant() + "." + baseDomain : null;
    }

    /// <summary>Ishga tushishda: tenantlar va bazadagi oxirgi nusxalar (ERP'siz ham sayt ochiladi).</summary>
    public async Task LoadAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SiteDbContext>();
        _tenants = await db.Tenants.AsNoTracking().Where(t => t.IsActive).ToListAsync(ct);
        foreach (var row in await db.Snapshots.AsNoTracking().ToListAsync(ct))
        {
            var tenant = _tenants.FirstOrDefault(t => t.Id == row.TenantId);
            if (tenant == null) continue;
            try
            {
                _indexes[tenant.Slug] = new CatalogIndex(tenant.Slug, row.ETag, Snapshot.Parse(row.Json));
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Nusxani o'qib bo'lmadi: {Tenant}", tenant.Slug);
            }
        }
        RebuildHosts();
    }

    public async Task RefreshTenantsAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SiteDbContext>();
        _tenants = await db.Tenants.AsNoTracking().Where(t => t.IsActive).ToListAsync(ct);
        foreach (var gone in _indexes.Keys.Where(k => _tenants.All(t => !string.Equals(t.Slug, k, StringComparison.OrdinalIgnoreCase))).ToList())
            _indexes.TryRemove(gone, out _);
        RebuildHosts();
    }

    public void Set(CatalogIndex index)
    {
        _indexes[index.Tenant] = index;
        RebuildHosts();
    }

    private void RebuildHosts()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var idx in _indexes.Values)
            foreach (var d in idx.Snapshot.Domains ?? [])
                map[d.Host] = idx.Tenant;
        // panel ulagan domen noyob (panel va SiteTenants indeksi tekshiradi) — ERP sozlamasidagi bir xil hostdan ustun
        foreach (var t in _tenants)
            if (!string.IsNullOrEmpty(t.CustomDomain)) map[t.CustomDomain] = t.Slug;
        _hosts = map;
    }
}

/// <summary>
/// Har tenant uchun SyncSeconds'da: ERP'dan nusxa (If-None-Match), o'zgargan bo'lsa — bazaga, indeksga, rasmlar oldindan
/// tayyorlanadi va sahifa keshi tozalanadi. ERP o'chiq bo'lsa — eski nusxa bilan ishlashda davom etadi.
/// </summary>
public sealed class CatalogSyncWorker(IServiceScopeFactory scopes, CatalogStore store, SiteErpClient erp, Media.MediaCache media,
    IOutputCacheStore outputCache, IOptions<SiteOptions> options, TimeProvider clock, ILogger<CatalogSyncWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await store.LoadAsync(ct);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(options.Value.SyncSeconds, 10, 3600)), clock);
        do
        {
            try
            {
                await store.RefreshTenantsAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogWarning(ex, "Tenantlarni o'qib bo'lmadi");
            }
            foreach (var tenant in store.Tenants)
            {
                try
                {
                    await SyncAsync(tenant, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    log.LogWarning("Sinxron {Tenant}: {Error}", tenant.Slug, ex.Message);
                }
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    internal async Task SyncAsync(SiteTenant tenant, CancellationToken ct)
    {
        var current = store.Get(tenant.Slug);
        var res = await erp.SnapshotAsync(tenant, current?.ETag, ct);
        if (res.NotModified || res.Json == null) return;
        await ApplyAsync(tenant, res.ETag ?? "", res.Json, ct);
    }

    /// <summary>Yangi nusxa: bazaga, indeksga, keshlar. CLI ham chaqiradi (fayldan import).</summary>
    public async Task ApplyAsync(SiteTenant tenant, string etag, string json, CancellationToken ct)
    {
        var index = new CatalogIndex(tenant.Slug, etag, Snapshot.Parse(json));
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SiteDbContext>();
            var row = await db.Snapshots.FindAsync([tenant.Id], ct);
            if (row == null) db.Snapshots.Add(row = new SiteSnapshotRow { TenantId = tenant.Id });
            row.ETag = etag;
            row.Json = json;
            row.FetchedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
        }
        store.Set(index);
        await outputCache.EvictByTagAsync("tenant:" + tenant.Slug, ct);
        log.LogInformation("Katalog yangilandi: {Tenant}, {Products} tovar", tenant.Slug, index.Products.Count);
        _ = media.PrefetchAsync(tenant, index, CancellationToken.None);   // fon: asosiy rasmlar oldindan
    }
}
