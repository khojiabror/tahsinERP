using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Licensing;
using tahsinERP.ControlPanel.Portal;
using tahsinERP.ControlPanel.Services;
using static tahsinERP.ControlPanel.Tests.PortalTestKit;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Host ajratilishi: xodim va mijoz zonalari bir-biriga kira olmaydi (404, alohida sxema/cookie).</summary>
public class PortalHostIsolationTests : IClassFixture<PanelFactory>
{
    private readonly PanelFactory f;
    public PortalHostIsolationTests(PanelFactory factory) { f = factory; f.Telegram.Enabled = true; }

    private static readonly string[] StaffOnly = ["/Account/Login", "/Tenants", "/Tenants/Create", "/Plans", "/Audit", "/Staff", "/Licenses/Issue?tenantId=1", "/Account/ChangePassword"];
    private static readonly string[] PortalOnly = ["/Login", "/Otp", "/Invite?token=PI-X", "/Payment", "/Profile", "/Logout"];

    [Fact]
    public async Task StaffPages_404_OnPortalHost_AndPortalPages_404_OnAdminHosts()
    {
        var (my, _) = Client(f, PortalBase);
        foreach (var url in StaffOnly)
            Assert.True((await my.GetAsync(url)).StatusCode == HttpStatusCode.NotFound, "my.* " + url);
        foreach (var baseUrl in new[] { AdminBase, "https://localhost" })
        {
            var (admin, _) = Client(f, baseUrl);
            foreach (var url in PortalOnly)
                Assert.True((await admin.GetAsync(url)).StatusCode == HttpStatusCode.NotFound, baseUrl + url);
            // Bir xil yo'l — har zona o'z kirish sahifasiga yuboradi.
            var root = await admin.GetAsync("/Invoices");
            Assert.Equal(HttpStatusCode.Redirect, root.StatusCode);
            Assert.Contains("/Account/Login", root.Headers.Location!.OriginalString);
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/health")).StatusCode);
        }
        foreach (var url in new[] { "/", "/Invoices", "/Requests" })
        {
            var r = await my.GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
            string loc = r.Headers.Location!.OriginalString;
            Assert.Contains("/Login", loc);
            Assert.DoesNotContain("/Account/", loc);
        }
        Assert.Equal(HttpStatusCode.OK, (await my.GetAsync("/Login")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await my.GetAsync("/health")).StatusCode);
        // Telegram webhook — zonasiz, lekin sirsiz 404.
        Assert.Equal(HttpStatusCode.NotFound, (await WebhookAsync(f, "1", "/start", secret: null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await WebhookAsync(f, "1", "/start", secret: "noto'g'ri")).StatusCode);
    }

    [Fact]
    public async Task Cookies_AreSeparate_AndNeverCrossZones()
    {
        var tenant = await f.CreateTenantAsync("iso-a");
        var (_, token) = await InviteAsync(f, tenant.Id, "iso@mijoz.uz");
        await AcceptInviteAsync(f, token, linkChat: "7001");
        var (my, myJar) = await LoginAsync(f, "iso@mijoz.uz", "7001");
        Assert.Equal(HttpStatusCode.OK, (await my.GetAsync("/")).StatusCode);

        // Portal cookie: boshqa nom, Domain'siz (faqat my.* host'i), Secure, HttpOnly, Path=/.
        var portalCookie = myJar.For("my.localhost");
        Assert.True(portalCookie.ContainsKey("__Host-tcp.my"));
        Assert.DoesNotContain(portalCookie.Keys, k => k == "tcp.auth");
        string raw = myJar.RawSetCookies.Last(c => c.StartsWith("__Host-tcp.my="));
        Assert.Contains("secure", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", raw, StringComparison.OrdinalIgnoreCase);

        // Mijoz cookie'si admin host'ga ataylab yuborilsa ham — xodim sahifalari yopiq.
        var (admin, adminJar) = Client(f, AdminBase);
        adminJar.Inject["__Host-tcp.my"] = portalCookie["__Host-tcp.my"];
        foreach (var url in new[] { "/", "/Tenants", "/Requests", "/Account/ChangePassword" })
        {
            var r = await admin.GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
            Assert.Contains("/Account/Login", r.Headers.Location!.OriginalString);
        }

        // Xodim cookie'si portal host'ga yuborilsa — portal yopiq, xodim sahifalari esa 404.
        var (staff, staffJar) = await StaffLoginAsync(f, "iso-staff@test.uz", StaffRole.Owner);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/Tenants")).StatusCode);
        var staffCookie = staffJar.For("admin.localhost");
        Assert.True(staffCookie.ContainsKey("tcp.auth"));
        var (my2, my2Jar) = Client(f, PortalBase);
        my2Jar.Inject["tcp.auth"] = staffCookie["tcp.auth"];
        foreach (var url in new[] { "/", "/Invoices", "/Profile" })
        {
            var r = await my2.GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
            Assert.Contains("/Login", r.Headers.Location!.OriginalString);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await my2.GetAsync("/Tenants")).StatusCode);
    }

    [Fact]
    public async Task OtherHostsNone_OnlyNamedHostsServePages()
    {
        using var strict = f.WithWebHostBuilder(b => b.UseSetting("ControlPanel:Hosts:OtherHosts", "None"));
        using var local = strict.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.NotFound, (await local.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await local.GetAsync("/Account/Login")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await local.GetAsync("/Login")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await local.GetAsync("/health")).StatusCode);
        using var admin = strict.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(AdminBase), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Account/Login")).StatusCode);
        using var my = strict.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(PortalBase), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.OK, (await my.GetAsync("/Login")).StatusCode);
    }
}

