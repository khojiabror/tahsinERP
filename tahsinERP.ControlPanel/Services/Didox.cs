using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;

namespace tahsinERP.ControlPanel.Services;

// Didox e-hisob-faktura (00_PLAN.md 10, 4-bosqich; 08_STAGE4.md 3).
// MUHIM: haqiqiy Didox hisobi/partner tokeni bilan TEKSHIRILMAGAN. Endpoint va maydon nomlari ochiq manbalardan
// (Didox partner API, didox npm SDK v1.0.6) olingan — birinchi ulashda stage (stage.goodsign.biz) muhitida sinab ko'ring.

public class DidoxOptions
{
    /// <summary>Partner API manzili. Ishlab chiqarish: https://api-partners.didox.uz, sinov: https://stage.goodsign.biz.</summary>
    public string BaseUrl { get; set; } = "https://api-partners.didox.uz";
    /// <summary>Didox bergan partner tokeni (Partner-Authorization sarlavhasi). Faqat env/user-secrets. Bo'sh — Didox o'chiq.</summary>
    public string? PartnerToken { get; set; }
    /// <summary>Sotuvchi (bizning MChJ) STIR — Didox'ga kirish logini. Bo'sh bo'lsa Vendor:Inn.</summary>
    public string? Inn { get; set; }
    /// <summary>Didox kabinet paroli (yuridik shaxs, /v1/auth/{stir}/password). Faqat env/user-secrets.</summary>
    public string? Password { get; set; }
    public string Locale { get; set; } = "ru";
    /// <summary>MXIK (IKPU) kodi — dasturiy ta'minot xizmati (tasnif.soliq.uz). Bo'sh — Didox o'chiq.</summary>
    public string? CatalogCode { get; set; }
    public string? CatalogName { get; set; }
    /// <summary>MXIK bo'yicha o'lchov birligi (qadoq) kodi va nomi.</summary>
    public string? PackageCode { get; set; }
    public string? PackageName { get; set; }
    /// <summary>Shartnoma raqami (ommaviy oferta). Bo'sh — "Oferta".</summary>
    public string? ContractNo { get; set; }
    /// <summary>Shartnoma sanasi (yyyy-MM-dd). Bo'sh — tenant yaratilgan sana.</summary>
    public string? ContractDate { get; set; }
    /// <summary>Yuborilgan hujjatlar holatini tekshirish oralig'i, daqiqa (fon ishi).</summary>
    public int RefreshMinutes { get; set; } = 60;

    /// <summary>Sotuvchi STIR: Didox:Inn, bo'sh bo'lsa Vendor:Inn.</summary>
    public string SellerInn(VendorOptions vendor) => (string.IsNullOrWhiteSpace(Inn) ? vendor.Inn : Inn).Trim();

    public bool IsConfigured(VendorOptions vendor) =>
        !string.IsNullOrWhiteSpace(PartnerToken) && !string.IsNullOrWhiteSpace(Password)
        && SellerInn(vendor).Length > 0 && !string.IsNullOrWhiteSpace(CatalogCode) && !string.IsNullOrWhiteSpace(PackageCode);

    /// <summary>Sozlanmagan bo'lsa — nima yetishmaydi (UI izohi uchun).</summary>
    public IReadOnlyList<string> Missing(VendorOptions vendor)
    {
        var m = new List<string>();
        if (string.IsNullOrWhiteSpace(PartnerToken)) m.Add("Didox:PartnerToken");
        if (string.IsNullOrWhiteSpace(Password)) m.Add("Didox:Password");
        if (SellerInn(vendor).Length == 0) m.Add("Didox:Inn (yoki Vendor:Inn)");
        if (string.IsNullOrWhiteSpace(CatalogCode)) m.Add("Didox:CatalogCode (MXIK)");
        if (string.IsNullOrWhiteSpace(PackageCode)) m.Add("Didox:PackageCode");
        return m;
    }
}

