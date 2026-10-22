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
        Assert.Contains("Botni o'chirish", page);

        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(c, details + "?handler=SiteEnable", [], tokenFrom: details)).StatusCode);
        Assert.Contains(fake.Calls, x => x.Op == "site" && x.Erp == $"https://apps-firma.{PanelFactory.BaseDomain}/");
        page = await (await c.GetAsync(details)).Content.ReadAsStringAsync();
        Assert.Contains("https://apps-firma.sayt.x.uz", page);

        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(c, details + "?handler=BotDisable", [], tokenFrom: details)).StatusCode);

        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var actions = await db.AuditLog.Where(a => a.Action.StartsWith("tenant.bot") || a.Action.StartsWith("tenant.site")).Select(a => a.Action + " " + a.After).ToListAsync();
            Assert.Contains(actions, a => a.StartsWith("tenant.bot-connect"));
            Assert.Contains(actions, a => a.StartsWith("tenant.site-enable"));
            Assert.Contains(actions, a => a.StartsWith("tenant.bot-disable"));
            Assert.DoesNotContain(actions, a => a.Contains("secret-token"));
            return 0;
        });
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
