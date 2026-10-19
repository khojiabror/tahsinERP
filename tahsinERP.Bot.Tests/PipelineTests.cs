using tahsinERP.Bot.Data;
using tahsinERP.Bot.Files;
using tahsinERP.Bot.Handlers;
using tahsinERP.Bot.Pipeline;
using tahsinERP.Bot.Telegram;

namespace tahsinERP.Bot.Tests;

public class PipelineTests
{
    private static readonly DateTime T0 = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RateLimiter_GlobalAndPerChat()
    {
        var rl = new RateLimiter(globalPerSecond: 5, chatBurst: 3, chatPerSecond: 1);
        // bitta chatga: 3 ta darhol, 4-si ~1 s kutadi
        for (int i = 0; i < 3; i++) Assert.True(rl.TryAcquire(1, 100, true, T0, out _));
        Assert.False(rl.TryAcquire(1, 100, true, T0, out var wait));
        Assert.InRange(wait.TotalMilliseconds, 900, 1100);
        Assert.True(rl.TryAcquire(1, 100, true, T0.AddSeconds(1), out _));

        // global: 3/s — turli chatlar ham umumiy kvotani sarflaydi; boshqa tenant (bot) — alohida
        var g = new RateLimiter(globalPerSecond: 3, chatBurst: 10);
        for (int i = 0; i < 3; i++) Assert.True(g.TryAcquire(1, 1000 + i, true, T0, out _));
        Assert.False(g.TryAcquire(1, 2000, true, T0, out var gw));
        Assert.InRange(gw.TotalMilliseconds, 300, 340);
        Assert.True(g.TryAcquire(2, 2000, true, T0, out _));
        Assert.True(g.TryAcquire(1, 2000, true, T0.AddMilliseconds(340), out _));
        // answerCallbackQuery — chat kvotasi emas, faqat global
        var rl2 = new RateLimiter(10, 1, 1);
        Assert.True(rl2.TryAcquire(1, 5, true, T0, out _));
        Assert.True(rl2.TryAcquire(1, 5, false, T0, out _));
        Assert.False(rl2.TryAcquire(1, 5, true, T0, out _));
    }

    [Fact]
    public void RateLimiter_PauseFromRetryAfter()
    {
        var rl = new RateLimiter(30);
        rl.Pause(1, 7, TimeSpan.FromSeconds(12), T0);
        Assert.False(rl.TryAcquire(1, 7, true, T0.AddSeconds(5), out var wait));
        Assert.Equal(TimeSpan.FromSeconds(7), wait);
        Assert.True(rl.TryAcquire(1, 8, true, T0.AddSeconds(5), out _), "boshqa chat to'xtamaydi");
        Assert.True(rl.TryAcquire(1, 7, true, T0.AddSeconds(12.1), out _));
    }

    [Fact]
    public void Outbox_ClassifiesTelegramResults()
    {
        TgResult R(bool ok, int? code = null, string? d = null, int? retry = null) =>
            new(ok, code, d, retry == null ? null : new TgResponseParameters(retry, null), null);

        Assert.Equal(OutboxWorker.OutcomeKind.Sent, OutboxWorker.Classify(R(true), "sendMessage").Kind);
        var flood = OutboxWorker.Classify(R(false, 429, "Too Many Requests: retry after 17", 17), "sendMessage");
        Assert.Equal(OutboxWorker.OutcomeKind.RetryAfter, flood.Kind);
        Assert.Equal(TimeSpan.FromSeconds(17), flood.Delay);
        Assert.Equal(OutboxWorker.OutcomeKind.Permanent, OutboxWorker.Classify(R(false, 403, "Forbidden: bot was blocked by the user"), "sendMessage").Kind);
        Assert.Equal(OutboxWorker.OutcomeKind.Sent,
            OutboxWorker.Classify(R(false, 400, "Bad Request: message is not modified"), "editMessageText").Kind);
        Assert.Equal(OutboxWorker.OutcomeKind.Sent,
            OutboxWorker.Classify(R(false, 400, "Bad Request: query is too old and response timeout expired"), "answerCallbackQuery").Kind);
        Assert.Equal(OutboxWorker.OutcomeKind.Transient, OutboxWorker.Classify(R(false, 502, "Bad Gateway"), "sendMessage").Kind);
        Assert.Equal(OutboxWorker.OutcomeKind.Transient, OutboxWorker.Classify(R(false, null, "timeout"), "sendMessage").Kind);
        Assert.Equal(TimeSpan.FromSeconds(8), OutboxWorker.Backoff(3));
        Assert.Equal(TimeSpan.FromSeconds(300), OutboxWorker.Backoff(20));
    }

