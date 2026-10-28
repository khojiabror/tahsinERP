using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Licensing;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;
using ErpLicense = tahsinERP.Licensing.LicenseHelper;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>
/// Haqiqiy panel (Program) + LOKAL SQL Server'da vaqtinchalik baza (tahsin_control_test_&lt;guid&gt;, migratsiyalar bilan,
/// test oxirida o'chiriladi). Server: env CP_TEST_SQL yoki ".". Imzolash — test uchun yaratilgan RSA jufti (git'da yo'q).
/// </summary>
public sealed class PanelFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly DateTimeOffset Start = new(2026, 10, 18, 5, 0, 0, TimeSpan.Zero); // Toshkent 10:00
    public TestClock Time { get; } = new(Start);
    /// <summary>Telegram o'rnini bosuvchi: yuborilgan xabarlarni yig'adi. Standart — sozlanmagan (eski testlar: faqat log).</summary>
    public FakeTelegram Telegram { get; } = new();
    /// <summary>SMS (Eskiz) va email (SMTP) o'rnini bosuvchilar — standart sozlanmagan (4-bosqich testlari yoqadi).</summary>
    public FakeSms Sms { get; } = new();
    public FakeEmail Email { get; } = new();
    public const string WebhookSecret = "test-webhook-secret";
    public const string BotUsername = "tahsin_test_bot";
    public string PublicKeyXml { get; }
    private readonly string _privateKeyXml;
    private readonly string _database = "tahsin_control_test_" + Guid.NewGuid().ToString("N")[..12];
    private readonly string _server = Environment.GetEnvironmentVariable("CP_TEST_SQL") ?? ".";

    public string SqlServer => _server;
    /// <summary>Provisioning testlari uchun demo slug va bazaviy domen.</summary>
    public const string DemoSlug = "demo-it";
    public const string BaseDomain = "test.local";

    public string ConnectionString => $"Server={_server};Database={_database};Trusted_Connection=True;TrustServerCertificate=True";

    public PanelFactory()
    {
        ErpLicense.GenerateKeyPair(out string pub, out string priv);
        PublicKeyXml = pub;
        _privateKeyXml = priv;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Control", ConnectionString);
        builder.UseSetting("ControlPanel:Security:MasterKey", AesGcmSecretProtector.GenerateMasterKey());
        builder.UseSetting("ControlPanel:Panel:EnableDailyJob", "false");
        builder.UseSetting("ControlPanel:Panel:MigrateOnStartup", "false");
        builder.UseSetting("ControlPanel:Panel:ApiRateLimitPerMinute", "100000");
        builder.UseSetting("ControlPanel:Panel:LoginRateLimitPerMinute", "100000");
        builder.UseSetting("ControlPanel:Provisioning:DemoSlug", DemoSlug);
        builder.UseSetting("ControlPanel:Provisioning:BaseDomain", BaseDomain);
        builder.UseSetting("ControlPanel:Telegram:WebhookSecret", WebhookSecret);
        builder.UseSetting("ControlPanel:Telegram:BotUsername", BotUsername);
        builder.UseSetting("ControlPanel:Hosts:Domain", "localhost");
        builder.ConfigureTestServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(Time);
            s.RemoveAll<ILicenseSigner>();
            s.AddSingleton<ILicenseSigner>(new LocalRsaSigner("k2", _privateKeyXml));
            s.RemoveAll<ITelegramSender>();
            s.AddSingleton<ITelegramSender>(Telegram);
            s.RemoveAll<tahsinERP.ControlPanel.Portal.ISmsSender>();
            s.AddSingleton<tahsinERP.ControlPanel.Portal.ISmsSender>(Sms);
            s.RemoveAll<tahsinERP.ControlPanel.Portal.IEmailSender>();
            s.AddSingleton<tahsinERP.ControlPanel.Portal.IEmailSender>(Email);
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ControlDbContext>().Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        SqlConnection.ClearAllPools();
        await using var c = new SqlConnection($"Server={_server};Database=master;Trusted_Connection=True;TrustServerCertificate=True");
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = $"IF DB_ID('{_database}') IS NOT NULL BEGIN ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}]; END";
        await cmd.ExecuteNonQueryAsync();
    }

    public HttpClient Https() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    /// <summary>ERP'ning haqiqiy klienti panelning test serveriga ulangan.</summary>
    public tahsinERP.Services.LicenseSync.LicensePanelClient ErpClient(Func<DateTime>? utcNow = null) =>
        new("https://localhost", Server.CreateHandler(), utcNow ?? (() => Time.GetUtcNow().UtcDateTime));

    public IReadOnlyList<ErpLicense.TrustedPublicKey> ErpTrusted =>
        ErpLicense.TrustedPublicKeys.Concat([new ErpLicense.TrustedPublicKey("k2", PublicKeyXml)]).ToList();

    public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    public Task WithScopeAsync(Func<IServiceProvider, Task> action) => WithScopeAsync<int>(async sp => { await action(sp); return 0; });

    /// <summary>Test xodimi (Owner) — billing amallari uchun.</summary>
    public Task<int> EnsureStaffAsync(string email = "owner@test.uz", StaffRole role = StaffRole.Owner) => WithScopeAsync(async sp =>
    {
        var db = sp.GetRequiredService<ControlDbContext>();
        var u = await db.StaffUsers.FirstOrDefaultAsync(x => x.Email == email);
        if (u != null) return u.Id;
        u = await sp.GetRequiredService<StaffAuthService>().CreateStaffAsync(email, email, role, "TestParol-2026xx", null);
        return u.Id;
    });

    public Task<Tenant> CreateTenantAsync(string slug, bool trial = true, DeploymentKind deployment = DeploymentKind.SaaS) => WithScopeAsync(sp =>
        sp.GetRequiredService<TenantService>().CreateAsync(new Tenant { Slug = slug, LegalName = slug + " MChJ", Deployment = deployment, CompanyId = 1 }, trial));

    public Task<string> RegTokenAsync(int tenantId) => WithScopeAsync(async sp =>
    {
        var db = sp.GetRequiredService<ControlDbContext>();
        return await sp.GetRequiredService<TenantService>().IssueRegTokenAsync(await db.Tenants.FirstAsync(t => t.Id == tenantId));
    });
}

