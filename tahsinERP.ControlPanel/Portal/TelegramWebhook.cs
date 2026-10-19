using System.Text.Json;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Services;

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

        string? text = null, chatId = null;
        try
        {
            using var doc = await JsonDocument.ParseAsync(ctx.Request.Body, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("message", out var msg))
            {
                if (msg.TryGetProperty("text", out var t)) text = t.GetString();
                if (msg.TryGetProperty("chat", out var chat) && chat.TryGetProperty("id", out var id)) chatId = id.GetRawText();
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
                ? "Kod yaroqsiz yoki muddati o'tgan. Portal → Profil sahifasidan yangi havola oling yoki xodimdan yangi taklif so'rang."
                : $"Tayyor: {user.FullName}, bu chat tahsinERP portaliga bog'landi. Kirish kodlari shu yerga keladi.", ct);
            lf.CreateLogger("TelegramWebhook").LogInformation("Telegram /start bog'lash: {Result}", user is null ? "rad" : "#" + user.Id);
        }
        else
        {
            await notifier.SendDirectAsync(chatId, $"tahsinERP bot. Bu chat ID: {chatId}. Portalga bog'lash uchun portaldagi havoladan foydalaning.", ct);
        }
        return Results.Ok();
    }
}
