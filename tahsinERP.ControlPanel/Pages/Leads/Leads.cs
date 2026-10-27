using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.I18n;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Leads;

/// <summary>
/// tahsinerp.uz demo so'rovlari. Xodim bog'lanadi ("Bog'lanildi"), sinov ochadi (Tenants/Trial?demo=ID — forma
/// so'rovdan to'ldiriladi, sinov ochilgach so'rov tenantga bog'lanadi) yoki rad etadi. Owner va Sales.
/// </summary>
public class IndexModel(ControlDbContext db, Clock clock) : PanelPageModel
{
    [BindProperty(SupportsGet = true)] public bool All { get; set; }
    [BindProperty] public int RequestId { get; set; }
    [BindProperty] public string? Note { get; set; }
    public List<DemoRequest> Rows { get; private set; } = [];
    public PageInfo Pager { get; private set; } = new(1, PageInfo.DefaultSize, 0);
    public Dictionary<int, string> StaffNames { get; private set; } = [];
    public Dictionary<int, string> Tenants { get; private set; } = [];

    private async Task LoadAsync()
    {
        var q = db.DemoRequests.AsNoTracking();
        if (!All) q = q.Where(r => r.Status == DemoRequestStatus.New || r.Status == DemoRequestStatus.Contacted);
        (Rows, Pager) = await q.OrderByDescending(r => r.Id).ToPageAsync(HttpContext);
        StaffNames = await db.StaffUsers.ToDictionaryAsync(s => s.Id, s => s.FullName);
        var ids = Rows.Where(r => r.TenantId != null).Select(r => r.TenantId!.Value).ToList();
        Tenants = await db.Tenants.Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.LegalName);
    }

    public async Task OnGetAsync() => await LoadAsync();

    public Task<IActionResult> OnPostContactedAsync() => SetStatusAsync(DemoRequestStatus.Contacted, "Pnl_Lead_MarkedContacted");

    public Task<IActionResult> OnPostRejectAsync() => SetStatusAsync(DemoRequestStatus.Rejected, "Pnl_Lead_Closed");

    /// <param name="doneKey">natija xabari kaliti ({0} = so'rov raqami)</param>
    private async Task<IActionResult> SetStatusAsync(DemoRequestStatus status, string doneKey)
    {
        if (!CanSell) return Forbidden();
        var r = await db.DemoRequests.FindAsync(RequestId);
        if (r is null) return NotFound();
        if (r.Status == DemoRequestStatus.TrialOpened)
        {
            FlashError = L.T("Pnl_Lead_AlreadyTrial", r.Id);
            return RedirectToPage(new { All });
        }
        r.Status = status;
        if (!string.IsNullOrWhiteSpace(Note))
            r.StaffNote = Note.Trim().Length > 1000 ? Note.Trim()[..1000] : Note.Trim();
        r.HandledByStaffId = StaffId;
        r.HandledAtUtc = clock.UtcNow;
        await db.SaveChangesAsync();
        FlashOk = L.T(doneKey, r.Id);
        return RedirectToPage(new { All });
    }

    public static string Of(DemoRequestStatus s) => s switch
    {
        DemoRequestStatus.New => L.T("Pnl_Lead_St_New"),
        DemoRequestStatus.Contacted => L.T("Pnl_Lead_St_Contacted"),
        DemoRequestStatus.TrialOpened => L.T("Pnl_Lead_St_TrialOpened"),
        DemoRequestStatus.Rejected => L.T("Pnl_Lead_St_Rejected"),
        _ => s.ToString(),
    };

    public static string Badge(DemoRequestStatus s) => s switch
    {
        DemoRequestStatus.New => "warn",
        DemoRequestStatus.Contacted => "info",
        DemoRequestStatus.TrialOpened => "ok",
        _ => "muted",
    };

    /// <summary>Faoliyat turi joriy tilda (xodimlarga ketadigan xabar — DemoRequestService.IndustryText, o'zbekcha).</summary>
    public static string Industry(string? code) =>
        code is not null && DemoRequestService.Industries.Contains(code) ? L.T("Pnl_Lead_Ind_" + code) : DemoRequestService.IndustryText(code);
}