/// <summary>Didox hujjat holati: kod (0 qoralama, 1 yuborilgan, 2 imzolangan, 3 rad, 4 bekor, ...) va nomi.</summary>
public sealed record DidoxDocumentState(int? Code, string Label)
{
    public bool IsFinal => Code is 2 or 3 or 4 or 9;
    public override string ToString() => Code is null ? Label : $"{Code} {Label}";

    public static string LabelOf(int? code) => code switch
    {
        0 => "Qoralama (Didox'da imzolang)",
        1 => "Yuborilgan (xaridor imzosi kutilmoqda)",
        2 => "Imzolangan",
        3 => "Rad etilgan",
        4 => "Bekor qilingan",
        5 => "Xato",
        6 => "Qisman imzolangan",
        7 => "Imzo kutilmoqda",
        8 => "Ishlov berilmoqda",
        9 => "Muddati o'tgan",
        _ => "Noma'lum",
    };
}

public sealed class DidoxException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Didox partner API. Sozlanmagan bo'lsa IsConfigured = false va panel tugmani ko'rsatmaydi.</summary>
public interface IDidoxClient
{
    bool IsConfigured { get; }
    /// <summary>Hisob-faktura (hujjat turi 002) qoralamasini yaratadi; Didox hujjat ID'sini qaytaradi.</summary>
    Task<string> CreateInvoiceDraftAsync(JsonObject payload, CancellationToken ct = default);
    Task<DidoxDocumentState> GetDocumentAsync(string documentId, CancellationToken ct = default);
}

/// <summary>Token keshi (singleton): /v1/auth javobidagi token "user-key" sarlavhasida yuboriladi; 401 bo'lsa qayta kiriladi.</summary>
public sealed class DidoxTokenCache
{
    public string? Token;
    public DateTime ExpiresUtc;
    public readonly SemaphoreSlim Lock = new(1, 1);
}

