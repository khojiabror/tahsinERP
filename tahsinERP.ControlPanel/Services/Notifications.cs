using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.I18n;

namespace tahsinERP.ControlPanel.Services;

public class TelegramOptions
{
    /// <summary>Bot token — faqat env/user-secrets (<c>ControlPanel__Telegram__BotToken</c>). Bo'sh bo'lsa xabarlar faqat logga yoziladi.</summary>
    public string? BotToken { get; set; }
    /// <summary>Xodimlar guruhi chat ID'si (ixtiyoriy) — barcha eslatmalarning nusxasi.</summary>
    public string? StaffChatId { get; set; }
    /// <summary>Bot foydalanuvchi nomi (@siz) — portalda t.me/&lt;BotUsername&gt;?start=... bog'lash havolasi uchun.</summary>
    public string? BotUsername { get; set; }
    /// <summary>setWebhook secret_token — /api/telegram/webhook shu sarlavhasiz so'rovni rad etadi. Faqat env/user-secrets.</summary>
    public string? WebhookSecret { get; set; }
    /// <summary>Texnik yordam guruhi (sotuv boti murojaatlari). Panel «Telegram bot» sahifasida tanlangani ustun.</summary>
    public string? SupportChatId { get; set; }
    /// <summary>Testlar: webhook yangilanishni navbatsiz, so'rov ichida qayta ishlaydi (javob kelganda natija tayyor).</summary>
    public bool ProcessInline { get; set; }
}

public interface ITelegramSender
{
    bool IsConfigured { get; }
    Task SendAsync(string chatId, string text, CancellationToken ct = default);
}

public sealed class TelegramSender(HttpClient http, IOptions<TelegramOptions> options) : ITelegramSender
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.BotToken);

    public async Task SendAsync(string chatId, string text, CancellationToken ct = default)
    {
        var url = $"https://api.telegram.org/bot{options.Value.BotToken}/sendMessage";
        using var response = await http.PostAsJsonAsync(url, new { chat_id = chatId, text, disable_web_page_preview = true }, ct);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Telegram {(int)response.StatusCode}: {(body.Length > 300 ? body[..300] : body)}");
        }
    }
}