/// <summary>Taklif → parol → Telegram bog'lash → email/telefon + parol → bir martalik kod → kirish.</summary>
public class PortalLoginTests : IClassFixture<PanelFactory>
{
    private readonly PanelFactory f;
    public PortalLoginTests(PanelFactory factory) { f = factory; f.Telegram.Enabled = true; }

    [Fact]
    public async Task Invite_SetPassword_LinkTelegram_Otp_Login_Logout()
    {
        var tenant = await f.CreateTenantAsync("pl-a");
        var (user, token) = await InviteAsync(f, tenant.Id, "A@Mijoz.uz");
        Assert.Equal("a@mijoz.uz", user.Email);

        // Token bazada faqat xesh.
        await f.WithScopeAsync(async sp =>
        {
            var inv = await sp.GetRequiredService<ControlDbContext>().PortalInvites.SingleAsync(i => i.PortalUserId == user.Id);
            Assert.Equal(PortalTokens.Hash(token), inv.TokenHash);
            Assert.DoesNotContain(token, inv.TokenHash);
        });

        var (c, _) = Client(f, PortalBase);
        string url = "/Invite?token=" + Uri.EscapeDataString(token);
        Assert.Contains("Xush kelibsiz", Html(await c.GetAsync(url)));
        var weak = await PostAsync(c, url, new() { ["Token"] = token, ["Password"] = "qisqa1", ["Confirm"] = "qisqa1" });
        Assert.Contains("kamida 12", Html(weak));

        await AcceptInviteAsync(f, token, linkChat: "5001");
        Assert.Contains(f.Telegram.To("5001"), m => m.Text.Contains("bog'landi"));
        // Havola bir martalik.
        Assert.Contains("yaroqsiz", Html(await c.GetAsync(url)));

        // Parol → kod (shaxsiy chatga) → noto'g'ri kod rad → to'g'ri kod bilan kirish.
        var r = await PostAsync(c, "/Login", new() { ["Login"] = "a@mijoz.uz", ["Password"] = Password });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/")).StatusCode); // kod kiritilmaguncha yopiq
        string code = LastOtp(f, "5001");
        string wrong = code[..5] + (code[5] == '9' ? '0' : (char)(code[5] + 1));
        Assert.Contains("Kod noto'g'ri", Html(await PostAsync(c, "/Otp", new() { ["Code"] = wrong })));
        var ok = await PostAsync(c, "/Otp", new() { ["Code"] = code });
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        string home = Html(await c.GetAsync("/"));
        Assert.Contains("Obuna holati", home);
        Assert.Contains("pl-a MChJ", home);
        Assert.Contains("Sinov", home);

        // Ishlatilgan kod qayta ishlamaydi.
        f.Time.Advance(TimeSpan.FromSeconds(61));
        var (c2, _) = Client(f, PortalBase);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(c2, "/Login", new() { ["Login"] = "a@mijoz.uz", ["Password"] = Password })).StatusCode);
        Assert.Contains("Kod noto'g'ri", Html(await PostAsync(c2, "/Otp", new() { ["Code"] = code })));

        // Chiqish.
        var bye = await PostAsync(c, "/Logout", [], tokenFrom: "/");
        Assert.Equal(HttpStatusCode.Redirect, bye.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/")).StatusCode);

        await f.WithScopeAsync(async sp =>
        {
            var actions = await sp.GetRequiredService<ControlDbContext>().AuditLog.Where(a => a.PortalUserId == user.Id).Select(a => a.Action).ToListAsync();
            foreach (var a in new[] { "portal.password-set", "portal.telegram-link", "portal.otp-send", "portal.login", "portal.logout" })
                Assert.Contains(a, actions);
            var db = sp.GetRequiredService<ControlDbContext>();
            Assert.True(await db.AuditLog.AnyAsync(a => a.Action == "portal.invite" && a.EntityId == user.Id.ToString() && a.StaffUserId != null));
            // Kodlar bazada ochiq holda yo'q.
            Assert.DoesNotContain(await db.PortalOtps.Where(o => o.PortalUserId == user.Id).Select(o => o.CodeHash).ToListAsync(), h => h == code);
        });
    }

    [Fact]
    public async Task PhoneLogin_UsesTenantChat_WhenNoPersonalChat_AndNoChannelIsRefused()
    {
        var tenant = await f.CreateTenantAsync("pl-b");
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            (await db.Tenants.FirstAsync(t => t.Id == tenant.Id)).TelegramChatId = "-100777";
            await db.SaveChangesAsync();
        });
        var (_, token) = await InviteAsync(f, tenant.Id, email: null!, phone: "+998 90 111-22-33");
        await AcceptInviteAsync(f, token, linkChat: null);
        await LoginAsync(f, "90 111 22 33", "-100777");

        // Na shaxsiy, na tenant chati yo'q — kod yuborib bo'lmaydi, kirish yo'q.
        var lone = await f.CreateTenantAsync("pl-c");
        var (_, t2) = await InviteAsync(f, lone.Id, "c@mijoz.uz");
        await AcceptInviteAsync(f, t2, linkChat: null);
        var (c, _) = Client(f, PortalBase);
        var r = await PostAsync(c, "/Login", new() { ["Login"] = "c@mijoz.uz", ["Password"] = Password });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Telegram bog'lanmagan", Html(r));
    }

    [Fact]
    public async Task StaffUi_AddsPortalUser_InviteShownOnce_RoleToggleKillsSession()
    {
        var tenant = await f.CreateTenantAsync("pl-ui");
        var (staff, _) = await StaffLoginAsync(f, "pl-ui-sales@test.uz", StaffRole.Sales);
        string details = $"/Tenants/Details/{tenant.Id}";
        Assert.Contains("Mijoz portali", Html(await staff.GetAsync(details)));
        var add = await PostAsync(staff, details + "?handler=PortalUser",
            new() { ["PuName"] = "Ali Valiyev", ["PuEmail"] = "ali@pl-ui.uz", ["PuPhone"] = "", ["PuRole"] = "Owner" }, tokenFrom: details);
        string html = Html(add);
        var m = System.Text.RegularExpressions.Regex.Match(html, @"https://my\.localhost/Invite\?token=(PI-[A-Z2-7]+)");
        Assert.True(m.Success, "taklif havolasi ko'rsatilmadi");
        Assert.DoesNotContain(m.Groups[1].Value, Html(await staff.GetAsync(details))); // faqat bir marta
        Assert.Contains("ali@pl-ui.uz", Html(await staff.GetAsync(details)));
        Assert.Contains("bu email bilan", Html(await PostAsync(staff, details + "?handler=PortalUser",
            new() { ["PuName"] = "Takror", ["PuEmail"] = "ALI@pl-ui.uz", ["PuRole"] = "Viewer" }, tokenFrom: details)), StringComparison.OrdinalIgnoreCase);

        await AcceptInviteAsync(f, m.Groups[1].Value, linkChat: "5101");
        var (c, _) = await LoginAsync(f, "ali@pl-ui.uz", "5101");
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/")).StatusCode);

        int uid = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().PortalUsers.Where(u => u.Email == "ali@pl-ui.uz").Select(u => u.Id).SingleAsync());
        var toggle = await PostAsync(staff, details + "?handler=PortalToggle&what=role", new() { ["PortalUserId"] = uid.ToString() }, tokenFrom: details);
        Assert.Equal(HttpStatusCode.Redirect, toggle.StatusCode);
        Assert.Equal(PortalRole.Viewer, await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().PortalUsers.Where(u => u.Id == uid).Select(u => u.Role).SingleAsync()));
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/")).StatusCode); // rol o'zgardi — sessiya yopildi
    }

    [Fact]
    public async Task Webhook_StartWithoutCode_RepliesChatId_BadCodeRejected()
    {
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync(f, "8001", "/start")).StatusCode);
        Assert.Contains(f.Telegram.To("8001"), m => m.Text.Contains("chat ID: 8001"));
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync(f, "8001", "/start LAAAAAAAAAAAAAAAAAAAAAAAA")).StatusCode);
        Assert.Contains(f.Telegram.To("8001"), m => m.Text.Contains("yaroqsiz"));
    }
}

