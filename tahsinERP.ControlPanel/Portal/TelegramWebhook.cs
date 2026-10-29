using System.Text.Json;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Bot;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Portal;

/// <summary>
/// Telegram bot webhook'i: <c>POST /api/telegram/webhook</c>. Telegram har so'rovda
/// <c>X-Telegram-Bot-Api-Secret-Token</c> sarlavhasini yuboradi (setWebhook'dagi secret_token) — mos kelmasa 404.
/// Yangilanish navbatga qo'yiladi va darhol 200 qaytadi; ishlov — <see cref="SalesBot"/> (sotuv boti, portalni
/// <c>/start &lt;kod&gt;</c> bilan bog'lash ham shu yerda). Webhook panel «Telegram bot» sahifasidan o'rnatiladi.
/// </summary>
public static class TelegramWebhook
{
    public const string Path = "/api/telegram/webhook";
    public const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";
    public const string RateLimitPolicy = "telegram-webhook";

    public static void Map(WebApplication app) =>
        app.MapPost(Path, HandleAsync).AllowAnonymous().DisableAntiforgery().RequireRateLimiting(RateLimitPolicy);

    private static async Task<IResult> HandleAsync(HttpContext ctx, BotConfig config, BotQueue queue, IOptions<TelegramOptions> tg, CancellationToken ct)
    {
        string? secret = (await config.GetAsync(ct)).WebhookSecret;
        if (string.IsNullOrEmpty(secret) || !ctx.Request.Headers.TryGetValue(SecretHeader, out var given)
            || !PortalTokens.FixedEquals(given.ToString(), secret))
            return Results.NotFound();

        try
        {
            using var doc = await JsonDocument.ParseAsync(ctx.Request.Body, cancellationToken: ct);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && queue.MarkSeen(doc.RootElement))
            {
                if (tg.Value.ProcessInline)
                    await ctx.RequestServices.GetRequiredService<SalesBot>().HandleAsync(doc.RootElement, ct);
                else
                    queue.Enqueue(doc.RootElement.Clone());
            }
        }
        catch (JsonException)
        {
            // Telegram qayta yubormasin
        }
        return Results.Ok();
    }
}
