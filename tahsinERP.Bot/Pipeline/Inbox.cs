using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using tahsinERP.Bot.Data;
using tahsinERP.Bot.Handlers;
using tahsinERP.Bot.Telegram;

namespace tahsinERP.Bot.Pipeline;

/// <summary>
/// POST /tg/{slug}: Telegram webhook. secret_token sarlavhasi tekshiriladi, update BotInbox'ga yoziladi va darhol 200 qaytadi
/// (Telegram javobni kutmaydi, qayta yubormaydi). Takroriy update_id — unikal indeks bilan e'tiborsiz.
/// </summary>
public static class WebhookEndpoint
{
    public const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";
    private const int MaxBody = 1 << 20;

    public static void Map(IEndpointRouteBuilder app) => app.MapPost("/tg/{slug}", HandleAsync);

    internal static async Task<IResult> HandleAsync(string slug, HttpRequest request, TenantRegistry tenants, BotDbContext db,
        InboxSignal signal, TimeProvider clock, CancellationToken ct)
    {
        var tenant = await tenants.BySlugAsync(slug, ct);
        if (tenant == null) return Results.NotFound();
        if (!SecretMatches(request.Headers[SecretHeader].ToString(), tenant.WebhookSecret)) return Results.Unauthorized();
        if (request.ContentLength > MaxBody) return Results.StatusCode(413);

        string body;
        using (var reader = new StreamReader(request.Body, Encoding.UTF8))
            body = await reader.ReadToEndAsync(ct);
        long updateId;
        long? chatId;
        try
        {
            var update = TgJson.ParseUpdate(body);
            if (update == null) return Results.BadRequest();
            updateId = update.UpdateId;
            chatId = update.ChatId;
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        if (await db.Inbox.AnyAsync(i => i.TenantId == tenant.Id && i.UpdateId == updateId, ct))
            return Results.Ok();   // takroriy yetkazish
        db.Inbox.Add(new BotInboxItem
        {
            TenantId = tenant.Id, UpdateId = updateId, ChatId = chatId, Payload = body, ReceivedAt = clock.GetUtcNow().UtcDateTime
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // parallel takroriy yetkazish — allaqachon navbatda
        }
        signal.Signal.Set();
        return Results.Ok();
    }

    internal static bool SecretMatches(string given, string expected) =>
        !string.IsNullOrEmpty(expected) &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given ?? ""), Encoding.UTF8.GetBytes(expected));
}

/// <summary>
/// BotInbox ishlovchisi: navbatdagilarni "band" qiladi (UPDATE ... OUTPUT, READPAST — bir nechta nusxa ham xavfsiz),
/// bir chat ichida ketma-ket, chatlar parallel. Xato — 3 martagacha qayta urinish, keyin foydalanuvchiga umumiy xabar.
/// </summary>
public sealed class InboxWorker(IServiceScopeFactory scopes, TenantRegistry tenants, InboxSignal signal, OutboxSignal outboxSignal,
    TimeProvider clock, ILogger<InboxWorker> log) : BackgroundService
{
    internal const int MaxAttempts = 3;
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

    private const string ClaimSql = """
        UPDATE TOP (@take) i WITH (ROWLOCK, READPAST, UPDLOCK)
           SET LockedUntil = @until, Attempts = Attempts + 1
        OUTPUT inserted.Id, inserted.TenantId, inserted.UpdateId, inserted.ChatId, inserted.Payload, inserted.ReceivedAt,
               inserted.LockedUntil, inserted.ProcessedAt, inserted.Attempts, inserted.Error
          FROM dbo.BotInbox i
         WHERE i.ProcessedAt IS NULL AND (i.LockedUntil IS NULL OR i.LockedUntil < @now) AND i.Attempts < @max
        """;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            int handled = 0;
            try
            {
                handled = await RoundAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Inbox aylanmasi xatosi");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
            if (handled == 0) await signal.Signal.WaitAsync(TimeSpan.FromSeconds(3), ct);
        }
    }

    private async Task<int> RoundAsync(CancellationToken ct)
    {
        List<BotInboxItem> claimed;
        var now = clock.GetUtcNow().UtcDateTime;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
            claimed = await db.Inbox.FromSqlRaw(ClaimSql,
                    new SqlParameter("@take", 100), new SqlParameter("@until", now + Lease), new SqlParameter("@now", now),
                    new SqlParameter("@max", MaxAttempts))
                .AsNoTracking().ToListAsync(ct);
        }
        if (claimed.Count == 0) return 0;

        var groups = claimed.GroupBy(i => (i.TenantId, i.ChatId ?? -i.Id)).Select(g => g.OrderBy(i => i.UpdateId).ToList()).ToList();
        await Parallel.ForEachAsync(groups, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct }, async (items, token) =>
        {
            foreach (var item in items) await ProcessAsync(item, token);
        });
        outboxSignal.Signal.Set();
        return claimed.Count;
    }

    private async Task ProcessAsync(BotInboxItem item, CancellationToken ct)
    {
        string? error = null;
        var tenant = await tenants.ByIdAsync(item.TenantId, ct);
        if (tenant != null)
        {
            try
            {
                var update = TgJson.ParseUpdate(item.Payload);
                if (update != null)
                {
                    using var scope = scopes.CreateScope();
                    var router = scope.ServiceProvider.GetRequiredService<UpdateRouter>();
                    await router.HandleAsync(tenant, update, ct);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                log.LogError(ex, "Update {UpdateId} (tenant {Slug}) xatosi", item.UpdateId, tenant.Slug);
            }
        }

        using var s = scopes.CreateScope();
        var db = s.ServiceProvider.GetRequiredService<BotDbContext>();
        var row = await db.Inbox.FindAsync([item.Id], ct);
        if (row == null) return;
        var now = clock.GetUtcNow().UtcDateTime;
        if (error == null)
        {
            row.ProcessedAt = now;
            row.Error = null;
        }
        else
        {
            row.Error = error.Length > 1000 ? error[..1000] : error;
            row.LockedUntil = now + TimeSpan.FromSeconds(10 * row.Attempts);   // qayta urinish oldidan kutish
            if (row.Attempts >= MaxAttempts)
            {
                row.ProcessedAt = now;
                if (tenant != null && item.ChatId is long chat)
                    s.ServiceProvider.GetRequiredService<Outbox>().Send(tenant.Id, chat, Texts.Get(Texts.Uz, "Error_Generic"));
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
