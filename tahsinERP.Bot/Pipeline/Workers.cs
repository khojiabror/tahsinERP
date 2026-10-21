using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.Bot.Data;
using tahsinERP.Bot.Erp;
using tahsinERP.Bot.Handlers;
using tahsinERP.Bot.Telegram;

namespace tahsinERP.Bot.Pipeline;

/// <summary>
/// ERP bildirishnomalarini tortib olish (pull): har tenant uchun N soniyada — avval yuborilganlarini tasdiqlash (Ack),
/// keyin yangilarini olish va Outbox'ga qo'yish. ERP'da tranzaksion outbox (dst.NOTIFICATIONS), bot ERP'ga ulanadi —
/// ERP tomonida Hangfire/timer yo'q, bot o'chiq bo'lsa xabarlar yo'qolmaydi (navbatda kutadi).
/// </summary>
public sealed class NotificationPoller(IServiceScopeFactory scopes, TenantRegistry tenants, ErpClientFactory erpFactory, OutboxSignal outboxSignal,
    IOptions<BotOptions> options, TimeProvider clock, ILogger<NotificationPoller> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var period = TimeSpan.FromSeconds(Math.Clamp(options.Value.NotificationPollSeconds, 3, 300));
        using var timer = new PeriodicTimer(period, clock);
        do
        {
            foreach (var tenant in await tenants.AllAsync(ct))
            {
                try
                {
                    await PollAsync(tenant, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Bildirishnomalar: tenant {Slug}", tenant.Slug);
                }
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    internal async Task PollAsync(TenantRegistry.Tenant tenant, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<Outbox>();
        var erp = erpFactory.For(tenant.Row);
        var now = clock.GetUtcNow().UtcDateTime;

        // 1) yakunlanganlarini ERP'ga tasdiqlash
        var done = await db.Outbox.Where(o => o.TenantId == tenant.Id && o.ErpNotificationId != null && o.AckedAt == null
                                              && (o.SentAt != null || o.FailedAt != null)).Take(500).ToListAsync(ct);
        if (done.Count > 0)
        {
            await erp.PostAsync<object>("NotificationsAck", new
            {
                sent = done.Where(o => o.SentAt != null).Select(o => o.ErpNotificationId!.Value).ToArray(),
                failed = done.Where(o => o.SentAt == null).Select(o => o.ErpNotificationId!.Value).ToArray()
            }, null, ct);
            foreach (var o in done) o.AckedAt = now;
            await db.SaveChangesAsync(ct);
        }

        // 2) yangilari
        var pending = await erp.GetAsync<List<ErpPending>>("Notifications", null, ct, ("max", 100)) ?? [];
        if (pending.Count == 0) return;
        var ids = pending.Select(p => p.Id).ToList();
        var existing = await db.Outbox.Where(o => o.TenantId == tenant.Id && o.ErpNotificationId != null && ids.Contains(o.ErpNotificationId.Value))
            .ToListAsync(ct);
        foreach (var e in existing.Where(e => e.FailedAt != null && e.AckedAt != null))
            e.AckedAt = null;   // yuborib bo'lmagan — ERP urinishlar sonini oshirsin (10 tadan keyin qaytarmaydi)
        var chats = pending.Select(p => p.ChatId).Distinct().ToList();
        var langs = await db.Links.Where(l => l.TenantId == tenant.Id && chats.Contains(l.ChatId))
            .Select(l => new { l.ChatId, l.Language }).ToListAsync(ct);
        var files = scope.ServiceProvider.GetRequiredService<Files.IFileStore>();
        foreach (var p in pending.Where(p => existing.All(e => e.ErpNotificationId != p.Id)))
        {
            var lang = langs.FirstOrDefault(l => l.ChatId == p.ChatId)?.Language ?? Texts.Uz;
            if (InvoiceStopId(p.Payload) is int stopId && await InvoiceAsync(tenant, erp, files, outbox, p, stopId, lang, ct)) continue;
            outbox.Send(tenant.Id, p.ChatId, p.Text, KeyboardFor(p.Payload, lang), Outbox.Background, p.Id);
        }
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // parallel nusxa allaqachon qo'shgan — keyingi aylanada to'g'rilanadi
        }
        outboxSignal.Signal.Set();
    }

    /// <summary>
    /// Payload {"action":"override","orderId":N} — rahbarga "Tasdiqlash" tugmasi;
    /// {"action":"writeoff","id":N} — hisobdan chiqarish: "Tasdiqlash" / "Rad etish".
    /// </summary>
    /// <summary>
    /// Yetkazildi xabari + nakladnoy PDF: ERP'dan ma'lumot → PDF (MigraDoc) → fayl ombori → Outbox "sendDocument" (matn — izoh sifatida).
    /// Tayyorlab bo'lmasa (ERP/shrift xatosi) — false: xabar oddiy matn bo'lib ketadi, mijoz yetkazish haqidagi xabarni baribir oladi.
    /// </summary>
    private async Task<bool> InvoiceAsync(TenantRegistry.Tenant tenant, ErpClient erp, Files.IFileStore files, Outbox outbox, ErpPending p, int stopId,
        string lang, CancellationToken ct)
    {
        try
        {
            var inv = await erp.GetAsync<ErpInvoice>("StopInvoice", null, ct, ("stopId", stopId));
            if (inv == null || inv.Lines.Count == 0) return false;
            Documents.BotFontResolver.ExtraFontsDir ??= options.Value.FontsDir;
            var pdf = Documents.InvoicePdf.Render(inv, lang);
            var key = await files.SaveAsync(tenant.Slug, pdf, "pdf", ct);
            outbox.SendDocument(tenant.Id, p.ChatId, key, Documents.InvoicePdf.FileName(inv), p.Text, Outbox.Background, p.Id);
            return true;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Nakladnoy PDF tayyorlanmadi (tenant {Slug}, stop {Stop})", tenant.Slug, stopId);
            return false;
        }
    }

    /// <summary>Payload {"action":"invoice","stopId":N} — nakladnoy PDF bilan yuboriladigan xabar.</summary>
    internal static int? InvoiceStopId(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            return root.TryGetProperty("action", out var a) && a.GetString() == "invoice" && root.TryGetProperty("stopId", out var s) && s.TryGetInt32(out var id)
                ? id : null;
        }
        catch (JsonException) { return null; }
    }

    internal static object? KeyboardFor(string? payload, string lang)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.TryGetProperty("action", out var a) && a.GetString() == "override" &&
                root.TryGetProperty("orderId", out var o) && o.TryGetInt32(out var orderId))
                return Kb.Inline([Kb.Button(Texts.Get(lang, "Btn_Confirm"), Callback.Make("ov", orderId))]);
            if (root.TryGetProperty("action", out var w) && w.GetString() == "writeoff" &&
                root.TryGetProperty("id", out var i) && i.TryGetInt32(out var writeOffId))
                return Kb.Inline([Kb.Button(Texts.Get(lang, "Btn_Approve"), Callback.Make("wo", writeOffId, 1)),
                                  Kb.Button(Texts.Get(lang, "Btn_Reject"), Callback.Make("wo", writeOffId, 0))]);
        }
        catch (JsonException) { }
        return null;
    }
}

