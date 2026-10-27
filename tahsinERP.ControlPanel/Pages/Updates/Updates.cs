using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.I18n;
using tahsinERP.ControlPanel.Services;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.ControlPanel.Pages.Updates;

public sealed record AppRow(string App, InstalledApp? Installed, AppRelease? Offer, List<AppRelease> Newer, UpdateJob? Active);

public sealed record TenantRow(Tenant Tenant, ErpInstanceInfo? Instance, AppRelease? Offer, List<AppRelease> Newer, UpdateJob? Active);

/// <summary>Yangilanishlar (Owner): server ilovalari va firma ERP instance'lari — joriy/mavjud versiya, o'rnatish, rollback.</summary>
public class IndexModel(ControlDbContext db, UpdatesService updates, IReleaseSource source, Clock clock) : PanelPageModel
{
    [BindProperty(SupportsGet = true)] public int? ServerId { get; set; }
    public List<Server> Servers { get; private set; } = [];
    public Server? Server { get; private set; }
    public UpdateInventory? Inventory { get; private set; }
    public DateTime? InventoryAtUtc { get; private set; }
    public bool SourceConfigured => source.IsConfigured;
    public List<AppRelease> Releases { get; private set; } = [];
    public List<AppRow> Apps { get; private set; } = [];
    public List<TenantRow> Tenants { get; private set; } = [];
    public List<UpdateJob> Jobs { get; private set; } = [];
    public DateTime UtcNow => clock.UtcNow;

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        await updates.ExpireStaleAsync();
        Servers = await db.Servers.AsNoTracking().Where(s => s.AgentSecretProtected != null).OrderBy(s => s.Name).ToListAsync();
        Server = Servers.FirstOrDefault(s => s.Id == ServerId) ?? Servers.FirstOrDefault();
        ServerId = Server?.Id;
        Releases = (await db.AppReleases.AsNoTracking().ToListAsync())
            .OrderByDescending(r => ReleaseVerifier.ParseVersion(r.Version)).Take(20).ToList();
        if (Server is null) return;
        (Inventory, InventoryAtUtc) = await updates.InventoryAsync(Server.Id);
        var valid = await updates.ValidReleasesAsync();
        Jobs = await db.UpdateJobs.AsNoTracking().Where(j => j.ServerId == Server.Id).OrderByDescending(j => j.Id).Take(30).ToListAsync();
        var active = Jobs.Where(j => j.IsActive).ToList();
        foreach (var app in ReleaseApps.ServerApps)
        {
            var inst = Inventory?.Apps.FirstOrDefault(a => a.App == app);
            Apps.Add(new AppRow(app, inst, UpdatesService.Offer(valid, app, inst?.Version), UpdatesService.NewerThan(valid, app, inst?.Version),
                active.FirstOrDefault(j => j.App == app)));
        }
        var tenants = await db.Tenants.AsNoTracking()
            .Where(t => t.ServerId == Server.Id && t.DbName != null && t.DeletedAtUtc == null && t.Status != TenantStatus.Archived)
            .OrderBy(t => t.Slug).ToListAsync();
        foreach (var t in tenants)
        {
            var inst = Inventory?.Instances.FirstOrDefault(i => i.Slug == t.Slug);
            string? v = inst?.Version;
            Tenants.Add(new TenantRow(t, inst, inst is null ? null : UpdatesService.Offer(valid, ReleaseApps.Erp, v),
                inst is null ? valid.Where(r => UpdatesService.Contains(r, ReleaseApps.Erp)).ToList() : UpdatesService.NewerThan(valid, ReleaseApps.Erp, v),
                active.FirstOrDefault(j => j.Slug == t.Slug)));
        }
        if (active.Count > 0) ViewData["AutoRefresh"] = 10;
    }

    public async Task<IActionResult> OnPostRefreshAsync()
    {
        if (await TryAsync(async () =>
        {
            int n = await updates.RefreshAsync(HttpContext.RequestAborted);
            FlashOk = n > 0 ? L.T("Pnl_Upd_NewReleases", n) : L.T("Pnl_Upd_NoNewReleases");
        })) return RedirectToPage(new { ServerId });
        await LoadAsync();
        return Page();
    }

    public Task<IActionResult> OnPostInstallAsync(string app, string version) =>
        EnqueueAsync(() => updates.EnqueueAsync(ServerId ?? 0, UpdateAction.InstallApp, app, null, version, false));

    public Task<IActionResult> OnPostRollbackAsync(string app) =>
        EnqueueAsync(() => updates.EnqueueAsync(ServerId ?? 0, UpdateAction.RollbackApp, app, null, null, false));

    public Task<IActionResult> OnPostTenantAsync(int tenantId, UpdateAction action, string? version, bool restoreDatabase) =>
        EnqueueAsync(() => updates.EnqueueAsync(ServerId ?? 0, action, ReleaseApps.Erp, tenantId, version, restoreDatabase));

    private async Task<IActionResult> EnqueueAsync(Func<Task<UpdateJob>> enqueue)
    {
        UpdateJob? job = null;
        if (await TryAsync(async () => job = await enqueue()))
        {
            FlashOk = L.T("Pnl_Upd_Enqueued", job!.Id);
            return RedirectToPage("Job", new { id = job.Id });
        }
        await LoadAsync();
        return Page();
    }

    /// <summary>Reliz izohidan shu ilova bo'limi ("### erp" ... ) — bo'lmasa butun matn.</summary>
    public static string NotesFor(AppRelease r, string app)
    {
        string notes = r.Notes ?? "";
        string marker = "### " + app;
        int i = notes.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return notes.Trim();
        int start = notes.IndexOf('\n', i);
        if (start < 0) return "";
        int end = notes.IndexOf("\n### ", start, StringComparison.Ordinal);
        return (end < 0 ? notes[start..] : notes[start..end]).Trim();
    }
}

