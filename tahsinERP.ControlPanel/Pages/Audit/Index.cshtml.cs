using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Audit;

/// <summary>AuditLog ko'rish — faqat Owner. Yozuvlar faqat qo'shiladi, UI'da tahrirlash/o'chirish yo'q.</summary>
public class IndexModel(ControlDbContext db) : PanelPageModel
{
    [BindProperty(SupportsGet = true)] public string? Entity { get; set; }
    [BindProperty(SupportsGet = true)] public string? EntityId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Action { get; set; }
    public List<AuditEntry> Rows { get; private set; } = [];
    public PageInfo Pager { get; private set; } = new(1, PageInfo.DefaultSize, 0);
    public Dictionary<int, string> StaffNames { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var q = db.AuditLog.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Entity)) q = q.Where(a => a.Entity == Entity);
        if (!string.IsNullOrWhiteSpace(EntityId)) q = q.Where(a => a.EntityId == EntityId);
        if (!string.IsNullOrWhiteSpace(Action)) q = q.Where(a => a.Action.StartsWith(Action));
        (Rows, Pager) = await q.OrderByDescending(a => a.Id).ToPageAsync(HttpContext);
        StaffNames = await db.StaffUsers.ToDictionaryAsync(s => s.Id, s => s.FullName);
    }
}
