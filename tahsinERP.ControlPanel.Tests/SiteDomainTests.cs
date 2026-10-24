using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;
using tahsinERP.Provisioning.Contracts;
using tahsinERP.ProvisioningAgent;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Domen qoidasi va agent tomoni (IIS/win-acme buyruqlari soxta runner bilan) — SQL'siz.</summary>
public class SiteDomainUnitTests : IDisposable
{
    private readonly string _wacs = Path.Combine(Path.GetTempPath(), "wacs-" + Guid.NewGuid().ToString("N")[..8] + ".exe");

    public SiteDomainUnitTests() => File.WriteAllText(_wacs, "");

    public void Dispose() => File.Delete(_wacs);

    [Theory]
    [InlineData("Shop.Firma.UZ.", "shop.firma.uz")]
    [InlineData("  firma.uz ", "firma.uz")]
    [InlineData("магазин.уз", "xn--80aairftm.xn--g1ax")]
    [InlineData("http://firma.uz", null)]
    [InlineData("firma.uz:8080", null)]
    [InlineData("firma.uz/shop", null)]
    [InlineData("firma", null)]
    [InlineData("10.0.0.1", null)]
    [InlineData("a_b.uz", null)]
    [InlineData("", null)]
    public void Rules_Normalize(string input, string? expected) => Assert.Equal(expected, SiteDomainRules.Normalize(input));

    [Fact]
    public void Rules_IsUnder()
    {
        Assert.True(SiteDomainRules.IsUnder("tahsinerp.uz", "tahsinerp.uz"));
        Assert.True(SiteDomainRules.IsUnder("x.sayt.tahsinerp.uz", ".tahsinerp.uz"));
        Assert.False(SiteDomainRules.IsUnder("nottahsinerp.uz", "tahsinerp.uz"));
        Assert.False(SiteDomainRules.IsUnder("firma.uz", ""));
        Assert.True(SiteDomainRules.IsValid("firma.uz"));
        Assert.False(SiteDomainRules.IsValid("Firma.uz"));
        Assert.False(SiteDomainRules.IsValid("x.uz'] /commit"));
    }

    private sealed class FakeRunner : ICommandRunner
    {
        public readonly List<string> Calls = [];
        public string Bindings = "https/*:443:*.sayt.x.uz";
        public int WacsExit;
        public Task<CommandResult> RunAsync(string exe, IReadOnlyList<string> args, TimeSpan timeout, CancellationToken ct)
        {
            string line = Path.GetFileName(exe) + " " + string.Join(' ', args);
            Calls.Add(line);
            if (args is ["list", "site", _, "/text:bindings"]) return Ok(Bindings);
            if (args is ["list", "site", _, "/text:id"]) return Ok("7\r\n");
            if (args.Count > 3 && args[0] == "set" && args[3].StartsWith("/+bindings.") && Regex.Match(args[3], @"protocol='(\w+)',bindingInformation='\*:\d+:([^']+)'") is { Success: true } add)
                Bindings += $",{add.Groups[1].Value}/*:{(add.Groups[1].Value == "https" ? 443 : 80)}:{add.Groups[2].Value}";
            if (args.Count > 3 && args[0] == "set" && args[3].StartsWith("/-bindings.") && Regex.Match(args[3], @"protocol='(\w+)',bindingInformation='(\*:\d+:[^']+)'") is { Success: true } rm)
                Bindings = string.Join(',', Bindings.Split(',').Where(b => b != rm.Groups[1].Value + "/" + rm.Groups[2].Value));
            if (exe.EndsWith(".exe") && args.Contains("--installation"))
            {
                if (WacsExit != 0) return Task.FromResult(new CommandResult(WacsExit, "Authorization failed: DNS problem: NXDOMAIN"));
                string host = args[args.ToList().IndexOf("--host") + 1];
                Bindings += $",https/*:443:{host}";
            }
            return Ok("");
        }