/// <summary>Bitta yangilash job'i: bosqich, jonli log (avtomatik yangilanadi), bekor qilish.</summary>
public class JobModel(ControlDbContext db, UpdatesService updates) : PanelPageModel
{
    public UpdateJob Job { get; private set; } = null!;
    public string? CreatedBy { get; private set; }

    private async Task<bool> LoadAsync(long id)
    {
        await updates.ExpireStaleAsync();
        var j = await db.UpdateJobs.Include(x => x.Server).Include(x => x.Tenant).FirstOrDefaultAsync(x => x.Id == id);
        if (j is null) return false;
        Job = j;
        CreatedBy = j.CreatedByStaffId is int sid ? await db.StaffUsers.Where(s => s.Id == sid).Select(s => s.FullName).FirstOrDefaultAsync() : null;
        if (j.IsActive) ViewData["AutoRefresh"] = 3;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(long id) => await LoadAsync(id) ? Page() : NotFound();

    public async Task<IActionResult> OnPostCancelAsync(long id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (await TryAsync(() => updates.CancelAsync(Job)))
        {
            FlashOk = L.T("Pnl_Upd_Cancelled");
            return RedirectToPage(new { id });
        }
        return Page();
    }
}

public static class UpdateLabels
{
    public static string Of(UpdateAction a) => a switch
    {
        UpdateAction.InstallApp => L.T("Pnl_Upd_Action_InstallApp"),
        UpdateAction.InstallTenant => L.T("Pnl_Upd_Action_InstallTenant"),
        UpdateAction.CreateInstance => L.T("Pnl_Upd_Action_CreateInstance"),
        UpdateAction.RollbackApp => L.T("Pnl_Upd_Rollback"),
        UpdateAction.RollbackTenant => L.T("Pnl_Upd_Action_RollbackTenant"),
        UpdateAction.MigrateTenant => L.T("Pnl_Upd_Action_MigrateTenant"),
        UpdateAction.RemoveInstance => L.T("Pnl_Upd_Action_RemoveInstance"),
        _ => a.ToString(),
    };

    public static string Of(UpdateJobStatus s) => s switch
    {
        UpdateJobStatus.Pending => L.T("Pnl_JobStatus_Pending"),
        UpdateJobStatus.Running => L.T("Pnl_JobStatus_Running"),
        UpdateJobStatus.Succeeded => L.T("Pnl_JobStatus_Succeeded"),
        UpdateJobStatus.Failed => L.T("Pnl_JobStatus_Failed"),
        UpdateJobStatus.RolledBack => L.T("Pnl_Upd_Status_RolledBack"),
        UpdateJobStatus.Cancelled => L.T("Pnl_JobStatus_Cancelled"),
        _ => s.ToString(),
    };

    public static string Badge(UpdateJobStatus s) => s switch
    {
        UpdateJobStatus.Succeeded => "ok", UpdateJobStatus.Running => "info", UpdateJobStatus.Pending => "warn",
        UpdateJobStatus.Failed or UpdateJobStatus.RolledBack => "bad", _ => "muted",
    };

    public static string Version(string? v) => v switch { null => L.T("Pnl_Upd_VersionUnknown"), "legacy" => L.T("Pnl_Upd_VersionLegacy"), _ => v };
}
