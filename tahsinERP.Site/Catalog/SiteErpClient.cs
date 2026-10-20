using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using tahsinERP.Integration;
using tahsinERP.Site.Data;

namespace tahsinERP.Site.Catalog;

/// <summary>ERP xatosi (400 — biznes qoidasi, matni foydalanuvchiga; qolganlari — texnik).</summary>
public sealed class SiteErpException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
    public bool IsBusiness => Status == 400;
}

/// <summary>
/// ERP SiteApi (/Site/SiteApi/...) — DstApiSignature imzosi bilan (bot bilan bir xil master kalit, tenant kaliti ERP tenant slug'idan).
/// Vakil foydalanuvchi yo'q. Redirect'ga ergashilmaydi (login sahifasi — xato).
/// </summary>
public sealed class SiteErpClient(IHttpClientFactory httpFactory, IOptions<SiteOptions> options, TimeProvider clock)
{
    public const string HttpClientName = "erp";
    public const string ApiPath = "Site/SiteApi/";

    public sealed record SnapshotResponse(bool NotModified, string? ETag, string? Json);

    public async Task<SnapshotResponse> SnapshotAsync(SiteTenant tenant, string? etag, CancellationToken ct)
    {
        using var req = Create(tenant, HttpMethod.Get, "Snapshot", null);
        if (!string.IsNullOrEmpty(etag)) req.Headers.TryAddWithoutValidation("If-None-Match", etag);
        using var res = await Http().SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.NotModified) return new SnapshotResponse(true, etag, null);
        var body = await EnsureJson(res, ct);
        return new SnapshotResponse(false, res.Headers.ETag?.ToString(), body);
    }

    public async Task<(byte[] Bytes, string ContentType)> MediaAsync(SiteTenant tenant, int id, CancellationToken ct)
    {
        using var req = Create(tenant, HttpMethod.Get, "Media?id=" + id.ToString(CultureInfo.InvariantCulture), null);
        using var res = await Http().SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) throw new SiteErpException((int)res.StatusCode, "media " + id);
        return (await res.Content.ReadAsByteArrayAsync(ct), res.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
    }

    public async Task<int> LeadAsync(SiteTenant tenant, string payloadJson, CancellationToken ct)
    {
        var body = System.Text.Encoding.UTF8.GetBytes(payloadJson);
        using var req = Create(tenant, HttpMethod.Post, "Lead", body);
        using var res = await Http().SendAsync(req, ct);
        var json = await EnsureJson(res, ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetInt32();
    }

    private HttpClient Http() => httpFactory.CreateClient(HttpClientName);

    internal HttpRequestMessage Create(SiteTenant tenant, HttpMethod method, string action, byte[]? body)
    {
        var root = tenant.ErpBaseUrl.EndsWith('/') ? tenant.ErpBaseUrl : tenant.ErpBaseUrl + "/";
        var uri = new Uri(new Uri(root), ApiPath + action);
        var key = DstApiSignature.TenantKey(options.Value.MasterKey, string.IsNullOrWhiteSpace(tenant.ErpTenant) ? DstApiSignature.DefaultTenant : tenant.ErpTenant);
        var ts = clock.GetUtcNow().ToUnixTimeSeconds();
        var req = new HttpRequestMessage(method, uri);
        req.Headers.Add(DstApiSignature.TimestampHeader, ts.ToString(CultureInfo.InvariantCulture));
        req.Headers.Add(DstApiSignature.SignatureHeader, DstApiSignature.Sign(key, ts, method.Method, uri.PathAndQuery, null, body ?? []));
        req.Headers.Add("X-Requested-With", "XMLHttpRequest");
        if (body != null)
        {
            req.Content = new ByteArrayContent(body);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        }
        return req;
    }

    private static async Task<string> EnsureJson(HttpResponseMessage res, CancellationToken ct)
    {
        var text = await res.Content.ReadAsStringAsync(ct);
        bool json = res.Content.Headers.ContentType?.MediaType == "application/json";
        if (res.IsSuccessStatusCode && json) return text;
        string message = res.ReasonPhrase ?? res.StatusCode.ToString();
        if (json)
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("error", out var e)) message = e.GetString() ?? message;
            }
            catch (JsonException) { }
        }
        throw new SiteErpException(res.IsSuccessStatusCode ? 502 : (int)res.StatusCode, message);
    }
}
