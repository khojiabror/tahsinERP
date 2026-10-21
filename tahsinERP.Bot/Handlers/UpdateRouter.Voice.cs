using System.Text.Json;
using tahsinERP.Bot.Erp;
using tahsinERP.Bot.Telegram;
using tahsinERP.Bot.Voice;

namespace tahsinERP.Bot.Handlers;

/// <summary>
/// Ovozdan buyurtma (00_PLAN 4.2, Taskly g'oyasi): ovozli xabar → Groq Whisper → mijoz va qatorlar (LLM yoki qoidaviy ajratgich) →
/// ERP VoiceDraft (katalogdan tovar, birlik, narx; mijoz agent ko'rinishidan) → qoralama ko'rsatiladi → "✅ Buyurtma berish" →
/// oddiy Order (ClientRequestId — qayta bosishda ham bitta buyurtma). ERP sozlamasi VoiceOrders o'chiq yoki kalit yo'q — bot aytadi.
/// callback: vc:{mijoz} mijozni tanlash · vo! buyurtma berish.
/// </summary>
public sealed partial class UpdateRouter
{
    private sealed record VoiceState(List<ErpVoiceItem> Items, ErpVoiceDraft? Draft);

    private async Task OnVoiceAsync(Ctx ctx, TgVoice v, CancellationToken ct)
    {
        if (!RequireField(ctx)) return;
        if (ctx.IsExpeditor)   // ovozli buyurtma — agent ishi (01_PHASE1_MVP 17-bo'lim), K3-35
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Voice_AgentOnly"));
            return;
        }
        var me = await ctx.Erp.GetAsync<ErpDeliveryMe>("DeliveryMe", ctx.UserId, ct);
        if (me?.VoiceOrders != true)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Voice_Disabled"));
            return;
        }
        if (!voice.Enabled)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Voice_NoKey"));
            return;
        }
        if (v.Duration > voice.MaxSeconds || v.FileSize is > 10 * 1024 * 1024)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Voice_TooLong", voice.MaxSeconds));
            return;
        }
        byte[]? audio;
        try { audio = await telegram.DownloadFileAsync(ctx.Tenant.Token, v.FileId, ct); }
        catch (HttpRequestException ex)
        {
            log.LogWarning(ex, "Ovozli faylni yuklab bo'lmadi (tenant {Slug})", ctx.Tenant.Slug);
            audio = null;
        }
        if (audio == null || audio.Length == 0)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Photo_Failed"));
            return;
        }
        string? transcript;
        try { transcript = await voice.TranscribeAsync(audio, "voice.ogg", ctx.Lang, ct); }
        catch (HttpRequestException ex)
        {
            log.LogWarning(ex, "Nutqni aniqlash xatosi (tenant {Slug})", ctx.Tenant.Slug);
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Voice_NotUnderstood"));
            return;
        }
        if (string.IsNullOrWhiteSpace(transcript))
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Voice_NotUnderstood"));
            return;
        }
        var parsed = await voice.ExtractAsync(transcript, ctx.Lang, ct);
        if (parsed.Items.Count == 0)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, "🎤 «" + Cut(transcript, 500) + "»\n" + ctx.T("Voice_NotUnderstood"));
            return;
        }
        var items = parsed.Items.Select(i => new ErpVoiceItem(i.Name, i.Qty, i.Unit)).ToList();
        await VoiceDraftAsync(ctx, transcript, items, null, parsed.Customer, ct);
    }

    /// <summary>ERP qoralamasi va ko'rsatish (mijoz aniq bo'lmasa — tanlash yoki nomini so'rash).</summary>
    private async Task VoiceDraftAsync(Ctx ctx, string transcript, List<ErpVoiceItem> items, int? customerId, string? customerHint, CancellationToken ct)
    {
        var draft = await ctx.Erp.PostAsync<ErpVoiceDraft>("VoiceDraft",
            new { customerId, customerHint, transcript = Cut(transcript, 1000), items }, ctx.UserId, ct) ?? throw new ErpException(500, "Bo'sh javob");
        var data = new SessionData { Transcript = Cut(transcript, 1000), Json = JsonSerializer.Serialize(new VoiceState(items, draft)), RequestId = Guid.NewGuid() };
        if (draft.CustomerId == null)
        {
            if (draft.CustomerOptions is { Count: > 0 } options)
            {
                await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.VoiceConfirm, data, ct);
                var rows = options.Take(5).Select(o => new[] { Kb.Button(o.Name, Callback.Make("vc", o.Id)) }).Append([Kb.Button(ctx.T("Btn_Cancel"), "x")]);
                outbox.Send(ctx.Tenant.Id, ctx.ChatId, "🎤 «" + Cut(transcript, 500) + "»\n" + ctx.T("Voice_ChooseCustomer"), Kb.Inline(rows));
            }
            else
            {
                await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.VoiceCustomer, data, ct);
                outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Voice_AskCustomer", Cut(transcript, 500)), CancelKeyboard(ctx));
            }
            return;
        }
        data.CustomerId = draft.CustomerId;
        await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.VoiceConfirm, data, ct);
        var text = VoiceDraftText(ctx.Lang, transcript, draft);
        object keyboard = draft.Lines is { Count: > 0 }
            ? Kb.Inline([Kb.Button(ctx.T("Btn_VoiceOrder"), "vo!"), Kb.Button(ctx.T("Btn_Cancel"), "x")],
                [Kb.WebApp(ctx.T("Btn_Order"), MiniAppUrl(options.Value.PublicBaseUrl, ctx.Tenant.Slug, draft.CustomerId.Value, null))])
            : Kb.Inline([Kb.WebApp(ctx.T("Btn_Order"), MiniAppUrl(options.Value.PublicBaseUrl, ctx.Tenant.Slug, draft.CustomerId.Value, null))]);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, text, keyboard);
    }

    internal static string VoiceDraftText(string lang, string transcript, ErpVoiceDraft d)
    {
        var lines = new List<string> { Texts.Get(lang, "Voice_Draft", Cut(transcript, 500), d.Customer) };
        foreach (var l in d.Lines ?? [])
            lines.Add(Texts.Get(lang, "Voice_Line", l.Product, Texts.Qty(l.Qty), l.Unit, l.Price is decimal p ? Texts.Money(p) : "—",
                l.Amount is decimal a ? Texts.Money(a) : "—"));
        if (d.Unmatched is { Count: > 0 } u) lines.Add(Texts.Get(lang, "Voice_Unmatched", string.Join("; ", u)));
        lines.Add(d.Lines is { Count: > 0 } ? Texts.Get(lang, "Voice_Total", Texts.Money(d.Total)) : Texts.Get(lang, "Voice_NoLines"));
        return string.Join("\n", lines);
    }

    private async Task VoiceCustomerTextAsync(Ctx ctx, SessionData data, string text, CancellationToken ct)
    {
        var state = data.Json == null ? null : JsonSerializer.Deserialize<VoiceState>(data.Json);
        if (state == null) { await SendMenuAsync(ctx, ctx.T("Menu"), ct); return; }
        await VoiceDraftAsync(ctx, data.Transcript ?? "", state.Items, null, text.Length > 100 ? text[..100] : text, ct);
    }

    private async Task VoicePickCustomerAsync(Ctx ctx, int customerId, CancellationToken ct)
    {
        var (st, data) = await sessions.GetAsync(ctx.Tenant.Id, ctx.From.Id, ct);
        var state = st == States.VoiceConfirm && data.Json != null ? JsonSerializer.Deserialize<VoiceState>(data.Json) : null;
        if (state == null) { await SendMenuAsync(ctx, ctx.T("Menu"), ct); return; }
        await VoiceDraftAsync(ctx, data.Transcript ?? "", state.Items, customerId, null, ct);
    }

    /// <summary>Qoralamani buyurtma qilish (holatdagi RequestId — ClientRequestId: ERP xatosidan keyin qayta bosilsa ham bitta buyurtma).</summary>
    private async Task VoiceSubmitAsync(Ctx ctx, CancellationToken ct)
    {
        var (st, data) = await sessions.GetAsync(ctx.Tenant.Id, ctx.From.Id, ct);
        var state = st == States.VoiceConfirm && data.Json != null ? JsonSerializer.Deserialize<VoiceState>(data.Json) : null;
        if (state?.Draft is not { CustomerId: int customerId, Lines.Count: > 0 } draft || data.RequestId is not Guid rid)
        {
            await SendMenuAsync(ctx, ctx.T("Menu"), ct);
            return;
        }
        var r = await ctx.Erp.PostAsync<ErpOrderResult>("Order", new ErpOrderInput(customerId, null, "🎤 " + Cut(data.Transcript ?? "", 400), 0, rid, null, null,
            draft.Lines!.Select(l => new ErpOrderLine(l.ProductId, l.ProductUnitId, l.Qty)).ToList()), ctx.UserId, ct);
        await sessions.ClearAsync(ctx.Tenant.Id, ctx.From.Id, ct);
        await SendMenuAsync(ctx, ctx.T("Order_Accepted", r?.DocNo, Texts.Money(r?.Total ?? 0)), ct);
    }

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
