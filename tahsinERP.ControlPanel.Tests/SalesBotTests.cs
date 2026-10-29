using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tahsinERP.ControlPanel.Bot;
using tahsinERP.ControlPanel.Data;

namespace tahsinERP.ControlPanel.Tests;

/// <summary>Sotuv boti matnlari, qo'llanma bilim bazasi va qidiruv (bazasiz).</summary>
public class SalesBotContentTests
{
    [Fact]
    public void Texts_SameKeys_InAllLanguages()
    {
        var keys = typeof(BotContent).Assembly.GetManifestResourceNames().Where(n => n.Contains(".Bot.Content.bot.")).ToList();
        Assert.Equal(2, keys.Count);
        foreach (var key in new[] { "welcome", "menu", "demo.done", "consult.done", "support.done", "thread.staffReply", "help", "cmd.demo" })
            foreach (var lang in BotContent.Languages)
                Assert.True(BotContent.HasKey(lang, key), $"{lang}: {key}");
        var uz = Load("uz");
        var ru = Load("ru");
        Assert.Empty(uz.Keys.Except(ru.Keys));
        Assert.Empty(ru.Keys.Except(uz.Keys));
        // Format o'rinlari bir xil ({0}, {1} ...)
        foreach (var k in uz.Keys)
            Assert.Equal(Placeholders(uz[k]), Placeholders(ru[k]));
    }

    private static Dictionary<string, string> Load(string lang)
    {
        using var s = typeof(BotContent).Assembly.GetManifestResourceStream($"tahsinERP.ControlPanel.Bot.Content.bot.{lang}.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(s)!;
    }

    private static string Placeholders(string s) =>
        string.Join(",", System.Text.RegularExpressions.Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).Distinct().Order());

    [Fact]
    public void Cyrillic_IsTransliterated_TagsAndLinksKept()
    {
        string oz = BotContent.T("oz", "welcome", "Ali");
        Assert.Contains("<b>tahsinERP</b>", oz);
        Assert.Contains("Ассалому алайкум", oz);
        Assert.Contains("Ali", oz);
        Assert.Contains("tahsinerp.uz", BotContent.T("oz", "support.unavailable"));
        Assert.Contains("/menu", BotContent.T("oz", "help"));
        Assert.Equal(BotContent.Modules("uz").Count, BotContent.Modules("oz").Count);
    }

    [Fact]
    public void KnowledgeBase_AllModules_PagesFitTelegram()
    {
        foreach (var lang in BotContent.Languages)
        {
            var mods = BotContent.Modules(lang);
            Assert.Equal(14, mods.Count);
            Assert.All(mods, m => Assert.NotEmpty(m.Summary));
            Assert.True(mods.Sum(m => m.Sections.Count) > 300, lang);
            foreach (var s in mods.SelectMany(m => m.Sections))
                foreach (var page in SalesBot.Paginate(s.Body, 3300))
                {
                    Assert.True(page.Length <= 3300, $"{lang} {s.Num}: {page.Length}");
                    Assert.Equal(Count(page, "<b>"), Count(page, "</b>"));
                }
        }
    }

    private static int Count(string s, string what) => (s.Length - s.Replace(what, "").Length) / what.Length;

    [Theory]
    [InlineData("uz", "inventarizatsiya qanday qilinadi", "05")]
    [InlineData("uz", "tannarx kalkulyatsiyasi", "03")]
    [InlineData("ru", "инвентаризация склада", "05")]
    [InlineData("ru", "расчёт заработной платы", "09")]
    public void Search_FindsRelevantModule(string lang, string query, string module)
    {
        var hits = BotSearch.Search(lang, query);
        Assert.NotEmpty(hits);
        Assert.Contains(hits.Take(3), h => h.Module == module);
    }

    [Fact]
    public void Search_FaqAndNothing()
    {
        Assert.True(BotSearch.Search("uz", "demo qanday ishlaydi")[0].IsFaq);
        Assert.Empty(BotSearch.Search("uz", "qwzxv jjkkll"));
        Assert.Empty(BotSearch.Search("uz", "va bilan uchun"));
    }
}

