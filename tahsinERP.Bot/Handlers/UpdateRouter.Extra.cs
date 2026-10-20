using System.Collections.Concurrent;
using System.Globalization;
using tahsinERP.Bot.Erp;
using tahsinERP.Bot.Files;
using tahsinERP.Bot.Pipeline;
using tahsinERP.Bot.Telegram;

namespace tahsinERP.Bot.Handlers;

/// <summary>
/// Jonli joylashuv yangilanishlarini siyraklashtirish: bitta foydalanuvchidan ERP'ga ko'pi bilan har 30 soniyada bir nuqta
/// (Telegram jonli joylashuvni bir necha soniyada yangilaydi). ERP ham shu oraliqni tekshiradi — bu yerda ortiqcha HTTP chaqiruvlar kesiladi.
/// </summary>
public sealed class LiveThrottle(TimeProvider clock)
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<(int, long), DateTimeOffset> _last = new();

    public bool Allow(int tenantId, long telegramUserId)
    {
        var now = clock.GetUtcNow();
        var key = (tenantId, telegramUserId);
        if (_last.TryGetValue(key, out var at) && now - at < Interval && now >= at) return false;
        _last[key] = now;
        if (_last.Count > 10_000)
            foreach (var old in _last.Where(p => now - p.Value > TimeSpan.FromHours(1)).Select(p => p.Key).ToList()) _last.TryRemove(old, out _);
        return true;
    }
}

public sealed partial class UpdateRouter
{
    private static decimal Coord(double v) => Math.Round((decimal)v, 6);

    private static int? Accuracy(TgLocation loc) => loc.HorizontalAccuracy is double a ? (int?)Math.Round(a) : null;

