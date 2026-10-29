using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tahsinERP.Bot.Data;
using tahsinERP.Bot.Erp;
using tahsinERP.Bot.Handlers;
using tahsinERP.Bot.Security;

namespace tahsinERP.Bot.MiniApp;

/// <summary>
/// Telegram do'kon (mijoz Mini App'i, /app/shop.html): /app/api/{slug}/shop/... Faqat CUSTOMER roli bilan bog'langan foydalanuvchi.
/// ERP'ga vakil foydalanuvchisiz, telegramUserId bilan imzolangan so'rov ketadi — mijozni ERP o'zi TELEGRAM_ACCOUNTS'dan topadi
/// (brauzer customerId yubormaydi, boshqa mijoz nomidan buyurtma berib bo'lmaydi). To'lov endpointi YO'Q — onlayn to'lov qilinmaydi.
/// </summary>
public static partial class MiniAppEndpoints
{
    public sealed record ShopOrderRequest(bool IsPickup, string? Address, string? Comment, Guid ClientRequestId, List<OrderLineRequest> Lines);

    private sealed record CustomerCaller(BotLink Link, ErpClient Erp);

    private static void MapShop(RouteGroupBuilder g)
    {
        var s = g.MapGroup("/shop");
        s.MapGet("/info", async (string slug, HttpRequest req, Deps d, CancellationToken ct) =>
            await RunCustomer(slug, req, d, ct, async c =>
            {
                var info = await c.Erp.GetAsync<JsonElement>("ShopInfo", null, ct, ("telegramUserId", c.Link.TelegramUserId));
                return new { language = c.Link.Language, info };
            }));
        s.MapGet("/categories", async (string slug, HttpRequest req, Deps d, CancellationToken ct) =>
            await RunCustomer(slug, req, d, ct, async c =>
                await c.Erp.GetAsync<JsonElement>("ShopCategories", null, ct, ("telegramUserId", c.Link.TelegramUserId))));
        s.MapGet("/catalog", async (string slug, string? q, int? categoryId, HttpRequest req, Deps d, CancellationToken ct) =>
            await RunCustomer(slug, req, d, ct, async c =>
                await c.Erp.GetAsync<JsonElement>("ShopCatalog", null, ct, ("telegramUserId", c.Link.TelegramUserId),
                    ("q", string.IsNullOrWhiteSpace(q) ? null : Cut(q.Trim(), 100)), ("categoryId", categoryId))));
        s.MapGet("/orders", async (string slug, HttpRequest req, Deps d, CancellationToken ct) =>
            await RunCustomer(slug, req, d, ct, async c =>
                await c.Erp.GetAsync<JsonElement>("ShopOrders", null, ct, ("telegramUserId", c.Link.TelegramUserId))));
        s.MapPost("/order", async (string slug, ShopOrderRequest body, HttpRequest req, Deps d, CancellationToken ct) =>
            await RunCustomer(slug, req, d, ct, async c =>
            {
                if (body.Lines is not { Count: > 0 } || body.Lines.Count > 200 || body.Lines.Any(l => l.Qty <= 0 || l.Qty > 1_000_000))
                    throw new ErpException(400, Texts.Get(c.Link.Language, "Shop_BadQty"));
                if (body.ClientRequestId == Guid.Empty) throw new ErpException(400, "ClientRequestId");
                return await c.Erp.PostAsync<JsonElement>("ShopOrder", new
                {
                    telegramUserId = c.Link.TelegramUserId, isPickup = body.IsPickup, address = Cut(body.Address, 200),
                    comment = Cut(body.Comment, 300), clientRequestId = body.ClientRequestId,
                    lines = body.Lines.Select(l => new { productId = l.ProductId, productUnitId = l.ProductUnitId, qty = l.Qty })
                }, null, ct);
            }));
    }

    private static async Task<IResult> RunCustomer(string slug, HttpRequest req, Deps d, CancellationToken ct, Func<CustomerCaller, Task<object?>> body)
    {
        var tenant = await d.Tenants.BySlugAsync(slug, ct);
        if (tenant == null) return Results.NotFound();
        var init = WebAppInitData.Validate(req.Headers[InitDataHeader].ToString(), tenant.Token, d.Clock.GetUtcNow());
        if (init == null) return Results.Json(new { error = "initData" }, statusCode: 401);
        var link = await d.Db.Links.AsNoTracking().FirstOrDefaultAsync(l => l.TenantId == tenant.Id && l.TelegramUserId == init.UserId, ct);
        if (link?.CustomerId == null || link.Role != "CUSTOMER")
            return Results.Json(new { error = Texts.Get(link?.Language, "Shop_NotCustomer") }, statusCode: 403);
        try
        {
            return Results.Json(await body(new CustomerCaller(link, d.Erp.For(tenant.Row))), ErpClient.Json);
        }
        catch (ErpException ex) when (ex.IsBusiness)
        {
            return Results.Json(new { error = ex.Message, problems = ex.Problems }, statusCode: 400);
        }
        catch (ErpException ex)
        {
            d.Log.LogWarning("ERP {Status}: {Message} (tenant {Slug}, shop)", ex.Status, ex.Message, slug);
            return Results.Json(new { error = Texts.Get(link.Language, ex.IsLicense ? "Error_License" : ex.Status == 403 ? "NotAllowed" : "Error_Erp") }, statusCode: 502);
        }
        catch (HttpRequestException ex)
        {
            d.Log.LogWarning(ex, "ERP bilan aloqa yo'q (tenant {Slug}, shop)", slug);
            return Results.Json(new { error = Texts.Get(link.Language, "Error_Erp") }, statusCode: 502);
        }
    }
}
