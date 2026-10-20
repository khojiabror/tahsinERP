using System.Globalization;
using tahsinERP.Bot.Erp;
using tahsinERP.Bot.Files;
using tahsinERP.Bot.Telegram;

namespace tahsinERP.Bot.Handlers;

public sealed partial class UpdateRouter
{
    private const int MaxRouteButtons = 40;

    // ---------- tugmalar ----------

    private async Task OnCallbackAsync(Ctx ctx, TgCallbackQuery cq, CancellationToken ct)
    {
        if (!await EnsureLinkAsync(ctx, ct))
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, Texts.Get(LangOf(ctx.From), "Welcome_Unlinked"));
            return;
        }
        var cb = Callback.Parse(cq.Data);
        if (cb.Command == "ov")
        {
            if (RequireManager(ctx) && cb.Int(0) is int orderId) await ConfirmOverrideAsync(ctx, orderId, cq.Message, ct);
            return;
        }
        if (cb.Command == "x")
        {
            await sessions.ClearAsync(ctx.Tenant.Id, ctx.From.Id, ct);
            await SendMenuAsync(ctx, ctx.T("Cancelled"), ct);
            return;
        }
        if (!RequireField(ctx)) return;

        switch (cb.Command)
        {
            case "rt": await SendRouteAsync(ctx, cq.Message?.MessageId, ct); break;
            case "c" when cb.Int(0) is int id: await SendCardAsync(ctx, id, ct); break;
            case "ci" when cb.Int(0) is int id:
                await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.CheckIn, new SessionData { CustomerId = id }, ct);
                outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("CheckIn_Ask"), LocationKeyboard(ctx));
                break;
            case "map" when cb.Int(0) is int id:
                var card = await CardAsync(ctx, id, ct);
                if (card.Lat is decimal lat && card.Lng is decimal lng) outbox.SendLocation(ctx.Tenant.Id, ctx.ChatId, lat, lng);
                break;
            case "pay" when cb.Int(0) is int id: await AskPaymentAsync(ctx, id, cb.Int(1), ct); break;
            case "pm" when cb.Str(0) is string method && Texts.PayMethods.Contains(method): await SubmitPaymentAsync(ctx, method, ct); break;
            case "no" when cb.Int(0) is int visitId: await AskNoOrderReasonAsync(ctx, visitId, ct); break;
            case "nr" when cb.Int(0) is int visitId && cb.Int(1) is int reasonId: await SaveNoOrderAsync(ctx, visitId, reasonId, ct); break;
            case "ph" when cb.Int(0) is int visitId:
                await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.Photo, new SessionData { VisitId = visitId }, ct);
                outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Photo_Ask"), CancelKeyboard(ctx));
                break;
            case "ho": await AskHandoverAsync(ctx, ct); break;
            case "ho!": await SubmitHandoverAsync(ctx, ct); break;
        }
    }

    // ---------- ish kuni ----------

    private async Task AskDayLocationAsync(Ctx ctx, string state, string textKey, CancellationToken ct)
    {
        if (!RequireField(ctx)) return;
        await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, state, null, ct);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T(textKey), LocationKeyboard(ctx));
    }

    private async Task OnLocationAsync(Ctx ctx, string? state, SessionData data, TgLocation loc, CancellationToken ct)
    {
        if (!RequireField(ctx)) return;
        decimal lat = Math.Round((decimal)loc.Latitude, 6), lng = Math.Round((decimal)loc.Longitude, 6);
        switch (state)
        {
            case States.DayStart:
            {
                var day = await ctx.Erp.PostAsync<ErpDayStarted>("DayStart", new { lat, lng }, ctx.UserId, ct);
                await sessions.ClearAsync(ctx.Tenant.Id, ctx.From.Id, ct);
                await SendMenuAsync(ctx, ctx.T("Day_Started", day?.StartAt.ToString("HH:mm", CultureInfo.InvariantCulture)), ct);
                await SendRouteAsync(ctx, null, ct);
                break;
            }
            case States.DayEnd:
            {
                var d = await ctx.Erp.PostAsync<ErpDay>("DayEnd", new { lat, lng }, ctx.UserId, ct);
                await sessions.ClearAsync(ctx.Tenant.Id, ctx.From.Id, ct);
                await SendMenuAsync(ctx, d == null ? ctx.T("Menu") : DaySummaryText(ctx, d), ct);
                break;
            }
            case States.CheckIn when data.CustomerId is int customerId:
            {
                var accuracy = loc.HorizontalAccuracy is double a ? (int?)Math.Round(a) : null;
                var r = await ctx.Erp.PostAsync<ErpCheckIn>("CheckIn",
                    new { customerId, lat, lng, accuracyM = accuracy, live = loc.LivePeriod is > 0 }, ctx.UserId, ct);
                await sessions.ClearAsync(ctx.Tenant.Id, ctx.From.Id, ct);
                await SendMenuAsync(ctx, ctx.T("CheckIn_Ok", r?.DistanceM is int dm ? ctx.T("CheckIn_Distance", dm) : ""), ct);
                await SendCardAsync(ctx, customerId, ct);
                break;
            }
            default:
                await SendMenuAsync(ctx, ctx.T("Menu"), ct);
                break;
        }
    }

    private static string DaySummaryText(Ctx ctx, ErpDay d) =>
        ctx.T("Day_Summary", d.Visited, d.Planned, d.NoOrder, d.Orders, Texts.Money(d.OrdersTotal), Texts.Money(d.Collected), Texts.Money(d.CashOnHand));

    // ---------- marshrut va mijoz ----------

    private async Task SendRouteAsync(Ctx ctx, long? editMessageId, CancellationToken ct)
    {
        if (!RequireField(ctx)) return;
        var stops = await ctx.Erp.GetAsync<List<ErpRouteStop>>("Route", ctx.UserId, ct) ?? [];
        if (stops.Count == 0)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Route_Empty"));
            return;
        }
        var text = ctx.T("Route_Title", stops.Count, stops.Count(s => s.Visited));
        var rows = stops.Take(MaxRouteButtons)
            .Select(s => new[] { Kb.Button(StopLabel(s), Callback.Make("c", s.CustomerId)) })
            .Append([Kb.Button(ctx.T("Btn_Refresh"), "rt")]);
        var keyboard = Kb.Inline(rows);
        if (editMessageId is long mid) outbox.Edit(ctx.Tenant.Id, ctx.ChatId, mid, text, keyboard);
        else outbox.Send(ctx.Tenant.Id, ctx.ChatId, text, keyboard);
    }

    internal static string StopLabel(ErpRouteStop s)
    {
        var mark = s.Visited ? s.Result switch { "ORDER" => "✅", "PAYMENT" => "💵", "NO_ORDER" => "🚫", _ => "🟢" } : s.Planned ? "⏳" : "•";
        var label = mark + " " + s.Name + (s.Debt > 0 ? " · " + Texts.Money(s.Debt) + (s.Overdue > 0 ? "❗" : "") : "");
        return label.Length <= 60 ? label : label[..59] + "…";
    }

    private async Task<ErpCustomerCard> CardAsync(Ctx ctx, int customerId, CancellationToken ct) =>
        await ctx.Erp.GetAsync<ErpCustomerCard>("Customer", ctx.UserId, ct, ("id", customerId)) ?? throw new ErpException(404, "Mijoz topilmadi");

    private async Task SendCardAsync(Ctx ctx, int customerId, CancellationToken ct)
    {
        var c = await CardAsync(ctx, customerId, ct);
        var text = ctx.T("Card", c.Name, JoinNonEmpty(c.Address, c.Landmark), c.Phone ?? "—", c.PriceType ?? "—", Texts.Money(c.Debt), Texts.Money(c.Overdue),
            Texts.Money(c.CreditLimit), c.PaymentDays);
        if (!c.Approved) text += "\n" + ctx.T("Card_NotApproved");
        if (c.Recommended is { Count: > 0 } rec)
            text += "\n\n" + ctx.T("Card_Recommended") + "\n" + string.Join("\n", rec.Take(5).Select(r =>
                ctx.T("Card_RecommendedLine", r.Product ?? "#" + r.ProductId, Texts.Qty(r.AvgQtyBase), r.DaysSinceLast, r.Overdue ? ctx.T("Card_LongAgo") : "")));
        if (c.OpenVisitId != null) text += "\n\n" + ctx.T("Card_Visit");

        var visit = c.OpenVisitId;
        var rows = new List<object[]>();
        var first = new List<object>();
        if (visit == null) first.Add(Kb.Button(ctx.T("Btn_CheckIn"), Callback.Make("ci", c.CustomerId)));
        first.Add(Kb.WebApp(ctx.T("Btn_Order"), MiniAppUrl(options.Value.PublicBaseUrl, ctx.Tenant.Slug, c.CustomerId, visit)));
        rows.Add([.. first]);
        rows.Add(visit == null
            ? [Kb.Button(ctx.T("Btn_Payment"), Callback.Make("pay", c.CustomerId, 0))]
            : [Kb.Button(ctx.T("Btn_Payment"), Callback.Make("pay", c.CustomerId, visit)), Kb.Button(ctx.T("Btn_NoOrder"), Callback.Make("no", visit))]);
        var third = new List<object>();
        if (visit != null) third.Add(Kb.Button(ctx.T("Btn_Photo"), Callback.Make("ph", visit)));
        if (c.Lat != null && c.Lng != null) third.Add(Kb.Button(ctx.T("Btn_Map"), Callback.Make("map", c.CustomerId)));
        rows.Add([.. third]);
        rows.Add([Kb.Button(ctx.T("Btn_Back"), "rt")]);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, text, Kb.Inline(rows));
    }

    private static string JoinNonEmpty(params string?[] parts)
    {
        var s = string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return s.Length == 0 ? "—" : s;
    }

    private async Task SearchAsync(Ctx ctx, string q, CancellationToken ct)
    {
        await sessions.ClearAsync(ctx.Tenant.Id, ctx.From.Id, ct);
        var found = await ctx.Erp.GetAsync<List<ErpCustomerShort>>("Customers", ctx.UserId, ct, ("q", q.Length > 100 ? q[..100] : q)) ?? [];
        if (found.Count == 0)
        {
            await SendMenuAsync(ctx, ctx.T("Search_None"), ct);
            return;
        }
        await SendMenuAsync(ctx, ctx.T("Search_Found", found.Count), ct);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, "🔎 " + q,
            Kb.Inline(found.Take(20).Select(c => new[] { Kb.Button((c.Approved ? "" : "⚠️ ") + c.Name, Callback.Make("c", c.Id)) })));
    }

    // ---------- to'lov ----------

    private async Task AskPaymentAsync(Ctx ctx, int customerId, int? visitId, CancellationToken ct)
    {
        var c = await CardAsync(ctx, customerId, ct);
        await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.PayAmount,
            new SessionData { CustomerId = customerId, VisitId = visitId, RequestId = Guid.NewGuid() }, ct);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Pay_AskAmount", c.Name, Texts.Money(c.Debt)), CancelKeyboard(ctx));
    }

    private async Task PaymentAmountAsync(Ctx ctx, SessionData data, string text, CancellationToken ct)
    {
        if (Amounts.Parse(text) is not decimal amount)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Pay_BadAmount"));
            return;
        }
        data.Amount = amount;
        await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.PayMethod, data, ct);
        var rows = Texts.PayMethods.Chunk(3)
            .Select(chunk => chunk.Select(m => Kb.Button(ctx.T("PayMethod_" + m), Callback.Make("pm", m))))
            .Append([Kb.Button(ctx.T("Btn_Cancel"), "x")]);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Pay_AskMethod", Texts.Money(amount)), Kb.Inline(rows));
    }

    private async Task SubmitPaymentAsync(Ctx ctx, string method, CancellationToken ct)
    {
        var (state, data) = await sessions.GetAsync(ctx.Tenant.Id, ctx.From.Id, ct);
        if (state != States.PayMethod || data.CustomerId is not int customerId || data.Amount is not decimal amount || data.RequestId is not Guid rid)
        {
            await SendMenuAsync(ctx, ctx.T("Menu"), ct);
            return;
        }
        // ClientRequestId — takroriy bosishda ham bitta to'lov
        var r = await ctx.Erp.PostAsync<ErpPaymentResult>("Payment",
            new ErpPaymentInput(customerId, null, amount, method, null, rid, data.VisitId is > 0 ? data.VisitId : null), ctx.UserId, ct);
        await sessions.ClearAsync(ctx.Tenant.Id, ctx.From.Id, ct);
        await SendMenuAsync(ctx, ctx.T("Pay_Done", r?.DocNo, Texts.Money(r?.Amount ?? amount), Texts.Money(r?.OnHand ?? 0)), ct);
    }

    // ---------- tashrif natijasi ----------

    private async Task AskNoOrderReasonAsync(Ctx ctx, int visitId, CancellationToken ct)
    {
        var reasons = await ctx.Erp.GetAsync<List<ErpNamed>>("Reasons", ctx.UserId, ct, ("type", "NO_ORDER")) ?? [];
        var rows = reasons.Select(r => new[] { Kb.Button(r.Name, Callback.Make("nr", visitId, r.Id)) }).Append([Kb.Button(ctx.T("Btn_Cancel"), "x")]);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("NoOrder_Ask"), Kb.Inline(rows));
    }

    private async Task SaveNoOrderAsync(Ctx ctx, int visitId, int reasonId, CancellationToken ct)
    {
        await ctx.Erp.PostAsync<object>("VisitResult", new { visitId, result = "NO_ORDER", reasonId }, ctx.UserId, ct);
        var reasons = await ctx.Erp.GetAsync<List<ErpNamed>>("Reasons", ctx.UserId, ct, ("type", "NO_ORDER")) ?? [];
        await SendMenuAsync(ctx, ctx.T("NoOrder_Saved", reasons.FirstOrDefault(r => r.Id == reasonId)?.Name ?? ""), ct);
        await SendRouteAsync(ctx, null, ct);
    }

    private async Task SavePhotoAsync(Ctx ctx, int visitId, List<TgPhotoSize> sizes, CancellationToken ct)
    {
        var best = sizes.Where(s => s.FileSize is null or <= LocalFileStore.MaxBytes).OrderByDescending(s => s.Width * s.Height).FirstOrDefault();
        var bytes = best == null ? null : await telegram.DownloadFileAsync(ctx.Tenant.Token, best.FileId, ct);
        if (bytes == null || bytes.Length > LocalFileStore.MaxBytes)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Photo_TooBig"));
            return;
        }
        var key = await files.SaveAsync(ctx.Tenant.Slug, bytes, "jpg", ct);
        await ctx.Erp.PostAsync<object>("VisitResult", new { visitId, photoKey = key }, ctx.UserId, ct);
        await sessions.ClearAsync(ctx.Tenant.Id, ctx.From.Id, ct);
        await SendMenuAsync(ctx, ctx.T("Photo_Saved"), ct);
    }

    // ---------- naqd pul ----------

    private async Task SendCashAsync(Ctx ctx, CancellationToken ct)
    {
        if (!RequireField(ctx)) return;
        var me = await ctx.Erp.GetAsync<ErpMe>("Me", ctx.UserId, ct);
        var day = me?.Day;
        var cash = day?.CashOnHand ?? 0;
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Cash_Info", Texts.Money(cash), Texts.Money(day?.Collected ?? 0)),
            cash > 0 ? Kb.Inline([Kb.Button(ctx.T("Btn_Handover"), "ho")]) : null);
    }

    private async Task AskHandoverAsync(Ctx ctx, CancellationToken ct)
    {
        var me = await ctx.Erp.GetAsync<ErpMe>("Me", ctx.UserId, ct);
        var cash = me?.Day?.CashOnHand ?? 0;
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Cash_ConfirmHandover", Texts.Money(cash)),
            Kb.Inline([Kb.Button(ctx.T("Btn_Confirm"), "ho!"), Kb.Button(ctx.T("Btn_Cancel"), "x")]));
    }

    private async Task SubmitHandoverAsync(Ctx ctx, CancellationToken ct)
    {
        var h = await ctx.Erp.PostAsync<ErpHandover>("HandoverSubmit", new { }, ctx.UserId, ct);
        await SendMenuAsync(ctx, ctx.T("Cash_HandedOver", h?.DocNo, Texts.Money(h?.AmountDeclared ?? 0)), ct);
    }

    // ---------- rahbar ----------

    private async Task ConfirmOverrideAsync(Ctx ctx, int orderId, TgMessage? source, CancellationToken ct)
    {
        var r = await ctx.Erp.PostAsync<ErpOverrideResult>("ConfirmOverride", new { orderId, note = "Telegram" }, ctx.UserId, ct);
        if (source != null)
            outbox.Enqueue(ctx.Tenant.Id, ctx.ChatId, "editMessageReplyMarkup",
                new { chat_id = ctx.ChatId, message_id = source.MessageId, reply_markup = new { inline_keyboard = Array.Empty<object>() } });
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Override_Done", r?.DocNo));
    }

    private async Task SendSummaryAsync(Ctx ctx, CancellationToken ct)
    {
        if (!RequireManager(ctx)) return;
        var all = await ctx.Erp.GetAsync<List<ErpPending>>("DailySummary", null, ct) ?? [];
        var mine = all.FirstOrDefault(s => s.ChatId == ctx.ChatId);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, mine?.Text ?? ctx.T("Summary_None"));
    }
}