public sealed class DidoxHttpClient(HttpClient http, IOptions<DidoxOptions> options, IOptions<VendorOptions> vendor, DidoxTokenCache cache,
    TimeProvider time) : IDidoxClient
{
    // Token muddati hujjatlashtirilmagan — ehtiyot uchun 6 soatda yangilanadi.
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(6);
    private DidoxOptions O => options.Value;

    public bool IsConfigured => O.IsConfigured(vendor.Value);

    private Uri Url(string path) => new(new Uri(O.BaseUrl.TrimEnd('/') + "/"), path.TrimStart('/'));

    private async Task<string> TokenAsync(bool force, CancellationToken ct)
    {
        DateTime now = time.GetUtcNow().UtcDateTime;
        if (!force && cache.Token is not null && cache.ExpiresUtc > now) return cache.Token;
        await cache.Lock.WaitAsync(ct);
        try
        {
            if (!force && cache.Token is not null && cache.ExpiresUtc > now) return cache.Token;
            string inn = O.SellerInn(vendor.Value);
            using var req = new HttpRequestMessage(HttpMethod.Post, Url($"v1/auth/{Uri.EscapeDataString(inn)}/password/{O.Locale}"))
            {
                Content = JsonContent.Create(new { password = O.Password }),
            };
            req.Headers.Add("Partner-Authorization", O.PartnerToken);
            using var resp = await SendAsync(req, ct);
            var json = await ReadAsync(resp, ct);
            if (!resp.IsSuccessStatusCode) throw new DidoxException($"Didox'ga kirib bo'lmadi ({(int)resp.StatusCode}): {Message(json)}");
            string token = Str(json, "token") ?? Str(json?["data"], "token") ?? throw new DidoxException("Didox kirish javobida token yo'q.");
            cache.Token = token;
            cache.ExpiresUtc = now.Add(TokenLifetime);
            return token;
        }
        finally { cache.Lock.Release(); }
    }

    private async Task<(HttpResponseMessage Resp, JsonNode? Json)> AuthorizedAsync(Func<HttpRequestMessage> build, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            var req = build();
            req.Headers.Add("Partner-Authorization", O.PartnerToken);
            req.Headers.Add("user-key", await TokenAsync(attempt > 0, ct));
            var resp = await SendAsync(req, ct);
            if (resp.StatusCode == HttpStatusCode.Unauthorized && attempt == 0) { resp.Dispose(); continue; }
            return (resp, await ReadAsync(resp, ct));
        }
    }

    public async Task<string> CreateInvoiceDraftAsync(JsonObject payload, CancellationToken ct = default)
    {
        var (resp, json) = await AuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Post, Url("v1/documents/002/create"))
        {
            Content = new StringContent(payload.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        }, ct);
        using (resp)
        {
            if (!resp.IsSuccessStatusCode) throw new DidoxException($"Didox hujjatni qabul qilmadi ({(int)resp.StatusCode}): {Message(json)}");
            return Str(json, "_id") ?? Str(json, "doc_id") ?? Str(json, "id") ?? Str(json?["data"], "_id") ?? Str(json?["data"], "doc_id")
                ?? throw new DidoxException("Didox javobida hujjat ID'si yo'q: " + Cut(json?.ToJsonString()));
        }
    }

    public async Task<DidoxDocumentState> GetDocumentAsync(string documentId, CancellationToken ct = default)
    {
        var (resp, json) = await AuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Get, Url("v1/documents/" + Uri.EscapeDataString(documentId) + "?owner=1")), ct);
        using (resp)
        {
            if (!resp.IsSuccessStatusCode) throw new DidoxException($"Didox hujjat holati olinmadi ({(int)resp.StatusCode}): {Message(json)}");
            // Javob tuzilishi turiga qarab farq qiladi — holat kodi bir nechta joydan qidiriladi.
            int? code = Int(json, "doc_status") ?? Int(json, "status") ?? Int(json?["data"], "doc_status") ?? Int(json?["data"], "status")
                        ?? Int(json?["document"], "doc_status") ?? Int(json?["data"]?["document"], "doc_status");
            return new DidoxDocumentState(code, DidoxDocumentState.LabelOf(code));
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        try { return await http.SendAsync(req, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new DidoxException("Didox'ga ulanib bo'lmadi: " + ex.Message, ex);
        }
    }

    private static async Task<JsonNode?> ReadAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        string text = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(text)) return null;
        try { return JsonNode.Parse(text); }
        catch (JsonException) { return JsonValue.Create(Cut(text)); }
    }

    private static string? Str(JsonNode? n, string key) =>
        n is JsonObject o && o.TryGetPropertyValue(key, out var v) && v is JsonValue jv ? jv.ToString() is { Length: > 0 } s ? s : null : null;

    private static int? Int(JsonNode? n, string key)
    {
        string? s = Str(n, key);
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : null;
    }

    private static string Message(JsonNode? json) =>
        Str(json, "message") ?? Str(json, "error") ?? Cut(json?.ToJsonString()) ?? "javob bo'sh";

    private static string? Cut(string? s) => s is null ? null : s.Length > 300 ? s[..300] : s;
}