        private static Task<CommandResult> Ok(string output) => Task.FromResult(new CommandResult(0, output));
    }

    private sealed class FakeCerts : ICertificateCleaner
    {
        public readonly List<string> Removed = [];
        public int Remove(string storeName, string friendlyName) { Removed.Add(storeName + "/" + friendlyName); return 1; }
    }

    private (SiteDomainOps Ops, FakeRunner Runner, FakeCerts Certs) Ops()
    {
        var runner = new FakeRunner();
        var certs = new FakeCerts();
        var opts = new AgentOptions { SiteDomains = { WacsExe = _wacs, AppCmd = "appcmd.exe", IisSite = "tahsinERP.Site", Email = "ops@x.uz" } };
        return (new SiteDomainOps(opts, runner, certs), runner, certs);
    }

    [Fact]
    public async Task Bind_AddsHttpBinding_RunsWacsHttp01_AndVerifiesHttps()
    {
        var (ops, runner, _) = Ops();
        var log = new List<string>();
        await ops.BindAsync("shop.firma.uz", log.Add, default);
        Assert.Contains("appcmd.exe set site tahsinERP.Site /+bindings.[protocol='http',bindingInformation='*:80:shop.firma.uz']", runner.Calls);
        var wacs = Assert.Single(runner.Calls, c => c.Contains("--installation"));
        Assert.Contains("--source manual --host shop.firma.uz --friendlyname tahsin-site-shop.firma.uz --validation selfhosting", wacs);
        Assert.Contains("--store certificatestore --certificatestore WebHosting --installation iis --installationsiteid 7 --accepttos --emailaddress ops@x.uz", wacs);
        Assert.Contains(log, l => l.Contains("https://shop.firma.uz"));

        // qayta urinish: binding bor — qo'shilmaydi
        int before = runner.Calls.Count(c => c.Contains("/+bindings."));
        await ops.BindAsync("shop.firma.uz", log.Add, default);
        Assert.Equal(before, runner.Calls.Count(c => c.Contains("/+bindings.")));
    }

    [Fact]
    public async Task Bind_WacsFailure_IsJobError_WithOutput()
    {
        var (ops, runner, _) = Ops();
        runner.WacsExit = -1;
        var ex = await Assert.ThrowsAsync<InvalidJobException>(() => ops.BindAsync("shop.firma.uz", _ => { }, default));
        Assert.Contains("NXDOMAIN", ex.Message);
        await Assert.ThrowsAsync<InvalidJobException>(() => ops.BindAsync("bad host.uz", _ => { }, default));
    }

    [Fact]
    public async Task Unbind_CancelsRenewal_RemovesBindings_SslcertAndCertificate()
    {
        var (ops, runner, certs) = Ops();
        runner.Bindings += ",http/*:80:old.firma.uz,https/*:443:old.firma.uz";
        await ops.UnbindAsync("old.firma.uz", _ => { }, default);
        Assert.Contains(runner.Calls, c => c.EndsWith("--cancel --friendlyname tahsin-site-old.firma.uz"));
        Assert.Contains("appcmd.exe set site tahsinERP.Site /-bindings.[protocol='https',bindingInformation='*:443:old.firma.uz']", runner.Calls);
        Assert.Contains("appcmd.exe set site tahsinERP.Site /-bindings.[protocol='http',bindingInformation='*:80:old.firma.uz']", runner.Calls);
        Assert.Contains("netsh http delete sslcert hostnameport=old.firma.uz:443", runner.Calls);
        Assert.Equal(["WebHosting/tahsin-site-old.firma.uz"], certs.Removed);
        Assert.Equal("https/*:443:*.sayt.x.uz", runner.Bindings);   // wildcard tegilmadi
    }

