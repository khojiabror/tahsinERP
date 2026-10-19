using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages;

public class IndexModel(DashboardService dashboard, ControlDbContext db, UsageService usage) : PanelPageModel
{
    /// <summary>Baza yaratilgan, lekin InactiveTrialDays (3) kundan beri kirilmagan sinov tenantlari.</summary>
    public List<UsageService.InactiveTrial> InactiveTrials { get; private set; } = [];

    public DashboardData Data { get; private set; } = null!;
    public int UnboundCount { get; private set; }
    public int FailedNotifications { get; private set; }

    public int Count(TenantStatus s) => Data.TenantsByStatus.TryGetValue(s, out int c) ? c : 0;

    public async Task OnGetAsync()
    {
        Data = await dashboard.GetAsync();
        UnboundCount = await db.Tenants.CountAsync(t => t.InstallationId == null && t.Status != TenantStatus.Archived);
        FailedNotifications = await db.Notifications.CountAsync(n => n.Status == NotificationStatus.Failed);
        InactiveTrials = await usage.InactiveTrialsAsync();
    }
}
