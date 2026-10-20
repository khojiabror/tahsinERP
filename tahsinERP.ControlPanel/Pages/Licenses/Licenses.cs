using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Licensing;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;

namespace tahsinERP.ControlPanel.Pages.Licenses;

public class IndexModel(ControlDbContext db) : PanelPageModel
{
    [BindProperty(SupportsGet = true)] public int? TenantId { get; set; }
    [BindProperty(SupportsGet = true)] public bool OnlyCurrent { get; set; }
    public List<License> Rows { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var q = db.Licenses.Include(l => l.Tenant).AsNoTracking().AsQueryable();
        if (TenantId is not null) q = q.Where(l => l.TenantId == TenantId);
        if (OnlyCurrent) q = q.Where(l => l.SupersededById == null && l.RevokedAtUtc == null);
        Rows = await q.OrderByDescending(l => l.Id).Take(300).ToListAsync();
    }
}

/// <summary>Qo'lda kalit chiqarish (on-prem, individual shartnoma, favqulodda) — faqat Owner, sabab majburiy.</summary>
[Authorize(Policy = Policies.Owner)]
public class IssueModel(ControlDbContext db, LicenseIssuer issuer, Clock clock) : PanelPageModel
{
    [BindProperty(SupportsGet = true)] public int TenantId { get; set; }
    [BindProperty] public string Edition { get; set; } = PlanCodes.Start;
    /// <summary>Belgilangan qo'shimcha xizmat kodlari (SITE...) — Edition'ga "+KOD" bo'lib qo'shiladi.</summary>
    [BindProperty] public List<string> AddOnCodes { get; set; } = [];
    [BindProperty] public int MaxUsers { get; set; } = 5;
    [BindProperty] public DateOnly StartDate { get; set; }
    [BindProperty] public DateOnly ExpiryDate { get; set; }
    [BindProperty] public string? Reason { get; set; }
    public Tenant Tenant { get; private set; } = null!;
    public List<Plan> Plans { get; private set; } = [];

    private async Task<bool> LoadAsync()
    {
        var t = await db.Tenants.FindAsync(TenantId);
        if (t is null) return false;
        Tenant = t;
        Plans = await db.Plans.OrderBy(p => p.SortOrder).ToListAsync();
        return true;
    }

    public IEnumerable<Plan> Packages => Plans.Where(p => !p.IsAddOn);
    public IEnumerable<Plan> AddOnPlans => Plans.Where(p => p.IsAddOn);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();
        var current = await issuer.GetCurrentAsync(Tenant);
        StartDate = clock.Today;
        ExpiryDate = current?.ExpiryDate ?? clock.Today.AddYears(1).AddDays(-1);
        if (current != null)
        {
            Edition = tahsinERP.Services.Editions.EditionModules.BaseEdition(current.Edition);
            AddOnCodes = AddOnPlans.Where(p => tahsinERP.Services.Editions.EditionModules.AddOnsOf(current.Edition)
                .Intersect(tahsinERP.Services.Editions.EditionModules.AddOnsOf("X+" + p.Code)).Any()).Select(p => p.Code).ToList();
            MaxUsers = current.MaxUsers;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadAsync()) return NotFound();
        if (string.IsNullOrWhiteSpace(Reason)) ModelState.AddModelError(nameof(Reason), "Sabab majburiy (audit uchun).");
        if (!Packages.Any(p => p.Code == Edition)) ModelState.AddModelError(nameof(Edition), "Noma'lum paket.");
        if (AddOnCodes.Any(c => !AddOnPlans.Any(p => p.Code == c))) ModelState.AddModelError(nameof(AddOnCodes), "Noma'lum qo'shimcha xizmat.");
        if (!ModelState.IsValid) return Page();
        string edition;
        try { edition = LicenseIssuer.ComposeEdition(Edition, AddOnCodes); }
        catch (DomainException ex) { ModelState.AddModelError("", ex.Message); return Page(); }
        License? issued = null;
        try
        {
            if (!await TryAsync(async () => issued = await issuer.IssueAsync(Tenant,
                    new LicenseRequest(edition, MaxUsers, StartDate, ExpiryDate, null, Reason!.Trim()), LicenseSource.Manual, StaffId)))
                return Page();
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or InvalidOperationException or NotSupportedException)
        {
            ModelState.AddModelError("", "Imzolab bo'lmadi: " + ex.Message);
            return Page();
        }
        FlashOk = $"Kalit chiqarildi (#{issued!.Id}).";
        return RedirectToPage("Details", new { id = issued.Id });
    }
}

public class DetailsModel(ControlDbContext db, LicenseIssuer issuer) : PanelPageModel
{
    public License License { get; private set; } = null!;
    public Dictionary<int, string> StaffNames { get; private set; } = [];
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public DateOnly? ReplacementExpiry { get; set; }

    private async Task<bool> LoadAsync(int id)
    {
        var l = await db.Licenses.Include(x => x.Tenant).FirstOrDefaultAsync(x => x.Id == id);
        if (l is null) return false;
        License = l;
        StaffNames = await db.StaffUsers.ToDictionaryAsync(s => s.Id, s => s.FullName);
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id) => await LoadAsync(id) ? Page() : NotFound();

    /// <summary>Kalitni matn fayl sifatida (on-prem offline o'rnatish uchun).</summary>
    public async Task<IActionResult> OnGetDownloadAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        return File(System.Text.Encoding.UTF8.GetBytes(License.LicenseKey), "text/plain", $"tahsinERP-{License.Tenant!.Slug}-{License.Id}.lic");
    }

    public async Task<IActionResult> OnPostRevokeAsync(int id)
    {
        if (!IsOwner) return Forbidden();
        if (!await LoadAsync(id)) return NotFound();
        License? replacement = null;
        try
        {
            if (!await TryAsync(async () => replacement = await issuer.RevokeAsync(License, Reason ?? "", StaffId, ReplacementExpiry)))
                return Page();
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or InvalidOperationException or NotSupportedException)
        {
            FlashError = "Kalit bekor qilindi, lekin o'rniga yangisi chiqmadi: " + ex.Message;
            return RedirectToPage(new { id });
        }
        FlashOk = replacement is null
            ? "Kalit bekor qilindi. ERP keyingi pull'da 410 revoked oladi; kalit muddati o'zi tugaydi."
            : $"Kalit bekor qilindi, o'rniga {replacement.ExpiryDate:dd.MM.yyyy} gacha qisqa kalit chiqdi (#{replacement.Id}).";
        return RedirectToPage(new { id = replacement?.Id ?? id });
    }
}
