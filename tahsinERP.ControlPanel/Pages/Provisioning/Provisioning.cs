using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;
using tahsinERP.ControlPanel.I18n;

namespace tahsinERP.ControlPanel.Pages.Provisioning;

/// <summary>Barcha provisioning joblari (navbat holati).</summary>
public class IndexModel(ControlDbContext db) : PanelPageModel
{
    [BindProperty(SupportsGet = true)] public ProvisioningJobStatus? Status { get; set; }
    public List<ProvisioningJob> Jobs { get; private set; } = [];
    public List<Server> Servers { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var q = db.ProvisioningJobs.AsNoTracking().Include(j => j.Tenant).AsQueryable();
        if (Status is not null) q = q.Where(j => j.Status == Status);
        Jobs = await q.OrderByDescending(j => j.Id).Take(200).ToListAsync();
        Servers = await db.Servers.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        if (Jobs.Any(j => j.IsActive)) ViewData["AutoRefresh"] = 10;
    }
}

/// <summary>Bitta job: to'liq log, natija, qayta urinish / bekor qilish.</summary>
public class JobModel(ControlDbContext db, ProvisioningService provisioning) : PanelPageModel
{
    public ProvisioningJob Job { get; private set; } = null!;
    public string? CreatedBy { get; private set; }

    private async Task<bool> LoadAsync(long id)
    {
        var j = await db.ProvisioningJobs.Include(x => x.Tenant).FirstOrDefaultAsync(x => x.Id == id);
        if (j is null) return false;
        Job = j;
        CreatedBy = j.CreatedByStaffId is int sid ? await db.StaffUsers.Where(s => s.Id == sid).Select(s => s.FullName).FirstOrDefaultAsync() : null;
        if (j.IsActive) ViewData["AutoRefresh"] = 5;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(long id) => await LoadAsync(id) ? Page() : NotFound();

    public async Task<IActionResult> OnPostRetryAsync(long id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!CanSell || (Job.Type is ProvisioningJobType.Archive or ProvisioningJobType.Delete && !IsOwner)) return Forbidden();
        if (await TryAsync(() => provisioning.RetryAsync(Job)))
        {
            FlashOk = L.T("Pnl_Prov_Requeued");
            return RedirectToPage(new { id });
        }
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(long id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!CanSell) return Forbidden();
        if (await TryAsync(() => provisioning.CancelAsync(Job)))
        {
            FlashOk = L.T("Pnl_Prov_Cancelled");
            return RedirectToPage(new { id });
        }
        return Page();
    }
}
