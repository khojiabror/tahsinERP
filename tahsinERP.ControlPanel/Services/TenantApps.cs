using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.I18n;

namespace tahsinERP.ControlPanel.Services;

/// <summary>
/// Telegram bot (bot.&lt;domen&gt;) va sayt-katalog ({firma}.sayt.&lt;domen&gt;) ilovalarining /admin/api manzillari.
/// AdminKey — faqat env (<c>ControlPanel__Apps__AdminKey</c>): bot/sayt master kalitidan HMAC hosilasi (Install-BotSite.ps1 yozadi);
/// u bilan bot tokenlarini ochib bo'lmaydi.
/// </summary>
public class AppsOptions
{
    /// <summary>Masalan https://bot.tahsinerp.uz</summary>
    public string? BotUrl { get; set; }
    /// <summary>Sayt ilovasiga istalgan *.sayt host orqali: masalan https://api.sayt.tahsinerp.uz</summary>
    public string? SiteUrl { get; set; }
    public string? AdminKey { get; set; }
    /// <summary>
    /// Sayt serverining ochiq IP manzil(lar)i (vergul bilan), masalan 63.183.64.107 — firmaning o'z domeni A yozuvi shunga qarashi kerak.
    /// Bo'sh — standart sayt hosti ({slug}.sayt.&lt;domen&gt;) manzillari DNS'dan olinadi.
    /// </summary>
    public string? SiteServerIp { get; set; }
}

public sealed record BotAppStatus(string Slug, bool Exists, bool Active, string? BotUsername, string? ErpBaseUrl, string? ErpTenant,
    string? WebhookUrl, int? PendingUpdates, string? LastError);

public sealed record SiteAppStatus(string Slug, bool Exists, bool Active, string? ErpBaseUrl, string? ErpTenant, DateTime? SnapshotAt, int? Products,
    string? Domain = null, bool RedirectToDomain = false);

/// <summary>DELETE /admin/api/tenants/{slug} javobi: Deleted — yozuv bor edi va o'chirildi.</summary>
public sealed record AppDeleteResult(string Slug, bool Deleted);

public interface IAppsClient
{
    bool BotConfigured { get; }
    bool SiteConfigured { get; }
    Task<BotAppStatus> GetBotAsync(string slug, CancellationToken ct = default);
    Task<BotAppStatus> ConnectBotAsync(string slug, string erpBaseUrl, string erpTenant, string? token, CancellationToken ct = default);
    Task<BotAppStatus> DisableBotAsync(string slug, CancellationToken ct = default);
    Task<SiteAppStatus> GetSiteAsync(string slug, CancellationToken ct = default);
    Task<SiteAppStatus> EnableSiteAsync(string slug, string erpBaseUrl, string erpTenant, CancellationToken ct = default);
    Task<SiteAppStatus> DisableSiteAsync(string slug, CancellationToken ct = default);
    /// <summary>Firmaning o'z domeni (host null — olib tashlash); redirect — standart subdomendan 301.</summary>
    Task<SiteAppStatus> SetSiteDomainAsync(string slug, string? host, bool redirect, CancellationToken ct = default);
    /// <summary>Firma o'chirilganda: bot ilovasidan butunlay (Telegram ulanishlari, seanslar, navbat). Idempotent.</summary>
    Task<AppDeleteResult> DeleteBotAsync(string slug, CancellationToken ct = default);
    /// <summary>Firma o'chirilganda: sayt ilovasidan butunlay (katalog nusxasi, ariza navbati). Idempotent.</summary>
    Task<AppDeleteResult> DeleteSiteAsync(string slug, CancellationToken ct = default);
}

