using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace tahsinERP.Site.Admin;

/// <summary>
/// Boshqaruv paneli uchun API (/admin/api, istalgan *.{BaseDomain} host orqali, tenant middleware'dan oldin).
/// Kirish: "Authorization: Bearer {AdminKey}" (SiteTenantAdmin.DeriveAdminKey).
///   GET  /admin/api/tenants/{slug}          — holat
///   PUT  /admin/api/tenants/{slug}          — {erpBaseUrl, erpTenant} yoqish/yangilash
///   POST /admin/api/tenants/{slug}/disable  — o'chirish
///   DELETE /admin/api/tenants/{slug}        — butunlay olib tashlash (nusxa, ariza navbati) — firma o'chirilganda
///   PUT  /admin/api/tenants/{slug}/domain   — {host, redirect} o'z domeni (host null — olib tashlash)
/// </summary>
public static class AdminEndpoints
{
    public sealed record UpsertRequest(string? ErpBaseUrl, string? ErpTenant);
    public sealed record DomainRequest(string? Host, bool Redirect);

    public static void Map(WebApplication app)
    {
        var g = app.MapGroup("/admin/api").AddEndpointFilter(async (ctx, next) =>
            Authorized(ctx.HttpContext) ? await next(ctx) : Results.StatusCode(StatusCodes.Status401Unauthorized));

        g.MapGet("/tenants/{slug}", async (string slug, SiteTenantAdmin admin, CancellationToken ct) =>
            await Safe(async () => Results.Ok(await admin.StatusAsync(slug, ct))));

        g.MapPut("/tenants/{slug}", async (string slug, UpsertRequest body, SiteTenantAdmin admin, CancellationToken ct) =>
            await Safe(async () =>
            {
                await admin.UpsertAsync(slug, body.ErpBaseUrl ?? "", body.ErpTenant, ct);
                return Results.Ok(await admin.StatusAsync(slug, ct));
            }));

        g.MapPut("/tenants/{slug}/domain", async (string slug, DomainRequest body, SiteTenantAdmin admin, IOptions<SiteOptions> options, CancellationToken ct) =>
            await Safe(async () =>
            {
                await admin.SetDomainAsync(slug, body.Host, body.Redirect, options.Value.BaseDomain, ct);
                return Results.Ok(await admin.StatusAsync(slug, ct));
            }));

        g.MapPost("/tenants/{slug}/disable", async (string slug, SiteTenantAdmin admin, CancellationToken ct) =>
            await Safe(async () =>
            {
                await admin.DisableAsync(slug, ct);
                return Results.Ok(await admin.StatusAsync(slug, ct));
            }));

        g.MapDelete("/tenants/{slug}", async (string slug, SiteTenantAdmin admin, CancellationToken ct) =>
            await Safe(async () => Results.Ok(new { slug, deleted = await admin.DeleteAsync(slug, ct) })));
    }

    /// <summary>/admin/api so'rovi — tenant middleware uni o'tkazib yuboradi.</summary>
    public static bool IsAdminPath(PathString path) => path.StartsWithSegments("/admin/api");

    private static bool Authorized(HttpContext ctx)
    {
        var master = ctx.RequestServices.GetRequiredService<IOptions<SiteOptions>>().Value.MasterKey;
        var header = ctx.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return false;
        var given = Encoding.UTF8.GetBytes(header["Bearer ".Length..].Trim());
        var expected = Encoding.UTF8.GetBytes(SiteTenantAdmin.DeriveAdminKey(master));
        return CryptographicOperations.FixedTimeEquals(given, expected);
    }

    private static async Task<IResult> Safe(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UriFormatException)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
