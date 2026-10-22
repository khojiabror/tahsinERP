using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;
using tahsinERP.ControlPanel.I18n;

namespace tahsinERP.ControlPanel.Pages.Subscriptions;

[Authorize(Policy = Policies.Sales)]
public class CreateModel(ControlDbContext db, SubscriptionService subscriptions, Clock clock) : PanelPageModel
{
    public Tenant Tenant { get; private set; } = null!;
    public List<Plan> Plans { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public int TenantId { get; set; }
    [BindProperty] public int PlanId { get; set; }
    [BindProperty] public BillingPeriod Period { get; set; } = BillingPeriod.Month;
    [BindProperty] public int? Users { get; set; }
    [BindProperty] public DateOnly StartDate { get; set; }
    [BindProperty] public bool AutoRenew { get; set; }

    private async Task<bool> LoadAsync()
    {
        var t = await db.Tenants.FindAsync(TenantId);
        if (t is null) return false;
        Tenant = t;
        Plans = await db.Plans.Where(p => p.IsActive && p.Code != PlanCodes.Trial).OrderBy(p => p.SortOrder).ToListAsync();
        return true;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();
        StartDate = clock.Today;
        PlanId = Plans.FirstOrDefault()?.Id ?? 0;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadAsync()) return NotFound();
        Subscription? sub = null;
        if (!await TryAsync(async () => sub = await subscriptions.CreateAsync(Tenant, PlanId, Period, Users, StartDate, AutoRenew)))
            return Page();
        FlashOk = sub!.Plan?.IsAddOn == true ? L.T("Pnl_Sub_AddOnCreated") : L.T("Pnl_Sub_Created");
        return RedirectToPage("/Invoices/Create", new { tenantId = TenantId, subscriptionId = sub!.Id });
    }
}

[Authorize(Policy = Policies.Sales)]
public class EditModel(ControlDbContext db, SubscriptionService subscriptions) : PanelPageModel
{
    public Subscription Sub { get; private set; } = null!;
    public List<Plan> Plans { get; private set; } = [];
    [BindProperty] public int PlanId { get; set; }
    [BindProperty] public int Users { get; set; }
    [BindProperty] public bool AutoRenew { get; set; }
    [BindProperty] public string? Reason { get; set; }

    private async Task<bool> LoadAsync(int id)
    {
        var s = await db.Subscriptions.Include(x => x.Plan).Include(x => x.Tenant).FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return false;
        Sub = s;
        Plans = await db.Plans.Where(p => p.IsActive && p.Code != PlanCodes.Trial).OrderBy(p => p.SortOrder).ToListAsync();
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        PlanId = Sub.PlanId; Users = Sub.Users; AutoRenew = Sub.AutoRenew;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (Sub.Period == BillingPeriod.Trial) { ModelState.AddModelError("", L.T("Pnl_Sub_TrialNotEditable")); return Page(); }
        if (!await TryAsync(() => subscriptions.UpdateAsync(Sub, PlanId, Users, AutoRenew))) return Page();
        FlashOk = L.T("Pnl_Sub_Saved");
        return RedirectToPage("/Tenants/Details", new { id = Sub.TenantId });
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!await TryAsync(() => subscriptions.CancelAsync(Sub, Reason ?? ""))) return Page();
        FlashOk = L.T("Pnl_Sub_Cancelled");
        return RedirectToPage("/Tenants/Details", new { id = Sub.TenantId });
    }
}
