using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Licensing;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Tenants;

public class TenantInput
{
    [Required(ErrorMessage = "Slug majburiy"), StringLength(63)] public string Slug { get; set; } = "";
    [Required(ErrorMessage = "Yuridik nom majburiy"), StringLength(300)] public string LegalName { get; set; } = "";
    [StringLength(14)] public string? Inn { get; set; }
    [StringLength(200)] public string? ContactName { get; set; }
    [StringLength(50)] public string? Phone { get; set; }
    [StringLength(200), EmailAddress(ErrorMessage = "Email noto'g'ri")] public string? Email { get; set; }
    [StringLength(50)] public string? TelegramChatId { get; set; }
    [StringLength(500)] public string? Address { get; set; }
    public DeploymentKind Deployment { get; set; } = DeploymentKind.SaaS;
    [Range(1, int.MaxValue, ErrorMessage = "Musbat son")] public int CompanyId { get; set; } = 1;
    [StringLength(128)] public string? DbName { get; set; }
    public int? ServerId { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }

    public static TenantInput From(Tenant t) => new()
    {
        Slug = t.Slug, LegalName = t.LegalName, Inn = t.Inn, ContactName = t.ContactName, Phone = t.Phone, Email = t.Email,
        TelegramChatId = t.TelegramChatId, Address = t.Address, Deployment = t.Deployment, CompanyId = t.CompanyId,
        DbName = t.DbName, ServerId = t.ServerId, Notes = t.Notes,
    };

    public void ApplyTo(Tenant t, bool includeSlug)
    {
        if (includeSlug) t.Slug = Slug;
        t.LegalName = LegalName; t.Inn = Inn; t.ContactName = ContactName?.Trim(); t.Phone = Phone?.Trim(); t.Email = Email?.Trim();
        t.TelegramChatId = TelegramChatId?.Trim(); t.Address = Address?.Trim(); t.Deployment = Deployment; t.CompanyId = CompanyId;
        t.DbName = DbName; t.ServerId = ServerId; t.Notes = Notes;
    }
}

public class IndexModel(ControlDbContext db) : PanelPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public TenantStatus? Status { get; set; }
    public List<Row> Rows { get; private set; } = [];

    public record Row(Tenant Tenant, string? Plan, DateOnly? EndDate, long DebtTiyin);

    public async Task OnGetAsync()
    {
        var q = db.Tenants.AsNoTracking().AsQueryable();
        if (Status is not null) q = q.Where(t => t.Status == Status);
        else q = q.Where(t => t.Status != TenantStatus.Archived);
        if (!string.IsNullOrWhiteSpace(Q))
        {
            string s = Q.Trim();
            q = q.Where(t => t.Slug.Contains(s) || t.LegalName.Contains(s) || (t.Inn != null && t.Inn.Contains(s)) || (t.Phone != null && t.Phone.Contains(s)));
        }
        var tenants = await q.OrderBy(t => t.LegalName).Take(500).ToListAsync();
        var ids = tenants.Select(t => t.Id).ToList();
        var subs = await db.Subscriptions.Where(s => ids.Contains(s.TenantId) && s.Status == SubscriptionStatus.Active)
            .Select(s => new { s.TenantId, Plan = s.Plan!.Name, s.EndDate }).ToListAsync();
        var debts = await db.Invoices.Where(i => ids.Contains(i.TenantId) && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid))
            .GroupBy(i => i.TenantId).Select(g => new { g.Key, D = g.Sum(i => i.TotalTiyin - i.PaidTiyin) }).ToDictionaryAsync(x => x.Key, x => x.D);
        Rows = tenants.Select(t =>
        {
            var s = subs.Where(x => x.TenantId == t.Id).OrderByDescending(x => x.EndDate).FirstOrDefault();
            return new Row(t, s?.Plan, s?.EndDate, debts.GetValueOrDefault(t.Id));
        }).ToList();
    }
}

[Authorize(Policy = Policies.Sales)]
public class CreateModel(ControlDbContext db, TenantService tenants) : PanelPageModel
{
    [BindProperty] public TenantInput Input { get; set; } = new();
    [BindProperty] public bool StartTrial { get; set; } = true;
    public List<SelectListItem> Servers { get; private set; } = [];

    private async Task LoadAsync() => Servers = await ServerList.LoadAsync(db);

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        if (!ModelState.IsValid) return Page();
        var t = new Tenant();
        Input.ApplyTo(t, includeSlug: true);
        if (!await TryAsync(() => tenants.CreateAsync(t, StartTrial))) return Page();
        FlashOk = $"«{t.LegalName}» yaratildi." + (StartTrial ? " 14 kunlik sinov boshlandi." : "");
        return RedirectToPage("Details", new { id = t.Id });
    }
}

