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
    public sealed record Status(string Slug, bool Exists, bool Active, string? ErpBaseUrl, string? ErpTenant, DateTime? SnapshotAt, int? Products);

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,49}$")]
    private static partial Regex SlugRegex();

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
        return new Status(t.Slug, true, t.IsActive, t.ErpBaseUrl, t.ErpTenant, snap, store.Get(t.Slug)?.Products.Count);
    }

    public async Task DisableAsync(string slug, CancellationToken ct)
    {
        slug = NormalizeSlug(slug);
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.Slug == slug, ct) ?? throw new InvalidOperationException("Topilmadi: " + slug);
        t.IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Panel API kaliti = Base64Url(HMAC-SHA256(MasterKey, "tahsin-admin-api")) — bot bilan bir xil.</summary>
    public static string DeriveAdminKey(string masterKey) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(masterKey), "tahsin-admin-api"u8))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
