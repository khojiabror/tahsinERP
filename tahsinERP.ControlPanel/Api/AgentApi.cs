using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.ControlPanel.Api;

/// <summary>
/// Provisioning agenti API (05_PROVISIONING.md, 3-bo'lim). Har so'rov server siri bilan HMAC imzolangan
/// (<see cref="AgentProtocol"/>), ±300 s, nonce bir marta (ApiNonces, kalit "agent:&lt;serverId&gt;").
/// Panel SQL serverga ulanmaydi — agent o'zi keladi.
/// </summary>
public static class AgentApi
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/agent").RequireRateLimiting(LicenseApi.RateLimitPolicy).DisableAntiforgery().AllowAnonymous();
        group.MapPost("/lease", LeaseAsync);
        group.MapPost("/jobs/{id:long}/extend", ExtendAsync);
        group.MapPost("/jobs/{id:long}/complete", CompleteAsync);
        group.MapPost("/schedule", ScheduleAsync);
        group.MapPost("/usage/targets", UsageTargetsAsync);
        group.MapPost("/usage", UsageAsync);
        // Avto-yangilash (Docs/Deploy/03_AUTO_UPDATE.md)
        group.MapPost("/updates/lease", UpdateLeaseAsync);
        group.MapPost("/updates/jobs/{id:long}/progress", UpdateProgressAsync);
        group.MapPost("/updates/jobs/{id:long}/complete", UpdateCompleteAsync);
        group.MapPost("/updates/jobs/{id:long}/manifest", UpdateManifestAsync);
        group.MapPost("/updates/jobs/{id:long}/artifact", UpdateArtifactAsync);
        group.MapPost("/updates/inventory", UpdateInventoryAsync);
    }

    private static IResult Error(int status, string error, string? message = null) =>
        Results.Json(message is null ? new Dictionary<string, string> { ["error"] = error }
            : new Dictionary<string, string> { ["error"] = error, ["message"] = message }, statusCode: status);

    private sealed record Authed(Server Server, byte[] Body);

    /// <summary>Imzoni tekshiradi; muvaffaqiyatda server va xom tana qaytadi, aks holda xato javobi.</summary>
    private static async Task<(Authed? ok, IResult? error)> AuthenticateAsync(HttpRequest http, ControlDbContext db, ISecretProtector protector,
        Clock clock, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await http.Body.CopyToAsync(ms, ct);
        byte[] body = ms.ToArray();
        if (body.Length > 512 * 1024) return (null, Error(413, "too_large"));

        string? sid = http.Headers[AgentProtocol.HeaderServer].FirstOrDefault();
        string? ts = http.Headers[AgentProtocol.HeaderTimestamp].FirstOrDefault();
        string? nonce = http.Headers[AgentProtocol.HeaderNonce].FirstOrDefault();
        string? sig = http.Headers[AgentProtocol.HeaderSignature].FirstOrDefault();
        if (!int.TryParse(sid, NumberStyles.None, CultureInfo.InvariantCulture, out int serverId))
            return (null, Error(401, "bad_signature"));
        if (!long.TryParse(ts, NumberStyles.None, CultureInfo.InvariantCulture, out long tsValue)
            || Math.Abs(clock.Now.ToUnixTimeSeconds() - tsValue) > AgentProtocol.MaxClockSkewSeconds)
            return (null, Error(401, "clock_skew"));
        if (nonce is null || nonce.Length != 32 || !nonce.All(Uri.IsHexDigit))
            return (null, Error(401, "bad_signature"));

        var server = await db.Servers.FirstOrDefaultAsync(s => s.Id == serverId, ct);
        if (server is null || !server.IsActive || server.AgentSecretProtected is null)
            return (null, Error(401, "bad_signature"));
        byte[] secret = Convert.FromBase64String(protector.Unprotect(server.AgentSecretProtected, SecretPurposes.AgentSecret));
        string path = http.PathBase.Add(http.Path).Value ?? "";
        if (!AgentProtocol.Verify(secret, AgentProtocol.StringToSign(http.Method, path, ts!, nonce, AgentProtocol.BodyHash(body)), sig))
            return (null, Error(401, "bad_signature"));

        DateTime now = clock.UtcNow;
        db.ApiNonces.Add(new ApiNonce { InstallationId = "agent:" + serverId.ToString(CultureInfo.InvariantCulture), Nonce = nonce.ToLowerInvariant(), ExpiresUtc = now.Add(LicenseApi.NonceWindow) });
        server.AgentLastSeenUtc = now;
        string? version = http.Headers[AgentProtocol.HeaderVersion].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(version)) server.AgentVersion = version.Length > 50 ? version[..50] : version;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            return (null, Error(401, "replay"));
        }
        return (new Authed(server, body), null);
    }

    private static T? Read<T>(byte[] body) where T : class
    {
        try { return body.Length == 0 ? null : JsonSerializer.Deserialize<T>(body, AgentProtocol.Json); }
        catch (JsonException) { return null; }
    }

    internal static async Task<IResult> LeaseAsync(HttpRequest http, ControlDbContext db, ISecretProtector protector, Clock clock,
        ProvisioningService provisioning, CancellationToken ct)
    {
        var (a, err) = await AuthenticateAsync(http, db, protector, clock, ct);
        if (a is null) return err!;
        var lease = await provisioning.LeaseAsync(a.Server, ct, Read<LeaseRequest>(a.Body)?.Capabilities);
        return lease is null ? Results.NoContent() : Results.Json(lease, AgentProtocol.Json);
    }

    internal static async Task<IResult> ExtendAsync(long id, HttpRequest http, ControlDbContext db, ISecretProtector protector, Clock clock,
        ProvisioningService provisioning, CancellationToken ct)
    {
        var (a, err) = await AuthenticateAsync(http, db, protector, clock, ct);
        if (a is null) return err!;
        var r = Read<ExtendRequest>(a.Body);
        if (r is null) return Error(400, "bad_request");
        return Map(await provisioning.ExtendAsync(a.Server, id, r.LeaseToken, ct));
    }

    internal static async Task<IResult> CompleteAsync(long id, HttpRequest http, ControlDbContext db, ISecretProtector protector, Clock clock,
        ProvisioningService provisioning, CancellationToken ct)
    {
        var (a, err) = await AuthenticateAsync(http, db, protector, clock, ct);
        if (a is null) return err!;
        var r = Read<JobCompletion>(a.Body);
        if (r is null || !Enum.IsDefined(r.Outcome)) return Error(400, "bad_request");
        return Map(await provisioning.CompleteAsync(a.Server, id, r, ct));
    }

    internal static async Task<IResult> ScheduleAsync(HttpRequest http, ControlDbContext db, ISecretProtector protector, Clock clock,
        ProvisioningService provisioning, CancellationToken ct)
    {
        var (a, err) = await AuthenticateAsync(http, db, protector, clock, ct);
        if (a is null) return err!;
        var r = Read<ScheduleRequest>(a.Body);
        if (r is null) return Error(400, "bad_request");
        try { return Results.Json(await provisioning.ScheduleAsync(a.Server, r, ct), AgentProtocol.Json); }
        catch (DomainException ex) { return Error(422, "rejected", ex.Message); }
    }

    internal static async Task<IResult> UsageTargetsAsync(HttpRequest http, ControlDbContext db, ISecretProtector protector, Clock clock,
        UsageService usage, CancellationToken ct)
    {
        var (a, err) = await AuthenticateAsync(http, db, protector, clock, ct);
        if (a is null) return err!;
        return Results.Json(await usage.TargetsAsync(a.Server, ct), AgentProtocol.Json);
    }

    internal static async Task<IResult> UsageAsync(HttpRequest http, ControlDbContext db, ISecretProtector protector, Clock clock,
        UsageService usage, CancellationToken ct)
    {
        var (a, err) = await AuthenticateAsync(http, db, protector, clock, ct);
        if (a is null) return err!;
        var r = Read<UsageReport>(a.Body);
        if (r is null || string.IsNullOrWhiteSpace(r.Slug) || r.Events.Count > 20_000 || r.Users.Count > 5_000) return Error(400, "bad_request");
        try
        {
            await usage.IngestAsync(a.Server, r, ct);
            return Results.Json(new { ok = true });
        }
        catch (DomainException ex) { return Error(422, "rejected", ex.Message); }
    }

    internal static async Task<IResult> UpdateLeaseAsync(HttpRequest http, ControlDbContext db, ISecretProtector protector, Clock clock,
        UpdatesService updates, CancellationToken ct)
    {
        var (a, err) = await AuthenticateAsync(http, db, protector, clock, ct);
        if (a is null) return err!;
        var lease = await updates.LeaseAsync(a.Server, ct);
        return lease is null ? Results.NoContent() : Results.Json(lease, AgentProtocol.Json);
    }

    internal static async Task<IResult> UpdateProgressAsync(long id, HttpRequest http, ControlDbContext db, ISecretProtector protector, Clock clock,
        UpdatesService updates, CancellationToken ct)
    {
        var (a, err) = await AuthenticateAsync(http, db, protector, clock, ct);
        if (a is null) return err!;
        var r = Read<UpdateProgress>(a.Body);
        if (r is null) return Error(400, "bad_request");
        return Map(await updates.ProgressAsync(a.Server, id, r, ct));
    }

    internal static async Task<IResult> UpdateCompleteAsync(long id, HttpRequest http, ControlDbContext db, ISecretProtector protector, Clock clock,
        UpdatesService updates, CancellationToken ct)
    {
        var (a, err) = await AuthenticateAsync(http, db, protector, clock, ct);
        if (a is null) return err!;
        var r = Read<UpdateCompletion>(a.Body);
        if (r is null || !Enum.IsDefined(r.Outcome)) return Error(400, "bad_request");
        return Map(await updates.CompleteAsync(a.Server, id, r, ct));
    }

    internal static async Task<IResult> UpdateManifestAsync(long id, HttpRequest http, ControlDbContext db, ISecretProtector protector, Clock clock,
        UpdatesService updates, CancellationToken ct)
    {
        var (a, err) = await AuthenticateAsync(http, db, protector, clock, ct);
        if (a is null) return err!;
        var m = await updates.ManifestForJobAsync(a.Server, id, ct);
        return m is null ? Error(404, "not_found") : Results.Json(m, AgentProtocol.Json);
    }

    private sealed class ArtifactRequest { public string File { get; set; } = ""; }

    internal static async Task<IResult> UpdateArtifactAsync(long id, HttpRequest http, ControlDbContext db, ISecretProtector protector, Clock clock,
        UpdatesService updates, CancellationToken ct)
    {
        var (a, err) = await AuthenticateAsync(http, db, protector, clock, ct);
        if (a is null) return err!;
        var r = Read<ArtifactRequest>(a.Body);
        if (r is null || string.IsNullOrWhiteSpace(r.File)) return Error(400, "bad_request");
        try
        {
            var stream = await updates.OpenArtifactForJobAsync(a.Server, id, r.File, ct);
            return stream is null ? Error(404, "not_found") : Results.Stream(stream, "application/zip");
        }
        catch (DomainException ex) { return Error(502, "upstream", ex.Message); }
    }

    internal static async Task<IResult> UpdateInventoryAsync(HttpRequest http, ControlDbContext db, ISecretProtector protector, Clock clock,
        UpdatesService updates, CancellationToken ct)
    {
        var (a, err) = await AuthenticateAsync(http, db, protector, clock, ct);
        if (a is null) return err!;
        var r = Read<UpdateInventory>(a.Body);
        if (r is null || r.Apps.Count > 50 || r.Instances.Count > 2000) return Error(400, "bad_request");
        await updates.SaveInventoryAsync(a.Server, r, ct);
        return Results.Json(new { ok = true });
    }

    private static IResult Map(ProvisioningService.CompleteStatus s) => s switch
    {
        ProvisioningService.CompleteStatus.Ok => Results.Json(new { ok = true }),
        ProvisioningService.CompleteStatus.NotFound => Error(404, "not_found"),
        _ => Error(409, "lease_lost"),
    };
}
