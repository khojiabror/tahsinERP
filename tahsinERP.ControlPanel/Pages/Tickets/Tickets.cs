using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Bot;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.I18n;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Tickets;

/// <summary>
/// «Murojaatlar»: Telegram bot orqali kelgan texnik yordam murojaatlari (buglar) va demo/konsultatsiya so'rovlari —
/// kim, qachon, nima yozgan. Dasturchi / support mutaxassisi hal qilgach «Hal qilindi» (izoh bilan) belgilaydi.
/// Barcha xodimlar (Owner, Sales, Support).
/// </summary>
public class IndexModel(ControlDbContext db, BotTicketService tickets) : PanelPageModel
{
    /// <summary>open (standart) / closed / all.</summary>
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    /// <summary>support / request (demo + konsultatsiya) / bo'sh — hammasi.</summary>
    [BindProperty(SupportsGet = true)] public string? Kind { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty] public int ThreadId { get; set; }
    [BindProperty] public string? Note { get; set; }

    public List<Row> Rows { get; private set; } = [];
    public PageInfo Pager { get; private set; } = new(1, PageInfo.DefaultSize, 0);
    public int OpenSupport { get; private set; }
    public int OpenRequests { get; private set; }

    public sealed record Row(BotThread Thread, BotChat? Chat, int Messages, string? ResolvedBy);

    public string EffectiveStatus => Status is "closed" or "all" ? Status : "open";

