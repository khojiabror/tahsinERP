using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using tahsinERP.Integration;

namespace tahsinERP.Bot.Erp;

/// <summary>
/// Bitta tenant ERP'si uchun imzolangan mijoz (DstApiSignature). X-Acting-User — ERP USERS.ID (agent/rahbar nomidan);
/// tizim chaqiruvlari (bog'lash, bildirishnomalar) — usiz. X-Requested-With: litsenziya filtri redirect o'rniga 403 qaytaradi.
/// </summary>
public sealed class ErpClient(HttpClient http, Uri baseUrl, byte[] tenantKey, TimeProvider clock)
{
    public const string ApiPath = "Distribution/DstApi/";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Uri BuildUri(string action, IEnumerable<KeyValuePair<string, string?>>? query = null)
    {
        var sb = new StringBuilder(ApiPath).Append(action);
        var first = true;
        foreach (var (k, v) in query ?? [])
        {
            if (v == null) continue;
            sb.Append(first ? '?' : '&').Append(Uri.EscapeDataString(k)).Append('=').Append(Uri.EscapeDataString(v));
            first = false;
        }
        var root = baseUrl.AbsoluteUri.EndsWith('/') ? baseUrl : new Uri(baseUrl.AbsoluteUri + "/");
        return new Uri(root, sb.ToString());
    }

    public Task<T?> GetAsync<T>(string action, int? actingUser, CancellationToken ct, params (string Key, object? Value)[] query) =>
        SendAsync<T>(HttpMethod.Get, BuildUri(action, Q(query)), actingUser, null, ct);

    public Task<T?> PostAsync<T>(string action, object body, int? actingUser, CancellationToken ct) =>
        SendAsync<T>(HttpMethod.Post, BuildUri(action), actingUser, JsonSerializer.SerializeToUtf8Bytes(body, Json), ct);

    internal HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, int? actingUser, byte[]? body)
    {
        var req = new HttpRequestMessage(method, uri);
        var acting = actingUser?.ToString(CultureInfo.InvariantCulture);
        var ts = clock.GetUtcNow().ToUnixTimeSeconds();
        req.Headers.Add(DstApiSignature.TimestampHeader, ts.ToString(CultureInfo.InvariantCulture));
        req.Headers.Add(DstApiSignature.SignatureHeader, DstApiSignature.Sign(tenantKey, ts, method.Method, uri.PathAndQuery, acting, body ?? []));
        if (acting != null) req.Headers.Add(DstApiSignature.ActingUserHeader, acting);
        req.Headers.Add("X-Requested-With", "XMLHttpRequest");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (body != null)
        {
            req.Content = new ByteArrayContent(body);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        }
        return req;
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, Uri uri, int? actingUser, byte[]? body, CancellationToken ct)
    {
        using var req = CreateRequest(method, uri, actingUser, body);
        using var res = await http.SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (res.IsSuccessStatusCode && res.Content.Headers.ContentType?.MediaType == "application/json")
            return string.IsNullOrWhiteSpace(text) || text == "null" ? default : JsonSerializer.Deserialize<T>(text, Json);
        if (res.IsSuccessStatusCode || (int)res.StatusCode is >= 300 and < 400)   // HTML (login/litsenziya sahifasi) — API emas
            throw new ErpException(502, "ERP JSON qaytarmadi: " + (int)res.StatusCode + " " + res.Content.Headers.ContentType?.MediaType);

        string message = res.ReasonPhrase ?? res.StatusCode.ToString();
        List<string>? problems = null;
        if (res.Content.Headers.ContentType?.MediaType == "application/json" && !string.IsNullOrWhiteSpace(text))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String) message = e.GetString()!;
                if (doc.RootElement.TryGetProperty("problems", out var p) && p.ValueKind == JsonValueKind.Array)
                    problems = p.EnumerateArray().Select(x => x.GetString() ?? "").ToList();
            }
            catch (JsonException) { }
        }
        throw new ErpException((int)res.StatusCode, message, problems);
    }

    private static IEnumerable<KeyValuePair<string, string?>> Q((string Key, object? Value)[] query) =>
        query.Select(q => new KeyValuePair<string, string?>(q.Key, q.Value switch
        {
            null => null,
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            var o => o.ToString()
        }));
}

/// <summary>Tenant bo'yicha ErpClient (kalit ERP tenant slug'idan hosil qilinadi, master — Bot:MasterKey).</summary>
public sealed class ErpClientFactory(IHttpClientFactory httpFactory, Microsoft.Extensions.Options.IOptions<BotOptions> options, TimeProvider clock)
{
    public const string HttpClientName = "erp";

    public ErpClient For(Data.BotTenant tenant) =>
        new(httpFactory.CreateClient(HttpClientName), new Uri(tenant.ErpBaseUrl),
            DstApiSignature.TenantKey(options.Value.MasterKey, string.IsNullOrWhiteSpace(tenant.ErpTenant) ? DstApiSignature.DefaultTenant : tenant.ErpTenant), clock);
}