/// <summary>Bot oqimlari webhook orqali (ProcessInline): demo so'rovi, xodim javobi, texnik yordam murojaati, baho.</summary>
public class SalesBotFlowTests : IClassFixture<PanelFactory>
{
    private const long SalesGroup = -500100;
    private const long SupportGroup = -500200;
    private readonly PanelFactory f;
    private static long _updateId = 100_000;
    private static long _messageId = 50;

    public SalesBotFlowTests(PanelFactory factory) => f = factory;

    private WebApplicationFactory<Program> App() =>
        f.WithWebHostBuilder(b => b.UseSetting("ControlPanel:Telegram:StaffChatId", SalesGroup.ToString()));

    private static async Task PostAsync(WebApplicationFactory<Program> app, object update)
    {
        using var c = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/telegram/webhook")
        {
            Content = new StringContent(JsonSerializer.Serialize(update), System.Text.Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-Telegram-Bot-Api-Secret-Token", PanelFactory.WebhookSecret);
        var r = await c.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    private static object From(long id) => new { id, is_bot = false, first_name = "Ali", last_name = "Valiyev", username = "ali_v", language_code = "uz" };

    private static Task TextAsync(WebApplicationFactory<Program> app, long chat, string text, object? extra = null) => PostAsync(app, new
    {
        update_id = Interlocked.Increment(ref _updateId),
        message = Merge(new Dictionary<string, object?>
        {
            ["message_id"] = Interlocked.Increment(ref _messageId),
            ["chat"] = new { id = chat, type = "private" },
            ["from"] = From(chat),
            ["text"] = text,
        }, extra),
    });

    private static Dictionary<string, object?> Merge(Dictionary<string, object?> d, object? extra)
    {
        if (extra is not null)
            foreach (var p in extra.GetType().GetProperties())
                d[p.Name] = p.GetValue(extra);
        return d;
    }

    private static Task ButtonAsync(WebApplicationFactory<Program> app, long chat, string data) => PostAsync(app, new
    {
        update_id = Interlocked.Increment(ref _updateId),
        callback_query = new
        {
            id = "cb" + _updateId, data, from = From(chat),
            message = new { message_id = 1, chat = new { id = chat, type = "private" }, text = "menu" },
        },
    });

    private static Task GroupAsync(WebApplicationFactory<Program> app, long group, long replyTo, string text) => PostAsync(app, new
    {
        update_id = Interlocked.Increment(ref _updateId),
        message = new
        {
            message_id = Interlocked.Increment(ref _messageId),
            chat = new { id = group, type = "supergroup", title = "tahsinERP test" },
            from = new { id = 9, is_bot = false, first_name = "Xodim" },
            reply_to_message = new { message_id = replyTo },
            text,
        },
    });

    private Task<T> Db<T>(Func<ControlDbContext, Task<T>> q) => f.WithScopeAsync(sp => q(sp.GetRequiredService<ControlDbContext>()));

    [Fact]
    public async Task Demo_FromStartToSalesGroup_StaffReplyRelayed()
    {
        const long chat = 7001;
        using var app = App();
        await TextAsync(app, chat, "/start");
        Assert.Contains(f.Bot.To(chat), m => m.Text.Contains("Tilni tanlang"));
        await ButtonAsync(app, chat, "lang:uz");
        Assert.Contains(f.Bot.To(chat), m => m.Method == "editMessageText" && m.Text.Contains("Assalomu alaykum, Ali"));

        await ButtonAsync(app, chat, "demo");
        Assert.Contains(f.Bot.To(chat), m => m.Text.Contains("Ismingiz"));
        await TextAsync(app, chat, "Ali Valiyev");
        await TextAsync(app, chat, "Test Savdo MChJ");
        await TextAsync(app, chat, "salom");   // noto'g'ri raqam — qayta so'raladi
        Assert.Contains(f.Bot.To(chat), m => m.Text.Contains("Raqam noto'g'ri"));
        await PostAsync(app, new
        {
            update_id = Interlocked.Increment(ref _updateId),
            message = new
            {
                message_id = Interlocked.Increment(ref _messageId), chat = new { id = chat, type = "private" }, from = From(chat),
                contact = new { phone_number = "998901234567", user_id = chat, first_name = "Ali" },
            },
        });
        await TextAsync(app, chat, "🏭 Ishlab chiqarish");
        Assert.Contains(f.Bot.To(chat), m => m.Text.Contains("Demo so'rovingiz qabul qilindi"));

        var req = await Db(db => db.DemoRequests.AsNoTracking().SingleAsync(r => r.TelegramChatId == chat));
        Assert.Equal(DemoRequestSources.Telegram, req.Source);
        Assert.Equal(DemoRequestKind.Demo, req.Kind);
        Assert.Equal("production", req.Industry);
        Assert.Equal("+998901234567", req.Phone);
        Assert.Equal("Test Savdo MChJ", req.Company);
        Assert.NotNull(req.StaffNotifiedAtUtc);
        var groupMsg = Assert.Single(f.Bot.To(SalesGroup), m => m.Text.Contains("Demo so'rovi #" + req.Id));
        Assert.Contains("tg://user?id=7001", groupMsg.Text);

        // Xodim guruhda reply qiladi — mijozga yetadi, so'rov «Bog'lanildi».
        var thread = await Db(db => db.BotThreads.AsNoTracking().SingleAsync(t => t.DemoRequestId == req.Id));
        await GroupAsync(app, SalesGroup, thread.GroupMessageId!.Value, "Ertaga soat 10 da qo'ng'iroq qilamiz");
        Assert.Contains(f.Bot.To(chat), m => m.Text.Contains("Ertaga soat 10 da qo&#39;ng&#39;iroq") || m.Text.Contains("Ertaga soat 10 da qo'ng'iroq"));
        Assert.Equal(DemoRequestStatus.Contacted, (await Db(db => db.DemoRequests.AsNoTracking().SingleAsync(r => r.Id == req.Id))).Status);

        // Mijoz «Javob yozish» orqali yozadi — guruhga sarlavhaga reply bo'lib boradi.
        await ButtonAsync(app, chat, "thr:" + thread.Id);
        await TextAsync(app, chat, "Rahmat, kutaman");
        var back = f.Bot.To(SalesGroup).Last(m => m.Method == "sendMessage");
        Assert.Contains("Rahmat, kutaman", back.Text);
        Assert.Equal(thread.GroupMessageId, back.Payload["reply_parameters"]?["message_id"]?.GetValue<long>());

        // Takroriy demo so'rovi 30 daqiqa ichida — yangi yozuv yaratilmaydi.
        await ButtonAsync(app, chat, "menu");
        await ButtonAsync(app, chat, "demo");
        await TextAsync(app, chat, "Ali");
        await TextAsync(app, chat, "Test Savdo MChJ");
        await TextAsync(app, chat, "+998 90 123 45 67");
        await TextAsync(app, chat, "⏭ O'tkazib yuborish");
        Assert.Equal(1, await Db(db => db.DemoRequests.CountAsync(r => r.TelegramChatId == chat)));
        Assert.Contains(f.Bot.To(chat), m => m.Text.Contains("allaqachon qabul qilingan"));
    }

    [Fact]
    public async Task Support_TicketToSupportGroup_CloseAndRate()
    {
        const long chat = 7002;
        using var app = App();
        await f.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ControlDbContext>();
            (await db.BotSettings.SingleAsync()).SupportChatId = SupportGroup;
            await db.SaveChangesAsync();
        });
        app.Services.GetRequiredService<BotConfig>().Invalidate();

        await TextAsync(app, chat, "/start support");
        await ButtonAsync(app, chat, "lang:ru");     // til tanlangach — to'g'ridan-to'g'ri yordam oqimi
        Assert.Contains(f.Bot.To(chat), m => m.Text.Contains("Техподдержка"));
        await TextAsync(app, chat, "ipack.tahsinerp.uz");
        await TextAsync(app, chat, "Ошибка при сохранении накладной");
        var thread = await Db(db => db.BotThreads.AsNoTracking().SingleAsync(t => t.ChatId == chat && t.Kind == BotThreadKind.Support));
        Assert.Equal("ipack.tahsinerp.uz", thread.Company);
        Assert.Contains(f.Bot.To(SupportGroup), m => m.Text.Contains("Murojaat №" + thread.Id) && m.Text.Contains("Ошибка при сохранении"));

        // Qo'shimcha skrinshot — guruhga nusxa (copyMessage) sarlavhaga reply.
        await PostAsync(app, new
        {
            update_id = Interlocked.Increment(ref _updateId),
            message = new
            {
                message_id = Interlocked.Increment(ref _messageId), chat = new { id = chat, type = "private" }, from = From(chat),
                photo = new[] { new { file_id = "x", width = 10, height = 10 } }, caption = "скрин",
            },
        });
        Assert.Contains(f.Bot.To(SupportGroup), m => m.Method == "copyMessage");

        // Xodim javobi va /close — mijozdan baho so'raladi.
        await GroupAsync(app, SupportGroup, thread.GroupMessageId!.Value, "Исправили, обновите страницу");
        Assert.Contains(f.Bot.To(chat), m => m.Text.Contains("Исправили"));
        await GroupAsync(app, SupportGroup, thread.GroupMessageId!.Value, "/close");
        Assert.Contains(f.Bot.To(chat), m => m.Text.Contains("закрыто"));
        await ButtonAsync(app, chat, $"rate:{thread.Id}:5");
        var closed = await Db(db => db.BotThreads.AsNoTracking().SingleAsync(t => t.Id == thread.Id));
        Assert.Equal(BotThreadStatus.Closed, closed.Status);
        Assert.Equal(5, closed.Rating);
        Assert.Equal("Xodim", closed.ClosedBy);
    }

    [Fact]
    public async Task FreeText_SearchesManual_AndMenusRender()
    {
        const long chat = 7003;
        using var app = App();
        await TextAsync(app, chat, "/start");
        await ButtonAsync(app, chat, "lang:oz");
        await ButtonAsync(app, chat, "lang:uz");
        await TextAsync(app, chat, "inventarizatsiya");
        var answer = f.Bot.To(chat).Last(m => m.Method == "sendMessage");
        Assert.Contains("🔎", answer.Text);
        Assert.Contains("sec:05:", answer.Payload.ToJsonString());

        foreach (var data in new[] { "about", "about:process", "about:dist", "mods", "mod:07", "mod:07:1", "sec:07:3", "plans", "faq", "faq:0", "ask", "lang" })
        {
            int before = f.Bot.To(chat).Count;
            await ButtonAsync(app, chat, data);
            var screen = f.Bot.To(chat).Skip(before).Last(m => m.Method is "editMessageText" or "sendMessage");
            Assert.False(string.IsNullOrWhiteSpace(screen.Text), data);
            Assert.True(screen.Text.Length <= 4096, $"{data}: {screen.Text.Length}");
            foreach (var row in screen.Payload["reply_markup"]?["inline_keyboard"]?.AsArray() ?? [])
                foreach (var b in row!.AsArray())
                    if (b?["callback_data"]?.GetValue<string>() is { } cd)
                        Assert.True(System.Text.Encoding.UTF8.GetByteCount(cd) <= 64, cd);
        }
    }

    [Fact]
    public async Task Group_IdCommand_And_MembershipRegistersGroup()
    {
        using var app = App();
        await PostAsync(app, new
        {
            update_id = Interlocked.Increment(ref _updateId),
            my_chat_member = new
            {
                chat = new { id = -500300, type = "group", title = "tahsinERP: Bugs" },
                from = new { id = 9, is_bot = false, first_name = "Owner" },
                new_chat_member = new { status = "member", user = new { id = 1, is_bot = true, first_name = "bot" } },
            },
        });
        Assert.True(await Db(db => db.BotGroups.AnyAsync(g => g.Id == -500300 && g.IsMember && g.Title == "tahsinERP: Bugs")));
        Assert.Contains(f.Bot.To(-500300), m => m.Text.Contains("-500300"));
    }
}
