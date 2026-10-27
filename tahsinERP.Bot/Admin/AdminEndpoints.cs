using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace tahsinERP.Bot.Admin;

/// <summary>
/// Boshqaruv paneli uchun API (/admin/api). Kirish: "Authorization: Bearer {AdminKey}" (BotTenantAdmin.DeriveAdminKey).
///   GET  /admin/api/tenants/{slug}          — holat (webhook bilan)
///   PUT  /admin/api/tenants/{slug}          — {erpBaseUrl, erpTenant, token?} ulash/yangilash + webhook
///   POST /admin/api/tenants/{slug}/disable  — o'chirish
///   DELETE /admin/api/tenants/{slug}        — butunlay olib tashlash (ulanishlar, seanslar, navbat) — firma o'chirilganda
/// </summary>
public static class AdminEndpoints
{
    public sealed record UpsertRequest(string? ErpBaseUrl, string? ErpTenant, string? Token);

    public static void Map(WebApplication app)
    {
        var g = app.MapGroup("/admin/api").AddEndpointFilter(async (ctx, next) =>
            Authorized(ctx.HttpContext) ? await next(ctx) : Results.StatusCode(StatusCodes.Status401Unauthorized));

        g.MapGet("/tenants/{slug}", async (string slug, BotTenantAdmin admin, CancellationToken ct) =>
            await Safe(async () => Results.Ok(await admin.StatusAsync(slug, withWebhook: true, ct))));

        g.MapPut("/tenants/{slug}", async (string slug, UpsertRequest body, BotTenantAdmin admin, CancellationToken ct) =>
            await Safe(async () =>
            {
                var t = await admin.UpsertAsync(slug, body.ErpBaseUrl ?? "", body.ErpTenant, body.Token, verifyToken: true, ct);
                await admin.SetWebhookAsync(t, ct);
                return Results.Ok(await admin.StatusAsync(slug, withWebhook: true, ct));
            }));

        g.MapPost("/tenants/{slug}/disable", async (string slug, BotTenantAdmin admin, CancellationToken ct) =>
            await Safe(async () =>
            {
                await admin.DisableAsync(slug, ct);
                return Results.Ok(await admin.StatusAsync(slug, withWebhook: false, ct));
            }));

        g.MapDelete("/tenants/{slug}", async (string slug, BotTenantAdmin admin, CancellationToken ct) =>
            await Safe(async () => Results.Ok(new { slug, deleted = await admin.DeleteAsync(slug, ct) })));
    }

    private static bool Authorized(HttpContext ctx)
    {
        var master = ctx.RequestServices.GetRequiredService<IOptions<BotOptions>>().Value.MasterKey;
        var header = ctx.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return false;
        var given = Encoding.UTF8.GetBytes(header["Bearer ".Length..].Trim());
        var expected = Encoding.UTF8.GetBytes(BotTenantAdmin.DeriveAdminKey(master));
        return CryptographicOperations.FixedTimeEquals(given, expected);
    }

    private static async Task<IResult> Safe(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UriFormatException or HttpRequestException)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
