using Microsoft.Extensions.Options;
using tahsinERP.Bot.Erp;
using tahsinERP.Bot.Pipeline;
using tahsinERP.Integration;

namespace tahsinERP.Bot.Files;

/// <summary>
/// GET /files/{kalit}?e=..&amp;s=.. — ERP sahifalaridagi rasmlar (selfi, tashrif fotosi, imzo, nuqta fotosi).
/// Havolani ERP tenant kaliti bilan imzolaydi (<see cref="DstApiSignature.SignFile"/>), muddati cheklangan; kalitning birinchi
/// bo'lagi — bot tenant slug'i (fayl shu tenantga tegishli), imzo o'sha tenantning ERP kaliti bilan tekshiriladi.
/// </summary>
public static class FileEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/files/{**key}", async (string key, string? e, string? s, TenantRegistry tenants, IFileStore files,
            IOptions<BotOptions> options, TimeProvider clock, HttpResponse response, CancellationToken ct) =>
        {
            var slug = key.Split('/')[0];
            var tenant = await tenants.BySlugAsync(slug, ct);
            if (tenant == null || !DstApiSignature.VerifyFile(ErpClientFactory.TenantKey(options.Value.MasterKey, tenant.Row), key, e, s, clock.GetUtcNow()))
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            var bytes = await files.ReadAsync(key, ct);
            if (bytes == null) return Results.NotFound();
            response.Headers.CacheControl = "private, max-age=3600";
            return Results.File(bytes, key.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg");
        });
}