    public async Task OnGetAsync(CancellationToken ct)
    {
        OpenSupport = await db.BotThreads.CountAsync(t => t.Status == BotThreadStatus.Open && t.Kind == BotThreadKind.Support, ct);
        OpenRequests = await db.BotThreads.CountAsync(t => t.Status == BotThreadStatus.Open && t.Kind != BotThreadKind.Support, ct);

        var q = db.BotThreads.AsNoTracking();
        q = EffectiveStatus switch
        {
            "open" => q.Where(t => t.Status == BotThreadStatus.Open),
            "closed" => q.Where(t => t.Status == BotThreadStatus.Closed),
            _ => q,
        };
        if (Kind == "support") q = q.Where(t => t.Kind == BotThreadKind.Support);
        else if (Kind == "request") q = q.Where(t => t.Kind != BotThreadKind.Support);
        if (!string.IsNullOrWhiteSpace(Q))
        {
            string s = Q.Trim();
            int.TryParse(s.TrimStart('#', '№'), out int id);
            q = q.Where(t => t.Id == id || (t.Company != null && t.Company.Contains(s)) || (t.Subject != null && t.Subject.Contains(s))
                || db.BotChats.Any(c => c.Id == t.ChatId && ((c.FullName != null && c.FullName.Contains(s)) || (c.FirstName != null && c.FirstName.Contains(s))
                    || (c.Username != null && c.Username.Contains(s)) || (c.Phone != null && c.Phone.Contains(s)))));
        }
        // Ochiqlar — eng eski faollik yuqorida (kutayotgan mijoz birinchi); yopilganlar — eng yangisi.
        var ordered = EffectiveStatus == "open" ? q.OrderBy(t => t.LastActivityUtc) : q.OrderByDescending(t => t.Id);
        var (threads, pager) = await ordered.ToPageAsync(HttpContext);
        Pager = pager;
        var ids = threads.Select(t => t.Id).ToList();
        var chatIds = threads.Select(t => t.ChatId).Distinct().ToList();
        var chats = await db.BotChats.AsNoTracking().Where(c => chatIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var counts = await db.BotThreadMessages.Where(m => ids.Contains(m.ThreadId)).GroupBy(m => m.ThreadId)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var staffIds = threads.Where(t => t.ResolvedByStaffId != null).Select(t => t.ResolvedByStaffId!.Value).Distinct().ToList();
        var staff = await db.StaffUsers.Where(u => staffIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        Rows = threads.Select(t => new Row(t, chats.GetValueOrDefault(t.ChatId), counts.GetValueOrDefault(t.Id),
            t.ResolvedByStaffId is int sid ? staff.GetValueOrDefault(sid) : t.ClosedBy)).ToList();
    }

    public async Task<IActionResult> OnPostResolveAsync(CancellationToken ct)
    {
        var t = await db.BotThreads.FirstOrDefaultAsync(x => x.Id == ThreadId, ct);
        if (t is null) return NotFound();
        if (t.Status == BotThreadStatus.Open)
        {
            await tickets.ResolveAsync(t, StaffId, StaffName, Note, notifyUser: true, ct);
            FlashOk = L.T("Pnl_Tk_Resolved", t.Id);
        }
        return RedirectToPage(new { Status, Kind, Q });
    }

    public async Task<IActionResult> OnPostReopenAsync(CancellationToken ct)
    {
        var t = await db.BotThreads.FirstOrDefaultAsync(x => x.Id == ThreadId, ct);
        if (t is null) return NotFound();
        await tickets.ReopenAsync(t, StaffName, ct);
        FlashOk = L.T("Pnl_Tk_Reopened", t.Id);
        return RedirectToPage(new { Status, Kind, Q });
    }

    private string StaffName => User.Identity?.Name ?? "Xodim";

    public static string KindLabel(BotThreadKind k) => k switch
    {
        BotThreadKind.Demo => L.T("Pnl_Bot_KindDemo"),
        BotThreadKind.Consultation => L.T("Pnl_Bot_KindConsult"),
        _ => L.T("Pnl_Tk_KindBug"),
    };

    public static string KindBadge(BotThreadKind k) => k == BotThreadKind.Support ? "bad" : k == BotThreadKind.Demo ? "info" : "warn";

    public static string Who(BotChat? c) =>
        c is null ? "—" : c.FullName ?? string.Join(" ", new[] { c.FirstName, c.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

/// <summary>Bitta murojaat: yozishma tarixi, paneldan javob yozish, hal qilindi / qayta ochish.</summary>
public class DetailsModel(ControlDbContext db, BotTicketService tickets) : PanelPageModel
{
    public BotThread Thread { get; private set; } = null!;
    public BotChat? Chat { get; private set; }
    public List<BotThreadMessage> Messages { get; private set; } = [];
    public DemoRequest? Request { get; private set; }
    public string? ResolvedBy { get; private set; }

    [BindProperty] public string? Note { get; set; }
    [BindProperty] public bool NotifyUser { get; set; } = true;
    [BindProperty] public string? ReplyText { get; set; }

    private async Task<bool> LoadAsync(int id, CancellationToken ct)
    {
        var t = await db.BotThreads.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return false;
        Thread = t;
        Chat = await db.BotChats.AsNoTracking().FirstOrDefaultAsync(c => c.Id == t.ChatId, ct);
        Messages = await db.BotThreadMessages.AsNoTracking().Where(m => m.ThreadId == id).OrderBy(m => m.Id).ToListAsync(ct);
        Request = t.DemoRequestId is int rid ? await db.DemoRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rid, ct) : null;
        ResolvedBy = t.ResolvedByStaffId is int sid ? await db.StaffUsers.Where(u => u.Id == sid).Select(u => u.FullName).FirstOrDefaultAsync(ct) : t.ClosedBy;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct) => await LoadAsync(id, ct) ? Page() : NotFound();

    public async Task<IActionResult> OnPostResolveAsync(int id, CancellationToken ct)
    {
        var t = await db.BotThreads.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        if (t.Status == BotThreadStatus.Open)
        {
            await tickets.ResolveAsync(t, StaffId, StaffName, Note, NotifyUser, ct);
            FlashOk = L.T("Pnl_Tk_Resolved", t.Id);
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReopenAsync(int id, CancellationToken ct)
    {
        var t = await db.BotThreads.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        await tickets.ReopenAsync(t, StaffName, ct);
        FlashOk = L.T("Pnl_Tk_Reopened", t.Id);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReplyAsync(int id, CancellationToken ct)
    {
        var t = await db.BotThreads.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        if (string.IsNullOrWhiteSpace(ReplyText))
        {
            FlashError = L.T("Pnl_Tk_ReplyEmpty");
            return RedirectToPage(new { id });
        }
        if (await tickets.ReplyAsync(t, StaffName, ReplyText, ct))
            FlashOk = L.T("Pnl_Tk_ReplySent");
        else
            FlashError = L.T("Pnl_Tk_ReplyFailed");
        return RedirectToPage(new { id });
    }

    private string StaffName => User.Identity?.Name ?? "Xodim";
}
