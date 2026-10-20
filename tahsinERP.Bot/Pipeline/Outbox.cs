using System.Threading.Channels;
using tahsinERP.Bot.Data;
using tahsinERP.Bot.Telegram;

namespace tahsinERP.Bot.Pipeline;

/// <summary>Yangi ish borligi haqida ishlovchini uyg'otish (navbat — bazada, signal — faqat tezlik uchun).</summary>
public sealed class Signal
{
    private readonly Channel<bool> _ch = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Set() => _ch.Writer.TryWrite(true);

    public async Task WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try { await _ch.Reader.ReadAsync(cts.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
    }
}

public sealed class OutboxSignal
{
    public Signal Signal { get; } = new();
}

public sealed class InboxSignal
{
    public Signal Signal { get; } = new();
}

/// <summary>
/// Telegram'ga har qanday chiqish shu yerdan: chaqiruv BotOutbox'ga yoziladi (chaqiruvchining SaveChanges'i bilan),
/// keyin OutboxWorker cheklovlarga rioya qilib yuboradi. Priority 0 — foydalanuvchi amaliga javob.
/// </summary>
public sealed class Outbox(BotDbContext db, OutboxSignal signal, TimeProvider clock)
{
    public const byte Interactive = 0;
    public const byte Background = 1;

    public BotOutboxItem Enqueue(int tenantId, long chatId, string method, object payload, byte priority = Interactive, long? erpNotificationId = null)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var item = new BotOutboxItem
        {
            TenantId = tenantId, ChatId = chatId, Method = method, Payload = TgJson.Serialize(payload), Priority = priority,
            CreatedAt = now, NotBefore = now, ErpNotificationId = erpNotificationId
        };
        db.Outbox.Add(item);
        return item;
    }

    public BotOutboxItem Send(int tenantId, long chatId, string text, object? replyMarkup = null, byte priority = Interactive, long? erpNotificationId = null) =>
        Enqueue(tenantId, chatId, "sendMessage", new { chat_id = chatId, text = Cut(text), reply_markup = replyMarkup, link_preview_options = new { is_disabled = true } },
            priority, erpNotificationId);

    public BotOutboxItem Edit(int tenantId, long chatId, long messageId, string text, object? replyMarkup = null) =>
        Enqueue(tenantId, chatId, "editMessageText", new { chat_id = chatId, message_id = messageId, text = Cut(text), reply_markup = replyMarkup });

    public BotOutboxItem AnswerCallback(int tenantId, long chatId, string callbackId, string? text = null, bool alert = false) =>
        Enqueue(tenantId, chatId, "answerCallbackQuery", new { callback_query_id = callbackId, text, show_alert = alert });

    public BotOutboxItem SendLocation(int tenantId, long chatId, decimal lat, decimal lng) =>
        Enqueue(tenantId, chatId, "sendLocation", new { chat_id = chatId, latitude = lat, longitude = lng });

    /// <summary>Saqlangandan keyin chaqiriladi (ishlovchi darhol uyg'onadi).</summary>
    public void Flush() => signal.Signal.Set();

    /// <summary>Telegram xabari 4096 belgigacha.</summary>
    internal static string Cut(string s) => s.Length <= 4000 ? s : s[..3990] + "…";

    /// <summary>Chat kvotasini sarflamaydigan usullar.</summary>
    internal static bool CountsForChat(string method) => method is not ("answerCallbackQuery" or "setWebhook" or "getFile");
}
