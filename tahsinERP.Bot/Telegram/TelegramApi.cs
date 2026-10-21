using System.Text;
using System.Text.Json;

namespace tahsinERP.Bot.Telegram;

/// <summary>Telegram Bot API (oddiy HttpClient). Xabar yuborish faqat Outbox orqali; bu yerda — past darajali chaqiruv.</summary>
public sealed class TelegramApi(HttpClient http)
{
    public const string HttpClientName = "telegram";
    private const string Root = "https://api.telegram.org/";

    public async Task<TgResult> CallAsync(string token, string method, string payloadJson, CancellationToken ct)
    {
        using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        using var res = await http.PostAsync(MethodUri(token, method), content, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        try
        {
            return JsonSerializer.Deserialize<TgResult>(text, TgJson.Options)
                   ?? new TgResult(false, (int)res.StatusCode, "Bo'sh javob", null, null);
        }
        catch (JsonException)
        {
            return new TgResult(false, (int)res.StatusCode, text.Length > 200 ? text[..200] : text, null, null);
        }
    }

    public Task<TgResult> CallAsync(string token, string method, object payload, CancellationToken ct) =>
        CallAsync(token, method, TgJson.Serialize(payload), ct);

    /// <summary>Token "123:ABC" ko'rinishida — nisbiy URI sifatida qo'shib bo'lmaydi (":" sxema deb o'qiladi).</summary>
    internal static Uri MethodUri(string token, string method) => new(Root + "bot" + token + "/" + method);

    /// <summary>Hujjat yuborish (multipart): Outbox'dagi "sendDocument" yozuvi fayl omboridagi kalit bilan keladi.</summary>
    public async Task<TgResult> SendDocumentAsync(string token, long chatId, string? caption, string fileName, byte[] content, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(chatId.ToString(System.Globalization.CultureInfo.InvariantCulture)), "chat_id");
        if (!string.IsNullOrEmpty(caption)) form.Add(new StringContent(caption.Length <= 1024 ? caption : caption[..1020] + "…"), "caption");
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "application/octet-stream");
        form.Add(file, "document", fileName);
        using var res = await http.PostAsync(MethodUri(token, "sendDocument"), form, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        try
        {
            return JsonSerializer.Deserialize<TgResult>(text, TgJson.Options) ?? new TgResult(false, (int)res.StatusCode, "Bo'sh javob", null, null);
        }
        catch (JsonException)
        {
            return new TgResult(false, (int)res.StatusCode, text.Length > 200 ? text[..200] : text, null, null);
        }
    }

    /// <summary>Fayl (foto) yuklab olish: getFile -> https://api.telegram.org/file/bot&lt;token&gt;/&lt;path&gt;. 20 MB gacha.</summary>
    public async Task<byte[]?> DownloadFileAsync(string token, string fileId, CancellationToken ct)
    {
        var r = await CallAsync(token, "getFile", new { file_id = fileId }, ct);
        if (!r.Ok || r.Result is not { } el) return null;
        var file = el.Deserialize<TgFile>(TgJson.Options);
        if (string.IsNullOrEmpty(file?.FilePath)) return null;
        return await http.GetByteArrayAsync(new Uri(Root + "file/bot" + token + "/" + file.FilePath), ct);
    }
}
