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

    [Fact]
    public void MiniAppUrl_AndStopLabel()
    {
        Assert.Equal("https://bot.uz/app/?t=firma1&c=5&v=9", UpdateRouter.MiniAppUrl("https://bot.uz/", "firma1", 5, 9));
        Assert.Equal("https://bot.uz/app/?t=firma1&c=5", UpdateRouter.MiniAppUrl("https://bot.uz", "firma1", 5, null));
        var stop = new ErpRouteStop(1, "Do'kon", null, null, null, null, null, true, true, "ORDER", 150000, 1);
        Assert.Equal("✅ Do'kon · 150 000❗", UpdateRouter.StopLabel(stop));
        Assert.StartsWith("⏳", UpdateRouter.StopLabel(stop with { Visited = false }));
        Assert.True(UpdateRouter.StopLabel(stop with { Name = new string('x', 100) }).Length <= 60);
    }
}

/// <summary>UpdateRouter oqimlari: in-memory bot bazasi + soxta ERP (imzo tekshiriladi).</summary>
public class HandlerTests : IDisposable
{
    private const string Master = "0123456789abcdef0123456789abcdef-bot";
    private readonly ServiceProvider _sp;
    private readonly FakeHandler _erp;
    private readonly TenantRegistry.Tenant _tenant;
    private readonly Dictionary<string, Func<FakeHandler.Captured, (HttpStatusCode, string)>> _routes = new();

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
        services.AddSingleton<TimeProvider>(new FixedClock(new DateTimeOffset(2026, 10, 19, 6, 0, 0, TimeSpan.Zero)));
        services.AddSingleton(Options.Create(new BotOptions { MasterKey = Master, PublicBaseUrl = "https://bot.example.uz" }));
        services.AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(_erp));
        services.AddSingleton<ErpClientFactory>();
        services.AddSingleton<OutboxSignal>();
        services.AddSingleton(new TelegramApi(new HttpClient(new FakeHandler(_ => FakeHandler.Json(HttpStatusCode.OK, "{\"ok\":true}")))));
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

        _routes["DayStart"] = _ => (HttpStatusCode.OK, "{\"id\":1,\"startAt\":\"2026-10-19T09:01:00\"}");
        _routes["Route"] = _ => (HttpStatusCode.OK, "[{\"customerID\":5,\"name\":\"Do'kon\",\"planned\":true,\"visited\":false,\"debt\":0,\"overdue\":0}]");
        await Handle(Location(3, 41.3111, 69.2797));
        var req = _erp.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/DayStart"));
        Assert.Equal("12", req.Headers["X-Acting-User"]);
        Assert.Contains("41.3111", req.Text);
        var texts = Out().Select(TextOf).ToList();
        Assert.Contains(texts, t => t.Contains("09:01"));
        Assert.Contains(Out(), o => o.Payload.Contains("\"callback_data\":\"c:5\""));
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
