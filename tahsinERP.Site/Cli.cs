using Microsoft.EntityFrameworkCore;
using tahsinERP.Site.Catalog;
using tahsinERP.Site.Data;

namespace tahsinERP.Site;

/// <summary>
/// Boshqaruv buyruqlari (server ishga tushmaydi):
///   tenant add &lt;slug&gt; &lt;erpBaseUrl&gt; [--erp-tenant &lt;slug&gt;]  — sayt tenanti (subdomen: {slug}.{BaseDomain})
///   tenant list | tenant disable &lt;slug&gt;
///   snapshot sync &lt;slug&gt;                     — ERP'dan nusxani hozir olish (tekshiruv uchun)
///   snapshot import &lt;slug&gt; &lt;file.json&gt;     — fayldan nusxa (ERP'siz sinov/namoyish); rasmlar: {DataRoot}/{slug}/orig/{id}_{hash}
///   schema                                   — faqat bazani yaratish/yangilash
/// </summary>
public static class Cli
{
    private static readonly string[] Commands = ["tenant", "snapshot", "schema"];

    public static bool IsCommand(string[] args) => args.Length > 0 && Commands.Contains(args[0]);

    public static async Task<int> RunAsync(IServiceProvider root, string[] args)
    {
        using var scope = root.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SiteDbContext>();
        string Arg(int i) => args.Length > i ? args[i] : throw new ArgumentException("Argument yetarli emas. Qarang: Cli.cs");
        try
        {
            switch (args[0], args.ElementAtOrDefault(1))
            {
                case ("schema", _):
                    Console.WriteLine("Baza tayyor.");
                    return 0;

                case ("tenant", "add"):
                {
                    var slug = Arg(2).Trim().ToLowerInvariant();
                    if (!slug.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') || slug.Length > 50) throw new ArgumentException("Slug: a-z, 0-9, '-'");
                    var erp = new Uri(Arg(3), UriKind.Absolute).AbsoluteUri;
                    var flags = args.Skip(4).ToList();
                    var at = flags.IndexOf("--erp-tenant");
                    var t = await db.Tenants.FirstOrDefaultAsync(x => x.Slug == slug);
                    if (t == null) db.Tenants.Add(t = new SiteTenant { Slug = slug, CreatedAt = DateTime.UtcNow });
                    t.ErpBaseUrl = erp;
                    t.ErpTenant = at >= 0 ? flags.ElementAtOrDefault(at + 1)?.Trim().ToLowerInvariant() : null;
                    t.IsActive = true;
                    await db.SaveChangesAsync();
                    Console.WriteLine($"Sayt tenanti {slug}: ERP {erp} (tenant: {t.ErpTenant ?? "default"}).");
                    return 0;
                }

                case ("tenant", "list"):
                    foreach (var t in await db.Tenants.AsNoTracking().OrderBy(x => x.Slug).ToListAsync())
                    {
                        var snap = await db.Snapshots.AsNoTracking().Where(s => s.TenantId == t.Id).Select(s => (DateTime?)s.FetchedAt).FirstOrDefaultAsync();
                        Console.WriteLine($"{t.Slug,-20} {(t.IsActive ? "faol" : "o'chiq"),-7} {t.ErpBaseUrl} [{t.ErpTenant ?? "default"}] nusxa: {snap?.ToString("yyyy-MM-dd HH:mm") ?? "—"}");
                    }
                    return 0;

                case ("tenant", "disable"):
                {
                    var slug = Arg(2).ToLowerInvariant();
                    var t = await db.Tenants.FirstOrDefaultAsync(x => x.Slug == slug) ?? throw new InvalidOperationException("Topilmadi: " + slug);
                    t.IsActive = false;
                    await db.SaveChangesAsync();
                    Console.WriteLine("O'chirildi: " + slug);
                    return 0;
                }

                case ("snapshot", "sync"):
                {
                    var t = await Find(db, Arg(2));
                    var store = root.GetRequiredService<CatalogStore>();
                    await store.LoadAsync(CancellationToken.None);
                    await root.GetRequiredService<CatalogSyncWorker>().SyncAsync(t, CancellationToken.None);
                    Console.WriteLine($"{t.Slug}: {store.Get(t.Slug)?.Products.Count ?? 0} tovar");
                    return 0;
                }

                case ("snapshot", "import"):
                {
                    var t = await Find(db, Arg(2));
                    var json = await File.ReadAllTextAsync(Arg(3));
                    var parsed = Snapshot.Parse(json);
                    await root.GetRequiredService<CatalogStore>().LoadAsync(CancellationToken.None);
                    await root.GetRequiredService<CatalogSyncWorker>().ApplyAsync(t, "\"file-" + DateTime.UtcNow.Ticks + "\"", json, CancellationToken.None);
                    Console.WriteLine($"{t.Slug}: {parsed.Products?.Count ?? 0} tovar, {parsed.Categories?.Count ?? 0} kategoriya import qilindi.");
                    return 0;
                }
            }
            Console.Error.WriteLine("Noma'lum buyruq. Qarang: Cli.cs");
            return 2;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UriFormatException or IOException or System.Text.Json.JsonException
                                       or SiteErpException or HttpRequestException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static async Task<SiteTenant> Find(SiteDbContext db, string slug)
    {
        slug = slug.ToLowerInvariant();
        return await db.Tenants.FirstOrDefaultAsync(x => x.Slug == slug) ?? throw new InvalidOperationException("Topilmadi: " + slug);
    }
}
