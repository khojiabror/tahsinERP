using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using tahsinERP.Bot.Data;
using tahsinERP.Bot.Erp;
using tahsinERP.Bot.Files;
using tahsinERP.Bot.Handlers;
using tahsinERP.Bot.Pipeline;
using tahsinERP.Bot.Security;
using tahsinERP.Bot.Telegram;

namespace tahsinERP.Bot.Tests;

public class HelperTests
{
    [Theory]
    [InlineData("250000", 250000)]
    [InlineData("250 000", 250000)]
    [InlineData("250,000", 250000)]
    [InlineData("250 000 so'm", 250000)]
    [InlineData("1,250,000.50", 1250000.50)]
    [InlineData("99,5", 99.5)]
    public void Amounts_Parse(string text, double expected) => Assert.Equal((decimal)expected, Amounts.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("0")]
    public void Amounts_Rejects(string text) => Assert.Null(Amounts.Parse(text));

    [Fact]
    public void Callback_RoundTrip()
    {
        var data = Callback.Make("pay", 15, null);
        Assert.Equal("pay:15:0", data);
        var cb = Callback.Parse(data);
        Assert.Equal("pay", cb.Command);
        Assert.Equal(15, cb.Int(0));
        Assert.Null(cb.Int(1));
        Assert.Null(cb.Int(5));
        Assert.Equal("ho!", Callback.Parse("ho!").Command);
        Assert.True(Callback.Make("nr", int.MaxValue, int.MaxValue).Length <= 64);
    }

    [Fact]
    public void Texts_BothLanguages_AndButtons()
    {
        foreach (var k in Texts.Keys)
        {
            Assert.False(string.IsNullOrWhiteSpace(Texts.Get(Texts.Uz, k)), k);
            Assert.False(string.IsNullOrWhiteSpace(Texts.Get(Texts.Ru, k)), k);
        }
        Assert.Equal("Btn_Route", Texts.KeyOf("🗺 Маршрут", "Btn_DayStart", "Btn_Route"));
        Assert.Equal("Btn_Route", Texts.KeyOf("🗺 Marshrut", "Btn_DayStart", "Btn_Route"));
        Assert.Null(Texts.KeyOf("salom", "Btn_Route"));
        Assert.All(Texts.PayMethods, m => Assert.NotEqual("PayMethod_" + m, Texts.Get(Texts.Uz, "PayMethod_" + m)));
        Assert.Equal("1 250 000", Texts.Money(1250000.4m));
    }

    [Theory]
    [InlineData("90 123 45 67", "+998901234567")]
    [InlineData("+998 (90) 123-45-67", "+998901234567")]
    [InlineData("998901234567", "+998901234567")]
    [InlineData("12345", null)]
    [InlineData("telefon 901234567", null)]
    [InlineData("", null)]
    public void Phones_Normalize(string text, string? expected) => Assert.Equal(expected, Phones.Normalize(text));