/// <summary>Blok, kod chastotasi, parolni tiklash (yangi taklif), muddati o'tgan havola. Vaqt suriladi — alohida fixture.</summary>
public class PortalLockoutTests : IClassFixture<PanelFactory>
{
    private readonly PanelFactory f;
    public PortalLockoutTests(PanelFactory factory) { f = factory; f.Telegram.Enabled = true; }

    [Fact]
    public async Task Lockout_ResendThrottle_ResetInviteKillsSessions_InviteExpires()
    {
        var tenant = await f.CreateTenantAsync("lk-a");
        var (user, token) = await InviteAsync(f, tenant.Id, "lk@mijoz.uz");
        await AcceptInviteAsync(f, token, linkChat: "5003");

        var (c, _) = Client(f, PortalBase);
        for (int i = 0; i < PortalAuthService.MaxFailedAttempts; i++)
            Assert.Contains(PortalAuthService.GenericLoginError, Html(await PostAsync(c, "/Login", new() { ["Login"] = "lk@mijoz.uz", ["Password"] = "Noto'g'ri-parol-1" })));
        Assert.Contains("bloklangan", Html(await PostAsync(c, "/Login", new() { ["Login"] = "lk@mijoz.uz", ["Password"] = Password })));
        // Noma'lum login — xuddi shu umumiy xato (login mavjudligi oshkor bo'lmaydi).
        Assert.Contains(PortalAuthService.GenericLoginError, Html(await PostAsync(c, "/Login", new() { ["Login"] = "yoq@mijoz.uz", ["Password"] = Password })));

        f.Time.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(c, "/Login", new() { ["Login"] = "lk@mijoz.uz", ["Password"] = Password })).StatusCode);
        string first = LastOtp(f, "5003");
        Assert.Contains("yaqinda yuborildi", Html(await PostAsync(c, "/Otp?handler=Resend", [], tokenFrom: "/Otp")));
        f.Time.Advance(TimeSpan.FromSeconds(61));
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(c, "/Otp?handler=Resend", [], tokenFrom: "/Otp")).StatusCode);
        string second = LastOtp(f, "5003");
        if (first != second) // eski kod yaroqsiz bo'ldi
            Assert.Contains("Kod noto'g'ri", Html(await PostAsync(c, "/Otp", new() { ["Code"] = first })));
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(c, "/Otp", new() { ["Code"] = second })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/")).StatusCode);

        // Parolni tiklash: xodim yangi taklif chiqaradi → havola bog'langan chatga ham boradi; parol almashgach eski sessiya yopiladi.
        string reset = await f.WithScopeAsync(async sp =>
        {
            int staff = await f.EnsureStaffAsync();
            var db = sp.GetRequiredService<ControlDbContext>();
            var u = await db.PortalUsers.FirstAsync(x => x.Id == user.Id);
            var r = await sp.GetRequiredService<PortalAuthService>().CreateInviteAsync(u, staff, PortalBase, sp.GetRequiredService<PortalNotifier>());
            Assert.True(r.SentToTelegram);
            return r.Token;
        });
        Assert.Contains(f.Telegram.To("5003"), m => m.Text.Contains("parolni tiklash") && m.Text.Contains(Uri.EscapeDataString(reset)));
        await AcceptInviteAsync(f, reset, linkChat: null);
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/")).StatusCode);

        // Muddati o'tgan havola.
        string late = await f.WithScopeAsync(async sp =>
        {
            int staff = await f.EnsureStaffAsync();
            var u = await sp.GetRequiredService<ControlDbContext>().PortalUsers.FirstAsync(x => x.Id == user.Id);
            return (await sp.GetRequiredService<PortalAuthService>().CreateInviteAsync(u, staff, PortalBase, sp.GetRequiredService<PortalNotifier>())).Token;
        });
        f.Time.Advance(TimeSpan.FromHours(73));
        Assert.Contains("yaroqsiz", Html(await c.GetAsync("/Invite?token=" + Uri.EscapeDataString(late))));

        // O'chirilgan foydalanuvchi kira olmaydi.
        await f.WithScopeAsync(async sp =>
        {
            int staff = await f.EnsureStaffAsync();
            var u = await sp.GetRequiredService<ControlDbContext>().PortalUsers.FirstAsync(x => x.Id == user.Id);
            await sp.GetRequiredService<PortalAuthService>().UpdateUserAsync(u, u.Role, false, staff);
        });
        Assert.Contains(PortalAuthService.GenericLoginError, Html(await PostAsync(c, "/Login", new() { ["Login"] = "lk@mijoz.uz", ["Password"] = Password })));
        await f.WithScopeAsync(async sp =>
            Assert.True(await sp.GetRequiredService<ControlDbContext>().AuditLog.AnyAsync(a => a.Action == "portal.lockout" && a.PortalUserId == user.Id)));
    }
}

