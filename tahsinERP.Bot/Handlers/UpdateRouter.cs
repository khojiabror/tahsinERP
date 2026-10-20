using Microsoft.Extensions.Options;
using tahsinERP.Bot.Data;
using tahsinERP.Bot.Erp;
using tahsinERP.Bot.Files;
using tahsinERP.Bot.Pipeline;
using tahsinERP.Bot.Telegram;

namespace tahsinERP.Bot.Handlers;

/// <summary>
/// Bitta update'ga ishlov: bog'lanish (/start KOD), rol bo'yicha menyu, suhbat holati, tugmalar.
/// Barcha javoblar Outbox orqali; ERP biznes xatosi (400/409) foydalanuvchiga matn sifatida ko'rsatiladi.
/// ERP texnik xatosida update qayta ishlanmaydi (amal ikki marta bajarilmasin) — foydalanuvchiga "aloqa yo'q".
/// </summary>
public sealed partial class UpdateRouter(BotDbContext db, Outbox outbox, SessionStore sessions, ErpClientFactory erpFactory,
    TelegramApi telegram, IFileStore files, IOptions<BotOptions> options, TimeProvider clock, ILogger<UpdateRouter> log)
{
    /// <summary>Bitta update konteksti.</summary>
    private sealed class Ctx
    {
        public required TenantRegistry.Tenant Tenant { get; init; }
        public required TgUser From { get; init; }
        public required long ChatId { get; init; }
        public required ErpClient Erp { get; init; }
        public BotLink? Link { get; set; }
        public string Lang => Link?.Language ?? Texts.Uz;
        public string T(string key, params object?[] args) => Texts.Get(Lang, key, args);
        public int? UserId => Link?.UserId;
        public bool IsField => Link is { UserId: not null, Role: "AGENT" or "EXPEDITOR" };
        public bool IsManager => Link is { UserId: not null, Role: "MANAGER" };
    }

    public async Task HandleAsync(TenantRegistry.Tenant tenant, TgUpdate update, CancellationToken ct)
    {
        var from = update.From;
        var chatId = update.ChatId;
        if (from == null || chatId == null || from.IsBot) return;
        if (update.EditedMessage != null) return;                           // jonli joylashuv yangilanishlari — hozircha kerak emas
        var chatType = update.Message?.Chat.Type ?? update.CallbackQuery?.Message?.Chat.Type;
        if (chatType != null && chatType != "private") return;              // guruhlarda ishlamaydi

        var ctx = new Ctx { Tenant = tenant, From = from, ChatId = chatId.Value, Erp = erpFactory.For(tenant.Row) };
        ctx.Link = await db.Links.FindAsync([tenant.Id, from.Id], ct);

        try
        {
            if (update.CallbackQuery is { } cq)
            {
                outbox.AnswerCallback(tenant.Id, ctx.ChatId, cq.Id);
                await OnCallbackAsync(ctx, cq, ct);
            }
            else if (update.Message is { } msg)
            {
                await OnMessageAsync(ctx, msg, ct);
            }
        }
        catch (ErpException ex) when (ex.IsBusiness)
        {
            var text = "⚠️ " + ex.Message + (ex.Problems.Count > 0 ? "\n" + string.Join("\n", ex.Problems) : "");
            outbox.Send(tenant.Id, ctx.ChatId, text);
        }
        catch (ErpException ex) when (ex.Status == 403 && ctx.Link != null)
        {
            log.LogWarning("ERP 403 ({Message}) tenant {Slug}, tg {User}", ex.Message, tenant.Slug, from.Id);
            outbox.Send(tenant.Id, ctx.ChatId, ctx.T("NotAllowed"));
        }
        catch (Exception ex) when (ex is ErpException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "ERP bilan aloqa xatosi, tenant {Slug}", tenant.Slug);
            outbox.Send(tenant.Id, ctx.ChatId, ctx.T("Error_Erp"));
        }
        await db.SaveChangesAsync(ct);
        outbox.Flush();
    }

    // ---------- xabarlar ----------

    private async Task OnMessageAsync(Ctx ctx, TgMessage msg, CancellationToken ct)
    {
        var text = msg.Text?.Trim();
        if (text != null && (text == "/start" || text.StartsWith("/start ", StringComparison.Ordinal)))
        {
            var code = text.Length > 7 ? text[7..].Trim() : "";
            if (code.Length > 0) await LinkAsync(ctx, code, ct);
            else if (await EnsureLinkAsync(ctx, ct)) await SendMenuAsync(ctx, ctx.T("Menu"), ct);
            else outbox.Send(ctx.Tenant.Id, ctx.ChatId, Texts.Get(LangOf(ctx.From), "Welcome_Unlinked"));
            return;
        }
        if (!await EnsureLinkAsync(ctx, ct))
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, Texts.Get(LangOf(ctx.From), "Welcome_Unlinked"));
            return;
        }
        if (text == "/stop")
        {
            await UnlinkAsync(ctx, ct);
            return;
        }

        var (state, data) = await sessions.GetAsync(ctx.Tenant.Id, ctx.From.Id, ct);
        if (text == "/menu" || text != null && Texts.KeyOf(text, "Btn_Cancel") != null)
        {
            await sessions.ClearAsync(ctx.Tenant.Id, ctx.From.Id, ct);
            await SendMenuAsync(ctx, ctx.T(state == null ? "Menu" : "Cancelled"), ct);
            return;
        }

        if (msg.Location is { } loc)
        {
            await OnLocationAsync(ctx, state, data, loc, ct);
            return;
        }
        if (msg.Photo is { Count: > 0 } photo)
        {
            if (state == States.Photo && data.VisitId is int visitId) await SavePhotoAsync(ctx, visitId, photo, ct);
            return;
        }
        if (text == null) return;

        switch (Texts.KeyOf(text, "Btn_DayStart", "Btn_Route", "Btn_Search", "Btn_Cash", "Btn_DayEnd", "Btn_Summary"))
        {
            case "Btn_DayStart": await AskDayLocationAsync(ctx, States.DayStart, "Day_AskLocation", ct); return;
            case "Btn_DayEnd": await AskDayLocationAsync(ctx, States.DayEnd, "Day_AskEndLocation", ct); return;
            case "Btn_Route": await SendRouteAsync(ctx, null, ct); return;
            case "Btn_Search":
                if (!RequireField(ctx)) return;
                await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.Search, null, ct);
                outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Search_Ask"), CancelKeyboard(ctx));
                return;
            case "Btn_Cash": await SendCashAsync(ctx, ct); return;
            case "Btn_Summary": await SendSummaryAsync(ctx, ct); return;
        }

        switch (state)
        {
            case States.Search:
                await SearchAsync(ctx, text, ct);
                return;
            case States.PayAmount:
                await PaymentAmountAsync(ctx, data, text, ct);
                return;
            default:
                await SendMenuAsync(ctx, ctx.T("Menu"), ct);
                return;
        }
    }

    // ---------- bog'lash ----------

    private async Task LinkAsync(Ctx ctx, string code, CancellationToken ct)
    {
        if (code.Length > 32)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, Texts.Get(LangOf(ctx.From), "Welcome_Unlinked"));
            return;
        }
        var account = await ctx.Erp.PostAsync<ErpAccount>("Link", new { code, telegramUserId = ctx.From.Id, chatId = ctx.ChatId }, null, ct)
                      ?? throw new ErpException(500, "Bo'sh javob");
        SaveLink(ctx, account);
        await sessions.ClearAsync(ctx.Tenant.Id, ctx.From.Id, ct);
        await SendMenuAsync(ctx, ctx.T("Linked", account.Name, ctx.T("Role_" + account.Role)), ct);
    }

    /// <summary>Bog'lanish bot bazasida bo'lmasa — ERP'dan tiklanadi (bot bazasi yo'qolgan/yangi server).</summary>
    private async Task<bool> EnsureLinkAsync(Ctx ctx, CancellationToken ct)
    {
        if (ctx.Link != null) return true;
        var account = await ctx.Erp.GetAsync<ErpAccount>("Account", null, ct, ("telegramUserId", ctx.From.Id));
        if (account == null) return false;
        SaveLink(ctx, account);
        return true;
    }

    private void SaveLink(Ctx ctx, ErpAccount a)
    {
        var link = ctx.Link;
        if (link == null)
        {
            link = new BotLink { TenantId = ctx.Tenant.Id, TelegramUserId = ctx.From.Id };
            db.Links.Add(link);
        }
        link.ChatId = ctx.ChatId;
        link.UserId = a.UserId;
        link.CustomerId = a.CustomerId;
        link.Role = a.Role;
        link.Name = a.Name;
        link.Language = a.UserId.HasValue ? (a.Language == Texts.Ru ? Texts.Ru : Texts.Uz) : LangOf(ctx.From);
        link.LinkedAt = clock.GetUtcNow().UtcDateTime;
        ctx.Link = link;
    }

    private async Task UnlinkAsync(Ctx ctx, CancellationToken ct)
    {
        await ctx.Erp.PostAsync<object>("Unlink", new { telegramUserId = ctx.From.Id }, null, ct);
        var lang = ctx.Lang;
        if (ctx.Link != null) db.Links.Remove(ctx.Link);
        await sessions.ClearAsync(ctx.Tenant.Id, ctx.From.Id, ct);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, Texts.Get(lang, "Unlinked"), Kb.Remove());
    }

    private static string LangOf(TgUser u) => u.LanguageCode?.StartsWith("ru", StringComparison.OrdinalIgnoreCase) == true ? Texts.Ru : Texts.Uz;

    // ---------- menyu ----------

    private Task SendMenuAsync(Ctx ctx, string text, CancellationToken ct)
    {
        object? keyboard;
        if (ctx.IsField)
            keyboard = Kb.Reply([
                [Kb.Text(ctx.T("Btn_DayStart")), Kb.Text(ctx.T("Btn_Route"))],
                [Kb.Text(ctx.T("Btn_Search")), Kb.Text(ctx.T("Btn_Cash"))],
                [Kb.Text(ctx.T("Btn_DayEnd"))]
            ]);
        else if (ctx.IsManager)
            keyboard = Kb.Reply([[Kb.Text(ctx.T("Btn_Summary"))]]);
        else
        {
            keyboard = Kb.Remove();
            text += "\n" + ctx.T(ctx.Link?.CustomerId != null ? "Customer_Menu" : "Staff_Menu");
        }
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, text, keyboard);
        return Task.CompletedTask;
    }

    private object CancelKeyboard(Ctx ctx) => Kb.Reply([[Kb.Text(ctx.T("Btn_Cancel"))]], oneTime: true);

    private object LocationKeyboard(Ctx ctx) =>
        Kb.Reply([[Kb.Location(ctx.T("Btn_SendLocation"))], [Kb.Text(ctx.T("Btn_Cancel"))]], oneTime: true);

    private bool RequireField(Ctx ctx)
    {
        if (ctx.IsField) return true;
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("NotAllowed"));
        return false;
    }

    private bool RequireManager(Ctx ctx)
    {
        if (ctx.IsManager) return true;
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("NotAllowed"));
        return false;
    }

    /// <summary>Mini App havolasi: {PublicBaseUrl}/app/?t=slug&amp;c=mijoz&amp;v=tashrif.</summary>
    internal static string MiniAppUrl(string publicBaseUrl, string slug, int customerId, int? visitId) =>
        publicBaseUrl.TrimEnd('/') + "/app/?t=" + Uri.EscapeDataString(slug) + "&c=" + customerId + (visitId.HasValue ? "&v=" + visitId : "");
}
