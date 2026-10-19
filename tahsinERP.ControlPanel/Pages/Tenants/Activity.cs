using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Tenants;

/// <summary>
/// Tenant foydalanuvchi faolligi: kunlik grafik, modullar, foydalanuvchilar, so'nggi kirish/amallar (filtr bilan).
/// Ma'lumot agent yuborgan nusxadan (panel tenant bazasiga ulanmaydi); IP/MAC/F.I.Sh./email yo'q.
/// </summary>
public class ActivityModel(ControlDbContext db, UsageService usage, Clock clock) : PanelPageModel
{
    public Tenant Tenant { get; private set; } = null!;
    [BindProperty(SupportsGet = true)] public int Days { get; set; } = 30;
    [BindProperty(SupportsGet = true)] public int? UserId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Module { get; set; }
    [BindProperty(SupportsGet = true)] public UsageEventKind? Kind { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    public List<UsageService.DayRow> DayRows { get; private set; } = [];
    public List<UsageService.ModuleRow> Modules { get; private set; } = [];
    public List<UsageUser> Users { get; private set; } = [];
    public UsageState? State { get; private set; }
    public List<UsageEvent> Events { get; private set; } = [];
    public Dictionary<int, string> Logins { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var t = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        Tenant = t;
        Days = Days is 7 or 30 or 90 ? Days : 30;
        (DayRows, Modules, Users, State) = await usage.SummaryAsync(id, Days);
        Logins = Users.ToDictionary(u => u.UserId, u => u.Login);
        Events = await usage.EventsAsync(id, UserId, Module, Kind, Q, 200);
        return Page();
    }

    public DateTime Local(DateTime utc) => clock.ToLocal(utc);

    public string LoginOf(int userId) => Logins.TryGetValue(userId, out var l) ? l : "#" + userId;
}
