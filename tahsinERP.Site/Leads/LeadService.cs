using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tahsinERP.Site.Catalog;
using tahsinERP.Site.Data;

namespace tahsinERP.Site.Leads;

public sealed record LeadItem(int ProductId, int? ProductUnitId, decimal Qty);

public sealed record LeadForm(string? Name, string? Phone, string? Company, string? Inn, string? Message, string? Items, string? Website, long? Ts, Guid? RequestId);

/// <summary>
/// Saytdan ariza: tekshiruv (ism, telefon, savatdagi tovarlar nusxada borligi), spam himoyasi (yashirin maydon,
/// forma 3 soniyadan tez yuborilmasin, IP va telefon bo'yicha chegara), keyin LeadOutbox → LeadWorker → ERP.
/// </summary>
public sealed class LeadService(SiteDbContext db, TimeProvider clock)
{
    public const int MaxItems = 100;
    private static readonly ConcurrentDictionary<string, Queue<DateTime>> Hits = new();

    public sealed record Result(bool Ok, string? ErrorKey, string? Phone);

    public async Task<Result> SubmitAsync(SiteTenant tenant, CatalogIndex index, LeadForm form, string lang, string host, string ip, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (!index.Settings.LeadsEnabled) return new Result(false, "LeadsDisabled", null);
        // bot: yashirin maydon to'ldirilgan yoki forma juda tez — "muvaffaqiyat" ko'rsatiladi, lekin hech narsa yozilmaydi
        if (!string.IsNullOrEmpty(form.Website)) return new Result(true, null, null);
        if (form.Ts is long ts && now - DateTimeOffset.FromUnixTimeMilliseconds(ts).UtcDateTime < TimeSpan.FromSeconds(3)) return new Result(true, null, null);

        var name = Clean(form.Name, 100);
        if (name == null || name.Length < 2) return new Result(false, "Err_Name", null);
        var phone = NormalizePhone(form.Phone);
        if (phone == null) return new Result(false, "Err_Phone", null);
        if (!Allow("ip:" + tenant.Slug + ":" + ip, 5, now) || !Allow("phone:" + tenant.Slug + ":" + phone, 3, now))
            return new Result(false, "Err_TooMany", null);

        List<LeadItem> items;
        try
        {
            items = ParseItems(form.Items, index);
        }
        catch (FormatException)
        {
            return new Result(false, "Err_Cart", null);
        }

        var requestId = form.RequestId is Guid g && g != Guid.Empty ? g : Guid.NewGuid();
        if (await db.LeadOutbox.AnyAsync(l => l.ClientRequestId == requestId, ct)) return new Result(true, null, phone);   // ikki marta bosildi
        var payload = JsonSerializer.Serialize(new
        {
            clientRequestId = requestId, name, phone, companyName = Clean(form.Company, 200), inn = Clean(form.Inn, 20),
            message = Clean(form.Message, 1000), language = lang, host,
            items = items.Select(i => new { productId = i.ProductId, productUnitId = i.ProductUnitId, qty = i.Qty })
        });
        db.LeadOutbox.Add(new LeadOutboxItem { TenantId = tenant.Id, ClientRequestId = requestId, Payload = payload, CreatedAt = now, NextAt = now });
        await db.SaveChangesAsync(ct);
        return new Result(true, null, phone);
    }

    /// <summary>Savat JSON: [{id, unit, qty}] — faqat nusxadagi tovar va birliklar; noto'g'ri bo'lsa FormatException.</summary>
    internal static List<LeadItem> ParseItems(string? json, CatalogIndex index)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        List<JsonElement>? raw;
        try { raw = JsonSerializer.Deserialize<List<JsonElement>>(json); }
        catch (JsonException) { throw new FormatException(); }
        if (raw == null || raw.Count > MaxItems) throw new FormatException();
        var result = new List<LeadItem>();
        foreach (var e in raw)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("id", out var idEl) || !idEl.TryGetInt32(out var id)) throw new FormatException();
            int? unit = e.TryGetProperty("unit", out var u) && u.ValueKind == JsonValueKind.Number ? u.GetInt32() : null;
            if (!e.TryGetProperty("qty", out var q) || !q.TryGetDecimal(out var qty) || qty <= 0 || qty > 1_000_000) throw new FormatException();
            var p = index.ProductById(id) ?? throw new FormatException();
            if (!(p.Units ?? []).Any(x => x.Id == unit)) throw new FormatException();
            result.Add(new LeadItem(id, unit, Math.Round(qty, 3)));
        }
        return result;
    }

    /// <summary>"+998 90 123-45-67", "90 1234567" → "+998901234567". O'zbekiston raqamlari: 9 raqam yoki 998 + 9.</summary>
    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 9) digits = "998" + digits;
        return digits.Length is >= 11 and <= 15 ? "+" + digits : null;
    }

    private static bool Allow(string key, int perTenMinutes, DateTime now)
    {
        var q = Hits.GetOrAdd(key, _ => new Queue<DateTime>());
        lock (q)
        {
            while (q.Count > 0 && now - q.Peek() > TimeSpan.FromMinutes(10)) q.Dequeue();
            if (q.Count >= perTenMinutes) return false;
            q.Enqueue(now);
            return true;
        }
    }

    internal static void ResetLimits() => Hits.Clear();

    private static string? Clean(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length <= max ? s : s[..max];
    }
}

/// <summary>LeadOutbox → ERP SiteApi/Lead. Tarmoq/5xx — qayta urinish (eksponensial), 400 — doimiy xato (logga).</summary>
public sealed class LeadWorker(IServiceScopeFactory scopes, CatalogStore store, SiteErpClient erp, TimeProvider clock, ILogger<LeadWorker> log) : BackgroundService
{
    internal const int MaxAttempts = 12;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), clock);
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await RoundAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogWarning(ex, "Arizalar navbati");
            }
        }
    }

    internal async Task RoundAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SiteDbContext>();
        var now = clock.GetUtcNow().UtcDateTime;
        var items = await db.LeadOutbox.Where(l => l.SentAt == null && l.FailedAt == null && l.NextAt <= now).OrderBy(l => l.Id).Take(20).ToListAsync(ct);
        foreach (var item in items)
        {
            var tenant = store.Tenants.FirstOrDefault(t => t.Id == item.TenantId);
            if (tenant == null) continue;
            try
            {
                item.ErpLeadId = await erp.LeadAsync(tenant, item.Payload, ct);
                item.SentAt = clock.GetUtcNow().UtcDateTime;
                item.Error = null;
            }
            catch (SiteErpException ex) when (ex.IsBusiness)
            {
                item.FailedAt = clock.GetUtcNow().UtcDateTime;
                item.Error = Trim(ex.Message);
                log.LogWarning("Ariza {Id} ({Tenant}) ERP rad etdi: {Error}", item.Id, tenant.Slug, ex.Message);
            }
            catch (Exception ex) when (ex is SiteErpException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                item.Attempts++;
                item.Error = Trim(ex.Message);
                if (item.Attempts >= MaxAttempts) item.FailedAt = clock.GetUtcNow().UtcDateTime;
                else item.NextAt = clock.GetUtcNow().UtcDateTime.AddSeconds(Math.Min(3600, 15 * Math.Pow(2, item.Attempts)));
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static string Trim(string s) => s.Length <= 1000 ? s : s[..1000];
}