/// <summary>
/// Muddat eslatmalari: obuna tugashiga 14/7/3/1 kun qolganda va tugagan kuni (00_PLAN.md 6). Har (tenant, shablon,
/// tugash sanasi, qabul qiluvchi) uchun bir marta — Notifications.DedupKey noyob. Token yo'q bo'lsa — faqat log.
/// </summary>
public sealed class ExpiryReminderService(ControlDbContext db, ITelegramSender telegram, IOptions<TelegramOptions> tgOptions, Clock clock,
    ILogger<ExpiryReminderService> log)
{
    public static readonly int[] Thresholds = [14, 7, 3, 1, 0];

    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        DateOnly today = clock.Today;
        DateOnly horizon = today.AddDays(Thresholds.Max());
        var subs = await db.Subscriptions.Include(s => s.Tenant).Include(s => s.Plan)
            .Where(s => s.Status == SubscriptionStatus.Active && s.EndDate >= today && s.EndDate <= horizon
                        && s.Tenant!.Status != TenantStatus.Archived && s.Tenant.Status != TenantStatus.Suspended
                        && !s.Tenant.IsDemo)   // demo obunasi o'zi uzayadi (DemoService) — eslatma kerak emas
            .ToListAsync(ct);

        // Tenantning eng uzoq faol obunasi bo'yicha (masalan TRIAL + yangi to'langan obuna bo'lsa — eskisi uchun eslatma kerak emas).
        // Paket va har bir qo'shimcha xizmat alohida: xizmat obunasi paket eslatmasini yashirmaydi (va aksincha).
        var latestEnd = await db.Subscriptions.Where(s => s.Status == SubscriptionStatus.Active)
            .GroupBy(s => new { s.TenantId, AddOnPlan = s.Plan!.IsAddOn ? s.PlanId : 0 })
            .Select(g => new { g.Key.TenantId, g.Key.AddOnPlan, End = g.Max(s => s.EndDate) })
            .ToDictionaryAsync(x => (x.TenantId, x.AddOnPlan), x => x.End, ct);

        int sent = 0;
        foreach (var s in subs)
        {
            bool addOn = SubscriptionService.IsAddOn(s);
            if (latestEnd.TryGetValue((s.TenantId, addOn ? s.PlanId : 0), out var end) && end > s.EndDate)
                continue;
            int days = s.EndDate.DayNumber - today.DayNumber;
            if (!Thresholds.Contains(days))
                continue;
            string template = (addOn ? s.Plan!.Code.ToLowerInvariant() + "-" : "") + "expiry-" + days;
            string text = BuildText(s, days);
            var recipients = new List<string?> { s.Tenant!.TelegramChatId, tgOptions.Value.StaffChatId }
                .Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToList();
            if (recipients.Count == 0)
                recipients.Add(null); // hech kim yo'q — baribir logga (dashboard'da ko'rinadi)
            foreach (var chat in recipients)
                if (await SendOnceAsync(s, template, chat, text, ct))
                    sent++;
        }
        return sent;
    }

    public static string BuildText(Subscription s, int days)
    {
        string when = days == 0 ? L.T("Pnl_Ntf_EndsToday") : L.T("Pnl_Ntf_DaysLeft", days, s.EndDate.ToString("dd.MM.yyyy"));
        string what = s.Period == BillingPeriod.Trial ? L.T("Pnl_Ntf_Trial")
            : SubscriptionService.IsAddOn(s) ? L.T("Pnl_Ntf_AddOn", s.Plan?.Name) : L.T("Pnl_Ntf_Subscription", s.Plan?.Name);
        return L.T("Pnl_Ntf_Text", s.Tenant?.LegalName, s.Tenant?.Slug, what, when) + " " +
               (s.Period == BillingPeriod.Trial ? L.T("Pnl_Ntf_TrialTail") : L.T("Pnl_Ntf_PayTail"));
    }

    private async Task<bool> SendOnceAsync(Subscription s, string template, string? chatId, string text, CancellationToken ct)
    {
        string dedup = $"{s.TenantId}|{template}|{s.EndDate:yyyyMMdd}|{chatId ?? "-"}";
        if (await db.Notifications.AnyAsync(n => n.DedupKey == dedup && n.Status != NotificationStatus.Failed, ct))
            return false;
        var existingFailed = await db.Notifications.FirstOrDefaultAsync(n => n.DedupKey == dedup, ct);
        var n = existingFailed ?? new Notification { TenantId = s.TenantId, Template = template, DedupKey = dedup, Recipient = chatId };
        n.Message = text;
        n.CreatedAtUtc = clock.UtcNow;
        if (chatId is null || !telegram.IsConfigured)
        {
            n.Channel = NotificationChannel.Log;
            n.Status = NotificationStatus.Logged;
            log.LogInformation("Eslatma (Telegram sozlanmagan yoki chat yo'q) → {Chat}: {Text}", chatId ?? "-", text);
        }
        else
        {
            n.Channel = NotificationChannel.Telegram;
            try
            {
                await telegram.SendAsync(chatId, text, ct);
                n.Status = NotificationStatus.Sent;
                n.Error = null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                n.Status = NotificationStatus.Failed; // ertaga qayta urinadi
                n.Error = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                log.LogWarning(ex, "Telegram xabari yuborilmadi ({Chat})", chatId);
            }
        }
        if (existingFailed is null)
            db.Notifications.Add(n);
        await db.SaveChangesAsync(ct);
        return n.Status != NotificationStatus.Failed;
    }
}

