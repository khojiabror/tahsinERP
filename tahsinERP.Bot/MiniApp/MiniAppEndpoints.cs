using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tahsinERP.Bot.Data;
using tahsinERP.Bot.Erp;
using tahsinERP.Bot.Files;
using tahsinERP.Bot.Handlers;
using tahsinERP.Bot.Pipeline;
using tahsinERP.Bot.Security;

namespace tahsinERP.Bot.MiniApp;

/// <summary>
/// Mini App backendi (BFF): /app/api/{slug}/... Har so'rovda X-Telegram-Init-Data tekshiriladi (bot tokeni bilan HMAC),
/// Telegram foydalanuvchisi BotLinks orqali ERP foydalanuvchisiga aylanadi va ERP'ga uning nomidan imzolangan so'rov ketadi.
/// Brauzer ERP kalitini hech qachon ko'rmaydi.
/// </summary>
public static partial class MiniAppEndpoints
{
    public const string InitDataHeader = "X-Telegram-Init-Data";
    private const int MaxSignatureBytes = 512 * 1024;

    public sealed record OrderLineRequest(int ProductId, int? ProductUnitId, decimal Qty);

    public sealed record OrderRequest(int CustomerId, int? VisitId, DateTime? DeliveryDate, string? Comment, Guid ClientRequestId,
        string? Signature, List<OrderLineRequest> Lines);

    public sealed record ScanRequest(string Code, decimal Qty = 1);

    public sealed record CountSetRequest(int LineId, decimal? Counted);

    public sealed record ShipLineRequest(int LineId, decimal QtyBase);

    public sealed record ShipRequest(List<ShipLineRequest> Lines);

    private sealed record Caller(TenantRegistry.Tenant Tenant, BotLink Link, ErpClient Erp);

    private static readonly string[] FieldRoles = ["AGENT", "EXPEDITOR", "MANAGER"];
    private static readonly string[] StoreRoles = ["STOREKEEPER", "MANAGER"];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/app/api/{slug}");
        MapDelivery(g);
        g.MapGet("/me", async (string slug, HttpRequest req, Deps d, CancellationToken ct) =>
            await Run(slug, req, d, ct, c => Task.FromResult<object?>(new { name = c.Link.Name, language = c.Link.Language, role = c.Link.Role }),
                [.. FieldRoles, .. StoreRoles]));
        g.MapGet("/customer/{id:int}", async (string slug, int id, HttpRequest req, Deps d, CancellationToken ct) =>
            await Run(slug, req, d, ct, async c => await c.Erp.GetAsync<ErpCustomerCard>("Customer", c.Link.UserId, ct, ("id", id))));
        g.MapGet("/categories", async (string slug, HttpRequest req, Deps d, CancellationToken ct) =>
            await Run(slug, req, d, ct, async c => await c.Erp.GetAsync<List<ErpNamed>>("Categories", c.Link.UserId, ct)));
        g.MapGet("/catalog", async (string slug, string? q, int? customerId, int? categoryId, HttpRequest req, Deps d, CancellationToken ct) =>
            await Run(slug, req, d, ct, async c => await c.Erp.GetAsync<List<ErpCatalogItem>>("Catalog", c.Link.UserId, ct,
                ("q", string.IsNullOrWhiteSpace(q) ? null : q.Trim()), ("customerId", customerId), ("categoryId", categoryId))));
        g.MapPost("/order", async (string slug, OrderRequest body, HttpRequest req, Deps d, CancellationToken ct) =>
            await Run(slug, req, d, ct, c => SubmitOrderAsync(c, body, d, ct)));

