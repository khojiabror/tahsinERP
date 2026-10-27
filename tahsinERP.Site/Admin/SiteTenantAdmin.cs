using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.Site.Catalog;
using tahsinERP.Site.Data;

namespace tahsinERP.Site.Admin;

/// <summary>Firma saytini yoqish/o'chirish — CLI (`tenant add`) va boshqaruv paneli API'si (/admin/api) shu yerdan.</summary>
public sealed partial class SiteTenantAdmin(SiteDbContext db, CatalogStore store)
{
    public sealed record Status(string Slug, bool Exists, bool Active, string? ErpBaseUrl, string? ErpTenant, DateTime? SnapshotAt, int? Products,
        string? Domain = null, bool RedirectToDomain = false);

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,49}$")]
    private static partial Regex SlugRegex();

    [GeneratedRegex(@"^(?=.{4,253}$)([a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z][a-z0-9-]{0,61}[a-z0-9]$")]
    private static partial Regex HostRegex();

    /// <summary>
    /// O'z domeni: kichik harf, oxirgi nuqtasiz, IDN → punycode; kamida ikki bo'lak, IP emas. Panel bilan bir xil qoida
    /// (tahsinERP.ControlPanel SiteDomainService.NormalizeHost).
    /// </summary>
    public static string NormalizeHost(string? host)
    {
        var h = (host ?? "").Trim().TrimEnd('.').ToLowerInvariant();
        try { h = new System.Globalization.IdnMapping().GetAscii(h); }
        catch (ArgumentException) { throw new ArgumentException("Domen formati noto'g'ri: " + host); }
        return HostRegex().IsMatch(h) ? h : throw new ArgumentException("Domen formati noto'g'ri: " + host);
    }

    public static string NormalizeSlug(string? slug)
    {
        var s = (slug ?? "").Trim().ToLowerInvariant();
        return SlugRegex().IsMatch(s) ? s : throw new ArgumentException("Slug: a-z, 0-9, '-' (50 belgigacha).");
    }

    /// <summary>Tenantni qo'shadi/yangilaydi. Katalog nusxasi fon ishida (SyncSeconds ichida) olinadi.</summary>
    public async Task<SiteTenant> UpsertAsync(string slug, string erpBaseUrl, string? erpTenant, CancellationToken ct)
    {
        slug = NormalizeSlug(slug);
        var erp = new Uri(erpBaseUrl, UriKind.Absolute);
        if (erp.Scheme is not ("https" or "http")) throw new ArgumentException("ERP manzili http(s) bo'lishi kerak.");
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.Slug == slug, ct);
        if (t == null) db.Tenants.Add(t = new SiteTenant { Slug = slug, CreatedAt = DateTime.UtcNow });
        t.ErpBaseUrl = erp.AbsoluteUri;
        t.ErpTenant = string.IsNullOrWhiteSpace(erpTenant) ? null : erpTenant.Trim().ToLowerInvariant();
        t.IsActive = true;
        await db.SaveChangesAsync(ct);
        return t;
    }

    public async Task<Status> StatusAsync(string slug, CancellationToken ct)
    {
        slug = NormalizeSlug(slug);
        var t = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug, ct);
        if (t == null) return new Status(slug, false, false, null, null, null, null);
        var snap = await db.Snapshots.AsNoTracking().Where(s => s.TenantId == t.Id).Select(s => (DateTime?)s.FetchedAt).FirstOrDefaultAsync(ct);
        return new Status(t.Slug, true, t.IsActive, t.ErpBaseUrl, t.ErpTenant, snap, store.Get(t.Slug)?.Products.Count, t.CustomDomain, t.RedirectToDomain);
    }

    /// <summary>
    /// O'z domenini ulaydi (host) yoki olib tashlaydi (null). Domen standart subdomen asosida bo'lmasligi va boshqa tenantda band
    /// emasligi kerak. Host xaritasi darhol yangilanadi.
    /// </summary>
    public async Task SetDomainAsync(string slug, string? host, bool redirect, string baseDomain, CancellationToken ct)
    {
        slug = NormalizeSlug(slug);
        string? h = string.IsNullOrWhiteSpace(host) ? null : NormalizeHost(host);
        var bd = baseDomain.Trim().TrimStart('.').ToLowerInvariant();
        if (h != null && bd.Length > 0 && (h == bd || h.EndsWith("." + bd)))
            throw new ArgumentException("Bu domen standart manzillar uchun band: " + h);
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.Slug == slug, ct) ?? throw new InvalidOperationException("Sayt yoqilmagan: " + slug);
        if (h != null && await db.Tenants.AnyAsync(x => x.Id != t.Id && x.CustomDomain == h, ct))
            throw new InvalidOperationException("Domen boshqa saytga ulangan: " + h);
        t.CustomDomain = h;
        t.RedirectToDomain = h != null && redirect;
        await db.SaveChangesAsync(ct);
        await store.RefreshTenantsAsync(ct);
    }

    public async Task DisableAsync(string slug, CancellationToken ct)
    {
        slug = NormalizeSlug(slug);
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.Slug == slug, ct) ?? throw new InvalidOperationException("Topilmadi: " + slug);
        t.IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Firma butunlay olib tashlanadi (panel firmani o'chirganda): katalog nusxasi, ariza navbati va tenant yozuvi; xotiradagi
    /// indeks ham chiqadi. Nom keyin ochiladigan yangi firmaga bo'shaydi. Topilmasa — false (idempotent).
    /// </summary>
    public async Task<bool> DeleteAsync(string slug, CancellationToken ct)
    {
        slug = NormalizeSlug(slug);
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.Slug == slug, ct);
        if (t is null) return false;
        db.Snapshots.RemoveRange(await db.Snapshots.Where(x => x.TenantId == t.Id).ToListAsync(ct));
        db.LeadOutbox.RemoveRange(await db.LeadOutbox.Where(x => x.TenantId == t.Id).ToListAsync(ct));
        db.Tenants.Remove(t);
        await db.SaveChangesAsync(ct);
        await store.RefreshTenantsAsync(ct);
        return true;
    }

    /// <summary>Panel API kaliti = Base64Url(HMAC-SHA256(MasterKey, "tahsin-admin-api")) — bot bilan bir xil.</summary>
    public static string DeriveAdminKey(string masterKey) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(masterKey), "tahsin-admin-api"u8))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
