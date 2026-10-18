using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Provisioning;

public class IndexModel(ControlDbContext db) : PanelPageModel
{
    [BindProperty(SupportsGet = true)] public ProvisioningJobStatus? Status { get; set; }
    public List<ProvisioningJob> Rows { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var q = db.ProvisioningJobs.Include(j => j.Server).Include(j => j.Tenant).AsNoTracking().AsQueryable();
        if (Status is not null) q = q.Where(j => j.Status == Status);
        Rows = await q.OrderByDescending(j => j.Id).Take(300).ToListAsync();
    }
}

public class DetailsModel(ControlDbContext db, ProvisioningService provisioning) : PanelPageModel
{
    public ProvisioningJob Job { get; private set; } = null!;
    /// <summary>Faqat shu javobda ko'rsatiladi; bazada allaqachon o'chirilgan.</summary>
    public string? RevealedPassword { get; private set; }

    private async Task<bool> LoadAsync(int id)
    {
        var j = await db.ProvisioningJobs.Include(x => x.Server).Include(x => x.Tenant).FirstOrDefaultAsync(x => x.Id == id);
        if (j is null) return false;
        Job = j;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id) => await LoadAsync(id) ? Page() : NotFound();

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        if (!CanSell) return Forbidden();
        if (!await LoadAsync(id)) return NotFound();
        if (!await TryAsync(() => provisioning.CancelAsync(Job, StaffId))) return Page();
        FlashOk = "Ish bekor qilindi.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRevealAsync(int id)
    {
        if (!CanSell) return Forbidden();
        if (!await LoadAsync(id)) return NotFound();
        string? password = null;
        if (await TryAsync(async () => password = await provisioning.RevealTempPasswordAsync(Job, StaffId)))
            RevealedPassword = password;
        return Page();
    }
}
