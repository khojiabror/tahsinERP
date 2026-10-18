using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Brauzersiz UI oqimi: login → 2FA ulash (majburiy) → sahifalar, rollar, antiforgery.</summary>
public partial class WebUiTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();

    [GeneratedRegex("<p class=\"secret mono\">([A-Z2-7 ]+)</p>")]
    private static partial Regex SecretRegex();

    private static async Task<string> TokenAsync(HttpClient c, string url)
    {
        var html = await (await c.GetAsync(url)).Content.ReadAsStringAsync();
        return WebUtility.HtmlDecode(TokenRegex().Match(html).Groups[1].Value);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient c, string url, Dictionary<string, string> form, string? tokenFrom = null)
    {
        form["__RequestVerificationToken"] = await TokenAsync(c, tokenFrom ?? url);
        return await c.PostAsync(url, new FormUrlEncodedContent(form));
    }

    /// <summary>To'liq kirish; qaytaradi: autentifikatsiya qilingan klient.</summary>
    private async Task<HttpClient> LoginAsync(string email, StaffRole role)
    {
        await f.EnsureStaffAsync(email, role);
        var c = f.Https();
        var r = await PostAsync(c, "/Account/Login", new() { ["Email"] = email, ["Password"] = "TestParol-2026xx" });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        string next = r.Headers.Location!.OriginalString;
        Assert.Contains("/Account/EnrollTotp", next);

        // 2FA cookie bilan bosh sahifa hali yopiq.
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/")).StatusCode);

        string html = await (await c.GetAsync(next)).Content.ReadAsStringAsync();
        string secret = SecretRegex().Match(html).Groups[1].Value.Replace(" ", "");
        string protectedSecret = WebUtility.HtmlDecode(Regex.Match(html, "name=\"ProtectedSecret\" value=\"([^\"]+)\"").Groups[1].Value);
        string token = WebUtility.HtmlDecode(TokenRegex().Match(html).Groups[1].Value);
        string code = Totp.Compute(Base32.Decode(secret), Totp.CurrentStep(f.Time.GetUtcNow()));
        var done = await c.PostAsync(next, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ProtectedSecret"] = protectedSecret, ["Code"] = code, ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
        return c;
    }

    [Fact]
    public async Task Anonymous_IsRedirectedToLogin_ApiIsOpen()
    {
        using var c = f.Https();
        var r = await c.GetAsync("/Tenants");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Account/Login", r.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Account/Login")).StatusCode);
    }

    [Fact]
    public async Task WrongPassword_GenericError_AndLockout()
    {
        await f.EnsureStaffAsync("lock@test.uz", StaffRole.Sales);
        using var c = f.Https();
        for (int i = 0; i < StaffAuthService.MaxFailedAttempts; i++)
        {
            var r = await PostAsync(c, "/Account/Login", new() { ["Email"] = "lock@test.uz", ["Password"] = "noto'g'ri-parol-1" });
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }
        var locked = await PostAsync(c, "/Account/Login", new() { ["Email"] = "lock@test.uz", ["Password"] = "TestParol-2026xx" });
        Assert.Contains("bloklangan", await locked.Content.ReadAsStringAsync());
        var unknown = await PostAsync(c, "/Account/Login", new() { ["Email"] = "yoq@test.uz", ["Password"] = "x" });
        Assert.Contains("Email yoki parol noto&#x27;g&#x27;ri", await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Owner_FullFlow_PagesRender_And_SecondLoginUsesTotp()
    {
        using var c = await LoginAsync("ui-owner@test.uz", StaffRole.Owner);
        foreach (var url in new[] { "/", "/Tenants", "/Tenants/Create", "/Invoices", "/Licenses", "/Plans", "/Plans/Edit/2", "/Servers", "/Servers/Edit", "/Staff", "/Staff/Create", "/Audit", "/Notifications", "/Account/ChangePassword" })
        {
            var r = await c.GetAsync(url);
            Assert.True(r.StatusCode == HttpStatusCode.OK, $"{url}: {(int)r.StatusCode}");
        }

        // Tashkilot yaratish formasi orqali (antiforgery bilan).
        var create = await PostAsync(c, "/Tenants/Create", new()
        {
            ["Input.Slug"] = "ui-firma", ["Input.LegalName"] = "UI Firma MChJ", ["Input.Deployment"] = "SaaS",
            ["Input.CompanyId"] = "1", ["Input.Inn"] = "123456789", ["StartTrial"] = "true",
        });
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        string details = create.Headers.Location!.OriginalString;
        var page = await (await c.GetAsync(details)).Content.ReadAsStringAsync();
        Assert.Contains("UI Firma MChJ", page);
        Assert.Contains("Sinov", page);

        // reg_token bir marta ko'rsatiladi.
        var tok = await PostAsync(c, details + "?handler=RegToken", [], tokenFrom: details);
        Assert.Matches("RT-[A-Z2-7]{32}", await tok.Content.ReadAsStringAsync());

        // Antiforgery'siz POST rad etiladi.
        var noToken = await c.PostAsync("/Tenants/Create", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Slug"] = "x" }));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);

        // Ikkinchi kirish: TOTP sahifasi (ulash emas).
        using var c2 = f.Https();
        var r2 = await PostAsync(c2, "/Account/Login", new() { ["Email"] = "ui-owner@test.uz", ["Password"] = "TestParol-2026xx" });
        Assert.Contains("/Account/Totp", r2.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Sales_CannotOpenOwnerPages_OrReversePayments()
    {
        using var c = await LoginAsync("ui-sales@test.uz", StaffRole.Sales);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Tenants/Create")).StatusCode);
        foreach (var url in new[] { "/Staff", "/Audit", "/Servers", "/Plans/Edit/1", "/Licenses/Issue?tenantId=1" })
        {
            var r = await c.GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
            Assert.Contains("/Account/Denied", r.Headers.Location!.OriginalString);
        }
    }

    [Fact]
    public async Task Support_IsReadOnly()
    {
        using var c = await LoginAsync("ui-support@test.uz", StaffRole.Support);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Tenants")).StatusCode);
        Assert.Contains("/Account/Denied", (await c.GetAsync("/Tenants/Create")).Headers.Location!.OriginalString);
        await f.WithScopeAsync(async sp =>
            Assert.True(await sp.GetRequiredService<ControlDbContext>().AuditLog.AnyAsync(a => a.Action == "staff.totp-enroll")));
    }
}