    [Fact]
    public void StatementPeriod_Months()
    {
        var today = new DateOnly(2026, 3, 1);
        Assert.Equal((new DateOnly(2026, 3, 1), today), UpdateRouter.StatementPeriod("m0", today));
        Assert.Equal((new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)), UpdateRouter.StatementPeriod("m1", today));
        Assert.Equal((new DateOnly(2025, 12, 2), today), UpdateRouter.StatementPeriod("q", today));
        Assert.Null(UpdateRouter.StatementPeriod("zz", today));
    }

    [Fact]
    public void StatementText_FitsTelegramLimit_AndCountsHiddenRows()
    {
        var rows = Enumerable.Range(1, 300).Select(i => new ErpStatementRow("01.09.2026", "Jo'natish", "BUY-" + i, 1000 * i, 0, 1000 * i)).ToList();
        var s = new ErpStatement("Baraka", "01.09.2026", "30.09.2026", 0, 1, 0, 1, rows);
        var text = UpdateRouter.StatementText(Texts.Uz, s);
        Assert.True(text.Length <= 3800, text.Length.ToString());
        Assert.Contains("BUY-1:", text);
        Assert.Matches(@"yana \d+ ta yozuv", text);
        Assert.Contains("Yakuniy qoldiq", text);
        Assert.Contains("harakat yo'q", UpdateRouter.StatementText(Texts.Uz, s with { Rows = [] }));
    }

    [Fact]
    public void LiveThrottle_PerUserInterval()
    {
        var clock = new FixedClock(DateTimeOffset.UnixEpoch.AddDays(1));
        var t = new LiveThrottle(clock);
        Assert.True(t.Allow(1, 5));
        Assert.False(t.Allow(1, 5));
        Assert.True(t.Allow(1, 6));
        Assert.True(t.Allow(2, 5));
        clock.Now = clock.Now.Add(LiveThrottle.Interval);
        Assert.True(t.Allow(1, 5));
    }

    [Fact]
    public void MiniAppUrl_AndStopLabel()
    {
        Assert.Equal("https://bot.uz/app/?t=firma1&c=5&v=9", UpdateRouter.MiniAppUrl("https://bot.uz/", "firma1", 5, 9));
        Assert.Equal("https://bot.uz/app/?t=firma1&c=5", UpdateRouter.MiniAppUrl("https://bot.uz", "firma1", 5, null));
        Assert.Equal("https://bot.uz/app/store.html?t=firma%201", UpdateRouter.StoreAppUrl("https://bot.uz/", "firma 1"));
        var stop = new ErpRouteStop(1, "Do'kon", null, null, null, null, null, true, true, "ORDER", 150000, 1);
        Assert.Equal("✅ Do'kon · 150 000❗", UpdateRouter.StopLabel(stop));
        Assert.StartsWith("⏳", UpdateRouter.StopLabel(stop with { Visited = false }));
        Assert.True(UpdateRouter.StopLabel(stop with { Name = new string('x', 100) }).Length <= 60);
    }
}