/// <summary>So'rov → xodim tasdiqlaydi → qoralama hisob-faktura → to'lov; rad etish; Viewer cheklovi; tenantlar izolyatsiyasi.</summary>
public class PortalRequestTests : IClassFixture<PanelFactory>
{
    private readonly PanelFactory f;
    public PortalRequestTests(PanelFactory factory) { f = factory; f.Telegram.Enabled = true; }

    [Fact]
    public async Task Upgrade_Request_StaffApproves_DraftInvoice_Pay_PortalSeesPaidAndPdf()
    {
        await SetPricesAsync(f);
        var tenant = await f.CreateTenantAsync("rq-a");
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            (await db.Tenants.FirstAsync(t => t.Id == tenant.Id)).TelegramChatId = "-200001";
            await db.SaveChangesAsync();
        });
        var biznes = await PaidAsync(f, tenant.Id, PlanCodes.Biznes, 1, new DateOnly(2026, 10, 18));
        var (user, token) = await InviteAsync(f, tenant.Id, "rq-a@mijoz.uz");
        await AcceptInviteAsync(f, token, linkChat: "6001");
        var (c, _) = await LoginAsync(f, "rq-a@mijoz.uz", "6001");

        string page = Html(await c.GetAsync("/Requests"));
        Assert.Contains("Korporativ", page);
        Assert.Contains("Tarifni oshirish", page);
        int korp = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Plans.Where(p => p.Code == PlanCodes.Korp).Select(p => p.Id).SingleAsync());
        var sent = await PostAsync(c, "/Requests", new() { ["PlanId"] = korp.ToString(), ["Period"] = "Month", ["PeriodCount"] = "2", ["Comment"] = "50 kishi bo'ldik" });
        Assert.Equal(HttpStatusCode.Redirect, sent.StatusCode);
        Assert.Contains("ko'rib chiqilayotgan", Html(await PostAsync(c, "/Requests", new() { ["PlanId"] = korp.ToString(), ["Period"] = "Month", ["PeriodCount"] = "1" })));

        var req = await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var r = await db.PortalRequests.SingleAsync(x => x.TenantId == tenant.Id);
            Assert.Equal(PortalRequestKind.Upgrade, r.Kind);
            Assert.Equal(PortalRequestStatus.Pending, r.Status);
            Assert.Equal(user.Id, r.PortalUserId);
            Assert.True(await db.Notifications.AnyAsync(n => n.Template == "portal-request" && n.DedupKey.StartsWith($"portal-req|{r.Id}|new")));
            Assert.True(await db.AuditLog.AnyAsync(a => a.Action == "portal.request.create" && a.PortalUserId == user.Id));
            return r;
        });

        // Xodim paneli (admin host): navbat → tasdiqlash → qoralama hisob-faktura sahifasiga.
        var (staff, _) = await StaffLoginAsync(f, "rq-sales@test.uz", StaffRole.Sales);
        string queue = Html(await staff.GetAsync("/Requests"));
        Assert.Contains("rq-a MChJ", queue);
        Assert.Contains("50 kishi bo'ldik", queue);
        Assert.Contains("class=\"sb-count\">1<", Html(await staff.GetAsync("/")));
        var approve = await PostAsync(staff, "/Requests?handler=Approve", new() { ["RequestId"] = req.Id.ToString() }, tokenFrom: "/Requests");
        Assert.Equal(HttpStatusCode.Redirect, approve.StatusCode);
        Assert.Contains("/Invoices/Details/", approve.Headers.Location!.OriginalString);

        var (inv, newSub) = await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var r = await db.PortalRequests.SingleAsync(x => x.Id == req.Id);
            Assert.Equal(PortalRequestStatus.Approved, r.Status);
            var i = await db.Invoices.Include(x => x.Lines).SingleAsync(x => x.Id == r.InvoiceId);
            var s = await db.Subscriptions.Include(x => x.Plan).SingleAsync(x => x.Id == r.SubscriptionId);
            Assert.Equal(InvoiceStatus.Draft, i.Status);
            Assert.Equal(2 * 3 * 100_000_000L, i.TotalTiyin); // KORP oyiga 3 000 000 so'm × 2
            Assert.Equal(PlanCodes.Korp, s.Plan!.Code);
            Assert.Equal(SubscriptionStatus.Pending, s.Status);
            Assert.Equal(new DateOnly(2026, 10, 18), s.StartDate);
            Assert.NotEqual(biznes.Id, s.Id);
            return (i, s);
        });
        Assert.Contains(f.Telegram.To("-200001"), m => m.Text.Contains("tasdiqlandi"));
        Assert.Contains(f.Telegram.To("6001"), m => m.Text.Contains("tasdiqlandi"));

        // Qoralama mijozga ko'rinmaydi.
        Assert.DoesNotContain("handler=Pdf&id=" + inv.Id, await (await c.GetAsync("/Invoices")).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/Invoices?handler=Pdf&id={inv.Id}")).StatusCode);
        Assert.Contains("hisob-faktura tayyorlanmoqda", Html(await c.GetAsync("/Requests")));

        // Xodim chiqaradi va to'lovni belgilaydi → KORP faol, BIZNES yopiladi.
        string number = await f.WithScopeAsync(async sp =>
        {
            int sid = await f.EnsureStaffAsync();
            var billing = sp.GetRequiredService<BillingService>();
            var i = await sp.GetRequiredService<ControlDbContext>().Invoices.Include(x => x.Lines).FirstAsync(x => x.Id == inv.Id);
            await billing.IssueAsync(i);
            await billing.RecordPaymentAsync(i.Id, i.TotalTiyin, new DateOnly(2026, 10, 18), PaymentMethod.Bank, "PT-77", null, sid);
            var db = sp.GetRequiredService<ControlDbContext>();
            Assert.Equal(SubscriptionStatus.Active, (await db.Subscriptions.FirstAsync(x => x.Id == newSub.Id)).Status);
            Assert.Equal(SubscriptionStatus.Expired, (await db.Subscriptions.FirstAsync(x => x.Id == biznes.Id)).Status);
            return i.Number!;
        });

        string list = Html(await c.GetAsync("/Invoices"));
        Assert.Contains(number, list);
        Assert.Contains("To'langan", list);
        var pdf = await c.GetAsync($"/Invoices?handler=Pdf&id={inv.Id}");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync())[..4]));
        Assert.Contains("Korporativ", Html(await c.GetAsync("/")));
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            Assert.True(await db.AuditLog.AnyAsync(a => a.Action == "portal.invoice.pdf" && a.PortalUserId == user.Id && a.EntityId == inv.Id.ToString()));
            Assert.True(await db.AuditLog.AnyAsync(a => a.Action == "portal.request.approve" && a.StaffUserId != null));
        });
    }

    [Fact]
    public async Task Reject_WithReason_Viewer_CannotRequest_OtherTenantInvoice_404_PaymentPage()
    {
        await SetPricesAsync(f);
        var tenant = await f.CreateTenantAsync("rq-r");
        var (owner, token) = await InviteAsync(f, tenant.Id, "rq-r@mijoz.uz");
        await AcceptInviteAsync(f, token, linkChat: "6101");
        var (c, _) = await LoginAsync(f, "rq-r@mijoz.uz", "6101");
        int biznes = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Plans.Where(p => p.Code == PlanCodes.Biznes).Select(p => p.Id).SingleAsync());

        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(c, "/Requests", new() { ["PlanId"] = biznes.ToString(), ["Period"] = "Year", ["PeriodCount"] = "1" })).StatusCode);
        var req = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().PortalRequests.SingleAsync(r => r.TenantId == tenant.Id));
        Assert.Equal(PortalRequestKind.New, req.Kind); // sinovdagi tenant

        var (staff, _) = await StaffLoginAsync(f, "rq-owner@test.uz", StaffRole.Owner);
        Assert.Contains("sababi majburiy", Html(await PostAsync(staff, "/Requests?handler=Reject", new() { ["RequestId"] = req.Id.ToString(), ["Reason"] = " " }, tokenFrom: "/Requests")));
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(staff, "/Requests?handler=Reject",
            new() { ["RequestId"] = req.Id.ToString(), ["Reason"] = "Yillik narx kelishilmagan" }, tokenFrom: "/Requests")).StatusCode);
        Assert.Contains(f.Telegram.To("6101"), m => m.Text.Contains("rad etildi") && m.Text.Contains("Yillik narx kelishilmagan"));
        string mine = Html(await c.GetAsync("/Requests"));
        Assert.Contains("Rad etilgan", mine);
        Assert.Contains("Yillik narx kelishilmagan", mine);

        // O'z so'rovini bekor qilish.
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(c, "/Requests", new() { ["PlanId"] = biznes.ToString(), ["Period"] = "Month", ["PeriodCount"] = "1" })).StatusCode);
        int second = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().PortalRequests.Where(r => r.TenantId == tenant.Id && r.Status == PortalRequestStatus.Pending).Select(r => r.Id).SingleAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(c, "/Requests?handler=Cancel", new() { ["RequestId"] = second.ToString() }, tokenFrom: "/Requests")).StatusCode);

        // Viewer: ko'radi, lekin so'rov yubora olmaydi.
        var (_, vt) = await InviteAsync(f, tenant.Id, "rq-v@mijoz.uz", PortalRole.Viewer);
        await AcceptInviteAsync(f, vt, linkChat: "6102");
        var (viewer, _) = await LoginAsync(f, "rq-v@mijoz.uz", "6102");
        Assert.Contains("faqat tashkilot mas'uli", Html(await viewer.GetAsync("/Requests")));
        var denied = await PostAsync(viewer, "/Requests", new() { ["PlanId"] = biznes.ToString(), ["Period"] = "Month", ["PeriodCount"] = "1" });
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Equal(0, await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().PortalRequests.CountAsync(r => r.TenantId == tenant.Id && r.Status == PortalRequestStatus.Pending)));

        // Boshqa tenant hisob-fakturasi — 404.
        var other = await f.CreateTenantAsync("rq-x");
        int otherInv = await f.WithScopeAsync(async sp =>
        {
            int sid = await f.EnsureStaffAsync();
            var billing = sp.GetRequiredService<BillingService>();
            var i = await billing.CreateDraftAsync(other.Id, null, 0, [new InvoiceLineInput("Xizmat", 1, 10_000_00, 12)], null, sid);
            await billing.IssueAsync(i);
            return i.Id;
        });
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/Invoices?handler=Pdf&id={otherInv}")).StatusCode);

        // To'lov rekvizitlari sahifasi (sozlanmagan rekvizitlar — ogohlantirish).
        Assert.Contains("Rekvizitlar hali kiritilmagan", Html(await c.GetAsync("/Payment")));
    }
}

