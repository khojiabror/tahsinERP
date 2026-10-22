using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
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
    public Dictionary<int, string> StaffNames { get; private set; } = [];
    public Dictionary<int, string> Tenants { get; private set; } = [];

    private async Task LoadAsync()
    {
        var q = db.DemoRequests.AsNoTracking();
        if (!All) q = q.Where(r => r.Status == DemoRequestStatus.New || r.Status == DemoRequestStatus.Contacted);
        Rows = await q.OrderByDescending(r => r.Id).Take(300).ToListAsync();
        StaffNames = await db.StaffUsers.ToDictionaryAsync(s => s.Id, s => s.FullName);
        var ids = Rows.Where(r => r.TenantId != null).Select(r => r.TenantId!.Value).ToList();
        Tenants = await db.Tenants.Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.LegalName);
    }

    public async Task OnGetAsync() => await LoadAsync();

    public Task<IActionResult> OnPostContactedAsync() => SetStatusAsync(DemoRequestStatus.Contacted, "bog'lanildi deb belgilandi");

    public Task<IActionResult> OnPostRejectAsync() => SetStatusAsync(DemoRequestStatus.Rejected, "yopildi");

    private async Task<IActionResult> SetStatusAsync(DemoRequestStatus status, string done)
    {
        if (!CanSell) return Forbidden();
        var r = await db.DemoRequests.FindAsync(RequestId);
        if (r is null) return NotFound();
        if (r.Status == DemoRequestStatus.TrialOpened)
        {
            FlashError = $"So'rov #{r.Id} uchun sinov allaqachon ochilgan.";
            return RedirectToPage(new { All });
        }
        r.Status = status;
        if (!string.IsNullOrWhiteSpace(Note))
            r.StaffNote = Note.Trim().Length > 1000 ? Note.Trim()[..1000] : Note.Trim();
        r.HandledByStaffId = StaffId;
        r.HandledAtUtc = clock.UtcNow;
        await db.SaveChangesAsync();
        FlashOk = $"So'rov #{r.Id} {done}.";
        return RedirectToPage(new { All });
    }

    public static string Of(DemoRequestStatus s) => s switch
    {
        DemoRequestStatus.New => "Yangi",
        DemoRequestStatus.Contacted => "Bog'lanildi",
        DemoRequestStatus.TrialOpened => "Sinov ochildi",
        DemoRequestStatus.Rejected => "Yopildi",
        _ => s.ToString(),
    };

    public static string Badge(DemoRequestStatus s) => s switch
    {
        DemoRequestStatus.New => "warn",
        DemoRequestStatus.Contacted => "info",
        DemoRequestStatus.TrialOpened => "ok",
        _ => "muted",
    };

    public static string Industry(string? code) => DemoRequestService.IndustryText(code);
}
