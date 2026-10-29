using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Bot;

/// <summary>
/// Panel «Murojaatlar» sahifasidan bot yozishmalari bilan ishlash: hal qilindi / qayta ochish / javob yozish.
/// Har amal guruhdagi murojaat sarlavhasiga reply bilan ham qayd etiladi (guruh va panel bir xil holatni ko'radi).
/// Telegram xatosi amalni to'xtatmaydi — holat bazada saqlanadi, xato logga yoziladi.
/// </summary>
public sealed class BotTicketService(ControlDbContext db, IBotApi api, AuditLogger audit, Clock clock, ILogger<BotTicketService> log)
{
    public const int NoteMax = 1000;
    public const int ReplyMax = 3500;

    /// <summary>Hal qilindi: yopiladi, kim va izoh yoziladi; texnik yordam murojaatida mijozga xabar + baho so'rovi.</summary>
    public async Task ResolveAsync(BotThread thread, int staffId, string staffName, string? note, bool notifyUser, CancellationToken ct = default)
    {
        note = string.IsNullOrWhiteSpace(note) ? null : Cut(note.Trim(), NoteMax);
        var before = new { thread.Status, thread.Resolution };
        thread.Status = BotThreadStatus.Closed;
        thread.ClosedAtUtc = clock.UtcNow;
        thread.ClosedBy = Cut(staffName, 100);
        thread.ResolvedByStaffId = staffId;
        thread.Resolution = note;
        thread.LastActivityUtc = clock.UtcNow;
        audit.Add("bot.thread-resolve", "BotThread", thread.Id, before, new { thread.Status, thread.Resolution, Notified = notifyUser });
        await db.SaveChangesAsync(ct);

        await PostToGroupAsync(thread, $"✅ <b>№{thread.Id} hal qilindi</b> — {SalesBot.Esc(staffName)} (panel)" +
                                       (note is null ? "" : "\n" + SalesBot.Esc(note)), ct);
        if (!notifyUser || thread.Kind != BotThreadKind.Support) return;
        var chat = await db.BotChats.AsNoTracking().FirstOrDefaultAsync(c => c.Id == thread.ChatId, ct);
        if (chat is null) return;
        string lang = chat.Lang ?? "uz";
        string extra = note is null ? "" : BotContent.T(lang, "thread.resolvedNote", SalesBot.Esc(note));
        await TryUserAsync(chat, () => api.SendAsync(chat.Id, BotContent.T(lang, "thread.resolved", thread.Id, extra),
            Kb.Inline(Enumerable.Range(1, 5).Select(i => Kb.Btn("⭐" + i, $"rate:{thread.Id}:{i}")).ToArray()), ct: ct), ct);
    }

    public async Task ReopenAsync(BotThread thread, string staffName, CancellationToken ct = default)
    {
        if (thread.Status == BotThreadStatus.Open) return;
        thread.Status = BotThreadStatus.Open;
        thread.ClosedAtUtc = null;
        thread.LastActivityUtc = clock.UtcNow;
        audit.Add("bot.thread-reopen", "BotThread", thread.Id, new { BotThreadStatus.Closed }, new { BotThreadStatus.Open });
        await db.SaveChangesAsync(ct);
        await PostToGroupAsync(thread, $"🔄 №{thread.Id} qayta ochildi — {SalesBot.Esc(staffName)} (panel)", ct);
    }

    /// <summary>Paneldan mijozga javob (bot orqali) + guruhga nusxa. false — mijozga yetmadi (bloklagan yoki Telegram xatosi).</summary>
    public async Task<bool> ReplyAsync(BotThread thread, string staffName, string text, CancellationToken ct = default)
    {
        text = Cut(text.Trim(), ReplyMax)!;
        var chat = await db.BotChats.FirstOrDefaultAsync(c => c.Id == thread.ChatId, ct);
        if (chat is null || text.Length == 0) return false;
        string lang = chat.Lang ?? "uz";
        long userMsg = 0;
        bool ok = await TryUserAsync(chat, async () => userMsg = await api.SendAsync(chat.Id,
            BotContent.T(lang, "thread.staffReply", thread.Id, SalesBot.Esc(text)),
            SalesBot.ThreadButtons(lang, thread.Id, thread.Kind == BotThreadKind.Support), ct: ct), ct);
        if (!ok) return false;

        long? groupMsg = await PostToGroupAsync(thread, $"💬 <b>№{thread.Id}</b> · {SalesBot.Esc(staffName)} (panel):\n{SalesBot.Esc(text)}", ct);
        thread.Status = BotThreadStatus.Open;
        thread.ClosedAtUtc = null;
        thread.LastActivityUtc = clock.UtcNow;
        db.BotThreadMessages.Add(new BotThreadMessage
        {
            ThreadId = thread.Id, FromStaff = true, Author = Cut(staffName, 100), Text = Cut(text, 4000), GroupChatId = groupMsg is null ? null : thread.GroupChatId,
            GroupMessageId = groupMsg, UserMessageId = userMsg == 0 ? null : userMsg, CreatedAtUtc = clock.UtcNow,
        });
        if (thread.DemoRequestId is int rid && await db.DemoRequests.FirstOrDefaultAsync(r => r.Id == rid, ct) is { Status: DemoRequestStatus.New } r)
        {
            r.Status = DemoRequestStatus.Contacted;
            r.HandledAtUtc = clock.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<long?> PostToGroupAsync(BotThread thread, string html, CancellationToken ct)
    {
        if (thread.GroupChatId is not long group || !api.IsConfigured) return null;
        try
        {
            return await api.SendAsync(group, html, replyTo: thread.GroupMessageId, ct: ct);
        }
        catch (Exception ex) when (ex is BotApiException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Murojaat №{Id}: guruhga xabar yuborilmadi", thread.Id);
            return null;
        }
    }

    private async Task<bool> TryUserAsync(BotChat chat, Func<Task> send, CancellationToken ct)
    {
        if (!api.IsConfigured) return false;
        try
        {
            await send();
            return true;
        }
        catch (BotApiException ex) when (ex.Blocked)
        {
            var tracked = await db.BotChats.FirstOrDefaultAsync(c => c.Id == chat.Id, ct);
            if (tracked is not null)
            {
                tracked.BlockedAtUtc = clock.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            return false;
        }
        catch (Exception ex) when (ex is BotApiException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Bot: mijozga ({Chat}) xabar yetkazilmadi", chat.Id);
            return false;
        }
    }

    private static string? Cut(string? s, int max) => s is null ? null : s.Length > max ? s[..max] : s;
}
