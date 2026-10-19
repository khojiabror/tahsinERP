using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Portal;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>
/// Brauzer kabi ishlaydigan cookie qutisi: cookie faqat uni o'rnatgan host'ga qaytariladi (Domain'siz = host-only).
/// <see cref="Inject"/> — ataylab boshqa host'ga cookie yuborish (izolyatsiyani sinash uchun).
/// </summary>
public sealed class HostCookieJar : DelegatingHandler
{
    private readonly Dictionary<string, Dictionary<string, string>> _byHost = new(StringComparer.OrdinalIgnoreCase);
    public List<string> RawSetCookies { get; } = [];
    public Dictionary<string, string> Inject { get; } = [];

    public IReadOnlyDictionary<string, string> For(string host) =>
        _byHost.TryGetValue(host, out var d) ? d : new Dictionary<string, string>();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        string host = request.RequestUri!.Host;
        var cookies = new Dictionary<string, string>(For(host));
        foreach (var kv in Inject) cookies[kv.Key] = kv.Value;
        if (cookies.Count > 0)
            request.Headers.Add("Cookie", string.Join("; ", cookies.Select(kv => kv.Key + "=" + kv.Value)));
        var response = await base.SendAsync(request, ct);
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            if (!_byHost.TryGetValue(host, out var jar)) _byHost[host] = jar = new(StringComparer.Ordinal);
            foreach (var raw in values)
            {
                RawSetCookies.Add(raw);
                string first = raw.Split(';')[0];
                int eq = first.IndexOf('=');
                string name = first[..eq].Trim(), value = first[(eq + 1)..].Trim();
                bool expired = raw.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase) || value.Length == 0;
                if (expired) jar.Remove(name); else jar[name] = value;
            }
        }
        return response;
    }
}

