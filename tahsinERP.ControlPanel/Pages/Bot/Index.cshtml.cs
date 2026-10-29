using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Bot;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Hosting;
using tahsinERP.ControlPanel.I18n;
using tahsinERP.ControlPanel.Portal;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Bot;

/// <summary>
/// «Telegram bot» (@tahsinERPBot sotuv boti): holat (getMe/getWebhookInfo), webhook'ni o'rnatish (secret panel o'zi
/// yaratadi, buyruqlar va tavsif uz/ru), texnik yordam guruhini tanlash (bot qo'shilgan guruhlar ro'yxatidan),
/// statistika va yozishmalar ro'yxati. Ko'rish — Owner va Sales, sozlash — faqat Owner.
/// </summary>
public class IndexModel(ControlDbContext db, IBotApi api, BotConfig config, ISecretProtector protector, IOptions<TelegramOptions> tg,
    IOptions<PanelHostOptions> hosts, AuditLogger audit, Clock clock, ILogger<IndexModel> log) : PanelPageModel
{
    public static readonly string[] AllowedUpdates = ["message", "callback_query", "my_chat_member"];

    public bool TokenConfigured => api.IsConfigured;
    public string? BotUsername { get; private set; }
    public string? WebhookUrl { get; private set; }
    public int PendingUpdates { get; private set; }
    public string? WebhookError { get; private set; }
    public DateTime? WebhookErrorAt { get; private set; }
    public string? ApiError { get; private set; }
    public string ExpectedUrl => "https://" + hosts.Value.EffectiveAdminHost + TelegramWebhook.Path;
    public BotSettings Settings { get; private set; } = new() { Id = 1 };
    public BotConfig.Snapshot Effective { get; private set; } = new(null, null, null);
    public bool SupportFromEnv => Settings.SupportChatId is null && Effective.SupportChatId is not null;
    public List<BotGroup> Groups { get; private set; } = [];

    public int Chats { get; private set; }
    public int Active7 { get; private set; }
    public int Blocked { get; private set; }
    public int BotLeads { get; private set; }
    public int OpenSupport { get; private set; }
    public double? AvgRating { get; private set; }

    public List<BotThread> Rows { get; private set; } = [];
    public PageInfo Pager { get; private set; } = new(1, PageInfo.DefaultSize, 0);
    public Dictionary<long, BotChat> ChatsById { get; private set; } = [];

    [BindProperty] public long? SupportChatId { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Settings = await db.BotSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, ct) ?? Settings;
        Effective = await config.GetAsync(ct);
        Groups = await db.BotGroups.AsNoTracking().Where(g => g.IsMember).OrderByDescending(g => g.UpdatedAtUtc).ToListAsync(ct);
        DateTime week = clock.UtcNow.AddDays(-7);
        Chats = await db.BotChats.CountAsync(ct);
        Active7 = await db.BotChats.CountAsync(c => c.LastSeenUtc > week, ct);
        Blocked = await db.BotChats.CountAsync(c => c.BlockedAtUtc != null, ct);
        BotLeads = await db.DemoRequests.CountAsync(r => r.Source == DemoRequestSources.Telegram, ct);
        OpenSupport = await db.BotThreads.CountAsync(t => t.Kind == BotThreadKind.Support && t.Status == BotThreadStatus.Open, ct);
        AvgRating = await db.BotThreads.Where(t => t.Rating != null).AverageAsync(t => (double?)t.Rating, ct);
        (Rows, Pager) = await db.BotThreads.AsNoTracking().OrderByDescending(t => t.Id).ToPageAsync(HttpContext);
        var ids = Rows.Select(r => r.ChatId).Distinct().ToList();
        ChatsById = await db.BotChats.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        if (!TokenConfigured) return;
        try
        {
            var me = await api.CallAsync("getMe", new JsonObject(), ct);
            BotUsername = me.TryGetProperty("username", out var u) ? u.GetString() : null;
            var info = await api.CallAsync("getWebhookInfo", new JsonObject(), ct);
            WebhookUrl = info.TryGetProperty("url", out var url) ? url.GetString() : null;
            PendingUpdates = info.TryGetProperty("pending_update_count", out var p) ? p.GetInt32() : 0;
            WebhookError = info.TryGetProperty("last_error_message", out var e) ? e.GetString() : null;
            if (info.TryGetProperty("last_error_date", out var d) && d.TryGetInt64(out long unix))
                WebhookErrorAt = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
        }
        catch (Exception ex) when (ex is BotApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            ApiError = ex.Message;
        }
    }

    /// <summary>Webhook + buyruqlar menyusi + tavsif (uz standart, ru — language_code). Qayta bosish xavfsiz.</summary>
    public async Task<IActionResult> OnPostWebhookAsync(CancellationToken ct)
    {
        if (!IsOwner) return Forbidden();
        if (!api.IsConfigured)
        {
            FlashError = L.T("Pnl_Bot_NoToken");
            return RedirectToPage();
        }
        var s = await db.BotSettings.FirstAsync(x => x.Id == 1, ct);
        string secret;
        if (!string.IsNullOrWhiteSpace(tg.Value.WebhookSecret))
            secret = tg.Value.WebhookSecret;
        else if (s.WebhookSecret is { Length: > 0 } stored)
            secret = protector.Unprotect(stored, BotConfig.SecretPurpose);
        else
        {
            secret = BotConfig.NewSecret();
            s.WebhookSecret = protector.Protect(secret, BotConfig.SecretPurpose);
        }
        try
        {
            await api.CallAsync("setWebhook", new JsonObject
            {
                ["url"] = ExpectedUrl,
                ["secret_token"] = secret,
                ["allowed_updates"] = new JsonArray(AllowedUpdates.Select(x => (JsonNode)x).ToArray()),
                ["max_connections"] = 20,
            }, ct);
            foreach (var (lang, code) in new[] { ("uz", (string?)null), ("ru", "ru") })
            {
                await api.CallAsync("setMyCommands", WithLang(new JsonObject { ["commands"] = SalesBot.Commands(lang) }, code), ct);
                await api.CallAsync("setMyDescription", WithLang(new JsonObject { ["description"] = BotContent.T(lang, "bot.description") }, code), ct);
                await api.CallAsync("setMyShortDescription", WithLang(new JsonObject { ["short_description"] = BotContent.T(lang, "bot.short") }, code), ct);
            }
        }
        catch (Exception ex) when (ex is BotApiException or HttpRequestException or TaskCanceledException)
        {
            log.LogWarning(ex, "Telegram webhook o'rnatilmadi");
            FlashError = L.T("Pnl_Bot_WebhookFailed", ex.Message);
            return RedirectToPage();
        }
        s.WebhookSetAtUtc = clock.UtcNow;
        audit.Add("bot.webhook", "BotSettings", 1, null, new { Url = ExpectedUrl });
        await db.SaveChangesAsync(ct);
        config.Invalidate();
        FlashOk = L.T("Pnl_Bot_WebhookSet", ExpectedUrl);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSupportAsync(CancellationToken ct)
    {
        if (!IsOwner) return Forbidden();
        var s = await db.BotSettings.FirstAsync(x => x.Id == 1, ct);
        BotGroup? group = null;
        if (SupportChatId is long id)
        {
            group = await db.BotGroups.FirstOrDefaultAsync(g => g.Id == id && g.IsMember, ct);
            if (group is null) return RedirectToPage();
        }
        var before = new { s.SupportChatId };
        s.SupportChatId = group?.Id;
        audit.Add("bot.support-group", "BotSettings", 1, before, new { s.SupportChatId, group?.Title });
        await db.SaveChangesAsync(ct);
        config.Invalidate();
        if (group is not null && api.IsConfigured)
        {
            try
            {
                await api.SendAsync(group.Id, "✅ Bu guruh tahsinERP botining <b>texnik yordam guruhi</b> qilib belgilandi.\n" +
                                              "Mijoz murojaatlari shu yerga keladi; bot xabariga reply — mijozga javob, /close — murojaatni yopish.", ct: ct);
            }
            catch (Exception ex) when (ex is BotApiException or HttpRequestException or TaskCanceledException)
            {
                log.LogWarning(ex, "Support guruhiga sinov xabari yuborilmadi");
            }
        }
        FlashOk = group is null ? L.T("Pnl_Bot_SupportCleared") : L.T("Pnl_Bot_SupportSet", group.Title);
        return RedirectToPage();
    }

    private static JsonObject WithLang(JsonObject o, string? code)
    {
        if (code is not null) o["language_code"] = code;
        return o;
    }

    public static string KindLabel(BotThreadKind k) => k switch
    {
        BotThreadKind.Demo => L.T("Pnl_Bot_KindDemo"),
        BotThreadKind.Consultation => L.T("Pnl_Bot_KindConsult"),
        _ => L.T("Pnl_Bot_KindSupport"),
    };
}