/// <summary>Pasaytirish faqat joriy davr oxirida: kalit yangi davr boshlangandagina almashadi.</summary>
public class PortalDowngradeTests : IClassFixture<PanelFactory>
{
    private readonly PanelFactory f;
    public PortalDowngradeTests(PanelFactory factory) { f = factory; f.Telegram.Enabled = true; }

    [Fact]
    public async Task Downgrade_TakesEffectAtPeriodEnd_KeySwitchesWhenPeriodStarts()
    {
        await SetPricesAsync(f);
        var tenant = await f.CreateTenantAsync("rq-d");
        var reg = await f.ErpClient().RegisterAsync(await f.RegTokenAsync(tenant.Id), Guid.NewGuid().ToString().ToUpperInvariant(), 1, "1.0", null);
        Assert.True(reg.Ok, reg.Error);
        var korp = await PaidAsync(f, tenant.Id, PlanCodes.Korp, 1, new DateOnly(2026, 10, 18));
        Assert.Equal(new DateOnly(2026, 11, 17), korp.EndDate);
        var (user, _) = await InviteAsync(f, tenant.Id, "rq-d@mijoz.uz");

        var req = await f.WithScopeAsync(async sp =>
        {
            int start = await sp.GetRequiredService<ControlDbContext>().Plans.Where(p => p.Code == PlanCodes.Start).Select(p => p.Id).SingleAsync();
            return await sp.GetRequiredService<PortalRequestService>().CreateAsync(tenant.Id, user.Id, start, BillingPeriod.Month, 1, null);
        });
        Assert.Equal(PortalRequestKind.ChangeAtPeriodEnd, req.Kind);
        Assert.Equal(new DateOnly(2026, 11, 18), req.EffectiveDate);

        await f.WithScopeAsync(async sp =>
        {
            int sid = await f.EnsureStaffAsync();
            var inv = await sp.GetRequiredService<PortalRequestService>().ApproveAsync(req.Id, sid);
            var billing = sp.GetRequiredService<BillingService>();
            var i = await sp.GetRequiredService<ControlDbContext>().Invoices.Include(x => x.Lines).FirstAsync(x => x.Id == inv.Id);
            await billing.IssueAsync(i);
            var paid = await billing.RecordPaymentAsync(i.Id, i.TotalTiyin, new DateOnly(2026, 10, 18), PaymentMethod.Bank, "PT-D", null, sid);
            Assert.Null(paid.IssuedLicense); // yangi davr hali boshlanmagan — kalit o'zgarmaydi
        });

        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var subs = await db.Subscriptions.Include(s => s.Plan).Where(s => s.TenantId == tenant.Id && s.Status == SubscriptionStatus.Active).ToListAsync();
            var oldSub = Assert.Single(subs, s => s.Plan!.Code == PlanCodes.Korp);
            var next = Assert.Single(subs, s => s.Plan!.Code == PlanCodes.Start);
            Assert.Equal(new DateOnly(2026, 11, 17), oldSub.EndDate); // KORP oxirigacha ishlaydi
            Assert.Equal(new DateOnly(2026, 11, 18), next.StartDate);
            Assert.Equal(new DateOnly(2026, 12, 17), next.EndDate);
            var t = await db.Tenants.FirstAsync(x => x.Id == tenant.Id);
            Assert.Equal(PlanCodes.Korp, (await sp.GetRequiredService<LicenseIssuer>().GetCurrentAsync(t))!.Edition);
            var ov = await sp.GetRequiredService<PortalRequestService>().OverviewAsync(tenant.Id);
            Assert.Equal(PlanCodes.Korp, ov.Current!.Plan!.Code);
            Assert.Equal(PlanCodes.Start, ov.Upcoming!.Plan!.Code);
            // Keyingi davr to'langan — yangi so'rov hozircha qabul qilinmaydi.
            await Assert.ThrowsAsync<DomainException>(() => sp.GetRequiredService<PortalRequestService>()
                .CreateAsync(tenant.Id, user.Id, oldSub.PlanId, BillingPeriod.Month, 1, null));
        });

        // Yangi davr boshlangan kun: kunlik ish START kalitini chiqaradi.
        f.Time.SetUtcNow(new DateTimeOffset(2026, 11, 18, 5, 0, 0, TimeSpan.Zero));
        await f.WithScopeAsync(async sp =>
        {
            int n = await DailyJobHostedService.EnsureKeysForStartedPeriodsAsync(sp, NullLogger.Instance, CancellationToken.None);
            Assert.Equal(1, n);
            var db = sp.GetRequiredService<ControlDbContext>();
            var t = await db.Tenants.FirstAsync(x => x.Id == tenant.Id);
            var key = (await sp.GetRequiredService<LicenseIssuer>().GetCurrentAsync(t))!;
            Assert.Equal(PlanCodes.Start, key.Edition);
            Assert.Equal(5, key.MaxUsers);
            Assert.Equal(new DateOnly(2026, 12, 20), key.ExpiryDate);
            Assert.Equal(0, await DailyJobHostedService.EnsureKeysForStartedPeriodsAsync(sp, NullLogger.Instance, CancellationToken.None)); // idempotent
        });
        f.Time.SetUtcNow(PanelFactory.Start);
    }
}

