using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Bot;

/// <summary>Telegram Bot API xatosi. <see cref="Blocked"/> — foydalanuvchi botni bloklagan (403).</summary>
public sealed class BotApiException(int code, string description) : Exception($"Telegram {code}: {description}")
{
    public int Code { get; } = code;
    public string Description { get; } = description;
    public bool Blocked => Code == 403;
    /// <summary>"message is not modified" — tahrirlanayotgan xabar o'zgarmagan (xato emas).</summary>
    public bool NotModified => Code == 400 && Description.Contains("not modified", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Bot API chaqiruvlari (sotuv boti). Barcha matnlar HTML parse_mode'da.</summary>
public interface IBotApi
{
    bool IsConfigured { get; }
    /// <summary>Xom chaqiruv: natija <c>result</c> maydoni. Xato — <see cref="BotApiException"/>.</summary>
    Task<JsonElement> CallAsync(string method, JsonObject payload, CancellationToken ct = default);
}

public sealed class TelegramBotApi(HttpClient http, IOptions<TelegramOptions> options) : IBotApi
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.BotToken);

    public async Task<JsonElement> CallAsync(string method, JsonObject payload, CancellationToken ct = default)
    {
        var url = $"https://api.telegram.org/bot{options.Value.BotToken}/{method}";
        using var response = await http.PostAsJsonAsync(url, payload, ct);
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = doc.RootElement;
        if (root.TryGetProperty("ok", out var ok) && ok.GetBoolean())
            return root.TryGetProperty("result", out var r) ? r.Clone() : default;
        int code = root.TryGetProperty("error_code", out var c) ? c.GetInt32() : (int)response.StatusCode;
        string desc = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : response.ReasonPhrase ?? "";
        throw new BotApiException(code, desc);
    }
}

/// <summary>Tez-tez ishlatiladigan chaqiruvlar.</summary>
public static class BotApiExtensions
{
    public static async Task<long> SendAsync(this IBotApi api, long chatId, string html, JsonNode? markup = null, long? replyTo = null,
        CancellationToken ct = default)
    {
        var p = new JsonObject
        {
            ["chat_id"] = chatId,
            ["text"] = html,
            ["parse_mode"] = "HTML",
            ["link_preview_options"] = new JsonObject { ["is_disabled"] = true },
        };
        if (markup is not null) p["reply_markup"] = markup.DeepClone();
        if (replyTo is long r) p["reply_parameters"] = new JsonObject { ["message_id"] = r, ["allow_sending_without_reply"] = true };
        var res = await api.CallAsync("sendMessage", p, ct);
        return res.ValueKind == JsonValueKind.Object && res.TryGetProperty("message_id", out var id) ? id.GetInt64() : 0;
    }

    /// <summary>Xabar matnini tahrirlaydi (inline menyu navigatsiyasi); o'zgarmagan bo'lsa jim o'tadi.</summary>
    public static async Task EditAsync(this IBotApi api, long chatId, long messageId, string html, JsonNode? markup, CancellationToken ct = default)
    {
        var p = new JsonObject
        {
            ["chat_id"] = chatId,
            ["message_id"] = messageId,
            ["text"] = html,
            ["parse_mode"] = "HTML",
            ["link_preview_options"] = new JsonObject { ["is_disabled"] = true },
        };
        if (markup is not null) p["reply_markup"] = markup.DeepClone();
        try { await api.CallAsync("editMessageText", p, ct); }
        catch (BotApiException ex) when (ex.NotModified) { }
    }

    /// <summary>Xabar nusxasi (rasm/fayl/ovoz ham) — muallifsiz. Yangi xabar ID'si.</summary>
    public static async Task<long> CopyAsync(this IBotApi api, long toChat, long fromChat, long messageId, long? replyTo = null,
        JsonNode? markup = null, CancellationToken ct = default)
    {
        var p = new JsonObject { ["chat_id"] = toChat, ["from_chat_id"] = fromChat, ["message_id"] = messageId };
        if (replyTo is long r) p["reply_parameters"] = new JsonObject { ["message_id"] = r, ["allow_sending_without_reply"] = true };
        if (markup is not null) p["reply_markup"] = markup.DeepClone();
        var res = await api.CallAsync("copyMessage", p, ct);
        return res.ValueKind == JsonValueKind.Object && res.TryGetProperty("message_id", out var id) ? id.GetInt64() : 0;
    }

    public static async Task AnswerCallbackAsync(this IBotApi api, string callbackId, string? text = null, CancellationToken ct = default)
    {
        var p = new JsonObject { ["callback_query_id"] = callbackId };
        if (text is not null) p["text"] = text;
        try { await api.CallAsync("answerCallbackQuery", p, ct); }
        catch (BotApiException) { } // eskirgan callback — muhim emas
    }

    /// <summary>Xabarga reaksiya (xodim javobi yetkazilganini bildiradi). Xato yutiladi.</summary>
    public static async Task ReactAsync(this IBotApi api, long chatId, long messageId, string emoji, CancellationToken ct = default)
    {
        try
        {
            await api.CallAsync("setMessageReaction", new JsonObject
            {
                ["chat_id"] = chatId,
                ["message_id"] = messageId,
                ["reaction"] = new JsonArray(new JsonObject { ["type"] = "emoji", ["emoji"] = emoji }),
            }, ct);
        }
        catch (BotApiException) { }
    }
}

/// <summary>Klaviaturalar (reply_markup).</summary>
public static class Kb
{
    public static JsonObject Btn(string text, string data) => new() { ["text"] = text, ["callback_data"] = data };

    public static JsonObject Url(string text, string url) => new() { ["text"] = text, ["url"] = url };

    public static JsonObject Inline(params IEnumerable<JsonObject>[] rows) =>
        new() { ["inline_keyboard"] = new JsonArray(rows.Select(r => (JsonNode)new JsonArray(r.Select(b => (JsonNode)b).ToArray())).ToArray()) };

    public static JsonObject InlineRows(IEnumerable<IEnumerable<JsonObject>> rows) => Inline(rows.ToArray());

    public static JsonObject[] Row(params JsonObject[] buttons) => buttons;

    /// <summary>Pastki (reply) klaviatura: oddiy matn tugmalari; <paramref name="contact"/> — telefon raqamini yuborish tugmasi.</summary>
    public static JsonObject Reply(IEnumerable<IEnumerable<string>> rows, string? contact = null, string? placeholder = null)
    {
        var all = new JsonArray();
        if (contact is not null)
            all.Add(new JsonArray(new JsonObject { ["text"] = contact, ["request_contact"] = true }));
        foreach (var row in rows)
        {
            var arr = new JsonArray(row.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => (JsonNode)new JsonObject { ["text"] = t }).ToArray());
            if (arr.Count > 0) all.Add(arr);
        }
        var o = new JsonObject { ["keyboard"] = all, ["resize_keyboard"] = true, ["one_time_keyboard"] = true };
        if (placeholder is not null) o["input_field_placeholder"] = placeholder.Length > 64 ? placeholder[..64] : placeholder;
        return o;
    }

    public static JsonObject RemoveReply() => new() { ["remove_keyboard"] = true };
}