/// <summary>UpdateRouter oqimlari: in-memory bot bazasi + soxta ERP (imzo tekshiriladi).</summary>
public partial class HandlerTests : IDisposable
{
    private const string Master = "0123456789abcdef0123456789abcdef-bot";
    private readonly ServiceProvider _sp;
    private readonly FakeHandler _erp;
    private readonly TenantRegistry.Tenant _tenant;
    private readonly Dictionary<string, Func<FakeHandler.Captured, (HttpStatusCode, string)>> _routes = new();
    private readonly FakeVoice _voice = new();
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 19, 6, 0, 0, TimeSpan.Zero));

    public HandlerTests()
    {
        _erp = new FakeHandler(req =>
        {
            var action = req.RequestUri!.AbsolutePath.Split('/').Last();
            var captured = _erpLast();
            if (!_routes.TryGetValue(action, out var f)) return FakeHandler.Json(HttpStatusCode.NotFound, "{\"error\":\"no route " + action + "\"}");
            var (status, json) = f(captured);
            return FakeHandler.Json(status, json);
        });
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<BotDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton(Options.Create(new BotOptions { MasterKey = Master, PublicBaseUrl = "https://bot.example.uz" }));
        services.AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(_erp));
        services.AddSingleton<ErpClientFactory>();
        services.AddSingleton<OutboxSignal>();
        // getFile -> fayl yo'li, /file/bot... -> JPEG baytlari (selfi va foto oqimlari uchun)
        services.AddSingleton(new TelegramApi(new HttpClient(new FakeHandler(req =>
            req.RequestUri!.AbsolutePath.EndsWith("/getFile")
                ? FakeHandler.Json(HttpStatusCode.OK, "{\"ok\":true,\"result\":{\"file_id\":\"f1\",\"file_path\":\"photos/a.jpg\",\"file_size\":4}}")
                : req.RequestUri.AbsolutePath.Contains("/file/bot")
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]) }
                    : FakeHandler.Json(HttpStatusCode.OK, "{\"ok\":true}")))));
        services.AddSingleton<LiveThrottle>();
        services.AddSingleton<tahsinERP.Bot.Voice.IVoiceRecognizer>(_voice);
        services.AddSingleton<IFileStore>(sp => new LocalFileStore(Path.GetTempPath(), sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddScoped<Outbox>();
        services.AddScoped<SessionStore>();
        services.AddScoped<UpdateRouter>();
        _sp = services.BuildServiceProvider();

        var row = new BotTenant { Id = 1, Slug = "firma1", ErpBaseUrl = "https://erp.example.uz/", TokenEnc = "x", WebhookSecret = "s", IsActive = true };
        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
            db.Tenants.Add(row);
            db.SaveChanges();
        }
        _tenant = new TenantRegistry.Tenant(1, "firma1", row.ErpBaseUrl, "123:tok", "s", "firma1_bot", row);
    }

    private FakeHandler.Captured _erpLast() => _erp.Requests.Last();

    public void Dispose() => _sp.Dispose();

    private const long Tg = 5550001;

    private static TgUpdate Text(long updateId, string text) =>
        new(updateId, new TgMessage(updateId, new TgUser(Tg, false, "Ali", null, null, "uz"), new TgChat(Tg, "private"), 0, text, null, null, null, null),
            null, null);

    private static TgUpdate Location(long updateId, double lat, double lng) =>
        new(updateId, new TgMessage(updateId, new TgUser(Tg, false, "Ali", null, null, "uz"), new TgChat(Tg, "private"), 0, null, null,
            new TgLocation(lat, lng, 15, null), null, null), null, null);

    private static TgUpdate LiveLocation(long updateId, double lat, double lng, bool edited = false)
    {
        var msg = new TgMessage(updateId, new TgUser(Tg, false, "Ali", null, null, "uz"), new TgChat(Tg, "private"), 0, null, null,
            new TgLocation(lat, lng, 12, 28800), null, null);
        return edited ? new TgUpdate(updateId, null, msg, null) : new TgUpdate(updateId, msg, null, null);
    }

    private static TgUpdate Photo(long updateId) =>
        new(updateId, new TgMessage(updateId, new TgUser(Tg, false, "Ali", null, null, "uz"), new TgChat(Tg, "private"), 0, null, null, null,
            [new TgPhotoSize("small", null, 90, 90, 100), new TgPhotoSize("big", null, 1280, 960, 4)], null), null, null);

    private static TgUpdate Contact(long updateId, string phone) =>
        new(updateId, new TgMessage(updateId, new TgUser(Tg, false, "Ali", null, null, "uz"), new TgChat(Tg, "private"), 0, null, null, null, null, null,
            new TgContact(phone, "Do'kon", null)), null, null);

    private static TgUpdate Click(long updateId, string data) =>
        new(updateId, null, null, new TgCallbackQuery("cb" + updateId, new TgUser(Tg, false, "Ali", null, null, "uz"),
            new TgMessage(99, null, new TgChat(Tg, "private"), 0, "x", null, null, null, null), data));

    private async Task Handle(TgUpdate u)
    {
        using var scope = _sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<UpdateRouter>().HandleAsync(_tenant, u, CancellationToken.None);
    }

    private List<BotOutboxItem> Out()
    {
        using var scope = _sp.CreateScope();
        return scope.ServiceProvider.GetRequiredService<BotDbContext>().Outbox.AsNoTracking().OrderBy(o => o.Id).ToList();
    }

    private static string TextOf(BotOutboxItem o) => JsonDocument.Parse(o.Payload).RootElement.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";

    private void LinkAs(string role)
    {
        _routes["Link"] = _ => (HttpStatusCode.OK, "{\"userID\":12,\"customerID\":null,\"role\":\"" + role + "\",\"name\":\"Ali Agent\",\"language\":\"uz\"}");
        Handle(Text(1, "/start ABCDEFGH23")).Wait();
    }

    [Fact]
    public async Task Start_WithCode_LinksAndShowsRoleMenu()
    {
        _routes["Account"] = _ => (HttpStatusCode.OK, "null");
        await Handle(Text(1, "/start"));
        Assert.Contains("ulanish kodini", TextOf(Out().Last()));

        LinkAs("AGENT");
        var linkReq = _erp.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/Link"));
        Assert.Contains("\"code\":\"ABCDEFGH23\"", linkReq.Text);
        Assert.Contains("\"telegramUserId\":" + Tg, linkReq.Text);
        Assert.False(linkReq.Headers.ContainsKey("X-Acting-User"));

        var msg = Out().Last();
        Assert.Contains("Ali Agent", TextOf(msg));
        Assert.Contains("Kunni boshlash", msg.Payload);
        using var scope = _sp.CreateScope();
        var link = scope.ServiceProvider.GetRequiredService<BotDbContext>().Links.Single();
        Assert.Equal(12, link.UserId);
        Assert.Equal("AGENT", link.Role);
    }

    [Fact]
    public async Task Unlinked_RestoresFromErpAccount()
    {
        _routes["Account"] = _ => (HttpStatusCode.OK, "{\"userID\":12,\"role\":\"MANAGER\",\"name\":\"Rahbar\",\"language\":\"ru\"}");
        await Handle(Text(1, "/menu"));
        var last = Out().Last();
        Assert.Contains("Главное меню", TextOf(last));
        Assert.Contains("Итоги дня", last.Payload);
    }

    [Fact]
    public async Task DayStart_AsksLocation_ThenCallsErpAsActingUser()
    {
        LinkAs("AGENT");
        await Handle(Text(2, "▶️ Kunni boshlash"));
        Assert.Contains("request_location", Out().Last().Payload);

        _routes["Me"] = _ => (HttpStatusCode.OK, "{\"userId\":12,\"isAgent\":true,\"visitRadiusM\":200,\"requireSelfie\":false,\"day\":null}");
        _routes["DayStart"] = _ => (HttpStatusCode.OK, "{\"id\":1,\"startAt\":\"2026-10-19T09:01:00\"}");
        _routes["Route"] = _ => (HttpStatusCode.OK, "[{\"customerID\":5,\"name\":\"Do'kon\",\"planned\":true,\"visited\":false,\"debt\":0,\"overdue\":0}]");
        await Handle(Location(3, 41.3111, 69.2797));
        Assert.Contains("Selfi", TextOf(Out().Last()));
        Assert.Contains("O'tkazib yuborish", Out().Last().Payload);
        Assert.DoesNotContain(_erp.Requests, r => r.Uri.AbsolutePath.EndsWith("/DayStart"));

        await Handle(Text(4, "⏭ O'tkazib yuborish"));
        var req = _erp.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/DayStart"));
        Assert.Equal("12", req.Headers["X-Acting-User"]);
        Assert.Contains("41.3111", req.Text);
        Assert.Contains("\"selfieKey\":null", req.Text);
        var texts = Out().Select(TextOf).ToList();
        Assert.Contains(texts, t => t.Contains("09:01"));
        Assert.Contains(Out(), o => o.Payload.Contains("\"callback_data\":\"c:5\""));
    }

    [Fact]
    public async Task DayStart_RequiredSelfie_PhotoSavedAndKeySent_SkipRefused()
    {
        LinkAs("AGENT");
        _routes["Me"] = _ => (HttpStatusCode.OK, "{\"userId\":12,\"isAgent\":true,\"visitRadiusM\":200,\"requireSelfie\":true,\"day\":null}");
        _routes["DayStart"] = _ => (HttpStatusCode.OK, "{\"id\":1,\"startAt\":\"2026-10-19T09:01:00\"}");
        _routes["Route"] = _ => (HttpStatusCode.OK, "[]");
        await Handle(Text(2, "▶️ Kunni boshlash"));
        await Handle(Location(3, 41.3111, 69.2797));
        Assert.DoesNotContain("O'tkazib", Out().Last().Payload);

        await Handle(Text(4, "⏭ O'tkazib yuborish"));
        Assert.Contains("selfi kerak", TextOf(Out().Last()));
        Assert.DoesNotContain(_erp.Requests, r => r.Uri.AbsolutePath.EndsWith("/DayStart"));

        await Handle(Photo(5));
        var body = JsonDocument.Parse(_erp.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/DayStart")).Text).RootElement;
        var key = body.GetProperty("selfieKey").GetString();
        Assert.StartsWith("firma1/2026/10/", key);
        Assert.EndsWith(".jpg", key);
        Assert.Equal(41.3111m, body.GetProperty("lat").GetDecimal());
        Assert.Contains(Out().Select(TextOf), t => t.Contains("09:01") && t.Contains("Jonli joylashuv", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DayStart_AlreadyStarted_NoSelfieAsked()
    {
        LinkAs("AGENT");
        _routes["Me"] = _ => (HttpStatusCode.OK, "{\"userId\":12,\"requireSelfie\":true,\"day\":{\"startAt\":\"2026-10-19T08:00:00\",\"planned\":0,\"visited\":0}}");
        _routes["DayStart"] = _ => (HttpStatusCode.OK, "{\"id\":1,\"startAt\":\"2026-10-19T08:00:00\"}");
        _routes["Route"] = _ => (HttpStatusCode.OK, "[]");
        await Handle(Text(2, "▶️ Kunni boshlash"));
        await Handle(Location(3, 41.3111, 69.2797));
        Assert.Single(_erp.Requests, r => r.Uri.AbsolutePath.EndsWith("/DayStart"));
        Assert.Contains(Out().Select(TextOf), t => t.Contains("08:00"));
    }

    [Fact]
    public async Task NewPoint_FullFlow_SendsCustomerAndShowsCard()
    {
        LinkAs("AGENT");
        await Handle(Text(2, "➕ Yangi nuqta"));
        Assert.Contains("nomini", TextOf(Out().Last()));
        await Handle(Text(3, "X"));
        Assert.Contains("2–50", TextOf(Out().Last()));
        await Handle(Text(4, "  Baraka   do'koni "));
        await Handle(Text(5, "telefon yo'q"));
        Assert.Contains("Telefon noto'g'ri", TextOf(Out().Last()));
        await Handle(Contact(6, "+998 90 123-45-67"));
        Assert.Contains("request_location", Out().Last().Payload);
        await Handle(Text(7, "salom"));                       // joylashuv o'rniga matn — qayta so'raladi
        Assert.Contains("request_location", Out().Last().Payload);
        await Handle(Location(8, 41.30001, 69.25002));
        await Handle(Text(9, "bozor yonida"));
        Assert.Contains("fotosini", TextOf(Out().Last()));
        await Handle(Photo(10));
        var confirm = Out().Last();
        Assert.Contains("Baraka do'koni", TextOf(confirm));
        Assert.Contains("+998901234567", TextOf(confirm));
        Assert.Contains("\"callback_data\":\"nc!\"", confirm.Payload);

        _routes["NewCustomer"] = _ => (HttpStatusCode.OK, "{\"id\":77,\"duplicate\":false}");
        _routes["Customer"] = _ => (HttpStatusCode.OK, "{\"customerID\":77,\"name\":\"Baraka do'koni\",\"approved\":false,\"debt\":0,\"overdue\":0}");
        await Handle(Click(11, "nc!"));
        var req = _erp.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/NewCustomer"));
        Assert.Equal("12", req.Headers["X-Acting-User"]);
        var body = JsonDocument.Parse(req.Text).RootElement;
        Assert.Equal("Baraka do'koni", body.GetProperty("name").GetString());
        Assert.Equal("+998901234567", body.GetProperty("telephone").GetString());
        Assert.Equal("bozor yonida", body.GetProperty("landmark").GetString());
        Assert.Equal(41.30001m, body.GetProperty("lat").GetDecimal());
        Assert.StartsWith("firma1/", body.GetProperty("photoKey").GetString());
        Assert.Contains(Out().Select(TextOf), t => t.Contains("Nuqta qo'shildi"));
        Assert.Contains(Out().Select(TextOf), t => t.Contains("tasdiqlanmagan"));

        await Handle(Click(12, "nc!"));                         // holat tozalangan — ikkinchi so'rov ketmaydi
        Assert.Single(_erp.Requests, r => r.Uri.AbsolutePath.EndsWith("/NewCustomer"));
    }

    [Fact]
    public async Task LiveLocation_StartReplies_EditsThrottledAndSilent()
    {
        LinkAs("AGENT");
        _routes["Track"] = _ => (HttpStatusCode.OK, "{\"stored\":true,\"dayOpen\":true}");
        await Handle(LiveLocation(2, 41.3, 69.2));
        Assert.Contains("Jonli joylashuv qabul qilindi", TextOf(Out().Last()));
        var outCount = Out().Count;

        await Handle(LiveLocation(3, 41.301, 69.2, edited: true));   // 30 s o'tmagan — ERP'ga ketmaydi
        Assert.Single(_erp.Requests, r => r.Uri.AbsolutePath.EndsWith("/Track"));
        _clock.Now = _clock.Now.AddSeconds(31);
        await Handle(LiveLocation(4, 41.302, 69.2, edited: true));
        var tracks = _erp.Requests.Where(r => r.Uri.AbsolutePath.EndsWith("/Track")).ToList();
        Assert.Equal(2, tracks.Count);
        Assert.Equal("12", tracks[1].Headers["X-Acting-User"]);
        Assert.Contains("41.302", tracks[1].Text);

        _routes["Track"] = _ => (HttpStatusCode.ServiceUnavailable, "");
        _clock.Now = _clock.Now.AddSeconds(31);
        await Handle(LiveLocation(5, 41.303, 69.2, edited: true));   // ERP xatosi — foydalanuvchiga xabar yo'q
        Assert.Equal(outCount, Out().Count);

        _routes["Track"] = _ => (HttpStatusCode.OK, "{\"stored\":false,\"dayOpen\":false}");
        _clock.Now = _clock.Now.AddSeconds(31);
        await Handle(LiveLocation(6, 41.3, 69.2));
        Assert.Contains("Kunni boshlang", TextOf(Out().Last()));
    }

    [Fact]
    public async Task LiveEdits_FromNonFieldUsers_Ignored()
    {
        LinkAs("MANAGER");
        await Handle(LiveLocation(2, 41.3, 69.2, edited: true));
        Assert.DoesNotContain(_erp.Requests, r => r.Uri.AbsolutePath.EndsWith("/Track"));
    }

    [Fact]
    public async Task Customer_DebtAndStatement_ByTelegramId()
    {
        _routes["Link"] = _ => (HttpStatusCode.OK, "{\"userID\":null,\"customerID\":5,\"role\":\"CUSTOMER\",\"name\":\"Baraka\",\"language\":null}");
        await Handle(Text(1, "/start ABCDEFGH23"));
        Assert.Contains("Qarzim", Out().Last().Payload);

        _routes["MyBalance"] = _ => (HttpStatusCode.OK,
            "{\"name\":\"Baraka\",\"debt\":1500000,\"notDue\":1000000,\"overdue\":500000,\"advance\":0,\"maxOverdueDays\":12,\"creditLimit\":3000000,\"paymentDays\":10," +
            "\"lastPayments\":[{\"date\":\"15.10.2026\",\"amount\":200000}]}");
        await Handle(Text(2, "💳 Qarzim"));
        var debtReq = _erp.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/MyBalance"));
        Assert.Contains("telegramUserId=" + Tg, debtReq.Uri.Query);
        Assert.False(debtReq.Headers.ContainsKey("X-Acting-User"));
        var debt = TextOf(Out().Last());
        Assert.Contains("1 500 000", debt);
        Assert.Contains("12 kun", debt);
        Assert.Contains("15.10.2026 — 200 000", debt);

        await Handle(Text(3, "📄 Akt sverki"));
        Assert.Contains("ak:m1", Out().Last().Payload);
        _routes["MyStatement"] = _ => (HttpStatusCode.OK,
            "{\"name\":\"Baraka\",\"from\":\"01.09.2026\",\"to\":\"30.09.2026\",\"opening\":100000,\"totalDebit\":500000,\"totalCredit\":200000,\"closing\":400000," +
            "\"rows\":[{\"date\":\"05.09.2026\",\"type\":\"Jo'natish\",\"doc\":\"BUY-1\",\"debit\":500000,\"credit\":0,\"balance\":600000}," +
            "{\"date\":\"20.09.2026\",\"type\":\"To'lov\",\"doc\":\"TOL-3\",\"debit\":0,\"credit\":200000,\"balance\":400000}]}");
        await Handle(Click(4, "ak:m1"));
        var q = _erp.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/MyStatement")).Uri.Query;
        Assert.Contains("from=2026-09-01", q);
        Assert.Contains("to=2026-09-30", q);
        Assert.Contains("lang=uz", q);
        var stmt = TextOf(Out().Last());
        Assert.Contains("05.09 Jo'natish BUY-1: +500 000 → 600 000", stmt);
        Assert.Contains("−200 000", stmt);
        Assert.Contains("Yakuniy qoldiq: 400 000", stmt);
    }

    [Fact]
    public async Task Agent_CannotUseCustomerSections()
    {
        LinkAs("AGENT");
        await Handle(Text(2, "💳 Qarzim"));
        Assert.Contains("mijozlar uchun", TextOf(Out().Last()));
        Assert.DoesNotContain(_erp.Requests, r => r.Uri.AbsolutePath.EndsWith("/MyBalance"));
    }

    [Fact]
    public async Task Payment_AmountMethod_UsesSameRequestIdOnRetry()
    {
        LinkAs("AGENT");
        _routes["Customer"] = _ => (HttpStatusCode.OK, "{\"customerID\":5,\"name\":\"Do'kon\",\"debt\":300000,\"overdue\":0,\"approved\":true,\"openVisitId\":7}");
        await Handle(Click(2, "pay:5:7"));
        Assert.Contains("300 000", TextOf(Out().Last()));

        await Handle(Text(3, "abc"));
        Assert.Contains("raqam", TextOf(Out().Last()));
        await Handle(Text(4, "250 000"));
        Assert.Contains("pm:CASH", Out().Last().Payload);

        var calls = 0;
        _routes["Payment"] = _ => ++calls == 1
            ? (HttpStatusCode.ServiceUnavailable, "")
            : (HttpStatusCode.OK, "{\"id\":3,\"docNo\":\"TOL-1\",\"amount\":250000,\"onHand\":250000}");
        await Handle(Click(5, "pm:CASH"));
        Assert.Contains("ERP bilan aloqa yo'q", TextOf(Out().Last()));
        await Handle(Click(6, "pm:CASH"));   // holat saqlangan — qayta bosish
        Assert.Contains("TOL-1", TextOf(Out().Last()));

        var payments = _erp.Requests.Where(r => r.Uri.AbsolutePath.EndsWith("/Payment")).Select(r => JsonDocument.Parse(r.Text).RootElement).ToList();
        Assert.Equal(2, payments.Count);
        Assert.Equal(payments[0].GetProperty("clientRequestId").GetString(), payments[1].GetProperty("clientRequestId").GetString());
        Assert.Equal(250000m, payments[1].GetProperty("amount").GetDecimal());
        Assert.Equal(7, payments[1].GetProperty("visitId").GetInt32());

        await Handle(Click(7, "pm:CASH"));   // holat tozalangan — yangi to'lov ketmaydi
        Assert.Equal(2, _erp.Requests.Count(r => r.Uri.AbsolutePath.EndsWith("/Payment")));
    }

    [Fact]
    public async Task Payment_OffersOnlyMethodsAllowedInErp()
    {
        LinkAs("AGENT");
        _routes["Customer"] = _ => (HttpStatusCode.OK, "{\"customerID\":5,\"name\":\"Do'kon\",\"debt\":0,\"overdue\":0,\"approved\":true,\"openVisitId\":7}");
        _routes["Me"] = _ => (HttpStatusCode.OK, "{\"userId\":12,\"isAgent\":true,\"visitRadiusM\":200,\"payMethods\":[\"CASH\",\"CARD\",\"CLICK\"]}");
        await Handle(Click(2, "pay:5:7"));
        await Handle(Text(3, "100000"));
        var keyboard = Out().Last().Payload;
        Assert.Contains("pm:CASH", keyboard);
        Assert.Contains("pm:CARD", keyboard);
        Assert.Contains("pm:CLICK", keyboard);
        Assert.DoesNotContain("pm:PAYME", keyboard);
        Assert.DoesNotContain("pm:TRANSFER", keyboard);
    }

    [Fact]
    public async Task BusinessError_ShownToUser_CallbacksAnswered()
    {
        LinkAs("AGENT");
        _routes["CheckIn"] = _ => (HttpStatusCode.BadRequest, "{\"error\":\"Siz mijozdan 450 m uzoqdasiz (ruxsat: 200 m).\"}");
        await Handle(Click(2, "ci:5"));
        Assert.Contains(Out(), o => o.Method == "answerCallbackQuery");
        await Handle(Location(3, 41.3151, 69.2797));
        Assert.Contains("450 m", TextOf(Out().Last()));
        var checkIn = JsonDocument.Parse(_erp.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/CheckIn")).Text).RootElement;
        Assert.Equal(5, checkIn.GetProperty("customerId").GetInt32());
        Assert.Equal(15, checkIn.GetProperty("accuracyM").GetInt32());
    }

    [Fact]
    public async Task LicenseBlocked_ExplainedNotAsConnectionOrRoleError()
    {
        LinkAs("AGENT");
        _routes["Route"] = _ => (HttpStatusCode.Forbidden, "{\"error\":\"License\"}");
        await Handle(Text(2, "🗺 Marshrut"));
        var text = TextOf(Out().Last());
        Assert.Contains("litsenziyasi", text);
        Assert.DoesNotContain("rolingiz", text);
    }

    [Fact]
    public async Task Manager_ConfirmsOverride_FieldActionsDenied()
    {
        LinkAs("MANAGER");
        _routes["ConfirmOverride"] = _ => (HttpStatusCode.OK, "{\"id\":42,\"docNo\":\"BUY-42\",\"status\":\"Confirmed\"}");
        await Handle(Click(2, "ov:42"));
        Assert.Contains(Out(), o => o.Method == "editMessageReplyMarkup");
        Assert.Contains("BUY-42", TextOf(Out().Last()));

        await Handle(Click(3, "pay:5:0"));
        Assert.Contains("rolingiz", TextOf(Out().Last()));
        Assert.DoesNotContain(_erp.Requests, r => r.Uri.AbsolutePath.EndsWith("/Customer"));
    }

    [Fact]
    public async Task GroupChats_AndBots_Ignored()
    {
        var group = new TgUpdate(1, new TgMessage(1, new TgUser(Tg, false, "Ali", null, null, null), new TgChat(-100, "supergroup"), 0, "/start X",
            null, null, null, null), null, null);
        await Handle(group);
        var bot = new TgUpdate(2, new TgMessage(2, new TgUser(9, true, "B", null, null, null), new TgChat(9, "private"), 0, "hi",
            null, null, null, null), null, null);
        await Handle(bot);
        Assert.Empty(Out());
        Assert.Empty(_erp.Requests);
    }
}