/// <summary>On-prem mijoz: o'z o'rnatmasi kalitini ko'rish/yuklab olish (faqat Owner); SaaS'da sahifa yo'q.</summary>
public class PortalOnPremTests : IClassFixture<PanelFactory>
{
    private readonly PanelFactory f;
    public PortalOnPremTests(PanelFactory factory) { f = factory; f.Telegram.Enabled = true; }

    [Fact]
    public async Task OnPrem_Owner_RevealsAndDownloadsKey_Viewer_Cannot_SaaS_404()
    {
        var tenant = await f.CreateTenantAsync("op-a", deployment: DeploymentKind.OnPrem);
        string inst = Guid.NewGuid().ToString().ToUpperInvariant();
        var reg = await f.ErpClient().RegisterAsync(await f.RegTokenAsync(tenant.Id), inst, 1, "1.0", null);
        Assert.True(reg.Ok, reg.Error);
        string key = await f.WithScopeAsync(async sp => (await sp.GetRequiredService<ControlDbContext>().Licenses
            .Where(l => l.TenantId == tenant.Id && l.SupersededById == null).OrderByDescending(l => l.Id).FirstAsync()).LicenseKey);

        var (_, ot) = await InviteAsync(f, tenant.Id, "op@mijoz.uz");
        await AcceptInviteAsync(f, ot, linkChat: "9001");
        var (c, _) = await LoginAsync(f, "op@mijoz.uz", "9001");
        string page = Html(await c.GetAsync("/Licenses"));
        Assert.Contains(inst, page);
        Assert.Contains("Litsenziya kaliti", Html(await c.GetAsync("/")));
        Assert.DoesNotContain(key, page); // tugma bosilmaguncha ko'rsatilmaydi
        int licId = await f.WithScopeAsync(sp => sp.GetRequiredService<ControlDbContext>().Licenses
            .Where(l => l.TenantId == tenant.Id && l.SupersededById == null).Select(l => l.Id).FirstAsync());
        var reveal = await PostAsync(c, "/Licenses?handler=Reveal", new() { ["LicenseId"] = licId.ToString() }, tokenFrom: "/Licenses");
        Assert.Contains(key, Html(reveal));
        var file = await c.GetAsync($"/Licenses?handler=Download&id={licId}");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("text/plain", file.Content.Headers.ContentType!.MediaType);
        Assert.Equal(key, (await file.Content.ReadAsStringAsync()).Trim());

        var (_, vt) = await InviteAsync(f, tenant.Id, "op-v@mijoz.uz", PortalRole.Viewer);
        await AcceptInviteAsync(f, vt, linkChat: "9002");
        var (viewer, _) = await LoginAsync(f, "op-v@mijoz.uz", "9002");
        Assert.Contains("faqat tashkilot mas'uli", Html(await viewer.GetAsync("/Licenses")));
        Assert.Equal(HttpStatusCode.Redirect, (await viewer.GetAsync($"/Licenses?handler=Download&id={licId}")).StatusCode);

        var saas = await f.CreateTenantAsync("op-saas");
        var (_, st) = await InviteAsync(f, saas.Id, "saas@mijoz.uz");
        await AcceptInviteAsync(f, st, linkChat: "9003");
        var (sc, _) = await LoginAsync(f, "saas@mijoz.uz", "9003");
        Assert.Equal(HttpStatusCode.NotFound, (await sc.GetAsync("/Licenses")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await sc.GetAsync($"/Licenses?handler=Download&id={licId}")).StatusCode);
        Assert.DoesNotContain("Litsenziya kaliti", Html(await sc.GetAsync("/")));

        await f.WithScopeAsync(async sp =>
        {
            var actions = await sp.GetRequiredService<ControlDbContext>().AuditLog.Where(a => a.Entity == "License" && a.EntityId == licId.ToString()).Select(a => a.Action).ToListAsync();
            Assert.Contains("portal.license.view", actions);
            Assert.Contains("portal.license.download", actions);
        });
    }
}

