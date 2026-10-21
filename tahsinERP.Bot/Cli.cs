using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tahsinERP.Bot.Data;
using tahsinERP.Bot.Admin;

namespace tahsinERP.Bot;

/// <summary>
/// Boshqaruv buyruqlari (server ishga tushmaydi):
///   tenant add &lt;slug&gt; &lt;erpBaseUrl&gt; &lt;botToken&gt; [--erp-tenant &lt;slug&gt;] [--offline]
///       — tenant qo'shish/yangilash (token getMe bilan tekshiriladi; ERP tenant — bitta bazali ERP'da "default")
///   tenant list | tenant disable &lt;slug&gt;
///   webhook set &lt;slug&gt; | webhook info &lt;slug&gt;    — {PublicBaseUrl}/tg/{slug}, secret_token bilan
///   schema                                    — faqat bazani yaratish/yangilash
/// </summary>
public static class Cli
{
    private static readonly string[] Commands = ["tenant", "webhook", "schema"];

    public static bool IsCommand(string[] args) => args.Length > 0 && Commands.Contains(args[0]);

    public static async Task<int> RunAsync(IServiceProvider root, string[] args)
    {
        using var scope = root.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<BotDbContext>();
        var admin = sp.GetRequiredService<BotTenantAdmin>();
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
                    var flags = args.Skip(5).ToList();
                    var erpTenantAt = flags.IndexOf("--erp-tenant");
                    var erpTenant = erpTenantAt >= 0 ? flags.ElementAtOrDefault(erpTenantAt + 1) : null;
                    var t = await admin.UpsertAsync(Arg(2), Arg(3), erpTenant, Arg(4), verifyToken: !flags.Contains("--offline"), CancellationToken.None);
                    Console.WriteLine($"Tenant {t.Slug}: @{t.BotUsername}, ERP {t.ErpBaseUrl} (tenant: {t.ErpTenant ?? "default"}). ERP sozlamalarida bot nomi: {t.BotUsername}. Endi: webhook set {t.Slug}");
                    return 0;
                }

                case ("tenant", "list"):
                    foreach (var t in await db.Tenants.AsNoTracking().OrderBy(x => x.Slug).ToListAsync())
                        Console.WriteLine($"{t.Slug,-20} @{t.BotUsername,-25} {(t.IsActive ? "faol" : "o'chiq"),-7} {t.ErpBaseUrl} [{t.ErpTenant ?? "default"}]");
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

                case ("webhook", "set"):
                    Console.WriteLine("Webhook: " + await admin.SetWebhookAsync(await Find(db, Arg(2)), CancellationToken.None));
                    return 0;

                case ("webhook", "info"):
                {
                    var info = await admin.WebhookInfoAsync(await Find(db, Arg(2)), CancellationToken.None);
                    Console.WriteLine(JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
                    return 0;
                }
            }
            Console.Error.WriteLine("Noma'lum buyruq. Qarang: Cli.cs");
            return 2;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UriFormatException or HttpRequestException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static async Task<BotTenant> Find(BotDbContext db, string slug)
    {
        slug = slug.ToLowerInvariant();
        return await db.Tenants.FirstOrDefaultAsync(x => x.Slug == slug) ?? throw new InvalidOperationException("Topilmadi: " + slug);
    }

    internal static string NewSecret() => BotTenantAdmin.NewSecret();
}