/// <summary>Test soati: oldinga ham, orqaga ham o'rnatiladi (FakeTimeProvider orqaga qaytmaydi).</summary>
public sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void SetUtcNow(DateTimeOffset value) => _now = value;
    public void Advance(TimeSpan delta) => _now += delta;
}
/// <summary>Telegram o'rnini bosuvchi: <see cref="Enabled"/> false bo'lsa "sozlanmagan" (xabarlar faqat log'ga).</summary>
public sealed class FakeTelegram : ITelegramSender
{
    public bool Enabled { get; set; }
    public System.Collections.Concurrent.ConcurrentQueue<(string Chat, string Text)> Sent { get; } = new();
    public bool IsConfigured => Enabled;
    /// <summary>O'rnatilgan bo'lsa SendAsync shu xatoni tashlaydi (tarmoq uzilishi / timeout).</summary>
    public Exception? Fail { get; set; }

    public Task SendAsync(string chatId, string text, CancellationToken ct = default)
    {
        if (Fail is not null) return Task.FromException(Fail);
        Sent.Enqueue((chatId, text));
        return Task.CompletedTask;
    }

    public IReadOnlyList<(string Chat, string Text)> To(string chat) => Sent.Where(m => m.Chat == chat).ToList();
}

public sealed class FakeSms : tahsinERP.ControlPanel.Portal.ISmsSender
{
    public bool Enabled { get; set; }
    public bool IsConfigured => Enabled;
    public System.Collections.Concurrent.ConcurrentQueue<(string Phone, string Text)> Sent { get; } = new();

    public Task SendAsync(string phone, string text, CancellationToken ct = default)
    {
        Sent.Enqueue((phone, text));
        return Task.CompletedTask;
    }
}

public sealed class FakeEmail : tahsinERP.ControlPanel.Portal.IEmailSender
{
    public bool Enabled { get; set; }
    public bool IsConfigured => Enabled;
    public System.Collections.Concurrent.ConcurrentQueue<(string To, string Subject, string Body)> Sent { get; } = new();

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        Sent.Enqueue((to, subject, body));
        return Task.CompletedTask;
    }
}
