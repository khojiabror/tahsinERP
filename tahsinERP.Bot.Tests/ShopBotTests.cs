using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.Bot.Data;
using tahsinERP.Bot.Handlers;
using tahsinERP.Bot.Telegram;
using Xunit;

namespace tahsinERP.Bot.Tests;

/// <summary>Telegram do'kon: telefon bilan ro'yxatdan o'tish, mijoz menyusidagi "Do'kon" tugmasi. To'lov oqimi yo'q.</summary>
public partial class HandlerTests
{
    private static TgUpdate OwnContact(long updateId, string phone, long? contactUserId) =>
        new(updateId, new TgMessage(updateId, new TgUser(Tg, false, "Ali", "Valiyev", null, "uz"), new TgChat(Tg, "private"), 0, null, null, null, null, null,
            new TgContact(phone, "Ali", contactUserId)), null, null);

    private void ShopOpen(bool signup = true)
    {
        _routes["Account"] = _ => (HttpStatusCode.OK, "null");
        _routes["ShopPublic"] = _ => (HttpStatusCode.OK, "{\"enabled\":true,\"signup\":" + (signup ? "true" : "false") + "}");
    }

    [Fact]
    public async Task Shop_Unlinked_SignsUpWithOwnContact_ThenGetsShopMenu()
    {
        ShopOpen();
        await Handle(Text(1, "/start"));
        var welcome = Out().Last();
        Assert.Contains("telefon raqamingizni", TextOf(welcome));
        Assert.Contains("request_contact", welcome.Payload);

        _routes["ShopRegister"] = _ => (HttpStatusCode.OK, "{\"userID\":null,\"customerID\":77,\"role\":\"CUSTOMER\",\"name\":\"Ali Valiyev\",\"language\":\"uz\"}");
        await Handle(OwnContact(2, "+998901234567", Tg));
        var reg = _erp.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/ShopRegister"));
        Assert.Contains("998901234567\"", reg.Text);   // "+" JSON'da + bo'lib kodlanadi
        Assert.Contains("\"telegramUserId\":" + Tg, reg.Text);
        Assert.True(reg.Text.Contains("\"name\":\"Ali Valiyev\""), reg.Text);
        Assert.False(reg.Headers.ContainsKey("X-Acting-User"));   // mijoz — vakil xodimsiz

        var menu = Out().Last();
        Assert.Contains("ro'yxatdan o'tdingiz", TextOf(menu));
        Assert.Contains("Do'kon", menu.Payload);
        using var scope = _sp.CreateScope();
        var link = scope.ServiceProvider.GetRequiredService<BotDbContext>().Links.AsNoTracking().Single();
        Assert.Equal(77, link.CustomerId);
        Assert.Equal("CUSTOMER", link.Role);
    }

    [Fact]
    public async Task Shop_ForeignContact_Rejected_NoErpCall()
    {
        ShopOpen();
        await Handle(OwnContact(1, "+998901112233", 999));   // boshqa odamning kontakti
        Assert.DoesNotContain(_erp.Requests, r => r.Uri.AbsolutePath.EndsWith("/ShopRegister"));
        Assert.Contains("o'zingizning raqamingizni", TextOf(Out().Last()));

        await Handle(OwnContact(2, "+998901112233", null));  // user_id'siz (qo'lda yozilgan kontakt)
        Assert.DoesNotContain(_erp.Requests, r => r.Uri.AbsolutePath.EndsWith("/ShopRegister"));
    }

    [Fact]
    public async Task Shop_SignupClosed_ShowsLinkCodeText()
    {
        ShopOpen(signup: false);
        await Handle(Text(1, "/start"));
        var last = Out().Last();
        Assert.Contains("ulanish kodini", TextOf(last));
        Assert.DoesNotContain("request_contact", last.Payload);
    }

    [Fact]
    public async Task Shop_CustomerButton_OpensShopMiniApp()
    {
        ShopOpen();
        _routes["Link"] = _ => (HttpStatusCode.OK, "{\"userID\":null,\"customerID\":77,\"role\":\"CUSTOMER\",\"name\":\"Baraka\",\"language\":\"uz\"}");
        await Handle(Text(1, "/start ABCDEFGH23"));
        Assert.Contains("Do'kon", Out().Last().Payload);

        await Handle(Text(2, "🛒 Do'kon"));
        var open = Out().Last();
        Assert.Contains("web_app", open.Payload);
        Assert.Contains("https://bot.example.uz/app/shop.html?t=firma1", open.Payload);
        Assert.Equal("https://bot.example.uz/app/shop.html?t=firma1", UpdateRouter.ShopAppUrl("https://bot.example.uz/", "firma1"));
    }

    [Fact]
    public async Task Shop_Closed_CustomerMenuWithoutShopButton()
    {
        _routes["Account"] = _ => (HttpStatusCode.OK, "null");
        _routes["ShopPublic"] = _ => (HttpStatusCode.OK, "{\"enabled\":false,\"signup\":false}");
        _routes["Link"] = _ => (HttpStatusCode.OK, "{\"userID\":null,\"customerID\":77,\"role\":\"CUSTOMER\",\"name\":\"Baraka\",\"language\":\"uz\"}");
        await Handle(Text(1, "/start ABCDEFGH23"));
        var menu = Out().Last();
        Assert.DoesNotContain("Do'kon", menu.Payload);
        Assert.Contains("Qarz", menu.Payload, StringComparison.OrdinalIgnoreCase);
    }
}
