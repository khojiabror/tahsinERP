using tahsinERP.Bot.Erp;
using tahsinERP.Bot.Telegram;

namespace tahsinERP.Bot.Handlers;

/// <summary>
/// Telegram do'kon: mijoz Mini App'da o'zi buyurtma beradi (ERP: DstApi Shop*). Bog'lanmagan foydalanuvchi — firma ruxsat bergan
/// bo'lsa — telefon raqamini (o'z kontaktini) yuborib ro'yxatdan o'tadi. Onlayn to'lov yo'q: bot ham, Mini App ham pul qabul qilmaydi.
/// </summary>
public sealed partial class UpdateRouter
{
    /// <summary>Do'kon Mini App'i: {PublicBaseUrl}/app/shop.html?t=slug.</summary>
    internal static string ShopAppUrl(string publicBaseUrl, string slug) =>
        publicBaseUrl.TrimEnd('/') + "/app/shop.html?t=" + Uri.EscapeDataString(slug);

    /// <summary>ERP'dagi do'kon holati; ERP javob bermasa — yopiq deb olinadi (menyu baribir chiqadi).</summary>
    private async Task<ErpShopPublic> ShopStateAsync(Ctx ctx, CancellationToken ct)
    {
        try
        {
            return await ctx.Erp.GetAsync<ErpShopPublic>("ShopPublic", null, ct) ?? new ErpShopPublic(false, false);
        }
        catch (Exception ex) when (ex is ErpException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "ShopPublic: tenant {Slug}", ctx.Tenant.Slug);
            return new ErpShopPublic(false, false);
        }
    }

    /// <summary>Bog'lanmagan foydalanuvchi: ro'yxatdan o'tish ochiq bo'lsa — kontakt so'raladi, aks holda ulash kodi haqida matn.</summary>
    private async Task WelcomeUnlinkedAsync(Ctx ctx, CancellationToken ct)
    {
        var lang = LangOf(ctx.From);
        var shop = await ShopStateAsync(ctx, ct);
        if (!shop.Signup)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, Texts.Get(lang, "Welcome_Unlinked"));
            return;
        }
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, Texts.Get(lang, "Shop_Welcome"),
            Kb.Reply([[Kb.Contact(Texts.Get(lang, "Btn_SharePhone"))]], oneTime: true));
    }

    /// <summary>Kontakt bilan ro'yxatdan o'tish: faqat o'z raqami (Telegram kontaktidagi user_id = yuboruvchi).</summary>
    private async Task ShopSignupAsync(Ctx ctx, TgContact contact, CancellationToken ct)
    {
        var lang = LangOf(ctx.From);
        if (contact.UserId != ctx.From.Id)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, Texts.Get(lang, "Shop_ContactNotOwn"),
                Kb.Reply([[Kb.Contact(Texts.Get(lang, "Btn_SharePhone"))]], oneTime: true));
            return;
        }
        var name = string.Join(" ", new[] { ctx.From.FirstName, ctx.From.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        var account = await ctx.Erp.PostAsync<ErpAccount>("ShopRegister", new
        {
            telegramUserId = ctx.From.Id, chatId = ctx.ChatId, phone = contact.PhoneNumber,
            name = name.Length == 0 ? contact.FirstName : name, language = lang
        }, null, ct) ?? throw new ErpException(500, "Bo'sh javob");
        SaveLink(ctx, account);
        await sessions.ClearAsync(ctx.Tenant.Id, ctx.From.Id, ct);
        await SendMenuAsync(ctx, ctx.T("Shop_Registered", account.Name), ct);
    }

    private void SendShopButton(Ctx ctx) =>
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Shop_Open"),
            Kb.Inline([Kb.WebApp(ctx.T("Btn_ShopApp"), ShopAppUrl(options.Value.PublicBaseUrl, ctx.Tenant.Slug))]));
}
