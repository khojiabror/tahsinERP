using System.Text.Json;
using tahsinERP.Bot.Erp;
using tahsinERP.Bot.Handlers;

namespace tahsinERP.Bot.MiniApp;

/// <summary>
/// 2-bosqich: ekspeditorning yetkazish sahifasi (deliver.html) — manzil tovarlari, yetkazilgan miqdorlar (qisman), rad sababi,
/// mijoz imzosi (canvas → PNG → fayl ombori, ERP'ga kalit). ERP ekspeditor faqat o'z reysi bilan ishlashini tekshiradi.
/// </summary>
public static partial class MiniAppEndpoints
{
    private static readonly string[] ExpeditorRoles = ["EXPEDITOR"];

    public sealed record DeliverLine(int LineId, decimal Qty);

    public sealed record DeliverRequest(bool All, List<DeliverLine>? Lines, int? ReasonId, string? Note, string? Signature);

    private static void MapDelivery(RouteGroupBuilder g)
    {
        g.MapGet("/stop/{id:int}", async (string slug, int id, HttpRequest req, Deps d, CancellationToken ct) =>
            await Run(slug, req, d, ct, async c => await c.Erp.GetAsync<JsonElement>("TripStop", c.Link.UserId, ct, ("id", id)), ExpeditorRoles));
        g.MapGet("/reasons", async (string slug, HttpRequest req, Deps d, CancellationToken ct) =>
            await Run(slug, req, d, ct, async c => await c.Erp.GetAsync<List<ErpNamed>>("Reasons", c.Link.UserId, ct, ("type", "RETURN")), ExpeditorRoles));
        g.MapPost("/stop/{id:int}/deliver", async (string slug, int id, DeliverRequest body, HttpRequest req, Deps d, CancellationToken ct) =>
            await Run(slug, req, d, ct, c => DeliverAsync(c, id, body, d, ct), ExpeditorRoles));
    }

    private static async Task<object?> DeliverAsync(Caller c, int stopId, DeliverRequest body, Deps d, CancellationToken ct)
    {
        if (body.Lines is { Count: > 500 } || body.Lines?.Any(l => l.Qty < 0 || l.Qty > 10_000_000) == true)
            throw new ErpException(400, "Miqdor noto'g'ri.");
        string? signatureKey = null;
        if (!string.IsNullOrEmpty(body.Signature))
        {
            var png = DecodePngDataUrl(body.Signature) ?? throw new ErpException(400, "Imzo formati noto'g'ri.");
            signatureKey = await d.Files.SaveAsync(c.Tenant.Slug, png, "png", ct);
        }
        var r = await c.Erp.PostAsync<ErpDeliverResult>("StopDeliver", new
        {
            stopId, all = body.All, lines = (body.Lines ?? []).Select(l => new { lineId = l.LineId, qty = l.Qty }), reasonId = body.ReasonId,
            note = Cut(body.Note, 300), signatureKey
        }, c.Link.UserId, ct) ?? throw new ErpException(500, "Bo'sh javob");
        var status = r.Status switch { "Delivered" => "StopStatus_Delivered", "Partial" => "StopStatus_Partial", _ => "StopStatus_Refused" };
        var text = r.Status == "Partial"
            ? Texts.Get(c.Link.Language, "Stop_Partial", Texts.Money(r.DeliveredAmount), Texts.Money(r.Amount))
            : r.Status == "Delivered" ? Texts.Get(c.Link.Language, "Stop_Delivered", Texts.Money(r.DeliveredAmount)) : Texts.Get(c.Link.Language, "Stop_Refused");
        d.Outbox.Send(c.Tenant.Id, c.Link.ChatId, text);
        await d.Db.SaveChangesAsync(ct);
        d.Outbox.Flush();
        return new { r.Status, r.DeliveredAmount, r.Amount, statusKey = status };
    }
}