public sealed class AppsHttpClient(HttpClient http, IOptions<AppsOptions> options) : IAppsClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private AppsOptions O => options.Value;

    public bool BotConfigured => !string.IsNullOrWhiteSpace(O.BotUrl) && !string.IsNullOrWhiteSpace(O.AdminKey);
    public bool SiteConfigured => !string.IsNullOrWhiteSpace(O.SiteUrl) && !string.IsNullOrWhiteSpace(O.AdminKey);

    public Task<BotAppStatus> GetBotAsync(string slug, CancellationToken ct = default) =>
        SendAsync<BotAppStatus>(HttpMethod.Get, O.BotUrl, slug, "", null, ct);

    public Task<BotAppStatus> ConnectBotAsync(string slug, string erpBaseUrl, string erpTenant, string? token, CancellationToken ct = default) =>
        SendAsync<BotAppStatus>(HttpMethod.Put, O.BotUrl, slug, "", new { erpBaseUrl, erpTenant, token }, ct);

    public Task<BotAppStatus> DisableBotAsync(string slug, CancellationToken ct = default) =>
        SendAsync<BotAppStatus>(HttpMethod.Post, O.BotUrl, slug, "/disable", null, ct);

    public Task<SiteAppStatus> GetSiteAsync(string slug, CancellationToken ct = default) =>
        SendAsync<SiteAppStatus>(HttpMethod.Get, O.SiteUrl, slug, "", null, ct);

    public Task<SiteAppStatus> EnableSiteAsync(string slug, string erpBaseUrl, string erpTenant, CancellationToken ct = default) =>
        SendAsync<SiteAppStatus>(HttpMethod.Put, O.SiteUrl, slug, "", new { erpBaseUrl, erpTenant }, ct);

    public Task<SiteAppStatus> DisableSiteAsync(string slug, CancellationToken ct = default) =>
        SendAsync<SiteAppStatus>(HttpMethod.Post, O.SiteUrl, slug, "/disable", null, ct);

    public Task<SiteAppStatus> SetSiteDomainAsync(string slug, string? host, bool redirect, CancellationToken ct = default) =>
        SendAsync<SiteAppStatus>(HttpMethod.Put, O.SiteUrl, slug, "/domain", new { host, redirect }, ct);

    public Task<AppDeleteResult> DeleteBotAsync(string slug, CancellationToken ct = default) =>
        SendAsync<AppDeleteResult>(HttpMethod.Delete, O.BotUrl, slug, "", null, ct);

    public Task<AppDeleteResult> DeleteSiteAsync(string slug, CancellationToken ct = default) =>
        SendAsync<AppDeleteResult>(HttpMethod.Delete, O.SiteUrl, slug, "", null, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string? baseUrl, string slug, string suffix, object? body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(O.AdminKey))
            throw new DomainException(L.T("Pnl_Apps_NotConfigured"));
        using var req = new HttpRequestMessage(method, $"{baseUrl.TrimEnd('/')}/admin/api/tenants/{Uri.EscapeDataString(slug)}{suffix}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", O.AdminKey);
        if (body != null) req.Content = JsonContent.Create(body, options: Json);
        HttpResponseMessage res;
        try { res = await http.SendAsync(req, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new DomainException(L.T("Pnl_Apps_NoConnection", new Uri(baseUrl).Host, ex.Message));
        }
        using (res)
        {
            if (res.IsSuccessStatusCode)
                return await res.Content.ReadFromJsonAsync<T>(Json, ct) ?? throw new DomainException(L.T("Pnl_Apps_EmptyResponse"));
            string error = $"{(int)res.StatusCode}";
            try
            {
                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.TryGetProperty("error", out var e)) error = e.GetString() ?? error;
            }
            catch (JsonException) { }
            throw new DomainException(res.StatusCode == System.Net.HttpStatusCode.Unauthorized
                ? L.T("Pnl_Apps_KeyMismatch") : error);
        }
    }
}

/// <summary>Firma sahifasidagi «Telegram bot va sayt» amallari: ERP manzili va tenant slug'i paneldagi tenantdan olinadi, amal auditga yoziladi.</summary>
public sealed class TenantAppsService(IAppsClient apps, ProvisioningService provisioning, AuditLogger audit, ControlDbContext db, IOptions<AppsOptions> options)
{
    public bool BotConfigured => apps.BotConfigured;
    public bool SiteConfigured => apps.SiteConfigured;

    /// <summary>Firma saytining ochiq manzili: SiteUrl hostidagi birinchi bo'lak o'rniga slug (api.sayt.x.uz → firma.sayt.x.uz).</summary>
    public string? SitePublicUrl(Tenant t)
    {
        if (!Uri.TryCreate(options.Value.SiteUrl, UriKind.Absolute, out var u) || !u.Host.Contains('.')) return null;
        return $"https://{t.Slug}.{u.Host[(u.Host.IndexOf('.') + 1)..]}";
    }

