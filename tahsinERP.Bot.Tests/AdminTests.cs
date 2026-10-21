using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using tahsinERP.Bot.Admin;
using tahsinERP.Bot.Data;
using tahsinERP.Bot.Pipeline;
using tahsinERP.Bot.Security;
using tahsinERP.Bot.Telegram;

namespace tahsinERP.Bot.Tests;

/// <summary>Panel API'si orqali bot ulash: token tekshiriladi, shifrlanadi, webhook o'rnatiladi; kalit hosilasi PowerShell bilan bir xil.</summary>
public class AdminTests
{
    private const string Master = "0123456789abcdef0123456789abcdef-bot";

    private static (ServiceProvider Sp, FakeHandler Tg) Build()
    {
        var tg = new FakeHandler(req => req.RequestUri!.AbsolutePath switch
        {
            var p when p.EndsWith("/getMe") => p.Contains("bad")
                ? FakeHandler.Json(HttpStatusCode.Unauthorized, "{\"ok\":false,\"description\":\"Unauthorized\"}")
                : FakeHandler.Json(HttpStatusCode.OK, "{\"ok\":true,\"result\":{\"username\":\"firma1_bot\"}}"),
            var p when p.EndsWith("/getWebhookInfo") =>
                FakeHandler.Json(HttpStatusCode.OK, "{\"ok\":true,\"result\":{\"url\":\"https://bot.example.uz/tg/firma1\",\"pending_update_count\":0}}"),
            _ => FakeHandler.Json(HttpStatusCode.OK, "{\"ok\":true,\"result\":true}"),
        });
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<BotDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Options.Create(new BotOptions { MasterKey = Master, PublicBaseUrl = "https://bot.example.uz" }));
        services.AddSingleton(new SecretBox(Master));
        services.AddSingleton(new TelegramApi(new HttpClient(tg)));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<TenantRegistry>();
        services.AddScoped<BotTenantAdmin>();
        return (services.BuildServiceProvider(), tg);
    }

    [Fact]
    public async Task Upsert_VerifiesToken_EncryptsIt_AndSetsWebhook()
    {
        var (sp, tg) = Build();
        using var scope = sp.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<BotTenantAdmin>();

        var t = await admin.UpsertAsync("Firma1", "https://firma1.example.uz/", "firma1", "123:good", verifyToken: true, CancellationToken.None);
        Assert.Equal("firma1", t.Slug);
        Assert.Equal("firma1_bot", t.BotUsername);
        Assert.DoesNotContain("123:good", t.TokenEnc);
        Assert.Equal("123:good", new SecretBox(Master).Unprotect(t.TokenEnc));

        Assert.Equal("https://bot.example.uz/tg/firma1", await admin.SetWebhookAsync(t, CancellationToken.None));
        var hook = tg.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/setWebhook"));
        Assert.Contains(t.WebhookSecret, hook.Text);

        // Tokensiz yangilash: token va username qoladi, ERP manzili yangilanadi.
        var again = await admin.UpsertAsync("firma1", "https://firma1.example.uz/v2/", "firma1", null, verifyToken: true, CancellationToken.None);
        Assert.Equal("firma1_bot", again.BotUsername);
        Assert.Equal("123:good", new SecretBox(Master).Unprotect(again.TokenEnc));
        Assert.Equal("https://firma1.example.uz/v2/", again.ErpBaseUrl);

        var status = await admin.StatusAsync("firma1", withWebhook: true, CancellationToken.None);
        Assert.True(status.Active);
        Assert.Equal("https://bot.example.uz/tg/firma1", status.WebhookUrl);

        await admin.DisableAsync("firma1", CancellationToken.None);
        Assert.False((await admin.StatusAsync("firma1", false, CancellationToken.None)).Active);
        Assert.Contains(tg.Requests, r => r.Uri.AbsolutePath.EndsWith("/deleteWebhook"));
    }

    [Fact]
    public async Task Upsert_RejectsBadTokenSlugAndMissingToken()
    {
        var (sp, _) = Build();
        using var scope = sp.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<BotTenantAdmin>();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.UpsertAsync("firma1", "https://firma1.example.uz/", null, "123:bad", true, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            admin.UpsertAsync("firma1", "https://firma1.example.uz/", null, null, true, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            admin.UpsertAsync("../x", "https://firma1.example.uz/", null, "123:good", true, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            admin.UpsertAsync("firma1", "ftp://firma1.example.uz/", null, "123:good", true, CancellationToken.None));
        Assert.False((await admin.StatusAsync("firma1", false, CancellationToken.None)).Exists);
    }

    [Fact]
    public void AdminKey_MatchesInstallScriptDerivation()
    {
        // Install-BotSite.ps1 bilan bir xil: Base64Url(HMAC-SHA256(master, "tahsin-admin-api")), "=" siz.
        var key = BotTenantAdmin.DeriveAdminKey(Master);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", key);
        Assert.Equal(key, BotTenantAdmin.DeriveAdminKey(Master));
        Assert.NotEqual(key, BotTenantAdmin.DeriveAdminKey(Master + "x"));
        Assert.Equal("AfgNjogV3Tu18AnzR80Vw-vtF8Igme1GmarQ0k-Jqik", BotTenantAdmin.DeriveAdminKey("test-master-key-0123456789abcdef!!"));
    }
}
