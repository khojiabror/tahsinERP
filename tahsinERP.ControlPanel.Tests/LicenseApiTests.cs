using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.ControlPanel.Data;
using tahsinERP.Services.LicenseSync;
using ErpLicense = tahsinERP.Licensing.LicenseHelper;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>
/// License API ↔ ERP: so'rovlarni ERP'ning HAQIQIY LicensePanelClient'i yuboradi (fayl ulangan), panel — haqiqiy Program.
/// Kontrakt: Docs/ControlPanel/01_LICENSE_API.md.
/// </summary>
public class LicenseApiTests(PanelFactory f) : IClassFixture<PanelFactory>
{
    private static string NewInst() => Guid.NewGuid().ToString("D").ToUpperInvariant();

    [Fact]
    public async Task Register_Trial_ReturnsSecretAndErpAcceptableKey()
    {
        var t = await f.CreateTenantAsync("reg-trial");
        string token = await f.RegTokenAsync(t.Id);
        string inst = NewInst();

        var r = await f.ErpClient().RegisterAsync(token, inst, 1, "1.0.0.0", "reg-trial.tahsinerp.uz");
        Assert.True(r.Ok, r.Error);
        Assert.True(LicensePanelClient.IsValidSecret(r.TenantSecret));
        Assert.Equal(6, r.PollHours);
        Assert.NotNull(r.LicenseKey);

        // ERP LicenseService tekshiruvi: imzo (k2), o'rnatma, CompanyID, Edition = TRIAL, 14 kun.
        Assert.True(ErpLicense.TryParseKey(r.LicenseKey, f.ErpTrusted, out var info));
        Assert.True(info.IsBoundTo(inst));
        Assert.Equal(1, info.CompanyID);
        Assert.Equal("TRIAL", info.Edition);
        Assert.Equal(5, info.MaxUsers);
        Assert.Equal(new DateTime(2026, 10, 31), info.ExpiryDate);

        // Idempotent: javob yo'qolgan — o'sha token + o'sha o'rnatma → o'sha sir.
        var again = await f.ErpClient().RegisterAsync(token, inst, 1, "1.0.0.0", null);
        Assert.True(again.Ok, again.Error);
        Assert.Equal(r.TenantSecret, again.TenantSecret);
        Assert.Equal(r.LicenseKey, again.LicenseKey);

        // Boshqa o'rnatma bilan — 409 token_used.
        var other = await f.ErpClient().RegisterAsync(token, NewInst(), 1, "1.0.0.0", null);
        Assert.Equal(409, other.HttpStatus);
        Assert.Equal("token_used", other.Error);

        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var tenant = await db.Tenants.FirstAsync(x => x.Id == t.Id);
            Assert.Equal(inst, tenant.InstallationId);
            Assert.NotEqual(token, tenant.RegTokenHash); // faqat xesh saqlanadi
            Assert.DoesNotContain(r.TenantSecret!, tenant.ApiSecretProtected!); // sir shifrlangan
            Assert.True(await db.AuditLog.AnyAsync(a => a.Action == "tenant.register" && a.EntityId == t.Id.ToString()));
            Assert.Equal(1, await db.Licenses.CountAsync(l => l.TenantId == t.Id)); // qayta register yangi kalit chiqarmadi
        });
    }

    [Fact]
    public async Task Register_Errors()
    {
        var bad = await f.ErpClient().RegisterAsync("RT-YOQ", NewInst(), 1, "1.0", null);
        Assert.Equal(404, bad.HttpStatus);
        Assert.Equal("token_not_found", bad.Error);

        var t = await f.CreateTenantAsync("reg-errors");
        string token = await f.RegTokenAsync(t.Id);
        var notGuid = await f.ErpClient().RegisterAsync(token, "not-a-guid", 1, "1.0", null);
        Assert.Equal(400, notGuid.HttpStatus);
        Assert.Equal("bad_request", notGuid.Error);

        using var http = f.Https();
        var empty = await http.PostAsync("/api/license/register", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        // Muddati o'tgan token (7 kun) — 404.
        f.Time.Advance(TimeSpan.FromDays(8));
        try
        {
            var expired = await f.ErpClient().RegisterAsync(token, NewInst(), 1, "1.0", null);
            Assert.Equal("token_not_found", expired.Error);
        }
        finally { f.Time.SetUtcNow(PanelFactory.Start); }
    }

    [Fact]
    public async Task Register_AdoptsErpCompanyId()
    {
        var t = await f.CreateTenantAsync("reg-company");
        var r = await f.ErpClient().RegisterAsync(await f.RegTokenAsync(t.Id), NewInst(), 42, "1.0", null);
        Assert.True(r.Ok, r.Error);
        Assert.True(ErpLicense.TryParseKey(r.LicenseKey, f.ErpTrusted, out var info));
        Assert.Equal(42, info.CompanyID);
    }

    [Fact]
    public async Task Pull_Signed_ETag_304_And_Auth_Errors()
    {
        var t = await f.CreateTenantAsync("pull-flow");
        string inst = NewInst();
        var reg = await f.ErpClient().RegisterAsync(await f.RegTokenAsync(t.Id), inst, 1, "1.0", null);
        Assert.True(reg.Ok, reg.Error);
        var client = f.ErpClient();

        var p1 = await client.PullAsync(inst, reg.TenantSecret, null);
        Assert.Equal(LicensePanelClient.PullStatus.Updated, p1.Status);
        Assert.Equal(reg.LicenseKey, p1.LicenseKey);
        Assert.Equal(LicensePanelClient.ComputeETag(p1.LicenseKey), p1.ETag);
        Assert.Equal(6, p1.PollHours);

        var p2 = await client.PullAsync(inst, reg.TenantSecret, p1.LicenseKey);
        Assert.Equal(LicensePanelClient.PullStatus.NotModified, p2.Status);
        Assert.Equal(304, p2.HttpStatus);

        var wrong = await client.PullAsync(inst, Convert.ToBase64String(new byte[32]), null);
        Assert.Equal(LicensePanelClient.PullStatus.Unauthorized, wrong.Status);
        Assert.Equal("bad_signature", wrong.Error);

        var skewed = await f.ErpClient(() => f.Time.GetUtcNow().UtcDateTime.AddMinutes(6)).PullAsync(inst, reg.TenantSecret, null);
        Assert.Equal(LicensePanelClient.PullStatus.Unauthorized, skewed.Status);
        Assert.Equal("clock_skew", skewed.Error);

        var unknown = await client.PullAsync(NewInst(), reg.TenantSecret, null);
        Assert.Equal(LicensePanelClient.PullStatus.NotFound, unknown.Status);
        Assert.Equal("unknown_installation", unknown.Error);
    }

    [Fact]
    public async Task Pull_ReplayedNonce_Rejected()
    {
        var t = await f.CreateTenantAsync("pull-replay");
        string inst = NewInst();
        var reg = await f.ErpClient().RegisterAsync(await f.RegTokenAsync(t.Id), inst, 1, "1.0", null);
        string path = "/api/license/" + inst;
        string ts = f.Time.GetUtcNow().ToUnixTimeSeconds().ToString();
        string nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        string sig = LicensePanelClient.ComputeSignature(reg.TenantSecret, "GET", path, ts, nonce, null);

        using var http = f.Https();
        HttpRequestMessage Req()
        {
            var m = new HttpRequestMessage(HttpMethod.Get, path);
            m.Headers.Add("X-Tahsin-Timestamp", ts);
            m.Headers.Add("X-Tahsin-Nonce", nonce);
            m.Headers.Add("X-Tahsin-Signature", sig);
            return m;
        }
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Req())).StatusCode);
        var replay = await http.SendAsync(Req());
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("replay", (await replay.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["error"]);
    }

    [Fact]
    public async Task Revoke_WithoutReplacement_Gives410_WithReplacement_DeliversShortKey()
    {
        int staff = await f.EnsureStaffAsync();
        var t = await f.CreateTenantAsync("pull-revoke");
        string inst = NewInst();
        var reg = await f.ErpClient().RegisterAsync(await f.RegTokenAsync(t.Id), inst, 1, "1.0", null);

        // Qisqa almashtiruvchi kalit bilan bekor qilish → keyingi pull'da yangi kalit.
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var lic = await db.Licenses.SingleAsync(l => l.TenantId == t.Id);
            await sp.GetRequiredService<Licensing.LicenseIssuer>().RevokeAsync(lic, "test: shartnoma bekor", staff, new DateOnly(2026, 10, 25));
        });
        var p = await f.ErpClient().PullAsync(inst, reg.TenantSecret, reg.LicenseKey);
        Assert.Equal(LicensePanelClient.PullStatus.Updated, p.Status);
        Assert.True(ErpLicense.TryParseKey(p.LicenseKey, f.ErpTrusted, out var info));
        Assert.Equal(new DateTime(2026, 10, 25), info.ExpiryDate);

        // Almashtiruvchisiz bekor qilish → 410 revoked.
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            var lic = await db.Licenses.SingleAsync(l => l.TenantId == t.Id && l.SupersededById == null && l.RevokedAtUtc == null);
            await sp.GetRequiredService<Licensing.LicenseIssuer>().RevokeAsync(lic, "test: to'liq bekor", staff, null);
            Assert.Equal(2, await db.Licenses.CountAsync(l => l.TenantId == t.Id)); // tarix o'chmaydi
            Assert.Equal(2, await db.AuditLog.CountAsync(a => a.Action == "license.revoke" && a.StaffUserId == staff));
        });
        var gone = await f.ErpClient().PullAsync(inst, reg.TenantSecret, p.LicenseKey);
        Assert.Equal(LicensePanelClient.PullStatus.Revoked, gone.Status);
        Assert.Equal("revoked", gone.Error);
    }
}