    [Fact]
    public void Schema_SplitsIntoIdempotentBatches()
    {
        var batches = SchemaInitializer.Batches(SchemaInitializer.ReadScript()).ToList();
        Assert.True(batches.Count >= 10);
        Assert.All(batches, b => Assert.StartsWith("IF ", b));
        Assert.Contains(batches, b => b.Contains("UX_BotInbox_Update"));
        Assert.Contains(batches, b => b.Contains("UX_BotOutbox_ErpNotification"));
    }

    [Fact]
    public void DailySummary_DueOncePerDayInWindow()
    {
        var day = new DateOnly(2026, 10, 19);
        DateTimeOffset At(int h) => new(2026, 10, 19, h, 5, 0, TimeSpan.FromHours(5));
        Assert.False(DailySummaryWorker.IsDue(At(19), 20, null));
        Assert.True(DailySummaryWorker.IsDue(At(20), 20, null));
        Assert.True(DailySummaryWorker.IsDue(At(21), 20, day.AddDays(-1)));
        Assert.False(DailySummaryWorker.IsDue(At(21), 20, day));
        Assert.False(DailySummaryWorker.IsDue(At(23), 20, null), "kech qolgan — ertasiga");
        Assert.Equal(TimeSpan.FromHours(5), DailySummaryWorker.ResolveZone("Asia/Tashkent").GetUtcOffset(DateTime.UtcNow));
        Assert.Equal(TimeSpan.FromHours(5), DailySummaryWorker.ResolveZone("No/Such_Zone").BaseUtcOffset);
    }

    [Fact]
    public void Notification_OverridePayloadBecomesButton()
    {
        var kb = NotificationPoller.KeyboardFor("{\"action\":\"override\",\"orderId\":42}", Texts.Ru);
        Assert.NotNull(kb);
        var json = TgJson.Serialize(kb!);
        Assert.Contains("\"callback_data\":\"ov:42\"", json);
        Assert.Contains("Подтвердить", json);
        Assert.Null(NotificationPoller.KeyboardFor(null, Texts.Uz));
        Assert.Null(NotificationPoller.KeyboardFor("{\"action\":\"other\"}", Texts.Uz));
        Assert.Null(NotificationPoller.KeyboardFor("not json", Texts.Uz));
    }

    [Fact]
    public async Task FileStore_SavesUnderTenant_AndBlocksTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "botfiles-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalFileStore(root, new FixedClock(new DateTimeOffset(T0)));
            var key = await store.SaveAsync("firma1", [1, 2, 3], "jpg", CancellationToken.None);
            Assert.Matches(@"^firma1/2026/10/[0-9a-f]{32}\.jpg$", key);
            Assert.Equal(new byte[] { 1, 2, 3 }, await store.ReadAsync(key, CancellationToken.None));
            Assert.Null(store.PathOf("../x.jpg"));
            Assert.Null(store.PathOf("firma1\\..\\..\\x"));
            Assert.Null(store.PathOf(Path.Combine(root, "abs.jpg")));
            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync("../evil", [1], "jpg", CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync("firma1", [1], "exe", CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void TelegramUri_TokenWithColonStaysOnApiHost()
    {
        var uri = TelegramApi.MethodUri("123456:ABC-def_Gh", "sendMessage");
        Assert.Equal("https", uri.Scheme);
        Assert.Equal("api.telegram.org", uri.Host);
        Assert.Equal("/bot123456:ABC-def_Gh/sendMessage", uri.AbsolutePath);
    }

    [Fact]
    public void Outbox_CutsLongTexts_AndKnowsQuotaFreeMethods()
    {
        Assert.Equal(4000, Outbox.Cut(new string('a', 4000)).Length);
        Assert.True(Outbox.Cut(new string('a', 5000)).Length <= 4000);
        Assert.False(Outbox.CountsForChat("answerCallbackQuery"));
        Assert.True(Outbox.CountsForChat("sendMessage"));
    }
}
