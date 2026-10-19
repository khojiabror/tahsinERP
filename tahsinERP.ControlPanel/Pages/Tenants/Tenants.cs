using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using tahsinERP.ControlPanel.Data;
using tahsinERP.ControlPanel.Licensing;
using tahsinERP.ControlPanel.Security;
using tahsinERP.ControlPanel.Services;
using tahsinERP.ControlPanel.Hosting;
using tahsinERP.ControlPanel.Portal;
using Microsoft.Extensions.Options;

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

public class DetailsModel(ControlDbContext db, TenantService tenants, LicenseIssuer issuer, ProvisioningService provisioning, UsageService usage,
    PortalAuthService portalAuth, PortalNotifier notifier, IOptions<PanelHostOptions> hosts) : PanelPageModel
{
    /// <summary>Faqat sinov tenanti (pullik obunasi bo'lmagan) — «Sinovni yakunlash» ko'rinadi.</summary>
    public bool IsTrialOnly { get; private set; }
    public UsageState? Usage { get; private set; }
    public int Logins7d { get; private set; }
    public int Actions7d { get; private set; }
    [BindProperty] public string? EndTrialConfirmSlug { get; set; }
    [BindProperty] public string? EndTrialReason { get; set; }
    [BindProperty] public bool EndTrialBackup { get; set; } = true;

    // ── Mijoz portali foydalanuvchilari ──
    public List<PortalUser> PortalUsers { get; private set; } = [];
    /// <summary>Yangi taklif havolasi — faqat shu javobda bir marta ko'rsatiladi (bazada xeshi).</summary>
    public string? NewInviteUrl { get; private set; }
    public bool InviteSentToTelegram { get; private set; }
    public string? InviteFor { get; private set; }
    [BindProperty] public string? PuName { get; set; }
    [BindProperty] public string? PuEmail { get; set; }
    [BindProperty] public string? PuPhone { get; set; }
    [BindProperty] public PortalRole PuRole { get; set; } = PortalRole.Owner;
    [BindProperty] public int PortalUserId { get; set; }

    public List<ProvisioningJob> Jobs { get; private set; } = [];
    public ProvisioningJob? ActiveJob => Jobs.FirstOrDefault(j => j.IsActive);
    public bool IsDemo { get; private set; }
    public string? TenantUrl { get; private set; }
    /// <summary>Create job uchun yaratilgan vaqtinchalik admin paroli — faqat shu javobda bir marta ko'rsatiladi.</summary>
    public string? NewTempPassword { get; private set; }
    [BindProperty] public string? ProvisionReason { get; set; }

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
        PortalUsers = await db.PortalUsers.Where(u => u.TenantId == id).OrderBy(u => u.Id).ToListAsync();
        Jobs = await db.ProvisioningJobs.AsNoTracking().Where(j => j.TenantId == id).OrderByDescending(j => j.Id).Take(30).ToListAsync();
        IsDemo = provisioning.IsDemo(t);
        IsTrialOnly = t.Deployment == DeploymentKind.SaaS && t.DeletedAtUtc == null && await provisioning.IsTrialOnlyAsync(t);
        Usage = await db.UsageStates.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == id);
        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var counts = await db.UsageEvents.Where(e => e.TenantId == id && e.AtUtc >= weekAgo).GroupBy(e => e.Kind)
            .Select(g => new { g.Key, C = g.Count() }).ToListAsync();
        Logins7d = counts.FirstOrDefault(c => c.Key == Data.UsageEventKind.Login)?.C ?? 0;
        Actions7d = counts.FirstOrDefault(c => c.Key == Data.UsageEventKind.Action)?.C ?? 0;
        TenantUrl = provisioning.TenantUrl(t);
        if (ActiveJob != null) ViewData["AutoRefresh"] = 10;
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

    /// <summary>Baza yaratish (Create job). Vaqtinchalik parol shu javobda bir marta ko'rsatiladi.</summary>
    public async Task<IActionResult> OnPostProvisionAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!CanSell) return Forbidden();
        ProvisioningService.EnqueueResult? r = null;
        if (await TryAsync(async () => r = await provisioning.EnqueueAsync(Tenant, ProvisioningJobType.Create)))
        {
            if (r!.TempPassword is null)
            {
                FlashOk = $"Yaratish jobi allaqachon navbatda (#{r.Job.Id}).";
                return RedirectToPage(new { id });
            }
            NewTempPassword = r.TempPassword;
        }
        await LoadAsync(id);
        return Page();
    }

    public Task<IActionResult> OnPostSuspendAsync(int id) => RunAsync(id, false, t => provisioning.SuspendAsync(t, ProvisionReason), "To'xtatildi.");

    public Task<IActionResult> OnPostResumeAsync(int id) => RunAsync(id, false, t => provisioning.ResumeAsync(t, ProvisionReason), "Qayta yoqildi.");

    public Task<IActionResult> OnPostResetAsync(int id) => RunAsync(id, false,
        async t => (await provisioning.EnqueueAsync(t, ProvisioningJobType.Reset, ProvisionReason ?? "Qo'lda")).Job, "Qayta tiklash navbatga qo'yildi.");

    public Task<IActionResult> OnPostArchiveAsync(int id) => RunAsync(id, true,
        async t => (await provisioning.EnqueueAsync(t, ProvisioningJobType.Archive, ProvisionReason)).Job, "Arxivlash navbatga qo'yildi.");

    /// <summary>Sinovni muddatidan oldin yakunlash (Owner/Sales, sabab + slug tasdig'i, ixtiyoriy arxiv).</summary>
    public async Task<IActionResult> OnPostEndTrialAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!CanSell) return Forbidden();
        ProvisioningJob? job = null;
        if (await TryAsync(async () => job = await provisioning.EndTrialAsync(Tenant, EndTrialReason, EndTrialConfirmSlug, EndTrialBackup)))
        {
            FlashOk = job is null ? "Sinov yakunlandi (baza yaratilmagan edi) — tenant arxivlandi."
                : $"Sinov yakunlanmoqda: kirish yopildi, baza o'chiriladi{(EndTrialBackup ? " (oldin .bak arxiv)" : "")}. Job #{job.Id}.";
            return RedirectToPage(new { id });
        }
        await LoadAsync(id);
        return Page();
    }

    private async Task<IActionResult> RunAsync(int id, bool ownerOnly, Func<Tenant, Task<ProvisioningJob?>> action, string ok)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (ownerOnly ? !IsOwner : !CanSell) return Forbidden();
        ProvisioningJob? job = null;
        if (await TryAsync(async () => job = await action(Tenant)))
        {
            FlashOk = ok + (job != null ? $" Job #{job.Id}." : "");
            return RedirectToPage(new { id });
        }
        await LoadAsync(id);
        return Page();
    }

    /// <summary>Portal foydalanuvchisini qo'shadi va darhol taklif havolasini chiqaradi.</summary>
    public async Task<IActionResult> OnPostPortalUserAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!CanSell) return Forbidden();
        PortalUser? user = null;
        if (await TryAsync(async () => user = await portalAuth.CreateUserAsync(id, PuName ?? "", PuEmail, PuPhone, PuRole, StaffId)))
            await IssueInviteAsync(user!);
        await LoadAsync(id);
        return Page();
    }

    /// <summary>Taklifni qayta yuborish / parolni tiklash: yangi bir martalik havola (eskisi yopiladi).</summary>
    public async Task<IActionResult> OnPostPortalInviteAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!CanSell) return Forbidden();
        var user = PortalUsers.FirstOrDefault(u => u.Id == PortalUserId);
        if (user is null) return NotFound();
        await IssueInviteAsync(user);
        await LoadAsync(id);
        return Page();
    }

    private async Task IssueInviteAsync(PortalUser user)
    {
        string baseUrl = HostZones.PortalBaseUrl(HttpContext, hosts.Value);
        InviteResult? r = null;
        if (await TryAsync(async () => r = await portalAuth.CreateInviteAsync(user, StaffId, baseUrl, notifier)))
        {
            NewInviteUrl = PortalAuthService.InviteUrl(baseUrl, r!.Token);
            InviteSentToTelegram = r.SentToTelegram;
            InviteFor = user.FullName;
        }
    }

    /// <summary>Rol (Owner/Viewer), faollik yoki Telegram bog'lanishi. Foydalanuvchi sessiyalari yopiladi.</summary>
    public async Task<IActionResult> OnPostPortalToggleAsync(int id, string what)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!CanSell) return Forbidden();
        var user = PortalUsers.FirstOrDefault(u => u.Id == PortalUserId);
        if (user is null) return NotFound();
        Func<Task> action = what switch
        {
            "role" => () => portalAuth.UpdateUserAsync(user, user.Role == PortalRole.Owner ? PortalRole.Viewer : PortalRole.Owner, user.IsActive, StaffId),
            "active" => () => portalAuth.UpdateUserAsync(user, user.Role, !user.IsActive, StaffId),
            "telegram" => () => portalAuth.UnlinkTelegramAsync(user, StaffId),
            _ => () => throw new DomainException("Noma'lum amal."),
        };
        if (await TryAsync(action))
        {
            FlashOk = "Portal foydalanuvchisi yangilandi (uning sessiyalari yopildi).";
            return RedirectToPage(new { id });
        }
        await LoadAsync(id);
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

