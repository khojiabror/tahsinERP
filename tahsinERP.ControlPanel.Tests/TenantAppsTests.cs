using System.Net;
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

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Firma sahifasidan bot/saytni ulash: ERP manzili va slug paneldan, token saqlanmaydi, audit yoziladi.</summary>
public class TenantAppsTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    private sealed class FakeApps : IAppsClient
    {
        public readonly List<(string Op, string Slug, string? Erp, string? ErpTenant, string? Token)> Calls = [];
        private readonly Dictionary<string, BotAppStatus> _bots = [];
        private readonly Dictionary<string, SiteAppStatus> _sites = [];
        public bool BotConfigured => true;
        public bool SiteConfigured => true;

        public Task<BotAppStatus> GetBotAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(_bots.GetValueOrDefault(slug) ?? new BotAppStatus(slug, false, false, null, null, null, null, null, null));

        public Task<BotAppStatus> ConnectBotAsync(string slug, string erpBaseUrl, string erpTenant, string? token, CancellationToken ct = default)
        {
            Calls.Add(("bot", slug, erpBaseUrl, erpTenant, token));
            if (token == "bad") throw new DomainException("Token noto'g'ri: Unauthorized");
            return Task.FromResult(_bots[slug] = new BotAppStatus(slug, true, true, "firma_bot", erpBaseUrl, erpTenant, "https://bot.x.uz/tg/" + slug, 0, null));
        }

        public Task<BotAppStatus> DisableBotAsync(string slug, CancellationToken ct = default)
        {
            Calls.Add(("bot-off", slug, null, null, null));
            return Task.FromResult(_bots[slug] = _bots[slug] with { Active = false });
        }

        public Task<SiteAppStatus> GetSiteAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(_sites.GetValueOrDefault(slug) ?? new SiteAppStatus(slug, false, false, null, null, null, null));

        public Task<SiteAppStatus> EnableSiteAsync(string slug, string erpBaseUrl, string erpTenant, CancellationToken ct = default)
        {
            Calls.Add(("site", slug, erpBaseUrl, erpTenant, null));
            return Task.FromResult(_sites[slug] = new SiteAppStatus(slug, true, true, erpBaseUrl, erpTenant, null, null));
        }

        public Task<SiteAppStatus> DisableSiteAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(_sites[slug] = _sites[slug] with { Active = false });

        public Task<SiteAppStatus> SetSiteDomainAsync(string slug, string? host, bool redirect, CancellationToken ct = default)
        {
            Calls.Add(("domain", slug, host, redirect.ToString(), null));
            return Task.FromResult(_sites[slug] = _sites[slug] with { Domain = host, RedirectToDomain = redirect });
        }

        public Task<AppDeleteResult> DeleteBotAsync(string slug, CancellationToken ct = default)
        {
            Calls.Add(("bot-delete", slug, null, null, null));
            return Task.FromResult(new AppDeleteResult(slug, _bots.Remove(slug)));
        }

        public Task<AppDeleteResult> DeleteSiteAsync(string slug, CancellationToken ct = default)
        {
            Calls.Add(("site-delete", slug, null, null, null));
            return Task.FromResult(new AppDeleteResult(slug, _sites.Remove(slug)));
        }
    }

    private static string Token(string html) =>
        WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value);

    private static async Task<HttpResponseMessage> PostAsync(HttpClient c, string url, Dictionary<string, string> form, string? tokenFrom = null)
    {
        form["__RequestVerificationToken"] = Token(await (await c.GetAsync(tokenFrom ?? url)).Content.ReadAsStringAsync());
        return await c.PostAsync(url, new FormUrlEncodedContent(form));
    }

    [Fact]
    public async Task ConnectBotAndSite_FromTenantPage()
    {
        var fake = new FakeApps();
        await f.EnsureStaffAsync("apps-owner@test.uz", StaffRole.Owner);
        using var app = f.WithWebHostBuilder(b =>
        {
            b.UseSetting("ControlPanel:Security:RequireStaffTotp", "false");
            b.UseSetting("ControlPanel:Apps:SiteUrl", "https://api.sayt.x.uz");
            b.ConfigureTestServices(s => { s.RemoveAll<IAppsClient>(); s.AddSingleton<IAppsClient>(fake); });
        });
        using var c = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Redirect,
            (await PostAsync(c, "/Account/Login", new() { ["Email"] = "apps-owner@test.uz", ["Password"] = "TestParol-2026xx" })).StatusCode);

        var create = await PostAsync(c, "/Tenants/Create", new()
        {
            ["Input.Slug"] = "apps-firma", ["Input.LegalName"] = "Apps Firma MChJ", ["Input.Deployment"] = "SaaS",
            ["Input.CompanyId"] = "1", ["Input.Inn"] = "223456789", ["StartTrial"] = "true",
        });
        string details = create.Headers.Location!.OriginalString;
        string page = await (await c.GetAsync(details)).Content.ReadAsStringAsync();
        Assert.Contains("Telegram bot va sayt-katalog", page);
        Assert.Contains("Botni ulash", page);

        // Noto'g'ri token — xato sahifada, token qayta chiqmaydi.
        var bad = await PostAsync(c, details + "?handler=BotConnect", new() { ["BotToken"] = "bad" }, tokenFrom: details);
        string badHtml = await bad.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        Assert.Contains("Token noto&#x27;g&#x27;ri", badHtml);
        Assert.DoesNotContain("value=\"bad\"", badHtml);

        var ok = await PostAsync(c, details + "?handler=BotConnect", new() { ["BotToken"] = " 123:secret-token " }, tokenFrom: details);
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        var call = fake.Calls.Last(x => x.Op == "bot");
        Assert.Equal(("apps-firma", $"https://apps-firma.{PanelFactory.BaseDomain}/", "apps-firma", "123:secret-token"), (call.Slug, call.Erp, call.ErpTenant, call.Token));

        // matnlar resx'dan @L.T orqali — apostrof HTML-kodlanadi
        page = WebUtility.HtmlDecode(await (await c.GetAsync(details)).Content.ReadAsStringAsync());
        Assert.Contains("@firma_bot", page);
        Assert.Contains("Botni uzish va tozalash", page);

        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(c, details + "?handler=SiteEnable", [], tokenFrom: details)).StatusCode);
        Assert.Contains(fake.Calls, x => x.Op == "site" && x.Erp == $"https://apps-firma.{PanelFactory.BaseDomain}/");
        page = await (await c.GetAsync(details)).Content.ReadAsStringAsync();
        Assert.Contains("https://apps-firma.sayt.x.uz", page);

        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(c, details + "?handler=BotDisconnect", [], tokenFrom: details)).StatusCode);
        Assert.Contains(fake.Calls, x => x.Op == "bot-delete" && x.Slug == "apps-firma");   // uzish = to'liq tozalash, o'chirib qo'yish emas
        Assert.DoesNotContain("@firma_bot", WebUtility.HtmlDecode(await (await c.GetAsync(details)).Content.ReadAsStringAsync()));

        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var actions = await db.AuditLog.Where(a => a.Action.StartsWith("tenant.bot") || a.Action.StartsWith("tenant.site")).Select(a => a.Action + " " + a.After).ToListAsync();
            Assert.Contains(actions, a => a.StartsWith("tenant.bot-connect"));
            Assert.Contains(actions, a => a.StartsWith("tenant.site-enable"));
            Assert.Contains(actions, a => a.StartsWith("tenant.bot-disconnect"));
            Assert.DoesNotContain(actions, a => a.Contains("secret-token"));
            return 0;
        });

        // Butunlay o'chirish (arxivlangan firma): avval bot va sayt ilovalaridan firma yozuvlari, so'ng Delete job navbatga
        int tenantId = int.Parse(details.Split('/').Last());
        var (serverId, _) = await ProvisioningQueueTests.CreateServerAsync(f, "apps-srv-del");
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var t = await db.Tenants.FirstAsync(t => t.Id == tenantId);
            t.Status = TenantStatus.Archived;
            t.ServerId = serverId;
            return await db.SaveChangesAsync();
        });
        var del = await PostAsync(c, $"/Tenants/Delete/{tenantId}",
            new() { ["ConfirmSlug"] = "apps-firma", ["Reason"] = "test", ["Understand"] = "true", ["Step"] = "2" });
        Assert.True(del.StatusCode == HttpStatusCode.Redirect,
            WebUtility.HtmlDecode(Regex.Match(await del.Content.ReadAsStringAsync(), "validation-summary-errors.{0,400}", RegexOptions.Singleline).Value));
        Assert.Contains(fake.Calls, x => x.Op == "bot-delete" && x.Slug == "apps-firma");
        Assert.Contains(fake.Calls, x => x.Op == "site-delete" && x.Slug == "apps-firma");
        Assert.Equal(1, await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().ProvisioningJobs
            .CountAsync(j => j.TenantId == tenantId && j.Type == ProvisioningJobType.Delete)));
    }

    [Fact]
    public async Task ArchiveJobSucceeded_DisablesSiteAndBot()
    {
        // Arxivlangan firmaning sayti eski katalog nusxasi bilan ochiq qolmasligi kerak (demodist.sayt holati)
        var fake = new FakeApps();
        using var app = f.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => { s.RemoveAll<IAppsClient>(); s.AddSingleton<IAppsClient>(fake); }));
        var (serverId, _) = await ProvisioningQueueTests.CreateServerAsync(f, "apps-srv-arch");
        await fake.EnableSiteAsync("apps-arch", "https://apps-arch.x.uz/", "apps-arch");
        await fake.ConnectBotAsync("apps-arch", "https://apps-arch.x.uz/", "apps-arch", "1:t");

        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ControlDbContext>();
        var t = await sp.GetRequiredService<TenantService>().CreateAsync(
            new Tenant { Slug = "apps-arch", LegalName = "Apps Arch", Deployment = DeploymentKind.SaaS, ServerId = serverId }, true);
        t = await db.Tenants.FirstAsync(x => x.Id == t.Id);
        t.ProvisionedAtUtc = DateTime.UtcNow.AddDays(-30);
        var job = new ProvisioningJob
        {
            TenantId = t.Id, Type = ProvisioningJobType.Archive, Status = ProvisioningJobStatus.Running, Attempts = 1,
            LeaseToken = "lease-arch", LeasedByServerId = serverId, LeaseUntilUtc = DateTime.UtcNow.AddMinutes(5),
            NotBeforeUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow,
        };
        db.ProvisioningJobs.Add(job);
        await db.SaveChangesAsync();

        var server = await db.Servers.FirstAsync(s => s.Id == serverId);
        var r = await sp.GetRequiredService<ProvisioningService>().CompleteAsync(server, job.Id,
            new JobCompletion { LeaseToken = "lease-arch", Outcome = JobOutcome.Succeeded, ArchiveLocation = @"D:\arch\apps-arch.bak" });

        Assert.Equal(ProvisioningService.CompleteStatus.Ok, r);
        Assert.Equal(TenantStatus.Archived, (await db.Tenants.AsNoTracking().FirstAsync(x => x.Id == t.Id)).Status);
        Assert.False((await fake.GetSiteAsync("apps-arch")).Active);
        Assert.False((await fake.GetBotAsync("apps-arch")).Active);
        Assert.True((await fake.GetSiteAsync("apps-arch")).Exists); // yozuv qoladi — tiklashda qayta yoqiladi
        Assert.True(await db.AuditLog.AnyAsync(a => a.Action == "tenant.apps-disable" && a.EntityId == t.Id.ToString()));
    }

    [Fact]
    public void ReleasedSlug_FreesName_FitsLimit()
    {
        Assert.Equal("smarttech-o12", ProvisioningService.ReleasedSlug("smarttech", 12));
        var longSlug = new string('a', 62) + "b";
        var released = ProvisioningService.ReleasedSlug(longSlug, 345);
        Assert.Equal(63, released.Length);
        Assert.EndsWith("-o345", released);
        Assert.Matches("^[a-z0-9][a-z0-9-]*[a-z0-9]$", released);
    }

    [Fact]
    public async Task InvoiceCreate_AfterRecalc_DefaultSubmitCreatesDraft()
    {
        // Recalc'dan keyin URL ?handler=Recalc bo'lib qoladi — «Qoralama yaratish» yana Recalc'ga ketmasligi kerak.
        await f.EnsureStaffAsync("inv-owner@test.uz", StaffRole.Owner);
        using var app = f.WithWebHostBuilder(b => b.UseSetting("ControlPanel:Security:RequireStaffTotp", "false"));
        using var c = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await PostAsync(c, "/Account/Login", new() { ["Email"] = "inv-owner@test.uz", ["Password"] = "TestParol-2026xx" });
        var create = await PostAsync(c, "/Tenants/Create", new()
        {
            ["Input.Slug"] = "inv-firma", ["Input.LegalName"] = "Inv Firma MChJ", ["Input.Deployment"] = "SaaS",
            ["Input.CompanyId"] = "1", ["Input.Inn"] = "323456789",
        });
        int tenantId = int.Parse(create.Headers.Location!.OriginalString.Split('/').Last());

        var recalc = await PostAsync(c, $"/Invoices/Create?handler=Recalc", new() { ["TenantId"] = tenantId.ToString(), ["PeriodCount"] = "1" },
            tokenFrom: $"/Invoices/Create?tenantId={tenantId}");
        string html = await recalc.Content.ReadAsStringAsync();
        var action = WebUtility.HtmlDecode(Regex.Match(html, "<form method=\"post\" action=\"([^\"]*)\"[^>]*class=\"stack card\"").Groups[1].Value);
        Assert.True(action.StartsWith("/Invoices/Create"), "action=" + action + " status=" + (int)recalc.StatusCode + " loc=" + recalc.Headers.Location);
        Assert.DoesNotContain("handler", action);
        Assert.Matches("name=\"__RequestVerificationToken\"", html);

        var draft = await c.PostAsync(action, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html), ["TenantId"] = tenantId.ToString(), ["PeriodCount"] = "1",
            ["Lines[0].Description"] = "Xizmat", ["Lines[0].Quantity"] = "1", ["Lines[0].UnitPrice"] = "100000", ["Lines[0].VatRate"] = "12",
        }));
        Assert.Equal(HttpStatusCode.Redirect, draft.StatusCode);
        Assert.Contains("/Invoices/Details/", draft.Headers.Location!.OriginalString);
    }
}
