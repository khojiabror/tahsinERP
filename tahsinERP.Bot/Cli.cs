using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.Bot.Data;
using tahsinERP.Bot.Security;
using tahsinERP.Bot.Telegram;

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
        var api = sp.GetRequiredService<TelegramApi>();
        var secrets = sp.GetRequiredService<SecretBox>();
        var options = sp.GetRequiredService<IOptions<BotOptions>>().Value;
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
                    var erp = new Uri(Arg(3), UriKind.Absolute).AbsoluteUri;
                    var token = Arg(4).Trim();
                    var flags = args.Skip(5).ToList();
                    var erpTenantAt = flags.IndexOf("--erp-tenant");
                    var erpTenant = erpTenantAt >= 0 ? flags.ElementAtOrDefault(erpTenantAt + 1)?.Trim().ToLowerInvariant() : null;
                    string? username = null;
                    if (!flags.Contains("--offline"))   // --offline: lokal sinov (token tekshirilmaydi)
                    {
                        var me = await api.CallAsync(token, "getMe", new { }, CancellationToken.None);
                        if (!me.Ok || me.Result is not { } r) throw new InvalidOperationException("Token noto'g'ri: " + me.Description);
                        username = r.GetProperty("username").GetString();
                    }
                    var t = await db.Tenants.FirstOrDefaultAsync(x => x.Slug == slug);
                    if (t == null) db.Tenants.Add(t = new BotTenant { Slug = slug, CreatedAt = DateTime.UtcNow, WebhookSecret = NewSecret() });
                    t.ErpBaseUrl = erp;
                    t.ErpTenant = string.IsNullOrEmpty(erpTenant) ? null : erpTenant;
                    t.TokenEnc = secrets.Protect(token);
                    t.BotUsername = username;
                    t.IsActive = true;
                    await db.SaveChangesAsync();
                    Console.WriteLine($"Tenant {slug}: @{username}, ERP {erp} (tenant: {t.ErpTenant ?? "default"}). ERP sozlamalarida bot nomi: {username}. Endi: webhook set {slug}");
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
                {
                    var t = await Find(db, Arg(2));
                    if (!options.PublicBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Bot:PublicBaseUrl https bo'lishi kerak (Telegram talabi).");
                    var url = options.PublicBaseUrl.TrimEnd('/') + "/tg/" + t.Slug;
                    var res = await api.CallAsync(secrets.Unprotect(t.TokenEnc), "setWebhook", new
                    {
                        url, secret_token = t.WebhookSecret, max_connections = 40,
                        allowed_updates = new[] { "message", "edited_message", "callback_query" }
                    }, CancellationToken.None);
                    Console.WriteLine(res.Ok ? "Webhook: " + url : "Xato: " + res.Description);
                    return res.Ok ? 0 : 1;
                }

                case ("webhook", "info"):
                {
                    var t = await Find(db, Arg(2));
                    var res = await api.CallAsync(secrets.Unprotect(t.TokenEnc), "getWebhookInfo", new { }, CancellationToken.None);
                    Console.WriteLine(res.Result is { } info ? JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }) : res.Description);
                    return res.Ok ? 0 : 1;
                }
            }
            Console.Error.WriteLine("Noma'lum buyruq. Qarang: Cli.cs");
            return 2;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UriFormatException)
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

    /// <summary>secret_token: 1-256 belgi, A-Z a-z 0-9 _ -.</summary>
    internal static string NewSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