/// <summary>
/// Arxivlangan tenantni butunlay o'chirish (Delete job): faqat Owner, ikki bosqichli tasdiq — 1) slug'ni qo'lda yozish + sabab,
/// 2) yakuniy tasdiq sahifasi. Baza (agar qolgan bo'lsa), arxiv zaxirasi va tenants.json yozuvi o'chiriladi; moliyaviy tarix qoladi.
/// </summary>
[Authorize(Policy = Policies.Owner)]
public class DeleteModel(ControlDbContext db, ProvisioningService provisioning) : PanelPageModel
{
    public Tenant Tenant { get; private set; } = null!;
    [BindProperty] public string? ConfirmSlug { get; set; }
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public bool Understand { get; set; }
    /// <summary>1 — birinchi forma, 2 — yakuniy tasdiq.</summary>
    [BindProperty] public int Step { get; set; } = 1;

    private async Task<bool> LoadAsync(int id)
    {
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return false;
        Tenant = t;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        Step = 1;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (Tenant.Status != TenantStatus.Archived)
            ModelState.AddModelError(string.Empty, "O'chirish faqat arxivlangan tenant uchun.");
        if (!string.Equals(ConfirmSlug?.Trim(), Tenant.Slug, StringComparison.Ordinal))
            ModelState.AddModelError(nameof(ConfirmSlug), "Slug mos kelmadi.");
        if (string.IsNullOrWhiteSpace(Reason))
            ModelState.AddModelError(nameof(Reason), "Sabab majburiy.");
        if (!Understand)
            ModelState.AddModelError(nameof(Understand), "Tasdiqlang.");
        if (!ModelState.IsValid) { Step = 1; return Page(); }
        if (Step < 2)
        {
            Step = 2; // birinchi tasdiq qabul qilindi — yakuniy sahifa
            return Page();
        }
        ProvisioningJob? job = null;
        if (!await TryAsync(async () => job = (await provisioning.EnqueueAsync(Tenant, ProvisioningJobType.Delete, Reason)).Job)) { Step = 1; return Page(); }
        FlashOk = $"O'chirish navbatga qo'yildi (job #{job!.Id}).";
        return RedirectToPage("Details", new { id });
    }
}

