using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Hosting;
using tahsinERP.ControlPanel.Pages.Public;
using tahsinERP.ControlPanel.Services;
using static tahsinERP.ControlPanel.Tests.PortalTestKit;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Ommaviy sayt (tahsinerp.uz): uch til, zona ajratilishi va demo so'rovi formasi.</summary>
public class LandingTests : IClassFixture<PanelFactory>
{
    private const string PublicBase = "https://www.localhost";
    private readonly PanelFactory f;
    public LandingTests(PanelFactory factory) => f = factory;

    [Fact]
    public async Task Landing_ServesThreeLanguages_OnlyOnPublicHost()
    {
        var (web, _) = Client(f, PublicBase);
        foreach (var (url, marker) in new[] { ("/", "lang=\"uz\""), ("/ru", "lang=\"ru\""), ("/en", "lang=\"en\"") })
        {
            var r = await web.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            string html = await r.Content.ReadAsStringAsync();
            Assert.Contains(marker, html);
            Assert.Contains("data-demo-form", html);
        }
        // Kirill matni entity'siz chiqadi (UnicodeRanges.All).
        Assert.Contains("Запросить демо", await web.GetStringAsync("/ru"));
        Assert.Equal(HttpStatusCode.MovedPermanently, (await web.GetAsync("/uz")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await web.GetAsync("/de")).StatusCode);
        // Xodim va portal sahifalari ommaviy host'da yo'q; sahifa esa admin/portal host'larida yo'q.
        foreach (var url in new[] { "/Tenants", "/Leads", "/Account/Login", "/Login", "/Invoices" })
            Assert.True((await web.GetAsync(url)).StatusCode == HttpStatusCode.NotFound, "www " + url);
        foreach (var baseUrl in new[] { AdminBase, PortalBase })
        {
            var (other, _) = Client(f, baseUrl);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/ru")).StatusCode);
        }
    }

    [Fact]
    public async Task DemoForm_SavesRequest_ReturnsJson_AndRejectsBadInput()
    {
        var (web, _) = Client(f, PublicBase);
        string phone = "+998 90 " + Random.Shared.Next(100, 999) + " 11 22";
        var ok = await PostDemoAsync(web, "/en", new()
        {
            ["Input.FullName"] = "Ali Valiyev", ["Input.Company"] = "Demo Test LLC", ["Input.Phone"] = phone,
            ["Input.Industry"] = "distribution", ["Input.CompanySize"] = "21-100", ["Input.Plan"] = "DIST",
            ["Input.Modules"] = "crm", ["Input.Message"] = "Telegram orders",
        });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.True(JsonDocument.Parse(await ok.Content.ReadAsStringAsync()).RootElement.GetProperty("ok").GetBoolean());

        string normalized = DemoRequestService.NormalizePhone(phone);
        var saved = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().DemoRequests.SingleAsync(r => r.Phone == normalized));
        Assert.Equal("Demo Test LLC", saved.Company);
        Assert.Equal("en", saved.Lang);
        Assert.Equal("distribution", saved.Industry);
        Assert.Equal("DIST", saved.Plan);
        Assert.Equal("crm", saved.Modules);
        Assert.Equal(DemoRequestStatus.New, saved.Status);

