using System.Net;
using System.Text;
using tahsinERP.Bot.Erp;
using tahsinERP.Bot.MiniApp;
using tahsinERP.Bot.Pipeline;
using tahsinERP.Bot.Security;
using tahsinERP.Integration;

namespace tahsinERP.Bot.Tests;

public class SecurityTests
{
    private const string Master = "0123456789abcdef0123456789abcdef-bot";
    private const string Token = "123456:ABC-test-token";

    [Fact]
    public void InitData_ValidRoundTrip_AndRejectsTamperedExpiredOrForeign()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1792400000);
        var fields = new Dictionary<string, string>
        {
            ["auth_date"] = now.ToUnixTimeSeconds().ToString(),
            ["query_id"] = "AAH",
            ["user"] = "{\"id\":777,\"first_name\":\"Ali Vali\",\"language_code\":\"ru\"}"
        };
        var data = WebAppInitData.Build(fields, Token);

        var ok = WebAppInitData.Validate(data, Token, now.AddMinutes(10));
        Assert.NotNull(ok);
        Assert.Equal(777, ok!.UserId);
        Assert.Equal("Ali Vali", ok.FirstName);
        Assert.Equal("ru", ok.LanguageCode);

        Assert.Null(WebAppInitData.Validate(data, "999:other", now));
        Assert.Null(WebAppInitData.Validate(data.Replace("777", "778"), Token, now));
        Assert.Null(WebAppInitData.Validate(data, Token, now.AddHours(25)));
        Assert.Null(WebAppInitData.Validate(data + "x", Token, now));
        Assert.Null(WebAppInitData.Validate("", Token, now));
    }

    [Fact]
    public void SecretBox_RoundTrip_AndKeyBound()
    {
        var box = new SecretBox(Master);
        var enc = box.Protect(Token);
        Assert.NotEqual(Token, enc);
        Assert.NotEqual(enc, box.Protect(Token));   // har safar yangi nonce
        Assert.Equal(Token, box.Unprotect(enc));
        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() => new SecretBox(Master + "x").Unprotect(enc));
        Assert.Throws<InvalidOperationException>(() => new SecretBox("short"));
    }

    [Fact]
    public void WebhookSecret_FixedTimeCompare()
    {
        Assert.True(WebhookEndpoint.SecretMatches("abc", "abc"));
        Assert.False(WebhookEndpoint.SecretMatches("abd", "abc"));
        Assert.False(WebhookEndpoint.SecretMatches("", ""));
        Assert.False(WebhookEndpoint.SecretMatches(null!, "abc"));
        Assert.Matches("^[A-Za-z0-9_-]{40,}$", Cli.NewSecret());
    }

    [Fact]
    public async Task ErpClient_SignsRequest_VerifiableByErp()
    {
        var handler = new FakeHandler(_ => FakeHandler.Json(HttpStatusCode.OK, "{\"id\":5,\"docNo\":\"BUY-1\",\"total\":100}"));
        var clock = new FixedClock(DateTimeOffset.FromUnixTimeSeconds(1792400000));
        var key = DstApiSignature.TenantKey(Master, "firma1");
        var client = new ErpClient(new HttpClient(handler), new Uri("https://erp.example.uz/firma1"), key, clock);

        var r = await client.PostAsync<ErpOrderResult>("Order", new { customerId = 9 }, 12, CancellationToken.None);
        Assert.Equal("BUY-1", r!.DocNo);

        var req = handler.Requests.Single();
        Assert.Equal("/firma1/Distribution/DstApi/Order", req.Uri.PathAndQuery);
        Assert.Equal("XMLHttpRequest", req.Headers["X-Requested-With"]);
        Assert.Equal("12", req.Headers[DstApiSignature.ActingUserHeader]);
        Assert.True(DstApiSignature.Verify(key, req.Headers[DstApiSignature.TimestampHeader], req.Headers[DstApiSignature.SignatureHeader],
            "POST", req.Uri.PathAndQuery, "12", req.Body, clock.GetUtcNow()));
        Assert.Contains("\"customerId\":9", Encoding.UTF8.GetString(req.Body));
    }

    [Fact]
    public async Task ErpClient_QueryEscaping_AndSystemCallWithoutActingUser()
    {
        var handler = new FakeHandler(_ => FakeHandler.Json(HttpStatusCode.OK, "[]"));
        var clock = new FixedClock(DateTimeOffset.FromUnixTimeSeconds(1792400000));
        var key = DstApiSignature.TenantKey(Master, "default");
        var client = new ErpClient(new HttpClient(handler), new Uri("https://erp.example.uz/"), key, clock);

        await client.GetAsync<List<ErpPending>>("Catalog", 3, CancellationToken.None, ("q", "Сок & 1"), ("customerId", 5), ("categoryId", null));
        await client.GetAsync<List<ErpPending>>("Notifications", null, CancellationToken.None, ("max", 100));

        var catalog = handler.Requests[0];
        Assert.StartsWith("/Distribution/DstApi/Catalog?q=", catalog.Uri.PathAndQuery);
        Assert.Contains("&customerId=5", catalog.Uri.PathAndQuery);
        Assert.DoesNotContain("categoryId", catalog.Uri.PathAndQuery);
        Assert.Contains("%26", catalog.Uri.PathAndQuery);
        Assert.True(DstApiSignature.Verify(key, catalog.Headers[DstApiSignature.TimestampHeader], catalog.Headers[DstApiSignature.SignatureHeader],
            "GET", catalog.Uri.PathAndQuery, "3", [], clock.GetUtcNow()));

        var system = handler.Requests[1];
        Assert.False(system.Headers.ContainsKey(DstApiSignature.ActingUserHeader));
        Assert.True(DstApiSignature.Verify(key, system.Headers[DstApiSignature.TimestampHeader], system.Headers[DstApiSignature.SignatureHeader],
            "GET", "/Distribution/DstApi/Notifications?max=100", null, [], clock.GetUtcNow()));
    }

    [Fact]
    public async Task ErpClient_BusinessErrors()
    {
        var handler = new FakeHandler(req => req.RequestUri!.AbsolutePath.EndsWith("Order")
            ? FakeHandler.Json((HttpStatusCode)409, "{\"error\":\"Kredit limiti\",\"credit\":true,\"problems\":[\"Limit: 100\",\"Muddati o'tgan: 50\"]}")
            : FakeHandler.Json(HttpStatusCode.BadRequest, "{\"error\":\"Sababni tanlang\"}"));
        var client = new ErpClient(new HttpClient(handler), new Uri("https://erp/"), DstApiSignature.TenantKey(Master, "x"), TimeProvider.System);

        var credit = await Assert.ThrowsAsync<ErpException>(() => client.PostAsync<object>("Order", new { }, 1, CancellationToken.None));
        Assert.True(credit.IsBusiness);
        Assert.Equal(409, credit.Status);
        Assert.Equal(2, credit.Problems.Count);

        var bad = await Assert.ThrowsAsync<ErpException>(() => client.PostAsync<object>("VisitResult", new { }, 1, CancellationToken.None));
        Assert.Equal("Sababni tanlang", bad.Message);

        var html = new ErpClient(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Found)
            { Headers = { Location = new Uri("/Account/Login", UriKind.Relative) } })), new Uri("https://erp/"), DstApiSignature.TenantKey(Master, "x"), TimeProvider.System);
        Assert.Equal(502, (await Assert.ThrowsAsync<ErpException>(() => html.GetAsync<object>("Me", 1, CancellationToken.None))).Status);
        var page = new ErpClient(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("<html>", System.Text.Encoding.UTF8, "text/html") })), new Uri("https://erp/"), DstApiSignature.TenantKey(Master, "x"), TimeProvider.System);
        Assert.False((await Assert.ThrowsAsync<ErpException>(() => page.GetAsync<object>("Me", 1, CancellationToken.None))).IsBusiness);

        var down = new ErpClient(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))),
            new Uri("https://erp/"), DstApiSignature.TenantKey(Master, "x"), TimeProvider.System);
        var ex = await Assert.ThrowsAsync<ErpException>(() => down.GetAsync<object>("Me", 1, CancellationToken.None));
        Assert.False(ex.IsBusiness);
        Assert.False(ex.IsLicense);

        // DstApiAuth litsenziyasiz: 403 "License" + X-License-Message, JSON emas
        var blocked = new ErpClient(new HttpClient(new FakeHandler(_ =>
            {
                var r = new HttpResponseMessage(HttpStatusCode.Forbidden) { ReasonPhrase = "License", Content = new StringContent("", System.Text.Encoding.UTF8, "text/html") };
                r.Headers.Add("X-License-Message", "Litsenziya%20tugagan");
                return r;
            })), new Uri("https://erp/"), DstApiSignature.TenantKey(Master, "x"), TimeProvider.System);
        var lic = await Assert.ThrowsAsync<ErpException>(() => blocked.GetAsync<object>("Me", 1, CancellationToken.None));
        Assert.True(lic.IsLicense);
        Assert.False(lic.IsBusiness);
        var forbidden = new ErpClient(new HttpClient(new FakeHandler(_ => FakeHandler.Json(HttpStatusCode.Forbidden, "{\"error\":\"Ruxsat yo'q\"}"))),
            new Uri("https://erp/"), DstApiSignature.TenantKey(Master, "x"), TimeProvider.System);
        Assert.False((await Assert.ThrowsAsync<ErpException>(() => forbidden.GetAsync<object>("Me", 1, CancellationToken.None))).IsLicense);
    }

    [Fact]
    public async Task ErpClientFactory_UsesErpTenantKey_DefaultWhenEmpty()
    {
        var handler = new FakeHandler(_ => FakeHandler.Json(HttpStatusCode.OK, "null"));
        var clock = new FixedClock(DateTimeOffset.FromUnixTimeSeconds(1792400000));
        var factory = new ErpClientFactory(new FakeHttpClientFactory(handler),
            Microsoft.Extensions.Options.Options.Create(new BotOptions { MasterKey = Master }), clock);

        await factory.For(new Data.BotTenant { Slug = "firma1", ErpBaseUrl = "https://erp/" }).GetAsync<object>("Me", 1, CancellationToken.None);
        await factory.For(new Data.BotTenant { Slug = "firma1", ErpBaseUrl = "https://erp/", ErpTenant = "acme" }).GetAsync<object>("Me", 1, CancellationToken.None);

        bool Ok(FakeHandler.Captured r, string tenant) => DstApiSignature.Verify(DstApiSignature.TenantKey(Master, tenant),
            r.Headers[DstApiSignature.TimestampHeader], r.Headers[DstApiSignature.SignatureHeader], "GET", r.Uri.PathAndQuery, "1", [], clock.GetUtcNow());
        Assert.True(Ok(handler.Requests[0], "default"));
        Assert.False(Ok(handler.Requests[0], "firma1"));
        Assert.True(Ok(handler.Requests[1], "acme"));
    }

    [Fact]
    public void SignaturePng_OnlyRealPng()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };
        Assert.Equal(png, MiniAppEndpoints.DecodePngDataUrl("data:image/png;base64," + Convert.ToBase64String(png)));
        Assert.Null(MiniAppEndpoints.DecodePngDataUrl("data:image/png;base64," + Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8, 9])));
        Assert.Null(MiniAppEndpoints.DecodePngDataUrl("data:image/jpeg;base64,AAAA"));
        Assert.Null(MiniAppEndpoints.DecodePngDataUrl("data:image/png;base64,@@@"));
    }
}
