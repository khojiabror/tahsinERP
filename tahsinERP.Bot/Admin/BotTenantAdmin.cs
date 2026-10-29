using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.Bot.Data;
using tahsinERP.Bot.Erp;
using tahsinERP.Bot.Pipeline;
using tahsinERP.Bot.Security;
using tahsinERP.Bot.Telegram;

namespace tahsinERP.Bot.Admin;

/// <summary>
/// Firma botini ulash/o'chirish — CLI (`tenant add`, `webhook set`) va boshqaruv paneli API'si (/admin/api) shu yerdan.
/// Token faqat bazada shifrlangan holda saqlanadi; javob va loglarda chiqmaydi.
/// </summary>
public sealed partial class BotTenantAdmin(BotDbContext db, TelegramApi api, SecretBox secrets, IOptions<BotOptions> options, TenantRegistry registry,
    ErpClientFactory erp, ILogger<BotTenantAdmin> log)
{
    public sealed record Status(string Slug, bool Exists, bool Active, string? BotUsername, string? ErpBaseUrl, string? ErpTenant,
        string? WebhookUrl, int? PendingUpdates, string? LastError);

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,49}$")]
    private static partial Regex SlugRegex();

    public static string NormalizeSlug(string? slug)
    {
        var s = (slug ?? "").Trim().ToLowerInvariant();
        return SlugRegex().IsMatch(s) ? s : throw new ArgumentException("Slug: a-z, 0-9, '-' (50 belgigacha).");
    }

    /// <summary>Tenantni qo'shadi yoki yangilaydi. token null — mavjud token qoladi (faqat ERP manzili yangilanadi).</summary>
    public async Task<BotTenant> UpsertAsync(string slug, string erpBaseUrl, string? erpTenant, string? token, bool verifyToken, CancellationToken ct)
    {
        slug = NormalizeSlug(slug);
        var erp = new Uri(erpBaseUrl, UriKind.Absolute);
        if (erp.Scheme is not ("https" or "http")) throw new ArgumentException("ERP manzili http(s) bo'lishi kerak.");
        token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();

        var t = await db.Tenants.FirstOrDefaultAsync(x => x.Slug == slug, ct);
        if (t == null && token == null) throw new ArgumentException("Yangi bot uchun token kerak.");
        string? username = t?.BotUsername;
        if (token != null && verifyToken)
        {
            var me = await api.CallAsync(token, "getMe", new { }, ct);
            if (!me.Ok || me.Result is not { } r) throw new InvalidOperationException("Token noto'g'ri: " + me.Description);
            username = r.GetProperty("username").GetString();
        }
        if (token != null && username != null)
        {
            // Bitta Telegram boti bir vaqtda faqat bitta firmada: boshqa firmadagi eski ulanishi to'liq tozalanadi
            // (aks holda eski firma shu bot orqali o'z mijozlariga yozishda davom etadi yoki qayta yoqilganda botni tortib oladi).
            var others = await db.Tenants.Where(x => x.Slug != slug && x.BotUsername != null).ToListAsync(ct);
            foreach (var other in others.Where(o => string.Equals(o.BotUsername, username, StringComparison.OrdinalIgnoreCase)))
            {
                log.LogWarning("Bot @{Bot} {New} firmasiga ulandi — {Old} firmasidagi eski ulanishi tozalanadi", username, slug, other.Slug);
                await PurgeAsync(other, ct);
            }
        }
        if (t == null) db.Tenants.Add(t = new BotTenant { Slug = slug, CreatedAt = DateTime.UtcNow, WebhookSecret = NewSecret() });
        t.ErpBaseUrl = erp.AbsoluteUri;
        t.ErpTenant = string.IsNullOrWhiteSpace(erpTenant) ? null : erpTenant.Trim().ToLowerInvariant();
        if (token != null)
        {
            t.TokenEnc = secrets.Protect(token);
            t.BotUsername = username;
        }
        t.IsActive = true;
        await db.SaveChangesAsync(ct);
        registry.Invalidate();
        return t;
    }

    /// <summary>
    /// {PublicBaseUrl}/tg/{slug} — secret_token bilan. Qaytaradi: webhook manzili. <paramref name="dropPending"/> — yangi token
    /// ulanganda Telegram'da yig'ilib qolgan eski yangilanishlar (bot boshqa firmada yoki uzilgan paytdagi) tashlanadi.
    /// </summary>
    public async Task<string> SetWebhookAsync(BotTenant t, CancellationToken ct, bool dropPending = false)
    {
        if (!options.Value.PublicBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bot:PublicBaseUrl https bo'lishi kerak (Telegram talabi).");
        var url = options.Value.PublicBaseUrl.TrimEnd('/') + "/tg/" + t.Slug;
        var res = await api.CallAsync(secrets.Unprotect(t.TokenEnc), "setWebhook", new
        {
            url, secret_token = t.WebhookSecret, max_connections = 40, drop_pending_updates = dropPending,
            allowed_updates = new[] { "message", "edited_message", "callback_query" }
        }, ct);
        if (!res.Ok) throw new InvalidOperationException("Webhook o'rnatilmadi: " + res.Description);
        return url;
    }

    public async Task<JsonElement?> WebhookInfoAsync(BotTenant t, CancellationToken ct)
    {
        var res = await api.CallAsync(secrets.Unprotect(t.TokenEnc), "getWebhookInfo", new { }, ct);
        return res.Ok ? res.Result : throw new InvalidOperationException(res.Description);
    }

    public async Task<Status> StatusAsync(string slug, bool withWebhook, CancellationToken ct)
    {
        slug = NormalizeSlug(slug);
        var t = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug, ct);
        if (t == null) return new Status(slug, false, false, null, null, null, null, null, null);
        string? url = null, error = null;
        int? pending = null;
        if (withWebhook && t.IsActive)
        {
            try
            {
                if (await WebhookInfoAsync(t, ct) is { } info)
                {
                    url = info.TryGetProperty("url", out var u) ? u.GetString() : null;
                    pending = info.TryGetProperty("pending_update_count", out var p) ? p.GetInt32() : null;
                    error = info.TryGetProperty("last_error_message", out var e) ? e.GetString() : null;
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or CryptographicException or TaskCanceledException)
            {
                error = "Telegram'dan holat olinmadi: " + ex.Message;
            }
        }
        return new Status(t.Slug, true, t.IsActive, t.BotUsername, t.ErpBaseUrl, t.ErpTenant, url, pending, error);
    }

    /// <summary>Botni o'chiradi: Telegram webhook'i olib tashlanadi (iloji bo'lsa), tenant nofaol.</summary>
    public async Task DisableAsync(string slug, CancellationToken ct)
    {
        slug = NormalizeSlug(slug);
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.Slug == slug, ct) ?? throw new InvalidOperationException("Topilmadi: " + slug);
        try { await api.CallAsync(secrets.Unprotect(t.TokenEnc), "deleteWebhook", new { }, ct); }
        catch (Exception ex) when (ex is HttpRequestException or CryptographicException or TaskCanceledException) { }
        t.IsActive = false;
        await db.SaveChangesAsync(ct);
        registry.Invalidate();
    }

    /// <summary>
    /// Bot firmadan to'liq uziladi (panel «Botni uzish», firmani o'chirish, token boshqa firmaga ulanishi): webhook va Telegram'da
    /// kutib turgan yangilanishlar, firma ERP'sidagi Telegram bog'lanishlari / kutayotgan xabarlar / bot sozlamalari (iloji bo'lsa),
    /// bot bazasidagi ulanishlar, seanslar, navbatlar va tenant yozuvi. Shundan keyin bot boshqa firmaga toza ulanadi; shu nom
    /// bilan keyin ochilgan yangi firma eski ulanishlarni meros qilmaydi. Topilmasa — false (idempotent).
    /// </summary>
    public async Task<bool> DeleteAsync(string slug, CancellationToken ct)
    {
        slug = NormalizeSlug(slug);
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.Slug == slug, ct);
        if (t is null) return false;
        await PurgeAsync(t, ct);
        return true;
    }

    private async Task PurgeAsync(BotTenant t, CancellationToken ct)
    {
        try { await api.CallAsync(secrets.Unprotect(t.TokenEnc), "deleteWebhook", new { drop_pending_updates = true }, ct); }
        catch (Exception ex) when (ex is HttpRequestException or CryptographicException or TaskCanceledException) { }
        try
        {
            // ERP o'chirilgan / litsenziyasi tugagan bo'lishi mumkin — bot tomoni baribir tozalanadi.
            using var erpCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            erpCts.CancelAfter(TimeSpan.FromSeconds(10));   // panel so'rovi (30 s) ichida tugasin
            await erp.For(t).PostAsync<JsonElement>("Disconnect", new { }, null, erpCts.Token);
        }
        catch (Exception ex) when (ex is ErpException or HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            log.LogWarning("Bot {Slug}: ERP Telegram bog'lanishlari tozalanmadi ({Error})", t.Slug, ex.Message);
        }
        db.Links.RemoveRange(await db.Links.Where(x => x.TenantId == t.Id).ToListAsync(ct));
        db.Sessions.RemoveRange(await db.Sessions.Where(x => x.TenantId == t.Id).ToListAsync(ct));
        db.Inbox.RemoveRange(await db.Inbox.Where(x => x.TenantId == t.Id).ToListAsync(ct));
        db.Outbox.RemoveRange(await db.Outbox.Where(x => x.TenantId == t.Id).ToListAsync(ct));
        db.Tenants.Remove(t);
        await db.SaveChangesAsync(ct);
        registry.Invalidate();
    }

    /// <summary>secret_token: 1-256 belgi, A-Z a-z 0-9 _ -.</summary>
    internal static string NewSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// Panel API kaliti = Base64Url(HMAC-SHA256(MasterKey, "tahsin-admin-api")). Bot va sayt bir xil master kalitdan bir xil
    /// kalitni oladi; panelga faqat shu hosila beriladi (u bilan bot tokenlarini ochib bo'lmaydi).
    /// </summary>
    public static string DeriveAdminKey(string masterKey) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(masterKey), "tahsin-admin-api"u8))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