    private string ErpUrl(Tenant t) =>
        (provisioning.TenantUrl(t) ?? throw new DomainException(L.T("Pnl_Apps_NoBaseDomain"))) + "/";

    private static void EnsureSaaS(Tenant t)
    {
        if (t.Deployment != DeploymentKind.SaaS) throw new DomainException(L.T("Pnl_Apps_OnlySaas"));
        if (t.Status == TenantStatus.Archived) throw new DomainException(L.T("Pnl_Apps_Archived"));
    }

    public async Task<BotAppStatus> ConnectBotAsync(Tenant t, string? token, CancellationToken ct = default)
    {
        EnsureSaaS(t);
        token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
        var s = await apps.ConnectBotAsync(t.Slug, ErpUrl(t), t.Slug, token, ct);
        audit.Add(token != null ? "tenant.bot-connect" : "tenant.bot-refresh", "Tenant", t.Id, after: new { s.BotUsername, s.WebhookUrl });
        await db.SaveChangesAsync(ct);
        return s;
    }

    /// <summary>
    /// Botni firmadan uzish — to'liq tozalash (bot xizmati: ulanishlar, seanslar, navbatlar, Telegram'dagi kutib turgan
    /// yangilanishlar; firma ERP'si: Telegram bog'lanishlari, kutayotgan xabarlar, bot nomi). Shundan keyin shu bot tokeni
    /// boshqa firmaga toza ulanadi. Arxivlash esa faqat o'chirib qo'yadi (qayta tiklash uchun) — ProvisioningService.DisableAppsAsync.
    /// </summary>
    public async Task<bool> DisconnectBotAsync(Tenant t, CancellationToken ct = default)
    {
        var r = await apps.DeleteBotAsync(t.Slug, ct);
        audit.Add("tenant.bot-disconnect", "Tenant", t.Id, after: new { r.Deleted });
        await db.SaveChangesAsync(ct);
        return r.Deleted;
    }

    public async Task EnableSiteAsync(Tenant t, CancellationToken ct = default)
    {
        EnsureSaaS(t);
        await apps.EnableSiteAsync(t.Slug, ErpUrl(t), t.Slug, ct);
        audit.Add("tenant.site-enable", "Tenant", t.Id);
        await db.SaveChangesAsync(ct);
    }

    public async Task DisableSiteAsync(Tenant t, CancellationToken ct = default)
    {
        await apps.DisableSiteAsync(t.Slug, ct);
        audit.Add("tenant.site-disable", "Tenant", t.Id);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Firma butunlay o'chirilishidan oldin: bot va sayt ilovalaridan firma yozuvlari olib tashlanadi — shu nom bilan keyin ochilgan
    /// yangi firma eski Telegram ulanishlari va katalog nusxasini meros qilmasin. Ilova sozlanmagan bo'lsa o'tkazib yuboriladi;
    /// aloqa xatosi (DomainException) — o'chirish to'xtaydi, nom bo'shatilmaydi.
    /// </summary>
    public async Task PurgeAsync(Tenant t, CancellationToken ct = default)
    {
        bool? bot = apps.BotConfigured ? (await apps.DeleteBotAsync(t.Slug, ct)).Deleted : null;
        bool? site = apps.SiteConfigured ? (await apps.DeleteSiteAsync(t.Slug, ct)).Deleted : null;
        audit.Add("tenant.apps-purge", "Tenant", t.Id, after: new { Bot = bot, Site = site });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Sahifa uchun holat; xato bo'lsa matni qaytadi (sahifa baribir ochiladi).</summary>
    public async Task<(BotAppStatus? Bot, string? BotError, SiteAppStatus? Site, string? SiteError)> StatusAsync(Tenant t, CancellationToken ct = default)
    {
        BotAppStatus? bot = null; SiteAppStatus? site = null; string? botError = null, siteError = null;
        var botTask = apps.BotConfigured ? apps.GetBotAsync(t.Slug, ct) : null;
        var siteTask = apps.SiteConfigured ? apps.GetSiteAsync(t.Slug, ct) : null;
        try { if (botTask != null) bot = await botTask; } catch (DomainException ex) { botError = ex.Message; }
        try { if (siteTask != null) site = await siteTask; } catch (DomainException ex) { siteError = ex.Message; }
        return (bot, botError, site, siteError);
    }
}
