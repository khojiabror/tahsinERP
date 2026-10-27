using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.ProvisioningAgent;

/// <summary>Panel /api/agent klienti: har so'rov HMAC bilan imzolanadi (AgentProtocol).</summary>
public sealed class PanelClient
{
    public static readonly string Version = typeof(PanelClient).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    private readonly HttpClient _http;
    private readonly int _serverId;
    private readonly byte[] _secret;
    private readonly TimeProvider _time;

    [ActivatorUtilitiesConstructor]
    public PanelClient(HttpClient http, AgentOptions options, TimeProvider time) : this(http, options, time, null) { }

    /// <param name="downloadHttp">Katta fayllar uchun (vaqt chegarasiz) klient; testlarda test server handler'i bilan.</param>
    public PanelClient(HttpClient http, AgentOptions options, TimeProvider time, HttpClient? downloadHttp)
    {
        _http = http;
        _capabilities = options.SiteDomains.Enabled ? [AgentCapabilities.SiteDomain, AgentCapabilities.RestoreArchive] : [AgentCapabilities.RestoreArchive];
        _time = time;
        if (string.IsNullOrWhiteSpace(options.Panel.Url)) throw new InvalidOperationException("Panel:Url sozlanmagan.");
        if (options.Panel.ServerId <= 0) throw new InvalidOperationException("Panel:ServerId sozlanmagan.");
        try { _secret = Convert.FromBase64String(options.Panel.Secret ?? ""); }
        catch (FormatException) { throw new InvalidOperationException("Panel:Secret Base64 emas."); }
        if (_secret.Length < 16) throw new InvalidOperationException("Panel:Secret sozlanmagan (env TAHSIN_AGENT_Panel__Secret).");
        _serverId = options.Panel.ServerId;
        _http.BaseAddress ??= new Uri(options.Panel.Url.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(10, options.Panel.TimeoutSeconds));
        _downloadHttp = downloadHttp ?? new HttpClient { BaseAddress = _http.BaseAddress, Timeout = Timeout.InfiniteTimeSpan };
    }

    private readonly HttpClient _downloadHttp;
    private readonly List<string> _capabilities;

    public sealed class PanelException(HttpStatusCode status, string body) : Exception($"Panel {(int)status}: {body}")
    {
        public HttpStatusCode Status { get; } = status;
    }

    private async Task<HttpResponseMessage> PostAsync(string path, object body, CancellationToken ct)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(body, AgentProtocol.Json);
        var req = new HttpRequestMessage(HttpMethod.Post, new Uri(_http.BaseAddress!, path)) { Content = new ByteArrayContent(bytes) };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        AgentProtocol.SignRequest(req, _serverId, _secret, bytes, _time.GetUtcNow(), Version);
        return await _http.SendAsync(req, ct);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage r, CancellationToken ct)
    {
        string text = await r.Content.ReadAsStringAsync(ct);
        if (!r.IsSuccessStatusCode) throw new PanelException(r.StatusCode, text.Length > 500 ? text[..500] : text);
        return JsonSerializer.Deserialize<T>(text, AgentProtocol.Json) ?? throw new PanelException(r.StatusCode, "bo'sh javob");
    }

    public async Task<JobLease?> LeaseAsync(CancellationToken ct)
    {
        using var r = await PostAsync("api/agent/lease", new LeaseRequest { Host = Environment.MachineName, Capabilities = _capabilities }, ct);
        if (r.StatusCode == HttpStatusCode.NoContent) return null;
        return await ReadAsync<JobLease>(r, ct);
    }

    public async Task ExtendAsync(long jobId, string leaseToken, CancellationToken ct)
    {
        using var r = await PostAsync($"api/agent/jobs/{jobId}/extend", new ExtendRequest { LeaseToken = leaseToken }, ct);
        await ReadAsync<JsonElement>(r, ct);
    }

    public async Task CompleteAsync(long jobId, JobCompletion completion, CancellationToken ct)
    {
        using var r = await PostAsync($"api/agent/jobs/{jobId}/complete", completion, ct);
        await ReadAsync<JsonElement>(r, ct);
    }

    public async Task<UsageTargetsResponse> UsageTargetsAsync(CancellationToken ct)
    {
        using var r = await PostAsync("api/agent/usage/targets", new { }, ct);
        return await ReadAsync<UsageTargetsResponse>(r, ct);
    }

    public async Task UsageAsync(UsageReport report, CancellationToken ct)
    {
        using var r = await PostAsync("api/agent/usage", report, ct);
        await ReadAsync<JsonElement>(r, ct);
    }

    // ---------------- Avto-yangilash (03_AUTO_UPDATE.md)

    public async Task<UpdateJobLease?> UpdateLeaseAsync(CancellationToken ct)
    {
        using var r = await PostAsync("api/agent/updates/lease", new LeaseRequest { Host = Environment.MachineName }, ct);
        if (r.StatusCode == HttpStatusCode.NoContent) return null;
        return await ReadAsync<UpdateJobLease>(r, ct);
    }

    public async Task UpdateProgressAsync(long jobId, UpdateProgress progress, CancellationToken ct)
    {
        using var r = await PostAsync($"api/agent/updates/jobs/{jobId}/progress", progress, ct);
        await ReadAsync<JsonElement>(r, ct);
    }

    public async Task UpdateCompleteAsync(long jobId, UpdateCompletion completion, CancellationToken ct)
    {
        using var r = await PostAsync($"api/agent/updates/jobs/{jobId}/complete", completion, ct);
        await ReadAsync<JsonElement>(r, ct);
    }

    public async Task<ReleaseManifestResponse> ReleaseManifestAsync(long jobId, CancellationToken ct)
    {
        using var r = await PostAsync($"api/agent/updates/jobs/{jobId}/manifest", new { }, ct);
        return await ReadAsync<ReleaseManifestResponse>(r, ct);
    }

    /// <summary>Job relizining artefaktini (panel GitHub'dan proxy qiladi) faylga yozadi.</summary>
    public async Task DownloadArtifactAsync(long jobId, string file, string destination, CancellationToken ct)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { file }, AgentProtocol.Json);
        var req = new HttpRequestMessage(HttpMethod.Post, new Uri(_http.BaseAddress!, $"api/agent/updates/jobs/{jobId}/artifact")) { Content = new ByteArrayContent(bytes) };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        AgentProtocol.SignRequest(req, _serverId, _secret, bytes, _time.GetUtcNow(), Version);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(30));
        // Katta fayl: HttpClient.Timeout emas, alohida muddat
        using var r = await _downloadHttp.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        if (!r.IsSuccessStatusCode) throw new PanelException(r.StatusCode, await r.Content.ReadAsStringAsync(ct));
        await using var src = await r.Content.ReadAsStreamAsync(cts.Token);
        await using var dst = File.Create(destination);
        await src.CopyToAsync(dst, cts.Token);
    }

    public async Task InventoryAsync(UpdateInventory inventory, CancellationToken ct)
    {
        using var r = await PostAsync("api/agent/updates/inventory", inventory, ct);
        await ReadAsync<JsonElement>(r, ct);
    }

    public async Task<ScheduleResponse> ScheduleAsync(ScheduleRequest request, CancellationToken ct)
    {
        using var r = await PostAsync("api/agent/schedule", request, ct);
        return await ReadAsync<ScheduleResponse>(r, ct);
    }
}
