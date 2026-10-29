using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Hosting;
using tahsinERP.ControlPanel.I18n;
using tahsinERP.ControlPanel.Portal;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Bot;

/// <summary>
/// @tahsinERPBot — tahsinERP haqida ma'lumot beruvchi sotuv boti (Bot/README.md).
/// Shaxsiy chat: til tanlash → inline menyu (tizim haqida, modullar va qo'llanma bo'limlari, paketlar, FAQ), erkin savol —
/// qo'llanmadan qidiruv; oqimlar: 14 kunlik demo va konsultatsiya (DemoRequest + savdo guruhi), texnik yordam (support guruhi).
/// Guruhlar: bot xabariga reply — foydalanuvchiga yetkaziladi, /close — murojaatni yopish, /id — chat ID.
/// Portal bog'lash (<c>/start &lt;kod&gt;</c>) avvalgidek ishlaydi.
/// </summary>
public sealed class SalesBot(ControlDbContext db, IBotApi api, BotConfig config, DemoRequestService demos, PortalAuthService portal,
    IOptions<PanelHostOptions> hosts, Clock clock, ILogger<SalesBot> log)
{
    public const string SiteUrl = "https://tahsinerp.uz";
    public const int LeadsPerDay = 3;
    public const int TicketsPerDay = 10;
    public const int MessagesPerMinute = 20;
    private const int PageChars = 3300;
    private const int SectionsPerPage = 8;

    /// <summary>/start parametrlari (t.me/bot?start=demo) — boshqasi portal bog'lash kodi deb qaraladi.</summary>
    public static readonly string[] StartParams = ["demo", "consult", "support", "site", "modules", "plans"];

    private static readonly ConcurrentDictionary<long, Queue<DateTime>> RecentMessages = new();

    /// <summary>Navbat bo'limi uchun kalit: chat ID (bir chat — bir oqim).</summary>
    public static long ChatKey(JsonElement update)
    {
        foreach (var name in new[] { "message", "edited_message", "callback_query", "my_chat_member" })
            if (update.TryGetProperty(name, out var m))
            {
                var holder = name == "callback_query" && m.TryGetProperty("message", out var cm) ? cm : m;
                if (holder.TryGetProperty("chat", out var c) && c.TryGetProperty("id", out var id))
                    return Math.Abs(id.GetInt64());
                if (m.TryGetProperty("from", out var f) && f.TryGetProperty("id", out var fid))
                    return Math.Abs(fid.GetInt64());
            }
        return 0;
    }

    public async Task HandleAsync(JsonElement update, CancellationToken ct = default)
    {
        if (update.TryGetProperty("message", out var msg))
        {
            string type = msg.TryGetProperty("chat", out var chat) && chat.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            if (type == "private") await OnPrivateMessageAsync(msg, ct);
            else if (type is "group" or "supergroup") await OnGroupMessageAsync(msg, ct);
        }
        else if (update.TryGetProperty("callback_query", out var cb))
            await OnCallbackAsync(cb, ct);
        else if (update.TryGetProperty("my_chat_member", out var member))
            await OnMembershipAsync(member, ct);
    }

    // ── Shaxsiy chat ─────────────────────────────────────────────────────────────────────────────────────────────

    private async Task OnPrivateMessageAsync(JsonElement msg, CancellationToken ct)
    {
        long chatId = msg.GetProperty("chat").GetProperty("id").GetInt64();
        var from = msg.TryGetProperty("from", out var f) ? f : default;
        var chat = await TouchChatAsync(chatId, from, ct);
        string? text = Str(msg, "text");

        if (text is not null && text.StartsWith('/'))
        {
            await OnCommandAsync(chat, text, from, ct);
            return;
        }
        if (chat.Lang is null)
        {
            await SendLanguagePickerAsync(chat, ct);
            return;
        }
        string lang = chat.Lang;

        // Xodim javobiga (bot xabariga) reply — o'sha murojaatga.
        if (chat.State is null && msg.TryGetProperty("reply_to_message", out var rt) && rt.TryGetProperty("message_id", out var rtId))
        {
            long replied = rtId.GetInt64();
            var link = await db.BotThreadMessages.AsNoTracking()
                .Where(m => m.UserMessageId == replied && db.BotThreads.Any(th => th.Id == m.ThreadId && th.ChatId == chatId))
                .Select(m => (int?)m.ThreadId).FirstOrDefaultAsync(ct);
            if (link is int tid)
            {
                await ForwardToThreadAsync(chat, tid, msg, ct);
                return;
            }
        }

        if (chat.State is not null)
        {
            await OnFlowInputAsync(chat, msg, text, ct);
            return;
        }
        if (text is null)
        {
            await api.SendAsync(chatId, T(lang, "unknownMedia"), MenuButton(lang), ct: ct);
            return;
        }
        if (text == T(lang, "btn.cancel") || text == T(lang, "btn.home"))
        {
            await SendMenuAsync(chat, null, ct);
            return;
        }
        await SearchAsync(chat, text, ct);
    }

    private async Task OnCommandAsync(BotChat chat, string text, JsonElement from, CancellationToken ct)
    {
        string[] parts = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string cmd = parts[0].Split('@')[0].ToLowerInvariant();
        string? arg = parts.Length == 2 ? parts[1] : null;

        if (cmd == "/start" && arg is not null && !StartParams.Contains(arg, StringComparer.OrdinalIgnoreCase))
        {
            await LinkPortalAsync(chat, arg, from, ct);
            return;
        }
        if (cmd == "/start")
        {
            chat.StartParam ??= arg?.ToLowerInvariant();
            await ResetStateAsync(chat, ct);
            if (chat.Lang is null)
            {
                await SendLanguagePickerAsync(chat, ct, after: arg?.ToLowerInvariant());
                return;
            }
            await api.SendAsync(chat.Id, Welcome(chat), MainMenu(chat.Lang), ct: ct);
            if (arg is not null) await StartParamAsync(chat, arg.ToLowerInvariant(), ct);
            return;
        }
        if (cmd is "/id" or "/chatid")
        {
            // Xodim bu ID'ni firma kartasiga Telegram chati sifatida yozishi mumkin.
            string language = chat.Lang ?? tahsinERP.Localization.Languages.TryNormalize(Str(from, "language_code")) ?? tahsinERP.Localization.Languages.Default;
            await api.SendAsync(chat.Id, Html(L.TFor(language, "Ptl_BotInfo", chat.Id)), ct: ct);
            return;
        }
        if (chat.Lang is null)
        {
            await SendLanguagePickerAsync(chat, ct);
            return;
        }
        await ResetStateAsync(chat, ct);
        switch (cmd)
        {
            case "/demo": await StartLeadFlowAsync(chat, BotThreadKind.Demo, ct); break;
            case "/consult": await StartLeadFlowAsync(chat, BotThreadKind.Consultation, ct); break;
            case "/support": await StartSupportFlowAsync(chat, ct); break;
            case "/modules": await ShowAsync(chat, null, Modules(chat.Lang), ct); break;
            case "/plans": await ShowAsync(chat, null, Plans(chat.Lang), ct); break;
            case "/lang": await SendLanguagePickerAsync(chat, ct); break;
            case "/help": await api.SendAsync(chat.Id, T(chat.Lang, "help"), MenuButton(chat.Lang), ct: ct); break;
            case "/cancel":
                await api.SendAsync(chat.Id, T(chat.Lang, "flow.cancelled"), Kb.RemoveReply(), ct: ct);
                await SendMenuAsync(chat, null, ct);
                break;
            default: await SendMenuAsync(chat, null, ct); break;
        }
    }

    private Task StartParamAsync(BotChat chat, string arg, CancellationToken ct) => arg switch
    {
        "demo" => StartLeadFlowAsync(chat, BotThreadKind.Demo, ct),
        "consult" => StartLeadFlowAsync(chat, BotThreadKind.Consultation, ct),
        "support" => StartSupportFlowAsync(chat, ct),
        "modules" => ShowAsync(chat, null, Modules(chat.Lang!), ct),
        "plans" => ShowAsync(chat, null, Plans(chat.Lang!), ct),
        _ => Task.CompletedTask,
    };

    /// <summary>Mijoz portali: portal Profil sahifasidagi havola (t.me/bot?start=&lt;kod&gt;).</summary>
    private async Task LinkPortalAsync(BotChat chat, string code, JsonElement from, CancellationToken ct)
    {
        string language = chat.Lang ?? tahsinERP.Localization.Languages.TryNormalize(Str(from, "language_code")) ?? tahsinERP.Localization.Languages.Default;
        var user = await portal.LinkTelegramAsync(code, chat.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);
        string reply = user is null ? L.TFor(language, "Ptl_BotLinkInvalid") : L.TFor(language, "Ptl_BotLinked", user.FullName);
        await api.SendAsync(chat.Id, Html(reply), ct: ct);
        log.LogInformation("Telegram /start bog'lash: {Result}", user is null ? "rad" : "#" + user.Id);
    }

    // ── Oqimlar ──────────────────────────────────────────────────────────────────────────────────────────────────

    private static string Prefix(BotThreadKind k) => k == BotThreadKind.Demo ? "demo" : "consult";

    private async Task StartLeadFlowAsync(BotChat chat, BotThreadKind kind, CancellationToken ct)
    {
        string lang = chat.Lang!;
        var since = clock.UtcNow.AddDays(-1);
        if (await db.DemoRequests.CountAsync(r => r.TelegramChatId == chat.Id && r.CreatedAtUtc > since, ct) >= LeadsPerDay)
        {
            await api.SendAsync(chat.Id, T(lang, "limit.leads"), MenuButton(lang), ct: ct);
            return;
        }
        await api.SendAsync(chat.Id, T(lang, kind == BotThreadKind.Demo ? "demo.intro" : "consult.intro"), ct: ct);
        await SetStateAsync(chat, Prefix(kind) + ".name", new Dictionary<string, string>(), ct);
        await AskAsync(chat, "flow.name", Suggest(chat.FullName ?? Join(chat.FirstName, chat.LastName)), ct);
    }

    private async Task StartSupportFlowAsync(BotChat chat, CancellationToken ct)
    {
        string lang = chat.Lang!;
        if ((await config.GetAsync(ct)).SupportChatId is null)
        {
            await api.SendAsync(chat.Id, T(lang, "support.unavailable"), MainMenu(lang), ct: ct);
            return;
        }
        var since = clock.UtcNow.AddDays(-1);
        if (await db.BotThreads.CountAsync(t => t.ChatId == chat.Id && t.Kind == BotThreadKind.Support && t.CreatedAtUtc > since, ct) >= TicketsPerDay)
        {
            await api.SendAsync(chat.Id, T(lang, "limit.leads"), MenuButton(lang), ct: ct);
            return;
        }
        await api.SendAsync(chat.Id, T(lang, "support.intro"), ct: ct);
        await SetStateAsync(chat, "support.company", new Dictionary<string, string>(), ct);
        await AskAsync(chat, "support.company", Suggest(chat.Company), ct);
    }

    private static string[] Suggest(string? value) => string.IsNullOrWhiteSpace(value) ? [] : [value.Length > 60 ? value[..60] : value];

    /// <summary>Savol + pastki klaviatura (taklif qiymatlari, kerak bo'lsa telefon tugmasi, bekor qilish).</summary>
    private Task AskAsync(BotChat chat, string key, IEnumerable<string> suggestions, CancellationToken ct, bool contact = false, bool skip = false)
    {
        string lang = chat.Lang!;
        var rows = suggestions.Select(s => new[] { s }).ToList<IEnumerable<string>>();
        rows.Add(skip ? [T(lang, "btn.skip"), T(lang, "btn.cancel")] : [T(lang, "btn.cancel")]);
        return api.SendAsync(chat.Id, T(lang, key), Kb.Reply(rows, contact ? T(lang, "btn.sharePhone") : null), ct: ct);
    }

    private async Task OnFlowInputAsync(BotChat chat, JsonElement msg, string? text, CancellationToken ct)
    {
        string lang = chat.Lang!;
        string state = chat.State!;
        if (text == T(lang, "btn.cancel") || text == T(lang, "btn.home"))
        {
            await ResetStateAsync(chat, ct);
            await api.SendAsync(chat.Id, T(lang, "flow.cancelled"), Kb.RemoveReply(), ct: ct);
            await SendMenuAsync(chat, null, ct);
            return;
        }
        var data = Data(chat);
        string flow = state.Split('.')[0];
        string step = state.Split('.').ElementAtOrDefault(1) ?? "";
        BotThreadKind kind = flow == "demo" ? BotThreadKind.Demo : BotThreadKind.Consultation;

        if (state == "thread.reply" || state == "support.more")
        {
            if (data.TryGetValue("t", out var ts) && int.TryParse(ts, out int tid))
                await ForwardToThreadAsync(chat, tid, msg, ct);
            return;
        }
        if (state == "support.text")
        {
            await CreateTicketAsync(chat, data, msg, ct);
            return;
        }

        switch (step)
        {
            case "name":
                if (!TryText(text, 100, out string name, out string? err)) { await RepeatAsync(chat, err!, ct); return; }
                data["name"] = name;
                chat.FullName = name;
                await SetStateAsync(chat, flow + ".company", data, ct);
                await AskAsync(chat, "flow.company", Suggest(chat.Company), ct);
                return;
            case "company":
                if (!TryText(text, 200, out string company, out err)) { await RepeatAsync(chat, err!, ct); return; }
                data["company"] = company;
                chat.Company = company;
                if (flow == "support")
                {
                    await SetStateAsync(chat, "support.text", data, ct);
                    await api.SendAsync(chat.Id, T(lang, "support.text"), Kb.Reply([[T(lang, "btn.cancel")]]), ct: ct);
                    return;
                }
                await SetStateAsync(chat, flow + ".phone", data, ct);
                await AskAsync(chat, "flow.phone", Suggest(chat.Phone), ct, contact: true);
                return;
            case "phone":
            {
                string? phone = msg.TryGetProperty("contact", out var contact) ? Str(contact, "phone_number") : text;
                if (phone is null || !DemoRequestService.IsValidPhone(phone)) { await RepeatAsync(chat, "flow.badPhone", ct); return; }
                data["phone"] = DemoRequestService.NormalizePhone(phone);
                chat.Phone = data["phone"];
                if (kind == BotThreadKind.Demo)
                {
                    await SetStateAsync(chat, "demo.industry", data, ct);
                    var rows = DemoRequestService.Industries.Select(i => T(lang, "industry." + i)).Chunk(2).Select(r => (IEnumerable<string>)r).ToList();
                    rows.Add([T(lang, "btn.skip"), T(lang, "btn.cancel")]);
                    await api.SendAsync(chat.Id, T(lang, "demo.industry"), Kb.Reply(rows), ct: ct);
                }
                else
                {
                    await SetStateAsync(chat, "consult.question", data, ct);
                    await AskAsync(chat, "consult.question", [], ct, skip: true);
                }
                return;
            }
            case "industry":
            {
                string? code = DemoRequestService.Industries.FirstOrDefault(i => T(lang, "industry." + i) == text);
                if (code is null && text != T(lang, "btn.skip")) { await RepeatAsync(chat, "demo.industry", ct); return; }
                if (code is not null) data["industry"] = code;
                await SubmitLeadAsync(chat, kind, data, ct);
                return;
            }
            case "question":
                if (text is null) { await RepeatAsync(chat, "flow.textOnly", ct); return; }
                if (text != T(lang, "btn.skip"))
                {
                    if (text.Length > 1500) { await RepeatAsync(chat, "flow.tooLong", ct); return; }
                    data["question"] = text.Trim();
                }
                await SubmitLeadAsync(chat, kind, data, ct);
                return;
            default:
                await ResetStateAsync(chat, ct);
                await SendMenuAsync(chat, null, ct);
                return;
        }
    }

    private Task RepeatAsync(BotChat chat, string key, CancellationToken ct) => api.SendAsync(chat.Id, T(chat.Lang!, key), ct: ct);

    private static bool TryText(string? text, int max, out string value, out string? errorKey)
    {
        value = text?.Trim() ?? "";
        errorKey = text is null ? "flow.textOnly" : value.Length == 0 ? "flow.textOnly" : value.Length > max ? "flow.tooLong" : null;
        return errorKey is null;
    }

    private async Task SubmitLeadAsync(BotChat chat, BotThreadKind kind, Dictionary<string, string> data, CancellationToken ct)
    {
        string lang = chat.Lang!;
        var reqKind = kind == BotThreadKind.Demo ? DemoRequestKind.Demo : DemoRequestKind.Consultation;
        DateTime now = clock.UtcNow;
        await ResetStateAsync(chat, ct);

        var dup = await db.DemoRequests.Where(r => r.TelegramChatId == chat.Id && r.Kind == reqKind && r.CreatedAtUtc > now.AddMinutes(-30))
            .OrderByDescending(r => r.Id).FirstOrDefaultAsync(ct);
        if (dup is not null)
        {
            int? dupThread = await db.BotThreads.Where(t => t.DemoRequestId == dup.Id).Select(t => (int?)t.Id).FirstOrDefaultAsync(ct);
            await api.SendAsync(chat.Id, T(lang, "lead.duplicate", dup.Id), Kb.RemoveReply(), ct: ct);
            await api.SendAsync(chat.Id, T(lang, "menu"), dupThread is int dt ? ThreadMenu(lang, dt, false) : MainMenu(lang), ct: ct);
            return;
        }

        var r = await demos.SaveFromBotAsync(new DemoRequest
        {
            FullName = data.GetValueOrDefault("name", ""),
            Company = data.GetValueOrDefault("company", ""),
            Phone = data.GetValueOrDefault("phone", ""),
            Industry = data.GetValueOrDefault("industry"),
            Message = data.GetValueOrDefault("question"),
            Lang = lang,
            Kind = reqKind,
            Source = DemoRequestSources.Telegram,
            TelegramChatId = chat.Id,
            TelegramUsername = chat.Username,
        }, ct);

        var thread = new BotThread
        {
            ChatId = chat.Id, Kind = kind, Status = BotThreadStatus.Open, DemoRequestId = r.Id, Company = r.Company,
            Subject = Cut(r.Message ?? (kind == BotThreadKind.Demo ? "Demo" : "Konsultatsiya"), 500), CreatedAtUtc = now, LastActivityUtc = now,
        };
        db.BotThreads.Add(thread);
        await db.SaveChangesAsync(ct);

        await NotifySalesAsync(chat, r, thread, ct);

        await api.SendAsync(chat.Id, T(lang, kind == BotThreadKind.Demo ? "demo.done" : "consult.done", Esc(r.FullName), r.Id, Esc(r.Phone)),
            Kb.RemoveReply(), ct: ct);
        await api.SendAsync(chat.Id, T(lang, "menu"), ThreadMenu(lang, thread.Id, false), ct: ct);
    }

    /// <summary>Savdo guruhiga (StaffChatId) xabar; muvaffaqiyatsiz bo'lsa DemoNotifyRetryHostedService oddiy matn bilan qayta yuboradi.</summary>
    private async Task NotifySalesAsync(BotChat chat, DemoRequest r, BotThread thread, CancellationToken ct)
    {
        var cfg = await config.GetAsync(ct);
        string adminBase = "https://" + hosts.Value.EffectiveAdminHost;
        await demos.EmailStaffAsync(r, DemoRequestService.BuildStaffText(r, adminBase), ct);
        if (cfg.SalesChatId is not long group || !api.IsConfigured)
        {
            log.LogInformation("Bot so'rovi #{Id}: savdo guruhi sozlanmagan", r.Id);
            return;
        }
        string html = LeadHtml(chat, r, thread);
        try
        {
            long mid = await api.SendAsync(group, html, Kb.Inline(Kb.Row(Kb.Url("🗂 Panelda ochish", adminBase + "/Leads"))), ct: ct);
            r.StaffNotifiedAtUtc = clock.UtcNow;
            thread.GroupChatId = group;
            thread.GroupMessageId = mid;
            db.BotThreadMessages.Add(new BotThreadMessage
            {
                ThreadId = thread.Id, FromStaff = false, Text = Cut(BotSearch.PlainText(html), 4000), GroupChatId = group, GroupMessageId = mid,
                CreatedAtUtc = clock.UtcNow,
            });
        }
        catch (Exception ex) when (ex is BotApiException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            r.StaffNotifyAttempts = 1;
            r.StaffNotifyLastAtUtc = clock.UtcNow;
            r.StaffNotifyError = Cut(ex.Message, 500);
            log.LogWarning(ex, "Bot so'rovi #{Id} savdo guruhiga yuborilmadi", r.Id);
        }
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private static string LeadHtml(BotChat chat, DemoRequest r, BotThread thread)
    {
        var lines = new List<string>
        {
            (r.Kind == DemoRequestKind.Demo ? "🆕 <b>Demo so'rovi #" : "💼 <b>Konsultatsiya / sotib olish #") + r.Id + "</b> · Telegram bot (" + r.Lang.ToUpperInvariant() + ")",
            "Kompaniya: <b>" + Esc(r.Company) + "</b>",
            "Ism: " + Esc(r.FullName),
            "Telefon: " + Esc(r.Phone),
        };
        if (r.Industry is not null) lines.Add("Faoliyat: " + Esc(DemoRequestService.IndustryText(r.Industry)));
        if (r.Message is not null) lines.Add("Savol: " + Esc(r.Message));
        lines.Add("Telegram: " + UserLink(chat));
        lines.Add("");
        lines.Add($"↩️ Shu xabarga <b>reply</b> qilib yozsangiz — mijozga bot orqali yetkaziladi (№{thread.Id}). /close — yopish.");
        return string.Join("\n", lines);
    }

    private async Task CreateTicketAsync(BotChat chat, Dictionary<string, string> data, JsonElement msg, CancellationToken ct)
    {
        string lang = chat.Lang!;
        var (text, media) = Content(msg);
        if (text is null && media is null)
        {
            await RepeatAsync(chat, "support.text", ct);
            return;
        }
        var cfg = await config.GetAsync(ct);
        if (cfg.SupportChatId is not long group)
        {
            await ResetStateAsync(chat, ct);
            await api.SendAsync(chat.Id, T(lang, "support.unavailable"), Kb.RemoveReply(), ct: ct);
            return;
        }
        DateTime now = clock.UtcNow;
        var thread = new BotThread
        {
            ChatId = chat.Id, Kind = BotThreadKind.Support, Status = BotThreadStatus.Open, Company = data.GetValueOrDefault("company"),
            Subject = Cut(text ?? "[" + media + "]", 500), GroupChatId = group, CreatedAtUtc = now, LastActivityUtc = now,
        };
        db.BotThreads.Add(thread);
        await db.SaveChangesAsync(ct);

        var lines = new List<string>
        {
            $"🛠 <b>Murojaat №{thread.Id}</b> · {lang.ToUpperInvariant()}",
            "Firma: <b>" + Esc(thread.Company ?? "—") + "</b>",
            "Foydalanuvchi: " + UserLink(chat) + (chat.Phone is null ? "" : " · " + Esc(chat.Phone)),
        };
        if (text is not null) lines.AddRange(["", Esc(text)]);
        lines.AddRange(["", "↩️ <b>Reply</b> — javob mijozga yetkaziladi. /close — yopish."]);
        try
        {
            long header = await api.SendAsync(group, string.Join("\n", lines), ct: ct);
            thread.GroupMessageId = header;
            db.BotThreadMessages.Add(new BotThreadMessage
            {
                ThreadId = thread.Id, Text = Cut(text, 4000), Media = media, GroupChatId = group, GroupMessageId = header,
                UserMessageId = msg.GetProperty("message_id").GetInt64(), CreatedAtUtc = now,
            });
            if (media is not null)
            {
                long copy = await api.CopyAsync(group, chat.Id, msg.GetProperty("message_id").GetInt64(), replyTo: header, ct: ct);
                db.BotThreadMessages.Add(new BotThreadMessage
                {
                    ThreadId = thread.Id, Media = media, GroupChatId = group, GroupMessageId = copy, CreatedAtUtc = now,
                });
            }
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is BotApiException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            log.LogError(ex, "Murojaat №{Id} support guruhiga yuborilmadi", thread.Id);
            db.BotThreads.Remove(thread);
            await db.SaveChangesAsync(CancellationToken.None);
            await ResetStateAsync(chat, ct);
            await api.SendAsync(chat.Id, T(lang, "error"), Kb.RemoveReply(), ct: ct);
            return;
        }

        await SetStateAsync(chat, "support.more", new Dictionary<string, string> { ["t"] = thread.Id.ToString() }, ct);
        await api.SendAsync(chat.Id, T(lang, "support.done", thread.Id), Kb.RemoveReply(), ct: ct);
        await api.SendAsync(chat.Id, T(lang, "support.more"),
            Kb.Inline(Kb.Row(Kb.Btn(T(lang, "btn.done"), "done")), Kb.Row(Kb.Btn(T(lang, "btn.home"), "menu"))), ct: ct);
    }

    /// <summary>Foydalanuvchi xabari → guruhdagi murojaat (sarlavhaga reply). Yopilgan murojaat qayta ochiladi.</summary>
    private async Task ForwardToThreadAsync(BotChat chat, int threadId, JsonElement msg, CancellationToken ct)
    {
        string lang = chat.Lang!;
        var thread = await db.BotThreads.FirstOrDefaultAsync(t => t.Id == threadId && t.ChatId == chat.Id, ct);
        if (thread?.GroupChatId is not long group)
        {
            await ResetStateAsync(chat, ct);
            await api.SendAsync(chat.Id, T(lang, "error"), MenuButton(lang), ct: ct);
            return;
        }
        if (!AllowMessage(chat.Id))
        {
            await api.SendAsync(chat.Id, T(lang, "limit.messages"), ct: ct);
            return;
        }
        var (text, media) = Content(msg);
        if (text is null && media is null)
        {
            await api.SendAsync(chat.Id, T(lang, "unknownMedia"), ct: ct);
            return;
        }
        long userMsg = msg.GetProperty("message_id").GetInt64();
        bool reopened = thread.Status == BotThreadStatus.Closed;
        thread.Status = BotThreadStatus.Open;
        thread.ClosedAtUtc = null;
        thread.LastActivityUtc = clock.UtcNow;
        string head = $"💬 <b>№{thread.Id}</b> · {UserLink(chat)}" + (reopened ? " · 🔄 qayta ochildi" : "");
        try
        {
            long gm;
            if (media is null)
                gm = await api.SendAsync(group, head + "\n" + Esc(text!), replyTo: thread.GroupMessageId, ct: ct);
            else
            {
                await api.SendAsync(group, head, replyTo: thread.GroupMessageId, ct: ct);
                gm = await api.CopyAsync(group, chat.Id, userMsg, replyTo: thread.GroupMessageId, ct: ct);
            }
            db.BotThreadMessages.Add(new BotThreadMessage
            {
                ThreadId = thread.Id, Text = Cut(text, 4000), Media = media, GroupChatId = group, GroupMessageId = gm, UserMessageId = userMsg,
                CreatedAtUtc = clock.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            await api.ReactAsync(chat.Id, userMsg, "👌", ct);
        }
        catch (Exception ex) when (ex is BotApiException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Murojaat №{Id}: xabar guruhga yetkazilmadi", thread.Id);
            await api.SendAsync(chat.Id, T(lang, "error"), ct: ct);
        }
    }

    // ── Guruhlar (savdo / support) ───────────────────────────────────────────────────────────────────────────────

    private async Task OnGroupMessageAsync(JsonElement msg, CancellationToken ct)
    {
        var chatEl = msg.GetProperty("chat");
        long groupId = chatEl.GetProperty("id").GetInt64();
        if (msg.TryGetProperty("migrate_to_chat_id", out var mig))
        {
            await MigrateGroupAsync(groupId, mig.GetInt64(), Str(chatEl, "title"), ct);
            return;
        }
        await UpsertGroupAsync(groupId, Str(chatEl, "title"), true, ct);
        string? text = Str(msg, "text");
        var from = msg.TryGetProperty("from", out var f) ? f : default;
        if (from.ValueKind == JsonValueKind.Object && from.TryGetProperty("is_bot", out var isBot) && isBot.GetBoolean())
            return;

        string? cmd = text is not null && text.StartsWith('/') ? text.Split(' ', 2)[0].Split('@')[0].ToLowerInvariant() : null;
        if (cmd is "/start" or "/id" or "/chatid")
        {
            await api.SendAsync(groupId, $"Chat ID: <code>{groupId}</code>\n{Esc(Str(chatEl, "title") ?? "")}", ct: ct);
            return;
        }
        if (!msg.TryGetProperty("reply_to_message", out var rt) || !rt.TryGetProperty("message_id", out var rtId))
            return;
        long replied = rtId.GetInt64();
        int? threadId = await db.BotThreadMessages.AsNoTracking()
            .Where(m => m.GroupChatId == groupId && m.GroupMessageId == replied).Select(m => (int?)m.ThreadId).FirstOrDefaultAsync(ct)
            ?? await db.BotThreads.AsNoTracking().Where(t => t.GroupChatId == groupId && t.GroupMessageId == replied)
                .Select(t => (int?)t.Id).FirstOrDefaultAsync(ct);
        if (threadId is not int tid)
            return;
        var thread = await db.BotThreads.FirstAsync(t => t.Id == tid, ct);
        long staffMsg = msg.GetProperty("message_id").GetInt64();
        string author = Join(Str(from, "first_name"), Str(from, "last_name")) ?? "Xodim";

        if (cmd == "/close")
        {
            await CloseThreadAsync(thread, author, notifyUser: true, ct);
            await api.ReactAsync(groupId, staffMsg, "👍", ct);
            return;
        }
        await RelayStaffAsync(thread, groupId, msg, author, ct);
    }

    private async Task RelayStaffAsync(BotThread thread, long groupId, JsonElement msg, string author, CancellationToken ct)
    {
        var chat = await db.BotChats.FirstOrDefaultAsync(c => c.Id == thread.ChatId, ct);
        if (chat is null) return;
        string lang = chat.Lang ?? "uz";
        long staffMsg = msg.GetProperty("message_id").GetInt64();
        var (text, media) = Content(msg);
        if (text is null && media is null) return;
        var markup = ThreadButtons(lang, thread.Id, thread.Kind == BotThreadKind.Support);
        try
        {
            long um = media is null
                ? await api.SendAsync(chat.Id, T(lang, "thread.staffReply", thread.Id, Esc(text!)), markup, ct: ct)
                : await api.CopyAsync(chat.Id, groupId, staffMsg, markup: markup, ct: ct);
            thread.Status = BotThreadStatus.Open;
            thread.ClosedAtUtc = null;
            thread.LastActivityUtc = clock.UtcNow;
            db.BotThreadMessages.Add(new BotThreadMessage
            {
                ThreadId = thread.Id, FromStaff = true, Author = Cut(author, 100), Text = Cut(text, 4000), Media = media, GroupChatId = groupId,
                GroupMessageId = staffMsg, UserMessageId = um, CreatedAtUtc = clock.UtcNow,
            });
            await MarkContactedAsync(thread, ct);
            await db.SaveChangesAsync(ct);
            await api.ReactAsync(groupId, staffMsg, "👍", ct);
        }
        catch (BotApiException ex) when (ex.Blocked)
        {
            chat.BlockedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            await api.SendAsync(groupId, "⚠️ Foydalanuvchi botni bloklagan — xabar yetkazilmadi.", replyTo: staffMsg, ct: ct);
        }
    }

    /// <summary>Xodim bot orqali javob yozdi — yangi so'rov «Bog'lanildi» holatiga o'tadi.</summary>
    private async Task MarkContactedAsync(BotThread thread, CancellationToken ct)
    {
        if (thread.DemoRequestId is not int rid) return;
        var r = await db.DemoRequests.FirstOrDefaultAsync(x => x.Id == rid, ct);
        if (r is { Status: DemoRequestStatus.New })
        {
            r.Status = DemoRequestStatus.Contacted;
            r.HandledAtUtc = clock.UtcNow;
        }
    }

    private async Task CloseThreadAsync(BotThread thread, string by, bool notifyUser, CancellationToken ct)
    {
        if (thread.Status == BotThreadStatus.Closed) return;
        thread.Status = BotThreadStatus.Closed;
        thread.ClosedAtUtc = clock.UtcNow;
        thread.ClosedBy = Cut(by, 100);
        await db.SaveChangesAsync(ct);
        if (!notifyUser || thread.Kind != BotThreadKind.Support) return;
        var chat = await db.BotChats.AsNoTracking().FirstOrDefaultAsync(c => c.Id == thread.ChatId, ct);
        if (chat is null) return;
        string lang = chat.Lang ?? "uz";
        try
        {
            await api.SendAsync(chat.Id, T(lang, "thread.closed", thread.Id),
                Kb.Inline(Enumerable.Range(1, 5).Select(i => Kb.Btn("⭐" + i, $"rate:{thread.Id}:{i}")).ToArray()), ct: ct);
        }
        catch (BotApiException ex) when (ex.Blocked) { }
    }

    private async Task MigrateGroupAsync(long oldId, long newId, string? title, CancellationToken ct)
    {
        var s = await db.BotSettings.FirstAsync(x => x.Id == 1, ct);
        if (s.SupportChatId == oldId) s.SupportChatId = newId;
        await db.BotThreads.Where(t => t.GroupChatId == oldId).ExecuteUpdateAsync(u => u.SetProperty(t => t.GroupChatId, newId), ct);
        await db.BotThreadMessages.Where(m => m.GroupChatId == oldId).ExecuteUpdateAsync(u => u.SetProperty(m => m.GroupChatId, newId), ct);
        var old = await db.BotGroups.FirstOrDefaultAsync(g => g.Id == oldId, ct);
        if (old is not null) old.IsMember = false;
        await db.SaveChangesAsync(ct);
        await UpsertGroupAsync(newId, title ?? old?.Title, true, ct);
        config.Invalidate();
        if ((await config.GetAsync(ct)).SalesChatId == oldId)
            log.LogWarning("Savdo guruhi supergroup'ga o'tdi: ControlPanel__Telegram__StaffChatId ni {New} ga almashtiring", newId);
    }

    private async Task OnMembershipAsync(JsonElement member, CancellationToken ct)
    {
        var chatEl = member.GetProperty("chat");
        string type = Str(chatEl, "type") ?? "";
        long id = chatEl.GetProperty("id").GetInt64();
        string status = member.TryGetProperty("new_chat_member", out var nm) ? Str(nm, "status") ?? "" : "";
        bool isMember = status is "member" or "administrator";
        if (type == "private")
        {
            var chat = await db.BotChats.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (chat is not null)
            {
                chat.BlockedAtUtc = status == "kicked" ? clock.UtcNow : null;
                await db.SaveChangesAsync(ct);
            }
            return;
        }
        await UpsertGroupAsync(id, Str(chatEl, "title"), isMember, ct);
        if (isMember)
            await api.SendAsync(id, $"👋 tahsinERP bot guruhga qo'shildi.\nChat ID: <code>{id}</code>\n" +
                                    "Panel → «Telegram bot» sahifasida bu guruhni texnik yordam guruhi qilib belgilash mumkin.", ct: ct);
    }

    private async Task UpsertGroupAsync(long id, string? title, bool member, CancellationToken ct)
    {
        var g = await db.BotGroups.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (g is null)
        {
            g = new BotGroup { Id = id };
            db.BotGroups.Add(g);
        }
        else if (g.IsMember == member && (title is null || g.Title == title) && clock.UtcNow - g.UpdatedAtUtc < TimeSpan.FromHours(1))
            return;
        g.Title = Cut(title ?? g.Title, 200) ?? "";
        g.IsMember = member;
        g.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // ── Tugmalar ─────────────────────────────────────────────────────────────────────────────────────────────────

    private async Task OnCallbackAsync(JsonElement cb, CancellationToken ct)
    {
        string id = Str(cb, "id") ?? "";
        string data = Str(cb, "data") ?? "";
        var from = cb.GetProperty("from");
        if (!cb.TryGetProperty("message", out var msg) || Str(msg.GetProperty("chat"), "type") != "private")
        {
            await api.AnswerCallbackAsync(id, ct: ct);
            return;
        }
        long chatId = msg.GetProperty("chat").GetProperty("id").GetInt64();
        long messageId = msg.GetProperty("message_id").GetInt64();
        var chat = await TouchChatAsync(chatId, from, ct);
        string[] p = data.Split(':');
        await api.AnswerCallbackAsync(id, ct: ct);

        if (p[0] == "lang" && p.Length == 2)
        {
            bool first = chat.Lang is null;
            string? after = first ? Data(chat).GetValueOrDefault("after") : null;
            chat.Lang = BotContent.Normalize(p[1]);
            await SetStateAsync(chat, null, [], ct);
            await EditOrSendAsync(chat, messageId, (first ? Welcome(chat) : T(chat.Lang, "lang.changed") + "\n\n" + T(chat.Lang, "menu"), MainMenu(chat.Lang)), ct);
            if (first) await SetCommandsSafeAsync(chat, ct);
            if (after is not null) await StartParamAsync(chat, after, ct);
            return;
        }
        if (chat.Lang is null)
        {
            await SendLanguagePickerAsync(chat, ct);
            return;
        }
        string lang = chat.Lang;
        switch (p[0])
        {
            case "menu":
                if (chat.State is not null)
                {
                    await ResetStateAsync(chat, ct);
                    await api.SendAsync(chat.Id, "🏠", Kb.RemoveReply(), ct: ct);
                }
                await EditOrSendAsync(chat, messageId, (T(lang, "menu"), MainMenu(lang)), ct);
                break;
            case "about": await EditOrSendAsync(chat, messageId, p.Length > 1 ? AboutMore(lang, p[1]) : About(lang), ct); break;
            case "mods": await EditOrSendAsync(chat, messageId, Modules(lang), ct); break;
            case "mod" when p.Length >= 2:
                await EditOrSendAsync(chat, messageId, Module(lang, p[1], p.Length > 2 && int.TryParse(p[2], out int mp) ? mp : 0), ct);
                break;
            case "sec" when p.Length >= 3 && int.TryParse(p[2], out int si):
                await EditOrSendAsync(chat, messageId, Section(lang, p[1], si, p.Length > 3 && int.TryParse(p[3], out int pg) ? pg : 0), ct);
                break;
            case "plans": await EditOrSendAsync(chat, messageId, Plans(lang), ct); break;
            case "faq":
                await EditOrSendAsync(chat, messageId, p.Length > 1 && int.TryParse(p[1], out int fi) ? FaqAnswer(lang, fi) : Faq(lang), ct);
                break;
            case "ask":
                await ResetStateAsync(chat, ct);
                await EditOrSendAsync(chat, messageId, (T(lang, "ask.prompt"), MenuButton(lang)), ct);
                break;
            case "lang": await EditOrSendAsync(chat, messageId, LanguagePicker(), ct); break;
            case "demo": await ResetStateAsync(chat, ct); await StartLeadFlowAsync(chat, BotThreadKind.Demo, ct); break;
            case "consult": await ResetStateAsync(chat, ct); await StartLeadFlowAsync(chat, BotThreadKind.Consultation, ct); break;
            case "support": await ResetStateAsync(chat, ct); await StartSupportFlowAsync(chat, ct); break;
            case "done":
                await ResetStateAsync(chat, ct);
                await EditOrSendAsync(chat, messageId, (T(lang, "thread.sent") + "\n\n" + T(lang, "menu"), MainMenu(lang)), ct);
                break;
            case "thr" when p.Length == 2 && int.TryParse(p[1], out int tid):
                if (await db.BotThreads.AnyAsync(t => t.Id == tid && t.ChatId == chat.Id, ct))
                {
                    await SetStateAsync(chat, "thread.reply", new Dictionary<string, string> { ["t"] = tid.ToString() }, ct);
                    await api.SendAsync(chat.Id, T(lang, "thread.replyPrompt", tid), Kb.Reply([[T(lang, "btn.home")]]), ct: ct);
                }
                break;
            case "res" when p.Length == 2 && int.TryParse(p[1], out int rid):
            {
                var thread = await db.BotThreads.FirstOrDefaultAsync(t => t.Id == rid && t.ChatId == chat.Id, ct);
                if (thread is null) break;
                await ResetStateAsync(chat, ct);
                await CloseThreadAsync(thread, "mijoz", notifyUser: true, ct);
                if (thread.GroupChatId is long g)
                    await TrySendAsync(g, $"✅ №{thread.Id}: mijoz «muammo hal bo'ldi» deb belgiladi.", thread.GroupMessageId, ct);
                break;
            }
            case "rate" when p.Length == 3 && int.TryParse(p[1], out int rtid) && int.TryParse(p[2], out int stars) && stars is >= 1 and <= 5:
            {
                var thread = await db.BotThreads.FirstOrDefaultAsync(t => t.Id == rtid && t.ChatId == chat.Id, ct);
                if (thread is null || thread.Rating is not null) break;
                thread.Rating = stars;
                await db.SaveChangesAsync(ct);
                await EditOrSendAsync(chat, messageId, (T(lang, "thread.rated"), MainMenu(lang)), ct);
                if (thread.GroupChatId is long g)
                    await TrySendAsync(g, $"{string.Concat(Enumerable.Repeat("⭐", stars))} №{thread.Id}: mijoz bahosi {stars}/5", thread.GroupMessageId, ct);
                break;
            }
            default:
                await EditOrSendAsync(chat, messageId, (T(lang, "menu"), MainMenu(lang)), ct);
                break;
        }
    }

    private async Task TrySendAsync(long chatId, string html, long? replyTo, CancellationToken ct)
    {
        try { await api.SendAsync(chatId, html, replyTo: replyTo, ct: ct); }
        catch (Exception ex) when (ex is BotApiException or HttpRequestException) { log.LogWarning(ex, "Guruhga xabar yuborilmadi"); }
    }

    private async Task EditOrSendAsync(BotChat chat, long messageId, (string Html, JsonObject Markup) screen, CancellationToken ct)
    {
        try
        {
            await api.EditAsync(chat.Id, messageId, screen.Html, screen.Markup, ct);
        }
        catch (BotApiException)
        {
            await api.SendAsync(chat.Id, screen.Html, screen.Markup, ct: ct);
        }
    }

    private Task ShowAsync(BotChat chat, long? messageId, (string Html, JsonObject Markup) screen, CancellationToken ct) =>
        messageId is long m ? EditOrSendAsync(chat, m, screen, ct) : api.SendAsync(chat.Id, screen.Html, screen.Markup, ct: ct);

    private Task SendMenuAsync(BotChat chat, long? messageId, CancellationToken ct) => ShowAsync(chat, messageId, (T(chat.Lang!, "menu"), MainMenu(chat.Lang!)), ct);

    // ── Ekranlar ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static string T(string lang, string key, params object?[] args) => BotContent.T(lang, key, args);

    private string Welcome(BotChat chat) => T(chat.Lang!, "welcome", Esc(chat.FirstName ?? chat.FullName ?? "👋"));

    public static string SiteFor(string lang) => SiteUrl + (lang == "ru" ? "/ru" : "");

    public static JsonObject MainMenu(string lang) => Kb.Inline(
        Kb.Row(Kb.Btn(T(lang, "btn.about"), "about"), Kb.Btn(T(lang, "btn.modules"), "mods")),
        Kb.Row(Kb.Btn(T(lang, "btn.plans"), "plans"), Kb.Btn(T(lang, "btn.faq"), "faq")),
        Kb.Row(Kb.Btn(T(lang, "btn.demo"), "demo")),
        Kb.Row(Kb.Btn(T(lang, "btn.consult"), "consult")),
        Kb.Row(Kb.Btn(T(lang, "btn.support"), "support"), Kb.Btn(T(lang, "btn.ask"), "ask")),
        Kb.Row(Kb.Url(T(lang, "btn.site"), SiteFor(lang)), Kb.Btn(T(lang, "btn.lang"), "lang")));

    private static JsonObject MenuButton(string lang) => Kb.Inline(Kb.Row(Kb.Btn(T(lang, "btn.home"), "menu")));

    public static JsonObject ThreadButtons(string lang, int threadId, bool support) => support
        ? Kb.Inline(Kb.Row(Kb.Btn(T(lang, "btn.reply"), $"thr:{threadId}"), Kb.Btn(T(lang, "btn.resolved"), $"res:{threadId}")))
        : Kb.Inline(Kb.Row(Kb.Btn(T(lang, "btn.reply"), $"thr:{threadId}")));

    private static JsonObject ThreadMenu(string lang, int threadId, bool support)
    {
        var rows = ThreadButtons(lang, threadId, support)["inline_keyboard"]!.AsArray().DeepClone().AsArray();
        foreach (var r in MainMenu(lang)["inline_keyboard"]!.AsArray()) rows.Add(r!.DeepClone());
        return new JsonObject { ["inline_keyboard"] = rows };
    }

    private static (string, JsonObject) LanguagePicker() => (T("uz", "chooseLang"), Kb.Inline(
        Kb.Row(Kb.Btn(BotContent.LangName("uz"), "lang:uz"), Kb.Btn(BotContent.LangName("oz"), "lang:oz")),
        Kb.Row(Kb.Btn(BotContent.LangName("ru"), "lang:ru"))));

    private async Task SendLanguagePickerAsync(BotChat chat, CancellationToken ct, string? after = null)
    {
        if (after is not null && StartParams.Contains(after))
            await SetStateAsync(chat, null, new Dictionary<string, string> { ["after"] = after }, ct);
        var (html, markup) = LanguagePicker();
        await api.SendAsync(chat.Id, html, markup, ct: ct);
    }

    private static (string, JsonObject) About(string lang)
    {
        var s = BotContent.Site(lang);
        string Tr(string x) => BotContent.Tr(lang, x);
        var lines = new List<string> { T(lang, "about.title"), "", Esc(Tr(s["hero.subtitle"])), "", T(lang, "about.audience") };
        lines.AddRange(s.Strings("audience.items").Select(a => "• " + Esc(Tr(a))));
        lines.AddRange(["", T(lang, "about.pains")]);
        lines.AddRange(s.List("pains.items").Select(p => "✅ <b>" + Esc(Tr(p["title"])) + "</b> — " + Esc(Tr(p["result"]))));
        lines.AddRange(["", T(lang, "about.why")]);
        lines.AddRange(s.List("why.items").Select(w => "• <b>" + Esc(Tr(w["title"])) + "</b> — " + Esc(Tr(w["desc"]))));
        lines.AddRange(["", T(lang, "about.numbers")]);
        lines.AddRange(s.List("stats").Select(x => "• <b>" + Esc(Tr(x["value"] + x["suffix"])) + "</b> " + Esc(Tr(x["label"]))));
        return (string.Join("\n", lines), Kb.Inline(
            Kb.Row(Kb.Btn(T(lang, "btn.modules"), "mods"), Kb.Btn(T(lang, "btn.process"), "about:process")),
            Kb.Row(Kb.Btn(T(lang, "btn.dist"), "about:dist"), Kb.Btn(T(lang, "btn.plans"), "plans")),
            Kb.Row(Kb.Btn(T(lang, "btn.demo"), "demo")),
            Kb.Row(Kb.Btn(T(lang, "btn.home"), "menu"))));
    }

    private static (string, JsonObject) AboutMore(string lang, string what)
    {
        var s = BotContent.Site(lang);
        string Tr(string x) => BotContent.Tr(lang, x);
        var lines = new List<string>();
        if (what == "dist")
        {
            lines.AddRange(["📲 <b>" + Esc(Tr(s["dist.title"])) + "</b>", "", Esc(Tr(s["dist.subtitle"])), ""]);
            lines.AddRange(s.List("dist.features").Select(f => "• <b>" + Esc(Tr(f["title"])) + "</b> — " + Esc(Tr(f["desc"]))));
        }
        else
        {
            lines.AddRange(["🚀 <b>" + Esc(Tr(s["process.title"])) + "</b>", "", Esc(Tr(s["process.subtitle"])), ""]);
            lines.AddRange(s.List("process.steps").Select(st => "▫️ <b>" + Esc(Tr(st["when"])) + " — " + Esc(Tr(st["title"])) + "</b>\n" + Esc(Tr(st["desc"]))));
        }
        return (string.Join("\n", lines), Kb.Inline(
            Kb.Row(Kb.Btn(T(lang, "btn.demo"), "demo")),
            Kb.Row(Kb.Btn(T(lang, "btn.back"), "about"), Kb.Btn(T(lang, "btn.home"), "menu"))));
    }

    private static (string, JsonObject) Modules(string lang)
    {
        var mods = BotContent.Modules(lang);
        var rows = mods.Select(m => Kb.Row(Kb.Btn(Short($"{m.Num} · {m.Title}", 60), "mod:" + m.Num))).ToList<IEnumerable<JsonObject>>();
        rows.Add(Kb.Row(Kb.Btn(T(lang, "btn.home"), "menu")));
        return (T(lang, "modules.title", mods.Count), Kb.InlineRows(rows));
    }

    private static (string, JsonObject) Module(string lang, string num, int page)
    {
        var m = BotContent.Module(lang, num);
        if (m is null) return Modules(lang);
        int pages = Math.Max(1, (m.Sections.Count + SectionsPerPage - 1) / SectionsPerPage);
        page = Math.Clamp(page, 0, pages - 1);
        string html = $"🧩 <b>{Esc(m.Num)}. {Esc(m.Title)}</b>\n\n{Esc(m.Summary)}\n\n{T(lang, "module.sections", page + 1, pages)}";
        var rows = m.Sections.Select((s, i) => (s, i)).Skip(page * SectionsPerPage).Take(SectionsPerPage)
            .Select(x => Kb.Row(Kb.Btn(Short(BotSearch.PlainText((x.s.Num.Length > 0 ? x.s.Num + " " : "") + x.s.Title), 58), $"sec:{m.Num}:{x.i}")))
            .ToList<IEnumerable<JsonObject>>();
        var nav = new List<JsonObject>();
        if (page > 0) nav.Add(Kb.Btn("◀️", $"mod:{m.Num}:{page - 1}"));
        if (page < pages - 1) nav.Add(Kb.Btn("▶️", $"mod:{m.Num}:{page + 1}"));
        if (nav.Count > 0) rows.Add(nav);
        rows.Add(Kb.Row(Kb.Btn(T(lang, "btn.demo"), "demo")));
        rows.Add(Kb.Row(Kb.Btn(T(lang, "btn.allModules"), "mods"), Kb.Btn(T(lang, "btn.home"), "menu")));
        return (html, Kb.InlineRows(rows));
    }

    private static (string, JsonObject) Section(string lang, string num, int index, int page)
    {
        var m = BotContent.Module(lang, num);
        if (m is null || index < 0 || index >= m.Sections.Count) return Modules(lang);
        var s = m.Sections[index];
        var chunks = Paginate(s.Body, PageChars);
        page = Math.Clamp(page, 0, chunks.Count - 1);
        string head = $"🧩 {Esc(m.Num)}. {Esc(m.Title)}\n<b>{(s.Num.Length > 0 ? Esc(s.Num) + ". " : "")}{Esc(BotSearch.PlainText(s.Title))}</b>";
        string footer = chunks.Count > 1 ? $"\n\n<i>{T(lang, "section.page", page + 1, chunks.Count)}</i>" : "";
        if (page == chunks.Count - 1) footer += "\n\n<i>" + T(lang, "module.footer") + "</i>";
        var rows = new List<IEnumerable<JsonObject>>();
        var nav = new List<JsonObject>();
        if (page > 0) nav.Add(Kb.Btn("◀️", $"sec:{num}:{index}:{page - 1}"));
        if (page < chunks.Count - 1) nav.Add(Kb.Btn("▶️", $"sec:{num}:{index}:{page + 1}"));
        if (nav.Count > 0) rows.Add(nav);
        var step = new List<JsonObject>();
        if (index > 0) step.Add(Kb.Btn("⏮ " + Short(BotSearch.PlainText(m.Sections[index - 1].Num), 10), $"sec:{num}:{index - 1}"));
        if (index < m.Sections.Count - 1) step.Add(Kb.Btn(Short(BotSearch.PlainText(m.Sections[index + 1].Num), 10) + " ⏭", $"sec:{num}:{index + 1}"));
        if (step.Count > 0) rows.Add(step);
        rows.Add(Kb.Row(Kb.Btn(T(lang, "btn.sections"), $"mod:{num}:{index / SectionsPerPage}"), Kb.Btn(T(lang, "btn.home"), "menu")));
        return (head + "\n\n" + chunks[page] + footer, Kb.InlineRows(rows));
    }

    private static (string, JsonObject) Plans(string lang)
    {
        var s = BotContent.Site(lang);
        string Tr(string x) => BotContent.Tr(lang, x);
        var lines = new List<string> { T(lang, "plans.title"), "", Esc(Tr(s["plans.subtitle"])), "" };
        foreach (var p in s.List("plans.items"))
        {
            lines.Add((p.Flag("popular") ? "⭐ " : "▫️ ") + "<b>" + Esc(Tr(p["name"])) + "</b> · " + T(lang, "plans.users", p["users"]));
            lines.Add("<i>" + Esc(Tr(p["for"])) + "</i>");
            lines.AddRange(p.Strings("modules").Select(x => "  • " + Esc(Tr(x))));
            lines.Add("");
        }
        lines.Add("➕ " + Esc(Tr(s["plans.addon"])));
        lines.AddRange(["", T(lang, "plans.footer")]);
        return (string.Join("\n", lines), Kb.Inline(
            Kb.Row(Kb.Btn(T(lang, "btn.priceAsk"), "consult")),
            Kb.Row(Kb.Btn(T(lang, "btn.demo"), "demo")),
            Kb.Row(Kb.Btn(T(lang, "btn.home"), "menu"))));
    }

    private static (string, JsonObject) Faq(string lang)
    {
        var items = BotContent.Site(lang).List("faq.items").ToList();
        var rows = items.Select((f, i) => Kb.Row(Kb.Btn(Short(BotContent.Tr(lang, f["q"]), 60), "faq:" + i))).ToList<IEnumerable<JsonObject>>();
        rows.Add(Kb.Row(Kb.Btn(T(lang, "btn.ask"), "ask"), Kb.Btn(T(lang, "btn.home"), "menu")));
        return (T(lang, "faq.title"), Kb.InlineRows(rows));
    }

    private static (string, JsonObject) FaqAnswer(string lang, int index)
    {
        var items = BotContent.Site(lang).List("faq.items").ToList();
        if (index < 0 || index >= items.Count) return Faq(lang);
        var f = items[index];
        return ("❓ <b>" + Esc(BotContent.Tr(lang, f["q"])) + "</b>\n\n" + Esc(BotContent.Tr(lang, f["a"])), Kb.Inline(
            Kb.Row(Kb.Btn(T(lang, "btn.demo"), "demo")),
            Kb.Row(Kb.Btn(T(lang, "btn.back"), "faq"), Kb.Btn(T(lang, "btn.home"), "menu"))));
    }

    private async Task SearchAsync(BotChat chat, string query, CancellationToken ct)
    {
        string lang = chat.Lang!;
        query = query.Trim();
        if (BotSearch.Terms(query).Count() == 0 || query.Length < 3)
        {
            await api.SendAsync(chat.Id, T(lang, "search.tooShort"), MenuButton(lang), ct: ct);
            return;
        }
        var hits = BotSearch.Search(lang, query);
        string shown = Esc(Short(query, 80));
        if (hits.Count == 0)
        {
            await api.SendAsync(chat.Id, T(lang, "search.none", shown), Kb.Inline(
                Kb.Row(Kb.Btn(T(lang, "btn.consult"), "consult")),
                Kb.Row(Kb.Btn(T(lang, "btn.support"), "support"), Kb.Btn(T(lang, "btn.home"), "menu"))), ct: ct);
            return;
        }
        var lines = new List<string> { T(lang, "search.results", shown), "" };
        var rows = new List<IEnumerable<JsonObject>>();
        var faqs = BotContent.Site(lang).List("faq.items").ToList();
        var top = hits[0];
        if (top.IsFaq && top.Faq < faqs.Count)
            lines.Add(T(lang, "search.faqHit", Esc(BotContent.Tr(lang, faqs[top.Faq]["q"])), Esc(BotContent.Tr(lang, faqs[top.Faq]["a"]))));
        else if (top.Module is not null && BotContent.Module(lang, top.Module) is { } tm)
        {
            var sec = tm.Sections[top.Section];
            lines.Add($"📄 <b>{Esc(tm.Title)} → {Esc(sec.Num)} {Esc(BotSearch.PlainText(sec.Title))}</b>");
            lines.Add(Esc(BotSearch.Snippet(sec.Body, query)));
        }
        var sections = hits.Where(h => !h.IsFaq && h.Module is not null).Take(5).ToList();
        if (sections.Count > 0)
        {
            lines.AddRange(["", T(lang, "search.otherSections")]);
            foreach (var h in sections)
            {
                var m = BotContent.Module(lang, h.Module!)!;
                var s = m.Sections[h.Section];
                rows.Add(Kb.Row(Kb.Btn(Short($"📄 {m.Num} · {s.Num} {BotSearch.PlainText(s.Title)}", 60), $"sec:{m.Num}:{h.Section}")));
            }
        }
        foreach (var h in hits.Where(h => h.IsFaq && h != top).Take(2))
            if (h.Faq < faqs.Count) rows.Add(Kb.Row(Kb.Btn("❓ " + Short(BotContent.Tr(lang, faqs[h.Faq]["q"]), 56), "faq:" + h.Faq)));
        rows.Add(Kb.Row(Kb.Btn(T(lang, "btn.consult"), "consult")));
        rows.Add(Kb.Row(Kb.Btn(T(lang, "btn.home"), "menu")));
        await api.SendAsync(chat.Id, string.Join("\n", lines), Kb.InlineRows(rows), ct: ct);
    }

    // ── Yordamchilar ─────────────────────────────────────────────────────────────────────────────────────────────

    private async Task<BotChat> TouchChatAsync(long chatId, JsonElement from, CancellationToken ct)
    {
        var chat = await db.BotChats.FirstOrDefaultAsync(c => c.Id == chatId, ct);
        DateTime now = clock.UtcNow;
        if (chat is null)
        {
            chat = new BotChat { Id = chatId, CreatedAtUtc = now };
            db.BotChats.Add(chat);
        }
        if (from.ValueKind == JsonValueKind.Object)
        {
            chat.FirstName = Cut(Str(from, "first_name"), 100);
            chat.LastName = Cut(Str(from, "last_name"), 100);
            chat.Username = Cut(Str(from, "username"), 64);
        }
        chat.LastSeenUtc = now;
        chat.BlockedAtUtc = null;
        chat.MessageCount++;
        await db.SaveChangesAsync(ct);
        return chat;
    }

    private async Task SetCommandsSafeAsync(BotChat chat, CancellationToken ct)
    {
        // Buyruqlar menyusi foydalanuvchi tilida (scope: shu chat).
        try
        {
            await api.CallAsync("setMyCommands", new JsonObject
            {
                ["commands"] = Commands(chat.Lang!),
                ["scope"] = new JsonObject { ["type"] = "chat", ["chat_id"] = chat.Id },
            }, ct);
        }
        catch (Exception ex) when (ex is BotApiException or HttpRequestException) { }
    }

    public static JsonArray Commands(string lang) => new(
        new[] { "menu", "demo", "consult", "support", "modules", "lang", "help" }
            .Select(c => (JsonNode)new JsonObject { ["command"] = c, ["description"] = T(lang, "cmd." + c) }).ToArray());

    private async Task SetStateAsync(BotChat chat, string? state, Dictionary<string, string> data, CancellationToken ct)
    {
        chat.State = state;
        chat.StateData = data.Count == 0 ? null : JsonSerializer.Serialize(data);
        await db.SaveChangesAsync(ct);
    }

    private Task ResetStateAsync(BotChat chat, CancellationToken ct) =>
        chat.State is null && chat.StateData is null ? Task.CompletedTask : SetStateAsync(chat, null, [], ct);

    private static Dictionary<string, string> Data(BotChat chat)
    {
        if (string.IsNullOrEmpty(chat.StateData)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(chat.StateData) ?? []; }
        catch (JsonException) { return []; }
    }

    private static bool AllowMessage(long chatId)
    {
        var q = RecentMessages.GetOrAdd(chatId, _ => new Queue<DateTime>());
        lock (q)
        {
            DateTime now = DateTime.UtcNow;
            while (q.Count > 0 && now - q.Peek() > TimeSpan.FromMinutes(1)) q.Dequeue();
            if (q.Count >= MessagesPerMinute) return false;
            q.Enqueue(now);
            return true;
        }
    }

    /// <summary>Xabar matni (yoki rasm izohi) va media turi.</summary>
    private static (string? Text, string? Media) Content(JsonElement msg)
    {
        string? media = new[] { "photo", "document", "video", "voice", "audio", "video_note", "sticker" }
            .FirstOrDefault(k => msg.TryGetProperty(k, out _));
        string? text = Str(msg, "text") ?? Str(msg, "caption");
        if (msg.TryGetProperty("contact", out var c)) text = "📱 " + Str(c, "phone_number");
        return (string.IsNullOrWhiteSpace(text) ? null : text.Trim(), media);
    }

    public static string UserLink(BotChat chat)
    {
        string name = Esc(Join(chat.FirstName, chat.LastName) ?? chat.FullName ?? "Foydalanuvchi");
        return $"<a href=\"tg://user?id={chat.Id}\">{name}</a>" + (chat.Username is null ? "" : " @" + Esc(chat.Username));
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? Join(string? a, string? b) =>
        string.Join(" ", new[] { a, b }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } s ? s : null;

    public static string Esc(string? s) => WebUtility.HtmlEncode(s ?? "").Replace("&#39;", "'").Replace("&quot;", "\"");

    /// <summary>L.T matni (oddiy) → Telegram HTML.</summary>
    private static string Html(string text) => Esc(text);

    private static string? Cut(string? s, int max) => s is null ? null : s.Length > max ? s[..max] : s;

    private static string Short(string s, int max) => s.Length > max ? s[..(max - 1)].TrimEnd() + "…" : s;

    /// <summary>Telegram HTML matnini qatorlar bo'yicha sahifalarga bo'ladi (teglar qator ichida yopiq — build_bot_kb.py).</summary>
    public static List<string> Paginate(string html, int max)
    {
        var pages = new List<string>();
        var cur = new System.Text.StringBuilder();
        foreach (var raw in html.Split('\n'))
        {
            string line = raw;
            if (line.Length > max) line = Esc(Short(BotSearch.PlainText(line), max - 10));
            if (cur.Length > 0 && cur.Length + line.Length + 1 > max)
            {
                pages.Add(cur.ToString());
                cur.Clear();
            }
            if (cur.Length > 0) cur.Append('\n');
            cur.Append(line);
        }
        if (cur.Length > 0 || pages.Count == 0) pages.Add(cur.ToString());
        return pages;
    }
}