    [Fact]
    public void Bindings_ParseAndCertificateMatch()
    {
        Assert.Equal([("https", "*.sayt.x.uz"), ("http", "shop.firma.uz"), ("net.tcp", "")],
            SiteDomainOps.ParseBindings("https/*:443:*.sayt.x.uz,http/*:80:Shop.Firma.uz,net.tcp/808:*\r\n"));
        Assert.True(SiteDomainOps.IsOurCertificate("tahsin-site-a.uz @ 2026/10/25", "tahsin-site-a.uz"));
        Assert.True(SiteDomainOps.IsOurCertificate("tahsin-site-a.uz", "tahsin-site-a.uz"));
        Assert.False(SiteDomainOps.IsOurCertificate("tahsin-site-a.uz.evil @ 2026/10/25", "tahsin-site-a.uz"));
        Assert.False(SiteDomainOps.IsOurCertificate("*.sayt.tahsinerp.uz @ 2026/10/21", "tahsin-site-a.uz"));
        Assert.False(SiteDomainOps.IsOurCertificate(null, "tahsin-site-a.uz"));
    }
}

/// <summary>
/// Panel: domen kiritish → DNS tekshiruvi → agent job (faqat site-domain imkoniyatli agentga, imzolangan API orqali) → Active va saytga
/// yo'naltirish; band domen, olib tashlash va server tozalash; firma sahifasi.
/// </summary>
public class SiteDomainFlowTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    private sealed class FakeSite : IAppsClient
    {
        public readonly Dictionary<string, SiteAppStatus> Sites = [];
        public readonly List<string> Pushes = [];
        public bool BotConfigured => false;
        public bool SiteConfigured => true;
        public Task<BotAppStatus> GetBotAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<BotAppStatus> ConnectBotAsync(string slug, string erpBaseUrl, string erpTenant, string? token, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<BotAppStatus> DisableBotAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SiteAppStatus> GetSiteAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(Sites.GetValueOrDefault(slug) ?? new SiteAppStatus(slug, false, false, null, null, null, null));
        public Task<SiteAppStatus> EnableSiteAsync(string slug, string erpBaseUrl, string erpTenant, CancellationToken ct = default) =>
            Task.FromResult(Sites[slug] = new SiteAppStatus(slug, true, true, erpBaseUrl, erpTenant, null, null));
        public Task<SiteAppStatus> DisableSiteAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SiteAppStatus> SetSiteDomainAsync(string slug, string? host, bool redirect, CancellationToken ct = default)
        {
            Pushes.Add($"{slug}:{host ?? "-"}:{redirect}");
            return Task.FromResult(Sites[slug] = Sites[slug] with { Domain = host, RedirectToDomain = redirect });
        }
    }

    private sealed class FakeDns : IDnsResolver
    {
        public readonly Dictionary<string, IPAddress[]> Records = new(StringComparer.OrdinalIgnoreCase);
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct) =>
            Records.TryGetValue(host, out var a) ? Task.FromResult(a) : throw new SocketException((int)SocketError.HostNotFound);
    }

    private const string ServerIp = "63.183.64.107";

    private WebApplicationFactory<Program> App(FakeSite site, FakeDns dns) => f.WithWebHostBuilder(b =>
    {
        b.UseSetting("ControlPanel:Security:RequireStaffTotp", "false");
        b.UseSetting("ControlPanel:Apps:SiteUrl", "https://api.sayt.x.uz");
        b.UseSetting("ControlPanel:Apps:SiteServerIp", ServerIp);
        b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IAppsClient>(); s.AddSingleton<IAppsClient>(site);
            s.RemoveAll<IDnsResolver>(); s.AddSingleton<IDnsResolver>(dns);
        });
    });

    private static async Task<T> InScope<T>(WebApplicationFactory<Program> app, Func<SiteDomainService, ControlDbContext, Task<T>> a)
    {
        using var scope = app.Services.CreateScope();
        return await a(scope.ServiceProvider.GetRequiredService<SiteDomainService>(), scope.ServiceProvider.GetRequiredService<ControlDbContext>());
    }

    private static Task<Tenant> TenantAsync(WebApplicationFactory<Program> app, int id) =>
        InScope(app, (_, db) => db.Tenants.FirstAsync(t => t.Id == id));

    private static Task<TenantSiteDomain?> DomainAsync(WebApplicationFactory<Program> app, int tenantId) =>
        InScope(app, (_, db) => db.SiteDomains.AsNoTracking().FirstOrDefaultAsync(d => d.TenantId == tenantId));

    private async Task<(Tenant Tenant, int ServerId, string Secret)> SetupAsync(WebApplicationFactory<Program> app, FakeSite site, string slug)
    {
        // agent siri shu host'ning master kaliti bilan shifrlanadi (WithWebHostBuilder har safar yangi kalit oladi)
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ControlDbContext>();
        string secret = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var server = new Server
        {
            Name = slug + "-srv", Host = "localhost", SqlInstance = f.SqlServer, MaxTenants = 50, CreatedAtUtc = DateTime.UtcNow,
            AgentSecretProtected = scope.ServiceProvider.GetRequiredService<ISecretProtector>().Protect(secret, SecretPurposes.AgentSecret),
        };
        db.Servers.Add(server);
        await db.SaveChangesAsync();
        int serverId = server.Id;
        var t = await scope.ServiceProvider.GetRequiredService<TenantService>()
            .CreateAsync(new Tenant { Slug = slug, LegalName = slug + " MChJ", Deployment = DeploymentKind.SaaS, ServerId = serverId }, true);
        await site.EnableSiteAsync(slug, $"https://{slug}.{PanelFactory.BaseDomain}/", slug);
        return (t, serverId, secret);
    }

    private static PanelClient Agent(WebApplicationFactory<Program> app, PanelFactory f, int serverId, string secret, bool siteDomains) =>
        new(new HttpClient(app.Server.CreateHandler()),
            new AgentOptions { Panel = { Url = "https://localhost", ServerId = serverId, Secret = secret }, SiteDomains = { Enabled = siteDomains } }, f.Time);

    [Fact]
    public async Task Connect_DnsCheck_AgentJob_Active_Redirect_ThenRemove()
    {
        var site = new FakeSite();
        var dns = new FakeDns();
        using var app = App(site, dns);
        var (t, serverId, secret) = await SetupAsync(app, site, "sdom-firma");

        // format va band domenlar
        await Assert.ThrowsAsync<DomainException>(() => InScope(app, (s, _) => s.SetAsync(t, "http://shop.firma.uz/x", true)));
        await Assert.ThrowsAsync<DomainException>(() => InScope(app, (s, _) => s.SetAsync(t, "boshqa." + PanelFactory.BaseDomain, true)));
        await Assert.ThrowsAsync<DomainException>(() => InScope(app, (s, _) => s.SetAsync(t, "x.sayt.x.uz", true)));

        // DNS hali boshqa joyga qaraydi — saqlanadi, saytga host yuboriladi (yo'naltirishsiz), job yo'q
        dns.Records["shop.sdom.uz"] = [IPAddress.Parse("1.2.3.4")];
        var d = await InScope(app, (s, _) => s.SetAsync(t, "Shop.Sdom.UZ", redirect: true));
        Assert.Equal((SiteDomainStatus.PendingDns, "shop.sdom.uz", false), (d.Status, d.Host, d.DnsOk));
        Assert.Equal("sdom-firma:shop.sdom.uz:False", site.Pushes.Last());
        Assert.Equal("1.2.3.4", d.DnsResult);

        // boshqa firma shu domenni ololmaydi
        var (t2, _, _) = await SetupAsync(app, site, "sdom-boshqa");
        var taken = await Assert.ThrowsAsync<DomainException>(() => InScope(app, (s, _) => s.SetAsync(t2, "shop.sdom.uz", false)));
        Assert.Contains("shop.sdom.uz", taken.Message);

        // AAAA begona manzil — hali ham xato
        dns.Records["shop.sdom.uz"] = [IPAddress.Parse(ServerIp), IPAddress.Parse("2001:db8::1")];
        Assert.False((await InScope(app, (s, _) => s.CheckAsync(t))).Ok);

        dns.Records["shop.sdom.uz"] = [IPAddress.Parse(ServerIp)];
        var check = await InScope(app, (s, _) => s.CheckAsync(t));
        Assert.True(check.Ok);
        Assert.NotNull(check.Job);
        Assert.Equal(SiteDomainStatus.Binding, (await DomainAsync(app, t.Id))!.Status);

        // eski agent (imkoniyatsiz) bu job'ni olmaydi
        Assert.Null(await Agent(app, f, serverId, secret, siteDomains: false).LeaseAsync(default));
        var agent = Agent(app, f, serverId, secret, siteDomains: true);
        var lease = await agent.LeaseAsync(default);
        Assert.NotNull(lease);
        Assert.Equal((JobKind.SiteDomain, "shop.sdom.uz", (List<string>?)null), (lease!.Type, lease.Payload.SiteDomain, lease.Payload.SiteDomainRemove));

        await agent.CompleteAsync(lease.JobId, new JobCompletion { LeaseToken = lease.LeaseToken, Outcome = JobOutcome.Succeeded, Log = "ok" }, default);
        d = (await DomainAsync(app, t.Id))!;
        Assert.Equal(SiteDomainStatus.Active, d.Status);
        Assert.NotNull(d.ActivatedAtUtc);
        Assert.Equal("sdom-firma:shop.sdom.uz:True", site.Pushes.Last());   // sertifikat tayyor — yo'naltirish yoqildi

        // domenni almashtirish: eski domen tozalanadigan ro'yxatga
        dns.Records["new.sdom.uz"] = [IPAddress.Parse(ServerIp)];
        d = await InScope(app, (s, _) => s.SetAsync(t, "new.sdom.uz", redirect: false));
        Assert.Equal((SiteDomainStatus.Binding, "shop.sdom.uz"), (d.Status, d.PendingRemoval));
        lease = await agent.LeaseAsync(default);
        Assert.Equal(("new.sdom.uz", "shop.sdom.uz"), (lease!.Payload.SiteDomain, Assert.Single(lease.Payload.SiteDomainRemove!)));

        // agent xatosi — Failed, xato matni ko'rinadi; tekshirish qayta job qo'yadi
        await agent.CompleteAsync(lease.JobId, new JobCompletion { LeaseToken = lease.LeaseToken, Outcome = JobOutcome.Failed, Error = "win-acme xatosi (kod -1)" }, default);
        d = (await DomainAsync(app, t.Id))!;
        Assert.Equal((SiteDomainStatus.Failed, "win-acme xatosi (kod -1)"), (d.Status, d.Error));
        Assert.NotNull((await InScope(app, (s, _) => s.CheckAsync(t))).Job);
        lease = await agent.LeaseAsync(default);
        await agent.CompleteAsync(lease!.JobId, new JobCompletion { LeaseToken = lease.LeaseToken, Outcome = JobOutcome.Succeeded }, default);
        d = (await DomainAsync(app, t.Id))!;
        Assert.Equal((SiteDomainStatus.Active, (string?)null), (d.Status, d.PendingRemoval));
        Assert.Equal("sdom-firma:new.sdom.uz:False", site.Pushes.Last());

        // olib tashlash: saytdan darhol, server tozalash — agent, keyin yozuv o'chadi
        var removeJob = await InScope(app, (s, _) => s.RemoveAsync(t));
        Assert.NotNull(removeJob);
        Assert.Equal("sdom-firma:-:False", site.Pushes.Last());
        Assert.Equal(SiteDomainStatus.Removing, (await DomainAsync(app, t.Id))!.Status);
        lease = await agent.LeaseAsync(default);
        Assert.Equal(((string?)null, "new.sdom.uz"), (lease!.Payload.SiteDomain, Assert.Single(lease.Payload.SiteDomainRemove!)));
        await agent.CompleteAsync(lease.JobId, new JobCompletion { LeaseToken = lease.LeaseToken, Outcome = JobOutcome.Succeeded }, default);
        Assert.Null(await DomainAsync(app, t.Id));

        // bo'shagan domen endi boshqa firmaga
        dns.Records["shop.sdom.uz"] = [IPAddress.Parse("1.2.3.4")];
        Assert.Equal("shop.sdom.uz", (await InScope(app, (s, _) => s.SetAsync(t2, "shop.sdom.uz", false))).Host);
        // hali ulanmagan domenni olib tashlash — job kerak emas
        Assert.Null(await InScope(app, (s, _) => s.RemoveAsync(t2)));
        Assert.Null(await DomainAsync(app, t2.Id));

        var actions = await InScope(app, (_, db) => db.AuditLog.Where(a => a.Entity == "Tenant" && a.EntityId == t.Id.ToString()).Select(a => a.Action).ToListAsync());
        Assert.Contains("tenant.site-domain-set", actions);
        Assert.Contains("tenant.site-domain-check", actions);
        Assert.Contains("tenant.site-domain-remove", actions);
    }

    [Fact]
    public async Task SiteMustBeEnabled_AndTenantPageShowsDomainBlock()
    {
        var site = new FakeSite();
        var dns = new FakeDns();
        using var app = App(site, dns);
        var t0 = await f.CreateTenantAsync("sdom-nosite");
        await Assert.ThrowsAsync<DomainException>(() => InScope(app, (s, _) => s.SetAsync(t0, "nosite.uz", false)));

        var (t, _, _) = await SetupAsync(app, site, "sdom-ui");
        await f.EnsureStaffAsync("sdom-owner@test.uz", StaffRole.Owner);
        using var c = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Redirect,
            (await Post(c, "/Account/Login", new() { ["Email"] = "sdom-owner@test.uz", ["Password"] = "TestParol-2026xx" })).StatusCode);

        string url = $"/Tenants/Details/{t.Id}";
        string page = WebUtility.HtmlDecode(await (await c.GetAsync(url)).Content.ReadAsStringAsync());
        Assert.Contains("O'z domeni", page);
        Assert.Contains(ServerIp, page);                       // DNS ko'rsatmasi: A yozuv
        Assert.Contains("sdom-ui.sayt.x.uz", page);            // CNAME maqsadi

        var bad = await Post(c, url + "?handler=SiteDomainSave", new() { ["SiteDomainInput"] = "http://x", ["SiteDomainRedirect"] = "true" }, url);
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        Assert.Contains("Domen formati noto'g'ri", WebUtility.HtmlDecode(await bad.Content.ReadAsStringAsync()));

        dns.Records["ui.sdom.uz"] = [IPAddress.Parse(ServerIp)];
        var ok = await Post(c, url + "?handler=SiteDomainSave", new() { ["SiteDomainInput"] = "ui.sdom.uz", ["SiteDomainRedirect"] = "true" }, url);
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        page = WebUtility.HtmlDecode(await (await c.GetAsync(url)).Content.ReadAsStringAsync());
        Assert.Contains("ui.sdom.uz", page);
        Assert.Contains("Sertifikat olinmoqda", page);         // DNS to'g'ri — job navbatda
        Assert.Contains("Sayt domeni", page);                  // job turi jadvalda
        Assert.Equal(SiteDomainStatus.Binding, (await DomainAsync(app, t.Id))!.Status);
    }

    private static string Token(string html) =>
        WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value);

    private static async Task<HttpResponseMessage> Post(HttpClient c, string url, Dictionary<string, string> form, string? tokenFrom = null)
    {
        form["__RequestVerificationToken"] = Token(await (await c.GetAsync(tokenFrom ?? url)).Content.ReadAsStringAsync());
        return await c.PostAsync(url, new FormUrlEncodedContent(form));
    }
}
