using tahsinERP.Bot.Erp;
using tahsinERP.Bot.Telegram;

namespace tahsinERP.Bot.Handlers;

/// <summary>
/// Ekspeditor rejimi (2-bosqich): bugungi reys va manzillar, qo'ng'iroq (telefon matnda — Telegram bosiladigan qiladi) va navigatsiya
/// (Yandex havolasi), "Yetkazildi" (rasm) yoki Mini App'da imzo / qisman, rad (sabab bilan), to'lov, xarajat, reysni yopish.
/// callback: tr reys · ts:{reys} yo'lga chiqish · st:{manzil} karta · sd:{manzil} yetkazildi (rasm) · sr:{manzil} rad sabablari ·
/// srr:{manzil}:{sabab} rad · sp:{manzil} to'lov · tx:{reys} xarajat turi · txk:{reys}:{tur} summa · tc!:{reys} yopish.
/// </summary>
public sealed partial class UpdateRouter
{
    private static readonly string[] ExpenseKinds = ["FUEL", "PARKING", "REPAIR", "MEAL", "OTHER"];

    /// <summary>Ekspeditor callback'lari; o'zimizniki bo'lmasa — false.</summary>
    private async Task<bool> OnDeliveryCallbackAsync(Ctx ctx, Callback cb, TgMessage? source, CancellationToken ct)
    {
        switch (cb.Command)
        {
            case "tr": await SendTripAsync(ctx, source?.MessageId, ct); return true;
            case "ts" when cb.Int(0) is int tripId:
                await ctx.Erp.PostAsync<object>("TripStart", new { tripId }, ctx.UserId, ct);
                outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Trip_Started"));
                await SendTripAsync(ctx, null, ct);
                return true;
            case "st" when cb.Int(0) is int stopId: await SendStopAsync(ctx, stopId, ct); return true;
            case "sd" when cb.Int(0) is int stopId:
                await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.TripPhoto, new SessionData { StopId = stopId }, ct);
                outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Stop_AskPhoto"), CancelKeyboard(ctx));
                return true;
            case "sr" when cb.Int(0) is int stopId:
            {
                var reasons = await ctx.Erp.GetAsync<List<ErpNamed>>("Reasons", ctx.UserId, ct, ("type", "RETURN")) ?? [];
                var rows = reasons.Select(r => new[] { Kb.Button(r.Name, Callback.Make("srr", stopId, r.Id)) }).Append([Kb.Button(ctx.T("Btn_Cancel"), "x")]);
                outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Stop_AskReason"), Kb.Inline(rows));
                return true;
            }
            case "srr" when cb.Int(0) is int stopId && cb.Int(1) is int reasonId:
                await ctx.Erp.PostAsync<ErpDeliverResult>("StopDeliver", new { stopId, all = false, lines = Array.Empty<object>(), reasonId }, ctx.UserId, ct);
                outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Stop_Refused"));
                await SendTripAsync(ctx, null, ct);
                return true;
            case "sp" when cb.Int(0) is int stopId:
            {
                var s = await StopAsync(ctx, stopId, ct);
                await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.PayAmount,
                    new SessionData { StopId = stopId, CustomerId = 0, RequestId = Guid.NewGuid() }, ct);
                outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Stop_PayAsk", s.Stop.Customer, Texts.Money(s.Stop.Amount), Texts.Money(s.Stop.Debt)), CancelKeyboard(ctx));
                return true;
            }
            case "tx" when cb.Int(0) is int tripId:
            {
                var rows = ExpenseKinds.Chunk(3).Select(chunk => chunk.Select(k => Kb.Button(ctx.T("Expense_" + k), Callback.Make("txk", tripId, k))))
                    .Append([Kb.Button(ctx.T("Btn_Cancel"), "x")]);
                outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Expense_AskKind"), Kb.Inline(rows));
                return true;
            }
            case "txk" when cb.Int(0) is int tripId && cb.Str(1) is string kind && ExpenseKinds.Contains(kind):
                await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.TripExpense, new SessionData { TripId = tripId, Kind = kind }, ct);
                outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Expense_AskAmount", ctx.T("Expense_" + kind)), CancelKeyboard(ctx));
                return true;
            case "tc!" when cb.Int(0) is int tripId:
            {
                var r = await ctx.Erp.PostAsync<ErpTripClosed>("TripClose", new { tripId }, ctx.UserId, ct);
                await SendMenuAsync(ctx, r == null ? ctx.T("Menu") : ctx.T("Close_Done", r.DocNo, r.Delivered, r.Partial, r.Refused,
                    Texts.Money(r.DeliveredAmount), Texts.Money(r.ReturnedAmount), Texts.Money(r.Collected), Texts.Money(r.OnHand)), ct);
                if (r is { OnHand: > 0 })
                    outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Cash_Info", Texts.Money(r.OnHand), Texts.Money(r.Collected)),
                        Kb.Inline([Kb.Button(ctx.T("Btn_Handover"), "ho")]));
                return true;
            }
        }
        return false;
    }

    // ---------- reys ----------

    private async Task SendTripAsync(Ctx ctx, long? editMessageId, CancellationToken ct)
    {
        if (!RequireField(ctx)) return;
        var trip = await ctx.Erp.GetAsync<ErpTrip>("MyTrip", ctx.UserId, ct);
        if (trip == null)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Trip_None"));
            return;
        }
        var text = TripText(ctx.Lang, trip);
        var rows = new List<object[]>();
        if (trip.Status == ErpTrip.Planned) rows.Add([Kb.Button(ctx.T("Btn_TripStart"), Callback.Make("ts", trip.Id))]);
        rows.AddRange(trip.Stops.Take(40).Select(s => new[] { Kb.Button(StopButton(s), Callback.Make("st", s.Id)) }));
        var last = new List<object>();
        if (!string.IsNullOrEmpty(trip.RouteUrl)) last.Add(Kb.Url(ctx.T("Btn_TripRoute"), trip.RouteUrl));
        last.Add(Kb.Button(ctx.T("Btn_Expense"), Callback.Make("tx", trip.Id)));
        last.Add(Kb.Button(ctx.T("Btn_Refresh"), "tr"));
        rows.Add([.. last]);
        if (editMessageId is long mid) outbox.Edit(ctx.Tenant.Id, ctx.ChatId, mid, text, Kb.Inline(rows));
        else outbox.Send(ctx.Tenant.Id, ctx.ChatId, text, Kb.Inline(rows));
    }

    internal static string TripText(string lang, ErpTrip t) =>
        Texts.Get(lang, "Trip_Head", t.DocNo, t.Date, t.StatusName, t.Vehicle, Texts.Qty(t.WeightKg), Texts.Money(t.Total),
            t.Stops.Count(s => s.Status != 0), t.Stops.Count);

    internal static string StopButton(ErpStop s)
    {
        var mark = s.Status switch { 1 => "✅", 2 => "🟡", 3 => "🚫", _ => "⏳" };
        var label = mark + " " + s.Seq + ". " + s.Customer + " · " + Texts.Money(s.Amount);
        return label.Length <= 60 ? label : label[..59] + "…";
    }

    private async Task<ErpTripStop> StopAsync(Ctx ctx, int stopId, CancellationToken ct) =>
        await ctx.Erp.GetAsync<ErpTripStop>("TripStop", ctx.UserId, ct, ("id", stopId)) ?? throw new ErpException(404, "Manzil topilmadi");

    private async Task SendStopAsync(Ctx ctx, int stopId, CancellationToken ct)
    {
        var x = await StopAsync(ctx, stopId, ct);
        var s = x.Stop;
        var text = ctx.T("Stop_Card", s.Seq, s.Customer, JoinNonEmpty(s.Address, s.Landmark), s.Phone ?? "—", s.OrderNo, s.ShipmentNo,
            Texts.Money(s.Amount), Texts.Money(s.Debt), s.StatusName + (s.Reason != null ? " (" + s.Reason + ")" : ""));
        if (s.Lines is { Count: > 0 } lines)
            text += "\n\n" + ctx.T("Stop_Lines") + "\n" + string.Join("\n", lines.Take(30).Select(l =>
                "• " + l.Product + " — " + Texts.Qty(l.Qty) + " " + l.Unit + (l.Delivered is decimal d && d != l.Qty ? " (" + Texts.Qty(d) + ")" : "")));
        var rows = new List<object[]>();
        if (s.NavUrl != null) rows.Add([Kb.Url(ctx.T("Btn_Navigate"), s.NavUrl)]);
        if (x.Trip.Status == ErpTrip.InTransit && s.Status == 0)
        {
            rows.Add([Kb.Button(ctx.T("Btn_Delivered"), Callback.Make("sd", s.Id)),
                Kb.WebApp(ctx.T("Btn_SignPartial"), DeliverAppUrl(options.Value.PublicBaseUrl, ctx.Tenant.Slug, s.Id))]);
            rows.Add([Kb.Button(ctx.T("Btn_Refused"), Callback.Make("sr", s.Id)), Kb.Button(ctx.T("Btn_Payment"), Callback.Make("sp", s.Id))]);
        }
        else rows.Add([Kb.Button(ctx.T("Btn_Payment"), Callback.Make("sp", s.Id))]);
        rows.Add([Kb.Button(ctx.T("Btn_ToTrip"), "tr")]);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, text, Kb.Inline(rows));
    }

    /// <summary>Mini App yetkazish sahifasi: imzo, qisman miqdorlar, sabab.</summary>
    internal static string DeliverAppUrl(string publicBaseUrl, string slug, int stopId) =>
        publicBaseUrl.TrimEnd('/') + "/app/deliver.html?t=" + Uri.EscapeDataString(slug) + "&s=" + stopId;

    /// <summary>"Yetkazildi" rasmi: fayl omboriga, keyin ERP'ga (to'liq yetkazish, rasm — tasdiq).</summary>
    private async Task DeliveredPhotoAsync(Ctx ctx, SessionData data, List<TgPhotoSize> photo, CancellationToken ct)
    {
        if (data.StopId is not int stopId) return;
        var key = await SaveTelegramPhotoAsync(ctx, photo, ct);
        if (key == null) return;
        var r = await ctx.Erp.PostAsync<ErpDeliverResult>("StopDeliver", new { stopId, all = true, photoKey = key }, ctx.UserId, ct);
        await sessions.ClearAsync(ctx.Tenant.Id, ctx.From.Id, ct);
        await SendMenuAsync(ctx, ctx.T("Stop_Delivered", Texts.Money(r?.DeliveredAmount ?? 0)), ct);
        await SendTripAsync(ctx, null, ct);
    }

    private async Task ExpenseAmountAsync(Ctx ctx, SessionData data, string text, CancellationToken ct)
    {
        var parts = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        // "150000 AI-92" yoki "150 000" (izohsiz)
        var amount = Amounts.Parse(text) ?? (parts.Length > 0 ? Amounts.Parse(parts[0]) : null);
        var note = Amounts.Parse(text) == null && parts.Length > 1 ? parts[1] : null;
        if (amount is not decimal a || data.TripId is not int tripId)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Pay_BadAmount"));
            return;
        }
        await ctx.Erp.PostAsync<object>("TripExpense", new { tripId, kind = data.Kind, amount = a, note }, ctx.UserId, ct);
        await sessions.ClearAsync(ctx.Tenant.Id, ctx.From.Id, ct);
        await SendMenuAsync(ctx, ctx.T("Expense_Saved", Texts.Money(a)), ct);
    }

    private async Task AskTripCloseAsync(Ctx ctx, CancellationToken ct)
    {
        if (!RequireField(ctx)) return;
        var trip = await ctx.Erp.GetAsync<ErpTrip>("MyTrip", ctx.UserId, ct);
        if (trip == null || trip.Status != ErpTrip.InTransit)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Close_NotInTransit"));
            return;
        }
        var pending = trip.Stops.Count(s => s.Status == 0);
        if (pending > 0)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Close_Pending", pending));
            await SendTripAsync(ctx, null, ct);
            return;
        }
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Close_Confirm", trip.DocNo),
            Kb.Inline([Kb.Button(ctx.T("Btn_Confirm"), Callback.Make("tc!", trip.Id)), Kb.Button(ctx.T("Btn_Cancel"), "x")]));
    }
}