        // omborchi: inventarizatsiya va buyurtma yig'ish (shtrix-kod skaneri) — ERP javobi o'zgarishsiz uzatiladi
        var s = g.MapGroup("/store");
        s.MapGet("/resolve", async (string slug, string code, HttpRequest req, Deps d, CancellationToken ct) =>
            await Store(slug, req, d, ct, c => c.Erp.GetAsync<JsonElement>("StoreResolve", c.Link.UserId, ct, ("code", code))));
        s.MapGet("/counts", async (string slug, HttpRequest req, Deps d, CancellationToken ct) =>
            await Store(slug, req, d, ct, c => c.Erp.GetAsync<JsonElement>("StoreCounts", c.Link.UserId, ct)));
        s.MapGet("/counts/{id:int}", async (string slug, int id, HttpRequest req, Deps d, CancellationToken ct) =>
            await Store(slug, req, d, ct, c => c.Erp.GetAsync<JsonElement>("StoreCount", c.Link.UserId, ct, ("id", id))));
        s.MapPost("/counts/{id:int}/scan", async (string slug, int id, ScanRequest body, HttpRequest req, Deps d, CancellationToken ct) =>
            await Store(slug, req, d, ct, c => c.Erp.PostAsync<JsonElement>("StoreCountScan", new { id, code = Cut(body.Code, 60), qty = body.Qty }, c.Link.UserId, ct)));
        s.MapPost("/counts/{id:int}/set", async (string slug, int id, CountSetRequest body, HttpRequest req, Deps d, CancellationToken ct) =>
            await Store(slug, req, d, ct, c => c.Erp.PostAsync<JsonElement>("StoreCountSet", new { id, lineId = body.LineId, counted = body.Counted }, c.Link.UserId, ct)));
        s.MapGet("/orders", async (string slug, HttpRequest req, Deps d, CancellationToken ct) =>
            await Store(slug, req, d, ct, c => c.Erp.GetAsync<JsonElement>("StoreOrders", c.Link.UserId, ct)));
        s.MapGet("/orders/{id:int}", async (string slug, int id, HttpRequest req, Deps d, CancellationToken ct) =>
            await Store(slug, req, d, ct, c => c.Erp.GetAsync<JsonElement>("StoreOrder", c.Link.UserId, ct, ("id", id))));
        s.MapPost("/orders/{id:int}/start", async (string slug, int id, HttpRequest req, Deps d, CancellationToken ct) =>
            await Store(slug, req, d, ct, c => c.Erp.PostAsync<JsonElement>("StorePickStart", new { id }, c.Link.UserId, ct)));
        s.MapPost("/orders/{id:int}/ship", async (string slug, int id, ShipRequest body, HttpRequest req, Deps d, CancellationToken ct) =>
            await Store(slug, req, d, ct, c =>
            {
                if (body.Lines is not { Count: > 0 } || body.Lines.Count > 500 || body.Lines.Any(l => l.QtyBase <= 0 || l.QtyBase > 10_000_000))
                    throw new ErpException(400, "Miqdor noto'g'ri.");
                return c.Erp.PostAsync<JsonElement>("StoreShip", new { id, lines = body.Lines }, c.Link.UserId, ct);
            }));
    }

    private static Task<IResult> Store(string slug, HttpRequest req, Deps d, CancellationToken ct, Func<Caller, Task<JsonElement>> body) =>
        Run(slug, req, d, ct, async c => (object?)await body(c), StoreRoles);

    /// <summary>DI orqali kerakli servislar (endpoint imzolari qisqa bo'lsin).</summary>
    public sealed class Deps(TenantRegistry tenants, BotDbContext db, ErpClientFactory erp, IFileStore files, Outbox outbox, TimeProvider clock,
        ILoggerFactory logs)
    {
        public TenantRegistry Tenants { get; } = tenants;
        public BotDbContext Db { get; } = db;
        public ErpClientFactory Erp { get; } = erp;
        public IFileStore Files { get; } = files;
        public Outbox Outbox { get; } = outbox;
        public TimeProvider Clock { get; } = clock;
        public ILogger Log { get; } = logs.CreateLogger("MiniApp");
    }

    private static async Task<IResult> Run(string slug, HttpRequest req, Deps d, CancellationToken ct, Func<Caller, Task<object?>> body, string[]? roles = null)
    {
        roles ??= FieldRoles;
        var tenant = await d.Tenants.BySlugAsync(slug, ct);
        if (tenant == null) return Results.NotFound();
        var init = WebAppInitData.Validate(req.Headers[InitDataHeader].ToString(), tenant.Token, d.Clock.GetUtcNow());
        if (init == null) return Results.Json(new { error = "initData" }, statusCode: 401);
        var link = await d.Db.Links.AsNoTracking().FirstOrDefaultAsync(l => l.TenantId == tenant.Id && l.TelegramUserId == init.UserId, ct);
        if (link?.UserId == null || !roles.Contains(link.Role))
            return Results.Json(new { error = Texts.Get(link?.Language, "NotAllowed") }, statusCode: 403);
        try
        {
            return Results.Json(await body(new Caller(tenant, link, d.Erp.For(tenant.Row))), ErpClient.Json);
        }
        catch (ErpException ex) when (ex.IsBusiness)
        {
            return Results.Json(new { error = ex.Message, problems = ex.Problems }, statusCode: 400);
        }
        catch (ErpException ex)
        {
            d.Log.LogWarning("ERP {Status}: {Message} (tenant {Slug})", ex.Status, ex.Message, slug);
            return Results.Json(new { error = Texts.Get(link.Language, ex.IsLicense ? "Error_License" : ex.Status == 403 ? "NotAllowed" : "Error_Erp") }, statusCode: 502);
        }
        catch (HttpRequestException ex)
        {
            d.Log.LogWarning(ex, "ERP bilan aloqa yo'q (tenant {Slug})", slug);
            return Results.Json(new { error = Texts.Get(link.Language, "Error_Erp") }, statusCode: 502);
        }
    }

    private static async Task<object?> SubmitOrderAsync(Caller c, OrderRequest body, Deps d, CancellationToken ct)
    {
        if (body.Lines is not { Count: > 0 } || body.Lines.Count > 200 || body.Lines.Any(l => l.Qty <= 0 || l.Qty > 1_000_000))
            throw new ErpException(400, "Savat bo'sh yoki miqdor noto'g'ri.");
        string? signatureKey = null;
        if (!string.IsNullOrEmpty(body.Signature))
        {
            var png = DecodePngDataUrl(body.Signature) ?? throw new ErpException(400, "Imzo formati noto'g'ri.");
            signatureKey = await d.Files.SaveAsync(c.Tenant.Slug, png, "png", ct);
        }
        var result = await c.Erp.PostAsync<ErpOrderResult>("Order", new ErpOrderInput(
            body.CustomerId, body.DeliveryDate, Cut(body.Comment, 500), 0, body.ClientRequestId, body.VisitId, signatureKey,
            body.Lines.Select(l => new ErpOrderLine(l.ProductId, l.ProductUnitId, l.Qty)).ToList()), c.Link.UserId, ct)
            ?? throw new ErpException(500, "Bo'sh javob");
        d.Outbox.Send(c.Tenant.Id, c.Link.ChatId, Texts.Get(c.Link.Language, "Order_Accepted", result.DocNo, Texts.Money(result.Total)));
        await d.Db.SaveChangesAsync(ct);
        d.Outbox.Flush();
        return result;
    }

    /// <summary>"data:image/png;base64,..." -> PNG baytlari (imzo belgisi tekshiriladi).</summary>
    internal static byte[]? DecodePngDataUrl(string dataUrl)
    {
        const string prefix = "data:image/png;base64,";
        if (!dataUrl.StartsWith(prefix, StringComparison.Ordinal) || dataUrl.Length > MaxSignatureBytes * 4 / 3 + prefix.Length + 4) return null;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(dataUrl[prefix.Length..]); }
        catch (FormatException) { return null; }
        ReadOnlySpan<byte> magic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        return bytes.Length > 8 && bytes.AsSpan(0, 8).SequenceEqual(magic) ? bytes : null;
    }

    private static string? Cut(string? s, int max) => s == null ? null : s.Length <= max ? s.Trim() : s[..max].Trim();
}