/// <summary>Kunlik yakun rahbarlarga: mahalliy vaqt DailySummaryHour (20:00) dan keyin, kuniga bir marta (2 soat oynasida).</summary>
public sealed class DailySummaryWorker(IServiceScopeFactory scopes, TenantRegistry tenants, ErpClientFactory erpFactory, OutboxSignal outboxSignal,
    IOptions<BotOptions> options, TimeProvider clock, ILogger<DailySummaryWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var tz = ResolveZone(options.Value.TimeZone);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        while (await timer.WaitForNextTickAsync(ct))
        {
            var local = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), tz);
            foreach (var tenant in await tenants.AllAsync(ct))
            {
                if (!IsDue(local, options.Value.DailySummaryHour, tenant.Row.LastSummaryDate)) continue;
                try
                {
                    await SendAsync(tenant, DateOnly.FromDateTime(local.DateTime), ct);
                    tenants.Invalidate();
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    log.LogWarning(ex, "Kunlik yakun: tenant {Slug}", tenant.Slug);
                }
            }
        }
    }

    internal static bool IsDue(DateTimeOffset local, int hour, DateOnly? lastSent) =>
        local.Hour >= hour && local.Hour < hour + 2 && lastSent != DateOnly.FromDateTime(local.DateTime);

    internal static TimeZoneInfo ResolveZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.CreateCustomTimeZone("UTC+5", TimeSpan.FromHours(5), "UTC+5", "UTC+5"); }
    }

    private async Task SendAsync(TenantRegistry.Tenant tenant, DateOnly today, CancellationToken ct)
    {
        var items = await erpFactory.For(tenant.Row).GetAsync<List<ErpPending>>("DailySummary", null, ct) ?? [];
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<Outbox>();
        foreach (var item in items) outbox.Send(tenant.Id, item.ChatId, item.Text, null, Outbox.Background);
        var row = await db.Tenants.FindAsync([tenant.Id], ct);
        if (row != null) row.LastSummaryDate = today;
        await db.SaveChangesAsync(ct);
        outboxSignal.Signal.Set();
    }
}

/// <summary>Har soatda: eski inbox/outbox yozuvlari va eskirgan suhbat holatlari.</summary>
public sealed class HousekeepingWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<HousekeepingWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
                var now = clock.GetUtcNow().UtcDateTime;
                var inboxCut = now.AddDays(-7);
                var outboxCut = now.AddDays(-30);
                await db.Inbox.Where(i => i.ProcessedAt != null && i.ProcessedAt < inboxCut).ExecuteDeleteAsync(ct);
                await db.Outbox.Where(o => (o.SentAt != null && o.SentAt < outboxCut || o.FailedAt != null && o.FailedAt < outboxCut)
                                           && (o.ErpNotificationId == null || o.AckedAt != null)).ExecuteDeleteAsync(ct);
                await scope.ServiceProvider.GetRequiredService<SessionStore>().PurgeAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogWarning(ex, "Tozalash xatosi");
            }
        }
    }
}