public static partial class PortalTestKit
{
    public const string Password = "MijozParol-2026x";
    public const string PortalBase = "https://my.localhost";
    public const string AdminBase = "https://admin.localhost";

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"kirish kodi (\d{6})")]
    private static partial Regex OtpRegex();

    [GeneratedRegex(@"start[= ](L[A-Z2-7]+)")]
    public static partial Regex LinkRegex();

    public static (HttpClient Client, HostCookieJar Jar) Client(PanelFactory f, string baseUrl)
    {
        var jar = new HostCookieJar();
        var c = f.CreateDefaultClient(new Uri(baseUrl), jar);
        return (c, jar);
    }

    public static async Task<string> TokenAsync(HttpClient c, string url)
    {
        var html = await (await c.GetAsync(url)).Content.ReadAsStringAsync();
        var m = TokenRegex().Match(html);
        Assert.True(m.Success, $"{url}: antiforgery token topilmadi");
        return WebUtility.HtmlDecode(m.Groups[1].Value);
    }

    public static async Task<HttpResponseMessage> PostAsync(HttpClient c, string url, Dictionary<string, string> form, string? tokenFrom = null)
    {
        form["__RequestVerificationToken"] = await TokenAsync(c, tokenFrom ?? url);
        return await c.PostAsync(url, new FormUrlEncodedContent(form));
    }

    public static string Html(HttpResponseMessage r) => WebUtility.HtmlDecode(r.Content.ReadAsStringAsync().GetAwaiter().GetResult());

    /// <summary>Oxirgi yuborilgan kirish kodi (shu chatga).</summary>
    public static string LastOtp(PanelFactory f, string chat)
    {
        var msg = f.Telegram.To(chat).LastOrDefault(m => OtpRegex().IsMatch(m.Text));
        Assert.True(msg.Text is not null, $"{chat} chatiga kod kelmadi");
        return OtpRegex().Match(msg.Text).Groups[1].Value;
    }

    /// <summary>Portal foydalanuvchisi (xizmat orqali) + taklif tokeni.</summary>
    public static Task<(PortalUser User, string Token)> InviteAsync(PanelFactory f, int tenantId, string email, PortalRole role = PortalRole.Owner, string? phone = null) =>
        f.WithScopeAsync(async sp =>
        {
            int staff = await f.EnsureStaffAsync();
            var auth = sp.GetRequiredService<PortalAuthService>();
            var u = await auth.CreateUserAsync(tenantId, "Mijoz " + email, email, phone, role, staff);
            var r = await auth.CreateInviteAsync(u, staff, PortalBase, sp.GetRequiredService<PortalNotifier>());
            return (u, r.Token);
        });

    /// <summary>Taklif → parol → (Telegram bog'lash, agar chat berilsa) — UI orqali.</summary>
    public static async Task AcceptInviteAsync(PanelFactory f, string token, string? linkChat)
    {
        var (c, _) = Client(f, PortalBase);
        string url = "/Invite?token=" + Uri.EscapeDataString(token);
        var r = await PostAsync(c, url, new() { ["Token"] = token, ["Password"] = Password, ["Confirm"] = Password });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        string html = Html(r);
        Assert.Contains("Parol o'rnatildi", html);
        if (linkChat is null) return;
        var m = LinkRegex().Match(html);
        Assert.True(m.Success, "Telegram bog'lash havolasi ko'rsatilmadi");
        var hook = await WebhookAsync(f, linkChat, "/start " + m.Groups[1].Value);
        Assert.Equal(HttpStatusCode.OK, hook.StatusCode);
    }

    public static async Task<HttpResponseMessage> WebhookAsync(PanelFactory f, string chat, string text, string? secret = PanelFactory.WebhookSecret)
    {
        var (c, _) = Client(f, PortalBase);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/telegram/webhook")
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new
            {
                update_id = 1,
                message = new { message_id = 5, chat = new { id = long.Parse(chat), type = "private" }, text },
            }), System.Text.Encoding.UTF8, "application/json"),
        };
        if (secret is not null) req.Headers.Add("X-Telegram-Bot-Api-Secret-Token", secret);
        return await c.SendAsync(req);
    }

    /// <summary>To'liq kirish (parol + Telegram kodi); qaytaradi: kirgan portal klienti.</summary>
    public static async Task<(HttpClient Client, HostCookieJar Jar)> LoginAsync(PanelFactory f, string login, string chat)
    {
        var (c, jar) = Client(f, PortalBase);
        var r = await PostAsync(c, "/Login", new() { ["Login"] = login, ["Password"] = Password });
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Login: " + Html(r));
        Assert.StartsWith("/Otp", r.Headers.Location!.OriginalString);
        var done = await PostAsync(c, "/Otp", new() { ["Code"] = LastOtp(f, chat) });
        Assert.True(done.StatusCode == HttpStatusCode.Redirect, "Otp: " + Html(done));
        Assert.Equal("/", done.Headers.Location!.OriginalString);
        return (c, jar);
    }

    public static Task SetPricesAsync(PanelFactory f) => f.WithScopeAsync(async sp =>
    {
        var db = sp.GetRequiredService<ControlDbContext>();
        foreach (var p in await db.Plans.Where(p => p.Code != PlanCodes.Trial).ToListAsync())
        {
            p.PriceMonthTiyin = p.SortOrder * 100_000_000L;
            p.PriceYearTiyin = p.SortOrder * 1_000_000_000L;
        }
        await db.SaveChangesAsync();
    });

    /// <summary>Tenant'ga to'langan pullik obuna (paket, oylar) — hisob-faktura chiqarib, to'liq to'lab.</summary>
    public static Task<Subscription> PaidAsync(PanelFactory f, int tenantId, string planCode, int months, DateOnly start) => f.WithScopeAsync(async sp =>
    {
        int staff = await f.EnsureStaffAsync();
        var db = sp.GetRequiredService<ControlDbContext>();
        var t = await db.Tenants.FirstAsync(x => x.Id == tenantId);
        var plan = await db.Plans.SingleAsync(p => p.Code == planCode);
        var sub = await sp.GetRequiredService<SubscriptionService>().CreateAsync(t, plan.Id, BillingPeriod.Month, null, start, false);
        var billing = sp.GetRequiredService<BillingService>();
        var inv = await billing.CreateDraftAsync(t.Id, sub.Id, months, [BillingService.DefaultLine(sub, plan, months)], null, staff);
        await billing.IssueAsync(inv);
        await billing.RecordPaymentAsync(inv.Id, inv.TotalTiyin, sp.GetRequiredService<Clock>().Today, PaymentMethod.Bank, "PT", null, staff);
        return await db.Subscriptions.AsNoTracking().FirstAsync(s => s.Id == sub.Id);
    });

    /// <summary>Xodim (Owner) sifatida admin host'da to'liq kirish (parol + TOTP ulash).</summary>
    public static async Task<(HttpClient Client, HostCookieJar Jar)> StaffLoginAsync(PanelFactory f, string email, StaffRole role, string baseUrl = AdminBase)
    {
        await f.EnsureStaffAsync(email, role);
        var (c, jar) = Client(f, baseUrl);
        var r = await PostAsync(c, "/Account/Login", new() { ["Email"] = email, ["Password"] = "TestParol-2026xx" });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        string next = r.Headers.Location!.OriginalString;
        string html = await (await c.GetAsync(next)).Content.ReadAsStringAsync();
        string secret = Regex.Match(html, "<p class=\"secret mono\">([A-Z2-7 ]+)</p>").Groups[1].Value.Replace(" ", "");
        string protectedSecret = WebUtility.HtmlDecode(Regex.Match(html, "name=\"ProtectedSecret\" value=\"([^\"]+)\"").Groups[1].Value);
        string token = WebUtility.HtmlDecode(TokenRegex().Match(html).Groups[1].Value);
        string code = Totp.Compute(Base32.Decode(secret), Totp.CurrentStep(f.Time.GetUtcNow()));
        var done = await c.PostAsync(next, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ProtectedSecret"] = protectedSecret, ["Code"] = code, ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
        return (c, jar);
    }
}