[Authorize(Policy = Policies.Sales)]
public class EditModel(ControlDbContext db, TenantService tenants) : PanelPageModel
{
    [BindProperty] public TenantInput Input { get; set; } = new();
    public Tenant Tenant { get; private set; } = null!;
    public List<SelectListItem> Servers { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var t = await db.Tenants.FindAsync(id);
        if (t is null) return NotFound();
        Tenant = t;
        Input = TenantInput.From(t);
        Servers = await ServerList.LoadAsync(db);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var t = await db.Tenants.FindAsync(id);
        if (t is null) return NotFound();
        Tenant = t;
        Servers = await ServerList.LoadAsync(db);
        ModelState.Remove("Input.Slug");
        if (!ModelState.IsValid) return Page();
        if (t.InstallationId != null && Input.CompanyId != t.CompanyId)
        {
            ModelState.AddModelError(string.Empty, "CompanyID ERP ro'yxatdan o'tganda olingan — o'zgartirsangiz kalit ERP'da rad etiladi.");
            return Page();
        }
        var before = tenants.TakeSnapshot(t);
        Input.ApplyTo(t, includeSlug: false);
        if (!await TryAsync(() => tenants.UpdateAsync(t, before))) return Page();
        FlashOk = "Saqlandi.";
        return RedirectToPage("Details", new { id });
    }
}

public class DetailsModel(ControlDbContext db, TenantService tenants, LicenseIssuer issuer) : PanelPageModel
{
    public Tenant Tenant { get; private set; } = null!;
    public List<Subscription> Subscriptions { get; private set; } = [];
    public List<Invoice> Invoices { get; private set; } = [];
    public List<License> Licenses { get; private set; } = [];
    public License? Current { get; private set; }
    public List<AuditEntry> Audit { get; private set; } = [];
    public Dictionary<int, string> StaffNames { get; private set; } = [];
    /// <summary>Yangi chiqarilgan reg_token — faqat shu javobda bir marta ko'rsatiladi.</summary>
    public string? NewRegToken { get; private set; }
    [BindProperty] public TenantStatus NewStatus { get; set; }
    [BindProperty] public string? Reason { get; set; }

    private async Task<bool> LoadAsync(int id)
    {
        var t = await db.Tenants.Include(x => x.Server).FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return false;
        Tenant = t;
        Subscriptions = await db.Subscriptions.Include(s => s.Plan).Where(s => s.TenantId == id).OrderByDescending(s => s.Id).ToListAsync();
        Invoices = await db.Invoices.Where(i => i.TenantId == id).OrderByDescending(i => i.Id).Take(50).ToListAsync();
        Licenses = await db.Licenses.Where(l => l.TenantId == id).OrderByDescending(l => l.Id).Take(50).ToListAsync();
        Current = await issuer.GetCurrentAsync(t);
        var subIds = Subscriptions.Select(s => s.Id.ToString()).ToList();
        var invIds = Invoices.Select(i => i.Id.ToString()).ToList();
        Audit = await db.AuditLog.Where(a => (a.Entity == "Tenant" && a.EntityId == id.ToString())
                                             || (a.Entity == "Subscription" && subIds.Contains(a.EntityId!))
                                             || (a.Entity == "Invoice" && invIds.Contains(a.EntityId!)))
            .OrderByDescending(a => a.Id).Take(30).ToListAsync();
        StaffNames = await db.StaffUsers.ToDictionaryAsync(s => s.Id, s => s.FullName);
        NewStatus = t.Status;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id) => await LoadAsync(id) ? Page() : NotFound();

    public async Task<IActionResult> OnPostRegTokenAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!CanSell && !User.IsInRole(nameof(StaffRole.Support))) return Forbidden();
        string? token = null;
        if (await TryAsync(async () => token = await tenants.IssueRegTokenAsync(Tenant)))
            NewRegToken = token;
        await LoadAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostStatusAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!CanSell || (NewStatus == TenantStatus.Archived && !IsOwner)) return Forbidden();
        if (await TryAsync(() => tenants.ChangeStatusAsync(Tenant, NewStatus, Reason)))
        {
            FlashOk = $"Holat: {Labels.Of(NewStatus)}.";
            return RedirectToPage(new { id });
        }
        return Page();
    }

    /// <summary>Obuna bo'yicha kalitni qayta chiqarish (masalan to'lovdan keyin imzolash xatosi bo'lgan bo'lsa).</summary>
    public async Task<IActionResult> OnPostEnsureLicenseAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!CanSell) return Forbidden();
        License? issued = null;
        try
        {
            if (!await TryAsync(async () => issued = await issuer.EnsureForTenantAsync(Tenant))) return Page();
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or InvalidOperationException or NotSupportedException)
        {
            FlashError = "Kalit chiqmadi: " + ex.Message;
            return RedirectToPage(new { id });
        }
        FlashOk = issued is null ? "Joriy kalit obunaga mos — yangi kalit kerak emas (yoki tenant bog'lanmagan / faol obuna yo'q)." : $"Yangi kalit chiqarildi (#{issued.Id}).";
        return RedirectToPage(new { id });
    }
}

internal static class ServerList
{
    public static async Task<List<SelectListItem>> LoadAsync(ControlDbContext db)
    {
        var list = await db.Servers.Where(s => s.IsActive).OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Name + " (" + s.Host + ")", s.Id.ToString())).ToListAsync();
        list.Insert(0, new SelectListItem("— tanlanmagan —", ""));
        return list;
    }
}