/// <summary>
/// «Sinov ochish»: SaaS tenant + TRIAL obunasi (14 kun, 5 foydalanuvchi) + Create job. Server tanlanmasa — bo'sh joyi eng ko'p server.
/// Vaqtinchalik admin paroli shu javobda bir marta ko'rsatiladi (va baza tayyor bo'lgach tenant Telegram chatiga yuboriladi).
/// </summary>
[Authorize(Policy = Policies.Sales)]
public class TrialModel(ControlDbContext db, TenantService tenants, ProvisioningService provisioning) : PanelPageModel
{
    public class TrialInput
    {
        [Required(ErrorMessage = "Slug majburiy"), StringLength(63)] public string Slug { get; set; } = "";
        [Required(ErrorMessage = "Yuridik nom majburiy"), StringLength(300)] public string LegalName { get; set; } = "";
        [StringLength(14)] public string? Inn { get; set; }
        [StringLength(200)] public string? ContactName { get; set; }
        [StringLength(50)] public string? Phone { get; set; }
        [StringLength(200), EmailAddress(ErrorMessage = "Email noto'g'ri")] public string? Email { get; set; }
        [StringLength(50)] public string? TelegramChatId { get; set; }
        public int? ServerId { get; set; }
        /// <summary>Namuna ma'lumotlar bilan (Database/Demo): mijoz bo'sh emas, to'ldirilgan tizimni ko'radi.</summary>
        public bool SampleData { get; set; } = true;
    }

    [BindProperty] public TrialInput Input { get; set; } = new();
    public List<SelectListItem> Servers { get; private set; } = [];
    public Tenant? Created { get; private set; }
    public ProvisioningJob? Job { get; private set; }
    public string? TempPassword { get; private set; }
    public string? TenantUrl { get; private set; }

    public async Task OnGetAsync() => Servers = await ServerList.LoadAsync(db);

    public async Task<IActionResult> OnPostAsync()
    {
        Servers = await ServerList.LoadAsync(db);
        if (!ModelState.IsValid) return Page();
        var t = new Tenant
        {
            Slug = Input.Slug, LegalName = Input.LegalName, Inn = Input.Inn, ContactName = Input.ContactName?.Trim(), Phone = Input.Phone?.Trim(),
            Email = Input.Email?.Trim(), TelegramChatId = Input.TelegramChatId?.Trim(), Deployment = DeploymentKind.SaaS, CompanyId = 1,
            ServerId = Input.ServerId, SampleData = Input.SampleData,
        };
        if (!await TryAsync(async () =>
            {
                if (t.ServerId is null)
                    t.ServerId = (await provisioning.PickServerAsync())?.Id ?? throw new DomainException("Bo'sh joyi bor faol server yo'q (Serverlar bo'limi).");
                await tenants.CreateAsync(t, startTrial: true);
            }))
            return Page();
        ProvisioningService.EnqueueResult? r = null;
        if (!await TryAsync(async () => r = await provisioning.EnqueueAsync(t, ProvisioningJobType.Create)))
        {
            FlashError = "Tashkilot va sinov obunasi yaratildi, lekin baza jobi qo'shilmadi: " +
                         string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToPage("Details", new { id = t.Id });
        }
        Created = t;
        Job = r!.Job;
        TempPassword = r.TempPassword;
        TenantUrl = provisioning.TenantUrl(t);
        return Page();
    }
}
