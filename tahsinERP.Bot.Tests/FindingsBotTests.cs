using System.Net;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.Bot.Data;

namespace tahsinERP.Bot.Tests;

/// <summary>05_FINDINGS.md tuzatishlari (bot va Mini App tomoni).</summary>
public partial class HandlerTests
{
    [Fact]
    public async Task K2_24_LinkSendsTelegramLanguage_StartRefreshesStaffLanguage()
    {
        LinkAs("AGENT");
        Assert.Contains("\"language\":\"uz\"", _erp.Requests.Single(r => r.Uri.AbsolutePath.EndsWith("/Link")).Text);

        // ERP'da til ruschaga o'zgardi — qayta ulashsiz /start bilan yangilanadi
        _routes["Account"] = _ => (HttpStatusCode.OK, "{\"userID\":12,\"role\":\"AGENT\",\"name\":\"Ali Agent\",\"language\":\"ru\"}");
        await Handle(Text(2, "/start"));
        Assert.Contains("Главное меню", TextOf(Out().Last()));
        Assert.Contains("language=", _erp.Requests.Last(r => r.Uri.AbsolutePath.EndsWith("/Account")).Uri.Query);
        using var scope = _sp.CreateScope();
        Assert.Equal("ru", scope.ServiceProvider.GetRequiredService<BotDbContext>().Links.Single().Language);
    }

    [Fact]
    public async Task K3_35_Voice_ExpeditorRefused()
    {
        LinkAs("EXPEDITOR");
        _routes["DeliveryMe"] = _ => (HttpStatusCode.OK, "{\"isAgent\":false,\"voiceOrders\":true}");
        await Handle(VoiceMsg(2));
        Assert.Contains("faqat agentlar", TextOf(Out().Last()));
        Assert.Equal(0, _voice.Calls);
    }
}

/// <summary>Mini App: savat va yig'ish natijasi oyna yopilsa yo'qolmaydi (K2-23) — JS manbasi bo'yicha.</summary>
public class MiniAppStorageTests
{
    private static string Read(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "tahsinERP.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, "tahsinERP.Bot", "wwwroot", "app", file));
    }

    [Fact]
    public void K2_23_CartAndPicking_InLocalStorage()
    {
        var app = Read("app.js");
        Assert.Contains("localStorage.setItem(cartKey", app);
        Assert.DoesNotContain("sessionStorage.setItem", app);
        var store = Read("store.js");
        Assert.Contains("savePicked()", store);
        Assert.Contains("state.picked = loadPicked(doc)", store);
        Assert.Contains("clearPicked(shippedId)", store);
    }
}