/// <summary>Sof funksiyalar: telefon/email, so'rov turi, tokenlar.</summary>
public class PortalUnitTests
{
    [Theory]
    [InlineData("+998 90 123-45-67", "+998901234567")]
    [InlineData("901234567", "+998901234567")]
    [InlineData("998901234567", "+998901234567")]
    [InlineData("12345", null)]
    [InlineData("", null)]
    public void NormalizePhone(string input, string? expected) => Assert.Equal(expected, PortalIdentity.NormalizePhone(input));

    [Fact]
    public void Tokens_AreRandom_HashOnly_AndMasked()
    {
        string a = PortalTokens.New("PI-"), b = PortalTokens.New("PI-");
        Assert.NotEqual(a, b);
        Assert.Matches("^PI-[A-Z2-7]{32}$", a);
        Assert.Equal(64, PortalTokens.Hash(a).Length);
        Assert.Matches("^[0-9]{6}$", PortalTokens.NewOtpCode());
        Assert.NotEqual(PortalTokens.HashOtp("s1", "123456"), PortalTokens.HashOtp("s2", "123456"));
        Assert.Equal("*******567", PortalIdentity.Mask("1234567567"));
        Assert.Equal("https://my.x.uz/Invite?token=PI-AB", PortalAuthService.InviteUrl("https://my.x.uz/", "PI-AB"));
    }