/// <summary>Hisob-fakturani Didox'ga yuborish va holatini yangilash. Har amal AuditLog'da.</summary>
public sealed class DidoxService(ControlDbContext db, IDidoxClient client, AuditLogger audit, Clock clock, IOptions<DidoxOptions> options,
    IOptions<VendorOptions> vendor, ILogger<DidoxService> log)
{
    public bool Enabled => client.IsConfigured;
    public IReadOnlyList<string> Missing => options.Value.Missing(vendor.Value);

    public static bool CanSend(Invoice i) =>
        i.Number is not null && i.DidoxId is null && i.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid or InvoiceStatus.Paid;

    private static decimal Som(long tiyin) => tiyin / 100m;

    /// <summary>Didox 002 (hisob-faktura) tanasi. Narxlar bizda QQS bilan — Didox'ga QQSsiz summa + QQS alohida.</summary>
    public static JsonObject BuildPayload(Invoice invoice, Tenant tenant, VendorOptions v, DidoxOptions o)
    {
        string sellerTin = o.SellerInn(v);
        var products = new JsonArray();
        foreach (var l in invoice.Lines.OrderBy(l => l.LineNo))
        {
            long net = l.AmountTiyin - l.VatTiyin;
            products.Add(new JsonObject
            {
                ["OrdNo"] = l.LineNo,
                ["Name"] = l.Description,
                ["CatalogCode"] = o.CatalogCode,
                ["CatalogName"] = o.CatalogName ?? l.Description,
                ["PackageCode"] = o.PackageCode,
                ["PackageName"] = o.PackageName,
                ["Count"] = l.Quantity,
                // Soliq formati: Summa — birlik narxi (QQSsiz), DeliverySum — jami QQSsiz.
                ["Summa"] = Math.Round(Som(net) / (l.Quantity == 0 ? 1 : l.Quantity), 2),
                ["DeliverySum"] = Som(net),
                ["VatRate"] = l.VatRatePercent,
                ["VatSum"] = Som(l.VatTiyin),
                ["DeliverySumWithVat"] = Som(l.AmountTiyin),
                ["WithoutVat"] = l.VatRatePercent == 0,
            });
        }
        return new JsonObject
        {
            ["Version"] = 1,
            ["FacturaType"] = 0,
            ["FacturaDoc"] = new JsonObject { ["FacturaNo"] = invoice.Number, ["FacturaDate"] = invoice.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
            ["ContractDoc"] = new JsonObject
            {
                ["ContractNo"] = string.IsNullOrWhiteSpace(o.ContractNo) ? "Oferta" : o.ContractNo,
                ["ContractDate"] = string.IsNullOrWhiteSpace(o.ContractDate) ? DateOnly.FromDateTime(tenant.CreatedAtUtc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : o.ContractDate,
            },
            ["SellerTin"] = sellerTin,
            ["Seller"] = new JsonObject
            {
                ["Name"] = v.LegalName, ["VatRegCode"] = v.VatCode, ["Account"] = v.BankAccount, ["BankId"] = v.Mfo, ["Address"] = v.Address,
                ["Director"] = v.Director, ["Accountant"] = v.Accountant,
            },
            ["BuyerTin"] = tenant.Inn,
            ["Buyer"] = new JsonObject { ["Name"] = tenant.LegalName, ["Address"] = tenant.Address ?? "" },
            ["ProductList"] = new JsonObject
            {
                ["Tin"] = sellerTin, ["HasVat"] = invoice.VatTiyin > 0, ["HasExcise"] = false, ["HasLgota"] = false, ["HasCommittent"] = false,
                ["Products"] = products,
            },
        };
    }

    public async Task<Invoice> SendAsync(int invoiceId, int staffId, CancellationToken ct = default)
    {
        if (!Enabled) throw new DomainException("Didox sozlanmagan: " + string.Join(", ", Missing) + ".");
        var inv = await db.Invoices.Include(i => i.Lines).Include(i => i.Tenant).FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new DomainException("Hisob-faktura topilmadi.");
        if (inv.DidoxId is not null) throw new DomainException($"Hisob-faktura Didox'ga allaqachon yuborilgan (ID {inv.DidoxId}).");
        if (!CanSend(inv)) throw new DomainException("Faqat chiqarilgan (raqamli) va bekor qilinmagan hisob-faktura yuboriladi.");
        if (TenantService.ValidateInn(inv.Tenant!.Inn) is not null || inv.Tenant.Inn is null)
            throw new DomainException("Tashkilot STIR'i (9 yoki 14 raqam) kiritilmagan — Didox xaridorni STIR bo'yicha topadi.");
        var payload = BuildPayload(inv, inv.Tenant, vendor.Value, options.Value);
        try
        {
            string id = await client.CreateInvoiceDraftAsync(payload, ct);
            inv.DidoxId = id.Length > 100 ? id[..100] : id;
            inv.DidoxSentAtUtc = clock.UtcNow;
            inv.DidoxStatus = new DidoxDocumentState(0, DidoxDocumentState.LabelOf(0)).ToString();
            inv.DidoxError = null;
            audit.Add("didox.send", "Invoice", inv.Id, after: new { inv.Number, inv.DidoxId }, staffIdOverride: staffId);
            await db.SaveChangesAsync(ct);
            return inv;
        }
        catch (DidoxException ex)
        {
            inv.DidoxError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            audit.Add("didox.send-failed", "Invoice", inv.Id, after: new { inv.Number, Error = inv.DidoxError }, staffIdOverride: staffId);
            await db.SaveChangesAsync(ct);
            log.LogWarning(ex, "Didox'ga yuborilmadi ({Number})", inv.Number);
            throw new DomainException(ex.Message);
        }
    }

    /// <summary>Bitta hisob-faktura holatini Didox'dan yangilaydi. Xato — DidoxError'ga yoziladi (istisno chiqmaydi).</summary>
    public async Task<DidoxDocumentState?> RefreshAsync(Invoice inv, CancellationToken ct = default)
    {
        if (!Enabled || inv.DidoxId is null) return null;
        try
        {
            var state = await client.GetDocumentAsync(inv.DidoxId, ct);
            string text = state.ToString();
            if (text != inv.DidoxStatus)
                audit.Add("didox.status", "Invoice", inv.Id, new { Status = inv.DidoxStatus }, new { Status = text });
            inv.DidoxStatus = text.Length > 100 ? text[..100] : text;
            inv.DidoxError = null;
            inv.DidoxCheckedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            return state;
        }
        catch (DidoxException ex)
        {
            inv.DidoxError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            inv.DidoxCheckedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            log.LogWarning(ex, "Didox holati olinmadi ({Number})", inv.Number);
            return null;
        }
    }

    /// <summary>Yakuniy bo'lmagan (imzolanmagan/kutilayotgan) hujjatlar, oxirgi tekshiruvdan RefreshMinutes o'tganlari. Qaytaradi: tekshirilganlar soni.</summary>
    public async Task<int> RefreshPendingAsync(int max = 50, CancellationToken ct = default)
    {
        if (!Enabled) return 0;
        DateTime before = clock.UtcNow.AddMinutes(-Math.Max(5, options.Value.RefreshMinutes));
        var final = new[] { "2 ", "3 ", "4 ", "9 " };
        var list = await db.Invoices.Where(i => i.DidoxId != null && (i.DidoxCheckedAtUtc == null || i.DidoxCheckedAtUtc < before))
            .OrderBy(i => i.DidoxCheckedAtUtc).Take(max * 2).ToListAsync(ct);
        int n = 0;
        foreach (var inv in list.Where(i => i.DidoxStatus is null || !final.Any(f => i.DidoxStatus.StartsWith(f, StringComparison.Ordinal))).Take(max))
        {
            await RefreshAsync(inv, ct);
            n++;
        }
        return n;
    }
}

/// <summary>Didox holatlarini davriy yangilash (faqat sozlangan bo'lsa ishlaydi).</summary>
public sealed class DidoxRefreshHostedService(IServiceScopeFactory scopes, TimeProvider time, IOptions<DidoxOptions> options,
    ILogger<DidoxRefreshHostedService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var period = TimeSpan.FromMinutes(Math.Max(5, options.Value.RefreshMinutes));
        try { await Task.Delay(TimeSpan.FromMinutes(1), time, stoppingToken); } catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<DidoxService>();
                if (svc.Enabled)
                {
                    int n = await svc.RefreshPendingAsync(ct: stoppingToken);
                    if (n > 0) log.LogInformation("Didox: {Count} hujjat holati tekshirildi", n);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Didox holat yangilash xatosi"); }
            try { await Task.Delay(period, time, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
