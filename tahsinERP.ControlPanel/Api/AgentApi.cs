using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Api;

/// <summary>
/// Provisioning agenti API (05_PROVISIONING_AGENT.md). Autentifikatsiya: <c>Authorization: Bearer AG-...</c> — server tokeni
/// (panelda faqat SHA-256 xeshi). Faqat HTTPS orqali. Agent faqat O'Z serverining ishlari va reestrini ko'radi.
/// </summary>
public static class AgentApi
{
    public const string VersionHeader = "X-Agent-Version";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public sealed class CompleteRequest
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("exit_code")] public int ExitCode { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
        [JsonPropertyName("result")] public JsonObject? Result { get; set; }
        [JsonPropertyName("log")] public string? Log { get; set; }
        [JsonPropertyName("temp_password")] public string? TempPassword { get; set; }
    }

    public static void Map(WebApplication app)
    {
        var g = app.MapGroup("/api/agent").RequireRateLimiting(LicenseApi.RateLimitPolicy).DisableAntiforgery().AllowAnonymous();
        g.MapGet("/registry", RegistryAsync);
        g.MapPost("/jobs/lease", LeaseAsync);
        g.MapPost("/jobs/{id:int}/complete", CompleteAsync);
    }

    private static async Task<Server?> AuthAsync(HttpRequest http, ProvisioningService svc, CancellationToken ct)
    {
        string? auth = http.Headers.Authorization.FirstOrDefault();
        string? token = auth is not null && auth.StartsWith("Bearer ", StringComparison.Ordinal) ? auth[7..].Trim() : null;
        return await svc.AuthenticateAgentAsync(token, http.Headers[VersionHeader].FirstOrDefault(), ct);
    }

    private static IResult Unauthorized() => Results.Json(new { error = "unauthorized" }, statusCode: 401);

    internal static async Task<IResult> RegistryAsync(HttpRequest http, ProvisioningService svc, CancellationToken ct)
    {
        var server = await AuthAsync(http, svc, ct);
        if (server is null) return Unauthorized();
        var (json, etag) = await svc.BuildRegistryAsync(server, ct);
        http.HttpContext.Response.Headers.ETag = "\"" + etag + "\"";
        string? inm = http.Headers.IfNoneMatch.FirstOrDefault()?.Trim('"');
        if (inm == etag) return Results.StatusCode(304);
        return Results.Text(json, "application/json; charset=utf-8");
    }

    internal static async Task<IResult> LeaseAsync(HttpRequest http, ProvisioningService svc, CancellationToken ct)
    {
        var server = await AuthAsync(http, svc, ct);
        if (server is null) return Unauthorized();
        var job = await svc.LeaseAsync(server, ct);
        return job is null ? Results.NoContent() : Results.Json(job, Json);
    }

    internal static async Task<IResult> CompleteAsync(int id, HttpRequest http, ProvisioningService svc, CancellationToken ct)
    {
        var server = await AuthAsync(http, svc, ct);
        if (server is null) return Unauthorized();
        CompleteRequest? body;
        try { body = await http.ReadFromJsonAsync<CompleteRequest>(ct); }
        catch (JsonException) { return Results.Json(new { error = "bad_request" }, statusCode: 400); }
        if (body is null) return Results.Json(new { error = "bad_request" }, statusCode: 400);
        try
        {
            var job = await svc.CompleteAsync(server, id, new AgentReport(body.Ok, body.ExitCode, body.Error, body.Result, body.Log, body.TempPassword), ct);
            return Results.Json(new { status = job.Status.ToString() }, Json);
        }
        catch (DomainException ex)
        {
            return Results.Json(new { error = "conflict", message = ex.Message }, statusCode: 409);
        }
    }
}