    /// <summary>Eng katta (5 MB gacha) o'lchamdagi fotoni yuklab, fayl omboriga saqlaydi; bo'lmasa — foydalanuvchiga xabar, null.</summary>
    private async Task<string?> SaveTelegramPhotoAsync(Ctx ctx, List<TgPhotoSize> sizes, CancellationToken ct)
    {
        var best = sizes.Where(s => s.FileSize is null or <= LocalFileStore.MaxBytes).OrderByDescending(s => s.Width * s.Height).FirstOrDefault();
        if (best == null)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Photo_TooBig"));
            return null;
        }
        byte[]? bytes;
        try { bytes = await telegram.DownloadFileAsync(ctx.Tenant.Token, best.FileId, ct); }
        catch (HttpRequestException ex)
        {
            log.LogWarning(ex, "Telegram faylini yuklab bo'lmadi (tenant {Slug})", ctx.Tenant.Slug);
            bytes = null;
        }
        if (bytes == null || bytes.Length == 0 || bytes.Length > LocalFileStore.MaxBytes)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T(bytes == null ? "Photo_Failed" : "Photo_TooBig"));
            return null;
        }
        return await files.SaveAsync(ctx.Tenant.Slug, bytes, "jpg", ct);
    }

    private object SkipKeyboard(Ctx ctx) => Kb.Reply([[Kb.Text(ctx.T("Btn_Skip"))], [Kb.Text(ctx.T("Btn_Cancel"))]], oneTime: true);

    // ---------- kun boshi: joylashuv -> selfi ----------

    private async Task DayLocationAsync(Ctx ctx, decimal lat, decimal lng, CancellationToken ct)
    {
        var me = await ctx.Erp.GetAsync<ErpMe>("Me", ctx.UserId, ct);
        if (me?.Day?.StartAt != null)
        {
            await StartDayAsync(ctx, lat, lng, null, ct);   // kun allaqachon boshlangan — ERP o'shani qaytaradi
            return;
        }
        bool required = me?.RequireSelfie == true;
        await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.DaySelfie, new SessionData { Lat = lat, Lng = lng, SelfieRequired = required }, ct);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T(required ? "Day_AskSelfie" : "Day_AskSelfieOptional"),
            required ? CancelKeyboard(ctx) : SkipKeyboard(ctx));
    }

    private async Task SelfieAsync(Ctx ctx, SessionData data, List<TgPhotoSize> photo, CancellationToken ct)
    {
        var key = await SaveTelegramPhotoAsync(ctx, photo, ct);
        if (key != null) await StartDayAsync(ctx, data.Lat, data.Lng, key, ct);
    }

    private async Task SelfieSkippedAsync(Ctx ctx, SessionData data, CancellationToken ct)
    {
        if (data.SelfieRequired)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Day_SelfieRequired") + "\n" + ctx.T("Day_AskSelfie"), CancelKeyboard(ctx));
            return;
        }
        await StartDayAsync(ctx, data.Lat, data.Lng, null, ct);
    }

    private async Task StartDayAsync(Ctx ctx, decimal? lat, decimal? lng, string? selfieKey, CancellationToken ct)
    {
        var day = await ctx.Erp.PostAsync<ErpDayStarted>("DayStart", new { lat, lng, selfieKey }, ctx.UserId, ct);
        await sessions.ClearAsync(ctx.Tenant.Id, ctx.From.Id, ct);
        await SendMenuAsync(ctx, ctx.T("Day_Started", day?.StartAt.ToString("HH:mm", CultureInfo.InvariantCulture)) + "\n" + ctx.T("Day_LiveHint"), ct);
        await SendRouteAsync(ctx, null, ct);
    }

    // ---------- jonli joylashuv ----------

    /// <summary>
    /// Jonli joylashuv nuqtasini ERP'ga yuboradi. Tahrir (edited_message) yangilanishlari — jim: siyraklashtiriladi, xatolar faqat logga
    /// (har yangilanishda "aloqa yo'q" yozilmasin).
    /// </summary>
    private async Task<ErpTrack?> TrackAsync(Ctx ctx, TgLocation loc, bool silent, CancellationToken ct)
    {
        if (!liveThrottle.Allow(ctx.Tenant.Id, ctx.From.Id) && silent) return null;
        try
        {
            return await ctx.Erp.PostAsync<ErpTrack>("Track", new { lat = Coord(loc.Latitude), lng = Coord(loc.Longitude), accuracyM = Accuracy(loc) },
                ctx.UserId, ct);
        }
        catch (Exception ex) when (silent && (ex is ErpException or HttpRequestException || ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            log.LogInformation("Jonli joylashuv yozilmadi (tenant {Slug}, tg {User}): {Message}", ctx.Tenant.Slug, ctx.From.Id, ex.Message);
            return null;
        }
    }

    private async Task LiveStartedAsync(Ctx ctx, TgLocation loc, CancellationToken ct)
    {
        var r = await TrackAsync(ctx, loc, silent: false, ct);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T(r?.DayOpen == true ? "Live_Started" : "Live_NoDay"));
    }

    // ---------- yangi savdo nuqtasi ----------

    private async Task NewPointStartAsync(Ctx ctx, CancellationToken ct)
    {
        if (!RequireField(ctx)) return;
        await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.NewName, new SessionData(), ct);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("New_AskName"), CancelKeyboard(ctx));
    }

    private async Task NewNameAsync(Ctx ctx, SessionData data, string text, CancellationToken ct)
    {
        var name = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (name.Length is < 2 or > 50)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("New_BadName"));
            return;
        }
        data.Name = name;
        await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.NewPhone, data, ct);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("New_AskPhone"), CancelKeyboard(ctx));
    }

    private async Task NewPhoneAsync(Ctx ctx, SessionData data, string text, CancellationToken ct)
    {
        if (Phones.Normalize(text) is not string phone)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("New_BadPhone"));
            return;
        }
        data.Phone = phone;
        await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.NewLocation, data, ct);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("New_AskLocation"), LocationKeyboard(ctx));
    }

    private async Task NewLocationAsync(Ctx ctx, SessionData data, decimal lat, decimal lng, CancellationToken ct)
    {
        data.Lat = lat;
        data.Lng = lng;
        await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.NewLandmark, data, ct);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("New_AskLandmark"), SkipKeyboard(ctx));
    }

    private async Task NewLandmarkAsync(Ctx ctx, SessionData data, string? text, CancellationToken ct)
    {
        data.Landmark = text == null ? null : text.Length <= 200 ? text : text[..200];
        await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.NewPhoto, data, ct);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("New_AskPhoto"), SkipKeyboard(ctx));
    }

    private async Task NewPhotoAsync(Ctx ctx, SessionData data, List<TgPhotoSize>? photo, CancellationToken ct)
    {
        if (photo != null)
        {
            var key = await SaveTelegramPhotoAsync(ctx, photo, ct);
            if (key == null) return;
            data.PhotoKey = key;
        }
        await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.NewConfirm, data, ct);
        var inv = CultureInfo.InvariantCulture;
        var text = ctx.T("New_Confirm", data.Name, data.Phone, data.Lat?.ToString("0.00000", inv) + ", " + data.Lng?.ToString("0.00000", inv),
            data.Landmark ?? "—", ctx.T(data.PhotoKey != null ? "Word_Yes" : "Word_No"));
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, text, Kb.Inline([Kb.Button(ctx.T("Btn_Save"), "nc!"), Kb.Button(ctx.T("Btn_Cancel"), "x")]));
    }

    private async Task NewPointSaveAsync(Ctx ctx, CancellationToken ct)
    {
        var (state, data) = await sessions.GetAsync(ctx.Tenant.Id, ctx.From.Id, ct);
        if (state != States.NewConfirm || data.Name == null)
        {
            await SendMenuAsync(ctx, ctx.T("Menu"), ct);
            return;
        }
        // ERP qayta yuborishda (javob yo'qolsa) agentning so'nggi bir xil nomli nuqtasini qaytaradi — dublikat bo'lmaydi
        var r = await ctx.Erp.PostAsync<ErpNewCustomer>("NewCustomer", new
        {
            name = data.Name, telephone = data.Phone, landmark = data.Landmark, lat = data.Lat, lng = data.Lng, photoKey = data.PhotoKey
        }, ctx.UserId, ct) ?? throw new ErpException(500, "Bo'sh javob");
        await sessions.ClearAsync(ctx.Tenant.Id, ctx.From.Id, ct);
        await SendMenuAsync(ctx, ctx.T(r.Duplicate ? "New_Duplicate" : "New_Saved"), ct);
        if (ctx.Link?.Role == "AGENT") await SendCardAsync(ctx, r.Id, ct);   // ekspeditor qo'shgan nuqta agentsiz — kartasi agentlarga ko'rinadi
    }

    // ---------- mijoz: qarz va akt sverki ----------

    private bool RequireCustomer(Ctx ctx)
    {
        if (ctx.Link?.CustomerId != null) return true;
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Customer_Only"));
        return false;
    }

    private async Task SendDebtAsync(Ctx ctx, CancellationToken ct)
    {
        if (!RequireCustomer(ctx)) return;
        var b = await ctx.Erp.GetAsync<ErpBalance>("MyBalance", null, ct, ("telegramUserId", ctx.From.Id)) ?? throw new ErpException(500, "Bo'sh javob");
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, DebtText(ctx.Lang, b));
    }

    internal static string DebtText(string lang, ErpBalance b)
    {
        var text = b.Debt > 0
            ? Texts.Get(lang, "Debt_Info", b.Name, Texts.Money(b.Debt), Texts.Money(b.NotDue), Texts.Money(b.Overdue),
                b.Overdue > 0 && b.MaxOverdueDays > 0 ? Texts.Get(lang, "Debt_MaxDays", b.MaxOverdueDays) : "", Texts.Money(b.CreditLimit), b.PaymentDays)
            : Texts.Get(lang, "Debt_None", b.Name);
        if (b.Advance > 0) text += "\n" + Texts.Get(lang, "Debt_Advance", Texts.Money(b.Advance));
        if (b.LastPayments is { Count: > 0 } pays)
            text += "\n\n" + Texts.Get(lang, "Debt_LastPayments") + "\n" +
                    string.Join("\n", pays.Select(p => Texts.Get(lang, "Debt_PaymentLine", p.Date, Texts.Money(p.Amount))));
        return text;
    }

    private void AskStatementPeriod(Ctx ctx)
    {
        if (!RequireCustomer(ctx)) return;
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Stmt_AskPeriod"), Kb.Inline(
            [Kb.Button(ctx.T("Stmt_ThisMonth"), "ak:m0"), Kb.Button(ctx.T("Stmt_LastMonth"), "ak:m1")],
            [Kb.Button(ctx.T("Stmt_3Months"), "ak:q")]));
    }

    /// <summary>Davr: m0 — shu oy boshidan bugungacha, m1 — o'tgan oy to'liq, q — oxirgi 3 oy.</summary>
    internal static (DateOnly From, DateOnly To)? StatementPeriod(string? code, DateOnly today) => code switch
    {
        "m0" => (new DateOnly(today.Year, today.Month, 1), today),
        "m1" => (new DateOnly(today.Year, today.Month, 1).AddMonths(-1), new DateOnly(today.Year, today.Month, 1).AddDays(-1)),
        "q" => (today.AddMonths(-3).AddDays(1), today),
        _ => null
    };

    private async Task SendStatementAsync(Ctx ctx, string? code, CancellationToken ct)
    {
        if (!RequireCustomer(ctx)) return;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), DailySummaryWorker.ResolveZone(options.Value.TimeZone)).DateTime);
        if (StatementPeriod(code, today) is not var (from, to)) return;
        var s = await ctx.Erp.GetAsync<ErpStatement>("MyStatement", null, ct, ("telegramUserId", ctx.From.Id),
                    ("from", from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), ("to", to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    ("lang", ctx.Lang))
                ?? throw new ErpException(500, "Bo'sh javob");
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, StatementText(ctx.Lang, s));
    }

    // ---------- pilot: xato / muammo (ERP xatolar jurnaliga) ----------

    private async Task AskIssueAsync(Ctx ctx, CancellationToken ct)
    {
        if (ctx.UserId == null)
        {
            outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("NotAllowed"));
            return;
        }
        await sessions.SetAsync(ctx.Tenant.Id, ctx.From.Id, States.IssueText, null, ct);
        outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Issue_Ask"), CancelKeyboard(ctx));
    }

    /// <summary>Matn (yoki izohli rasm) → ERP Issue. Rasm izohsiz bo'lsa ham qabul qilinadi.</summary>
    private async Task SaveIssueAsync(Ctx ctx, string? text, List<TgPhotoSize>? photo, CancellationToken ct)
    {
        string? photoKey = photo is { Count: > 0 } ? await SaveTelegramPhotoAsync(ctx, photo, ct) : null;
        text = text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length < 5)
        {
            if (photoKey == null)
            {
                outbox.Send(ctx.Tenant.Id, ctx.ChatId, ctx.T("Issue_TooShort"));
                return;
            }
            text = ctx.T("Issue_PhotoOnly");
        }
        var r = await ctx.Erp.PostAsync<ErpIssueResult>("Issue", new { description = text, place = "Telegram", photoKey }, ctx.UserId, ct);
        await sessions.ClearAsync(ctx.Tenant.Id, ctx.From.Id, ct);
        await SendMenuAsync(ctx, ctx.T("Issue_Saved", r?.IssueNo ?? "—"), ct);
    }

    /// <summary>Akt sverki matni; Telegram chegarasi (4096) ichida — sig'magan yozuvlar soni ko'rsatiladi.</summary>
    internal static string StatementText(string lang, ErpStatement s, int maxLength = 3800)
    {
        var head = Texts.Get(lang, "Stmt_Head", s.Name, s.From, s.To, Texts.Money(s.Opening));
        var foot = Texts.Get(lang, "Stmt_Foot", Texts.Money(s.TotalDebit), Texts.Money(s.TotalCredit), Texts.Money(s.Closing));
        var rows = s.Rows ?? [];
        if (rows.Count == 0) return head + "\n\n" + Texts.Get(lang, "Stmt_Empty") + "\n\n" + foot;

        var lines = rows.Select(r => r.Date[..Math.Min(5, r.Date.Length)] + " " + r.Type + (string.IsNullOrWhiteSpace(r.Doc) ? "" : " " + r.Doc) + ": " +
                                     (r.Debit != 0 ? "+" + Texts.Money(r.Debit) : "") + (r.Credit != 0 ? (r.Debit != 0 ? " " : "") + "−" + Texts.Money(r.Credit) : "") +
                                     " → " + Texts.Money(r.Balance)).ToList();
        var budget = maxLength - head.Length - foot.Length - 80;
        var shown = new List<string>();
        foreach (var l in lines)
        {
            if (budget - (l.Length + 1) < 0) break;
            budget -= l.Length + 1;
            shown.Add(l);
        }
        var more = lines.Count - shown.Count;
        return head + "\n\n" + string.Join("\n", shown) + (more > 0 ? "\n" + Texts.Get(lang, "Stmt_More", more) : "") + "\n\n" + foot;
    }
}
