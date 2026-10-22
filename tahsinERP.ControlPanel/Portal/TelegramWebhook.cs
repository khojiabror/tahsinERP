using System.Text.Json;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.I18n;
using tahsinERP.ControlPanel.Services;
using tahsinERP.Localization;

namespace tahsinERP.ControlPanel.Portal;

/// <summary>
/// Telegram bot webhook'i: <c>POST /api/telegram/webhook</c>. Telegram har so'rovda
/// <c>X-Telegram-Bot-Api-Secret-Token</c> sarlavhasini yuboradi (setWebhook'dagi secret_token) — mos kelmasa 404.
/// Buyruqlar: <c>/start &lt;kod&gt;</c> — portal foydalanuvchisini shu chatga bog'lash; <c>/start</c> — chat ID'sini ko'rsatish
/// (xodim uni tenant'ning Telegram chati sifatida yozishi mumkin).
/// </summary>
public static class TelegramWebhook
{
    public const string Path = "/api/telegram/webhook";
    public const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    public static void Map(WebApplication app) =>
        app.MapPost(Path, HandleAsync).AllowAnonymous().DisableAntiforgery().RequireRateLimiting(Api.LicenseApi.RateLimitPolicy);

    private static async Task<IResult> HandleAsync(HttpContext ctx, IOptions<TelegramOptions> tg, PortalAuthService auth, PortalNotifier notifier,
        ILoggerFactory lf, CancellationToken ct)
    {
        string? secret = tg.Value.WebhookSecret;
        if (string.IsNullOrEmpty(secret) || !ctx.Request.Headers.TryGetValue(SecretHeader, out var given)
            || !PortalTokens.FixedEquals(given.ToString(), secret))
            return Results.NotFound();

        string? text = null, chatId = null, language = null;
        try
        {
            using var doc = await JsonDocument.ParseAsync(ctx.Request.Body, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("message", out var msg))
            {
                if (msg.TryGetProperty("text", out var t)) text = t.GetString();
                if (msg.TryGetProperty("chat", out var chat) && chat.TryGetProperty("id", out var id)) chatId = id.GetRawText();
                // Bot javobi foydalanuvchining Telegram tilida (uz/ru), noma'lum bo'lsa — o'zbekcha.
                if (msg.TryGetProperty("from", out var from) && from.TryGetProperty("language_code", out var lc) && lc.ValueKind == JsonValueKind.String)
                    language = Languages.TryNormalize(lc.GetString()!);
            }
        }
        catch (JsonException)
        {
            return Results.Ok(); // Telegram qayta yubormasin
        }
        if (text is null || chatId is null)
            return Results.Ok();

        string[] parts = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string cmd = parts[0].Split('@')[0];
        if (!cmd.Equals("/start", StringComparison.OrdinalIgnoreCase))
            return Results.Ok();

        if (parts.Length == 2)
        {
            var user = await auth.LinkTelegramAsync(parts[1], chatId, ct);
            await notifier.SendDirectAsync(chatId, user is null
                ? L.TFor(language ?? Languages.Default, "Ptl_BotLinkInvalid")
                : L.TFor(language ?? Languages.Default, "Ptl_BotLinked", user.FullName), ct);
            lf.CreateLogger("TelegramWebhook").LogInformation("Telegram /start bog'lash: {Result}", user is null ? "rad" : "#" + user.Id);
        }
        else
        {
            await notifier.SendDirectAsync(chatId, L.TFor(language ?? Languages.Default, "Ptl_BotInfo", chatId), ct);
        }
        return Results.Ok();
    }
}
