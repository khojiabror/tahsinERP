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
    public string PublicKeyXml { get; }
    private readonly string _privateKeyXml;
    private readonly string _database = "tahsin_control_test_" + Guid.NewGuid().ToString("N")[..12];
    private readonly string _server = Environment.GetEnvironmentVariable("CP_TEST_SQL") ?? ".";

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
        builder.ConfigureTestServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(Time);
            s.RemoveAll<ILicenseSigner>();
            s.AddSingleton<ILicenseSigner>(new LocalRsaSigner("k2", _privateKeyXml));
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