        // Qayta yuborish (ikki marta bosish) — yangi yozuv emas.
        await PostDemoAsync(web, "/en", new() { ["Input.FullName"] = "Ali", ["Input.Company"] = "Demo Test LLC", ["Input.Phone"] = phone });
        Assert.Equal(1, await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().DemoRequests.CountAsync(r => r.Phone == normalized)));

        var bad = await PostDemoAsync(web, "/", new() { ["Input.FullName"] = "X", ["Input.Company"] = "Y", ["Input.Phone"] = "12" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.False(JsonDocument.Parse(await bad.Content.ReadAsStringAsync()).RootElement.GetProperty("ok").GetBoolean());

        // Honeypot to'ldirilgan — bot: muvaffaqiyat ko'rinadi, lekin hech narsa saqlanmaydi.
        var bot = await PostDemoAsync(web, "/", new()
        {
            ["Input.FullName"] = "Bot", ["Input.Company"] = "Spam Inc", ["Input.Phone"] = "+998 99 000 00 01", ["Input.Website"] = "http://spam",
        });
        Assert.Equal(HttpStatusCode.OK, bot.StatusCode);
        Assert.False(await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().DemoRequests.AnyAsync(r => r.Company == "Spam Inc")));
    }

    [Fact]
    public async Task DemoForm_EmailsStaff_WhenSmtpConfigured()
    {
        f.Email.Enabled = true;
        try
        {
            var (web, _) = Client(f, PublicBase);
            var r = await PostDemoAsync(web, "/", new()
            {
                ["Input.FullName"] = "Email Test", ["Input.Company"] = "Mail Co", ["Input.Phone"] = "+998 93 " + Random.Shared.Next(100, 999) + " 33 44",
            });
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var mail = Assert.Single(f.Email.Sent, m => m.Subject.Contains("Mail Co"));
            Assert.Equal("khojiabror@schain.uz", mail.To); // appsettings.json -> ControlPanel:Leads:NotifyEmail
            Assert.Contains("Email Test", mail.Body);
        }
        finally { f.Email.Enabled = false; }
    }

    [Fact]
    public void PublicHosts_DefaultToApexAndWww()
    {
        Assert.Equal(["tahsinerp.uz", "www.tahsinerp.uz"], new PanelHostOptions { Domain = "tahsinerp.uz" }.EffectivePublicHosts);
        Assert.Equal(["www.localhost"], new PanelHostOptions { Domain = "localhost" }.EffectivePublicHosts);
        Assert.Equal(["a.uz", "b.uz"], new PanelHostOptions { Domain = "x.uz", PublicHosts = "a.uz, b.uz" }.EffectivePublicHosts);
    }

    [Fact]
    public void Content_HasSameShapeInAllLanguages()
    {
        foreach (var path in new[] { "modules.items", "faq.items", "plans.items", "flow.steps", "showcase.tabs", "compare.rows", "process.steps" })
        {
            var counts = LandingContent.Languages.Select(l => LandingContent.For(l).List(path).Count()).Distinct().ToList();
            Assert.True(counts.Count == 1 && counts[0] > 0, path);
        }
        foreach (var l in LandingContent.Languages)
        {
            var c = LandingContent.For(l);
            Assert.False(string.IsNullOrEmpty(c["demo.okTitle"]), l);
            foreach (var code in DemoRequestService.ModuleCodes)
                Assert.False(string.IsNullOrEmpty(c["demo.form.moduleNames." + code]), $"{l} {code}");
            foreach (var code in DemoRequestService.Industries)
                Assert.False(string.IsNullOrEmpty(c["demo.form.industries." + code]), $"{l} {code}");
        }
    }

    [Theory]
    [InlineData("\"Baraka Plast\" MChJ", "baraka-plast")]
    [InlineData("ООО «Тахсин Групп»", "taxsin-grupp")] // o'zbek lotini: х → x
    [InlineData("iPack", "ipack")]
    public void TrialSlug_SuggestedFromCompanyName(string company, string slug) =>
        Assert.Equal(slug, Pages.Tenants.TrialModel.SuggestSlug(company));

    private static async Task<HttpResponseMessage> PostDemoAsync(HttpClient c, string page, Dictionary<string, string> form)
    {
        form["__RequestVerificationToken"] = await TokenAsync(c, page);
        var req = new HttpRequestMessage(HttpMethod.Post, page.TrimEnd('/') + "?handler=Demo") { Content = new FormUrlEncodedContent(form) };
        req.Headers.Accept.ParseAdd("application/json");
        return await c.SendAsync(req);
    }
}
