using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace tahsinERP.Bot.Telegram;

// Bot API'ning bizga kerakli qismi (snake_case). To'liq ro'yxat: core.telegram.org/bots/api

public sealed record TgUser(long Id, bool IsBot, string? FirstName, string? LastName, string? Username, string? LanguageCode);

public sealed record TgChat(long Id, string? Type);

public sealed record TgLocation(double Latitude, double Longitude, double? HorizontalAccuracy, int? LivePeriod);

public sealed record TgPhotoSize(string FileId, string? FileUniqueId, int Width, int Height, long? FileSize);

public sealed record TgWebAppData(string Data, string? ButtonText);

public sealed record TgContact(string PhoneNumber, string? FirstName, long? UserId);

public sealed record TgMessage(long MessageId, TgUser? From, TgChat Chat, long Date, string? Text, string? Caption, TgLocation? Location,
    List<TgPhotoSize>? Photo, TgWebAppData? WebAppData, TgContact? Contact = null);

public sealed record TgCallbackQuery(string Id, TgUser From, TgMessage? Message, string? Data);

public sealed record TgUpdate(long UpdateId, TgMessage? Message, TgMessage? EditedMessage, TgCallbackQuery? CallbackQuery)
{
    public TgUser? From => Message?.From ?? EditedMessage?.From ?? CallbackQuery?.From;
    public long? ChatId => Message?.Chat.Id ?? EditedMessage?.Chat.Id ?? CallbackQuery?.Message?.Chat.Id ?? CallbackQuery?.From.Id;
}

public sealed record TgResponseParameters(int? RetryAfter, long? MigrateToChatId);

public sealed record TgResult(bool Ok, int? ErrorCode, string? Description, TgResponseParameters? Parameters, JsonElement? Result);

public sealed record TgFile(string FileId, string? FilePath, long? FileSize);

public static class TgJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping   // HTML'ga qo'yilmaydi; kirill/emoji o'z holicha (kichik payload)
    };

    public static TgUpdate? ParseUpdate(string json) => JsonSerializer.Deserialize<TgUpdate>(json, Options);

    public static string Serialize(object value) => JsonSerializer.Serialize(value, Options);
}
