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

    public PanelClient(HttpClient http, AgentOptions options, TimeProvider time)
    {
        _http = http;
        _time = time;
        if (string.IsNullOrWhiteSpace(options.Panel.Url)) throw new InvalidOperationException("Panel:Url sozlanmagan.");
        if (options.Panel.ServerId <= 0) throw new InvalidOperationException("Panel:ServerId sozlanmagan.");
        try { _secret = Convert.FromBase64String(options.Panel.Secret ?? ""); }
        catch (FormatException) { throw new InvalidOperationException("Panel:Secret Base64 emas."); }
        if (_secret.Length < 16) throw new InvalidOperationException("Panel:Secret sozlanmagan (env TAHSIN_AGENT_Panel__Secret).");
        _serverId = options.Panel.ServerId;
        _http.BaseAddress ??= new Uri(options.Panel.Url.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(10, options.Panel.TimeoutSeconds));
    }

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
        using var r = await PostAsync("api/agent/lease", new LeaseRequest { Host = Environment.MachineName }, ct);
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

    public async Task<ScheduleResponse> ScheduleAsync(ScheduleRequest request, CancellationToken ct)
    {
        using var r = await PostAsync("api/agent/schedule", request, ct);
        return await ReadAsync<ScheduleResponse>(r, ct);
    }
}