/// <summary>Kunlik fon ishi: hayot sikli (status) + eslatmalar. Ishga tushganda bir marta, keyin har kuni DailyJobHour da.</summary>
public sealed class DailyJobHostedService(IServiceScopeFactory scopes, TimeProvider time, IOptions<PanelOptions> options,
    ILogger<DailyJobHostedService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(30), time, stoppingToken); }
        catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(scopes, log, stoppingToken);
            try { await Task.Delay(DelayUntilNextRun(), time, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    public static async Task RunOnceAsync(IServiceScopeFactory scopes, ILogger log, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var sp = scope.ServiceProvider;
            int changed = await sp.GetRequiredService<SubscriptionService>().RunLifecycleAsync(ct);
            int keys = await EnsureKeysForStartedPeriodsAsync(sp, log, ct);
            int sent = await sp.GetRequiredService<ExpiryReminderService>().RunAsync(ct);
            int purged = await PurgeExpiredNoncesAsync(sp.GetRequiredService<ControlDbContext>(), sp.GetRequiredService<Clock>(), ct);
            int usagePurged = await sp.GetRequiredService<UsageService>().PurgeAsync(ct);
            log.LogInformation("Kunlik ish: {Changed} tenant holati o'zgardi, {Keys} kalit yangilandi, {Sent} eslatma, {Purged} eski nonce, {Usage} eski faollik yozuvi o'chirildi",
                changed, keys, sent, purged, usagePurged);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Kunlik ish xatosi");
        }
    }

    /// <summary>
    /// Oldindan to'langan, oxirgi 3 kunda boshlangan davrlar (masalan davr oxirida paket almashuvi) va oxirgi 3 kunda tugagan
    /// qo'shimcha xizmatlar (kalitdan "+KOD" olib tashlanadi) uchun kalitni obunaga moslaydi.
    /// EnsureForTenantAsync idempotent — mos kalit bo'lsa hech narsa qilmaydi.
    /// </summary>
    public static async Task<int> EnsureKeysForStartedPeriodsAsync(IServiceProvider sp, ILogger log, CancellationToken ct)
    {
        var db = sp.GetRequiredService<ControlDbContext>();
        DateOnly today = sp.GetRequiredService<Clock>().Today;
        DateOnly from = today.AddDays(-3);
        var tenantIds = await db.Subscriptions
            .Where(s => s.Tenant!.InstallationId != null
                && ((s.Status == SubscriptionStatus.Active && s.StartDate <= today && s.StartDate >= from)
                    || (s.Plan!.IsAddOn && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Expired)
                        && s.EndDate < today && s.EndDate >= from.AddDays(-1))))
            .Select(s => s.TenantId).Distinct().ToListAsync(ct);
        // Onlayn tekshiruv majburiy on-prem: aylanma kalit (grace'ning yarmi o'tganda yangisi) — 08_STAGE4.md 4.
        var rolling = await db.Tenants
            .Where(t => t.Deployment == DeploymentKind.OnPrem && t.OnlineCheck == OnlineCheckMode.Required && t.InstallationId != null
                && t.Status != TenantStatus.Archived && t.DeletedAtUtc == null)
            .Select(t => t.Id).ToListAsync(ct);
        tenantIds = tenantIds.Union(rolling).ToList();
        var issuer = sp.GetRequiredService<Licensing.LicenseIssuer>();
        int n = 0;
        foreach (int id in tenantIds)
        {
            try
            {
                var tenant = await db.Tenants.FirstAsync(t => t.Id == id, ct);
                if (await issuer.EnsureForTenantAsync(tenant, ct) is not null) n++;
            }
            catch (Exception ex) when (ex is DomainException or System.Security.Cryptography.CryptographicException or InvalidOperationException or NotSupportedException)
            {
                log.LogError(ex, "Yangi davr kaliti chiqmadi (tenant {Tenant})", id);
            }
        }
        return n;
    }

    /// <summary>License API nonce'lari: replay oynasidan (10 daq) o'tganlari endi kerak emas.</summary>
    public static Task<int> PurgeExpiredNoncesAsync(ControlDbContext db, Clock clock, CancellationToken ct = default)
    {
        DateTime now = clock.UtcNow;
        return db.ApiNonces.Where(n => n.ExpiresUtc < now).ExecuteDeleteAsync(ct);
    }

    private TimeSpan DelayUntilNextRun()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(time.GetUtcNow().UtcDateTime, zone);
        var next = nowLocal.Date.AddHours(Math.Clamp(options.Value.DailyJobHour, 0, 23));
        if (next <= nowLocal) next = next.AddDays(1);
        return next - nowLocal;
    }
}
