using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;
using tahsinERP.ControlPanel.I18n;

namespace tahsinERP.ControlPanel.Pages.Notifications;

public class IndexModel(ControlDbContext db, ITelegramSender telegram) : PanelPageModel
{
    public List<Notification> Rows { get; private set; } = [];
    public Dictionary<int, string> Tenants { get; private set; } = [];
    public bool TelegramConfigured => telegram.IsConfigured;

    public async Task OnGetAsync()
    {
        Rows = await db.Notifications.AsNoTracking().OrderByDescending(n => n.Id).Take(300).ToListAsync();
        var ids = Rows.Where(r => r.TenantId != null).Select(r => r.TenantId!.Value).Distinct().ToList();
        Tenants = await db.Tenants.Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.LegalName);
    }

    /// <summary>Kunlik ishni hozir ishga tushirish (status + eslatmalar) — faqat Owner. Takroriy eslatma yuborilmaydi.</summary>
    public async Task<IActionResult> OnPostRunAsync([FromServices] SubscriptionService subs, [FromServices] ExpiryReminderService reminders)
    {
        if (!IsOwner) return Forbidden();
        int changed = await subs.RunLifecycleAsync();
        int sent = await reminders.RunAsync();
        FlashOk = L.T("Pnl_Notif_RunDone", changed, sent);
        return RedirectToPage();
    }
}