    [Fact]
    public void Classify_RenewUpgradeDowngradeNew()
    {
        var start = new Plan { Id = 1, Code = PlanCodes.Start, SortOrder = 1 };
        var biznes = new Plan { Id = 2, Code = PlanCodes.Biznes, SortOrder = 2 };
        var korp = new Plan { Id = 3, Code = PlanCodes.Korp, SortOrder = 3 };
        var trial = new Plan { Id = 4, Code = PlanCodes.Trial, SortOrder = 4 };
        var today = new DateOnly(2026, 10, 20);
        var cur = new Subscription { PlanId = 2, Plan = biznes, Period = BillingPeriod.Month, StartDate = new(2026, 10, 1), EndDate = new(2026, 10, 31) };

        Assert.Equal((PortalRequestKind.Renew, new DateOnly(2026, 11, 1)), PortalRequestService.Classify(cur, biznes, BillingPeriod.Month, today));
        Assert.Equal((PortalRequestKind.Upgrade, today), PortalRequestService.Classify(cur, korp, BillingPeriod.Month, today));
        Assert.Equal((PortalRequestKind.ChangeAtPeriodEnd, new DateOnly(2026, 11, 1)), PortalRequestService.Classify(cur, start, BillingPeriod.Month, today));
        Assert.Equal((PortalRequestKind.ChangeAtPeriodEnd, new DateOnly(2026, 11, 1)), PortalRequestService.Classify(cur, biznes, BillingPeriod.Year, today));
        Assert.Equal((PortalRequestKind.New, today), PortalRequestService.Classify(null, start, BillingPeriod.Month, today));
        var tr = new Subscription { PlanId = 4, Plan = trial, Period = BillingPeriod.Trial, EndDate = new(2026, 10, 31) };
        Assert.Equal((PortalRequestKind.New, today), PortalRequestService.Classify(tr, start, BillingPeriod.Month, today));
        // Grace (muddat o'tgan, lekin hali faol) — uzaytirish bugundan.
        var grace = new Subscription { PlanId = 2, Plan = biznes, Period = BillingPeriod.Month, EndDate = new(2026, 10, 18) };
        Assert.Equal((PortalRequestKind.Renew, today), PortalRequestService.Classify(grace, biznes, BillingPeriod.Month, today));
    }
}
