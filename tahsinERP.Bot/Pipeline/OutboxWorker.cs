using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.Bot.Data;
using tahsinERP.Bot.Telegram;

namespace tahsinERP.Bot.Pipeline;

/// <summary>
/// BotOutbox -> Telegram. Har chatda tartib saqlanadi (bir vaqtda bitta so'rov), chatlar parallel.
/// Natija: ok — SentAt; 429 — retry_after bo'yicha kechiktirish; 400/403 — doimiy xato (FailedAt);
/// tarmoq/5xx — eksponensial qayta urinish (8 martagacha).
/// </summary>
public sealed class OutboxWorker(IServiceScopeFactory scopes, TenantRegistry tenants, TelegramApi telegram, OutboxSignal signal,
    IOptions<BotOptions> options, TimeProvider clock, ILogger<OutboxWorker> log, Files.IFileStore files) : BackgroundService
{
    internal const int MaxAttempts = 8;
    private readonly RateLimiter _limiter = new(Math.Max(1, options.Value.GlobalPerSecond));

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var lastTrim = DateTime.UtcNow;
        while (!ct.IsCancellationRequested)
        {
            TimeSpan next;
            try
            {
                next = await RoundAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Outbox aylanmasi xatosi");
                next = TimeSpan.FromSeconds(5);
            }
            var now = clock.GetUtcNow().UtcDateTime;
            if (now - lastTrim > TimeSpan.FromMinutes(10)) { _limiter.Trim(now); lastTrim = now; }
            await signal.Signal.WaitAsync(next, ct);
        }
    }

    /// <summary>Bitta aylana; keyingisigacha kutish vaqtini qaytaradi.</summary>
    private async Task<TimeSpan> RoundAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        var now = clock.GetUtcNow().UtcDateTime;
        var items = await db.Outbox.Where(o => o.SentAt == null && o.FailedAt == null && o.NotBefore <= now)
            .OrderBy(o => o.Priority).ThenBy(o => o.Id).Take(300).ToListAsync(ct);
        if (items.Count == 0)
        {
            var soonest = await db.Outbox.Where(o => o.SentAt == null && o.FailedAt == null).MinAsync(o => (DateTime?)o.NotBefore, ct);
            return soonest == null ? TimeSpan.FromSeconds(5) : Clamp(soonest.Value - now);
        }

        // har chat uchun navbatdagilardan kvota yetganicha (tartib buzilmasin: birinchi rad etilgandan keyingilari kutadi)
        var batches = new Dictionary<(int, long), List<BotOutboxItem>>();
        var blocked = new HashSet<(int, long)>();
        var wait = TimeSpan.FromSeconds(1);
        foreach (var item in items.OrderBy(i => i.Priority).ThenBy(i => i.Id))
        {
            var key = (item.TenantId, item.ChatId);
            if (blocked.Contains(key)) continue;
            if (!_limiter.TryAcquire(item.TenantId, item.ChatId, Outbox.CountsForChat(item.Method), now, out var w))
            {
                blocked.Add(key);
                if (w < wait) wait = w;
                continue;
            }
            if (!batches.TryGetValue(key, out var list)) batches[key] = list = [];
            list.Add(item);
        }

        await Parallel.ForEachAsync(batches, new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = ct }, async (kv, token) =>
        {
            var tenant = await tenants.ByIdAsync(kv.Key.Item1, token);
            foreach (var item in kv.Value)
            {
                if (tenant == null)
                {
                    item.FailedAt = clock.GetUtcNow().UtcDateTime;
                    item.Error = "Tenant faol emas";
                    continue;
                }
                var stop = await SendOneAsync(tenant, item, token);
                if (stop) break;   // chat to'xtatildi — qolganlari keyingi aylanada
            }
        });
        await db.SaveChangesAsync(ct);
        return batches.Count > 0 ? TimeSpan.Zero : Clamp(wait);
    }

    /// <returns>true — shu chatga hozircha yuborishni to'xtatish.</returns>
    private async Task<bool> SendOneAsync(TenantRegistry.Tenant tenant, BotOutboxItem item, CancellationToken ct)
    {
        TgResult r;
        try
        {
            r = item.Method == "sendDocument"
                ? await SendDocumentAsync(tenant, item, ct)
                : await telegram.CallAsync(tenant.Token, item.Method, item.Payload, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)   // bitta yozuv xatosi butun aylanani to'xtatmasin
        {
            r = new TgResult(false, null, ex.Message, null, null);
        }
        var outcome = Classify(r, item.Method);
        var now = clock.GetUtcNow().UtcDateTime;
        switch (outcome.Kind)
        {
            case OutcomeKind.Sent:
                item.SentAt = now;
                item.Error = null;
                return false;
            case OutcomeKind.RetryAfter:
                item.NotBefore = now + outcome.Delay;
                item.Error = r.Description;
                _limiter.Pause(item.TenantId, item.ChatId, outcome.Delay, now);
                return true;
            case OutcomeKind.Permanent:
                item.FailedAt = now;
                item.Error = Trim(r.ErrorCode + " " + r.Description);
                log.LogWarning("Telegram {Method} chat {Chat}: {Error}", item.Method, item.ChatId, item.Error);
                return false;
            default:
                item.Attempts++;
                item.Error = Trim(r.ErrorCode + " " + r.Description);
                if (item.Attempts >= MaxAttempts) item.FailedAt = now;
                else item.NotBefore = now + Backoff(item.Attempts);
                return true;
        }
    }

    /// <summary>Payload: {chat_id, caption, file_key, file_name}; fayl yo'q bo'lsa — doimiy xato (400).</summary>
    private async Task<TgResult> SendDocumentAsync(TenantRegistry.Tenant tenant, BotOutboxItem item, CancellationToken ct)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(item.Payload);
        var root = doc.RootElement;
        var key = root.TryGetProperty("file_key", out var k) ? k.GetString() : null;
        var bytes = key == null ? null : await files.ReadAsync(key, ct);
        if (bytes == null) return new TgResult(false, 400, "Fayl topilmadi: " + key, null, null);
        var caption = root.TryGetProperty("caption", out var c) ? c.GetString() : null;
        var name = root.TryGetProperty("file_name", out var n) ? n.GetString() ?? "document.pdf" : "document.pdf";
        return await telegram.SendDocumentAsync(tenant.Token, item.ChatId, caption, name, bytes, ct);
    }

    internal enum OutcomeKind { Sent, RetryAfter, Permanent, Transient }

    internal readonly record struct Outcome(OutcomeKind Kind, TimeSpan Delay);

    internal static Outcome Classify(TgResult r, string method)
    {
        if (r.Ok) return new(OutcomeKind.Sent, TimeSpan.Zero);
        if (r.ErrorCode == 429)
            return new(OutcomeKind.RetryAfter, TimeSpan.FromSeconds(Math.Clamp(r.Parameters?.RetryAfter ?? 5, 1, 3600)));
        // tahrirlashda matn o'zgarmagan, eskirgan tugma — yuborilgan deb hisoblanadi
        if (r.ErrorCode == 400 && r.Description != null &&
            (r.Description.Contains("message is not modified", StringComparison.OrdinalIgnoreCase) ||
             method == "answerCallbackQuery" && r.Description.Contains("query is too old", StringComparison.OrdinalIgnoreCase)))
            return new(OutcomeKind.Sent, TimeSpan.Zero);
        if (r.ErrorCode is 400 or 401 or 403 or 404) return new(OutcomeKind.Permanent, TimeSpan.Zero);
        return new(OutcomeKind.Transient, TimeSpan.Zero);
    }

    internal static TimeSpan Backoff(int attempts) => TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, attempts)));

    private static TimeSpan Clamp(TimeSpan t) => t < TimeSpan.FromMilliseconds(50) ? TimeSpan.FromMilliseconds(50) : t > TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : t;

    private static string Trim(string s) => s.Length <= 1000 ? s : s[..1000];
}
